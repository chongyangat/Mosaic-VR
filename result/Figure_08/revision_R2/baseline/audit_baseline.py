"""Read-only baseline reproduction and post hoc GC sensitivity audit.

All output is confined to this script's directory. Original plotting calls are
disabled; original numerical calculations run unchanged with OUTPUT redirected.
No original data/script is written. Private source paths are never exported.
"""
from __future__ import annotations
import hashlib
import importlib.util
import itertools
import json
import sys
import types
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0, str(Path(__file__).resolve().parent / "_audit_dependencies"))
import numpy as np
import pandas as pd

S = Path(r"C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\submission")
OUT = Path(__file__).resolve().parent
OUT.mkdir(parents=True, exist_ok=True)
CODE = S / "06_Code/analysis_upload_candidate"
REPRO = OUT / "original_reproduction"

def load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, file)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod

def save(df, name):
    df.to_csv(OUT / name, index=False, encoding="utf-8-sig", float_format="%.10f")

# This audit reproduces numbers, not figures. The bundled Python has no
# matplotlib, so inject unused import placeholders and disable only style/
# drawing entrypoints. Every numerical function and main() calculation remains
# byte-for-byte the original source executed through importlib.
sys.modules["matplotlib"] = types.ModuleType("matplotlib")
sys.modules["matplotlib.pyplot"] = types.ModuleType("matplotlib.pyplot")
plot = load_module("baseline_source", CODE / "plot_latency_stability_10participants_two_scenes.py")
stats = load_module("baseline_stats_source", CODE / "analyze_paired_scene_latency.py")
plot.OUTPUT = REPRO
for name in ["set_plot_style", "draw_main_figure", "draw_p99_figure", "draw_median_figure"]:
    setattr(plot, name, lambda *args, **kwargs: None)
plot.main()
stats.build(REPRO / "Participant_level_latency_metrics.csv", REPRO)

# Reproduction comparison: original numerical files have six-decimal output.
comparison = []
for source_dir in [S / "05_Data/plot_ready", S / "05_Data/reproduced_output"]:
    for filename in ["Participant_level_latency_metrics.csv", "Cleaning_audit.csv", "Table5_link_latency_summary.csv", "Scene_latency_summary.csv", "Paired_scene_difference.csv", "Paired_scene_latency_tests.csv"]:
        if not (source_dir / filename).exists():
            continue
        a, b = pd.read_csv(source_dir / filename), pd.read_csv(REPRO / filename)
        # Older plot-ready participant identities are normalized only for matching.
        if "participant" in a:
            for f in [a, b]:
                f["participant"] = f["participant"].str.replace(r"^P0", "P", regex=True)
        keys = [c for c in ["participant", "scene", "link", "metric"] if c in a and c in b]
        aa, bb = a.sort_values(keys).reset_index(drop=True), b.sort_values(keys).reset_index(drop=True)
        common = [c for c in aa.select_dtypes(include="number") if c in bb]
        max_delta = float(np.nanmax(np.abs(aa[common].to_numpy() - bb[common].to_numpy()))) if common else np.nan
        comparison.append(dict(source=source_dir.name, file=filename, rows_source=len(a), rows_reproduced=len(b), numeric_columns=len(common), maximum_absolute_numeric_difference=max_delta, equal_within_1e_6=bool(max_delta <= 1e-6)))
save(pd.DataFrame(comparison), "Reproduction_comparison.csv")

manifest = pd.read_csv(S / "05_Data/Run_manifest_public.csv")
private = pd.read_csv(S / "05_Data/internal_provenance_not_for_upload/Run_manifest_original_paths.csv")
private_by_hash = {r["raw_csv_sha256"]: r for r in private.to_dict("records")}
provenance, windows, rows, states, clockrows, eventrows = [], [], [], [], [], []
all_original_codes = set()
for spec in plot.RUNS:
    public = manifest[(manifest.participant.str.replace(r"^P0", "P", regex=True) == spec.participant) & (manifest.scene == spec.scene)].iloc[0]
    priv = private_by_hash[public.network_record_sha256]
    all_original_codes.add(str(priv["participant"]))
    orig_csv = Path(str(priv["raw_csv"]))
    # Original manifest may hold an absolute path or a workspace-relative path.
    if not orig_csv.is_absolute():
        orig_csv = Path(r"D:\视觉行为平台\test_2") / orig_csv
    orig_parent = orig_csv.parent
    log_candidates = sorted(orig_parent.rglob("*.log")) if orig_parent.exists() else []
    input_path = plot.find_csv(plot.ROOT / spec.folder)
    df = plot.read_network_csv(input_path)
    start = df.groupby("链路")["采集时间"].min().max()
    common_end = df.groupby("链路")["采集时间"].max().min()
    end = start + pd.Timedelta(seconds=300)
    win = df[(df["采集时间"] >= start) & (df["采集时间"] < end)].copy()
    win["elapsed_s"] = (win["采集时间"] - start).dt.total_seconds()
    heuristic = plot.detect_gc_heuristic(win)
    log_path = plot.find_gc_log(plot.ROOT / spec.folder)
    direct = [t for t in plot.parse_gc_log(log_path) if start <= t < end]
    gc_events = direct if log_path else heuristic
    matching_original_logs = []
    for log in log_candidates:
        parsed = plot.parse_gc_log(log)
        relevant = [t for t in parsed if start <= t < end]
        if relevant:
            matching_original_logs.append((log, relevant))
    provenance.append(dict(participant=spec.participant, scene=spec.scene, package_csv_sha256_matches_manifest=plot.sha256(input_path) == public.network_record_sha256, original_csv_exists=orig_csv.exists(), original_csv_sha256_matches_manifest=(plot.sha256(orig_csv) == public.network_record_sha256) if orig_csv.exists() else False, source_folder_log_files=len(log_candidates), source_folder_logs_with_GC_in_window=len(matching_original_logs), package_direct_log_available=log_path is not None, direct_gc_events=len(direct), heuristic_events=len(heuristic), selected_gc_events=len(gc_events), selected_source="direct_log" if log_path else "cross_link_heuristic"))
    windows.append(dict(participant=spec.participant, scene=spec.scene, common_start=start.isoformat(), analysis_end_exclusive=end.isoformat(), common_end=common_end.isoformat(), window_seconds=300, common_duration_seconds=(common_end-start).total_seconds()))
    for i, event in enumerate(gc_events, 1):
        eventrows.append(dict(participant=spec.participant, scene=spec.scene, source="direct_log" if log_path else "cross_link_heuristic", event_id=i, elapsed_s=(event-start).total_seconds(), nearest_heuristic_delta_ms=plot.nearest_delta_seconds(event,heuristic)*1000))
    for link in plot.LINKS:
        raw = win[win["链路"] == link].copy()
        invalid = raw["延迟(毫秒)"].isna() | (raw["延迟(毫秒)"] < 0) | (raw["延迟(毫秒)"] > plot.VALID_LATENCY_MAX_MS)
        gc = pd.Series(plot.gc_exclusion_mask(raw["采集时间"], gc_events), index=raw.index) if link in plot.PC_BOUND_LINKS else pd.Series(False, index=raw.index)
        for scheme, keep in [("GC_filtered", ~invalid & ~gc), ("GC_retained", ~invalid)]:
            selected = raw[keep].copy()
            m = plot.latency_metrics(selected)
            rows.append(dict(participant=spec.participant, scene=spec.scene, link=link, scheme=scheme, gc_source="direct_log" if log_path else "cross_link_heuristic", window_samples_n=len(raw), invalid_all_n=int(invalid.sum()), negative_n=int((raw["延迟(毫秒)"]<0).sum()), missing_latency_n=int(raw["延迟(毫秒)"].isna().sum()), over_10000_n=int((raw["延迟(毫秒)"]>10000).sum()), gc_window_all_n=int(gc.sum()), invalid_gc_overlap_n=int((invalid & gc).sum()), gc_valid_excluded_n=int((gc & ~invalid).sum()) if scheme=="GC_filtered" else 0, invalid_excluded_after_gc_n=int((invalid & ~gc).sum()) if scheme=="GC_filtered" else int(invalid.sum()), retained_pct=len(selected)/len(raw)*100, **m))
            state = selected["时钟同步状态"].fillna("<missing>").astype(str)
            for value,count in state.value_counts(dropna=False).items():
                states.append(dict(participant=spec.participant,scene=spec.scene,link=link,scheme=scheme,state=value,samples_n=int(count),denominator_n=len(selected),percent=count/len(selected)*100))
            validflag = selected["时钟同步有效"].astype(str).str.lower().eq("true")
            base = dict(participant=spec.participant, scene=spec.scene, link=link, scheme=scheme, samples_n=len(selected), ready_n=int(state.eq("Ready").sum()), ready_pct=float(state.eq("Ready").mean()*100), clock_valid_n=int(validflag.sum()), clock_valid_pct=float(validflag.mean()*100))
            for field,prefix in [("同步年龄(毫秒)","mapping_age_ms"),("偏移不确定度上界(毫秒)","uncertainty_ms"),("同步RTT(毫秒)","sync_rtt_ms")]:
                vals = pd.to_numeric(selected[field],errors="coerce")
                usable = vals[vals >= 0]
                base.update({prefix+"_available_n":len(usable),prefix+"_missing_or_negative_n":int(vals.isna().sum()+(vals<0).sum()),prefix+"_median":float(usable.median()),prefix+"_p95":float(usable.quantile(.95)),prefix+"_p99":float(usable.quantile(.99)),prefix+"_max":float(usable.max())})
            clockrows.append(base)
    print(f"AUDITED {spec.participant} {spec.scene}; source_logs={len(matching_original_logs)}",flush=True)

metrics = pd.DataFrame(rows)
save(metrics,"GC_sensitivity_participant_metrics.csv")
save(pd.DataFrame(provenance),"Input_and_GC_provenance_audit.csv")
save(pd.DataFrame(windows),"Analysis_windows_private_timestamps.csv")
save(pd.DataFrame(eventrows),"GC_event_evidence.csv")
save(pd.DataFrame(states),"Clock_state_counts.csv")
save(pd.DataFrame(clockrows),"Clock_mapping_participant_metrics.csv")

# Summaries treat each run/participant equally, never pool high-rate samples.
summary=[]
for (scheme,scene,link), group in metrics.groupby(["scheme","scene","link"],sort=False):
    for metric in ["median_ms","p95_ms","p99_ms","max_ms","gt50_pct","retained_pct","gc_valid_excluded_n","gc_window_all_n","invalid_all_n"]:
        vals=group[metric].astype(float)
        summary.append(dict(scheme=scheme,scene=scene,link=link,metric=metric,participants_n=len(vals),median=float(vals.median()),q1=float(vals.quantile(.25)),q3=float(vals.quantile(.75)),minimum=float(vals.min()),maximum=float(vals.max()),sum=float(vals.sum())))
save(pd.DataFrame(summary),"GC_sensitivity_scene_summary.csv")

# Exact signed-rank sign-flip distribution. Round paired differences to 1e-8 ms
# to preserve decimal ties against floating-point subtraction noise. CSV is
# rounded to 6 decimals in the released source analysis; 1e-8 is finer than that.
tests=[]
diffrows=[]
rng=np.random.default_rng(20260906)
boot_indices=rng.integers(0,10,size=(20000,10))
for scheme in ["GC_filtered","GC_retained"]:
    for metric in ["median_ms","p99_ms"]:
        for link in plot.LINKS:
            frame=metrics[(metrics.scheme==scheme)&(metrics.link==link)]
            pairs=frame.pivot(index="participant",columns="scene",values=metric).sort_index()
            assert len(pairs)==10 and not pairs.isna().any().any()
            d=np.round(pairs.street.to_numpy()-pairs.supermarket.to_numpy(),8)
            orig_test=stats.exact_wilcoxon_signed_rank(pairs.street.to_numpy()-pairs.supermarket.to_numpy())
            result=stats.exact_wilcoxon_signed_rank(d)
            bootstrap=np.median(d[boot_indices],axis=1)
            ci=np.quantile(bootstrap,[.025,.975])
            absvals=np.abs(d[np.abs(d)>1e-12])
            tests.append(dict(scheme=scheme,metric=metric,link=link,pairs_n=10,nonzero_pairs_n=result["nonzero_pairs_n"],zero_pairs_n=int((np.abs(d)<=1e-12).sum()),absolute_difference_tie_groups_n=int((pd.Series(absvals).value_counts()>1).sum()),wilcoxon_w=result["wilcoxon_w"],exact_two_sided_p=result["exact_two_sided_p"],unrounded_exact_p=orig_test["exact_two_sided_p"],rank_biserial=result["rank_biserial"],paired_difference_median_ms=float(np.median(d)),paired_difference_q1_ms=float(np.quantile(d,.25)),paired_difference_q3_ms=float(np.quantile(d,.75)),paired_median_bootstrap_ci95_low_ms=float(ci[0]),paired_median_bootstrap_ci95_high_ms=float(ci[1]),bootstrap_replicates=20000,bootstrap_seed=20260906,bootstrap_method="percentile; resample 10 paired participants with replacement"))
            for p,x in zip(pairs.index,d):
                diffrows.append(dict(scheme=scheme,metric=metric,link=link,participant=p,street_minus_supermarket_ms=float(x)))
tests=pd.DataFrame(tests)
for scheme in tests.scheme.unique():
    idx=tests.index[tests.scheme==scheme]
    tests.loc[idx,"holm_all_eight_p"]=stats.holm_adjust(tests.loc[idx,"exact_two_sided_p"].tolist())
    for metric in tests.metric.unique():
        idx2=tests.index[(tests.scheme==scheme)&(tests.metric==metric)]
        tests.loc[idx2,"holm_four_paths_per_metric_p"]=stats.holm_adjust(tests.loc[idx2,"exact_two_sided_p"].tolist())
save(tests,"Paired_scene_tests_and_effects.csv")
save(pd.DataFrame(diffrows),"Paired_scene_individual_differences.csv")

# Direct comparison against the reproduction retains the original source's
# floating-point ranking. The transparently rounded test is the audit result.
repro=pd.read_csv(REPRO/"Participant_level_latency_metrics.csv")
filt=metrics[metrics.scheme=="GC_filtered"]
keys=["participant","scene","link"]
repro=repro.sort_values(keys).reset_index(drop=True)
filt=filt.sort_values(keys).reset_index(drop=True)
cols=["median_ms","p95_ms","p99_ms","max_ms","gt50_pct","retained_pct","valid_n"]
delta=float(np.nanmax(np.abs(repro[cols].to_numpy()-filt[cols].to_numpy())))
facts=dict(public_manifest_rows=len(manifest),unique_public_participant_ids=int(manifest.participant.nunique()),unique_original_participant_codes=len(all_original_codes),original_manifest_rows=len(private),unique_network_hashes=int(manifest.network_record_sha256.nunique()),direct_log_runs=int(pd.DataFrame(provenance).package_direct_log_available.sum()),heuristic_only_runs=int((~pd.DataFrame(provenance).package_direct_log_available).sum()),source_log_recovery_runs=int(((~pd.DataFrame(provenance).package_direct_log_available)&(pd.DataFrame(provenance).source_folder_logs_with_GC_in_window>0)).sum()),independent_recalculation_vs_reproduction_max_delta=delta,plotting_omitted=True,python=sys.version,numpy=np.__version__,pandas=pd.__version__)
(OUT/"Audit_facts.json").write_text(json.dumps(facts,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(facts,ensure_ascii=False,indent=2))
print(tests[["scheme","metric","link","exact_two_sided_p","holm_all_eight_p","paired_difference_median_ms","paired_median_bootstrap_ci95_low_ms","paired_median_bootstrap_ci95_high_ms"]].to_string(index=False))
