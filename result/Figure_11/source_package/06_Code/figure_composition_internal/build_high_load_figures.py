from __future__ import annotations

import base64
import html
import os
from pathlib import Path


PACKAGE_ROOT = Path(
    os.environ.get("MOSAIC_SUBMISSION_ROOT", Path(__file__).resolve().parents[2])
)
SOURCES_ROOT = PACKAGE_ROOT / "03_Figure_Sources_Internal"


def image_data(path: Path) -> str:
    mime = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
    payload = base64.b64encode(path.read_bytes()).decode("ascii")
    # Percent-encode the standard data-URI semicolon so draw.io does not
    # interpret it as an mxGraph style-field delimiter.
    return f"data:{mime}%3Bbase64,{payload}"


def cell(cell_id: str, value: str, style: str, x: float, y: float, width: float, height: float) -> str:
    return (
        f'        <mxCell id="{html.escape(cell_id)}" value="{html.escape(value)}" '
        f'style="{html.escape(style, quote=True)}" vertex="1" parent="1">\n'
        f'          <mxGeometry x="{x:g}" y="{y:g}" width="{width:g}" height="{height:g}" as="geometry" />\n'
        f'        </mxCell>\n'
    )


def edge(cell_id: str, style: str, x1: float, y1: float, x2: float, y2: float) -> str:
    return (
        f'        <mxCell id="{html.escape(cell_id)}" value="" style="{html.escape(style, quote=True)}" edge="1" parent="1">\n'
        f'          <mxGeometry relative="1" as="geometry">\n'
        f'            <mxPoint x="{x1:g}" y="{y1:g}" as="sourcePoint" />\n'
        f'            <mxPoint x="{x2:g}" y="{y2:g}" as="targetPoint" />\n'
        f'          </mxGeometry>\n'
        f'        </mxCell>\n'
    )


def build(
    scene_key: str,
    title: str,
    chart_path: Path,
    runtime_path: Path,
    out_path: Path,
) -> Path:
    page_w, page_h = 1800, 1730
    chart_x, chart_y, chart_w, chart_h = 40, 90, 1720, 1146.667
    runtime_x, runtime_y, runtime_w, runtime_h = 40, 1260, 1720, 448.6
    split_x = runtime_x + runtime_w * 0.466

    body_style = (
        "rounded=1;arcSize=3;whiteSpace=wrap;html=1;fillColor=#FAFBFC;"
        "strokeColor=#CFD7E2;strokeWidth=1.4;shadow=0;"
    )
    header_style = (
        "rounded=1;arcSize=8;whiteSpace=wrap;html=1;fillColor=#E8F1FB;"
        "strokeColor=#3E6B99;strokeWidth=1.2;fontColor=#17212B;fontFamily=Arial;"
        "fontSize=24;fontStyle=1;align=left;verticalAlign=middle;spacingLeft=16;"
    )
    frame_style = (
        "rounded=1;arcSize=3;whiteSpace=wrap;html=1;fillColor=#FFFFFF;"
        "strokeColor=#B9C5D2;strokeWidth=1.2;shadow=0;"
    )
    image_style = "shape=image;imageAspect=1;aspect=fixed;html=1;strokeColor=none;verticalAlign=middle;"
    badge_blue = (
        "ellipse;whiteSpace=wrap;html=1;fillColor=#3E6B99;strokeColor=#FFFFFF;strokeWidth=2;"
        "fontColor=#FFFFFF;fontFamily=Arial;fontSize=22;fontStyle=1;align=center;verticalAlign=middle;"
    )
    badge_purple = badge_blue.replace("#3E6B99", "#8064A2")
    patch_style = "rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=none;opacity=100;"
    divider_style = "endArrow=none;startArrow=none;strokeColor=#FFFFFF;strokeWidth=4;html=1;"

    chart_data = image_data(chart_path)
    runtime_data = image_data(runtime_path)
    nodes = []
    nodes.append(cell("body", "", body_style, 20, 20, 1760, 1690))
    nodes.append(cell("header", title, header_style, 40, 32, 1720, 46))
    nodes.append(cell("chart_frame", "", frame_style, chart_x - 4, chart_y - 4, chart_w + 8, chart_h + 8))
    nodes.append(cell("chart", "", image_style + "image=" + chart_data + ";", chart_x, chart_y, chart_w, chart_h))
    nodes.append(cell("runtime_frame", "", frame_style, runtime_x - 4, runtime_y - 4, runtime_w + 8, runtime_h + 8))
    nodes.append(cell("runtime", "", image_style + "image=" + runtime_data + ";", runtime_x, runtime_y, runtime_w, runtime_h))

    # Replace the source plot's plain panel letters with the manuscript's consistent badge style.
    nodes.append(cell("patch_a", "", patch_style, 195, 104, 100, 82))
    nodes.append(cell("patch_b", "", patch_style, 195, 674, 100, 82))
    nodes.append(cell("badge_a", "(a)", badge_blue, 58, 118, 56, 56))
    nodes.append(cell("badge_b", "(b)", badge_purple, 58, 688, 56, 56))
    nodes.append(cell("badge_c", "(c)", badge_blue, 58, runtime_y + 14, 56, 56))
    nodes.append(cell("badge_d", "(d)", badge_purple, split_x + 16, runtime_y + 14, 56, 56))
    nodes.append(edge("runtime_divider", divider_style, split_x, runtime_y + 4, split_x, runtime_y + runtime_h - 4))

    xml = (
        "<?xml version='1.0' encoding='UTF-8'?>\n"
        '<mxfile host="app.diagrams.net" modified="2026-08-18T08:00:00.000Z" '
        'agent="OpenAI Codex" version="24.7.17" type="device">\n'
        f'  <diagram id="{scene_key}" name="{html.escape(title)}">\n'
        f'    <mxGraphModel dx="1800" dy="1730" grid="1" gridSize="10" guides="1" tooltips="1" '
        f'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{page_w}" pageHeight="{page_h}" '
        'math="0" shadow="0" background="#FFFFFF">\n'
        "      <root>\n"
        '        <mxCell id="0" />\n'
        '        <mxCell id="1" parent="0" />\n'
        + "".join(nodes)
        + "      </root>\n"
        "    </mxGraphModel>\n"
        "  </diagram>\n"
        "</mxfile>\n"
    )
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(xml, encoding="utf-8")
    return out_path


def main() -> None:
    figure10 = SOURCES_ROOT / "Figure_10"
    figure11 = SOURCES_ROOT / "Figure_11"
    paths = [
        build(
            "supermarket",
            "Representative full-workflow Supermarket run",
            figure10
            / "generated_outputs"
            / "NetworkPerformance_20260814_214957_AppliedSciences.png",
            figure10 / "assets" / "MoCap_monitoring_frame.png",
            figure10 / "Figure_10_representative_full_workflow_supermarket.drawio",
        ),
        build(
            "street",
            "Representative full-workflow Street run",
            figure11
            / "generated_outputs"
            / "NetworkPerformance_20260814_195140_AppliedSciences.png",
            figure11 / "assets" / "MoCap_monitoring_frame.png",
            figure11 / "Figure_11_representative_full_workflow_street.drawio",
        ),
    ]
    for path in paths:
        print(path)


if __name__ == "__main__":
    main()
