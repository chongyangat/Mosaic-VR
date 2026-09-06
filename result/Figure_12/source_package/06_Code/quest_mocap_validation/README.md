# Quest–MoCap spatiotemporal validation

This folder reproduces the temporal-alignment and held-out spatial-registration results reported in Sections 3.7.4 and 4.3 of the manuscript.

## Run

From this directory, use Python 3.13 (or a compatible Python 3 version):

```powershell
python -m pip install -r requirements.txt
python analyze_update_timestamp.py
python make_mdpi_figures.py
```

The scripts resolve all inputs and outputs relative to the `submission` root; no machine-specific data path is required.

## Analysis protocol

- The MoCap stream is read from the longest continuous block with valid timestamps and poses. Translation is converted from millimetres to metres, and the native frame is mapped with `M = diag(-1, 1, 1)` using `p_U = M p_M / 1000` and `R_U = M R_M M`.
- The saved Quest pose is the RenderPredicted CenterEye pose. Its pose-expression time is mapped to the PC clock as `PredictedDisplayTimeProxy_QuestUnix - ClockDiff(Quest-PC)`. RawPose was unavailable in this recording.
- Ready/render-valid Quest samples are retained when observation delay is at most 60 ms, prediction horizon is at most 70 ms, and clock-offset uncertainty is at most 3 ms. Tracking-origin discontinuities and gaps split the stream; the longest valid block is used.
- Translation-speed magnitude and SO(3) angular-speed magnitude are interpolated to a 2-ms grid, smoothed with a third-order 80-ms Savitzky–Golay window, standardized separately, and combined with equal weight. This construction is invariant to constant translation, fixed axis rotation/reflection, quaternion sign, and coordinate scale.
- Lag is searched from -80 to +80 ms in 0.1-ms increments in nine overlapping 12-s windows. The primary lag is the median of the nine window estimates; the highest-correlation window is used only for visualization.
- The first 60% of the common continuous interval is used for time estimation and robust `AX = YB` hand–eye registration. The fixed transform is evaluated on the disjoint final 40% at 30 Hz.
- Position error is Euclidean distance; rotation error is the SO(3) geodesic angle. Uncertainty intervals use 600 one-second block-bootstrap resamples.

Lag is defined by comparing `MoCap(t)` with `Quest(t + lag)`. A negative lag therefore means that the matching Quest sample carries an earlier mapped PC timestamp; aligning the records requires shifting Quest timestamps later by the lag magnitude.

## Main reproduced results

- Residual inter-system temporal offset: **-6.980 ms**; representative-window combined correlation: **0.98750**.
- Nine-window stability: mean **-7.206 ms**, SD **1.353 ms**, range **-8.832 to -5.228 ms**.
- Held-out position agreement: RMSE **6.620 mm**, P95 **9.998 mm**.
- Held-out rotation agreement: RMSE **1.157 degrees**, P95 **2.488 degrees**.

The window SD and leave-block dispersion describe within-recording stability, not hardware-level clock accuracy. The spatial values are inter-system agreement after a frozen registration with MoCap treated as the reference chain; they are not third-standard absolute metrology.

## Outputs

- `05_Data/quest_mocap_validation/processed/analysis_results.json`: complete parameters, matrices, quality audit, metrics, and limitations.
- `05_Data/quest_mocap_validation/processed/cross_correlation_windows.csv`: all lag-window estimates.
- `05_Data/quest_mocap_validation/processed/aligned_held_out_validation.csv`: held-out aligned poses and per-sample errors.
- `05_Data/quest_mocap_validation/processed/Table_quest_mocap_validation_metrics.csv`: manuscript-table values.
- `02_Figures_Submission/Figure_12_quest_mocap_temporal_alignment.png` and `Figure_13_quest_mocap_spatial_validation.png`: 600-dpi RGB composite figures.
- Editable SVG versions are under `03_Figure_Sources_Internal/Figure_12` and `Figure_13`.
