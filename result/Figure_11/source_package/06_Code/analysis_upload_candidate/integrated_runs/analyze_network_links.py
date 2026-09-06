from __future__ import annotations

import os
from pathlib import Path

import numpy as np
import pandas as pd


csv_path = Path(os.environ["MOSAIC_NETWORK_CSV"])
df = pd.read_csv(csv_path)
df["采集时间"] = pd.to_datetime(df["采集时间"], errors="coerce")
df["发出时间"] = pd.to_datetime(df["发出时间"], errors="coerce")
df = df.dropna(subset=["采集时间", "链路"]).sort_values("采集时间")

global_start = df["采集时间"].min()
global_end = df["采集时间"].max()
print(f"ROWS={len(df)}")
print(f"GLOBAL_START={global_start.isoformat(sep=' ')}")
print(f"GLOBAL_END={global_end.isoformat(sep=' ')}")
print(f"GLOBAL_DURATION_S={(global_end-global_start).total_seconds():.3f}")
print(f"LINKS={df['链路'].nunique()}")

summaries: list[dict[str, object]] = []
for link, group in df.groupby("链路", sort=True):
    times = group["采集时间"].sort_values()
    gaps_ms = times.diff().dt.total_seconds().dropna() * 1000.0
    summaries.append(
        {
            "link": link,
            "rows": len(group),
            "start": times.iloc[0],
            "end": times.iloc[-1],
            "start_offset_s": (times.iloc[0] - global_start).total_seconds(),
            "end_offset_s": (times.iloc[-1] - global_start).total_seconds(),
            "duration_s": (times.iloc[-1] - times.iloc[0]).total_seconds(),
            "median_gap_ms": float(gaps_ms.median()) if len(gaps_ms) else np.nan,
            "p95_gap_ms": float(gaps_ms.quantile(0.95)) if len(gaps_ms) else np.nan,
            "max_gap_ms": float(gaps_ms.max()) if len(gaps_ms) else np.nan,
        }
    )

summary = pd.DataFrame(summaries).sort_values("end")
for _, row in summary.iterrows():
    print(
        "LINK_SUMMARY|"
        f"{row['link']}|rows={int(row['rows'])}|start={row['start']}|end={row['end']}|"
        f"start_offset_s={row['start_offset_s']:.3f}|end_offset_s={row['end_offset_s']:.3f}|"
        f"duration_s={row['duration_s']:.3f}|median_gap_ms={row['median_gap_ms']:.3f}|"
        f"p95_gap_ms={row['p95_gap_ms']:.3f}|max_gap_ms={row['max_gap_ms']:.3f}"
    )

# Report the latest records around every distinct link-stop boundary.
for end_time in summary["end"].drop_duplicates().sort_values():
    offset = (end_time - global_start).total_seconds()
    active_after = summary.loc[summary["end"] > end_time, "link"].tolist()
    stopped_by = summary.loc[summary["end"] <= end_time, "link"].tolist()
    print(
        f"BOUNDARY|time={end_time}|offset_s={offset:.3f}|"
        f"stopped_by={';'.join(stopped_by)}|active_after={';'.join(active_after)}"
    )

# Use the user-confirmed 04:28 video endpoint as the experimental close marker.
experiment_close_offset_s = 4 * 60 + 28
experiment_close = global_start + pd.Timedelta(seconds=experiment_close_offset_s)
print(f"EXPERIMENT_CLOSE_ASSUMED={experiment_close.isoformat(sep=' ')}")
print(f"EXPERIMENT_CLOSE_OFFSET_S={experiment_close_offset_s}")
for link, group in df.groupby("链路", sort=True):
    post_close = group[group["采集时间"] > experiment_close]
    last_time = group["采集时间"].max()
    tail_s = max(0.0, (last_time - experiment_close).total_seconds())
    print(
        f"POST_CLOSE|{link}|records={len(post_close)}|"
        f"last={last_time}|tail_s={tail_s:.3f}"
    )

# Aggregate row counts in one-second bins near the tail to show residual activity.
df["offset_s"] = (df["采集时间"] - global_start).dt.total_seconds()
tail_start = max(0, int(np.floor(df["offset_s"].max())) - 20)
tail = df[df["offset_s"] >= tail_start].copy()
tail["second"] = np.floor(tail["offset_s"]).astype(int)
pivot = tail.pivot_table(index="second", columns="链路", values="序号", aggfunc="count", fill_value=0)
for second, row in pivot.iterrows():
    values = ";".join(f"{col}={int(row[col])}" for col in pivot.columns)
    print(f"TAIL_SECOND|offset_s={second}|{values}")
