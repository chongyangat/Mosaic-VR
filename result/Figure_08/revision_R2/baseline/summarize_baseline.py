from pathlib import Path
import json
import numpy as np
import pandas as pd

B=Path(__file__).resolve().parent
m=pd.read_csv(B/'GC_sensitivity_participant_metrics.csv')
c=pd.read_csv(B/'Clock_mapping_participant_metrics.csv')
g=pd.read_csv(B/'GC_event_evidence.csv')
w=pd.read_csv(B/'Analysis_windows_private_timestamps.csv')
t=pd.read_csv(B/'Paired_scene_tests_and_effects.csv')
p=pd.read_csv(B/'Input_and_GC_provenance_audit.csv')

def csv(df,name):
    df.to_csv(B/name,index=False,encoding='utf-8-sig',float_format='%.10f')
def md(df):
    cols=list(df.columns)
    lines=['| '+' | '.join(cols)+' |','| '+' | '.join(['---']*len(cols))+' |']
    for row in df.itertuples(index=False,name=None):
        lines.append('| '+' | '.join(str(x).replace('|','/') for x in row)+' |')
    return '\n'.join(lines)
def fmt(v): return f'{v:.3f}'

dur=[]
for (part,scene),group in g.groupby(['participant','scene'],sort=False):
    intervals=sorted((max(0,x-.25),min(300,x+.25)) for x in group.elapsed_s)
    merged=[]
    for a,b in intervals:
        if merged and a<=merged[-1][1]: merged[-1][1]=max(b,merged[-1][1])
        else: merged.append([a,b])
    seconds=sum(b-a for a,b in merged)
    dur.append(dict(participant=part,scene=scene,gc_source=group.source.iloc[0],events_n=len(group),union_gc_window_seconds=seconds,window_percent=seconds/3))
dur=pd.DataFrame(dur)
csv(dur,'GC_excluded_time_by_run.csv')

ss=[]
for (scene,link),group in m.groupby(['scene','link'],sort=False):
    row=dict(scene=scene,link=link)
    for scheme,part in group.groupby('scheme'):
        for metric in ['median_ms','p99_ms','gt50_pct']:
            row[f'{scheme}_{metric}_median']=part[metric].median()
            row[f'{scheme}_{metric}_q1']=part[metric].quantile(.25)
            row[f'{scheme}_{metric}_q3']=part[metric].quantile(.75)
        row[f'{scheme}_maximum_of_all_runs_ms']=part.max_ms.max()
        row[f'{scheme}_retained_samples_n']=part.valid_n.sum()
        row[f'{scheme}_gt50_samples_n']=part.gt50_n.sum()
    filtered=group[group.scheme=='GC_filtered']
    row['window_samples_n']=filtered.window_samples_n.sum()
    row['invalid_all_n']=filtered.invalid_all_n.sum()
    row['gc_valid_excluded_n']=filtered.gc_valid_excluded_n.sum()
    row['gc_window_all_n']=filtered.gc_window_all_n.sum()
    row['invalid_gc_overlap_n']=filtered.invalid_gc_overlap_n.sum()
    ss.append(row)
ss=pd.DataFrame(ss)
csv(ss,'Table_GC_sensitivity_for_manuscript.csv')

cs=[]
for (scheme,scene,link),group in c.groupby(['scheme','scene','link'],sort=False):
    row=dict(scheme=scheme,scene=scene,link=link,runs_n=len(group),samples_n=group.samples_n.sum(),ready_samples_n=group.ready_n.sum(),clock_valid_samples_n=group.clock_valid_n.sum())
    row['ready_sample_pct']=row['ready_samples_n']/row['samples_n']*100
    row['clock_valid_sample_pct']=row['clock_valid_samples_n']/row['samples_n']*100
    for prefix in ['mapping_age_ms','uncertainty_ms','sync_rtt_ms']:
        row[prefix+'_run_median_median']=group[prefix+'_median'].median()
        row[prefix+'_run_median_min']=group[prefix+'_median'].min()
        row[prefix+'_run_median_max']=group[prefix+'_median'].max()
        row[prefix+'_run_p99_median']=group[prefix+'_p99'].median()
        row[prefix+'_all_run_max']=group[prefix+'_max'].max()
    cs.append(row)
cs=pd.DataFrame(cs)
csv(cs,'Clock_mapping_scene_summary.csv')

# Numerical evidence sufficient for concise report, full precision in CSVs.
compact=[]
for row in ss.to_dict('records'):
    compact.append({'场景':row['scene'],'链路':row['link'],'中位数GC删→留(ms)':f"{row['GC_filtered_median_ms_median']:.3f} → {row['GC_retained_median_ms_median']:.3f}",'P99 GC删→留(ms)':f"{row['GC_filtered_p99_ms_median']:.3f} → {row['GC_retained_p99_ms_median']:.3f}",'>50ms比例GC删→留(%)':f"{row['GC_filtered_gt50_pct_median']:.3f} → {row['GC_retained_gt50_pct_median']:.3f}",'完整有效记录最高延迟(ms)':f"{row['GC_retained_maximum_of_all_runs_ms']:.2f}"})
test_table=t[t.scheme=='GC_filtered'][['metric','link','wilcoxon_w','exact_two_sided_p','holm_four_paths_per_metric_p','holm_all_eight_p','paired_difference_median_ms','paired_median_bootstrap_ci95_low_ms','paired_median_bootstrap_ci95_high_ms']].copy()
for col in test_table.select_dtypes(include='number'):
    test_table[col]=test_table[col].map(lambda x:f'{x:.6f}')
clock_eye=cs[(cs.scheme=='GC_retained')&(cs.link=='EyeTracking→PC')]
nf=m[m.scheme=='GC_retained']
eye=nf[nf.link=='EyeTracking→PC']
f=m[m.scheme=='GC_filtered']
direct=g[g.source=='direct_log']
facts=json.loads((B/'Audit_facts.json').read_text(encoding='utf-8'))
facts.update(total_window_samples_n=int(nf.window_samples_n.sum()),total_invalid_n=int(nf.invalid_all_n.sum()),total_valid_gc_retained_n=int(nf.valid_n.sum()),eye_window_samples_n=int(eye.window_samples_n.sum()),eye_invalid_n=int(eye.invalid_all_n.sum()),eye_valid_gc_retained_n=int(eye.valid_n.sum()),gc_excluded_valid_samples_both_PC_paths=int(f.gc_valid_excluded_n.sum()),gc_all_window_samples_both_PC_paths=int(f.gc_window_all_n.sum()),invalid_gc_overlap_n=int(f.invalid_gc_overlap_n.sum()),total_identified_events=len(g),direct_logged_events=len(direct),heuristic_only_events=int((g.source=='cross_link_heuristic').sum()),maximum_direct_heuristic_delta_ms=float(direct.nearest_heuristic_delta_ms.max()),all_direct_events_within250ms=bool((direct.nearest_heuristic_delta_ms<=250).all()),gc_union_seconds_total=float(dur.union_gc_window_seconds.sum()),gc_union_seconds_min=float(dur.union_gc_window_seconds.min()),gc_union_seconds_max=float(dur.union_gc_window_seconds.max()))
(B/'Audit_facts.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2),encoding='utf-8')

report=f'''# 基线 20 次运行：复现、GC 敏感性与配对统计审计

本审计于 2026-09-06 对提交包原始 CSV、公开与内部来源 manifest、原始 D:\\视觉行为平台\\test_2 对应运行目录及两份分析脚本进行只读检查。所有新输出位于本目录；原数据与原脚本未修改。

## 可以直接用于修改正文的结论

1. **表 5 的过滤后数值可复现，尾延迟不能推广到包含 GC 的整个运行。** 80 行参与者×场景×链路指标、40 行表 5 源数据、清洗审计、场景汇总、配对差与原 8 项统计全部与包内 CSV 数值逐项相同。独立重算相对六位小数导出值的最大偏差 {facts['independent_recalculation_vs_reproduction_max_delta']:.3g}，仅由输出舍入造成。
2. **实际证据是 15 次直接日志 + 5 次跨链路启发式，原始目录未补齐缺日志运行。** 缺日志为 P01 两场景、P02 两场景、P03 supermarket。全部 20 个原始 CSV 与包内 CSV 的 SHA-256 对应一致。直接日志事件 {len(direct)} 个、无日志运行启发式事件 {facts['heuristic_only_events']} 个，共 {len(g)} 个。15 次运行中直接日志与启发式事件数量相等，全部直接事件距最近候选≤250 ms，最大 {direct.nearest_heuristic_delta_ms.max():.3f} ms。这是同批日志中的一致性核对，不能证明五次无日志运行的事件均为 GC，也不是独立留出验证。
3. **prespecified / predeclared 无前瞻性证据。** 在提供的包内未找到预注册、采集前定稿的分析计划或时间戳协议；脚本内仅有“predeclared audit tolerance”注释和固定 t 临界值错误提示中的“prespecified”。这些字样不证明时间先后。应写明 adopted / used，GC 敏感性为本次 post hoc 分析；不要由已有脚本推断事先指定。
4. **10 个配对编码得到支持，10 名独立真人不能由日志单独验证。** 公开 manifest 有 10 个 pseudonym，各对应 2 场景；内部 manifest 有 10 个原始参与者代码，20 个独一无二的 CSV 哈希。这个一致性支持 10 个参与者编码、20 次独立文件，不替代招募/签署同意/分配记录，也不证明顺序随机或 cohort 互不重叠。作者须确认真实参与者身份独立性、顺序和是否与其他实验重叠。
5. **统计家族应说清楚，浮点 ties 应纠正。** 原脚本按两个 endpoint 分别对四条路径做 Holm，未对 8 项一起调整。建议把 8 项列为同一探索性 family，所有调整后 p=1.000；两组各四项的旧口径也保留供透明比较。未拒绝零差异不能证明等效或场景不变。

## 完全相同窗口与有效性规则

各运行开始为四条链路最早记录时刻的最大值，分析区间为 [start, start+300 s)，最短共同覆盖 {w.common_duration_seconds.min():.3f} s，最长 {w.common_duration_seconds.max():.3f} s。两方案使用同一窗口及同一导出记录：仅排除延迟缺失、<0 或 >10,000 ms。GC_filtered 额外从两条 PC 入站链路删除事件前后各 250 ms；GC_retained 保留这些区间。MoCap→VR 与 shared-state RTT 两方案完全相同。

原代码并未按 Ready 字段额外删除记录；本次敏感性也没有增加新排除规则。MoCap 的同步字段在采集器中未填写（由并行源码审计确认），其 false/空白/零不可视为实际时钟失效，也不可解释为零不确定度。RTT 不需要 Ready 状态。`retained_pct` 的分母是窗口中**实际记录的该链路行数**，不是理论应到达帧数；100% retained 不证明零丢包。

窗口中共 {facts['total_window_samples_n']:,} 行；无效值 {facts['total_invalid_n']} 行，均为 EyeTracking→PC 负值。保留 GC 后有效 {facts['total_valid_gc_retained_n']:,} 行。GC 窗口覆盖两 PC 入站路径 {facts['gc_all_window_samples_both_PC_paths']:,} 行，其中 {facts['invalid_gc_overlap_n']} 行本来无效，因此额外删除有效记录 {facts['gc_excluded_valid_samples_both_PC_paths']:,} 行。每次运行 GC 区间并集为 {dur.union_gc_window_seconds.min():.2f}–{dur.union_gc_window_seconds.max():.2f} s（总 {dur.union_gc_window_seconds.sum():.2f} s/6,000 s，不按两路径重复计时）。每行、每链路的数量见 `GC_sensitivity_participant_metrics.csv`，按运行时间见 `GC_excluded_time_by_run.csv`。

## GC 敏感性结果

下表中延迟的“中位数”“P99”和 >50 ms 比例均先在每次运行内计算，再取每个场景 10 个运行的中位数；最高延迟为 10 个运行的最高单样本值。不能将跨运行 P99 中位数误称为合并样本 P99。

{md(pd.DataFrame(compact))}

在保留 GC 的 PC 入站路径中，跨人 P99 中位数约 89–107 ms，而过滤后约 6–10 ms；两场景峰值可达约 309–329 ms。GC 在真实交互运行中发生，是部署性能的一部分。应并列报告 complete-run operational performance 与 GC-excluded descriptive summaries，并将后者作为条件性描述。对五次启发式运行尤其要说明：用高延迟自身识别候选再删除会直接降低尾部指标，存在选择偏差。

## 8 项配对 Wilcoxon、效应与区间

场景差定义为 Street − Supermarket。零差（|d|≤1e−12）删除；绝对差 ties 取平均秩；对剩余 n 个符号穷举 2^n 种，双侧极端事件为 |Σ sign×rank|≥观测值。输入指标原 CSV 写至六位小数，本审计将差值舍入到 1e−8 ms 来消除更低位的浮点噪声，不改变记录的测量精度。Eye median 中的三个相同绝对差组在旧严格浮点比较中拆开，旧 W=17、raw p=0.3125；纠正后 W=16、raw p=0.2734375。其余七项过滤后 raw p 不变。全部原结果在 `original_reproduction/Paired_scene_latency_tests.csv` 保留。

下列区间为 20,000 次配对参与者重抽样的**中位差 percentile bootstrap 95% CI**，固定随机种子 20260906；n=10、两场景成对保留。区间未做多重比较调整，不等同于 Wilcoxon 检验的置信区间；仅描述效应不确定性，不能用于等效结论。完整保留 GC 方案同样 8 项结果在 `Paired_scene_tests_and_effects.csv`，Holm 全 8 项均为 1.000。

{md(test_table)}

原脚本另输出 pooled 20 runs 的普通 t 均值区间，忽略同一个人两场景的依赖；不建议把该 pooled CI 作为独立样本推断证据。单场景 n=10 描述与配对差推断应分别报告。

## EyeTracking→PC 同步元数据

本次 300 s 窗口 EyeTracking→PC 共 {facts['eye_window_samples_n']:,} 行，负值 {facts['eye_invalid_n']} 行；保留 GC 的有效 {facts['eye_valid_gc_retained_n']:,} 行全部 Ready 且 syncValid=true。所有运行的有效 Eye 记录 Ready 比例为 100%。此数字仅指共同稳态窗口，不能外推到启动期、断线或整段完整文件。

{md(clock_eye[['scene','samples_n','ready_sample_pct','uncertainty_ms_run_median_median','uncertainty_ms_run_median_min','uncertainty_ms_run_median_max','uncertainty_ms_all_run_max','mapping_age_ms_run_median_median','mapping_age_ms_run_p99_median','mapping_age_ms_all_run_max']].round(3))}

偏移不确定度为同步算法的保守代理界，不能当成相对外部真实时钟的测量精度。它与约 2.3 ms 的 Eye 中位传输量级接近，正文应保留“estimated one-way delay / clock-mapped application timestamps”含义，不将微小场景差描述为亚毫秒准确度结论。MoCap 元数据的未填写不能用这些零值做同等时钟证据。

## 建议可写入论文的英文

**Methods—window and GC provenance.** “For each of the 20 runs, we analyzed the same 300-s interval beginning at the latest first-record timestamp across the four paths. Records with missing, negative, or >10,000-ms latency were excluded. Direct Unity GCMonitor logs were available for 15 runs. For the other five runs, candidate GC-associated events were identified retrospectively when EyeTracking→PC and MoCap→PC delays both exceeded 150 ms within 10 ms; candidates within 1 s were merged. In the 15 logged runs, all 237 logged events had a corresponding candidate within 250 ms. This agreement was used as a consistency check, rather than independent validation of GC attribution in the unlogged runs. The GC-excluded analysis removed samples within ±250 ms of each event from the two PC-bound paths only. A post hoc sensitivity analysis retained these intervals while preserving the same 300-s windows and all other validity rules.”

**Results—GC sensitivity.** “Retaining GC-associated intervals had little effect on participant-level median delays but substantially increased tail delays. Across participants, the median P99 for EyeTracking→PC increased from 5.68 to 95.09 ms in Supermarket and from 5.88 to 88.69 ms in Street; the corresponding MoCap→PC values increased from 10.14 to 106.74 ms and from 10.47 to 95.67 ms. With GC intervals retained, the across-participant median proportions of samples exceeding 50 ms were 1.33% and 1.27% for EyeTracking→PC and 1.39% and 1.24% for MoCap→PC in Supermarket and Street, respectively. Thus, the GC-excluded summaries describe performance outside the identified intervals and should be interpreted alongside complete-window results.”

**Statistics.** “Scene comparisons were exploratory and used paired participant-level median and P99 delays for each of four paths. We computed two-sided exact signed-rank p values by enumerating sign assignments, omitting zero differences and assigning average ranks to tied absolute differences. Holm adjustment was applied jointly to the eight comparisons. Paired median differences and unadjusted 95% percentile-bootstrap intervals were calculated by resampling the ten participant pairs 20,000 times. No comparison remained significant after adjustment (all adjusted p = 1.000); these results do not establish equivalence between scenes.”

**Timing qualification.** “All valid EyeTracking→PC records within the selected 300-s windows were marked Ready with a valid clock mapping. The recorded offset-uncertainty proxy remained on a similar scale to the approximately 2.3-ms median estimated one-way delay. The estimates therefore characterize application-level timing under the recorded clock mapping; they are not an external measurement of submillisecond absolute timing accuracy.”

## 复现边界与文件

- `audit_baseline.py`：导入原脚本并只将 OUTPUT 定向至本目录；因 bundled Python 无 matplotlib，使用未调用的导入占位符并关闭样式和绘图函数，原数值函数与 main 的数值路径未修改。原脚本只删除 OUTPUT 下已过时的已知图名，因此此动作限制在本次新目录，未触及输入。
- `original_reproduction/`：原数值运算导出；原脚本自动生成的 README/captions 包含历史措辞，仅为复现证据，**不是本次推荐正文**。
- `Reproduction_comparison.csv`：逐表数值对比；`Table_GC_sensitivity_for_manuscript.csv`：用于主文/补充表的两方案并列汇总。
- `GC_sensitivity_participant_metrics.csv`、`GC_sensitivity_scene_summary.csv`：完整 160 行运行×路径×方案指标与场景分布。
- `Paired_scene_tests_and_effects.csv`：原始/纠正 p、两种 Holm 家族、效应及 bootstrap CI；`Paired_scene_individual_differences.csv`：每人配对差。
- `Input_and_GC_provenance_audit.csv`：20 个源文件和 GC 日志存在性/哈希核查；不导出私人源路径。
- `Clock_mapping_participant_metrics.csv`、`Clock_mapping_scene_summary.csv`、`Clock_state_counts.csv`：同步元数据；MoCap 未填字段须按上述限制解释。
- `Analysis_windows_private_timestamps.csv`：确切窗口时间戳，仅用于内部审核，公开发布前需单独处理绝对时间敏感性。
- Python {facts['python'].split()[0]}，NumPy {facts['numpy']}，pandas {facts['pandas']}。脚本、数据哈希与结果保留，未运行或修改 Unity。
'''
(B/'baseline_evidence.md').write_text(report,encoding='utf-8')
print(json.dumps(facts,ensure_ascii=False,indent=2))
print(clock_eye.to_string(index=False))
