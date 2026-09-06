from __future__ import annotations

import json
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from PIL import Image

import analyze_update_timestamp as core


SUBMISSION_ROOT = Path(__file__).resolve().parents[2]
PROCESSED_DIR = SUBMISSION_ROOT / "05_Data" / "quest_mocap_validation" / "processed"
FIGURE_DIR = SUBMISSION_ROOT / "02_Figures_Submission"
SOURCE_ROOT = SUBMISSION_ROOT / "03_Figure_Sources_Internal"

BLUE = "#0072B2"
ORANGE = "#D55E00"
GREEN = "#009E73"
PURPLE = "#CC79A7"
DARK = "#222222"
MID = "#6F6F6F"
LIGHT = "#D9D9D9"


def configure_style() -> None:
    plt.rcParams.update(
        {
            "font.family": "sans-serif",
            "font.sans-serif": ["Arial", "Helvetica", "DejaVu Sans"],
            "font.size": 7.5,
            "axes.labelsize": 7.5,
            "axes.titlesize": 8.0,
            "legend.fontsize": 7.0,
            "xtick.labelsize": 7.0,
            "ytick.labelsize": 7.0,
            "axes.linewidth": 0.65,
            "lines.linewidth": 1.0,
            "xtick.major.width": 0.6,
            "ytick.major.width": 0.6,
            "xtick.major.size": 3.0,
            "ytick.major.size": 3.0,
            "figure.facecolor": "white",
            "axes.facecolor": "white",
            "savefig.facecolor": "white",
        }
    )


def standardize(values: np.ndarray) -> np.ndarray:
    values = np.asarray(values, dtype=float)
    return (values - np.nanmean(values)) / np.nanstd(values)


def signed(value: float, decimals: int = 2) -> str:
    return f"{value:.{decimals}f}".replace("-", "−")


def style_axis(axis, grid: bool = True) -> None:
    axis.spines["top"].set_visible(False)
    axis.spines["right"].set_visible(False)
    if grid:
        axis.grid(axis="y", color=LIGHT, linewidth=0.45, alpha=0.75)
    axis.tick_params(direction="out")


def panel_label(axis, label: str, is_3d: bool = False) -> None:
    kwargs = dict(fontweight="bold", fontsize=9, va="top", ha="left")
    if is_3d:
        axis.text2D(-0.10, 1.04, label, transform=axis.transAxes, **kwargs)
    else:
        axis.text(-0.12, 1.06, label, transform=axis.transAxes, **kwargs)


def save_rgb(
    fig: plt.Figure,
    png_path: Path,
    svg_path: Path,
    *,
    fixed_canvas: bool = False,
) -> None:
    png_path.parent.mkdir(parents=True, exist_ok=True)
    svg_path.parent.mkdir(parents=True, exist_ok=True)
    save_options = {} if fixed_canvas else {"bbox_inches": "tight"}
    fig.savefig(svg_path, **save_options)
    temp_path = png_path.with_suffix(".rgba.png")
    fig.savefig(
        temp_path,
        dpi=600,
        facecolor="white",
        transparent=False,
        **save_options,
    )
    with Image.open(temp_path) as image:
        image.convert("RGB").save(png_path, dpi=(600, 600), optimize=True)
    temp_path.unlink()


def temporal_figure(results: dict) -> Path:
    mocap, _, _ = core.read_mocap(core.MOCAP_FILE)
    quest, _, _ = core.read_quest(core.QUEST_FILE)
    timing = results["time_alignment"]
    quality = results["data_quality"]

    grid_ms = np.arange(
        quality["analysis_overlap_start_ms"],
        quality["analysis_overlap_end_ms"],
        core.DENSE_DT_MS,
    )
    mocap_speed, mocap_angular = core.dense_kinematic_signals(mocap, grid_ms)
    quest_speed, quest_angular = core.dense_kinematic_signals(quest, grid_ms)
    mask = (grid_ms >= timing["selected_window_start_ms"]) & (
        grid_ms <= timing["selected_window_end_ms"]
    )
    lags_ms = np.arange(
        timing["search_range_ms"][0],
        timing["search_range_ms"][1] + timing["search_step_ms"] * 0.5,
        timing["search_step_ms"],
    )
    curves = core.correlation_curves(
        grid_ms,
        mocap_speed,
        mocap_angular,
        quest_speed,
        quest_angular,
        mask,
        lags_ms,
    )
    pos_corr, rot_corr, combined_corr = curves
    selected_time = grid_ms[mask]
    time_s = (selected_time - selected_time[0]) / 1000.0
    best_lag = timing["best_lag_ms"]
    aligned_q_speed = np.interp(selected_time + best_lag, grid_ms, quest_speed)

    windows = pd.read_csv(PROCESSED_DIR / "cross_correlation_windows.csv")
    window_mid_s = (
        (windows["start_ms"] + windows["end_ms"]) / 2.0
        - quality["analysis_overlap_start_ms"]
    ) / 1000.0

    # Panel (a) deliberately uses the held-out block, not the calibration
    # interval containing the representative correlation window.  This keeps
    # the trajectory overlay independent of the samples used to estimate the
    # fixed spatial registration.
    aligned = pd.read_csv(PROCESSED_DIR / "aligned_held_out_validation.csv")
    mocap_xyz = aligned[["mocapEyeX_m", "mocapEyeY_m", "mocapEyeZ_m"]].to_numpy(float)
    quest_xyz = aligned[
        ["questRegisteredX_m", "questRegisteredY_m", "questRegisteredZ_m"]
    ].to_numpy(float)

    # Preserve the exact 4321 x 2951 px canvas used by the submitted figure at
    # 600 dpi so that the in-place DOCX media replacement cannot distort or
    # repaginate the manuscript.
    fig = plt.figure(figsize=(4321 / 600.0, 2951 / 600.0))
    grid = fig.add_gridspec(
        2,
        2,
        left=0.075,
        right=0.985,
        bottom=0.105,
        top=0.925,
        wspace=0.29,
        hspace=0.42,
    )

    ax = fig.add_subplot(grid[0, 0], projection="3d")
    ax.plot(
        mocap_xyz[:, 0],
        mocap_xyz[:, 1],
        mocap_xyz[:, 2],
        color=DARK,
        linewidth=1.15,
        label="MoCap",
    )
    ax.plot(
        quest_xyz[:, 0],
        quest_xyz[:, 1],
        quest_xyz[:, 2],
        color=BLUE,
        linewidth=1.05,
        linestyle=(0, (4, 2)),
        label="Quest",
    )
    ax.set_xlabel("X (m)", labelpad=2)
    ax.set_ylabel("Y (m)", labelpad=2)
    ax.set_zlabel("Z (m)", labelpad=2)
    ax.set_proj_type("ortho")
    ax.view_init(elev=20, azim=-58)
    set_equal_3d(ax, np.vstack([mocap_xyz, quest_xyz]))
    ax.xaxis.set_pane_color((1.0, 1.0, 1.0, 0.0))
    ax.yaxis.set_pane_color((1.0, 1.0, 1.0, 0.0))
    ax.zaxis.set_pane_color((1.0, 1.0, 1.0, 0.0))
    ax.grid(True, color=LIGHT, linewidth=0.4, alpha=0.65)
    ax.locator_params(nbins=4)
    ax.legend(
        frameon=False,
        ncol=2,
        loc="lower center",
        bbox_to_anchor=(0.53, 1.03),
        handlelength=2.5,
        columnspacing=1.2,
    )

    ax = fig.add_subplot(grid[0, 1])
    ax.plot(lags_ms, pos_corr, color=BLUE, label="Translation")
    ax.plot(lags_ms, rot_corr, color=ORANGE, label="Rotation")
    ax.plot(lags_ms, combined_corr, color=DARK, linewidth=1.45, label="Equal-weight mean")
    ax.axvline(0.0, color=MID, linewidth=0.6, alpha=0.65)
    ax.axvline(best_lag, color=PURPLE, linestyle="--", linewidth=1.0)
    best_corr = float(np.interp(best_lag, lags_ms, combined_corr))
    ax.scatter([best_lag], [best_corr], s=24, color=PURPLE, zorder=4)
    curve_min = float(np.nanmin(np.concatenate([pos_corr, rot_corr, combined_corr])))
    ax.set_ylim(max(0.0, curve_min - 0.012), 1.005)
    ax.text(
        0.025,
        0.045,
        f"Median lag: {signed(best_lag)} ms",
        transform=ax.transAxes,
        color=PURPLE,
        va="bottom",
        ha="left",
        bbox=dict(facecolor="white", edgecolor="none", alpha=0.92, pad=1.5),
    )
    ax.set_xlabel(r"Lag, $\tau$ (ms)")
    ax.set_ylabel(r"Pearson correlation, $r$")
    ax.set_xlim(timing["search_range_ms"])
    ax.legend(
        frameon=False,
        loc="lower center",
        bbox_to_anchor=(0.5, 1.035),
        ncol=3,
        handlelength=2.2,
        columnspacing=1.0,
    )
    style_axis(ax)

    ax = fig.add_subplot(grid[1, 0])
    lag_values = windows["best_lag_combined_ms"].to_numpy(float)
    lag_mean = timing["sliding_window_lag_mean_ms"]
    lag_sd = timing["sliding_window_lag_std_ms"]
    ax.fill_between(
        [window_mid_s.min() - 0.4, window_mid_s.max() + 0.4],
        lag_mean - lag_sd,
        lag_mean + lag_sd,
        color=BLUE,
        alpha=0.14,
        linewidth=0,
        label="Mean ± SD",
    )
    ax.axhline(lag_mean, color=BLUE, linewidth=1.0)
    ax.axhline(best_lag, color=PURPLE, linestyle="--", linewidth=0.9, label="Median")
    ax.plot(
        window_mid_s,
        lag_values,
        "o-",
        color=DARK,
        markersize=3.2,
        linewidth=0.8,
        label="Window estimate",
    )
    selected_index = int(
        np.argmin(np.abs(windows["start_ms"].to_numpy(float) - timing["selected_window_start_ms"]))
    )
    selected_mid = float(window_mid_s.iloc[selected_index])
    selected_lag = float(lag_values[selected_index])
    ax.scatter(
        [selected_mid],
        [selected_lag],
        marker="*",
        s=58,
        color=ORANGE,
        zorder=5,
        label="Representative",
    )
    ax.set_xlabel("Window midpoint relative to overlap start (s)")
    ax.set_ylabel(r"Estimated lag, $\tau$ (ms)")
    handles, labels = ax.get_legend_handles_labels()
    order = [2, 0, 1, 3]
    ax.legend(
        [handles[index] for index in order],
        [labels[index] for index in order],
        frameon=True,
        facecolor="white",
        edgecolor="none",
        framealpha=0.92,
        loc="upper right",
        ncol=2,
        fontsize=6.4,
        handlelength=2.0,
        columnspacing=0.8,
        borderpad=0.25,
    )
    style_axis(ax)

    ax = fig.add_subplot(grid[1, 1])
    ax.plot(
        time_s,
        standardize(mocap_speed[mask]),
        color=DARK,
        linewidth=1.15,
        label="MoCap",
    )
    ax.plot(
        time_s,
        standardize(aligned_q_speed),
        color=BLUE,
        linewidth=1.05,
        linestyle=(0, (4, 2)),
        label="Quest",
    )
    ax.set_xlabel("Time in representative window (s)")
    ax.set_ylabel("Standardized translation speed")
    ax.legend(
        frameon=True,
        facecolor="white",
        edgecolor="none",
        framealpha=0.90,
        ncol=2,
        loc="upper right",
        bbox_to_anchor=(0.985, 0.985),
        handlelength=2.5,
        columnspacing=1.2,
        borderpad=0.25,
    )
    style_axis(ax)

    # Use figure coordinates so each column and row of panel labels is
    # geometrically aligned even though panel (a) is a 3-D axes whose active
    # plotting box is narrower than its GridSpec cell.
    for x, y, label in (
        (0.025, 0.945, "(a)"),
        (0.525, 0.945, "(b)"),
        (0.025, 0.505, "(c)"),
        (0.525, 0.505, "(d)"),
    ):
        fig.text(x, y, label, fontsize=9.0, fontweight="bold", ha="left", va="top")

    png = FIGURE_DIR / "Figure_12_quest_mocap_temporal_alignment.png"
    svg = SOURCE_ROOT / "Figure_12" / "Figure_12_quest_mocap_temporal_alignment.svg"
    save_rgb(fig, png, svg, fixed_canvas=True)
    plt.close(fig)
    return png


def set_equal_3d(axis, xyz: np.ndarray) -> None:
    minima = xyz.min(axis=0)
    maxima = xyz.max(axis=0)
    center = (minima + maxima) / 2.0
    half = max((maxima - minima).max() / 2.0, 1e-3)
    axis.set_xlim(center[0] - half, center[0] + half)
    axis.set_ylim(center[1] - half, center[1] + half)
    axis.set_zlim(center[2] - half, center[2] + half)
    axis.set_box_aspect((1, 1, 1))


def spatial_figure(results: dict) -> Path:
    table = pd.read_csv(PROCESSED_DIR / "aligned_held_out_validation.csv")
    validation = results["spatial_accuracy"]["held_out_validation"]
    intervals = results["spatial_accuracy"]["held_out_block_bootstrap_95_interval"]

    time_s = (table["mocapTimePcMs"].to_numpy(float) - table["mocapTimePcMs"].iloc[0]) / 1000.0
    mocap_xyz = table[["mocapEyeX_m", "mocapEyeY_m", "mocapEyeZ_m"]].to_numpy(float)
    quest_xyz = table[["questRegisteredX_m", "questRegisteredY_m", "questRegisteredZ_m"]].to_numpy(float)
    residual = table[["errorX_mm", "errorY_mm", "errorZ_mm"]].to_numpy(float)
    position_error = table["positionError_mm"].to_numpy(float)
    rotation_error = table["rotationError_deg"].to_numpy(float)

    fig = plt.figure(figsize=(180 / 25.4, 132 / 25.4), constrained_layout=True)
    grid = fig.add_gridspec(2, 2)

    ax = fig.add_subplot(grid[0, 0], projection="3d")
    ax.plot(mocap_xyz[:, 0], mocap_xyz[:, 1], mocap_xyz[:, 2], color=DARK, label="MoCap", linewidth=1.2)
    ax.plot(quest_xyz[:, 0], quest_xyz[:, 1], quest_xyz[:, 2], color=BLUE, label="Registered Quest", linewidth=1.0)
    ax.scatter(*mocap_xyz[0], color=GREEN, s=13, depthshade=False, label="Start")
    ax.set_xlabel("x (m)", labelpad=1)
    ax.set_ylabel("y (m)", labelpad=1)
    ax.set_zlabel("z (m)", labelpad=1)
    ax.set_title("Held-out trajectory", loc="left", fontweight="bold", pad=3)
    ax.legend(frameon=False, loc="upper left", bbox_to_anchor=(0.00, 0.98))
    ax.grid(True, color=LIGHT, linewidth=0.4)
    set_equal_3d(ax, np.vstack([mocap_xyz, quest_xyz]))
    panel_label(ax, "a", is_3d=True)

    ax = fig.add_subplot(grid[0, 1])
    for index, (label, color) in enumerate((("Δx", BLUE), ("Δy", ORANGE), ("Δz", GREEN))):
        ax.plot(time_s, residual[:, index], color=color, label=label, linewidth=0.9)
    ax.axhline(0, color=DARK, linewidth=0.6)
    ax.set_title("Signed position residuals", loc="left", fontweight="bold")
    ax.set_xlabel("Held-out time (s)")
    ax.set_ylabel("Residual (mm)")
    ax.legend(frameon=False, ncol=3, loc="upper right")
    style_axis(ax)
    panel_label(ax, "b")

    ax = fig.add_subplot(grid[1, 0])
    ax.plot(time_s, position_error, color=BLUE, linewidth=0.9)
    ax.axhline(validation["position_rmse_mm"], color=ORANGE, linestyle="--", label=f"RMSE {validation['position_rmse_mm']:.2f} mm")
    ax.axhline(validation["position_p95_mm"], color=PURPLE, linestyle=":", label=f"P95 {validation['position_p95_mm']:.2f} mm")
    ax.text(
        0.02,
        0.95,
        f"Block-bootstrap 95% CI\nRMSE {intervals['position_rmse_mm']['lower_95']:.2f}–{intervals['position_rmse_mm']['upper_95']:.2f} mm",
        transform=ax.transAxes,
        va="top",
        bbox=dict(boxstyle="round,pad=0.25", facecolor="white", edgecolor=LIGHT, linewidth=0.6),
    )
    ax.set_title("Euclidean position error", loc="left", fontweight="bold")
    ax.set_xlabel("Held-out time (s)")
    ax.set_ylabel("Error (mm)")
    ax.legend(frameon=False, loc="lower right")
    style_axis(ax)
    panel_label(ax, "c")

    ax = fig.add_subplot(grid[1, 1])
    ax.plot(time_s, rotation_error, color=ORANGE, linewidth=0.9)
    ax.axhline(validation["rotation_rmse_deg"], color=BLUE, linestyle="--", label=f"RMSE {validation['rotation_rmse_deg']:.2f}°")
    ax.axhline(validation["rotation_p95_deg"], color=PURPLE, linestyle=":", label=f"P95 {validation['rotation_p95_deg']:.2f}°")
    ax.text(
        0.02,
        0.95,
        f"Block-bootstrap 95% CI\nRMSE {intervals['rotation_rmse_deg']['lower_95']:.2f}–{intervals['rotation_rmse_deg']['upper_95']:.2f}°",
        transform=ax.transAxes,
        va="top",
        bbox=dict(boxstyle="round,pad=0.25", facecolor="white", edgecolor=LIGHT, linewidth=0.6),
    )
    ax.set_title(r"$\mathrm{SO}(3)$ geodesic rotation error", loc="left", fontweight="bold")
    ax.set_xlabel("Held-out time (s)")
    ax.set_ylabel("Error (°)")
    ax.legend(frameon=False, loc="lower right")
    style_axis(ax)
    panel_label(ax, "d")

    png = FIGURE_DIR / "Figure_13_quest_mocap_spatial_validation.png"
    svg = SOURCE_ROOT / "Figure_13" / "Figure_13_quest_mocap_spatial_validation.svg"
    save_rgb(fig, png, svg)
    plt.close(fig)
    return png


def write_metric_table(results: dict) -> Path:
    rows = []
    for block, label in (("calibration", "Calibration fit"), ("held_out_validation", "Held-out validation")):
        values = results["spatial_accuracy"][block]
        rows.append(
            {
                "dataset": label,
                "n": values["count"],
                "position_mean_mm": values["position_mean_mm"],
                "position_median_mm": values["position_median_mm"],
                "position_rmse_mm": values["position_rmse_mm"],
                "position_p95_mm": values["position_p95_mm"],
                "rotation_mean_deg": values["rotation_mean_deg"],
                "rotation_median_deg": values["rotation_median_deg"],
                "rotation_rmse_deg": values["rotation_rmse_deg"],
                "rotation_p95_deg": values["rotation_p95_deg"],
            }
        )
    path = PROCESSED_DIR / "Table_quest_mocap_validation_metrics.csv"
    pd.DataFrame(rows).to_csv(path, index=False, encoding="utf-8-sig")
    return path


def main() -> None:
    configure_style()
    results = json.loads((PROCESSED_DIR / "analysis_results.json").read_text(encoding="utf-8"))
    outputs = [temporal_figure(results), spatial_figure(results), write_metric_table(results)]
    for output in outputs:
        print(output)


if __name__ == "__main__":
    main()
