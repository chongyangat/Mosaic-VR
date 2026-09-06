from __future__ import annotations

import hashlib
import math
import re
import shutil
from dataclasses import dataclass
from pathlib import Path

import matplotlib as mpl
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


PACKAGE_ROOT = Path(__file__).resolve().parents[2]
ROOT = PACKAGE_ROOT / "05_Data" / "raw_pseudonymized" / "baseline"
OUTPUT = PACKAGE_ROOT / "05_Data" / "reproduced_output"
WINDOW_SECONDS = 300
GC_THRESHOLD_MS = 150.0
GC_COINCIDENCE_SECONDS = 0.010
GC_MERGE_SECONDS = 1.0
GC_EXCLUSION_SECONDS = 0.250
VALID_LATENCY_MAX_MS = 10_000.0

LINKS = ["EyeTracking→PC", "MoCap→PC", "MoCap→VR", "PC<->VR 状态同步"]
HIGH_RATE_LINKS = LINKS[:3]
PC_BOUND_LINKS = LINKS[:2]
STATE_SYNC_LINK = LINKS[3]
SCENES = ["supermarket", "street"]
PARTICIPANTS = [f"P{i}" for i in range(1, 11)]

LINK_LABELS = {
    "EyeTracking→PC": "Eye tracking to PC",
    "MoCap→PC": "MoCap to PC",
    "MoCap→VR": "MoCap to VR",
    "PC<->VR 状态同步": "PC–VR shared-state RTT",
}
SCENE_LABELS = {"supermarket": "Supermarket scene", "street": "Street scene"}
COLORS = {
    "EyeTracking→PC": "#0072B2",
    "MoCap→PC": "#D55E00",
    "MoCap→VR": "#009E73",
    "PC<->VR 状态同步": "#7B3294",
}


@dataclass(frozen=True)
class RunSpec:
    participant: str
    scene: str
    folder: str


RUNS = [
    RunSpec("P1", "supermarket", "P01/supermarket"),
    RunSpec("P1", "street", "P01/street"),
    RunSpec("P2", "supermarket", "P02/supermarket"),
    RunSpec("P2", "street", "P02/street"),
    RunSpec("P3", "supermarket", "P03/supermarket"),
    RunSpec("P3", "street", "P03/street"),
    RunSpec("P4", "supermarket", "P04/supermarket"),
    RunSpec("P4", "street", "P04/street"),
    RunSpec("P5", "supermarket", "P05/supermarket"),
    RunSpec("P5", "street", "P05/street"),
    RunSpec("P6", "supermarket", "P06/supermarket"),
    RunSpec("P6", "street", "P06/street"),
    RunSpec("P7", "supermarket", "P07/supermarket"),
    RunSpec("P7", "street", "P07/street"),
    RunSpec("P8", "supermarket", "P08/supermarket"),
    RunSpec("P8", "street", "P08/street"),
    RunSpec("P9", "supermarket", "P09/supermarket"),
    RunSpec("P9", "street", "P09/street"),
    RunSpec("P10", "supermarket", "P10/supermarket"),
    RunSpec("P10", "street", "P10/street"),
]


def sha256(path: Path | None) -> str:
    if path is None or not path.exists():
        return ""
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def find_csv(folder: Path) -> Path:
    files = sorted(folder.glob("NetworkPerformance*.csv"))
    if len(files) != 1:
        raise RuntimeError(f"Expected one NetworkPerformance CSV in {folder}, found {len(files)}")
    return files[0]


def find_gc_log(folder: Path) -> Path | None:
    archived = sorted(folder.glob("UnityLog*.log"))
    if archived:
        return archived[0]
    editor = folder / "Editor" / "Editor.log"
    return editor if editor.exists() else None


def read_network_csv(path: Path) -> pd.DataFrame:
    df = pd.read_csv(path, encoding="utf-8-sig", low_memory=False)
    required = {"采集时间", "链路", "延迟(毫秒)"}
    missing = required - set(df.columns)
    if missing:
        raise RuntimeError(f"Missing columns {sorted(missing)} in {path}")
    out = df.copy()
    out["采集时间"] = pd.to_datetime(out["采集时间"], errors="coerce")
    out["链路"] = out["链路"].astype(str).str.strip()
    out["延迟(毫秒)"] = pd.to_numeric(out["延迟(毫秒)"], errors="coerce")
    out = out[out["链路"].isin(LINKS) & out["采集时间"].notna()].copy()
    return out.sort_values("采集时间").reset_index(drop=True)


GC_TIME_RE = re.compile(
    r"\[GCMonitor\].*?时间=(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)"
)


def parse_gc_log(path: Path | None) -> list[pd.Timestamp]:
    if path is None:
        return []
    text = path.read_text(encoding="utf-8", errors="ignore")
    return sorted({pd.Timestamp(x) for x in GC_TIME_RE.findall(text)})


def merge_events(events: list[pd.Timestamp], merge_seconds: float) -> list[pd.Timestamp]:
    if not events:
        return []
    clusters: list[list[pd.Timestamp]] = [[events[0]]]
    for event in events[1:]:
        if (event - clusters[-1][-1]).total_seconds() <= merge_seconds:
            clusters[-1].append(event)
        else:
            clusters.append([event])
    merged = []
    for cluster in clusters:
        ns = np.array([x.value for x in cluster], dtype=np.int64)
        merged.append(pd.Timestamp(int(np.median(ns))))
    return merged


def detect_gc_heuristic(window_df: pd.DataFrame) -> list[pd.Timestamp]:
    eye = window_df[
        (window_df["链路"] == "EyeTracking→PC")
        & (window_df["延迟(毫秒)"] > GC_THRESHOLD_MS)
    ]["采集时间"].sort_values()
    mocap = window_df[
        (window_df["链路"] == "MoCap→PC")
        & (window_df["延迟(毫秒)"] > GC_THRESHOLD_MS)
    ]["采集时间"].sort_values()
    if eye.empty or mocap.empty:
        return []
    # Pandas 3 may preserve datetime64[us] when astype("int64") is used,
    # whereas Timestamp.value is always expressed in nanoseconds. Convert
    # explicitly through Timestamp.value so the 10-ms comparison is unit-safe.
    mocap_ns = np.array([x.value for x in mocap], dtype=np.int64)
    candidates: list[pd.Timestamp] = []
    limit_ns = int(GC_COINCIDENCE_SECONDS * 1e9)
    for t in eye:
        t_ns = t.value
        pos = int(np.searchsorted(mocap_ns, t_ns))
        nearest = []
        if pos < len(mocap_ns):
            nearest.append(mocap_ns[pos])
        if pos > 0:
            nearest.append(mocap_ns[pos - 1])
        if not nearest:
            continue
        m_ns = min(nearest, key=lambda x: abs(int(x) - t_ns))
        if abs(int(m_ns) - t_ns) <= limit_ns:
            candidates.append(pd.Timestamp((t_ns + int(m_ns)) // 2))
    return merge_events(sorted(candidates), GC_MERGE_SECONDS)


def nearest_delta_seconds(event: pd.Timestamp, events: list[pd.Timestamp]) -> float:
    if not events:
        return math.nan
    return min(abs((event - x).total_seconds()) for x in events)


def gc_exclusion_mask(times: pd.Series, events: list[pd.Timestamp]) -> pd.Series:
    mask = pd.Series(False, index=times.index)
    for event in events:
        mask |= (times - event).abs() <= pd.Timedelta(seconds=GC_EXCLUSION_SECONDS)
    return mask


def percentile(series: pd.Series, q: float) -> float:
    return float(np.nanpercentile(series.to_numpy(dtype=float), q)) if len(series) else math.nan


def latency_metrics(df: pd.DataFrame) -> dict[str, float | int]:
    values = df["延迟(毫秒)"].astype(float)
    times = df["采集时间"].sort_values()
    gaps = times.diff().dt.total_seconds().dropna()
    return {
        "valid_n": int(len(values)),
        "median_ms": float(values.median()),
        "mean_ms": float(values.mean()),
        "p95_ms": percentile(values, 95),
        "p99_ms": percentile(values, 99),
        "max_ms": float(values.max()),
        "gt50_n": int((values > 50).sum()),
        "gt50_pct": float((values > 50).mean() * 100),
        "max_gap_ms": float(gaps.max() * 1000) if len(gaps) else math.nan,
        "nominal_rate_hz": float(len(values) / WINDOW_SECONDS),
    }


def round_up(value: float, step: float, minimum: float) -> float:
    return max(minimum, math.ceil(value / step) * step)


def set_plot_style() -> None:
    mpl.rcParams.update(
        {
            "font.family": "sans-serif",
            "font.sans-serif": ["Arial", "DejaVu Sans"],
            "font.size": 8,
            "axes.labelsize": 8,
            "axes.titlesize": 8.5,
            "xtick.labelsize": 7,
            "ytick.labelsize": 7,
            "legend.fontsize": 7,
            "axes.linewidth": 0.7,
            "xtick.major.width": 0.7,
            "ytick.major.width": 0.7,
            "xtick.major.size": 3,
            "ytick.major.size": 3,
            "pdf.fonttype": 42,
            "ps.fonttype": 42,
            "savefig.bbox": "tight",
            "savefig.pad_inches": 0.03,
        }
    )


def export_figure(fig: plt.Figure, stem: Path) -> None:
    fig.savefig(stem.with_suffix(".png"), dpi=600, facecolor="white")
    fig.savefig(stem.with_suffix(".tiff"), dpi=600, facecolor="white", pil_kwargs={"compression": "tiff_lzw"})
    fig.savefig(stem.with_suffix(".pdf"), facecolor="white")
    fig.savefig(stem.with_suffix(".svg"), facecolor="white")


def draw_main_figure(
    scene: str,
    curves: pd.DataFrame,
    curve_ymax: float,
    state_ymax: float,
) -> None:
    fig, axes = plt.subplots(2, 2, figsize=(7.08, 5.35), constrained_layout=True)
    panels = ["(a)", "(b)", "(c)", "(d)"]
    participants = PARTICIPANTS

    for ax, link, panel in zip(axes.flat, LINKS, panels):
        sub = curves[(curves["scene"] == scene) & (curves["link"] == link)]
        for participant in participants:
            p = sub[sub["participant"] == participant]
            ax.plot(p["elapsed_s"], p["latency_ms"], color="#9E9E9E", lw=0.45, alpha=0.45, zorder=1)
        agg = sub[sub["participant"] == "aggregate"]
        ax.fill_between(
            agg["elapsed_s"].to_numpy(float),
            agg["q1_ms"].to_numpy(float),
            agg["q3_ms"].to_numpy(float),
            color=COLORS[link],
            alpha=0.18,
            linewidth=0,
            label="Across-participant IQR",
            zorder=2,
        )
        ax.plot(
            agg["elapsed_s"],
            agg["latency_ms"],
            color=COLORS[link],
            lw=1.15,
            label="Across-participant median",
            zorder=3,
        )
        ax.set_xlim(0, WINDOW_SECONDS)
        ax.set_ylim(0, state_ymax if link == STATE_SYNC_LINK else curve_ymax)
        ax.set_xticks(np.arange(0, WINDOW_SECONDS + 1, 60))
        ax.set_xlabel("Elapsed time (s)")
        ax.set_ylabel("Latency (ms)")
        ax.set_title(f"{panel} {LINK_LABELS[link]}", loc="left", fontweight="bold")
        ax.grid(axis="y", color="#E3E3E3", lw=0.5)
        ax.spines[["top", "right"]].set_visible(False)
        bin_note = "; 5-s medians" if link == STATE_SYNC_LINK else ""
        ax.text(
            0.98,
            0.94,
            f"n = {len(PARTICIPANTS)}{bin_note}",
            transform=ax.transAxes,
            ha="right",
            va="top",
            color="#4D4D4D",
            fontsize=7,
        )
    axes.flat[0].legend(loc="upper left", frameon=False, handlelength=2.2)

    fig.suptitle(SCENE_LABELS[scene], fontsize=9.5, fontweight="bold")
    stem = OUTPUT / f"Figure_{scene}_latency_stability"
    export_figure(fig, stem)
    plt.close(fig)


def draw_p99_figure(metrics: pd.DataFrame, p99_ymax: float) -> None:
    fig, axes = plt.subplots(1, 2, figsize=(7.08, 3.00), sharey=True, constrained_layout=True)
    participants = PARTICIPANTS
    tick_labels = ["Eye tracking\nto PC", "MoCap\nto PC", "MoCap\nto VR", "PC–VR\nshared-state RTT"]

    for panel_index, (ax, scene, panel) in enumerate(zip(axes, SCENES, ["(a)", "(b)"])):
        data = []
        for link in LINKS:
            ordered = (
                metrics[(metrics["scene"] == scene) & (metrics["link"] == link)]
                .set_index("participant")
                .reindex(participants)["p99_ms"]
                .to_numpy(float)
            )
            data.append(ordered)
        bp = ax.boxplot(
            data,
            widths=0.52,
            patch_artist=True,
            showfliers=False,
            medianprops={"color": "#202020", "lw": 1.0},
            whiskerprops={"lw": 0.9},
            capprops={"lw": 0.9},
        )
        for box, link in zip(bp["boxes"], LINKS):
            box.set(facecolor=COLORS[link], alpha=0.22, edgecolor=COLORS[link], linewidth=0.9)
        rng = np.random.default_rng(20260820 + panel_index)
        for x, (vals, link) in enumerate(zip(data, LINKS), start=1):
            jitter = rng.uniform(-0.075, 0.075, len(vals))
            ax.scatter(
                np.full(len(vals), x) + jitter,
                vals,
                s=14,
                color=COLORS[link],
                edgecolor="white",
                linewidth=0.35,
                zorder=3,
            )
        ax.set_xticks([1, 2, 3, 4], tick_labels)
        ax.set_ylim(0, p99_ymax)
        ax.set_title(f"{panel} {SCENE_LABELS[scene]}", loc="left", fontweight="bold")
        ax.grid(axis="y", color="#E3E3E3", lw=0.5)
        ax.spines[["top", "right"]].set_visible(False)
        ax.text(0.98, 0.96, f"n = {len(PARTICIPANTS)}", transform=ax.transAxes, ha="right", va="top", color="#4D4D4D", fontsize=7)
    axes[0].set_ylabel("Participant-level P99 latency (ms)")
    fig.suptitle("Participant-level tail latency by scene", fontsize=9.5, fontweight="bold")
    stem = OUTPUT / "Figure_S_participant_P99_by_scene"
    export_figure(fig, stem)
    plt.close(fig)


def draw_median_figure(metrics: pd.DataFrame, median_ymax: float) -> None:
    """Plot participant-level median latency distributions for both scenes."""
    fig, axes = plt.subplots(1, 2, figsize=(7.08, 3.00), sharey=True, constrained_layout=True)
    participants = PARTICIPANTS
    tick_labels = ["Eye tracking\nto PC", "MoCap\nto PC", "MoCap\nto VR", "PC–VR\nshared-state RTT"]

    for panel_index, (ax, scene, panel) in enumerate(zip(axes, SCENES, ["(a)", "(b)"])):
        data = []
        for link in LINKS:
            ordered = (
                metrics[(metrics["scene"] == scene) & (metrics["link"] == link)]
                .set_index("participant")
                .reindex(participants)["median_ms"]
                .to_numpy(float)
            )
            data.append(ordered)
        bp = ax.boxplot(
            data,
            widths=0.52,
            patch_artist=True,
            showfliers=False,
            medianprops={"color": "#202020", "lw": 1.0},
            whiskerprops={"lw": 0.9},
            capprops={"lw": 0.9},
        )
        for box, link in zip(bp["boxes"], LINKS):
            box.set(facecolor=COLORS[link], alpha=0.22, edgecolor=COLORS[link], linewidth=0.9)
        rng = np.random.default_rng(20260821 + panel_index)
        for x, (vals, link) in enumerate(zip(data, LINKS), start=1):
            jitter = rng.uniform(-0.075, 0.075, len(vals))
            ax.scatter(
                np.full(len(vals), x) + jitter,
                vals,
                s=14,
                color=COLORS[link],
                edgecolor="white",
                linewidth=0.35,
                zorder=3,
            )
        ax.set_xticks([1, 2, 3, 4], tick_labels)
        ax.set_ylim(0, median_ymax)
        ax.set_title(f"{panel} {SCENE_LABELS[scene]}", loc="left", fontweight="bold")
        ax.grid(axis="y", color="#E3E3E3", lw=0.5)
        ax.spines[["top", "right"]].set_visible(False)
        ax.text(
            0.98,
            0.96,
            f"n = {len(PARTICIPANTS)}",
            transform=ax.transAxes,
            ha="right",
            va="top",
            color="#4D4D4D",
            fontsize=7,
        )
    axes[0].set_ylabel("Participant-level median latency (ms)")
    fig.suptitle("Participant-level median latency by scene", fontsize=9.5, fontweight="bold")
    stem = OUTPUT / "Figure_participant_median_by_scene"
    export_figure(fig, stem)
    plt.close(fig)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    set_plot_style()

    manifest_rows: list[dict] = []
    gc_rows: list[dict] = []
    cleaning_rows: list[dict] = []
    metric_rows: list[dict] = []
    curve_frames: list[pd.DataFrame] = []
    checksum_rows: list[dict] = []
    cleaned_by_run: dict[tuple[str, str], pd.DataFrame] = {}

    for spec in RUNS:
        folder = ROOT / spec.folder
        csv_path = find_csv(folder)
        log_path = find_gc_log(folder)
        df = read_network_csv(csv_path)
        starts = df.groupby("链路")["采集时间"].min()
        ends = df.groupby("链路")["采集时间"].max()
        if set(starts.index) != set(LINKS):
            raise RuntimeError(f"Not all four links are present in {csv_path}")
        common_start = starts.max()
        common_end = ends.min()
        common_duration = (common_end - common_start).total_seconds()
        if common_duration < WINDOW_SECONDS:
            raise RuntimeError(f"Common duration {common_duration:.3f}s is below {WINDOW_SECONDS}s in {csv_path}")
        analysis_end = common_start + pd.Timedelta(seconds=WINDOW_SECONDS)
        window = df[(df["采集时间"] >= common_start) & (df["采集时间"] < analysis_end)].copy()
        window["elapsed_s"] = (window["采集时间"] - common_start).dt.total_seconds()

        heuristic_events = detect_gc_heuristic(window)
        logged_all = parse_gc_log(log_path)
        logged_events = [x for x in logged_all if common_start <= x < analysis_end]
        if log_path is not None:
            gc_events = logged_events
            gc_source = "GCMonitor log; cross-link heuristic corroboration"
            if len(gc_events) != len(heuristic_events):
                raise RuntimeError(
                    f"GC log/heuristic count mismatch in {spec.folder}: {len(gc_events)} vs {len(heuristic_events)}"
                )
            # GCMonitor writes after the collection frame. Most matches differ by
            # only a few milliseconds; one long GC frame shifts the log timestamp
            # by 158 ms. The same ±250-ms window used for exclusion is therefore
            # the conservative, predeclared audit tolerance.
            if any(nearest_delta_seconds(x, heuristic_events) > GC_EXCLUSION_SECONDS for x in gc_events):
                raise RuntimeError(f"GC log/heuristic timing mismatch exceeds 250 ms in {spec.folder}")
        else:
            gc_events = heuristic_events
            gc_source = "Validated cross-link heuristic; session log unavailable"

        for idx, event in enumerate(gc_events, start=1):
            delta = nearest_delta_seconds(event, heuristic_events)
            gc_rows.append(
                {
                    "participant": spec.participant,
                    "scene": spec.scene,
                    "folder": spec.folder,
                    "event_id": idx,
                    "gc_event_time": event.isoformat(sep=" ", timespec="milliseconds"),
                    "elapsed_s": (event - common_start).total_seconds(),
                    "gc_source": gc_source,
                    "nearest_heuristic_delta_ms": delta * 1000 if not math.isnan(delta) else math.nan,
                    "heuristic_match_within_250ms": bool(
                        not math.isnan(delta) and delta <= GC_EXCLUSION_SECONDS
                    ),
                }
            )

        window["gc_excluded"] = False
        for link in PC_BOUND_LINKS:
            idx = window.index[window["链路"] == link]
            window.loc[idx, "gc_excluded"] = gc_exclusion_mask(window.loc[idx, "采集时间"], gc_events)
        window["invalid_latency"] = (
            window["延迟(毫秒)"].isna()
            | (window["延迟(毫秒)"] < 0)
            | (window["延迟(毫秒)"] > VALID_LATENCY_MAX_MS)
        )
        cleaned = window[~window["gc_excluded"] & ~window["invalid_latency"]].copy()
        cleaned_by_run[(spec.participant, spec.scene)] = cleaned

        for link in LINKS:
            raw_link = window[window["链路"] == link]
            clean_link = cleaned[cleaned["链路"] == link]
            gc_n = int(raw_link["gc_excluded"].sum())
            invalid_n = int((~raw_link["gc_excluded"] & raw_link["invalid_latency"]).sum())
            cleaning_rows.append(
                {
                    "participant": spec.participant,
                    "scene": spec.scene,
                    "folder": spec.folder,
                    "link": link,
                    "window_samples_n": int(len(raw_link)),
                    "gc_excluded_n": gc_n,
                    "gc_excluded_pct": gc_n / len(raw_link) * 100 if len(raw_link) else math.nan,
                    "invalid_excluded_n": invalid_n,
                    "invalid_excluded_pct": invalid_n / len(raw_link) * 100 if len(raw_link) else math.nan,
                    "retained_n": int(len(clean_link)),
                    "retained_pct": len(clean_link) / len(raw_link) * 100 if len(raw_link) else math.nan,
                }
            )
            row = {
                "participant": spec.participant,
                "scene": spec.scene,
                "folder": spec.folder,
                "link": link,
                "window_samples_n": int(len(raw_link)),
                "gc_excluded_n": gc_n,
                "invalid_excluded_n": invalid_n,
            }
            row.update(latency_metrics(clean_link))
            metric_rows.append(row)

        manifest_rows.append(
            {
                "participant": spec.participant,
                "scene": spec.scene,
                "folder": spec.folder,
                "raw_csv": csv_path.relative_to(PACKAGE_ROOT).as_posix(),
                "unity_log": log_path.relative_to(PACKAGE_ROOT).as_posix() if log_path else "",
                "common_start": common_start.isoformat(sep=" ", timespec="milliseconds"),
                "common_end": common_end.isoformat(sep=" ", timespec="milliseconds"),
                "common_duration_s": common_duration,
                "analysis_window_s": WINDOW_SECONDS,
                "gc_source": gc_source,
                "gc_events_used": len(gc_events),
                "heuristic_events": len(heuristic_events),
                "raw_csv_sha256": sha256(csv_path),
                "unity_log_sha256": sha256(log_path),
            }
        )
        checksum_rows.append(
            {"file": csv_path.relative_to(PACKAGE_ROOT).as_posix(), "sha256": sha256(csv_path)}
        )
        if log_path is not None:
            checksum_rows.append(
                {"file": log_path.relative_to(PACKAGE_ROOT).as_posix(), "sha256": sha256(log_path)}
            )

    metrics = pd.DataFrame(metric_rows)
    manifest = pd.DataFrame(manifest_rows)
    gc_audit = pd.DataFrame(gc_rows)
    cleaning_audit = pd.DataFrame(cleaning_rows)
    metrics = metrics.merge(
        cleaning_audit[["participant", "scene", "link", "retained_pct"]],
        on=["participant", "scene", "link"],
        how="left",
        validate="one_to_one",
    )

    for scene in SCENES:
        for link in LINKS:
            bin_seconds = 5 if link == STATE_SYNC_LINK else 1
            grid = np.arange(bin_seconds / 2, WINDOW_SECONDS, bin_seconds)
            participant_series = {}
            for participant in PARTICIPANTS:
                data = cleaned_by_run[(participant, scene)]
                data = data[data["链路"] == link].copy()
                data["bin"] = np.floor(data["elapsed_s"] / bin_seconds).astype(int)
                med = data.groupby("bin")["延迟(毫秒)"].median()
                med = med.reindex(range(len(grid)))
                participant_series[participant] = med.to_numpy(float)
                curve_frames.append(
                    pd.DataFrame(
                        {
                            "scene": scene,
                            "link": link,
                            "bin_seconds": bin_seconds,
                            "participant": participant,
                            "elapsed_s": grid,
                            "latency_ms": med.to_numpy(float),
                            "q1_ms": np.nan,
                            "q3_ms": np.nan,
                            "n_available": np.where(med.notna(), 1, 0),
                        }
                    )
                )
            matrix = np.column_stack([participant_series[participant] for participant in PARTICIPANTS])
            with np.errstate(all="ignore"):
                agg_median = np.nanmedian(matrix, axis=1)
                agg_q1 = np.nanpercentile(matrix, 25, axis=1)
                agg_q3 = np.nanpercentile(matrix, 75, axis=1)
            curve_frames.append(
                pd.DataFrame(
                    {
                        "scene": scene,
                        "link": link,
                        "bin_seconds": bin_seconds,
                        "participant": "aggregate",
                        "elapsed_s": grid,
                        "latency_ms": agg_median,
                        "q1_ms": agg_q1,
                        "q3_ms": agg_q3,
                        "n_available": np.sum(~np.isnan(matrix), axis=1),
                    }
                )
            )

    curves = pd.concat(curve_frames, ignore_index=True)

    summary_rows = []
    metric_names = [
        "median_ms",
        "mean_ms",
        "p95_ms",
        "p99_ms",
        "max_ms",
        "gt50_pct",
        "max_gap_ms",
        "nominal_rate_hz",
        "gc_excluded_n",
        "invalid_excluded_n",
        "retained_pct",
    ]
    for (scene, link), group in metrics.groupby(["scene", "link"], sort=False):
        for metric in metric_names:
            vals = group[metric].astype(float)
            summary_rows.append(
                {
                    "scene": scene,
                    "link": link,
                    "metric": metric,
                    "participants_n": int(vals.notna().sum()),
                    "median": float(vals.median()),
                    "q1": float(vals.quantile(0.25)),
                    "q3": float(vals.quantile(0.75)),
                    "minimum": float(vals.min()),
                    "maximum": float(vals.max()),
                }
            )
    scene_summary = pd.DataFrame(summary_rows)
    table5_summary = scene_summary[
        scene_summary["metric"].isin(
            ["median_ms", "p95_ms", "p99_ms", "gt50_pct", "retained_pct"]
        )
    ].copy()

    paired_rows = []
    for participant in PARTICIPANTS:
        for link in LINKS:
            for metric in ["median_ms", "p95_ms", "p99_ms", "gt50_pct"]:
                sup = metrics[(metrics["participant"] == participant) & (metrics["scene"] == "supermarket") & (metrics["link"] == link)][metric].iloc[0]
                street = metrics[(metrics["participant"] == participant) & (metrics["scene"] == "street") & (metrics["link"] == link)][metric].iloc[0]
                paired_rows.append(
                    {
                        "participant": participant,
                        "link": link,
                        "metric": metric,
                        "supermarket": sup,
                        "street": street,
                        "street_minus_supermarket": street - sup,
                    }
                )
    paired = pd.DataFrame(paired_rows)

    main_curve_values = curves[(curves["link"].isin(HIGH_RATE_LINKS)) & (curves["participant"] != "aggregate")]["latency_ms"].dropna()
    # Use the complete range of one-second participant medians so that the
    # retained, non-GC tail events remain visible rather than being clipped.
    curve_ymax = round_up(float(main_curve_values.max()) * 1.05, 10, 40)
    # The supplementary P99 figure contains all four links.  Use one shared
    # scale across scenes so the two panels can be compared directly.
    p99_ymax = round_up(float(metrics["p99_ms"].max()) * 1.10, 10, 60)
    # The participant-level median figure uses the same link order and a shared
    # scale across scenes.  Starting the axis at zero keeps the absolute range
    # visible while the overlaid points preserve the participant distribution.
    median_ymax = round_up(float(metrics["median_ms"].max()) * 1.10, 5, 35)
    state_values = curves[(curves["link"] == STATE_SYNC_LINK) & (curves["participant"] != "aggregate")]["latency_ms"].dropna()
    state_ymax = round_up(float(state_values.max()) * 1.05, 10, 50)

    for scene in SCENES:
        draw_main_figure(scene, curves, curve_ymax, state_ymax)
    draw_p99_figure(metrics, p99_ymax)
    draw_median_figure(metrics, median_ymax)

    # These former standalone time-series figures are superseded by panel (d)
    # in each scene-specific main figure.
    for scene in SCENES:
        for suffix in [".png", ".tiff", ".pdf", ".svg"]:
            (OUTPUT / f"Figure_S_{scene}_state_sync{suffix}").unlink(missing_ok=True)

    outputs = {
        "Run_manifest.csv": manifest,
        "GC_event_audit.csv": gc_audit,
        "Cleaning_audit.csv": cleaning_audit,
        "Participant_level_latency_metrics.csv": metrics,
        "Scene_latency_summary.csv": scene_summary,
        "Table5_link_latency_summary.csv": table5_summary,
        "Paired_scene_difference.csv": paired,
        "Figure_source_latency_curves.csv": curves,
        "Input_file_SHA256.csv": pd.DataFrame(checksum_rows),
    }
    for name, frame in outputs.items():
        frame.to_csv(OUTPUT / name, index=False, encoding="utf-8-sig", float_format="%.6f")

    script_target = OUTPUT / "plot_latency_stability_10participants_two_scenes.py"
    source_script = Path(__file__).resolve()
    if source_script != script_target.resolve():
        shutil.copy2(source_script, script_target)

    gc_counts = manifest.groupby("scene")["gc_events_used"].agg(["sum", "median", "min", "max"])
    clean_rates = cleaning_audit.groupby(["scene", "link"])["retained_pct"].median()
    readme = f"""# 十参与者双场景链路稳定性分析（统一300 s）

## 分析范围

- 参与者：P1–P10；每位参与者分别完成超市与街道场景，共20次记录。
- 为保证场景与参与者之间可直接比较，从四条链路共同开始时刻起截取所有记录均完整覆盖的300 s，不补零、不外推。
- 原始CSV和Unity日志保持不变；本文件夹只保存复现代码、审计表、统计结果和论文图。

## GC过滤规则

1. 有GCMonitor日志的15次记录直接使用日志时间；日志事件均由旧图9采用的跨链路规则复核：EyeTracking→PC与MoCap→PC在10 ms内同时超过150 ms，并将1 s内候选合并为一次事件。
2. 无日志的5次记录使用上述已验证跨链路规则回溯识别GC事件。
3. 仅删除GC事件前后各250 ms内的EyeTracking→PC和MoCap→PC样本；MoCap→VR与PC–VR shared-state RTT不受该规则删除。
4. 另排除负延迟、缺失值或超过10,000 ms的无效记录。GC窗口外的高延迟样本全部保留，不进行统计离群值裁剪。

## 统计与绘图

- 主图(a–c)中的三条高频链路使用1 s参与者内中位数；主图(d)中的PC–VR shared-state RTT因采样语义与频率不同，使用5 s参与者内中位数。
- 彩色实线和阴影分别表示十名参与者的逐时中位数及四分位距；灰线表示单个参与者。
- 参与者级P99延迟分布移至一张左右并列的补充图，左侧为超市、右侧为街道；其中保留全部四条链路。PC–VR shared-state RTT反映双向状态更新完成时间，其P99仅作描述性展示，不与三条单向高频链路作等价解释。
- 参与者级中位延迟另以双场景箱线图汇总，箱体表示四分位距、中心线表示跨参与者中位数，散点保留每名参与者的观测值。
- 所有坐标轴从0开始，超市与街道使用完全一致的纵轴范围。
- 输出PNG/TIFF均为600 dpi，并同时提供PDF/SVG矢量版本。

## GC事件数（300 s窗口）

```
{gc_counts.to_string(float_format=lambda x: f'{x:.1f}')}
```

## 复现

使用安装了pandas、numpy、matplotlib和Pillow的Python环境运行：

```powershell
python .\\plot_latency_stability_10participants_two_scenes.py
```

代码读取 submission 包内 `05_Data/raw_pseudonymized/baseline` 下的20个匿名化记录目录；输入文件哈希见 `Input_file_SHA256.csv`。
"""
    (OUTPUT / "README.md").write_text(readme, encoding="utf-8")

    captions = f"""【建议方法描述】
为比较两类场景下通信链路的稳定性，从四条链路同时有效的起点截取每次记录均完整覆盖的300 s。Unity GCMonitor日志可用时，直接采用日志中的GC时间；其余记录采用经日志验证的跨链路判据识别GC，即EyeTracking→PC与MoCap→PC延迟在10 ms内同时超过150 ms，并将1 s内的相邻候选合并为一次事件。仅剔除GC事件前后各250 ms内两条PC入站链路的样本；MoCap→VR与PC–VR shared-state RTT不作GC删除。负值、缺失值和大于10,000 ms的记录作为无效样本排除，其他高延迟观测均予保留。三条高频链路按1 s计算参与者内中位数，PC–VR shared-state RTT按5 s计算中位数；场景级曲线以十名参与者的中位数和四分位距表示。

【超市场景主图图注】
Figure X. Communication-link stability during the supermarket scene over a common 300-s analysis window. (a–c) One-second within-participant median latency for Eye tracking to PC, MoCap to PC, and MoCap to VR, respectively. (d) Five-second within-participant latency for PC–VR shared-state RTT. Gray lines denote individual participants; colored lines and bands denote the across-participant median and interquartile range (n = 10). GC-associated samples were excluded only from the two PC-bound links within ±250 ms of each identified event; non-GC tail observations were retained.

【街道场景主图图注】
Figure Y. Communication-link stability during the street scene over a common 300-s analysis window. Panel definitions and filtering rules are identical to those in Figure X, enabling direct comparison between scenes (n = 10).

【参与者级P99补充图图注】
Figure S. Participant-level P99 latency distributions in the (a) supermarket and (b) street scenes. Boxes indicate the interquartile range with the median line, and points denote individual participants (n = 10). All four links are shown. PC–VR shared-state RTT P99 is reported descriptively because it represents bidirectional state-update completion and uses a different sampling and aggregation scheme from the three high-rate unidirectional links.

【参与者级中位延迟图图注】
Figure Z. Participant-level median latency distributions in the (a) supermarket and (b) street scenes over the common 300-s analysis window. Boxes indicate the interquartile range, horizontal lines indicate the across-participant median, whiskers extend to 1.5 times the interquartile range, and points denote individual participants (n = 10). All four links are shown. PC–VR shared-state RTT is interpreted descriptively within its own RTT-based timing semantics and is not ranked directly against the three estimated one-way links.

【统一作图纵轴】
主图(a–c)：0–{curve_ymax:.0f} ms；主图(d)：0–{state_ymax:.0f} ms；参与者级P99补充图：0–{p99_ymax:.0f} ms；参与者级中位延迟图：0–{median_ymax:.0f} ms。
"""
    (OUTPUT / "Methods_and_figure_captions.txt").write_text(captions, encoding="utf-8")

    print(f"OUTPUT={OUTPUT}")
    print(
        f"curve_ymax={curve_ymax:.1f}, p99_ymax={p99_ymax:.1f}, "
        f"median_ymax={median_ymax:.1f}, state_ymax={state_ymax:.1f}"
    )
    print("GC counts by scene:")
    print(gc_counts)
    print("Median retained percentages:")
    print(clean_rates)
    print("Participant-level medians and P99 by scene/link:")
    print(
        metrics.groupby(["scene", "link"])[["median_ms", "p99_ms", "gt50_pct"]]
        .median()
        .round(3)
        .to_string()
    )


if __name__ == "__main__":
    main()
