# 两次 integrated runs 数值与来源审计

直接执行原绘图脚本的数值区段，未执行绘图、导出或改写输入；结果可复现。两份 NetworkPerformance.csv 的 SHA-256 均匹配 Integrated_run_manifest.csv。没有播放视频，故本审计只证明数值和归档时间偏移的一致性，不重新确认行为标签或因果归属。

## P99 与关键计数

### supermarket
原文件 27,561 行；开始 2026-08-14T21:49:56.903000，原时长 115.209 s。窗口为从全文件最早记录起到 2026-08-14T21:51:51.448000（包含终点），共 114.545 s、27,445 行；截去终点后残余 116 行。检测 7 个启发式候选事件，删除两PC入站链路 728 行，保留 26,717 行。

| 链路 | 起始偏移(s) | GC后P99(ms) | 论文P99(ms) | GC后max(ms) | 保留GC P99(ms) | GC删行 | >50ms计数 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EyeTracking→PC | 0.291 | 4.78000 | 4.78 | 15.39 | 160.89620 | 384 | 0 |
| MoCap→PC | 0.289 | 86.31790 | 86.32 | 117.75 | 215.56220 | 344 | 577 |
| MoCap→VR | 0.000 | 89.98440 | 89.98 | 120.98 | 89.98440 | 0 | 705 |
| PC<->VR 状态同步 | 0.637 | 36.95310 | 36.95 | 40.41 | 36.95310 | 0 | 0 |

全部四路 quoted P99 两位小数均匹配：True。归档 video offset 为 +13.63333 s，即 video_s = (CSV collection_time − CSV_global_start)_s + offset。分析窗口终点对应视频约 128.17833 s。

### street
原文件 87,351 行；开始 2026-08-14T19:51:40.620000，原时长 365.763 s。窗口为从全文件最早记录起到 2026-08-14T19:56:22.655000（包含终点），共 282.035 s、77,426 行；截去终点后残余 9,925 行。检测 4 个启发式候选事件，删除两PC入站链路 447 行，保留 76,979 行。

| 链路 | 起始偏移(s) | GC后P99(ms) | 论文P99(ms) | GC后max(ms) | 保留GC P99(ms) | GC删行 | >50ms计数 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EyeTracking→PC | 0.165 | 5.63770 | 5.64 | 23.01 | 6.67000 | 227 | 0 |
| MoCap→PC | 0.176 | 50.30220 | 50.30 | 101.07 | 59.34620 | 220 | 257 |
| MoCap→VR | 0.000 | 53.69000 | 53.69 | 111.84 | 53.69000 | 0 | 327 |
| PC<->VR 状态同步 | 0.314 | 40.11240 | 40.11 | 40.90 | 40.11240 | 0 | 0 |

全部四路 quoted P99 两位小数均匹配：True。归档 video offset 为 +14.26000 s，即 video_s = (CSV collection_time − CSV_global_start)_s + offset。分析窗口终点对应视频约 296.29500 s。

## 必须写清的定义

- Supermarket：窗口终点取四条预期链路最后时间戳的最小值。Street：终点明确取最后一条 MoCap→VR 记录。两者起点均为全CSV最早记录，不是 baseline 的四路共同开始时刻，且不是固定 300 s；起始阶段有部分链路晚到（见上表），不能称为每一瞬间四路均完整覆盖。
- 两个绘图数值区段均只 drop 缺失采集时间/链路/延迟。窗口内实际没有负延迟或 >10,000 ms 记录；补上 baseline 数值界不会改变当前结果。
- GC候选：一条 EyeTracking→PC 延迟 >150 ms 的去重采集时间，距任何 MoCap→PC 延迟 >150 ms 的去重时间 ≤10 ms。候选按时间排序；保留第一条，仅当新候选距最近已保留候选 >1 s 才记录下一事件。事件时间是最早合格 Eye 时间，不是两路中点，也不是簇中位数。删除该时刻 ±250 ms（含边界）内两条 PC 入站路径记录；MoCap→VR 和 shared-state RTT 不删除。
- 此规则与 baseline 的阈值、两路与窗口宽度相同，但事件时间与聚合实现不同。baseline 使用最近两路中点并取相邻候选簇中位时间；不得笼统声称两者逐点完全同一算法。Integrated 脚本没有读 GC 日志，必须使用 candidate GC-associated intervals / heuristic，不把机制识别当作直接证明。
- 公开 integrated 运行目录未附 .log。两个视频偏移与公开 manifest、对应高延迟分析脚本默认值一致（Supermarket 13.63333 s；Street 14.26 s）。这仅证明数值一致，不证明如何选定偏移、是否逐帧同步、标注者一致性或对齐不确定度。OPEN_ISSUES_BEFORE_UPLOAD.md 仍要求写明偏移来源与不确定度。
- analyze_high_latency.py 针对 Street 的探索性高延迟诊断读取完整文件，并未执行绘图脚本的 cutoff 或 GC 排除；因此其完整文件 P99/episode 列表不可直接替代 p352 的截断、GC后结果。analyze_supermarket_high_latency.py 执行与 Supermarket 绘图相同的截断/GC步骤；两种辅助脚本口径并不对称。

## MoCap–VR 时间轴与行为关联的限制

并行参考源码审计已确认：MoCap–VR sampleTimestamp/导出“采集时间”在返回PC后被重建为 sentTimestamp + latency，而 latency 已加 SDK FLatency。因此该轴接近映射后的早期处理时间再加 FLatency，既不是独立捕获的 Quest arrival，也不是PC收到批数据的时刻。FLatency 未独立导出，无法从旧CSV精确扣除。使用此时间轴定义窗口终止、跨链路瞬时同步或匹配视频动作，只能支持近似时间关联。对两次 integrated runs 仍需确认采集 build 与已审源码版本一致。

## 可写入正文的英文

“The two representative runs were summarized over their recorded task intervals rather than the fixed 300-s baseline windows. The Supermarket interval ended at the earliest final-record timestamp among the four paths; the Street interval ended at the final MoCap→VR record. Candidate GC-associated intervals were identified retrospectively from near-coincident (>150 ms, within 10 ms) elevations in EyeTracking→PC and MoCap→PC, with at most one retained candidate per 1-s interval anchored at the first qualifying Eye timestamp. Only the two PC-bound paths were filtered within ±250 ms of each candidate. The reported integrated-run P99 values therefore describe the retained intervals after this heuristic exclusion.”

“Recorded times were mapped to the archived videos using fixed offsets of 13.63333 s for Supermarket and 14.26 s for Street. These offsets permit approximate temporal review; their selection uncertainty was not quantified. In the reference implementation, the MoCap→VR collection-time field is reconstructed from the SDK timestamp and the reported latency, which includes FLatency. Associations between latency elevations and visible actions should therefore be interpreted as approximate and descriptive, without assigning a specific causal mechanism.”

## 保存内容

audit.json 保留输入哈希、原脚本哈希、执行源码行号、精确窗口、每路计数/指标与事件时间；audit_integrated.py 可复算本审计。所有新输出仅在本目录。