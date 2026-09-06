from __future__ import annotations

import os
import xml.etree.ElementTree as ET


OUTPUT_DIR = r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\figures\publication_ready_code_aligned"


PALETTE = {
    "blue_fill": "#E9F1FB",
    "blue_stroke": "#3E6B99",
    "green_fill": "#EAF4EA",
    "green_stroke": "#5E8C61",
    "purple_fill": "#F0EAF5",
    "purple_stroke": "#8064A2",
    "amber_fill": "#FFF4D8",
    "amber_stroke": "#B9851D",
    "red_fill": "#FBEAEA",
    "red_stroke": "#B85C5C",
    "grey_fill": "#F4F5F7",
    "grey_stroke": "#68707A",
    "ink": "#17212B",
    "muted": "#4F5B66",
    "panel": "#FAFBFC",
}


def style_box(fill: str, stroke: str, font_size: int = 28, bold: bool = False,
              rounded: int = 1, align: str = "center", vertical: str = "middle",
              dashed: bool = False, stroke_width: float = 1.8) -> str:
    font_size = max(17, round(font_size * 0.72))
    parts = [
        f"rounded={rounded}",
        "whiteSpace=wrap",
        "html=1",
        f"fillColor={fill}",
        f"strokeColor={stroke}",
        f"strokeWidth={stroke_width}",
        f"fontColor={PALETTE['ink']}",
        "fontFamily=Arial",
        f"fontSize={font_size}",
        f"fontStyle={1 if bold else 0}",
        f"align={align}",
        f"verticalAlign={vertical}",
        "spacing=8",
    ]
    if dashed:
        parts.append("dashed=1")
    return ";".join(parts) + ";"


def style_text(font_size: int = 28, bold: bool = False, align: str = "center",
               color: str | None = None) -> str:
    font_size = max(17, round(font_size * 0.72))
    return (
        "text;html=1;strokeColor=none;fillColor=none;whiteSpace=wrap;"
        f"fontFamily=Arial;fontSize={font_size};fontStyle={1 if bold else 0};"
        f"fontColor={color or PALETTE['ink']};align={align};verticalAlign=middle;spacing=4;"
    )


def style_edge(color: str = "#34495E", font_size: int = 24, both: bool = False,
               dashed: bool = False, width: float = 2.0) -> str:
    font_size = max(15, round(font_size * 0.68))
    parts = [
        "edgeStyle=orthogonalEdgeStyle",
        "rounded=1",
        "orthogonalLoop=1",
        "jettySize=auto",
        "html=1",
        f"strokeColor={color}",
        f"strokeWidth={width}",
        "endArrow=block",
        "endFill=1",
        f"fontFamily=Arial",
        f"fontSize={font_size}",
        f"fontColor={PALETTE['ink']}",
        "labelBackgroundColor=#FFFFFF",
    ]
    if both:
        parts.extend(["startArrow=block", "startFill=1"])
    if dashed:
        parts.append("dashed=1")
    return ";".join(parts) + ";"


class Diagram:
    def __init__(self, diagram_id: str, name: str, width: int, height: int):
        self.mxfile = ET.Element(
            "mxfile",
            {
                "host": "app.diagrams.net",
                "modified": "2026-08-17T08:00:00.000Z",
                "agent": "OpenAI Codex",
                "version": "24.7.17",
                "type": "device",
            },
        )
        diagram = ET.SubElement(self.mxfile, "diagram", {"id": diagram_id, "name": name})
        self.model = ET.SubElement(
            diagram,
            "mxGraphModel",
            {
                "dx": "1200",
                "dy": "900",
                "grid": "1",
                "gridSize": "10",
                "guides": "1",
                "tooltips": "1",
                "connect": "1",
                "arrows": "1",
                "fold": "1",
                "page": "1",
                "pageScale": "1",
                "pageWidth": str(width),
                "pageHeight": str(height),
                "math": "0",
                "shadow": "0",
            },
        )
        self.root = ET.SubElement(self.model, "root")
        ET.SubElement(self.root, "mxCell", {"id": "0"})
        ET.SubElement(self.root, "mxCell", {"id": "1", "parent": "0"})

    def box(self, cid: str, value: str, x: int, y: int, w: int, h: int, style: str,
            parent: str = "1") -> str:
        cell = ET.SubElement(
            self.root,
            "mxCell",
            {"id": cid, "value": value, "style": style, "vertex": "1", "parent": parent},
        )
        ET.SubElement(
            cell,
            "mxGeometry",
            {"x": str(x), "y": str(y), "width": str(w), "height": str(h), "as": "geometry"},
        )
        return cid

    def edge(self, cid: str, source: str, target: str, value: str = "", style: str | None = None,
             points: list[tuple[int, int]] | None = None) -> str:
        cell = ET.SubElement(
            self.root,
            "mxCell",
            {
                "id": cid,
                "value": value,
                "style": style or style_edge(),
                "edge": "1",
                "parent": "1",
                "source": source,
                "target": target,
            },
        )
        geo = ET.SubElement(cell, "mxGeometry", {"relative": "1", "as": "geometry"})
        if points:
            arr = ET.SubElement(geo, "Array", {"as": "points"})
            for x, y in points:
                ET.SubElement(arr, "mxPoint", {"x": str(x), "y": str(y)})
        return cid

    def save(self, filename: str):
        os.makedirs(OUTPUT_DIR, exist_ok=True)
        ET.indent(self.mxfile, space="  ")
        path = os.path.join(OUTPUT_DIR, filename)
        ET.ElementTree(self.mxfile).write(path, encoding="UTF-8", xml_declaration=True)
        return path


def panel(diagram: Diagram, prefix: str, label: str, x: int, y: int, w: int, h: int):
    diagram.box(
        prefix + "_body",
        "",
        x,
        y + 60,
        w,
        h - 60,
        style_box(PALETTE["panel"], PALETTE["blue_stroke"], 28, rounded=1, stroke_width=1.6),
    )
    diagram.box(
        prefix + "_head",
        label,
        x,
        y,
        w,
        50,
        style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 30, bold=True, rounded=1),
    )


def build_figure1():
    d = Diagram("fig1CodeAligned", "Figure 1", 1100, 650)
    panel(d, "p1a", "(a) Endpoints", 20, 20, 330, 610)
    panel(d, "p1b", "(b) Runtime coordination", 385, 20, 330, 610)
    panel(d, "p1c", "(c) Reuse and records", 750, 20, 330, 610)

    d.box("f1_pc", "Experimenter PC\nmanager / host", 45, 115, 135, 90,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 27, bold=True))
    d.box("f1_quest", "Participant\nMeta Quest Pro", 195, 115, 130, 90,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 27, bold=True))
    d.box("f1_mocap", "Nokov MoCap", 115, 390, 145, 85,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 27, bold=True))
    d.edge("f1_e1", "f1_pc", "f1_quest", "",
           style_edge(PALETTE["blue_stroke"], 23, both=True, width=2.2))
    d.edge("f1_e2", "f1_mocap", "f1_pc", "", style_edge(PALETTE["grey_stroke"], 22))
    d.edge("f1_e3", "f1_mocap", "f1_quest", "", style_edge(PALETTE["grey_stroke"], 22))
    d.box("f1_a_note", "Quest -> PC: gaze UDP\nMirror: task control + replicated state", 55, 500, 260, 85,
          style_box("#FFFFFF", PALETTE["green_stroke"], 24, rounded=1, dashed=True, stroke_width=1.4))

    d.box("f1_ui", "Experimenter UI\nparticipant ID | scene | record", 420, 105, 260, 80,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 26, bold=True))
    d.box("f1_main", "MainLogicSystem\nscene | spawn | teleport | cleanup", 420, 225, 260, 90,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 26, bold=True))
    d.box("f1_task", "TaskSystem + TaskList\nMirror SyncList: progress | status", 420, 355, 260, 90,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 26, bold=True))
    d.box("f1_rec", "SchemeSys + recorders\nmanual record | link metrics", 420, 485, 260, 90,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 26, bold=True))
    d.edge("f1_e4", "f1_ui", "f1_main", "trial control", style_edge(PALETTE["red_stroke"], 23))
    d.edge("f1_e5", "f1_main", "f1_task", "initialize / destroy", style_edge(PALETTE["blue_stroke"], 23))
    d.edge("f1_e6", "f1_ui", "f1_rec", "",
           style_edge(PALETTE["amber_stroke"], 22, dashed=True), points=[(700, 190), (700, 530)])

    d.box("f1_cfg1", "Supermarket config\nproducts | task groups", 780, 105, 270, 80,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 26, bold=True))
    d.box("f1_cfg2", "Street config\ntargets | obstacles", 780, 215, 270, 80,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 26, bold=True))
    d.box("f1_common", "Common runtime\nscene lifecycle | task adapters", 780, 335, 270, 90,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 26, bold=True))
    d.box("f1_services", "Shared services\nMirror | gaze UDP | clock sync | MoCap", 780, 465, 270, 80,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 25, bold=True))
    d.box("f1_output", "Session folder\ngaze | performance\nproduct movement (supermarket)", 780, 555, 270, 60,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 22, bold=True))
    d.edge("f1_e7", "f1_cfg1", "f1_common", "", style_edge(PALETTE["purple_stroke"], 22))
    d.edge("f1_e8", "f1_cfg2", "f1_common", "", style_edge(PALETTE["green_stroke"], 22))
    d.edge("f1_e9", "f1_common", "f1_services", "runs on", style_edge(PALETTE["blue_stroke"], 22))
    d.edge("f1_e10", "f1_services", "f1_output", "", style_edge(PALETTE["amber_stroke"], 22))
    return d.save("Figure1_platform_overview_CODE_ALIGNED.drawio")


def stage_band(d: Diagram, cid: str, label: str, y: int, h: int, fill: str, stroke: str):
    d.box(cid, label, 20, y, 135, h, style_box(fill, stroke, 27, bold=True, rounded=1))
    d.box(cid + "_bg", "", 170, y, 910, h,
          style_box("#FFFFFF", "#CBD3DC", 26, rounded=1, stroke_width=1.2))


def build_figure2():
    d = Diagram("fig2CodeAligned", "Figure 2", 1100, 880)
    d.box("f2_h0", "Workflow phase", 20, 20, 135, 55,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 25, bold=True))
    d.box("f2_h1", "Experimenter UI", 180, 20, 275, 55,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 28, bold=True))
    d.box("f2_h2", "Manager / host runtime", 465, 20, 300, 55,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 28, bold=True))
    d.box("f2_h3", "Participant endpoint", 775, 20, 295, 55,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 28, bold=True))

    stage_band(d, "f2_s1", "I\nPreparation", 90, 145, PALETTE["amber_fill"], PALETTE["amber_stroke"])
    stage_band(d, "f2_s2", "II\nInitialization", 245, 220, PALETTE["blue_fill"], PALETTE["blue_stroke"])
    stage_band(d, "f2_s3", "III\nExecution +\nrecording", 475, 225, PALETTE["green_fill"], PALETTE["green_stroke"])
    stage_band(d, "f2_s4", "IV\nShutdown", 710, 145, PALETTE["red_fill"], PALETTE["red_stroke"])

    d.box("f2_prep_ui", "Enter collection mode\nset participant ID", 190, 120, 245, 80,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 26, bold=True))
    d.box("f2_prep_host", "Room server\nconnection + readiness", 490, 120, 250, 80,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 26, bold=True))
    d.box("f2_prep_q", "Connect to host\nset Ready", 800, 120, 240, 80,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 26, bold=True))
    d.edge("f2_e1", "f2_prep_q", "f2_prep_host", "", style_edge(PALETTE["green_stroke"], 22))
    d.edge("f2_e2", "f2_prep_host", "f2_prep_ui", "", style_edge(PALETTE["blue_stroke"], 22))

    d.box("f2_init_ui", "Select Supermarket / Street", 190, 280, 245, 75,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 26, bold=True))
    d.box("f2_init_host1", "SyncScene -> GameplayScene", 490, 270, 250, 75,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 26, bold=True))
    d.box("f2_init_host2", "load scene; spawn TaskList + objects; initialize tasks", 490, 365, 250, 75,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 24, bold=True))
    d.box("f2_init_q", "load task scene; register objects; teleport; start gaze; enable avatar", 800, 315, 240, 105,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 24, bold=True))
    d.edge("f2_e3", "f2_init_ui", "f2_init_host1", "", style_edge(PALETTE["red_stroke"], 22))
    d.edge("f2_e4", "f2_init_host1", "f2_init_host2", "", style_edge(PALETTE["blue_stroke"], 22))
    d.edge("f2_e5", "f2_init_host1", "f2_init_q", "", style_edge(PALETTE["blue_stroke"], 22))

    d.box("f2_run_ui1", "Live supervision\ntask + gaze + avatar", 190, 505, 245, 85,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))
    d.box("f2_run_ui2", "Select folder; Start / Stop Recording\n(manual control)", 190, 610, 245, 65,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 23, bold=True, dashed=True))
    d.box("f2_run_host1", "TaskSystem + Mirror SyncList\nprogress / status", 490, 505, 250, 85,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 25, bold=True))
    d.box("f2_run_host2", "SchemeSys + recorders\nclock-ready gate for directional metrics", 490, 610, 250, 65,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 22, bold=True))
    d.box("f2_run_q", "Task interaction\n+ gaze transmission", 800, 525, 240, 95,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 26, bold=True))
    d.edge("f2_e6", "f2_run_q", "f2_run_host1", "", style_edge(PALETTE["green_stroke"], 22))
    d.edge("f2_e7", "f2_run_host1", "f2_run_ui1", "", style_edge(PALETTE["purple_stroke"], 22))
    d.edge("f2_e8", "f2_run_ui2", "f2_run_host2", "", style_edge(PALETTE["amber_stroke"], 22, dashed=True))

    d.box("f2_stop_ui", "Stop recording first;\nthen Return / Stop Trial", 190, 745, 245, 75,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 24, bold=True))
    d.box("f2_stop_host", "RoomScene; stop gaze; unregister objects; unload scene; destroy TaskList", 490, 735, 250, 95,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 22, bold=True))
    d.box("f2_stop_q", "Return to lobby\nand Ready state", 800, 745, 240, 75,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 26, bold=True))
    d.edge("f2_e9", "f2_stop_ui", "f2_stop_host", "", style_edge(PALETTE["red_stroke"], 22))
    d.edge("f2_e10", "f2_stop_host", "f2_stop_q", "", style_edge(PALETTE["blue_stroke"], 22))
    return d.save("Figure2_lifecycle_sequence_CODE_ALIGNED.drawio")


def build_figure3():
    d = Diagram("fig3CodeAligned", "Figure 3", 1100, 850)
    panel(d, "f3a", "(a) Acquisition and live observability", 20, 20, 1060, 430)
    panel(d, "f3b", "(b) Recording and time-aware link monitoring", 20, 470, 1060, 360)

    d.box("f3_src_h", "Source", 50, 105, 215, 45,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 25, bold=True))
    d.box("f3_path_h", "Runtime path", 315, 105, 380, 45,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 25, bold=True))
    d.box("f3_obs_h", "Experimenter-side use", 745, 105, 305, 45,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))

    d.box("f3_task_src", "Participant interaction", 50, 170, 215, 65,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 25, bold=True))
    d.box("f3_task_path", "TaskSystem -> Mirror TaskList\nSyncList progress / status", 315, 170, 380, 65,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 24, bold=True))
    d.box("f3_task_obs", "Replicated task UI\nstate and outcome", 745, 170, 305, 65,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))

    d.box("f3_gaze_src", "Quest eye state", 50, 255, 215, 65,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 25, bold=True))
    d.box("f3_gaze_path", "GazeDataSender -> UDP -> GazeDataReceiver\nsequence, validity, send time, arrival time", 315, 255, 380, 65,
          style_box(PALETTE["green_fill"], PALETTE["green_stroke"], 22, bold=True))
    d.box("f3_gaze_obs", "Gaze overlay + CSV\nlink-quality metrics", 745, 255, 305, 65,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))

    d.box("f3_mocap_src", "Nokov MoCap frames", 50, 340, 215, 65,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 25, bold=True))
    d.box("f3_mocap_path", "StreamingClient\nframe sequence, timestamp, fLatency", 315, 340, 380, 65,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 24, bold=True))
    d.box("f3_mocap_obs", "Avatar update\n+ link-quality metrics", 745, 340, 305, 65,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))
    for idx, (src, dst, color) in enumerate([
        ("f3_task_src", "f3_task_path", PALETTE["purple_stroke"]),
        ("f3_task_path", "f3_task_obs", PALETTE["purple_stroke"]),
        ("f3_gaze_src", "f3_gaze_path", PALETTE["green_stroke"]),
        ("f3_gaze_path", "f3_gaze_obs", PALETTE["green_stroke"]),
        ("f3_mocap_src", "f3_mocap_path", PALETTE["grey_stroke"]),
        ("f3_mocap_path", "f3_mocap_obs", PALETTE["grey_stroke"]),
    ], 1):
        d.edge(f"f3_e{idx}", src, dst, "", style_edge(color, 22))

    d.box("f3_record", "Operator: Start / Stop Recording", 50, 550, 270, 75,
          style_box(PALETTE["red_fill"], PALETTE["red_stroke"], 25, bold=True))
    d.box("f3_folder", "SchemeSys\ntimestamped session folder", 410, 550, 280, 75,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 25, bold=True))
    d.box("f3_clock", "UDPClockSync\n4 timestamps -> low-RTT offset, RTT, uncertainty\nReady gate for directional metrics", 50, 650, 355, 95,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 22, bold=True))
    d.box("f3_files", "Recorded outputs\nEye / gaze CSV + scheme manifest\nNetworkPerformance CSV\nProductMovement CSV (supermarket)", 760, 540, 290, 155,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 23, bold=True))
    d.edge("f3_e7", "f3_record", "f3_folder", "", style_edge(PALETTE["red_stroke"], 22))
    d.edge("f3_e8", "f3_folder", "f3_files", "", style_edge(PALETTE["amber_stroke"], 22))
    d.edge("f3_e9", "f3_clock", "f3_files", "",
           style_edge(PALETTE["blue_stroke"], 21, dashed=True), points=[(600, 760), (900, 760)])
    d.box(
        "f3_boundary",
        "Implementation boundary: records share a timestamped folder and recorded time fields; the code does not create a unified cross-modal event table or validate physical-event synchrony.",
        430,
        720,
        620,
        75,
        style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 21, rounded=1, dashed=True, stroke_width=1.4),
    )
    return d.save("Figure3_multimodal_acquisition_observability_CODE_ALIGNED.drawio")


if __name__ == "__main__":
    for output in (build_figure1(), build_figure2(), build_figure3()):
        print(output)
