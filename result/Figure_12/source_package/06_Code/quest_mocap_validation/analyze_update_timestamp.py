from __future__ import annotations

import base64
import json
import math
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy.interpolate import interp1d
from scipy.optimize import least_squares
from scipy.signal import savgol_filter
from scipy.spatial.transform import Rotation, Slerp


SUBMISSION_ROOT = Path(__file__).resolve().parents[2]
RAW_DATA_DIR = SUBMISSION_ROOT / "05_Data" / "quest_mocap_validation" / "raw"
MOCAP_FILE = RAW_DATA_DIR / "update_timestamp1-Tracker0.csv"
QUEST_FILE = RAW_DATA_DIR / "QuestPose_20260829_173101.csv"
OUTPUT_DIR = SUBMISSION_ROOT / "05_Data" / "quest_mocap_validation" / "processed"

MOCAP_GAP_THRESHOLD_MS = 50.0
QUEST_JUMP_POSITION_M = 0.25
QUEST_JUMP_ROTATION_DEG = 30.0
DENSE_DT_MS = 2.0
LAG_SEARCH_MIN_MS = -80.0
LAG_SEARCH_MAX_MS = 80.0
LAG_SEARCH_STEP_MS = 0.1
WINDOW_SECONDS = 12.0
WINDOW_STEP_SECONDS = 1.0
CALIBRATION_FRACTION = 0.60
SPATIAL_RATE_HZ = 30.0
RANDOM_SEED = 20260831

MIRROR_X = np.diag([-1.0, 1.0, 1.0])


@dataclass
class PoseSeries:
    time_ms: np.ndarray
    position_m: np.ndarray
    rotation: Rotation


def finite_rows(*arrays: np.ndarray) -> np.ndarray:
    mask = np.ones(len(arrays[0]), dtype=bool)
    for array in arrays:
        if array.ndim == 1:
            mask &= np.isfinite(array)
        else:
            mask &= np.isfinite(array).all(axis=1)
    return mask


def normalize_quaternions(quat_xyzw: np.ndarray) -> np.ndarray:
    quat = np.asarray(quat_xyzw, dtype=float).copy()
    norm = np.linalg.norm(quat, axis=1)
    valid = np.isfinite(norm) & (norm > 1e-10)
    quat[valid] /= norm[valid, None]
    quat[~valid] = np.nan
    return quat


def deduplicate_pose(
    time_ms: np.ndarray,
    position_m: np.ndarray,
    quat_xyzw: np.ndarray,
) -> PoseSeries:
    order = np.argsort(time_ms, kind="mergesort")
    time_ms = np.asarray(time_ms, dtype=float)[order]
    position_m = np.asarray(position_m, dtype=float)[order]
    quat_xyzw = normalize_quaternions(np.asarray(quat_xyzw, dtype=float)[order])
    mask = finite_rows(time_ms, position_m, quat_xyzw)
    time_ms = time_ms[mask]
    position_m = position_m[mask]
    quat_xyzw = quat_xyzw[mask]

    # Retain the last observation for an identical timestamp.
    _, reverse_unique_index = np.unique(time_ms[::-1], return_index=True)
    keep = len(time_ms) - 1 - reverse_unique_index
    keep.sort()
    time_ms = time_ms[keep]
    position_m = position_m[keep]
    quat_xyzw = quat_xyzw[keep]
    return PoseSeries(time_ms, position_m, Rotation.from_quat(quat_xyzw))


def read_mocap(path: Path) -> tuple[PoseSeries, dict, pd.DataFrame]:
    frame = pd.read_csv(path, skiprows=11, skipinitialspace=True)
    frame = frame.rename(columns={frame.columns[0]: "Frame"})
    required = [
        "Timestamp",
        "XToGlobal1",
        "YToGlobal1",
        "ZToGlobal1",
        "QxToGlobal1",
        "QyToGlobal1",
        "QzToGlobal1",
        "QwToGlobal1",
    ]
    for column in required:
        frame[column] = pd.to_numeric(frame[column], errors="coerce")

    valid = frame["Timestamp"].gt(0)
    valid &= frame[required].notna().all(axis=1)
    raw = frame.loc[valid].copy().reset_index(drop=True)

    time_ms = raw["Timestamp"].to_numpy(float)
    position_native_mm = raw[["XToGlobal1", "YToGlobal1", "ZToGlobal1"]].to_numpy(float)
    position_unity_m = (MIRROR_X @ position_native_mm.T).T / 1000.0

    quat_native = normalize_quaternions(
        raw[["QxToGlobal1", "QyToGlobal1", "QzToGlobal1", "QwToGlobal1"]].to_numpy(float)
    )
    rotation_native = Rotation.from_quat(quat_native)
    rotation_unity_matrix = np.einsum(
        "ij,njk,kl->nil", MIRROR_X, rotation_native.as_matrix(), MIRROR_X
    )
    rotation_unity = Rotation.from_matrix(rotation_unity_matrix)

    gap_indices = np.flatnonzero(np.diff(time_ms) > MOCAP_GAP_THRESHOLD_MS) + 1
    segments = np.split(np.arange(len(time_ms)), gap_indices)
    selected = max(segments, key=len)
    series = PoseSeries(
        time_ms[selected], position_unity_m[selected], rotation_unity[selected]
    )

    dt = np.diff(time_ms)
    metadata = {
        "rows_total": int(len(frame)),
        "rows_valid": int(len(raw)),
        "nominal_rate_hz": 90.0,
        "median_dt_ms": float(np.median(dt)),
        "gap_count_gt_50ms": int(len(gap_indices)),
        "continuous_segments": [
            {
                "rows": int(len(segment)),
                "start_ms": float(time_ms[segment[0]]),
                "end_ms": float(time_ms[segment[-1]]),
                "duration_s": float((time_ms[segment[-1]] - time_ms[segment[0]]) / 1000.0),
            }
            for segment in segments
        ],
        "selected_segment_rows": int(len(selected)),
        "selected_segment_start_ms": float(series.time_ms[0]),
        "selected_segment_end_ms": float(series.time_ms[-1]),
        "selected_segment_duration_s": float((series.time_ms[-1] - series.time_ms[0]) / 1000.0),
        "coordinate_conversion": "p=diag(-1,1,1)*p_mm/1000; R=diag(-1,1,1)*R_native*diag(-1,1,1)",
    }
    return series, metadata, raw


def read_quest(path: Path) -> tuple[PoseSeries, dict, pd.DataFrame]:
    frame = pd.read_csv(path)
    pose_required = [
        "PredictedDisplayTime代理-QuestUnix估算(ms)",
        "时钟偏移(ms)",
        "RenderPredicted-TrackingSpace位置X",
        "RenderPredicted-TrackingSpace位置Y",
        "RenderPredicted-TrackingSpace位置Z",
        "RenderPredicted-TrackingSpace四元数X",
        "RenderPredicted-TrackingSpace四元数Y",
        "RenderPredicted-TrackingSpace四元数Z",
        "RenderPredicted-TrackingSpace四元数W",
    ]
    numeric_columns = [
        *pose_required,
        "PredictionHorizon(ms)",
        "PoseReadTime至PC观测延迟(ms)",
        "同步RTT(ms)",
        "同步年龄(ms)",
        "偏移不确定度上界(ms)",
    ]
    for column in numeric_columns:
        frame[column] = pd.to_numeric(frame[column], errors="coerce")

    sync_valid = frame["时钟同步有效"].astype(str).str.lower().eq("true")
    render_valid = frame["RenderPoseTime有效"].astype(str).str.lower().eq("true")
    raw_valid = frame["RawPose有效"].astype(str).str.lower().eq("true")
    base_valid = sync_valid & render_valid & frame[pose_required].notna().all(axis=1)
    ready_raw = frame.loc[base_valid].copy().reset_index(drop=True)

    # Queue stalls and timestamp uncertainty are measurement-quality failures, not
    # physical head motion. Apply a predeclared filter before choosing a segment.
    quality = base_valid.copy()
    quality &= frame["PoseReadTime至PC观测延迟(ms)"].le(60.0)
    quality &= frame["PredictionHorizon(ms)"].le(70.0)
    quality &= frame["偏移不确定度上界(ms)"].le(3.0)
    quality_raw = frame.loc[quality].copy().reset_index(drop=True)

    def pose_from_rows(rows: pd.DataFrame) -> PoseSeries:
        # The saved RenderPredicted pose belongs to the predicted display proxy,
        # mapped from Quest Unix into the PC clock domain using ClockDiff(VR-PC).
        time_pc_ms = (
            rows["PredictedDisplayTime代理-QuestUnix估算(ms)"].to_numpy(float)
            - rows["时钟偏移(ms)"].to_numpy(float)
        )
        position_m = rows[
            [
                "RenderPredicted-TrackingSpace位置X",
                "RenderPredicted-TrackingSpace位置Y",
                "RenderPredicted-TrackingSpace位置Z",
            ]
        ].to_numpy(float)
        quat_xyzw = rows[
            [
                "RenderPredicted-TrackingSpace四元数X",
                "RenderPredicted-TrackingSpace四元数Y",
                "RenderPredicted-TrackingSpace四元数Z",
                "RenderPredicted-TrackingSpace四元数W",
            ]
        ].to_numpy(float)
        return deduplicate_pose(time_pc_ms, position_m, quat_xyzw)

    ready_series = pose_from_rows(ready_raw)
    quality_series = pose_from_rows(quality_raw)

    # Audit jumps on the complete Ready stream, but split the quality stream at
    # any long gap or tracking-origin discontinuity and retain its longest block.
    ready_dt = np.diff(ready_series.time_ms)
    ready_step_pos = np.linalg.norm(np.diff(ready_series.position_m, axis=0), axis=1)
    ready_step_rot_deg = np.rad2deg(
        (ready_series.rotation[:-1].inv() * ready_series.rotation[1:]).magnitude()
    )
    ready_jump_mask = (
        (ready_dt < 100.0)
        & (
            (ready_step_pos > QUEST_JUMP_POSITION_M)
            | (ready_step_rot_deg > QUEST_JUMP_ROTATION_DEG)
        )
    )
    ready_jump_indices = np.flatnonzero(ready_jump_mask) + 1

    quality_dt = np.diff(quality_series.time_ms)
    quality_step_pos = np.linalg.norm(np.diff(quality_series.position_m, axis=0), axis=1)
    quality_step_rot_deg = np.rad2deg(
        (quality_series.rotation[:-1].inv() * quality_series.rotation[1:]).magnitude()
    )
    split_mask = (
        (quality_dt > 100.0)
        | (quality_step_pos > QUEST_JUMP_POSITION_M)
        | (quality_step_rot_deg > QUEST_JUMP_ROTATION_DEG)
    )
    split_indices = np.flatnonzero(split_mask) + 1
    segments = np.split(np.arange(len(quality_series.time_ms)), split_indices)
    selected = max(segments, key=len)
    series = PoseSeries(
        quality_series.time_ms[selected],
        quality_series.position_m[selected],
        quality_series.rotation[selected],
    )
    selected_dt = np.diff(series.time_ms)

    metadata = {
        "rows_total": int(len(frame)),
        "rows_sync_ready_render_valid": int(len(ready_raw)),
        "rows_quality_filtered": int(len(quality_raw)),
        "rows_quality_unique_time": int(len(quality_series.time_ms)),
        "selected_segment_rows": int(len(series.time_ms)),
        "selected_segment_start_ms": float(series.time_ms[0]),
        "selected_segment_end_ms": float(series.time_ms[-1]),
        "selected_segment_duration_s": float((series.time_ms[-1] - series.time_ms[0]) / 1000.0),
        "quality_filter": "Ready and RenderPoseTime valid; observation delay <=60 ms; prediction horizon <=70 ms; offset uncertainty <=3 ms",
        "quality_segments": [
            {
                "rows": int(len(segment)),
                "start_ms": float(quality_series.time_ms[segment[0]]),
                "end_ms": float(quality_series.time_ms[segment[-1]]),
                "duration_s": float(
                    (quality_series.time_ms[segment[-1]] - quality_series.time_ms[segment[0]])
                    / 1000.0
                ),
            }
            for segment in segments
        ],
        "raw_pose_valid_rows": int(raw_valid.sum()),
        "sync_state_counts": {
            str(key): int(value)
            for key, value in frame["时钟同步状态"].value_counts(dropna=False).items()
        },
        "median_dt_ms": float(np.median(selected_dt)),
        "max_dt_ms": float(np.max(selected_dt)),
        "time_semantics": "PredictedDisplayTime proxy in Quest Unix minus per-row ClockDiff(VR-PC)",
        "clock_diff_median_ms": float(ready_raw["时钟偏移(ms)"].median()),
        "clock_diff_min_ms": float(ready_raw["时钟偏移(ms)"].min()),
        "clock_diff_max_ms": float(ready_raw["时钟偏移(ms)"].max()),
        "prediction_horizon_median_ms": float(ready_raw["PredictionHorizon(ms)"].median()),
        "prediction_horizon_p95_ms": float(ready_raw["PredictionHorizon(ms)"].quantile(0.95)),
        "observation_delay_median_ms": float(ready_raw["PoseReadTime至PC观测延迟(ms)"].median()),
        "observation_delay_p95_ms": float(ready_raw["PoseReadTime至PC观测延迟(ms)"].quantile(0.95)),
        "sync_rtt_median_ms": float(ready_raw["同步RTT(ms)"].median()),
        "offset_uncertainty_median_ms": float(ready_raw["偏移不确定度上界(ms)"].median()),
        "jumps": [
            {
                "row": int(index),
                "time_ms": float(ready_series.time_ms[index]),
                "position_jump_m": float(ready_step_pos[index - 1]),
                "rotation_jump_deg": float(ready_step_rot_deg[index - 1]),
            }
            for index in ready_jump_indices
        ],
    }
    return series, metadata, quality_raw


def interpolate_pose(series: PoseSeries, query_time_ms: np.ndarray) -> tuple[np.ndarray, Rotation]:
    query_time_ms = np.asarray(query_time_ms, dtype=float)
    if query_time_ms.min() < series.time_ms[0] or query_time_ms.max() > series.time_ms[-1]:
        raise ValueError("Pose interpolation requested outside source time range")
    position = np.column_stack(
        [np.interp(query_time_ms, series.time_ms, series.position_m[:, axis]) for axis in range(3)]
    )
    rotation = Slerp(series.time_ms, series.rotation)(query_time_ms)
    return position, rotation


def dense_kinematic_signals(series: PoseSeries, grid_ms: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    position, rotation = interpolate_pose(series, grid_ms)
    dt_s = float(np.median(np.diff(grid_ms)) / 1000.0)
    window = int(round(0.080 / dt_s))
    window = max(7, window + (1 - window % 2))
    if window >= len(grid_ms):
        window = len(grid_ms) - 1 if len(grid_ms) % 2 == 0 else len(grid_ms)
    position_smooth = savgol_filter(position, window_length=window, polyorder=3, axis=0, mode="interp")
    velocity = savgol_filter(
        position_smooth,
        window_length=window,
        polyorder=3,
        deriv=1,
        delta=dt_s,
        axis=0,
        mode="interp",
    )
    speed = np.linalg.norm(velocity, axis=1)

    angular_speed = np.empty(len(grid_ms), dtype=float)
    relative = rotation[:-2].inv() * rotation[2:]
    angular_speed[1:-1] = relative.magnitude() / (2.0 * dt_s)
    angular_speed[0] = angular_speed[1]
    angular_speed[-1] = angular_speed[-2]
    angular_speed = savgol_filter(
        angular_speed, window_length=window, polyorder=3, mode="interp"
    )
    angular_speed = np.clip(angular_speed, 0.0, None)
    return speed, angular_speed


def pearson(x: np.ndarray, y: np.ndarray) -> float:
    mask = np.isfinite(x) & np.isfinite(y)
    if mask.sum() < 20:
        return float("nan")
    x = x[mask]
    y = y[mask]
    sx = x.std()
    sy = y.std()
    if sx < 1e-12 or sy < 1e-12:
        return float("nan")
    return float(np.mean(((x - x.mean()) / sx) * ((y - y.mean()) / sy)))


def correlation_curves(
    grid_ms: np.ndarray,
    mocap_speed: np.ndarray,
    mocap_angular: np.ndarray,
    quest_speed: np.ndarray,
    quest_angular: np.ndarray,
    mask: np.ndarray,
    lags_ms: np.ndarray,
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    base_time = grid_ms[mask]
    m_speed = mocap_speed[mask]
    m_angular = mocap_angular[mask]
    pos_corr = np.empty(len(lags_ms), dtype=float)
    rot_corr = np.empty(len(lags_ms), dtype=float)
    for index, lag_ms in enumerate(lags_ms):
        q_speed = np.interp(base_time + lag_ms, grid_ms, quest_speed, left=np.nan, right=np.nan)
        q_angular = np.interp(base_time + lag_ms, grid_ms, quest_angular, left=np.nan, right=np.nan)
        pos_corr[index] = pearson(m_speed, q_speed)
        rot_corr[index] = pearson(m_angular, q_angular)
    combined = np.nanmean(np.vstack([pos_corr, rot_corr]), axis=0)
    return pos_corr, rot_corr, combined


def refine_peak(lags_ms: np.ndarray, values: np.ndarray) -> tuple[float, float, int]:
    index = int(np.nanargmax(values))
    lag = float(lags_ms[index])
    value = float(values[index])
    if 0 < index < len(values) - 1:
        ym = values[index - 1]
        y0 = values[index]
        yp = values[index + 1]
        denominator = ym - 2.0 * y0 + yp
        if np.isfinite(denominator) and abs(denominator) > 1e-12:
            fraction = 0.5 * (ym - yp) / denominator
            if abs(fraction) <= 1.0:
                lag += float(fraction * (lags_ms[1] - lags_ms[0]))
                value = float(y0 - 0.25 * (ym - yp) * fraction)
    return lag, value, index


def select_time_window(
    grid_ms: np.ndarray,
    mocap_speed: np.ndarray,
    mocap_angular: np.ndarray,
    quest_speed: np.ndarray,
    quest_angular: np.ndarray,
    search_start_ms: float,
    search_end_ms: float,
    lags_ms: np.ndarray,
) -> tuple[dict, pd.DataFrame, tuple[np.ndarray, np.ndarray, np.ndarray]]:
    rows = []
    latest_start = search_end_ms - WINDOW_SECONDS * 1000.0
    starts = np.arange(search_start_ms, latest_start + 1e-6, WINDOW_STEP_SECONDS * 1000.0)
    for start_ms in starts:
        end_ms = start_ms + WINDOW_SECONDS * 1000.0
        mask = (grid_ms >= start_ms) & (grid_ms <= end_ms)
        if mask.sum() < 100:
            continue
        pos_corr, rot_corr, combined = correlation_curves(
            grid_ms,
            mocap_speed,
            mocap_angular,
            quest_speed,
            quest_angular,
            mask,
            lags_ms,
        )
        lag_combined, corr_combined, _ = refine_peak(lags_ms, combined)
        lag_pos, corr_pos, _ = refine_peak(lags_ms, pos_corr)
        lag_rot, corr_rot, _ = refine_peak(lags_ms, rot_corr)
        activity_pos = float(np.std(mocap_speed[mask]))
        activity_rot = float(np.std(mocap_angular[mask]))
        agreement_penalty = 0.12 * abs(corr_pos - corr_rot)
        score = corr_combined - agreement_penalty
        rows.append(
            {
                "start_ms": float(start_ms),
                "end_ms": float(end_ms),
                "best_lag_combined_ms": lag_combined,
                "combined_peak_correlation": corr_combined,
                "best_lag_position_ms": lag_pos,
                "position_peak_correlation": corr_pos,
                "best_lag_rotation_ms": lag_rot,
                "rotation_peak_correlation": corr_rot,
                "mocap_speed_std_mps": activity_pos,
                "mocap_angular_speed_std_radps": activity_rot,
                "selection_score": score,
            }
        )
    window_table = pd.DataFrame(rows).sort_values("start_ms").reset_index(drop=True)
    if window_table.empty:
        raise RuntimeError("No valid cross-correlation windows")
    selected_row = window_table.loc[window_table["selection_score"].idxmax()].to_dict()
    selected_mask = (grid_ms >= selected_row["start_ms"]) & (grid_ms <= selected_row["end_ms"])
    selected_curves = correlation_curves(
        grid_ms,
        mocap_speed,
        mocap_angular,
        quest_speed,
        quest_angular,
        selected_mask,
        lags_ms,
    )
    return selected_row, window_table, selected_curves


def jackknife_lag(
    grid_ms: np.ndarray,
    mocap_speed: np.ndarray,
    mocap_angular: np.ndarray,
    quest_speed: np.ndarray,
    quest_angular: np.ndarray,
    start_ms: float,
    end_ms: float,
    lags_ms: np.ndarray,
    block_seconds: float = 2.0,
) -> np.ndarray:
    estimates = []
    base = (grid_ms >= start_ms) & (grid_ms <= end_ms)
    block_starts = np.arange(start_ms, end_ms, block_seconds * 1000.0)
    for block_start in block_starts:
        mask = base & ~(
            (grid_ms >= block_start) & (grid_ms < block_start + block_seconds * 1000.0)
        )
        _, _, combined = correlation_curves(
            grid_ms,
            mocap_speed,
            mocap_angular,
            quest_speed,
            quest_angular,
            mask,
            lags_ms,
        )
        lag, _, _ = refine_peak(lags_ms, combined)
        estimates.append(lag)
    return np.asarray(estimates, dtype=float)


def hand_eye_rotation_initialization(
    a_rotation: Rotation,
    b_rotation: Rotation,
    sample_rate_hz: float,
) -> Rotation:
    a_relative = []
    b_relative = []
    count = len(a_rotation)
    for gap_seconds in (0.35, 0.70, 1.40, 2.50):
        gap = max(1, int(round(gap_seconds * sample_rate_hz)))
        stride = max(1, int(round(0.20 * sample_rate_hz)))
        for index in range(0, count - gap, stride):
            ar = a_rotation[index].inv() * a_rotation[index + gap]
            br = b_rotation[index].inv() * b_rotation[index + gap]
            if max(ar.magnitude(), br.magnitude()) < np.deg2rad(4.0):
                continue
            a_relative.append(ar.as_quat())
            b_relative.append(br.as_quat())
    if len(a_relative) < 10:
        raise RuntimeError("Insufficient rotational excitation for hand-eye calibration")
    ar = Rotation.from_quat(np.asarray(a_relative))
    br = Rotation.from_quat(np.asarray(b_relative))

    def residual(rotvec: np.ndarray) -> np.ndarray:
        x_rotation = Rotation.from_rotvec(rotvec)
        mismatch = (ar * x_rotation).inv() * (x_rotation * br)
        return mismatch.as_rotvec().ravel()

    rng = np.random.default_rng(RANDOM_SEED)
    starts = [np.zeros(3)]
    starts.extend(Rotation.random(15, random_state=rng).as_rotvec())
    best = None
    for start in starts:
        result = least_squares(residual, start, loss="soft_l1", f_scale=np.deg2rad(1.0))
        cost = float(np.mean(residual(result.x) ** 2))
        if best is None or cost < best[0]:
            best = (cost, result.x)
    return Rotation.from_rotvec(best[1])


def average_rotation(rotation: Rotation) -> Rotation:
    return rotation.mean()


def solve_hand_eye(
    a_position: np.ndarray,
    a_rotation: Rotation,
    b_position: np.ndarray,
    b_rotation: Rotation,
    sample_rate_hz: float,
) -> dict:
    x_rotation_initial = hand_eye_rotation_initialization(a_rotation, b_rotation, sample_rate_hz)
    y_candidates = a_rotation * x_rotation_initial * b_rotation.inv()
    y_rotation_initial = average_rotation(y_candidates)

    lhs = np.concatenate(
        [
            a_rotation.as_matrix(),
            -np.broadcast_to(np.eye(3), (len(a_position), 3, 3)),
        ],
        axis=2,
    )
    lhs = lhs.reshape(-1, 6)
    rhs = (y_rotation_initial.apply(b_position) - a_position).reshape(-1)
    translation_solution, *_ = np.linalg.lstsq(lhs, rhs, rcond=None)
    x_translation_initial = translation_solution[:3]
    y_translation_initial = translation_solution[3:]

    initial = np.concatenate(
        [
            x_rotation_initial.as_rotvec(),
            x_translation_initial,
            y_rotation_initial.as_rotvec(),
            y_translation_initial,
        ]
    )
    position_scale_m = 0.005
    rotation_scale_rad = np.deg2rad(0.5)

    def residual(parameters: np.ndarray) -> np.ndarray:
        x_rotation = Rotation.from_rotvec(parameters[:3])
        x_translation = parameters[3:6]
        y_rotation = Rotation.from_rotvec(parameters[6:9])
        y_translation = parameters[9:12]
        left_position = a_position + a_rotation.apply(x_translation)
        right_position = y_translation + y_rotation.apply(b_position)
        left_rotation = a_rotation * x_rotation
        right_rotation = y_rotation * b_rotation
        position_error = (left_position - right_position) / position_scale_m
        rotation_error = (right_rotation.inv() * left_rotation).as_rotvec() / rotation_scale_rad
        return np.concatenate([position_error.ravel(), rotation_error.ravel()])

    fit = least_squares(
        residual,
        initial,
        loss="soft_l1",
        f_scale=1.0,
        max_nfev=400,
        xtol=1e-12,
        ftol=1e-12,
        gtol=1e-12,
    )
    return {
        "x_rotation": Rotation.from_rotvec(fit.x[:3]),
        "x_translation": fit.x[3:6],
        "y_rotation": Rotation.from_rotvec(fit.x[6:9]),
        "y_translation": fit.x[9:12],
        "optimizer_success": bool(fit.success),
        "optimizer_message": str(fit.message),
        "optimizer_cost": float(fit.cost),
    }


def transformed_pose_errors(
    model: dict,
    a_position: np.ndarray,
    a_rotation: Rotation,
    b_position: np.ndarray,
    b_rotation: Rotation,
) -> dict:
    x_rotation = model["x_rotation"]
    x_translation = model["x_translation"]
    y_rotation = model["y_rotation"]
    y_translation = model["y_translation"]
    mocap_eye_position = a_position + a_rotation.apply(x_translation)
    quest_eye_position = y_translation + y_rotation.apply(b_position)
    mocap_eye_rotation = a_rotation * x_rotation
    quest_eye_rotation = y_rotation * b_rotation
    position_vector = quest_eye_position - mocap_eye_position
    position_error_mm = np.linalg.norm(position_vector, axis=1) * 1000.0
    rotation_error_deg = np.rad2deg(
        (mocap_eye_rotation.inv() * quest_eye_rotation).magnitude()
    )
    return {
        "mocap_eye_position": mocap_eye_position,
        "quest_eye_position": quest_eye_position,
        "mocap_eye_rotation": mocap_eye_rotation,
        "quest_eye_rotation": quest_eye_rotation,
        "position_vector_m": position_vector,
        "position_error_mm": position_error_mm,
        "rotation_error_deg": rotation_error_deg,
    }


def metric_summary(position_error_mm: np.ndarray, rotation_error_deg: np.ndarray) -> dict:
    return {
        "count": int(len(position_error_mm)),
        "position_mean_mm": float(np.mean(position_error_mm)),
        "position_median_mm": float(np.median(position_error_mm)),
        "position_rmse_mm": float(np.sqrt(np.mean(position_error_mm**2))),
        "position_p95_mm": float(np.percentile(position_error_mm, 95)),
        "position_max_mm": float(np.max(position_error_mm)),
        "rotation_mean_deg": float(np.mean(rotation_error_deg)),
        "rotation_median_deg": float(np.median(rotation_error_deg)),
        "rotation_rmse_deg": float(np.sqrt(np.mean(rotation_error_deg**2))),
        "rotation_p95_deg": float(np.percentile(rotation_error_deg, 95)),
        "rotation_max_deg": float(np.max(rotation_error_deg)),
    }


def block_bootstrap_metrics(
    position_error_mm: np.ndarray,
    rotation_error_deg: np.ndarray,
    sample_rate_hz: float,
    repetitions: int = 600,
) -> dict:
    rng = np.random.default_rng(RANDOM_SEED)
    n = len(position_error_mm)
    block = max(1, int(round(sample_rate_hz)))
    starts = np.arange(0, n, block)
    estimates = {
        "position_rmse_mm": [],
        "position_p95_mm": [],
        "rotation_rmse_deg": [],
        "rotation_p95_deg": [],
    }
    for _ in range(repetitions):
        sampled = []
        while len(sampled) < n:
            start = int(rng.choice(starts))
            sampled.extend(range(start, min(start + block, n)))
        index = np.asarray(sampled[:n], dtype=int)
        p = position_error_mm[index]
        r = rotation_error_deg[index]
        estimates["position_rmse_mm"].append(float(np.sqrt(np.mean(p**2))))
        estimates["position_p95_mm"].append(float(np.percentile(p, 95)))
        estimates["rotation_rmse_deg"].append(float(np.sqrt(np.mean(r**2))))
        estimates["rotation_p95_deg"].append(float(np.percentile(r, 95)))
    return {
        key: {
            "lower_95": float(np.percentile(value, 2.5)),
            "upper_95": float(np.percentile(value, 97.5)),
        }
        for key, value in estimates.items()
    }


def homogeneous(rotation: Rotation, translation: np.ndarray) -> np.ndarray:
    matrix = np.eye(4)
    matrix[:3, :3] = rotation.as_matrix()
    matrix[:3, 3] = translation
    return matrix


def local_time_string(timestamp_ms: float) -> str:
    value = pd.to_datetime(timestamp_ms, unit="ms", utc=True).tz_convert("Asia/Shanghai")
    return value.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]


def save_time_plot(
    path: Path,
    grid_ms: np.ndarray,
    mocap_speed: np.ndarray,
    mocap_angular: np.ndarray,
    quest_speed: np.ndarray,
    quest_angular: np.ndarray,
    selected: dict,
    window_table: pd.DataFrame,
    lags_ms: np.ndarray,
    curves: tuple[np.ndarray, np.ndarray, np.ndarray],
    best_lag_ms: float,
) -> None:
    plt.rcParams["font.sans-serif"] = ["Microsoft YaHei", "SimHei", "DejaVu Sans"]
    plt.rcParams["axes.unicode_minus"] = False
    fig, axes = plt.subplots(2, 2, figsize=(14, 9), constrained_layout=True)
    mask = (grid_ms >= selected["start_ms"]) & (grid_ms <= selected["end_ms"])
    time_s = (grid_ms[mask] - selected["start_ms"]) / 1000.0

    def zscore(values: np.ndarray) -> np.ndarray:
        return (values - np.mean(values)) / max(np.std(values), 1e-12)

    q_speed = np.interp(grid_ms[mask] + best_lag_ms, grid_ms, quest_speed)
    q_angular = np.interp(grid_ms[mask] + best_lag_ms, grid_ms, quest_angular)
    axes[0, 0].plot(time_s, zscore(mocap_speed[mask]), label="MoCap", lw=1.5)
    axes[0, 0].plot(time_s, zscore(q_speed), label="Quest shifted", lw=1.2, alpha=0.85)
    axes[0, 0].set_title("Selected window: translation speed (standardized)")
    axes[0, 0].set_xlabel("Time in window (s)")
    axes[0, 0].set_ylabel("z-score")
    axes[0, 0].legend()
    axes[0, 0].grid(alpha=0.25)

    axes[0, 1].plot(time_s, zscore(mocap_angular[mask]), label="MoCap", lw=1.5)
    axes[0, 1].plot(time_s, zscore(q_angular), label="Quest shifted", lw=1.2, alpha=0.85)
    axes[0, 1].set_title("Selected window: angular speed (standardized)")
    axes[0, 1].set_xlabel("Time in window (s)")
    axes[0, 1].set_ylabel("z-score")
    axes[0, 1].legend()
    axes[0, 1].grid(alpha=0.25)

    pos_corr, rot_corr, combined = curves
    axes[1, 0].plot(lags_ms, pos_corr, label="Translation speed")
    axes[1, 0].plot(lags_ms, rot_corr, label="Angular speed")
    axes[1, 0].plot(lags_ms, combined, label="Combined", lw=2.0)
    axes[1, 0].axvline(best_lag_ms, color="black", ls="--", lw=1.2, label=f"Best {best_lag_ms:.2f} ms")
    axes[1, 0].set_title("Cross-correlation curve")
    axes[1, 0].set_xlabel("lag in Quest(t + lag) vs MoCap(t), ms")
    axes[1, 0].set_ylabel("Pearson r")
    axes[1, 0].legend()
    axes[1, 0].grid(alpha=0.25)

    window_time_s = (window_table["start_ms"] - window_table["start_ms"].min()) / 1000.0
    scatter = axes[1, 1].scatter(
        window_time_s,
        window_table["best_lag_combined_ms"],
        c=window_table["combined_peak_correlation"],
        cmap="viridis",
        s=46,
    )
    axes[1, 1].axhline(best_lag_ms, color="black", ls="--", lw=1.0)
    axes[1, 1].set_title("Calibration-block sliding windows")
    axes[1, 1].set_xlabel("Window start relative to search start (s)")
    axes[1, 1].set_ylabel("Best lag (ms)")
    axes[1, 1].grid(alpha=0.25)
    fig.colorbar(scatter, ax=axes[1, 1], label="Combined peak r")
    fig.suptitle("Quest–MoCap temporal alignment", fontsize=16)
    fig.savefig(path, dpi=180)
    plt.close(fig)


def save_spatial_plot(
    path: Path,
    validation_time_ms: np.ndarray,
    errors: dict,
) -> None:
    plt.rcParams["font.sans-serif"] = ["Microsoft YaHei", "SimHei", "DejaVu Sans"]
    plt.rcParams["axes.unicode_minus"] = False
    fig = plt.figure(figsize=(14, 9), constrained_layout=True)
    ax3d = fig.add_subplot(2, 2, 1, projection="3d")
    mocap = errors["mocap_eye_position"]
    quest = errors["quest_eye_position"]
    ax3d.plot(mocap[:, 0], mocap[:, 1], mocap[:, 2], label="MoCap reference", lw=1.8)
    ax3d.plot(quest[:, 0], quest[:, 1], quest[:, 2], label="Quest registered", lw=1.2, alpha=0.85)
    ax3d.set_xlabel("X (m)")
    ax3d.set_ylabel("Y (m)")
    ax3d.set_zlabel("Z (m)")
    ax3d.set_title("Held-out trajectory in common frame")
    ax3d.legend()

    time_s = (validation_time_ms - validation_time_ms[0]) / 1000.0
    ax_pos = fig.add_subplot(2, 2, 2)
    colors = ["#d62728", "#2ca02c", "#1f77b4"]
    for axis, label, color in zip(range(3), ["X", "Y", "Z"], colors):
        ax_pos.plot(time_s, mocap[:, axis], color=color, lw=1.6, label=f"MoCap {label}")
        ax_pos.plot(time_s, quest[:, axis], color=color, ls="--", lw=1.0, alpha=0.8, label=f"Quest {label}")
    ax_pos.set_title("Held-out position components")
    ax_pos.set_xlabel("Held-out time (s)")
    ax_pos.set_ylabel("Position (m)")
    ax_pos.legend(ncol=2, fontsize=8)
    ax_pos.grid(alpha=0.25)

    ax_err = fig.add_subplot(2, 2, 3)
    ax_err.plot(time_s, errors["position_error_mm"], color="#d62728", lw=1.2)
    ax_err.axhline(np.percentile(errors["position_error_mm"], 95), color="black", ls="--", lw=1.0, label="P95")
    ax_err.set_title("Held-out position error")
    ax_err.set_xlabel("Held-out time (s)")
    ax_err.set_ylabel("Error (mm)")
    ax_err.legend()
    ax_err.grid(alpha=0.25)

    ax_rot = fig.add_subplot(2, 2, 4)
    ax_rot.plot(time_s, errors["rotation_error_deg"], color="#9467bd", lw=1.2)
    ax_rot.axhline(np.percentile(errors["rotation_error_deg"], 95), color="black", ls="--", lw=1.0, label="P95")
    ax_rot.set_title("Held-out SO(3) geodesic rotation error")
    ax_rot.set_xlabel("Held-out time (s)")
    ax_rot.set_ylabel("Error (degree)")
    ax_rot.legend()
    ax_rot.grid(alpha=0.25)
    fig.suptitle("Quest–MoCap spatial registration validation", fontsize=16)
    fig.savefig(path, dpi=180)
    plt.close(fig)


def image_data_uri(path: Path) -> str:
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:image/png;base64,{encoded}"


def html_metric_row(label: str, calibration: float, validation: float, unit: str) -> str:
    return (
        f"<tr><td>{label}</td><td>{calibration:.3f} {unit}</td>"
        f"<td>{validation:.3f} {unit}</td></tr>"
    )


def build_html_report(
    path: Path,
    results: dict,
    time_plot: Path,
    spatial_plot: Path,
) -> None:
    time = results["time_alignment"]
    cal = results["spatial_accuracy"]["calibration"]
    val = results["spatial_accuracy"]["held_out_validation"]
    ci = results["spatial_accuracy"]["held_out_block_bootstrap_95_interval"]
    x_matrix = np.asarray(results["spatial_registration"]["TrackerFromCenterEye"])
    y_matrix = np.asarray(results["spatial_registration"]["MocapUnityFromQuest"])

    matrix_html = lambda matrix: "<pre>" + "\n".join(
        "[ " + "  ".join(f"{value: .8f}" for value in row) + " ]" for row in matrix
    ) + "</pre>"
    metric_rows = "".join(
        [
            html_metric_row("Position median", cal["position_median_mm"], val["position_median_mm"], "mm"),
            html_metric_row("Position RMSE", cal["position_rmse_mm"], val["position_rmse_mm"], "mm"),
            html_metric_row("Position P95", cal["position_p95_mm"], val["position_p95_mm"], "mm"),
            html_metric_row("Rotation median", cal["rotation_median_deg"], val["rotation_median_deg"], "°"),
            html_metric_row("Rotation RMSE", cal["rotation_rmse_deg"], val["rotation_rmse_deg"], "°"),
            html_metric_row("Rotation P95", cal["rotation_p95_deg"], val["rotation_p95_deg"], "°"),
        ]
    )
    html = f"""<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><title>Quest–MoCap 时空对齐分析</title>
<style>
body{{font-family:'Microsoft YaHei','Segoe UI',sans-serif;margin:0;background:#f4f7fb;color:#1f2937;line-height:1.6}}
.wrap{{max-width:1280px;margin:0 auto;padding:28px}} h1{{margin:0 0 6px}} h2{{margin-top:34px;color:#0f3d66}}
.subtitle{{color:#64748b}} .cards{{display:grid;grid-template-columns:repeat(5,1fr);gap:14px;margin:24px 0}}
.card{{background:white;border-radius:12px;padding:18px;box-shadow:0 2px 10px #00000012;border-top:4px solid #2b6cb0}}
.card .v{{font-size:27px;font-weight:700;color:#0f3d66}} .card .k{{font-size:13px;color:#64748b}}
.panel{{background:white;border-radius:12px;padding:22px;margin:18px 0;box-shadow:0 2px 10px #00000010}}
img{{width:100%;height:auto;border-radius:8px}} table{{border-collapse:collapse;width:100%}} th,td{{padding:10px 12px;border-bottom:1px solid #e2e8f0;text-align:left}}
th{{background:#eaf2fb;color:#0f3d66}} code,pre{{font-family:Consolas,monospace}} pre{{background:#f8fafc;padding:14px;overflow:auto;border-radius:8px}}
.note{{background:#fff7ed;border-left:4px solid #f59e0b;padding:12px 16px}} .ok{{background:#ecfdf5;border-left:4px solid #10b981;padding:12px 16px}}
@media(max-width:900px){{.cards{{grid-template-columns:1fr 1fr}}}}
</style></head><body><div class="wrap">
<h1>Quest–MoCap 时间同步与空间精度</h1>
<div class="subtitle">数据：update_timestamp1 Tracker0 + QuestPose_20260829_173101｜生成：{results['generated_at']}</div>
<div class="cards">
  <div class="card"><div class="k">互相关时间偏移</div><div class="v">{time['best_lag_ms']:.2f} ms</div></div>
  <div class="card"><div class="k">组合峰值相关</div><div class="v">{time['combined_peak_correlation']:.3f}</div></div>
  <div class="card"><div class="k">验证位置 RMSE</div><div class="v">{val['position_rmse_mm']:.2f} mm</div></div>
  <div class="card"><div class="k">验证位置 P95</div><div class="v">{val['position_p95_mm']:.2f} mm</div></div>
  <div class="card"><div class="k">验证旋转 P95</div><div class="v">{val['rotation_p95_deg']:.2f}°</div></div>
</div>
<div class="ok"><b>时间偏移定义：</b>比较 MoCap(t) 与 Quest(t + lag)。负值表示要读取更早的 Quest 时间戳才能匹配 MoCap 在 t 的状态；它是两条完整测量链的残余相对时间偏移，不是 Quest-PC 时钟差。</div>

<h2>1. 时间互相关</h2><div class="panel"><img src="{image_data_uri(time_plot)}" alt="time alignment"></div>
<div class="panel"><table><tr><th>项目</th><th>结果</th></tr>
<tr><td>自动选择窗口</td><td>{time['selected_window_start_local']} ～ {time['selected_window_end_local']}</td></tr>
<tr><td>组合 lag</td><td>{time['best_lag_ms']:.3f} ms</td></tr>
<tr><td>位置速度 lag / 峰值 r</td><td>{time['position_best_lag_ms']:.3f} ms / {time['position_peak_correlation']:.4f}</td></tr>
<tr><td>角速度 lag / 峰值 r</td><td>{time['rotation_best_lag_ms']:.3f} ms / {time['rotation_peak_correlation']:.4f}</td></tr>
<tr><td>两通道 lag 差</td><td>{time['position_rotation_lag_difference_ms']:.3f} ms（方法/信号语义差异的保守提示）</td></tr>
<tr><td>删块 jackknife lag</td><td>均值 {time['jackknife_mean_ms']:.3f} ms，范围 [{time['jackknife_min_ms']:.3f}, {time['jackknife_max_ms']:.3f}] ms</td></tr>
<tr><td>9个重叠滑窗 lag</td><td>均值 {time['sliding_window_lag_mean_ms']:.3f} ms，SD {time['sliding_window_lag_std_ms']:.3f} ms，范围 [{time['sliding_window_lag_min_ms']:.3f}, {time['sliding_window_lag_max_ms']:.3f}] ms</td></tr>
<tr><td>Quest 时间列</td><td>PredictedDisplayTime代理-QuestUnix估算 - ClockDiff(VR-PC)</td></tr>
</table></div>

<h2>2. 空间配准与独立验证</h2><div class="panel"><img src="{image_data_uri(spatial_plot)}" alt="spatial validation"></div>
<div class="panel"><table><tr><th>指标</th><th>标定块</th><th>后40%连续留出块</th></tr>{metric_rows}</table>
<p>位置 RMSE 的1秒块bootstrap 95%区间：[{ci['position_rmse_mm']['lower_95']:.3f}, {ci['position_rmse_mm']['upper_95']:.3f}] mm；
旋转 RMSE 区间：[{ci['rotation_rmse_deg']['lower_95']:.3f}, {ci['rotation_rmse_deg']['upper_95']:.3f}]°。</p></div>

<h2>3. 方法与坐标控制</h2><div class="panel"><ol>
<li>动捕位置由 mm 换算为 m，并执行 <code>diag(-1,1,1)</code> 反射；旋转矩阵执行 <code>M R M</code>，得到 Unity 兼容坐标。</li>
<li>时间互相关使用平移速度模长与 SO(3) 角速度模长；二者对刚体坐标轴旋转、平移和四元数符号不敏感，分别标准化后等权组合。</li>
<li>仅在最长的无大于50 ms间断的动捕连续块内分析；前60%用于时间窗口选择和手眼空间标定，后40%作为连续留出验证。</li>
<li>冻结 lag 后，在标定块求解 <code>A(t) X = Y B(t+lag)</code>；验证块不再重新拟合。</li>
<li>旋转误差使用 SO(3) 测地角，位置误差使用三维欧氏距离。</li>
</ol></div>

<h2>4. 固定空间变换</h2><div class="panel"><h3>TrackerFromCenterEye (X)</h3>{matrix_html(x_matrix)}
<h3>MocapUnityFromQuest (Y)</h3>{matrix_html(y_matrix)}</div>

<h2>5. 解释边界</h2><div class="note">
当前 Quest CSV 的 RawPose 全部无效，因此结果针对保存的 RenderPredicted CenterEye 位姿。MoCap 被当作参考链，但本结果仍是系统间相对误差，不是外部计量学绝对真值。标定块残差不能当作最终精度，报告应优先使用后40%连续留出块。</div>
</div></body></html>"""
    path.write_text(html, encoding="utf-8")


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    mocap, mocap_meta, _ = read_mocap(MOCAP_FILE)
    quest, quest_meta, _ = read_quest(QUEST_FILE)

    overlap_start = max(mocap.time_ms[0], quest.time_ms[0]) + 250.0
    overlap_end = min(mocap.time_ms[-1], quest.time_ms[-1]) - 250.0
    if overlap_end - overlap_start < 20_000.0:
        raise RuntimeError("Insufficient common continuous duration")
    calibration_end = overlap_start + CALIBRATION_FRACTION * (overlap_end - overlap_start)

    grid_ms = np.arange(overlap_start, overlap_end, DENSE_DT_MS)
    mocap_speed, mocap_angular = dense_kinematic_signals(mocap, grid_ms)
    quest_speed, quest_angular = dense_kinematic_signals(quest, grid_ms)
    lags_ms = np.arange(
        LAG_SEARCH_MIN_MS,
        LAG_SEARCH_MAX_MS + LAG_SEARCH_STEP_MS * 0.5,
        LAG_SEARCH_STEP_MS,
    )
    search_start = overlap_start + 500.0
    search_end = calibration_end - 500.0
    selected, window_table, curves = select_time_window(
        grid_ms,
        mocap_speed,
        mocap_angular,
        quest_speed,
        quest_angular,
        search_start,
        search_end,
        lags_ms,
    )
    pos_corr, rot_corr, combined = curves
    representative_lag_ms, combined_peak, _ = refine_peak(lags_ms, combined)
    pos_lag_ms, pos_peak, _ = refine_peak(lags_ms, pos_corr)
    rot_lag_ms, rot_peak, _ = refine_peak(lags_ms, rot_corr)
    # Use the median of all qualified, strongly overlapping 12-s windows as the
    # primary point estimate.  The maximum-correlation window remains a
    # representative visualization only.  For this recording the two values
    # coincide numerically (both are -6.980221 ms).
    best_lag_ms = float(window_table["best_lag_combined_ms"].median())
    jackknife = jackknife_lag(
        grid_ms,
        mocap_speed,
        mocap_angular,
        quest_speed,
        quest_angular,
        selected["start_ms"],
        selected["end_ms"],
        lags_ms,
    )

    spatial_step_ms = 1000.0 / SPATIAL_RATE_HZ
    calibration_time = np.arange(overlap_start + 250.0, calibration_end - 250.0, spatial_step_ms)
    validation_time = np.arange(calibration_end + 250.0, overlap_end - 250.0, spatial_step_ms)
    a_cal_position, a_cal_rotation = interpolate_pose(mocap, calibration_time)
    b_cal_position, b_cal_rotation = interpolate_pose(quest, calibration_time + best_lag_ms)
    a_val_position, a_val_rotation = interpolate_pose(mocap, validation_time)
    b_val_position, b_val_rotation = interpolate_pose(quest, validation_time + best_lag_ms)

    model = solve_hand_eye(
        a_cal_position,
        a_cal_rotation,
        b_cal_position,
        b_cal_rotation,
        SPATIAL_RATE_HZ,
    )
    calibration_errors = transformed_pose_errors(
        model, a_cal_position, a_cal_rotation, b_cal_position, b_cal_rotation
    )
    validation_errors = transformed_pose_errors(
        model, a_val_position, a_val_rotation, b_val_position, b_val_rotation
    )
    calibration_metrics = metric_summary(
        calibration_errors["position_error_mm"], calibration_errors["rotation_error_deg"]
    )
    validation_metrics = metric_summary(
        validation_errors["position_error_mm"], validation_errors["rotation_error_deg"]
    )
    validation_ci = block_bootstrap_metrics(
        validation_errors["position_error_mm"],
        validation_errors["rotation_error_deg"],
        SPATIAL_RATE_HZ,
    )

    x_matrix = homogeneous(model["x_rotation"], model["x_translation"])
    y_matrix = homogeneous(model["y_rotation"], model["y_translation"])

    time_plot = OUTPUT_DIR / "01_time_cross_correlation.png"
    spatial_plot = OUTPUT_DIR / "02_spatial_validation.png"
    save_time_plot(
        time_plot,
        grid_ms,
        mocap_speed,
        mocap_angular,
        quest_speed,
        quest_angular,
        selected,
        window_table,
        lags_ms,
        curves,
        best_lag_ms,
    )
    save_spatial_plot(spatial_plot, validation_time, validation_errors)

    mocap_quat = validation_errors["mocap_eye_rotation"].as_quat()
    quest_quat = validation_errors["quest_eye_rotation"].as_quat()
    validation_table = pd.DataFrame(
        {
            "mocapTimePcMs": validation_time,
            "questEvaluatedTimePcMs": validation_time + best_lag_ms,
            "mocapEyeX_m": validation_errors["mocap_eye_position"][:, 0],
            "mocapEyeY_m": validation_errors["mocap_eye_position"][:, 1],
            "mocapEyeZ_m": validation_errors["mocap_eye_position"][:, 2],
            "questRegisteredX_m": validation_errors["quest_eye_position"][:, 0],
            "questRegisteredY_m": validation_errors["quest_eye_position"][:, 1],
            "questRegisteredZ_m": validation_errors["quest_eye_position"][:, 2],
            "mocapQuatX": mocap_quat[:, 0],
            "mocapQuatY": mocap_quat[:, 1],
            "mocapQuatZ": mocap_quat[:, 2],
            "mocapQuatW": mocap_quat[:, 3],
            "questQuatX": quest_quat[:, 0],
            "questQuatY": quest_quat[:, 1],
            "questQuatZ": quest_quat[:, 2],
            "questQuatW": quest_quat[:, 3],
            "errorX_mm": validation_errors["position_vector_m"][:, 0] * 1000.0,
            "errorY_mm": validation_errors["position_vector_m"][:, 1] * 1000.0,
            "errorZ_mm": validation_errors["position_vector_m"][:, 2] * 1000.0,
            "positionError_mm": validation_errors["position_error_mm"],
            "rotationError_deg": validation_errors["rotation_error_deg"],
        }
    )
    validation_table.to_csv(
        OUTPUT_DIR / "aligned_held_out_validation.csv", index=False, encoding="utf-8-sig"
    )
    window_table.assign(
        start_local=window_table["start_ms"].map(local_time_string),
        end_local=window_table["end_ms"].map(local_time_string),
    ).to_csv(OUTPUT_DIR / "cross_correlation_windows.csv", index=False, encoding="utf-8-sig")

    results = {
        "generated_at": datetime.now(timezone.utc).astimezone().isoformat(),
        "source_files": {"mocap": str(MOCAP_FILE), "quest": str(QUEST_FILE)},
        "definitions": {
            "lag": "compare MoCap(t) with Quest(t + lag_ms)",
            "clock_diff": "Quest(VR) - PC",
            "quest_pose_time": "PredictedDisplayTime proxy QuestUnix - per-row ClockDiff",
            "position_error": "Euclidean distance after frozen AX=YB calibration",
            "rotation_error": "SO(3) geodesic angle after frozen AX=YB calibration",
        },
        "data_quality": {
            "mocap": mocap_meta,
            "quest": quest_meta,
            "analysis_overlap_start_ms": float(overlap_start),
            "analysis_overlap_end_ms": float(overlap_end),
            "analysis_overlap_duration_s": float((overlap_end - overlap_start) / 1000.0),
            "calibration_fraction": CALIBRATION_FRACTION,
            "calibration_end_ms": float(calibration_end),
        },
        "time_alignment": {
            "best_lag_ms": float(best_lag_ms),
            "combined_peak_correlation": float(combined_peak),
            "lag_point_estimator": "median of nine overlapping 12-s window estimates",
            "representative_window_lag_ms": float(representative_lag_ms),
            "position_best_lag_ms": float(pos_lag_ms),
            "position_peak_correlation": float(pos_peak),
            "rotation_best_lag_ms": float(rot_lag_ms),
            "rotation_peak_correlation": float(rot_peak),
            "position_rotation_lag_difference_ms": float(pos_lag_ms - rot_lag_ms),
            "selected_window_start_ms": float(selected["start_ms"]),
            "selected_window_end_ms": float(selected["end_ms"]),
            "selected_window_start_local": local_time_string(selected["start_ms"]),
            "selected_window_end_local": local_time_string(selected["end_ms"]),
            "jackknife_estimates_ms": [float(value) for value in jackknife],
            "jackknife_mean_ms": float(np.mean(jackknife)),
            "jackknife_std_ms": float(np.std(jackknife, ddof=1)),
            "jackknife_conventional_se_ms": float(
                np.sqrt((len(jackknife) - 1.0) / len(jackknife) * np.sum((jackknife - np.mean(jackknife)) ** 2))
            ),
            "jackknife_min_ms": float(np.min(jackknife)),
            "jackknife_max_ms": float(np.max(jackknife)),
            "sliding_window_count": int(len(window_table)),
            "sliding_window_lag_mean_ms": float(window_table["best_lag_combined_ms"].mean()),
            "sliding_window_lag_std_ms": float(window_table["best_lag_combined_ms"].std(ddof=1)),
            "sliding_window_lag_min_ms": float(window_table["best_lag_combined_ms"].min()),
            "sliding_window_lag_max_ms": float(window_table["best_lag_combined_ms"].max()),
            "search_range_ms": [LAG_SEARCH_MIN_MS, LAG_SEARCH_MAX_MS],
            "search_step_ms": LAG_SEARCH_STEP_MS,
            "window_seconds": WINDOW_SECONDS,
            "signal": "equal-weight standardized translation-speed magnitude and SO(3) angular-speed magnitude",
        },
        "spatial_registration": {
            "equation": "MocapUnityFromTracker(t) * TrackerFromCenterEye = MocapUnityFromQuest * QuestFromCenterEye(t+lag)",
            "TrackerFromCenterEye": x_matrix.tolist(),
            "MocapUnityFromQuest": y_matrix.tolist(),
            "optimizer_success": model["optimizer_success"],
            "optimizer_message": model["optimizer_message"],
            "optimizer_cost": model["optimizer_cost"],
            "calibration_start_local": local_time_string(calibration_time[0]),
            "calibration_end_local": local_time_string(calibration_time[-1]),
            "validation_start_local": local_time_string(validation_time[0]),
            "validation_end_local": local_time_string(validation_time[-1]),
        },
        "spatial_accuracy": {
            "calibration": calibration_metrics,
            "held_out_validation": validation_metrics,
            "held_out_block_bootstrap_95_interval": validation_ci,
            "held_out_signed_axis_error_mm": {
                axis: {
                    "mean": float(np.mean(validation_errors["position_vector_m"][:, index]) * 1000.0),
                    "std": float(np.std(validation_errors["position_vector_m"][:, index], ddof=1) * 1000.0),
                }
                for index, axis in enumerate(("x", "y", "z"))
            },
        },
        "limitations": [
            "Quest RawPose is unavailable in this CSV; results apply to the stored RenderPredicted CenterEye pose.",
            "MoCap is treated as the reference chain, so metrics are inter-system relative errors rather than external absolute metrology.",
            "The time window is selected for high and mutually consistent cross-correlation inside the calibration block; spatial accuracy is therefore reported primarily on the disjoint held-out block.",
            "The sub-millisecond numerical peak interpolation does not imply sub-millisecond hardware accuracy; use jackknife spread and sampling/clock uncertainties when interpreting the lag.",
            "The nine timing windows overlap strongly; their standard deviation describes within-recording stability rather than an independent-repeat confidence interval or hardware accuracy.",
        ],
    }
    (OUTPUT_DIR / "analysis_results.json").write_text(
        json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    build_html_report(
        OUTPUT_DIR / "Quest_Mocap_UpdateTimestamp_TimeSpace_Analysis_20260831.html",
        results,
        time_plot,
        spatial_plot,
    )
    print(json.dumps({
        "best_lag_ms": results["time_alignment"]["best_lag_ms"],
        "combined_peak_correlation": results["time_alignment"]["combined_peak_correlation"],
        "selected_window": [
            results["time_alignment"]["selected_window_start_local"],
            results["time_alignment"]["selected_window_end_local"],
        ],
        "calibration": calibration_metrics,
        "held_out_validation": validation_metrics,
        "output_dir": str(OUTPUT_DIR),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
