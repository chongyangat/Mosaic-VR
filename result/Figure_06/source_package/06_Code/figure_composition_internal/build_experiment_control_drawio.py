from __future__ import annotations

import base64
import html
from pathlib import Path


ASSET_DIR = Path(
    r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\figures\fig_experiment_control"
)
OUTPUT_DIR = ASSET_DIR / "output"
OUTPUT_FILE = OUTPUT_DIR / "MOSAIC_VR_dual_end_state_interaction.drawio"

PAGE_W = 2100
PAGE_H = 1550


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


def edge(cell_id: str, source: str, target: str, style: str) -> None:
    cells.append(
        f'<mxCell id="{esc(cell_id)}" value="" style="{esc(style)}" '
        f'edge="1" parent="1" source="{esc(source)}" target="{esc(target)}">'
        '<mxGeometry relative="1" as="geometry"/>'
        '</mxCell>'
    )


def text_box(
    cell_id: str,
    value: str,
    x: float,
    y: float,
    w: float,
    h: float,
    *,
    size: int = 22,
    color: str = "#26364A",
    bold: bool = False,
    align: str = "center",
) -> None:
    style = (
        "text;html=1;whiteSpace=wrap;overflow=hidden;rounded=0;"
        f"align={align};verticalAlign=middle;fontFamily=Helvetica;"
        f"fontSize={size};fontColor={color};"
        f"fontStyle={1 if bold else 0};strokeColor=none;fillColor=none;"
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
    caption_y: float,
) -> str:
    frame_id = f"{prefix}_frame"
    frame_style = (
        "rounded=1;arcSize=5;whiteSpace=wrap;html=1;fillColor=#FFFFFF;"
        "strokeColor=#B8C6D6;strokeWidth=2;shadow=1;"
    )
    vertex(frame_id, "", frame_style, x - 10, y - 10, w + 20, h + 20)
    image_style = (
        "shape=image;imageAspect=1;aspect=fixed;html=1;verticalAlign=middle;"
        "labelPosition=center;verticalLabelPosition=middle;strokeColor=none;"
        f"image={image_data_uri(ASSET_DIR / filename)};"
    )
    vertex(f"{prefix}_image", "", image_style, x, y, w, h)
    badge_style = (
        "ellipse;whiteSpace=wrap;html=1;aspect=fixed;"
        f"fillColor={badge_fill};strokeColor=#FFFFFF;strokeWidth=3;"
        "fontFamily=Helvetica;fontSize=20;fontColor=#FFFFFF;fontStyle=1;"
        "align=center;verticalAlign=middle;shadow=1;"
    )
    vertex(f"{prefix}_badge", badge, badge_style, x - 25, y - 25, 60, 60)
    text_box(
        f"{prefix}_caption",
        caption,
        x - 10,
        caption_y,
        w + 20,
        50,
        size=21,
        color="#223247",
        bold=True,
    )
    return frame_id


# Paper-friendly background and paired use-case bands.
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
    "use_case_1_bg",
    "",
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#F7FAFD;"
    "strokeColor=#DCE6F0;strokeWidth=1;",
    35,
    100,
    2030,
    570,
)
vertex(
    "use_case_2_bg",
    "",
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#FBFAFE;"
    "strokeColor=#E3DDF4;strokeWidth=1;",
    35,
    800,
    2030,
    720,
)

header_1_style = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#EAF3FB;"
    "strokeColor=#B7D1E7;strokeWidth=2;fontFamily=Helvetica;fontSize=23;"
    "fontColor=#245A85;fontStyle=1;align=left;verticalAlign=middle;spacingLeft=22;"
)
header_2_style = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#F0EDFA;"
    "strokeColor=#CFC6EE;strokeWidth=2;fontFamily=Helvetica;fontSize=23;"
    "fontColor=#5D4499;fontStyle=1;align=left;verticalAlign=middle;spacingLeft=22;"
)
vertex(
    "use_case_1_header",
    "USE CASE 1 &nbsp;&middot;&nbsp; Experimenter control &rarr; synchronized VR lighting",
    header_1_style,
    60,
    30,
    1980,
    55,
)
vertex(
    "use_case_2_header",
    "USE CASE 2 &nbsp;&middot;&nbsp; Participant action &rarr; synchronized experimenter monitoring",
    header_2_style,
    60,
    730,
    1980,
    55,
)

# Use case 1: two synchronized illumination states.
a_frame = add_panel(
    "panel_a",
    "light_intensity_low.png",
    60,
    130,
    900,
    456,
    "(a)",
    "#5B4BDB",
    "Low light-intensity setting",
    605,
)
b_frame = add_panel(
    "panel_b",
    "light_intensity_high.png",
    1140,
    130,
    900,
    455,
    "(b)",
    "#0F6CBD",
    "High light-intensity setting",
    605,
)

# Use case 2: participant action and mirrored experimenter-side task state.
c_frame = add_panel(
    "panel_c",
    "product_update_firstperson_view.png",
    90,
    855,
    600,
    600,
    "(c)",
    "#0F6CBD",
    "Participant retrieves a target product",
    1470,
)
d_frame = add_panel(
    "panel_d",
    "product_update_thirdperson_view.png",
    840,
    850,
    1200,
    609,
    "(d)",
    "#5B4BDB",
    "Experimenter view updates task progress",
    1470,
)

control_style = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
)
state_style = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#7A5CC7;strokeWidth=4;"
    "dashed=1;dashPattern=10 6;exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
)
edge("lighting_sync_flow", a_frame, b_frame, control_style)
edge("task_state_sync_flow", c_frame, d_frame, state_style)

# Compact labels in the routing corridor keep the communication semantics clear.
text_box(
    "lighting_flow_label",
    "lighting<br>command",
    980,
    285,
    140,
    70,
    size=17,
    color="#0F6CBD",
    bold=True,
)
text_box(
    "task_flow_label",
    "task-state<br>update",
    700,
    1085,
    130,
    70,
    size=17,
    color="#6A4EB2",
    bold=True,
)

xml = f'''<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="Electron" agent="Codex" version="26.0.16">
  <diagram id="mosaic-vr-dual-end-interaction" name="Dual-end interaction">
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
