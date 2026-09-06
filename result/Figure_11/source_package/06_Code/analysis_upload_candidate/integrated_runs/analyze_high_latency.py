from __future__ import annotations

import os
from pathlib import Path

import numpy as np
import pandas as pd


csv_path = Path(os.environ["MOSAIC_NETWORK_CSV"])
threshold_ms = float(os.environ.get("MOSAIC_LATENCY_THRESHOLD_MS", "50"))
video_record_start_s = float(os.environ.get("MOSAIC_VIDEO_RECORD_START_S", "14.26"))

df = pd.read_csv(csv_path)
df["采集时间"] = pd.to_datetime(df["采集时间"], errors="coerce")
df["延迟(毫秒)"] = pd.to_numeric(df["延迟(毫秒)"], errors="coerce")
df = df.dropna(subset=["采集时间", "链路", "延迟(毫秒)"]).sort_values("采集时间")
global_start = df["采集时间"].min()
df["csv_offset_s"] = (df["采集时间"] - global_start).dt.total_seconds()

print(f"CSV_START={global_start}")
print(f"VIDEO_RECORD_START_S={video_record_start_s:.3f}")
print(f"THRESHOLD_MS={threshold_ms:.3f}")

all_runs: list[dict[str, object]] = []
for link, group in df.groupby("链路", sort=True):
    group = group.sort_values("采集时间").reset_index(drop=True)
    above = group["延迟(毫秒)"] > threshold_ms
    print(
        f"LINK_STATS|{link}|rows={len(group)}|above={int(above.sum())}|"
        f"pct={100.0*above.mean():.4f}|max_ms={group['延迟(毫秒)'].max():.3f}|"
        f"p95_ms={group['延迟(毫秒)'].quantile(0.95):.3f}|"
        f"p99_ms={group['延迟(毫秒)'].quantile(0.99):.3f}"
    )

    # Strict runs: every consecutive record in the run is above threshold.
    run_id = (above != above.shift(fill_value=False)).cumsum()
    for _, run in group[above].groupby(run_id[above]):
        first = run.iloc[0]
        last = run.iloc[-1]
        start_offset = float(first["csv_offset_s"])
        end_offset = float(last["csv_offset_s"])
        all_runs.append(
            {
                "link": link,
                "count": len(run),
                "start": first["采集时间"],
                "end": last["采集时间"],
                "start_offset_s": start_offset,
                "end_offset_s": end_offset,
                "duration_s": end_offset - start_offset,
                "video_start_s": video_record_start_s + start_offset,
                "video_end_s": video_record_start_s + end_offset,
                "max_ms": float(run["延迟(毫秒)"].max()),
                "mean_ms": float(run["延迟(毫秒)"].mean()),
            }
        )

runs = pd.DataFrame(all_runs)
if runs.empty:
    raise SystemExit("NO_RUNS")

for link, group in runs.groupby("link", sort=True):
    qualified = group[(group["count"] >= 5) | (group["duration_s"] >= 0.10)]
    print(f"RUN_COUNTS|{link}|all={len(group)}|qualified={len(qualified)}")

qualified = runs[(runs["count"] >= 5) | (runs["duration_s"] >= 0.10)].copy()
qualified = qualified.sort_values(["duration_s", "count", "max_ms"], ascending=False)
for _, row in qualified.head(80).iterrows():
    print(
        "RUN|"
        f"{row['link']}|count={int(row['count'])}|start={row['start']}|end={row['end']}|"
        f"csv_start_s={row['start_offset_s']:.3f}|csv_end_s={row['end_offset_s']:.3f}|"
        f"duration_s={row['duration_s']:.3f}|video_start_s={row['video_start_s']:.3f}|"
        f"video_end_s={row['video_end_s']:.3f}|mean_ms={row['mean_ms']:.3f}|max_ms={row['max_ms']:.3f}"
    )

# Event-level episodes: merge strict runs separated by no more than 250 ms.
episodes: list[dict[str, object]] = []
for link, link_runs in runs.sort_values("start_offset_s").groupby("link", sort=True):
    current: dict[str, object] | None = None
    for _, row in link_runs.iterrows():
        item = row.to_dict()
        if current is None:
            current = item
            current["strict_runs"] = 1
            continue
        gap_s = float(item["start_offset_s"]) - float(current["end_offset_s"])
        if gap_s <= 0.250:
            current["end"] = item["end"]
            current["end_offset_s"] = item["end_offset_s"]
            current["video_end_s"] = item["video_end_s"]
            current["duration_s"] = float(current["end_offset_s"]) - float(current["start_offset_s"])
            current["count"] = int(current["count"]) + int(item["count"])
            current["max_ms"] = max(float(current["max_ms"]), float(item["max_ms"]))
            current["mean_ms"] = np.nan
            current["strict_runs"] = int(current["strict_runs"]) + 1
        else:
            episodes.append(current)
            current = item
            current["strict_runs"] = 1
    if current is not None:
        episodes.append(current)

episode_df = pd.DataFrame(episodes)
episode_df = episode_df[(episode_df["count"] >= 5) | (episode_df["duration_s"] >= 0.10)]
episode_df = episode_df.sort_values(["duration_s", "count"], ascending=False)
for _, row in episode_df.head(60).iterrows():
    print(
        "EPISODE|"
        f"{row['link']}|strict_runs={int(row['strict_runs'])}|high_records={int(row['count'])}|"
        f"csv_start_s={row['start_offset_s']:.3f}|csv_end_s={row['end_offset_s']:.3f}|"
        f"duration_s={row['duration_s']:.3f}|video_start_s={row['video_start_s']:.3f}|"
        f"video_end_s={row['video_end_s']:.3f}|max_ms={row['max_ms']:.3f}"
    )

# Also report the ten most severe single records for context.
for _, row in df.nlargest(10, "延迟(毫秒)").iterrows():
    print(
        "PEAK|"
        f"{row['链路']}|time={row['采集时间']}|csv_s={row['csv_offset_s']:.3f}|"
        f"video_s={video_record_start_s+row['csv_offset_s']:.3f}|latency_ms={row['延迟(毫秒)']:.3f}"
    )
