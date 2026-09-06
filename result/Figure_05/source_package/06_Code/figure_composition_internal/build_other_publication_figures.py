from __future__ import annotations

import base64
import importlib.util
from pathlib import Path


HELPER_PATH = Path(r"D:\vrcontent\MOSAIC-VR\.codex-work\build_code_aligned_figures.py")
spec = importlib.util.spec_from_file_location("figure_helper", HELPER_PATH)
helper = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(helper)

OUTPUT_DIR = Path(
    r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\figures\publication_ready_code_aligned"
)
ASSET_DIR = Path(r"D:\vrcontent\MOSAIC-VR\.codex-work\other_figures_review\extracted")
helper.OUTPUT_DIR = str(OUTPUT_DIR)

Diagram = helper.Diagram
PALETTE = helper.PALETTE
style_box = helper.style_box
style_text = helper.style_text
style_edge = helper.style_edge


def image_style(path: Path) -> str:
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return (
        "shape=image;imageAspect=1;aspect=fixed;html=1;verticalAlign=middle;"
        "labelPosition=center;verticalLabelPosition=middle;strokeColor=none;"
        f"image=data:image/png,{encoded};"
    )


def frame(d: Diagram, cid: str, image: Path, x: int, y: int, w: int, h: int) -> None:
    d.box(
        cid + "_frame",
        "",
        x - 5,
        y - 5,
        w + 10,
        h + 10,
        style_box("#FFFFFF", "#B9C5D2", 20, rounded=1, stroke_width=1.4),
    )
    d.box(cid, "", x, y, w, h, image_style(image))


def badge(d: Diagram, cid: str, label: str, x: int, y: int, color: str) -> None:
    d.box(
        cid,
        label,
        x,
        y,
        42,
        42,
        (
            "ellipse;whiteSpace=wrap;html=1;fillColor=" + color + ";strokeColor=#FFFFFF;"
            "strokeWidth=2;fontColor=#FFFFFF;fontFamily=Arial;fontSize=18;fontStyle=1;"
            "align=center;verticalAlign=middle;"
        ),
    )


def row_panel(d: Diagram, cid: str, label: str, x: int, y: int, w: int, h: int,
              fill: str, stroke: str) -> None:
    d.box(cid + "_body", "", x, y, w, h,
          style_box("#FAFBFC", "#CFD7E2", 20, rounded=1, stroke_width=1.1))
    d.box(cid + "_head", label, x + 10, y + 10, w - 20, 34,
          style_box(fill, stroke, 23, bold=True, rounded=1, align="left", stroke_width=1.1))


def build_dual_end() -> Path:
    d = Diagram("dualEndCodeAligned", "Dual-end state interaction", 1100, 850)
    row_panel(
        d, "de_top", "Runtime light-intensity control", 20, 20, 1060, 360,
        PALETTE["blue_fill"], PALETTE["blue_stroke"],
    )
    top_a = ASSET_DIR / "dual_end" / "panel_a_image.png"
    top_b = ASSET_DIR / "dual_end" / "panel_b_image.png"
    frame(d, "de_a", top_a, 45, 80, 455, 231)
    frame(d, "de_b", top_b, 600, 80, 455, 230)
    badge(d, "de_a_badge", "(a)", 30, 65, PALETTE["blue_stroke"])
    badge(d, "de_b_badge", "(b)", 585, 65, PALETTE["blue_stroke"])
    d.box("de_a_cap", "Lower Light Intensity setting", 45, 322, 455, 34,
          style_text(24, bold=True))
    d.box("de_b_cap", "Higher setting and brighter rendered scene", 600, 322, 455, 34,
          style_text(24, bold=True))
    d.box("de_light_state", "SyncVar", 510, 172, 80, 62,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 20, bold=True))
    d.edge("de_light_1", "de_a", "de_light_state", "",
           style_edge(PALETTE["blue_stroke"], 20, dashed=False, width=2.2))
    d.edge("de_light_2", "de_light_state", "de_b", "",
           style_edge(PALETTE["blue_stroke"], 20, dashed=False, width=2.2))

    row_panel(
        d, "de_bottom", "Participant interaction and replicated task progress", 20, 400, 1060, 430,
        PALETTE["purple_fill"], PALETTE["purple_stroke"],
    )
    bottom_c = ASSET_DIR / "dual_end" / "panel_c_image.png"
    bottom_d = ASSET_DIR / "dual_end" / "panel_d_image.png"
    frame(d, "de_c", bottom_c, 55, 475, 280, 280)
    frame(d, "de_d", bottom_d, 455, 470, 600, 305)
    badge(d, "de_c_badge", "(c)", 40, 460, PALETTE["purple_stroke"])
    badge(d, "de_d_badge", "(d)", 440, 455, PALETTE["purple_stroke"])
    d.box("de_c_cap", "Participant retrieves a target product", 45, 770, 300, 40,
          style_text(23, bold=True))
    d.box("de_d_cap", "Experimenter view reflects updated task progress", 455, 785, 600, 35,
          style_text(23, bold=True))
    d.box("de_task_state", "TaskList\nSyncList", 345, 590, 100, 66,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 20, bold=True))
    d.edge("de_task_1", "de_c", "de_task_state", "",
           style_edge(PALETTE["purple_stroke"], 20, dashed=True, width=2.2))
    d.edge("de_task_2", "de_task_state", "de_d", "",
           style_edge(PALETTE["purple_stroke"], 20, dashed=True, width=2.2))
    return Path(d.save("MOSAIC_VR_dual_end_state_interaction_CODE_ALIGNED.drawio"))


def build_dual_view() -> Path:
    d = Diagram("dualViewCodeAligned", "Dual-view workflow", 1200, 1600)
    d.box("dv_h_a", "(a) Participant view", 35, 20, 420, 48,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 28, bold=True))
    d.box("dv_h_c", "Runtime coordination", 500, 20, 200, 48,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 24, bold=True))
    d.box("dv_h_b", "(b) Experimenter view", 745, 20, 420, 48,
          style_box(PALETTE["purple_fill"], PALETTE["purple_stroke"], 28, bold=True))

    row_panel(d, "dv_p1", "I. Session preparation", 20, 80, 1160, 470,
              PALETTE["blue_fill"], PALETTE["blue_stroke"])
    frame(d, "dv_a1", ASSET_DIR / "dual_view" / "a1_image.png", 130, 170, 240, 240)
    badge(d, "dv_a1_badge", "a1", 140, 180, PALETTE["blue_stroke"])
    d.box("dv_a1_cap", "Safety notice and START", 80, 420, 340, 34, style_text(23, bold=True))
    frame(d, "dv_b1", ASSET_DIR / "dual_view" / "b1_image.png", 790, 120, 340, 173)
    badge(d, "dv_b1_badge", "b1", 800, 130, PALETTE["purple_stroke"])
    d.box("dv_b1_cap", "Select collection mode", 775, 300, 370, 30, style_text(22, bold=True))
    frame(d, "dv_b2", ASSET_DIR / "dual_view" / "b2_image.png", 790, 345, 340, 173)
    badge(d, "dv_b2_badge", "b2", 800, 355, PALETTE["purple_stroke"])
    d.box("dv_b2_cap", "Create participant profile", 775, 522, 370, 25, style_text(22, bold=True))
    d.box("dv_ready", "Connection + Ready state", 500, 255, 200, 72,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 23, bold=True))
    d.edge("dv_e1", "dv_a1", "dv_ready", "", style_edge(PALETTE["blue_stroke"], 20))
    d.edge("dv_e2", "dv_b2", "dv_ready", "", style_edge(PALETTE["purple_stroke"], 20))

    row_panel(d, "dv_p2", "II. Trial initialization", 20, 560, 1160, 440,
              PALETTE["purple_fill"], PALETTE["purple_stroke"])
    d.box("dv_participant_ready", "Participant endpoint\nscene load | task state | gaze", 85, 735, 330, 90,
          style_box(PALETTE["blue_fill"], PALETTE["blue_stroke"], 24, bold=True))
    d.box("dv_scene", "SyncScene to GameplayScene", 500, 735, 200, 80,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 23, bold=True))
    frame(d, "dv_b3", ASSET_DIR / "dual_view" / "b3_image.png", 805, 610, 315, 160)
    badge(d, "dv_b3_badge", "b3", 815, 620, PALETTE["purple_stroke"])
    d.box("dv_b3_cap", "Open scene selection", 790, 773, 345, 20, style_text(20, bold=True))
    frame(d, "dv_b4", ASSET_DIR / "dual_view" / "b4_image.png", 805, 820, 315, 160)
    badge(d, "dv_b4_badge", "b4", 815, 830, PALETTE["purple_stroke"])
    d.box("dv_b4_cap", "Apply scene selection", 790, 982, 345, 17, style_text(20, bold=True))
    d.edge("dv_e3", "dv_b4", "dv_scene", "", style_edge(PALETTE["purple_stroke"], 20))
    d.edge("dv_e4", "dv_scene", "dv_participant_ready", "", style_edge(PALETTE["blue_stroke"], 20))

    row_panel(d, "dv_p3", "III. Running trial and manual recording", 20, 1010, 1160, 300,
              PALETTE["green_fill"], PALETTE["green_stroke"])
    frame(d, "dv_a2", ASSET_DIR / "dual_view" / "a2_image.png", 45, 1065, 400, 203)
    badge(d, "dv_a2_badge", "a2", 55, 1075, PALETTE["blue_stroke"])
    frame(d, "dv_b5", ASSET_DIR / "dual_view" / "b5_image.png", 755, 1065, 400, 203)
    badge(d, "dv_b5_badge", "b5", 765, 1075, PALETTE["purple_stroke"])
    d.box("dv_run", "Task actions -> replicated progress/status\nQuest gaze -> UDP -> PC\nMoCap frames -> avatar + link metrics", 485, 1085, 230, 145,
          style_box(PALETTE["grey_fill"], PALETTE["grey_stroke"], 21, bold=True))
    d.edge("dv_e5", "dv_a2", "dv_run", "", style_edge(PALETTE["green_stroke"], 20))
    d.edge("dv_e6", "dv_run", "dv_b5", "", style_edge(PALETTE["green_stroke"], 20))
    d.box("dv_a2_cap", "First-person task execution", 45, 1270, 400, 28, style_text(22, bold=True))
    d.box("dv_b5_cap", "Experimenter-side supervision", 755, 1270, 400, 28, style_text(22, bold=True))

    row_panel(d, "dv_p4", "IV. Trial closeout", 20, 1320, 1160, 260,
              PALETTE["amber_fill"], PALETTE["amber_stroke"])
    frame(d, "dv_a3", ASSET_DIR / "dual_view" / "a3_image.png", 130, 1370, 180, 180)
    badge(d, "dv_a3_badge", "a3", 140, 1380, PALETTE["blue_stroke"])
    frame(d, "dv_b6", ASSET_DIR / "dual_view" / "b6_image.png", 790, 1380, 340, 173)
    badge(d, "dv_b6_badge", "b6", 800, 1390, PALETTE["purple_stroke"])
    d.box("dv_stop", "Stop recording first\nthen Stop Trial to RoomScene", 485, 1410, 230, 90,
          style_box(PALETTE["amber_fill"], PALETTE["amber_stroke"], 22, bold=True))
    d.edge("dv_e7", "dv_a3", "dv_stop", "", style_edge(PALETTE["amber_stroke"], 20))
    d.edge("dv_e8", "dv_stop", "dv_b6", "", style_edge(PALETTE["amber_stroke"], 20))
    d.box("dv_a3_cap", "Participant completion feedback", 70, 1550, 300, 25, style_text(21, bold=True))
    d.box("dv_b6_cap", "Experimenter completion summary", 775, 1554, 370, 22, style_text(21, bold=True))

    return Path(d.save("MOSAIC_VR_dual_view_workflow_word_page_CODE_ALIGNED.drawio"))


def build_setup() -> Path:
    d = Diagram("setupCodeAligned", "Experiment setup and software", 1200, 430)
    d.box("setup_body", "", 20, 20, 1160, 390,
          style_box("#FAFBFC", "#CFD7E2", 20, rounded=1, stroke_width=1.2))
    frame(d, "setup_a", ASSET_DIR / "setup" / "panel_a_image.png", 45, 55, 520, 293)
    frame(d, "setup_b", ASSET_DIR / "setup" / "panel_b_image.png", 625, 64, 530, 269)
    badge(d, "setup_a_badge", "(a)", 30, 40, PALETTE["blue_stroke"])
    badge(d, "setup_b_badge", "(b)", 610, 49, PALETTE["purple_stroke"])
    d.box("setup_a_cap", "Physical setup: HMD and optical MoCap markers", 45, 360, 520, 32,
          style_text(23, bold=True))
    d.box("setup_b_cap", "Experimenter view: MoCap-driven avatar and task state", 625, 360, 530, 32,
          style_text(23, bold=True))
    return Path(d.save("MOSAIC_VR_experiment_setup_and_software_CODE_ALIGNED.drawio"))


if __name__ == "__main__":
    for output in (build_dual_end(), build_dual_view(), build_setup()):
        print(output)
