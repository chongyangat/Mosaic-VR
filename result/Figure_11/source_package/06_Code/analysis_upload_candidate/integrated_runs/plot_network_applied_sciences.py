from __future__ import annotations

import math
import os
from pathlib import Path

import matplotlib as mpl
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib import font_manager
from matplotlib.ticker import MultipleLocator


source_csv = Path(os.environ["MOSAIC_NETWORK_CSV"])
output_dir = Path(os.environ["MOSAIC_FIGURE_OUTPUT_DIR"])
output_dir.mkdir(parents=True, exist_ok=True)

stem = "NetworkPerformance_20260814_195140_AppliedSciences"
png_path = output_dir / f"{stem}.png"
svg_path = output_dir / f"{stem}.svg"
pdf_path = output_dir / f"{stem}.pdf"
preview_path = output_dir / f"{stem}_preview.png"

# Journal-oriented typography. PDF uses embedded TrueType fonts; SVG converts
# text to paths so the appearance is independent of the reader's font setup.
arial_path = Path(r"C:\Windows\Fonts\arial.ttf")
if arial_path.exists():
    font_manager.fontManager.addfont(str(arial_path))
mpl.rcParams.update(
    {
        "font.family": "Arial",
        "font.size": 9,
        "axes.labelsize": 10,
        "xtick.labelsize": 9,
        "ytick.labelsize": 9,
        "legend.fontsize": 9,
        "axes.linewidth": 0.8,
        "xtick.major.width": 0.8,
        "ytick.major.width": 0.8,
        "xtick.minor.width": 0.6,
        "ytick.minor.width": 0.6,
        "xtick.major.size": 4,
        "ytick.major.size": 4,
        "xtick.minor.size": 2.5,
        "ytick.minor.size": 2.5,
        "pdf.fonttype": 42,
        "ps.fonttype": 42,
        "svg.fonttype": "path",
        "path.simplify": True,
        "path.simplify_threshold": 0.05,
        "agg.path.chunksize": 10000,
    }
)

df = pd.read_csv(source_csv)
df["采集时间"] = pd.to_datetime(df["采集时间"], errors="coerce")
df["延迟(毫秒)"] = pd.to_numeric(df["延迟(毫秒)"], errors="coerce")
df = df.dropna(subset=["采集时间", "链路", "延迟(毫秒)"]).sort_values("采集时间")

t0 = df["采集时间"].min()
df["elapsed_s"] = (df["采集时间"] - t0).dt.total_seconds()

# Truncate at the first four-to-three-link transition: the last MoCap-to-VR
# record. Any later PC-side residual capture is outside the retained interval.
mocap_vr = df[df["链路"] == "MoCap→VR"]
if mocap_vr.empty:
    raise RuntimeError("MoCap→VR link not found")
cutoff_time = mocap_vr["采集时间"].max()
cutoff_s = float((cutoff_time - t0).total_seconds())
truncated = df[df["采集时间"] <= cutoff_time].copy()

# Detect periodic GC events conservatively: a >150 ms EyeTracking-to-PC record
# coincident (within 10 ms) with a >150 ms MoCap-to-PC record. Cluster duplicate
# timestamps into one event, then remove PC-bound records within +/-250 ms.
# This preserves the MoCap-to-VR measurements while removing the complete
# PC-side GC disturbance window.
eye_peak_times = (
    truncated[
        (truncated["链路"] == "EyeTracking→PC")
        & (truncated["延迟(毫秒)"] > 150.0)
    ]["采集时间"]
    .drop_duplicates()
    .sort_values()
)
mocap_pc_peak_times = (
    truncated[
        (truncated["链路"] == "MoCap→PC")
        & (truncated["延迟(毫秒)"] > 150.0)
    ]["采集时间"]
    .drop_duplicates()
    .sort_values()
)

candidate_events: list[pd.Timestamp] = []
for eye_time in eye_peak_times:
    if len(mocap_pc_peak_times) == 0:
        continue
    nearest_delta = (mocap_pc_peak_times - eye_time).abs().min()
    if nearest_delta <= pd.Timedelta(milliseconds=10):
        candidate_events.append(eye_time)

gc_events: list[pd.Timestamp] = []
for event_time in sorted(candidate_events):
    if not gc_events or event_time - gc_events[-1] > pd.Timedelta(seconds=1):
        gc_events.append(event_time)

gc_window = pd.Timedelta(milliseconds=250)
gc_affected_links = {"EyeTracking→PC", "MoCap→PC"}
gc_mask = pd.Series(False, index=truncated.index)
for event_time in gc_events:
    gc_mask |= (
        truncated["链路"].isin(gc_affected_links)
        & (truncated["采集时间"].sub(event_time).abs() <= gc_window)
    )
clean = truncated.loc[~gc_mask].copy()

label_map = {
    "EyeTracking→PC": "Eye tracking → PC",
    "MoCap→PC": "MoCap → PC",
    "MoCap→VR": "MoCap → VR",
    "PC<->VR 状态同步": "PC–VR shared-state RTT",
}
colors = {
    "EyeTracking→PC": "#0072B2",
    "MoCap→PC": "#D55E00",
    "MoCap→VR": "#009E73",
    "PC<->VR 状态同步": "#CC79A7",
}

fig, axes = plt.subplots(
    2,
    1,
    figsize=(7.2, 4.8),
    sharex=True,
    gridspec_kw={"height_ratios": [1.05, 0.95]},
)
fig.patch.set_facecolor("white")

top_links = ["MoCap→VR", "MoCap→PC"]
bottom_links = ["EyeTracking→PC", "PC<->VR 状态同步"]

for link in top_links:
    group = clean[clean["链路"] == link].sort_values("elapsed_s")
    axes[0].plot(
        group["elapsed_s"],
        group["延迟(毫秒)"],
        color=colors[link],
        linewidth=0.65,
        alpha=0.82,
        label=label_map[link],
        solid_capstyle="round",
        zorder=3 if link == "MoCap→PC" else 2,
    )

for link in bottom_links:
    group = clean[clean["链路"] == link].sort_values("elapsed_s")
    axes[1].plot(
        group["elapsed_s"],
        group["延迟(毫秒)"],
        color=colors[link],
        linewidth=0.75,
        alpha=0.86,
        label=label_map[link],
        solid_capstyle="round",
    )

top_max = clean[clean["链路"].isin(top_links)]["延迟(毫秒)"].max()
bottom_max = clean[clean["链路"].isin(bottom_links)]["延迟(毫秒)"].max()
top_limit = max(100.0, 20.0 * math.ceil(float(top_max) / 20.0))
bottom_limit = max(50.0, 10.0 * math.ceil(float(bottom_max) / 10.0))

for idx, (axis, panel) in enumerate(zip(axes, ["(a)", "(b)"])):
    axis.set_xlim(0.0, cutoff_s)
    axis.set_ylim(bottom=0.0)
    axis.set_ylabel("Latency (ms)")
    axis.grid(axis="y", which="major", color="#D9D9D9", linewidth=0.55, linestyle="--")
    axis.grid(axis="x", which="major", color="#ECECEC", linewidth=0.45, linestyle=":")
    axis.tick_params(direction="out", top=False, right=False, pad=3)
    axis.spines["top"].set_visible(False)
    axis.spines["right"].set_visible(False)
    axis.text(
        0.012,
        0.94,
        panel,
        transform=axis.transAxes,
        ha="left",
        va="top",
        fontsize=10,
        fontweight="bold",
        color="#222222",
    )
    axis.legend(loc="upper right", frameon=False, handlelength=2.4, ncol=2)

# Keep the legend order semantically stable even though MoCap-to-PC is drawn
# last so that it remains visible above the denser MoCap-to-VR curve.
top_handles, top_labels = axes[0].get_legend_handles_labels()
top_order = [top_labels.index("MoCap → PC"), top_labels.index("MoCap → VR")]
axes[0].legend(
    [top_handles[i] for i in top_order],
    [top_labels[i] for i in top_order],
    loc="upper right",
    frameon=False,
    handlelength=2.4,
    ncol=2,
)

axes[0].set_ylim(0.0, top_limit)
axes[0].yaxis.set_major_locator(MultipleLocator(20.0))
axes[0].yaxis.set_minor_locator(MultipleLocator(10.0))
axes[1].set_ylim(0.0, bottom_limit)
axes[1].yaxis.set_major_locator(MultipleLocator(10.0))
axes[1].yaxis.set_minor_locator(MultipleLocator(5.0))
axes[1].set_xlabel("Elapsed time (s)")
axes[1].xaxis.set_major_locator(MultipleLocator(60.0))
axes[1].xaxis.set_minor_locator(MultipleLocator(30.0))

fig.subplots_adjust(left=0.105, right=0.985, top=0.985, bottom=0.115, hspace=0.12)

fig.savefig(preview_path, dpi=200, facecolor="white")
fig.savefig(png_path, dpi=600, facecolor="white")
fig.savefig(svg_path, facecolor="white")
fig.savefig(pdf_path, dpi=600, facecolor="white")
plt.close(fig)

print(f"SOURCE_ROWS={len(df)}")
print(f"TRUNCATED_ROWS={len(truncated)}")
print(f"CUTOFF_TIME={cutoff_time}")
print(f"CUTOFF_S={cutoff_s:.3f}")
print(f"GC_EVENT_COUNT={len(gc_events)}")
for event_time in gc_events:
    print(f"GC_EVENT={event_time}|elapsed_s={(event_time-t0).total_seconds():.3f}")
print(f"GC_FILTERED_ROWS={int(gc_mask.sum())}")
for link, group in clean.groupby("链路", sort=True):
    print(
        f"CLEAN_STATS|{link}|rows={len(group)}|max_ms={group['延迟(毫秒)'].max():.3f}|"
        f"p95_ms={group['延迟(毫秒)'].quantile(0.95):.3f}|"
        f"p99_ms={group['延迟(毫秒)'].quantile(0.99):.3f}"
    )
print(preview_path)
print(png_path)
print(svg_path)
print(pdf_path)
