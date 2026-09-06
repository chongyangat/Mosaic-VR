# Data for Quest–MoCap validation

## Raw

- `raw/update_timestamp1-Tracker0.csv` — optical motion-capture rigid-body trajectory; 6112 rows declared at 90 Hz, millimetre translation units.
- `raw/QuestPose_20260829_173101.csv` — Quest RenderPredicted CenterEye trajectory, clock-mapping diagnostics, validity flags, and timing-quality fields.

SHA-256:

- `update_timestamp1-Tracker0.csv`: `1B1C7ECC62640379E2E4F30361B406621CDE24E6BBFAD09F2BDE9040B7F12539`
- `QuestPose_20260829_173101.csv`: `2596B52B13FF62C545861BF682098EF07EA35AADCA8962BD62CF57C8921E64A5`

The duplicate MoCap CSV formerly stored beside the Quest file is byte-identical and is intentionally not duplicated here.

## Processed

`analysis_results.json` is the authoritative machine-readable result. The aligned validation table contains both transformed trajectories, quaternions in `(x, y, z, w)` order, signed axis residuals, Euclidean position error, and SO(3) geodesic rotation error.

These files contain device and experiment timestamps. Review the journal's data-sharing and de-identification policy before public upload.
