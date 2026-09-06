# Figure 12：Quest–MoCap 时间与空间对齐验证

当前主图位于 `figure/`。本目录为复制归档，源文件保持原内容，具体原路径、用途及 SHA-256 见 `FILE_MANIFEST.csv`。

## 目录与版本

- `figure/`：整理时现有 02_Figures_Submission 的高分辨率编号主图。
- `source_package/`：相关投稿包文件保留原相对目录，包含绘图源、数据与代码；不存在的类别不会人为补造。
- `drawio_subfigures/`（适用时）：从各 draw.io 无损提取的内嵌子图；每个子目录的 SUBFIGURE_MAP.json 对应图层 ID、页面及源文件。
- `drawing_sources/`、`raw_original/`、`external_source_assets/` 等（适用时）：独立矢量导出、原始记录或包外素材，详见清单。
- `manuscript_embedded/original_v2/`：用户最初指定 v2 稿件内的实际图片。
- `manuscript_embedded/revision_R2/`：本会话第二轮阅读稿内的实际图片。它可能经压缩或修改，与当前投稿高分辨率主图分开保存。
- `revision_R2/`（适用时）：后续修订源与审计资料，其处理用途不同于原图生成代码。

以上目录只在有对应资料时出现。原始数据/完整日志/原屏录保留其已有内容及内部来源标记；本目录是工作资料归档。

## 本图数据与使用说明

# Figure 12：Quest–MoCap 时空对齐

当前图是 **4321 × 2951 四面板版**：(a) 留出集三维轨迹；(b) 时滞相关曲线；(c) 九个重叠窗口的估计；(d) 代表性 12 s 速度曲线。以下相对路径均以本 `Figure_12` 文件夹为起点。

- **主数据**：`source_package/05_Data/quest_mocap_validation/raw/QuestPose_20260829_173101.csv` 和 `update_timestamp1-Tracker0.csv`。
- **图所需处理结果**：同一数据目录的 `processed/analysis_results.json`、`cross_correlation_windows.csv` 和 `aligned_held_out_validation.csv`，分别提供分析参数/结果、窗口估计和留出轨迹。其他同次分析表和报告作为关联材料保留。
- **数据处理代码**：`source_package/06_Code/quest_mocap_validation/analyze_update_timestamp.py`；**当前绘图代码**：同目录 `make_mdpi_figures.py` 的 `temporal_figure(results)`。依赖版本见该目录 `requirements.txt`。
- **可编辑图**：`source_package/03_Figure_Sources_Internal/Figure_12/Figure_12_quest_mocap_temporal_alignment.svg`；PNG 的来源版本见根归档说明中的 `submission_current` 和 `manuscript_R2`。本图由 Matplotlib 直接绘制四个面板，不使用 drawio，也没有需补找的 drawio 嵌入截图。
- **更上游原始材料**：`raw_original/Quest_session_20260829_173101/` 为完整 Quest 运行（含 Unity 日志、网络诊断和实验元数据）；`raw_original/MoCap/update_timestamp1.cap` 及同名目录为原生光学记录与模型/标定。它们原本在 `D:/视觉行为平台/test_3`；两份分析输入与原记录 CSV 已核实 SHA-256 一致。
- **R2 复核**：`revision_R2/supporting_audit/pose_validation/` 保留第二轮方法/数值审计、源码快照及重跑结果，不与原图生成材料混称。

## 路径与版本

两个主脚本用 `Path(__file__).resolve().parents[2]` 定位 `source_package`，因此请保留 `06_Code/quest_mocap_validation` 与 `05_Data/quest_mocap_validation` 的相对结构。绘图脚本会导入同目录的 `analyze_update_timestamp`。

`make_mdpi_figures.py` 的主入口还生成历史独立 Figure 13；本归档没有将旧 Figure 13 SVG 混入当前图源。单独重绘图 12 应仅调用 `temporal_figure()`。处理脚本的主入口会重新计算联合时空分析并改写其输出目录；已有处理结果可以直接用于绘图，无须为查看源图再次分析。

本图不依赖视频截帧，也不使用 Figure 10/11 的候选 GC 过滤规则。同次 Unity 日志已保留；此日志存在不等于已证明数据采集所用 APK/PC 二进制与指定源码提交完全一致，相关界限见 R2 审计。

## 只重新绘制当前图 12（可选）

以下命令已按现存函数与导入依赖核对，归档时没有执行。PowerShell 在本图目录运行。它读取归档的原始/处理数据，仅将重绘 PNG/SVG 写入新 `reproduced/`，不会调用旧 Figure 13 绘图或重新执行主分析。

```powershell
@'
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path('source_package/06_Code/quest_mocap_validation').resolve()))
import make_mdpi_figures as m
m.configure_style()
results = json.loads((m.PROCESSED_DIR / 'analysis_results.json').read_text(encoding='utf-8'))
m.FIGURE_DIR = Path('reproduced/png')
m.SOURCE_ROOT = Path('reproduced/svg')
print(m.temporal_figure(results))
'@ | python -
```

输出为 `reproduced/png/Figure_12_quest_mocap_temporal_alignment.png` 和 `reproduced/svg/Figure_12/Figure_12_quest_mocap_temporal_alignment.svg`。不同字体和库版本可能影响图形文件哈希；原文件字节由归档清单验证。


归档载荷：72 个文件，67.6 MiB。复制和提取文件均有 SHA-256 记录。
