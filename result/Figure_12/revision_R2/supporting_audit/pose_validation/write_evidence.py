from pathlib import Path
import json
import numpy as np
import pandas as pd
from scipy.spatial.transform import Rotation
from run_original_analysis import BASE, RAW, module as a

facts=json.loads((BASE/'audit_facts.json').read_text(encoding='utf-8'))
res=json.loads((BASE/'rerun/analysis_results.json').read_text(encoding='utf-8'))
q=pd.read_csv(RAW/'QuestPose_20260829_173101.csv')
dec=pd.read_csv(BASE/'quest_row_decisions.csv')
qq=q.loc[dec.quality_pass].copy()
t=dec.loc[dec.quality_pass,'mapped_prediction_time_pc_ms'].to_numpy()
pos=qq[['RenderPredicted-TrackingSpace位置'+c for c in 'XYZ']].to_numpy()
rot=Rotation.from_quat(qq[['RenderPredicted-TrackingSpace四元数'+c for c in 'XYZW']].to_numpy())
dt=np.diff(t);dp=np.linalg.norm(np.diff(pos,axis=0),axis=1);dr=np.rad2deg((rot[:-1].inv()*rot[1:]).magnitude())
b=(dt>100)|(dp>.25)|(dr>30)
pd.DataFrame({'preceding_original_csv_sequence':qq['序号'].to_numpy()[:-1][b], 'following_original_csv_sequence':qq['序号'].to_numpy()[1:][b], 'gap_ms':dt[b], 'step_position_m':dp[b], 'step_rotation_deg':dr[b], 'gap_gt100ms':(dt>100)[b], 'position_gt025m':(dp>.25)[b], 'rotation_gt30deg':(dr>30)[b]}).to_csv(BASE/'quest_segment_breaks.csv',index=False)
pd.DataFrame(res['data_quality']['quest']['quality_segments']).to_csv(BASE/'quest_continuous_segments.csv',index=False)
pd.DataFrame(res['data_quality']['mocap']['continuous_segments']).to_csv(BASE/'mocap_continuous_segments.csv',index=False)

findings=[
 {'id':'POSE01','priority':'must_correct','topic':'空间注册方法缺失','evidence':'原稿 P0272 不含 AX=YB、双变换、尺度、损失或空间抽样；脚本 solve_hand_eye/main 及已复現结果。','conclusion':'6.62 mm/1.16°来自先估计 lag，再由最初60%校准固定 tracker-to-eye X 和 Quest-to-MoCap Y，最终40%只评价；不能称为无注册的原生精度。','action':'新增下方 registration 段，明确622/410、30Hz、5mm/0.5°残差尺度与soft-L1、冻结两变换和lag、位置与旋转指标定义。'},
 {'id':'POSE02','priority':'must_qualify','topic':'预测时间与姿态未由同一返回值直接成对记录','evidence':'71ff03c/当前 VirtualPlayerControllor.cs:356–371先读CenterEyeAnchor Transform，独立读取GetNodePoseStateRaw；429–450把anchor pose与独立time传给记录器。QuestPoseRecorder.cs:374–385构造Unix代理。','conclusion':'源码支持render-step time proxy的来源与代数映射，但不能独立证明该Time精确对应存储anchor pose，更不能证明显示光子时间或传感器采样时间。','action':'P0271将corresponding改为assigned；保留proxy/assumption，加入限制句；不要以OpenXR一般规范替代此实现证据。'},
 {'id':'POSE03','priority':'must_qualify','topic':'指定commit无法覆盖实际姿态记录功能','evidence':'159447c (2026-08-25 22:01:55+08)没有QuestPoseRecorder.cs；该文件8/27才新增；8/29 16:01:11的71ff03c新增raw/predicted扩展。raw录制17:31，当前HEAD为38c6749 (9/3)。','conclusion':'可定位相关源代码历史，无法据此确认采集使用的APK/PC构建版本。','action':'不将71ff03c归为实际build；Supplement保留单独采集构建/哈希待作者核实。用“source inspection identified ...; the recording build could not be independently linked...” 。'},
 {'id':'POSE04','priority':'must_clarify','topic':'筛选与片段数量','evidence':'row_attrition.csv, quest_segment_breaks.csv, quality_statistics_by_population.csv。','conclusion':'Quest3048→3023（25未Ready）→2997（延迟9、预测跨度17；uncertainty0）→1631；MoCap6112→4783（1329零timestamp）→3760。Quest共25个质量片段，MoCap3个；算法按样本数最多选取，当前也确为时长最长。','action':'简报去除数量/比例；注明68.4s为分段前质量流首尾跨度，非连续可用时长；最终共同35.335s。筛选是软件时间/连续性代理，不是独立的光学跟踪质量证据。'},
 {'id':'POSE05','priority':'clarify','topic':'时序估计实现与不确定性','evidence':'dense_kinematic_signals/select_time_window/jackknife_lag/block_bootstrap_metrics。','conclusion':'平移先在2ms网格插值位置，再SG41点三阶平滑，再SG41点三阶求导；角速来自中心相对旋转再SG平滑；不是先插值已有速度。九窗口全部用中位数，无活动阈值。代表窗口以combined r−0.12|r_translation−r_rotation|最高选取。6次jackknife只属代表窗口；600次1s块bootstrap保留lag与变换，不含估计或跨会话不确定性。','action':'方法按代码重述；不能把jackknife散布当九窗中位数CI，也不能把block interval当总测量不确定度。0.1ms搜索和抛物线插值不代表0.1ms硬件分辨率。'},
 {'id':'POSE06','priority':'qualify','topic':'lever arm影响速度特征','evidence':'由刚体运动公式 v_eye=v_tracker+omega×(R t_X)；拟合t_X=[−13.209,−30.521,−9.726]mm，模34.650mm。校准后杆臂敏感性CSV。','conclusion':'平移速率对固定坐标变换不变，但对旋转中的不同刚体原点并不不变。原始九窗组合中位数−6.980ms，使用已拟合杆臂后的事后敏感性−6.831ms；差+0.149ms。本敏感性非独立替代估计。平移/旋转不一致可能含杆臂效应，不宜全归时钟。','action':'正文无需更换已复现主结果；可将敏感性放Supplement并在限制解释两速度特征的不同系统误差。'},
 {'id':'POSE07','priority':'clarify','topic':'统计总体','evidence':'quality_statistics_by_population.csv。','conclusion':'原稿38.15ms/P9555.14ms及offset bound中位1.87ms来自所有3023 base-valid行。实际选中1631行是37.98ms/P9555.02ms和1.888ms。','action':'保留原数字时写“across the 3023 clock-ready/render-valid rows”；如写最终片段则更新为37.98/55.02/1.89。不得混用。'},
 {'id':'POSE08','priority':'limitation','topic':'外推和参考链边界','evidence':'一个录制文件对、最终留出13.63s/410点；所有Quest行Update且RawPose有效0；MoCap文件无明确光学置信度/遮挡/可见marker字段，Timestamp列整数ms。','conclusion':'仅验证特定存储姿态链经过估计时间补偿和刚体注册的相对一致性；不证明两设备硬件时钟、采样同步、运动到光子延迟、绝对空间精度、跨会话复现、长期漂移，或未筛选运行表现。MoCap timestamp生成边界单凭CSV不能证明。','action':'保留single-recording/inter-system agreement限制；请作者补采集build、参考timestamp源、rigid mount/校准姿态运动范围以及阈值是否预先指定的实证，不把脚本注释predeclared当预注册证明。'}
]
out={'task':'HMD–MoCap原始数据重跑与Methods审计','summary':'252个共同数值字段与原processed结果完全一致；主要结果无数值差异。方法缺失、时间代理配对与build provenance需修订。','findings':findings,'facts':facts,'rerun_results':res}
(BASE/'pose_evidence.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')

english='''# Suggested manuscript text — source-grounded, not a claim about the actual binary

## P0271: replace the time-assignment wording

The recorded data contained the CenterEyeAnchor pose and a render-step time proxy obtained from `OVRPlugin.GetNodePoseStateRaw(EyeCenter, Step.Render).Time`. We assigned each stored anchor pose the PC-domain time

`t_Q,PC = t_read,QuestUnix + 1000 (t_render,OVR − t_read,OVR) − Δ_Quest−PC`,

where the OVR times are in seconds and the other terms are in milliseconds. This assignment uses the render-step time as a proxy for the stored pose's prediction target; it does not measure sensor sampling or photon emission. The inspected source reads the anchor transform and the render-step time through separate calls, so their exact correspondence was not independently verified. No valid nonpredicted pose samples were available in this recording, and the prediction horizon was not subtracted again.

## P0272: precise filtering/interpolation wording (merge with existing timing method)

We retained rows with valid clock and render-time flags and finite required pose fields, then applied thresholds of 60 ms for PC observation delay, 70 ms for prediction horizon, and 3 ms for the software-reported clock-offset uncertainty bound. These are analysis quality criteria, not independent tracking-quality measurements. Streams were split at the stated gaps and pose jumps, and the segment containing the most samples was retained for each stream; these were also the longest-duration segments in this recording. Their intersection was trimmed by 250 ms at each end, yielding 35.335 s. On a 2-ms grid, positions were linearly interpolated and orientations were interpolated by SLERP. Translation speed was obtained using a 41-point third-order Savitzky–Golay position smoother followed by a second 41-point third-order Savitzky–Golay derivative; SO(3) angular-speed magnitude was calculated from centered relative rotations and smoothed with the same window. The lag estimator used the median of all nine overlapping 12-s windows within the first 60% of the common interval. The representative window maximized the combined correlation with a penalty for the difference between the peak translation and rotation correlations. Its leave-block dispersion describes within-window stability.

## New registration paragraph after P0272 — required to support the spatial results

For spatial assessment, we estimated two fixed rigid transforms using `A(t)X = YB(t + τ)`, where `A` maps the optical rigid body to the MoCap reference frame, `B` maps CenterEye to the Quest tracking frame, `X` maps CenterEye to the optical rigid-body frame, and `Y` maps the Quest tracking frame to the MoCap frame. MoCap positions were converted from millimetres to metres and the native axes were reflected using `M = diag(−1, 1, 1)`, with rotations transformed as `MRM`. After lag estimation, the first 60% of the common interval supplied 622 pose pairs at 30 Hz for registration; the final 40% supplied 410 held-out pairs, with 250-ms guards around the split and interval ends. A relative-motion hand–eye initialization was followed by joint nonlinear least squares with a soft-L1 loss, using residual scales of 5 mm for translation and 0.5° for rotation. No scale parameter was fitted. The lag and both rigid transforms, including the tracker-to-eye lever arm, were frozen before evaluating the held-out pairs. Position error was Euclidean distance between the transformed eye positions; orientation error was the SO(3) geodesic angle.

## P0355–P0356: proposed results

Of 3048 Quest rows, 3023 met the clock/render-valid criteria and 2997 passed the three quality thresholds. Their first-to-last timestamp span was 68.413 s; continuity selection retained 1631 rows spanning 35.835 s. The MoCap file contained 6112 rows, including 1329 zero-timestamp rows; 3760 of the 4783 valid rows formed the selected continuous segment. Endpoint guards yielded a 35.335-s common interval. The nine-window median lag was −6.980 ms under `MoCap(t)` versus `Quest(t + τ)`, meaning that matched Quest poses carried mapped timestamps approximately 6.98 ms earlier. This is a residual relative offset between the stored pose chains. The representative window gave combined `r = 0.9875`; its translation and rotation peaks were −9.833 ms (`r = 0.9838`) and −5.299 ms (`r = 0.9915`). Across the 3023 clock-ready/render-valid rows, the prediction-horizon median and P95 were 38.147 and 55.144 ms, and the median software-reported offset-uncertainty bound was 1.871 ms.

The window-lag mean was −7.206 ms (SD 1.353 ms; range −8.832 to −5.228 ms); six leave-one-2-s-block estimates within the representative window averaged −6.985 ms (SD 0.341 ms; range −7.381 to −6.572 ms). Frozen registration estimated a 34.650-mm tracker-to-eye lever arm. In the 410 held-out pairs, position RMSE was 6.620 mm (P95 9.998 mm), and orientation RMSE was 1.157° (P95 2.488°). These values quantify inter-system agreement after estimated temporal alignment and fixed registration in this recording.

## P0374 / limitations — precise remaining boundary

The physical assessment used one recording and a selected continuous segment. It therefore does not establish cross-session repeatability, long-term drift, performance across rejected or discontinuous data, or absolute accuracy against a third reference. The lag combines residual clock mapping, the assumed association between separately read anchor poses and render-step time proxies, finite sampling and interpolation, and differences between the two pose-estimation chains; it does not isolate sensor delay, hardware synchronization, or motion-to-photon latency. Translation-speed magnitude also depends on the location of the tracked point during rotation, so a rigid-body lever arm can affect the translation-based timing feature. The spatial errors are conditional on the calibration-fitted lag and transforms. The available source history identifies the pose-recording implementation in a later revision than the specified platform commit, but the recording files do not independently establish the APK or PC build that produced them.

## Optional reproducibility supplement

The rotational initialization used relative motions separated by 0.35, 0.70, 1.40, and 2.50 s at a 0.20-s stride, retaining pairs with at least 4° of motion in either stream. Sixteen initial rotations (identity plus 15 seeded random rotations) were considered; the seed was 20260831. Translation was initialized by linear least squares. The final 12-parameter robust solve used soft-L1 scale 1, a maximum of 400 function evaluations, and `xtol = ftol = gtol = 10⁻¹²`. The fitted tracker-to-eye translation was [−13.209, −30.521, −9.726] mm in the optical rigid-body frame. A post-hoc calibration-based lever-arm sensitivity analysis changed the median combined lag from −6.980 to −6.831 ms; this is a within-recording sensitivity check, not an independently validated replacement estimator. If the supplied 600-resample, one-second block-bootstrap intervals are reported, they should be described as conditional intervals from the held-out error sequence with the fitted lag and transforms held fixed, not total measurement uncertainty.

Source-provenance statement: commit `159447c26485714716aa38c5353bf4c158d770aa` is dated 25 August 2026 and does not contain `QuestPoseRecorder.cs`. The relevant pose-recording changes are present in source commit `71ff03c11f1669a0b15fa42fa47c0109770a82fb`, dated 29 August 2026 at 16:01:11 +08:00, before the 17:31 recording. This chronological correspondence does not identify the actual acquisition binary; an APK/PC build hash or acquisition manifest is still needed to make that linkage.
'''
(BASE/'manuscript_methods_results_suggestions.md').write_text(english,encoding='utf-8')

md='''# HMD–MoCap 物理验证第二轮证据审计

重跑结论：原始两份 CSV 经过原分析脚本，复现 −6.980221 ms、留出位置 RMSE 6.619665 mm / P95 9.998044 mm、旋转 RMSE 1.157081° / P95 2.488022°。与原 processed JSON 的 252 个共同数值字段完全一致，未发现数值差异。核心问题是方法交代不全、预测时刻代理的精确对应关系未被记录本身验证，以及实际采集 binary 未被指定 commit 证明。

## 数据与重跑边界

- 输入、原脚本和 DOCX 均未修改；只复制分析脚本快照，运行时覆盖输入和输出路径。输入哈希见 `input_manifest.json`。
- 指定 bundled Python 缺 matplotlib；使用 C:\\Python313\\python.exe (3.13.7)，NumPy 2.4.4 / pandas 3.0.3 / SciPy 1.17.1 完整重跑。数值仍逐项相同。
- 所有 Quest 行均为 Update 阶段；RawPose 有效行数为 0。最终 selected segment 的平均记录频率约45.49 Hz、相邻时间中位27.82 ms；MoCap约90.00 Hz，Timestamp量化为整数ms。2-ms插值网格不是新增原始采样。
- 时间代数复核最大绝对差 <0.011 ms，未发现符号或再次扣除prediction horizon的错误。负lag表示匹配Quest记录的mapped timestamp较早，校正需把Quest时间戳后移其绝对值；不等于实测光子/传感器延迟。

## 逐项发现

'''
for f in findings:
    md+=f"### {f['id']} — {f['topic']} ({f['priority']})\n\n证据：{f['evidence']}\n\n结论：{f['conclusion']}\n\n处理：{f['action']}\n\n"
md+='''## 60/40、冻结与信息隔离的核查

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
'''
(BASE/'pose_evidence.md').write_text(md,encoding='utf-8')
print('Wrote pose_evidence.md, pose_evidence.json, manuscript_methods_results_suggestions.md and segment CSVs.')
