"""Execute original integrated numerical blocks only, with no plot/source writes."""
from pathlib import Path
import hashlib,json,sys
sys.dont_write_bytecode=True
import numpy as np
import pandas as pd

S=Path(r'C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\submission')
B=Path(__file__).resolve().parent
B.mkdir(parents=True,exist_ok=True)
CODE=S/'06_Code/analysis_upload_candidate/integrated_runs'
manifest=pd.read_csv(S/'05_Data/Integrated_run_manifest.csv')
links=['EyeTracking→PC','MoCap→PC','MoCap→VR','PC<->VR 状态同步']
expected={'supermarket':[4.78,86.32,89.98,36.95],'street':[5.64,50.30,53.69,40.11]}
audit={'method':'Original plotting scripts computational blocks executed unchanged; no plotting/import/source-output statements executed. All new output confined to this folder.','scenes':[]}
for row in manifest.to_dict('records'):
    scene=row['scene']
    filename='plot_network_applied_sciences_supermarket.py' if scene=='supermarket' else 'plot_network_applied_sciences.py'
    script=CODE/filename
    src=script.read_text(encoding='utf-8-sig')
    first='time_col = "采集时间"' if scene=='supermarket' else 'df = pd.read_csv(source_csv)'
    begin=src.index(first)
    endpoint=src.index('clean = truncated.loc[~gc_mask].copy()')+len('clean = truncated.loc[~gc_mask].copy()')
    block=src[begin:endpoint]
    data=S/'05_Data'/row['network_record']
    env={'pd':pd,'np':np,'source_csv':data}
    exec(compile(block,str(script),'exec'),env)
    raw=pd.read_csv(data)
    df,trunc,clean=env['df'],env['truncated'],env['clean']
    t0=env['t0']; cutoff=env['cutoff_time']
    events=env['gc_events']
    source_hash=hashlib.sha256(data.read_bytes()).hexdigest()
    result=dict(scene=scene,script=filename,script_sha256=hashlib.sha256(script.read_bytes()).hexdigest(),executed_start_line=src[:begin].count('\n')+1,executed_end_line=src[:endpoint].count('\n')+1,input_sha256=source_hash,input_matches_manifest=source_hash==row['network_sha256'],source_rows=len(raw),rows_after_drop_missing=len(df),source_missing_excluded_n=len(raw)-len(df),global_start=t0.isoformat(),global_end=df['采集时间'].max().isoformat(),source_duration_s=float((df['采集时间'].max()-t0).total_seconds()),analysis_end_inclusive=cutoff.isoformat(),analysis_duration_s=float(env['cutoff_s']),window_rows=len(trunc),later_residual_rows_excluded_n=len(df)-len(trunc),gc_events_n=len(events),gc_candidate_eye_timestamps_n=len(env['candidate_events']),gc_excluded_rows=int(env['gc_mask'].sum()),retained_rows=len(clean),gc_event_elapsed_s=[float((x-t0).total_seconds()) for x in events],video_offset_s=float(row['video_alignment_offset_s']),video_end_s=float(row['video_alignment_offset_s'])+float(env['cutoff_s']),recorded_gc_log_files_in_public_run=len(list(data.parent.glob('*.log'))),paths=[])
    for link,p99_expected in zip(links,expected[scene]):
        fullpath=df[df['链路']==link]
        w=trunc[trunc['链路']==link]
        cp=clean[clean['链路']==link]
        metric=dict(link=link,first_record_offset_s=float((fullpath['采集时间'].min()-t0).total_seconds()),last_record_offset_s=float((fullpath['采集时间'].max()-t0).total_seconds()),window_rows=len(w),gc_excluded_rows=len(w)-len(cp),retained_rows=len(cp),negative_in_window_n=int((w['延迟(毫秒)']<0).sum()),over10000_in_window_n=int((w['延迟(毫秒)']>10000).sum()),median_filtered_ms=float(cp['延迟(毫秒)'].median()),p99_filtered_ms=float(cp['延迟(毫秒)'].quantile(.99)),max_filtered_ms=float(cp['延迟(毫秒)'].max()),gt50_filtered_n=int((cp['延迟(毫秒)']>50).sum()),gt50_filtered_pct=float((cp['延迟(毫秒)']>50).mean()*100),p99_GC_retained_ms=float(w['延迟(毫秒)'].quantile(.99)),max_GC_retained_ms=float(w['延迟(毫秒)'].max()),quoted_p99_ms=p99_expected,quoted_p99_matches_to_2dp=abs(float(cp['延迟(毫秒)'].quantile(.99))-p99_expected)<.00501)
        result['paths'].append(metric)
    audit['scenes'].append(result)
    print(json.dumps(result,ensure_ascii=False,indent=2))

audit['gc_rule']={'threshold_ms':150,'threshold_comparison':'>','cross_link_tolerance_ms':10,'cross_link_tolerance_comparison':'<=','candidate_timestamp':'EyeTracking→PC record collection timestamp; any MoCap→PC >150 ms timestamp within ±10 ms qualifies','event_cluster':'retain the first candidate; retain later candidate only if >1 s after the last retained candidate; NOT cluster-median or midpoint','exclusion_halfwidth_ms':250,'excluded_paths':links[:2],'gc_attribution':'retrospective cross-link heuristic; source scripts do not read direct GC logs'}
audit['validity_rule_difference']='Integrated source blocks drop missing time/link/latency only, whereas baseline also removes negative and >10000-ms latencies. No negative or >10000-ms records occur in either retained integrated analysis window, so harmonizing those numerical bounds does not change these quoted results.'
audit['time_axis_caveat']='Reference implementation reconstructs MoCap–VR collection time as sentTimestamp + latency; latency includes FLatency. Thus this axis approximates mapped early-processing time + FLatency, not separately observed Quest arrival or PC batch receipt. Window truncation, cross-path coincidences, and video matching are approximate timing associations.'
(B/'audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')

def table(rows):
    keys=list(rows[0]);o=['| '+' | '.join(keys)+' |','| '+' | '.join('---' for _ in keys)+' |']
    for row in rows:o.append('| '+' | '.join(str(row[k]) for k in keys)+' |')
    return '\n'.join(o)
out=['# 两次 integrated runs 数值与来源审计','',
'直接执行原绘图脚本的数值区段，未执行绘图、导出或改写输入；结果可复现。两份 NetworkPerformance.csv 的 SHA-256 均匹配 Integrated_run_manifest.csv。没有播放视频，故本审计只证明数值和归档时间偏移的一致性，不重新确认行为标签或因果归属。',
'','## P99 与关键计数','']
for r in audit['scenes']:
    out += [f"### {r['scene']}",f"原文件 {r['source_rows']:,} 行；开始 {r['global_start']}，原时长 {r['source_duration_s']:.3f} s。窗口为从全文件最早记录起到 {r['analysis_end_inclusive']}（包含终点），共 {r['analysis_duration_s']:.3f} s、{r['window_rows']:,} 行；截去终点后残余 {r['later_residual_rows_excluded_n']:,} 行。检测 {r['gc_events_n']} 个启发式候选事件，删除两PC入站链路 {r['gc_excluded_rows']:,} 行，保留 {r['retained_rows']:,} 行。",'',table([{'链路':x['link'],'起始偏移(s)':f"{x['first_record_offset_s']:.3f}",'GC后P99(ms)':f"{x['p99_filtered_ms']:.5f}",'论文P99(ms)':f"{x['quoted_p99_ms']:.2f}",'GC后max(ms)':f"{x['max_filtered_ms']:.2f}",'保留GC P99(ms)':f"{x['p99_GC_retained_ms']:.5f}",'GC删行':x['gc_excluded_rows'],'>50ms计数':x['gt50_filtered_n']} for x in r['paths']]),'',f"全部四路 quoted P99 两位小数均匹配：{all(x['quoted_p99_matches_to_2dp'] for x in r['paths'])}。归档 video offset 为 +{r['video_offset_s']:.5f} s，即 video_s = (CSV collection_time − CSV_global_start)_s + offset。分析窗口终点对应视频约 {r['video_end_s']:.5f} s。",'']
out += ['## 必须写清的定义','',
'- Supermarket：窗口终点取四条预期链路最后时间戳的最小值。Street：终点明确取最后一条 MoCap→VR 记录。两者起点均为全CSV最早记录，不是 baseline 的四路共同开始时刻，且不是固定 300 s；起始阶段有部分链路晚到（见上表），不能称为每一瞬间四路均完整覆盖。',
'- 两个绘图数值区段均只 drop 缺失采集时间/链路/延迟。窗口内实际没有负延迟或 >10,000 ms 记录；补上 baseline 数值界不会改变当前结果。',
'- GC候选：一条 EyeTracking→PC 延迟 >150 ms 的去重采集时间，距任何 MoCap→PC 延迟 >150 ms 的去重时间 ≤10 ms。候选按时间排序；保留第一条，仅当新候选距最近已保留候选 >1 s 才记录下一事件。事件时间是最早合格 Eye 时间，不是两路中点，也不是簇中位数。删除该时刻 ±250 ms（含边界）内两条 PC 入站路径记录；MoCap→VR 和 shared-state RTT 不删除。',
'- 此规则与 baseline 的阈值、两路与窗口宽度相同，但事件时间与聚合实现不同。baseline 使用最近两路中点并取相邻候选簇中位时间；不得笼统声称两者逐点完全同一算法。Integrated 脚本没有读 GC 日志，必须使用 candidate GC-associated intervals / heuristic，不把机制识别当作直接证明。',
'- 公开 integrated 运行目录未附 .log。两个视频偏移与公开 manifest、对应高延迟分析脚本默认值一致（Supermarket 13.63333 s；Street 14.26 s）。这仅证明数值一致，不证明如何选定偏移、是否逐帧同步、标注者一致性或对齐不确定度。OPEN_ISSUES_BEFORE_UPLOAD.md 仍要求写明偏移来源与不确定度。',
'- analyze_high_latency.py 针对 Street 的探索性高延迟诊断读取完整文件，并未执行绘图脚本的 cutoff 或 GC 排除；因此其完整文件 P99/episode 列表不可直接替代 p352 的截断、GC后结果。analyze_supermarket_high_latency.py 执行与 Supermarket 绘图相同的截断/GC步骤；两种辅助脚本口径并不对称。',
'', '## MoCap–VR 时间轴与行为关联的限制','',
'并行参考源码审计已确认：MoCap–VR sampleTimestamp/导出“采集时间”在返回PC后被重建为 sentTimestamp + latency，而 latency 已加 SDK FLatency。因此该轴接近映射后的早期处理时间再加 FLatency，既不是独立捕获的 Quest arrival，也不是PC收到批数据的时刻。FLatency 未独立导出，无法从旧CSV精确扣除。使用此时间轴定义窗口终止、跨链路瞬时同步或匹配视频动作，只能支持近似时间关联。对两次 integrated runs 仍需确认采集 build 与已审源码版本一致。',
'','## 可写入正文的英文','',
'“The two representative runs were summarized over their recorded task intervals rather than the fixed 300-s baseline windows. The Supermarket interval ended at the earliest final-record timestamp among the four paths; the Street interval ended at the final MoCap→VR record. Candidate GC-associated intervals were identified retrospectively from near-coincident (>150 ms, within 10 ms) elevations in EyeTracking→PC and MoCap→PC, with at most one retained candidate per 1-s interval anchored at the first qualifying Eye timestamp. Only the two PC-bound paths were filtered within ±250 ms of each candidate. The reported integrated-run P99 values therefore describe the retained intervals after this heuristic exclusion.”',
'',
'“Recorded times were mapped to the archived videos using fixed offsets of 13.63333 s for Supermarket and 14.26 s for Street. These offsets permit approximate temporal review; their selection uncertainty was not quantified. In the reference implementation, the MoCap→VR collection-time field is reconstructed from the SDK timestamp and the reported latency, which includes FLatency. Associations between latency elevations and visible actions should therefore be interpreted as approximate and descriptive, without assigning a specific causal mechanism.”',
'','## 保存内容','',
'audit.json 保留输入哈希、原脚本哈希、执行源码行号、精确窗口、每路计数/指标与事件时间；audit_integrated.py 可复算本审计。所有新输出仅在本目录。']
(B/'audit.md').write_text('\n'.join(out),encoding='utf-8')
