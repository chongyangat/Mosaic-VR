# Figure 9：参与者时延中位数的场景比较

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

# Figure 9：两个场景的参与者中位时延

本目录整理当前论文 Figure 9 的图片、实验记录、处理与绘图代码，以及第二轮复核资料。原文件按原样复制，未重新分析数据。

## 文件对应关系

- `figure/`：当前投稿PNG；`drawing_sources/`：对应SVG、PDF、TIFF。此图由Python生成，没有Draw.io文件；两个场景面板由同一绘图函数生成，未另存独立子图文件。
- `source_package/05_Data/raw_pseudonymized/baseline/`：P01–P10的超市、街道原始NetworkPerformance.csv及现有最小GC事件日志。
- `source_package/05_Data/reproduced_output/Participant_level_latency_metrics.csv`：直接绘图数据，共10个参与者×2个场景×4条链路的80行；本图使用`median_ms`。每个散点为一次参与者–场景–链路的中位时延，箱线图汇总同场景同链路的10个参与者。它不是从逐时曲线点再次求中位数得到的图。
- 同一结果目录中的清理记录、GC事件表、配对场景统计和输入散列用于追溯；`source_package/05_Data/restricted_not_for_upload/baseline/`另存15份完整Unity日志上下文。
- `revision_R2/baseline/`：第二轮GC敏感性、时钟和来源复核资料，不是当前图像的生成源。

## 输入范围

本图直接涉及两个场景的全部20个CSV、15个最小GC日志。Figure 7、8、9均由同一原脚本整组生成；Figure 7/8的共同纵轴也读取两个场景。因此三图目录各自复制这35个输入，支持不修改代码独立复现。

直接GC日志缺失的5次运行是P01超市、P01街道、P02超市、P02街道、P03超市；现行脚本采用跨链路候选识别。其余15次使用已有GC事件日志。没有用其他运行日志填补缺口。

## 复现入口

在本Figure_09目录、具有Python环境的终端运行：

```powershell
python -m pip install -r .\source_package\06_Code\requirements_figures_07_11.txt
python .\source_package\06_Code\analysis_upload_candidate\plot_latency_stability_10participants_two_scenes.py
python .\source_package\06_Code\analysis_upload_candidate\analyze_paired_scene_latency.py
```

第一份脚本重新生成整组基线图及曲线/统计表；第二份脚本补充配对场景检验。结果写到`source_package/05_Data/reproduced_output/`，本图输出名为`Figure_participant_median_by_scene`，含PNG/SVG/PDF/TIFF。它不会自动更新`figure/`中的归档PNG。请保持现有目录层级，不要从结果目录中的历史脚本副本运行。Arial字体会影响与既有图的外观一致性。

R2审核脚本保留原投稿包、D盘原始记录等绝对路径，并依赖NumPy/Pandas环境；它按原样归档，不是可直接移机运行的便携入口。当前图的复现请使用上面两个`source_package/06_Code/analysis_upload_candidate/`脚本。


归档载荷：90 个文件，588.1 MiB。复制和提取文件均有 SHA-256 记录。
