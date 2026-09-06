# Figure 11：街道集成任务

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

## 可编辑 Draw.io 文件

- `source_package/03_Figure_Sources_Internal/Figure_11/Figure_11_representative_full_workflow_street.drawio`

## 本图数据与使用说明

# Figure 11：Street 代表性完整流程

本图由两组延迟曲线和同次运行的监控截图组成。所有相对路径均以本 `Figure_11` 文件夹为起点。

- **主数据**：`source_package/05_Data/raw_pseudonymized/integrated/street/NetworkPerformance.csv`。同目录 `ProductMovement.csv` 为任务事件记录；`source_package/05_Data/restricted_not_for_upload/integrated/street/EyesTracking.csv` 为同次眼动记录，二者不是延迟曲线的直接数值输入。
- **原始数据及视频**：`raw_original/` 保留原名 CSV、实验元数据和原生 MoCap 项目；`raw_original/integrated_run/` 中有 XINGYING_eWVNAALg3f.mp4 及 XINGYING_eWVNAALg3f_trimmed_04m28s.mp4（原图取截短版 225 s）。当前 `assets/MoCap_monitoring_frame.png` 与原名代表帧 PNG 的 SHA-256 一致。
- **当前绘图代码**：`source_package/06_Code/analysis_upload_candidate/integrated_runs/plot_network_applied_sciences.py`；同目录高延迟分析脚本是辅助分析，部分窗口/筛选口径不同，不能直接替代主图统计。
- **可编辑合成图及子图**：`source_package/03_Figure_Sources_Internal/Figure_11/Figure_11_representative_full_workflow_street.drawio`；其 `generated_outputs/` 中 `NetworkPerformance_20260814_195140_AppliedSciences.png/.svg/.pdf` 是曲线子图，`assets/MoCap_monitoring_frame.png` 是截图子图。已核实 drawio 的两个嵌入对象与上述 PNG 完全一致。
- **现有完整 PNG**：`source_package/03_Figure_Sources_Internal/Figure_11/generated_outputs/Figure_11_representative_full_workflow_street.png`，同目录保留 drawio 原始导出 PNG。根归档说明另区分 `submission_current` 和 `manuscript_R2`。
- **来源及审计**：`source_package/05_Data/Integrated_run_manifest.csv`；`revision_R2/supporting_audit/integrated/` 是第二轮复核，不是原绘图输出。

## 路径与缺失项

保留 `source_package` 层级是因为共享脚本使用 `parents[2]` 寻找投稿根。曲线脚本通过 `MOSAIC_NETWORK_CSV` / `MOSAIC_FIGURE_OUTPUT_DIR` 指定输入和输出。共享 `build_high_load_figures.py` 和 `prepare_manuscript_pngs.py` 的主入口会同时处理图 10、11；单图目录应调用指定图的函数。

这次 integrated run 未找到直接 GC 日志，当前曲线按脚本的跨链路延迟规则识别候选 GC 区间；未用其他实验的 GC 日志补齐。未找到原始截帧脚本及视频剪裁命令；已保留准确原帧和对应视频，文件名中的时间不是新推断。原始中文分析说明保留为历史记录，不能当作修订稿结论。

## 只重新生成本图（可选）

以下命令已按现存函数签名核对，归档时没有执行。PowerShell 在本图目录运行；Python 环境依赖见 `source_package/06_Code/requirements_figures_07_11.txt`，合成 PNG 需要 draw.io Desktop。输出写入新 `reproduced/`，保留归档源。

```powershell
$env:MOSAIC_NETWORK_CSV = (Resolve-Path '.\source_package\05_Data\raw_pseudonymized\integrated\street\NetworkPerformance.csv').Path
$env:MOSAIC_FIGURE_OUTPUT_DIR = Join-Path $PWD 'reproduced\charts'
python '.\source_package\06_Code\analysis_upload_candidate\integrated_runs\plot_network_applied_sciences.py'
@'
import importlib.util
from pathlib import Path
p = Path('source_package/06_Code/figure_composition_internal/build_high_load_figures.py')
spec = importlib.util.spec_from_file_location('compose', p)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
m.build(
    'street', 'Representative full-workflow Street run',
    Path('reproduced/charts/NetworkPerformance_20260814_195140_AppliedSciences.png'),
    Path('source_package/03_Figure_Sources_Internal/Figure_11/assets/MoCap_monitoring_frame.png'),
    Path('reproduced/Figure_11_representative_full_workflow_street.drawio'),
)
'@ | python -
& 'C:\Program Files\draw.io\draw.io.exe' -x -f png -s 2.4 -o '.\reproduced\Figure_11_representative_full_workflow_street_drawio_export.png' '.\reproduced\Figure_11_representative_full_workflow_street.drawio'
@'
from PIL import Image
with Image.open('reproduced/Figure_11_representative_full_workflow_street_drawio_export.png') as im:
    im.save('reproduced/Figure_11_representative_full_workflow_street.png', dpi=(600, 600), compress_level=6)
'@ | python -
```

重绘版本可能受字体、Matplotlib 或 draw.io 版本影响；本归档保留的是原文件原字节，不宣称重新导出必然产生相同文件哈希。


归档载荷：71 个文件，1013.9 MiB。复制和提取文件均有 SHA-256 记录。
