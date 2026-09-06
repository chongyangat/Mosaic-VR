# HMD–MoCap 物理验证第二轮证据审计

重跑结论：原始两份 CSV 经过原分析脚本，复现 −6.980221 ms、留出位置 RMSE 6.619665 mm / P95 9.998044 mm、旋转 RMSE 1.157081° / P95 2.488022°。与原 processed JSON 的 252 个共同数值字段完全一致，未发现数值差异。核心问题是方法交代不全、预测时刻代理的精确对应关系未被记录本身验证，以及实际采集 binary 未被指定 commit 证明。

## 数据与重跑边界

- 输入、原脚本和 DOCX 均未修改；只复制分析脚本快照，运行时覆盖输入和输出路径。输入哈希见 `input_manifest.json`。
- 指定 bundled Python 缺 matplotlib；使用 C:\Python313\python.exe (3.13.7)，NumPy 2.4.4 / pandas 3.0.3 / SciPy 1.17.1 完整重跑。数值仍逐项相同。
- 所有 Quest 行均为 Update 阶段；RawPose 有效行数为 0。最终 selected segment 的平均记录频率约45.49 Hz、相邻时间中位27.82 ms；MoCap约90.00 Hz，Timestamp量化为整数ms。2-ms插值网格不是新增原始采样。
- 时间代数复核最大绝对差 <0.011 ms，未发现符号或再次扣除prediction horizon的错误。负lag表示匹配Quest记录的mapped timestamp较早，校正需把Quest时间戳后移其绝对值；不等于实测光子/传感器延迟。

## 逐项发现

### POSE01 — 空间注册方法缺失 (must_correct)

证据：原稿 P0272 不含 AX=YB、双变换、尺度、损失或空间抽样；脚本 solve_hand_eye/main 及已复現结果。

结论：6.62 mm/1.16°来自先估计 lag，再由最初60%校准固定 tracker-to-eye X 和 Quest-to-MoCap Y，最终40%只评价；不能称为无注册的原生精度。

处理：新增下方 registration 段，明确622/410、30Hz、5mm/0.5°残差尺度与soft-L1、冻结两变换和lag、位置与旋转指标定义。

### POSE02 — 预测时间与姿态未由同一返回值直接成对记录 (must_qualify)

证据：71ff03c/当前 VirtualPlayerControllor.cs:356–371先读CenterEyeAnchor Transform，独立读取GetNodePoseStateRaw；429–450把anchor pose与独立time传给记录器。QuestPoseRecorder.cs:374–385构造Unix代理。

结论：源码支持render-step time proxy的来源与代数映射，但不能独立证明该Time精确对应存储anchor pose，更不能证明显示光子时间或传感器采样时间。

处理：P0271将corresponding改为assigned；保留proxy/assumption，加入限制句；不要以OpenXR一般规范替代此实现证据。

### POSE03 — 指定commit无法覆盖实际姿态记录功能 (must_qualify)

证据：159447c (2026-08-25 22:01:55+08)没有QuestPoseRecorder.cs；该文件8/27才新增；8/29 16:01:11的71ff03c新增raw/predicted扩展。raw录制17:31，当前HEAD为38c6749 (9/3)。

结论：可定位相关源代码历史，无法据此确认采集使用的APK/PC构建版本。

处理：不将71ff03c归为实际build；Supplement保留单独采集构建/哈希待作者核实。用“source inspection identified ...; the recording build could not be independently linked...” 。

### POSE04 — 筛选与片段数量 (must_clarify)

证据：row_attrition.csv, quest_segment_breaks.csv, quality_statistics_by_population.csv。

结论：Quest3048→3023（25未Ready）→2997（延迟9、预测跨度17；uncertainty0）→1631；MoCap6112→4783（1329零timestamp）→3760。Quest共25个质量片段，MoCap3个；算法按样本数最多选取，当前也确为时长最长。

处理：简报去除数量/比例；注明68.4s为分段前质量流首尾跨度，非连续可用时长；最终共同35.335s。筛选是软件时间/连续性代理，不是独立的光学跟踪质量证据。

### POSE05 — 时序估计实现与不确定性 (clarify)

证据：dense_kinematic_signals/select_time_window/jackknife_lag/block_bootstrap_metrics。

结论：平移先在2ms网格插值位置，再SG41点三阶平滑，再SG41点三阶求导；角速来自中心相对旋转再SG平滑；不是先插值已有速度。九窗口全部用中位数，无活动阈值。代表窗口以combined r−0.12|r_translation−r_rotation|最高选取。6次jackknife只属代表窗口；600次1s块bootstrap保留lag与变换，不含估计或跨会话不确定性。

处理：方法按代码重述；不能把jackknife散布当九窗中位数CI，也不能把block interval当总测量不确定度。0.1ms搜索和抛物线插值不代表0.1ms硬件分辨率。

### POSE06 — lever arm影响速度特征 (qualify)

证据：由刚体运动公式 v_eye=v_tracker+omega×(R t_X)；拟合t_X=[−13.209,−30.521,−9.726]mm，模34.650mm。校准后杆臂敏感性CSV。

结论：平移速率对固定坐标变换不变，但对旋转中的不同刚体原点并不不变。原始九窗组合中位数−6.980ms，使用已拟合杆臂后的事后敏感性−6.831ms；差+0.149ms。本敏感性非独立替代估计。平移/旋转不一致可能含杆臂效应，不宜全归时钟。

处理：正文无需更换已复现主结果；可将敏感性放Supplement并在限制解释两速度特征的不同系统误差。

### POSE07 — 统计总体 (clarify)

证据：quality_statistics_by_population.csv。

结论：原稿38.15ms/P9555.14ms及offset bound中位1.87ms来自所有3023 base-valid行。实际选中1631行是37.98ms/P9555.02ms和1.888ms。

处理：保留原数字时写“across the 3023 clock-ready/render-valid rows”；如写最终片段则更新为37.98/55.02/1.89。不得混用。

### POSE08 — 外推和参考链边界 (limitation)

证据：一个录制文件对、最终留出13.63s/410点；所有Quest行Update且RawPose有效0；MoCap文件无明确光学置信度/遮挡/可见marker字段，Timestamp列整数ms。

结论：仅验证特定存储姿态链经过估计时间补偿和刚体注册的相对一致性；不证明两设备硬件时钟、采样同步、运动到光子延迟、绝对空间精度、跨会话复现、长期漂移，或未筛选运行表现。MoCap timestamp生成边界单凭CSV不能证明。

处理：保留single-recording/inter-system agreement限制；请作者补采集build、参考timestamp源、rigid mount/校准姿态运动范围以及阈值是否预先指定的实证，不把脚本注释predeclared当预注册证明。

## 60/40、冻结与信息隔离的核查

共同区间为1787995885895.065–1787995921230.315 ms，切分点1787995907096.215 ms。校准抽样从17:31:26.145至17:31:46.845，622点；留出从17:31:47.346至17:32:00.979，410点。空间评估的250ms边界保护大于最大80ms lag；时序窗口还距校准边界500ms，并有平滑支持余量。没有发现用留出误差调优主lag或变换的代码路径。分段则在全记录上按连续性选取，因此是对事后选定连续段的留出评估，不能外推为未筛选全程表现。旋转和杆臂作为固定 X 与 Y 共同估计，没有每帧重配准，也没有缩放拟合。

原稿“smoothed speeds”与代码略有顺序差异；脚本先对位置进行两次SG处理（平滑再求导），角速度则先中心差分后SG。代表窗口分数的惩罚使用平移与旋转各自峰值相关系数的差，不是两lag差。九窗不存在单独相关性或活动强度淘汰阈值。本次最高分窗口恰与主中位数lag数值相同，不能据此把两者定义合并。

## 独立质量代理与仍需作者补充

CSV中有render-time flag和软件同步状态，但没有保存render pose每行position/orientation tracked flag，也没有MoCap可见marker数量、遮挡、残差/置信度等独立光学质量字段。有效时钟、观测延迟、预测跨度、offset uncertainty及连续性只证明所应用的筛选条件；不能自动推断“高质量ground truth”。在所审数据内，sync boolean与Ready字符串完全一致；质量指标无负值、有效quat无零模、质量时间戳无重复，因此这些实现细节没有影响本次结果。脚本只对阈值作上限比较，未来数据仍宜显式排除负值和非有限数。

需作者确认的实质信息：采集APK与PC build哈希/版本；MoCap Timestamp真正赋值边界（传感器、SDK、软件update或接收端）；刚体安装/尺寸与光学校准及遮挡质量；运动覆盖；质量阈值是否事前指定。现有脚本注释“predeclared”不足以证明预先指定，建议在论文写“specified analysis thresholds”。

## 交付物

- `pose_evidence.json`: 结构化发现、数值证据、source provenance、完整重跑结果。
- `manuscript_methods_results_suggestions.md`: 可直接合入P0271/P0272/P0355/P0356/P0374的英文替换、所需新增注册段和补充材料文本。
- `row_attrition.csv`, `quest_row_decisions.csv`: 顺序剔除与每行决定。
- `quest_segment_breaks.csv`, `quest_continuous_segments.csv`, `mocap_continuous_segments.csv`: 分段依据与数量。
- `quality_statistics_by_population.csv`: 3023总体、2997质量总体、1631最终片段的统计差别。
- `original_vs_rerun_numeric.csv`: 252个数值字段逐项原值/重跑/差值。
- `leverarm_corrected_timing_sensitivity.csv`: 事后杆臂敏感性，不能称独立验证。
- `rerun/`: 原脚本全部重跑结果和图表（另存）。
- `source_snapshot/`: 原分析快照及指定commit/后续commit相关source，用于证明可见实现；不是采集binary证明。
