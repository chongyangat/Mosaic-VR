# Reproducing Figures 7–11

Run the commands below from the `submission` root. The source CSV channel value
`PC<->VR 状态同步` is intentionally preserved; the plotting layer maps it to the
manuscript term `PC–VR shared-state RTT`.

## Environment

- Python 3.13
- Packages listed in `requirements_figures_07_11.txt`
- draw.io Desktop 31.3.2 for the Figure 10–11 composite export
- Arial (the scripts fall back to the Matplotlib font configuration if the
  Windows Arial file is unavailable)

## Figures 7–9 and Figure S1

```powershell
python .\06_Code\analysis_upload_candidate\plot_latency_stability_10participants_two_scenes.py
```

The script reads `05_Data/raw_pseudonymized/baseline` and writes the plots,
source tables, audit tables, and input hashes to `05_Data/reproduced_output`.
The Figure 7–9 upload PNG files are also copied to
`02_Figures_Submission` by the script.

## Figure 10 panel source

```powershell
$env:MOSAIC_NETWORK_CSV = "$PWD\05_Data\raw_pseudonymized\integrated\supermarket\NetworkPerformance.csv"
$env:MOSAIC_FIGURE_OUTPUT_DIR = "$PWD\03_Figure_Sources_Internal\Figure_10\generated_outputs"
python .\06_Code\analysis_upload_candidate\integrated_runs\plot_network_applied_sciences_supermarket.py
```

## Figure 11 panel source

```powershell
$env:MOSAIC_NETWORK_CSV = "$PWD\05_Data\raw_pseudonymized\integrated\street\NetworkPerformance.csv"
$env:MOSAIC_FIGURE_OUTPUT_DIR = "$PWD\03_Figure_Sources_Internal\Figure_11\generated_outputs"
python .\06_Code\analysis_upload_candidate\integrated_runs\plot_network_applied_sciences.py
```

## Editable composites and final PNG files

First rebuild the package-relative Draw.io sources:

```powershell
python .\06_Code\figure_composition_internal\build_high_load_figures.py
```

Export each Draw.io file at scale 2.4 without raster resampling:

```powershell
& "C:\Program Files\draw.io\draw.io.exe" -x -f png -s 2.4 `
  -o .\03_Figure_Sources_Internal\Figure_10\generated_outputs\Figure_10_representative_full_workflow_supermarket_drawio_export.png `
  .\03_Figure_Sources_Internal\Figure_10\Figure_10_representative_full_workflow_supermarket.drawio

& "C:\Program Files\draw.io\draw.io.exe" -x -f png -s 2.4 `
  -o .\03_Figure_Sources_Internal\Figure_11\generated_outputs\Figure_11_representative_full_workflow_street_drawio_export.png `
  .\03_Figure_Sources_Internal\Figure_11\Figure_11_representative_full_workflow_street.drawio
```

Finally set lossless 600-dpi PNG metadata and copy the two composites to the
submission-figure folder:

```powershell
python .\06_Code\figure_composition_internal\prepare_manuscript_pngs.py
```

Use `00_README/FIGURE_QA.csv`, `00_README/FILE_MANIFEST.csv`, and
`00_README/SHA256SUMS.txt` to verify the authoritative outputs.

Run the package integrity check after refreshing the manifests:

```powershell
python .\06_Code\verify_figures_07_11_package.py
```
