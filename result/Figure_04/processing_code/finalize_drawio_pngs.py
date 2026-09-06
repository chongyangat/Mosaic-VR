from __future__ import annotations

from pathlib import Path

from PIL import Image


PAIRS = [
    (
        Path(r"D:\vrcontent\MOSAIC-VR\_drawio_export_test\Figure_01_platform_overview_submission_fix_s3.png"),
        Path(r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\submission\02_Figures_Submission\Figure_01_platform_overview.png"),
        2,
    ),
    (
        Path(r"D:\vrcontent\MOSAIC-VR\_drawio_export_test\Figure_04_experimental_setup_monitoring_interface_submission_fix.png"),
        Path(r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\submission\02_Figures_Submission\Figure_04_experimental_setup_monitoring_interface.png"),
        1,
    ),
]


for source, destination, scale in PAIRS:
    with Image.open(source) as image:
        if scale != 1:
            image = image.resize(
                (image.width * scale, image.height * scale),
                Image.Resampling.LANCZOS,
            )
        image.save(destination, format="PNG", dpi=(600, 600), optimize=False)
    with Image.open(destination) as checked:
        print(destination)
        print(f"  pixels={checked.size}; dpi={checked.info.get('dpi')}; mode={checked.mode}")
