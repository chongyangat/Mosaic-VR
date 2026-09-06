from __future__ import annotations

import os
import shutil
from pathlib import Path

from PIL import Image


PACKAGE_ROOT = Path(
    os.environ.get("MOSAIC_SUBMISSION_ROOT", Path(__file__).resolve().parents[2])
)
SOURCE_ROOT = PACKAGE_ROOT / "03_Figure_Sources_Internal"
SUBMISSION_FIGURES = PACKAGE_ROOT / "02_Figures_Submission"


def convert(figure_folder: str, stem: str) -> None:
    generated = SOURCE_ROOT / figure_folder / "generated_outputs"
    source = generated / f"{stem}_drawio_export.png"
    internal_output = generated / f"{stem}.png"
    submission_output = SUBMISSION_FIGURES / f"{stem}.png"

    with Image.open(source) as image:
        # PNG re-encoding is lossless; pixel dimensions stay unchanged and only
        # the physical-resolution metadata is normalized to 600 dpi.
        image.save(internal_output, format="PNG", dpi=(600, 600), compress_level=6)
    shutil.copyfile(internal_output, submission_output)
    print(internal_output)
    print(submission_output)


def main() -> None:
    convert(
        "Figure_10",
        "Figure_10_representative_full_workflow_supermarket",
    )
    convert(
        "Figure_11",
        "Figure_11_representative_full_workflow_street",
    )


if __name__ == "__main__":
    main()
