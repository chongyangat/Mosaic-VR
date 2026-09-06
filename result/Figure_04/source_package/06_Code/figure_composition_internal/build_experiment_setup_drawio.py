from __future__ import annotations

import base64
import html
from pathlib import Path


ASSET_DIR = Path(
    r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\figures\fig_expriment_setup"
)
OUTPUT_DIR = ASSET_DIR / "output"
OUTPUT_FILE = OUTPUT_DIR / "MOSAIC_VR_experiment_setup_and_software.drawio"

PAGE_W = 2100
PAGE_H = 690


def esc(value: str) -> str:
    return html.escape(value, quote=True)


def image_data_uri(path: Path) -> str:
    payload = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:image/png,{payload}"


cells: list[str] = [
    '<mxCell id="0"/>',
    '<mxCell id="1" parent="0"/>',
]


def vertex(
    cell_id: str,
    value: str,
    style: str,
    x: float,
    y: float,
    w: float,
    h: float,
) -> None:
    cells.append(
        f'<mxCell id="{esc(cell_id)}" value="{esc(value)}" '
        f'style="{esc(style)}" vertex="1" parent="1">'
        f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/>'
        '</mxCell>'
    )


def text_box(
    cell_id: str,
    value: str,
    x: float,
    y: float,
    w: float,
    h: float,
) -> None:
    style = (
        "text;html=1;whiteSpace=wrap;overflow=hidden;rounded=0;"
        "align=center;verticalAlign=middle;fontFamily=Helvetica;"
        "fontSize=23;fontColor=#223247;fontStyle=1;"
        "strokeColor=none;fillColor=none;"
    )
    vertex(cell_id, value, style, x, y, w, h)


def add_panel(
    prefix: str,
    filename: str,
    x: float,
    y: float,
    w: float,
    h: float,
    badge: str,
    badge_fill: str,
    caption: str,
) -> None:
    frame_style = (
        "rounded=1;arcSize=5;whiteSpace=wrap;html=1;fillColor=#FFFFFF;"
        "strokeColor=#B8C6D6;strokeWidth=2;shadow=1;"
    )
    vertex(f"{prefix}_frame", "", frame_style, x - 10, y - 10, w + 20, h + 20)
    image_style = (
        "shape=image;imageAspect=1;aspect=fixed;html=1;verticalAlign=middle;"
        "labelPosition=center;verticalLabelPosition=middle;strokeColor=none;"
        f"image={image_data_uri(ASSET_DIR / filename)};"
    )
    vertex(f"{prefix}_image", "", image_style, x, y, w, h)
    badge_style = (
        "ellipse;whiteSpace=wrap;html=1;aspect=fixed;"
        f"fillColor={badge_fill};strokeColor=#FFFFFF;strokeWidth=3;"
        "fontFamily=Helvetica;fontSize=21;fontColor=#FFFFFF;fontStyle=1;"
        "align=center;verticalAlign=middle;shadow=1;"
    )
    vertex(f"{prefix}_badge", badge, badge_style, x - 25, y - 25, 62, 62)
    text_box(f"{prefix}_caption", caption, x - 10, 615, w + 20, 50)


vertex(
    "background",
    "",
    "rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=none;",
    0,
    0,
    PAGE_W,
    PAGE_H,
)
vertex(
    "panel_band",
    "",
    "rounded=1;arcSize=5;whiteSpace=wrap;html=1;fillColor=#FAFBFC;"
    "strokeColor=#E0E6EC;strokeWidth=1;",
    20,
    30,
    2060,
    640,
)

# Equal display height preserves visual balance while retaining each source ratio.
add_panel(
    "panel_a",
    "VID_20260814_191953_t05.000s.png",
    40,
    70,
    924,
    520,
    "(a)",
    "#0F6CBD",
    "Physical experimental setup",
)
add_panel(
    "panel_b",
    "thirdperson_step5.png",
    1035,
    70,
    1025,
    520,
    "(b)",
    "#5B4BDB",
    "Experimenter-side software interface",
)

xml = f'''<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="Electron" agent="Codex" version="26.0.16">
  <diagram id="mosaic-vr-experiment-setup" name="Experiment setup">
    <mxGraphModel dx="1422" dy="794" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{PAGE_W}" pageHeight="{PAGE_H}" math="0" shadow="0" background="#FFFFFF">
      <root>
        {''.join(cells)}
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
'''

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
OUTPUT_FILE.write_text(xml, encoding="utf-8")
print(OUTPUT_FILE)
print(f"size_bytes={OUTPUT_FILE.stat().st_size}")
