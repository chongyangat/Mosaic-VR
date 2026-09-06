from __future__ import annotations

import base64
import html
from pathlib import Path


ASSET_DIR = Path(
    r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\figures\fig_workflow"
)
OUTPUT_DIR = ASSET_DIR / "output"
OUTPUT_FILE = OUTPUT_DIR / "MOSAIC_VR_dual_view_workflow_word_page.drawio"

PAGE_W = 2100
PAGE_H = 3160


def esc(value: str) -> str:
    return html.escape(value, quote=True)


def image_data_uri(path: Path) -> str:
    payload = base64.b64encode(path.read_bytes()).decode("ascii")
    # draw.io's mxGraph image style uses a comma-only PNG data URI.  A
    # ``;base64`` marker would be parsed as a style delimiter and break the
    # embedded image.
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
    parent: str = "1",
) -> None:
    cells.append(
        f'<mxCell id="{esc(cell_id)}" value="{esc(value)}" '
        f'style="{esc(style)}" vertex="1" parent="{esc(parent)}">'
        f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/>'
        '</mxCell>'
    )


def edge(
    cell_id: str,
    source: str,
    target: str,
    style: str,
    points: list[tuple[float, float]] | None = None,
) -> None:
    if points:
        point_xml = "".join(
            f'<mxPoint x="{x}" y="{y}"/>' for x, y in points
        )
        geometry = (
            '<mxGeometry relative="1" as="geometry">'
            f'<Array as="points">{point_xml}</Array>'
            '</mxGeometry>'
        )
    else:
        geometry = '<mxGeometry relative="1" as="geometry"/>'
    cells.append(
        f'<mxCell id="{esc(cell_id)}" value="" style="{esc(style)}" '
        f'edge="1" parent="1" source="{esc(source)}" target="{esc(target)}">'
        f'{geometry}</mxCell>'
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
    align: str = "center",
    bold: bool = False,
) -> None:
    style = (
        "text;html=1;whiteSpace=wrap;overflow=hidden;rounded=0;"
        f"align={align};verticalAlign=middle;fontFamily=Helvetica;"
        f"fontSize={size};fontColor={color};"
        f"fontStyle={1 if bold else 0};strokeColor=none;fillColor=none;"
    )
    vertex(cell_id, value, style, x, y, w, h)


def rounded_box(
    cell_id: str,
    value: str,
    x: float,
    y: float,
    w: float,
    h: float,
    fill: str,
    stroke: str,
    *,
    size: int = 22,
    color: str = "#26364A",
    bold: bool = False,
    dashed: bool = False,
) -> None:
    style = (
        "rounded=1;arcSize=16;whiteSpace=wrap;html=1;"
        f"fillColor={fill};strokeColor={stroke};strokeWidth=2;"
        f"fontFamily=Helvetica;fontSize={size};fontColor={color};"
        f"fontStyle={1 if bold else 0};align=center;verticalAlign=middle;"
        f"dashed={1 if dashed else 0};spacing=10;"
    )
    vertex(cell_id, value, style, x, y, w, h)


def add_screenshot(
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
    caption_w: float | None = None,
) -> tuple[str, str]:
    frame_id = f"{prefix}_frame"
    image_id = f"{prefix}_image"
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
    vertex(image_id, "", image_style, x, y, w, h)
    badge_style = (
        "ellipse;whiteSpace=wrap;html=1;aspect=fixed;"
        f"fillColor={badge_fill};strokeColor=#FFFFFF;strokeWidth=3;"
        "fontFamily=Helvetica;fontSize=19;fontColor=#FFFFFF;fontStyle=1;"
        "align=center;verticalAlign=middle;shadow=1;"
    )
    vertex(f"{prefix}_badge", badge, badge_style, x - 25, y - 25, 58, 58)
    text_box(
        f"{prefix}_caption",
        caption,
        x - 5,
        caption_y,
        caption_w or w + 10,
        48,
        size=21,
        color="#223247",
        bold=True,
    )
    return frame_id, image_id


# Clean paper-friendly background.
vertex(
    "background",
    "",
    "rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=none;",
    0,
    0,
    PAGE_W,
    PAGE_H,
)

# Alternating workflow phases, spanning both viewpoints.
phase_style_1 = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#F7FAFD;"
    "strokeColor=#DCE6F0;strokeWidth=1;"
)
phase_style_2 = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#FBFAFE;"
    "strokeColor=#E3DDF4;strokeWidth=1;"
)
vertex("phase1_bg", "", phase_style_1, 45, 100, 2010, 940)
vertex("phase2_bg", "", phase_style_2, 45, 1050, 2010, 920)
vertex("phase3_bg", "", phase_style_1, 45, 1980, 2010, 500)
vertex("phase4_bg", "", phase_style_2, 45, 2490, 2010, 590)

# Lane outlines and headers.
lane_outline = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=none;"
    "strokeColor=#C8D3DF;strokeWidth=2;"
)
vertex("participant_lane", "", lane_outline, 70, 100, 850, 2980)
vertex("experimenter_lane", "", lane_outline, 1180, 100, 850, 2980)

participant_header = (
    "rounded=1;arcSize=10;whiteSpace=wrap;html=1;fillColor=#0F6CBD;"
    "gradientColor=#2B88D8;gradientDirection=east;strokeColor=#0B5596;"
    "strokeWidth=2;fontFamily=Helvetica;fontSize=27;fontColor=#FFFFFF;"
    "fontStyle=1;align=center;verticalAlign=middle;shadow=1;"
)
experimenter_header = (
    "rounded=1;arcSize=10;whiteSpace=wrap;html=1;fillColor=#5B4BDB;"
    "gradientColor=#7C6FE5;gradientDirection=east;strokeColor=#4435B5;"
    "strokeWidth=2;fontFamily=Helvetica;fontSize=27;fontColor=#FFFFFF;"
    "fontStyle=1;align=center;verticalAlign=middle;shadow=1;"
)
vertex(
    "participant_header",
    "(a) Participant view &nbsp;&middot;&nbsp; first-person",
    participant_header,
    70,
    20,
    850,
    65,
)
vertex(
    "experimenter_header",
    "(b) Experimenter view &nbsp;&middot;&nbsp; third-person",
    experimenter_header,
    1180,
    20,
    850,
    65,
)


def phase_label(cell_id: str, text: str, y: float, fill: str, stroke: str) -> None:
    style = (
        "rounded=1;arcSize=10;whiteSpace=wrap;html=1;"
        f"fillColor={fill};strokeColor={stroke};strokeWidth=1;"
        "fontFamily=Helvetica;fontSize=19;fontColor=#34465A;fontStyle=1;"
        "align=left;verticalAlign=middle;spacingLeft=16;"
    )
    vertex(cell_id, text, style, 78, y, 1944, 48)


phase_label("phase1_label", "PHASE 1 &nbsp;&middot;&nbsp; SESSION PREPARATION", 115, "#EAF3FB", "#B8D2EA")
phase_label("phase2_label", "PHASE 2 &nbsp;&middot;&nbsp; TRIAL INITIALIZATION", 1065, "#F0EDFA", "#CEC6EC")
phase_label("phase3_label", "PHASE 3 &nbsp;&middot;&nbsp; RUNNING TRIAL", 1995, "#EAF3FB", "#B8D2EA")
phase_label("phase4_label", "PHASE 4 &nbsp;&middot;&nbsp; TRIAL CLOSEOUT", 2505, "#F0EDFA", "#CEC6EC")

# Experimenter screenshots (top-to-bottom chronological order).
b1, _ = add_screenshot(
    "b1",
    "thirdperson_step1.png",
    1275,
    190,
    660,
    335,
    "B1",
    "#5B4BDB",
    "Choose data-collection mode",
    535,
)
b2, _ = add_screenshot(
    "b2",
    "thirdperson_step2.png",
    1275,
    600,
    660,
    335,
    "B2",
    "#5B4BDB",
    "Create participant profile and click Confirm",
    945,
)
b3, _ = add_screenshot(
    "b3",
    "thirdperson_step3.png",
    1275,
    1130,
    660,
    335,
    "B3",
    "#5B4BDB",
    "Open scene selection",
    1475,
)
b4, _ = add_screenshot(
    "b4",
    "thirdperson_step4.png",
    1275,
    1550,
    660,
    335,
    "B4",
    "#5B4BDB",
    "Apply scene and trial condition",
    1895,
)
b5, _ = add_screenshot(
    "b5",
    "thirdperson_step5.png",
    1275,
    2050,
    660,
    335,
    "B5",
    "#5B4BDB",
    "Monitor third-person execution",
    2395,
)
b6, _ = add_screenshot(
    "b6",
    "thirdperson_step6.png",
    1275,
    2600,
    660,
    335,
    "B6",
    "#5B4BDB",
    "Review experimenter-side completion summary",
    2945,
)

# Participant screenshots and role-state nodes.
a1, _ = add_screenshot(
    "a1",
    "firstperson_step1.png",
    265,
    320,
    460,
    460,
    "A1",
    "#0F6CBD",
    "Review safety notice and select START",
    800,
)
init_style_fill = "#E6F2FB"
rounded_box(
    "participant_init",
    "<b>Participant view ready</b><br><font color=\"#52667A\">scene + task state</font>",
    220,
    1720,
    550,
    110,
    init_style_fill,
    "#5AA2D6",
    size=22,
)
a2, _ = add_screenshot(
    "a2",
    "firstperson_step2.png",
    165,
    2050,
    660,
    335,
    "A2",
    "#0F6CBD",
    "Perform the task in first-person view",
    2395,
)
a3, _ = add_screenshot(
    "a3",
    "firstperson_step3.png",
    280,
    2590,
    430,
    430,
    "A3",
    "#0F6CBD",
    "Receive first-person completion feedback",
    3035,
)

# Within-lane progression (secondary, intentionally understated).
progress_style_left = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#8AA2B8;strokeWidth=2;"
    "dashed=1;dashPattern=7 5;exitX=0;exitY=0.75;entryX=0;entryY=0.5;"
)
progress_style_right = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#9A93B8;strokeWidth=2;"
    "dashed=1;dashPattern=7 5;exitX=1;exitY=0.5;entryX=1;entryY=0.5;"
)
edge("a1_to_init", a1, "participant_init", progress_style_left, [(105, 780), (105, 1775)])
edge("init_to_a2", "participant_init", a2, progress_style_left, [(105, 1775), (105, 2130)])
edge("a2_to_complete", a2, a3, progress_style_left, [(105, 2210), (105, 2805)])

edge("b1_to_b2", b1, b2, progress_style_right, [(1985, 358), (1985, 768)])
edge("b2_to_b3", b2, b3, progress_style_right, [(1985, 768), (1985, 1298)])
edge("b3_to_b4", b3, b4, progress_style_right, [(1985, 1298), (1985, 1718)])
edge("b4_to_b5", b4, b5, progress_style_right, [(1985, 1718), (1985, 2218)])
edge("b5_to_b6", b5, b6, progress_style_right, [(1985, 2218), (1985, 2768)])

# Cross-view interaction/data flows. Direct role-to-role links are mediated by
# the controller/task/sensing nodes in the central gutter.
control_left_to_box = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
)
control_right_to_box = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=0;exitY=0.55;entryX=1;entryY=0.5;"
)
control_box_to_left = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=0;exitY=0.5;entryX=1;entryY=0.5;"
)
control_box_to_right = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
)
control_top_to_bottom = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#0F6CBD;strokeWidth=4;"
    "exitX=0.5;exitY=1;entryX=0.5;entryY=0;"
)
data_left_to_box = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#7A5CC7;strokeWidth=4;"
    "dashed=1;dashPattern=10 6;exitX=1;exitY=0.35;entryX=0;entryY=0.5;"
)
data_box_to_right = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
    "html=1;endArrow=block;endFill=1;strokeColor=#7A5CC7;strokeWidth=4;"
    "dashed=1;dashPattern=10 6;exitX=1;exitY=0.5;entryX=0;entryY=0.9;"
)

edge("participant_ready_flow", a1, "ready_label", control_left_to_box)
edge("session_condition_flow", b2, "ready_label", control_right_to_box)
edge("start_trial_flow", b4, "start_label", control_right_to_box)
edge("prepare_participant_view_flow", "start_label", "participant_init", control_box_to_left)
edge("task_action_flow", a2, "task_data_label", data_left_to_box)
edge("task_state_flow", "task_data_label", "feedback_label", control_top_to_bottom)
edge("participant_feedback_flow", "feedback_label", a2, control_box_to_left)
edge("experimenter_feedback_flow", "feedback_label", b5, control_box_to_right)
edge("sensing_monitoring_flow", "sensing_label", b5, data_box_to_right)
edge("completion_flow", a3, "completion_label", data_left_to_box)
edge("return_ready_flow", "completion_label", b6, control_box_to_right)

# Labels sit in the central interaction gutter so arrows remain visually clear.
rounded_box(
    "ready_label",
    "<b>Experiment Controller</b><br>session · ready · condition<br><font color=\"#52667A\">Fig. 2 · 1–3</font>",
    865,
    620,
    350,
    110,
    "#FFFFFF",
    "#70A9D3",
    size=18,
    color="#245A85",
)
rounded_box(
    "start_label",
    "<b>Experiment Controller</b><br>start · load · views · sensing<br><font color=\"#52667A\">Fig. 2 · 4–7</font>",
    865,
    1720,
    350,
    110,
    "#FFFFFF",
    "#70A9D3",
    size=18,
    color="#245A85",
)
rounded_box(
    "task_data_label",
    "<b>Task Management</b><br>actions → shared task state<br><font color=\"#6B568F\">Fig. 2 · 8–9</font>",
    865,
    2070,
    350,
    90,
    "#FFFFFF",
    "#A591D8",
    size=18,
    color="#5D4499",
    dashed=True,
)
rounded_box(
    "feedback_label",
    "<b>Experiment Controller</b><br>state → role-specific views",
    865,
    2205,
    350,
    75,
    "#FFFFFF",
    "#70A9D3",
    size=18,
    color="#245A85",
)
rounded_box(
    "sensing_label",
    "<b>Multimodal Sensing</b><br>gaze + body pose → monitoring<br><font color=\"#6B568F\">Fig. 2 · 10</font>",
    865,
    2320,
    350,
    80,
    "#FFFFFF",
    "#A591D8",
    size=17,
    color="#5D4499",
    dashed=True,
)
rounded_box(
    "completion_label",
    "<b>Experiment Controller</b><br>stop · flush · return ready<br><font color=\"#52667A\">Fig. 2 · 11–12</font>",
    865,
    2750,
    350,
    110,
    "#FFFFFF",
    "#70A9D3",
    size=18,
    color="#245A85",
)

# Compact legend; no decorative arrows beyond the data semantics represented above.
vertex(
    "legend_control_line",
    "",
    "rounded=0;html=1;fillColor=#0F6CBD;strokeColor=#0F6CBD;strokeWidth=1;",
    105,
    3120,
    85,
    6,
)
text_box(
    "legend_control_text",
    "Control / task-state flow",
    205,
    3098,
    330,
    48,
    size=17,
    color="#4A5D70",
    align="left",
)
vertex(
    "legend_data_line_1",
    "",
    "rounded=0;html=1;fillColor=#7A5CC7;strokeColor=#7A5CC7;strokeWidth=1;",
    620,
    3120,
    32,
    6,
)
vertex(
    "legend_data_line_2",
    "",
    "rounded=0;html=1;fillColor=#7A5CC7;strokeColor=#7A5CC7;strokeWidth=1;",
    667,
    3120,
    32,
    6,
)
text_box(
    "legend_data_text",
    "Behavior / gaze / outcome data",
    715,
    3098,
    425,
    48,
    size=17,
    color="#4A5D70",
    align="left",
)
text_box(
    "legend_record_text",
    "Traceable records: session &middot; trial &middot; task &middot; event &middot; timestamps",
    1185,
    3098,
    800,
    48,
    size=17,
    color="#4A5D70",
    align="right",
)

xml = f'''<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="Electron" agent="Codex" version="26.0.16">
  <diagram id="mosaic-vr-workflow" name="Workflow">
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
