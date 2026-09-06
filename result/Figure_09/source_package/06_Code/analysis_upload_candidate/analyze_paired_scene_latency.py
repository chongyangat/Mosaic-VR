from __future__ import annotations

import argparse
import csv
import itertools
import math
from pathlib import Path

import numpy as np
import pandas as pd


METRICS = ("median_ms", "p99_ms")
SCENES = ("supermarket", "street")
LINK_ORDER = (
    "EyeTracking→PC",
    "MoCap→PC",
    "MoCap→VR",
    "PC<->VR 状态同步",
)
DISPLAY_LINK = {
    "EyeTracking→PC": "EyeTracking→PC",
    "MoCap→PC": "MoCap→PC",
    "MoCap→VR": "MoCap→VR",
    "PC<->VR 状态同步": "PC–VR shared-state RTT",
}


def mean_error(values: np.ndarray) -> dict[str, float]:
    values = np.asarray(values, dtype=float)
    n = len(values)
    sd = float(np.std(values, ddof=1))
    sem = sd / math.sqrt(n)
    # The current design has n=20 pooled runs and n=10 runs per scene.
    # These two-sided 95% Student-t critical values are fixed explicitly so
    # the analysis has no SciPy dependency.
    t975 = {9: 2.2621571628540993, 19: 2.093024054408263}.get(n - 1)
    if t975 is None:
        raise ValueError(f"No prespecified t critical value for n={n}")
    mean = float(np.mean(values))
    q1, q3 = np.quantile(values, [0.25, 0.75])
    return {
        "n": n,
        "mean_ms": mean,
        "sd_ms": sd,
        "sem_ms": sem,
        "mean_ci95_low_ms": mean - t975 * sem,
        "mean_ci95_high_ms": mean + t975 * sem,
        "median_ms": float(np.median(values)),
        "q1_ms": float(q1),
        "q3_ms": float(q3),
        "minimum_ms": float(np.min(values)),
        "maximum_ms": float(np.max(values)),
    }


def average_ranks(values: np.ndarray) -> np.ndarray:
    values = np.asarray(values, dtype=float)
    order = np.argsort(values, kind="mergesort")
    ranks = np.empty(len(values), dtype=float)
    start = 0
    while start < len(values):
        end = start + 1
        while end < len(values) and values[order[end]] == values[order[start]]:
            end += 1
        average = (start + 1 + end) / 2.0
        ranks[order[start:end]] = average
        start = end
    return ranks


def exact_wilcoxon_signed_rank(differences: np.ndarray) -> dict[str, float | int]:
    differences = np.asarray(differences, dtype=float)
    differences = differences[np.abs(differences) > 1e-12]
    if len(differences) == 0:
        return {
            "nonzero_pairs_n": 0,
            "wilcoxon_w": 0.0,
            "exact_two_sided_p": 1.0,
            "rank_biserial": 0.0,
        }
    ranks = average_ranks(np.abs(differences))
    signed_sum = float(np.sum(np.sign(differences) * ranks))
    positive_sum = float(np.sum(ranks[differences > 0]))
    negative_sum = float(np.sum(ranks[differences < 0]))
    wilcoxon_w = min(positive_sum, negative_sum)
    absolute_observed = abs(signed_sum)
    extreme = 0
    total = 2 ** len(ranks)
    for signs in itertools.product((-1.0, 1.0), repeat=len(ranks)):
        statistic = abs(float(np.dot(np.asarray(signs), ranks)))
        if statistic >= absolute_observed - 1e-12:
            extreme += 1
    return {
        "nonzero_pairs_n": len(ranks),
        "wilcoxon_w": wilcoxon_w,
        "exact_two_sided_p": extreme / total,
        "rank_biserial": signed_sum / float(np.sum(ranks)),
    }


def holm_adjust(p_values: list[float]) -> list[float]:
    count = len(p_values)
    order = sorted(range(count), key=lambda index: p_values[index])
    adjusted = [0.0] * count
    running = 0.0
    for rank, index in enumerate(order):
        candidate = (count - rank) * p_values[index]
        running = max(running, candidate)
        adjusted[index] = min(1.0, running)
    return adjusted


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def build(input_path: Path, output_dir: Path) -> tuple[Path, Path]:
    frame = pd.read_csv(input_path)
    required = {"participant", "scene", "link", *METRICS}
    missing = sorted(required - set(frame.columns))
    if missing:
        raise ValueError(f"Missing required columns: {missing}")
    if set(frame["scene"]) != set(SCENES):
        raise ValueError(f"Expected scenes {SCENES}, found {sorted(frame['scene'].unique())}")

    descriptive_rows: list[dict[str, object]] = []
    test_rows: list[dict[str, object]] = []
    for link in LINK_ORDER:
        link_frame = frame.loc[frame["link"] == link].copy()
        if len(link_frame) != 20:
            raise ValueError(f"Expected 20 rows for {link}, found {len(link_frame)}")
        for metric in METRICS:
            for scope in ("all_20_runs", *SCENES):
                values = (
                    link_frame[metric].to_numpy(float)
                    if scope == "all_20_runs"
                    else link_frame.loc[link_frame["scene"] == scope, metric].to_numpy(float)
                )
                descriptive_rows.append(
                    {
                        "scope": scope,
                        "link": DISPLAY_LINK[link],
                        "metric": metric,
                        **mean_error(values),
                    }
                )

            paired = link_frame.pivot(index="participant", columns="scene", values=metric)
            paired = paired.loc[:, list(SCENES)].dropna()
            if len(paired) != 10:
                raise ValueError(f"Expected 10 paired participants for {link}/{metric}, found {len(paired)}")
            difference = paired["street"].to_numpy(float) - paired["supermarket"].to_numpy(float)
            inferential = exact_wilcoxon_signed_rank(difference)
            difference_summary = mean_error(difference)
            test_rows.append(
                {
                    "metric": metric,
                    "link": DISPLAY_LINK[link],
                    "paired_participants_n": len(paired),
                    "nonzero_pairs_n": inferential["nonzero_pairs_n"],
                    "supermarket_mean_ms": float(paired["supermarket"].mean()),
                    "supermarket_sd_ms": float(paired["supermarket"].std(ddof=1)),
                    "street_mean_ms": float(paired["street"].mean()),
                    "street_sd_ms": float(paired["street"].std(ddof=1)),
                    "street_minus_supermarket_mean_ms": difference_summary["mean_ms"],
                    "difference_sd_ms": difference_summary["sd_ms"],
                    "difference_mean_ci95_low_ms": difference_summary["mean_ci95_low_ms"],
                    "difference_mean_ci95_high_ms": difference_summary["mean_ci95_high_ms"],
                    "wilcoxon_w": inferential["wilcoxon_w"],
                    "exact_two_sided_p": inferential["exact_two_sided_p"],
                    "holm_adjusted_p": None,
                    "rank_biserial_street_minus_supermarket": inferential["rank_biserial"],
                    "decision_alpha_0_05": None,
                }
            )

    for metric in METRICS:
        indices = [index for index, row in enumerate(test_rows) if row["metric"] == metric]
        adjusted = holm_adjust([float(test_rows[index]["exact_two_sided_p"]) for index in indices])
        for index, adjusted_p in zip(indices, adjusted):
            test_rows[index]["holm_adjusted_p"] = adjusted_p
            test_rows[index]["decision_alpha_0_05"] = (
                "scene effect detected" if adjusted_p < 0.05 else "no scene effect detected"
            )

    descriptive_path = output_dir / "Latency_descriptive_statistics_20_runs.csv"
    tests_path = output_dir / "Paired_scene_latency_tests.csv"
    write_csv(descriptive_path, descriptive_rows)
    write_csv(tests_path, test_rows)
    return descriptive_path, tests_path


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Summarize 20 participant-scene runs and compare paired scenes."
    )
    script_path = Path(__file__).resolve()
    submission_root = script_path.parents[2]
    parser.add_argument(
        "--input",
        type=Path,
        default=submission_root / "05_Data" / "reproduced_output" / "Participant_level_latency_metrics.csv",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=submission_root / "05_Data" / "reproduced_output",
    )
    args = parser.parse_args()
    descriptive, tests = build(args.input, args.output_dir)
    print(descriptive)
    print(tests)


if __name__ == "__main__":
    main()
