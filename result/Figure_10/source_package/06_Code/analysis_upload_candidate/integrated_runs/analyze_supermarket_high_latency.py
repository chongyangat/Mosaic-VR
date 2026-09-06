from __future__ import annotations

import os
from pathlib import Path

import pandas as pd


csv_path = Path(os.environ["MOSAIC_NETWORK_CSV"])
video_record_start_s = float(os.environ.get("MOSAIC_VIDEO_RECORD_START_S", "13.63333"))
threshold_ms = float(os.environ.get("MOSAIC_LATENCY_THRESHOLD_MS", "50"))

time_col = "采集时间"
link_col = "链路"
latency_col = "延迟(毫秒)"
expected_links = [
    "EyeTracking→PC",
    "MoCap→PC",
    "MoCap→VR",
    "PC<->VR 状态同步",
]

df = pd.read_csv(csv_path)
df[time_col] = pd.to_datetime(df[time_col], errors="coerce")
df[latency_col] = pd.to_numeric(df[latency_col], errors="coerce")
df = df.dropna(subset=[time_col, link_col, latency_col]).sort_values(time_col)
t0 = df[time_col].min()
df["elapsed_s"] = (df[time_col] - t0).dt.total_seconds()

last_time_by_link = df[df[link_col].isin(expected_links)].groupby(link_col)[time_col].max()
cutoff_time = last_time_by_link.min()
cutoff_s = float((cutoff_time - t0).total_seconds())
truncated = df[df[time_col] <= cutoff_time].copy()

eye_peak_times = (
    truncated[
        (truncated[link_col] == "EyeTracking→PC")
        & (truncated[latency_col] > 150.0)
    ][time_col]
    .drop_duplicates()
    .sort_values()
)
mocap_pc_peak_times = (
    truncated[
        (truncated[link_col] == "MoCap→PC")
        & (truncated[latency_col] > 150.0)
    ][time_col]
    .drop_duplicates()
    .sort_values()
)
candidate_events: list[pd.Timestamp] = []
for eye_time in eye_peak_times:
    if not mocap_pc_peak_times.empty and (
        (mocap_pc_peak_times - eye_time).abs().min() <= pd.Timedelta(milliseconds=10)
    ):
        candidate_events.append(eye_time)

gc_events: list[pd.Timestamp] = []
for event_time in sorted(candidate_events):
    if not gc_events or event_time - gc_events[-1] > pd.Timedelta(seconds=1):
        gc_events.append(event_time)

gc_mask = pd.Series(False, index=truncated.index)
for event_time in gc_events:
    gc_mask |= (
        truncated[link_col].isin({"EyeTracking→PC", "MoCap→PC"})
        & (truncated[time_col].sub(event_time).abs() <= pd.Timedelta(milliseconds=250))
    )
clean = truncated.loc[~gc_mask].copy()

print(f"CSV_START={t0}")
print(f"CUTOFF={cutoff_time}|elapsed_s={cutoff_s:.3f}")
print(f"VIDEO_RECORD_START_S={video_record_start_s:.5f}")
print("GC_EVENTS=" + ",".join(f"{(t - t0).total_seconds():.3f}" for t in gc_events))

for link in expected_links:
    group = clean[clean[link_col] == link]
    above = group[latency_col] > threshold_ms
    print(
        f"STATS|{link}|rows={len(group)}|max={group[latency_col].max():.3f}|"
        f"p95={group[latency_col].quantile(.95):.3f}|p99={group[latency_col].quantile(.99):.3f}|"
        f"above_n={int(above.sum())}|above_pct={100.0 * above.mean():.4f}"
    )


def build_link_episodes(link_frame: pd.DataFrame, max_gap_s: float = 0.250) -> list[dict[str, object]]:
    high = link_frame[link_frame[latency_col] > threshold_ms].sort_values("elapsed_s")
    episodes: list[dict[str, object]] = []
    current: dict[str, object] | None = None
    for _, row in high.iterrows():
        t = float(row["elapsed_s"])
        value = float(row[latency_col])
        if current is None or t - float(current["end_s"]) > max_gap_s:
            if current is not None:
                episodes.append(current)
            current = {
                "start_s": t,
                "end_s": t,
                "count": 1,
                "max_ms": value,
                "sum_ms": value,
            }
        else:
            current["end_s"] = t
            current["count"] = int(current["count"]) + 1
            current["max_ms"] = max(float(current["max_ms"]), value)
            current["sum_ms"] = float(current["sum_ms"]) + value
    if current is not None:
        episodes.append(current)
    return episodes


for link in ["MoCap→PC", "MoCap→VR"]:
    episodes = build_link_episodes(clean[clean[link_col] == link])
    episodes = [e for e in episodes if int(e["count"]) >= 5 or float(e["end_s"]) - float(e["start_s"]) >= 0.10]
    episodes.sort(key=lambda e: (float(e["end_s"]) - float(e["start_s"]), int(e["count"])), reverse=True)
    for e in episodes:
        start_s = float(e["start_s"])
        end_s = float(e["end_s"])
        print(
            f"LINK_EPISODE|{link}|csv={start_s:.3f}-{end_s:.3f}|"
            f"video={video_record_start_s + start_s:.3f}-{video_record_start_s + end_s:.3f}|"
            f"duration={end_s - start_s:.3f}|n={int(e['count'])}|"
            f"mean={float(e['sum_ms']) / int(e['count']):.3f}|max={float(e['max_ms']):.3f}"
        )

# Cross-link episodes merge high-latency MoCap records on either destination.
# A 250 ms gap threshold captures a coherent processing/navigation event while
# keeping temporally distinct events separate.
mocap_high = clean[
    clean[link_col].isin(["MoCap→PC", "MoCap→VR"])
    & (clean[latency_col] > threshold_ms)
].sort_values("elapsed_s")

cross_episodes: list[dict[str, object]] = []
current: dict[str, object] | None = None
for _, row in mocap_high.iterrows():
    t = float(row["elapsed_s"])
    value = float(row[latency_col])
    link = str(row[link_col])
    if current is None or t - float(current["end_s"]) > 0.250:
        if current is not None:
            cross_episodes.append(current)
        current = {
            "start_s": t,
            "end_s": t,
            "count": 1,
            "max_ms": value,
            "sum_ms": value,
            "links": {link},
        }
    else:
        current["end_s"] = t
        current["count"] = int(current["count"]) + 1
        current["max_ms"] = max(float(current["max_ms"]), value)
        current["sum_ms"] = float(current["sum_ms"]) + value
        current["links"].add(link)
if current is not None:
    cross_episodes.append(current)

qualified = [
    e
    for e in cross_episodes
    if int(e["count"]) >= 10 or float(e["end_s"]) - float(e["start_s"]) >= 0.25
]
qualified.sort(key=lambda e: (float(e["start_s"]), float(e["end_s"])))
for i, e in enumerate(qualified, start=1):
    start_s = float(e["start_s"])
    end_s = float(e["end_s"])
    print(
        f"CROSS_EPISODE|{i}|csv={start_s:.3f}-{end_s:.3f}|"
        f"video={video_record_start_s + start_s:.3f}-{video_record_start_s + end_s:.3f}|"
        f"duration={end_s - start_s:.3f}|n={int(e['count'])}|"
        f"mean={float(e['sum_ms']) / int(e['count']):.3f}|max={float(e['max_ms']):.3f}|"
        f"links={'+'.join(sorted(e['links']))}"
    )

# One-second bins support a robust ranking of sustained high-latency activity.
bin_frame = mocap_high.copy()
bin_frame["bin_s"] = bin_frame["elapsed_s"].astype(int)
ranked = (
    bin_frame.groupby("bin_s")
    .agg(high_records=(latency_col, "size"), max_ms=(latency_col, "max"), mean_ms=(latency_col, "mean"))
    .reset_index()
    .sort_values(["high_records", "max_ms"], ascending=False)
    .head(25)
)
for _, row in ranked.iterrows():
    bin_s = int(row["bin_s"])
    print(
        f"TOP_BIN|csv={bin_s}-{bin_s + 1}|video={video_record_start_s + bin_s:.3f}-"
        f"{video_record_start_s + bin_s + 1:.3f}|n={int(row['high_records'])}|"
        f"mean={row['mean_ms']:.3f}|max={row['max_ms']:.3f}"
    )
