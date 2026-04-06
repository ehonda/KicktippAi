from __future__ import annotations

import argparse
import json
import math
import warnings
from dataclasses import dataclass
from itertools import combinations
from pathlib import Path
from typing import Any, Callable, Sequence

import numpy as np
import pandas as pd
from scipy import stats as scipy_stats
from statsmodels.stats.multitest import multipletests


class AnalysisError(Exception):
    pass


@dataclass(frozen=True)
class OutputPaths:
    json_path: Path
    markdown_path: Path


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate a statistical comparison report from a normalized experiment analysis bundle."
    )
    parser.add_argument("--input", required=True, help="Path to the normalized analysis bundle JSON file")
    parser.add_argument(
        "--json-output",
        help="Optional output path for the machine-readable report JSON. Defaults next to the input bundle.",
    )
    parser.add_argument(
        "--markdown-output",
        help="Optional output path for the human-readable Markdown report. Defaults next to the input bundle.",
    )
    parser.add_argument(
        "--alpha",
        type=float,
        default=0.05,
        help="Significance level used for Wilcoxon, Friedman, and pairwise correction (default: 0.05)",
    )
    parser.add_argument(
        "--correction-method",
        default="holm",
        choices=["holm", "bonferroni", "fdr_bh"],
        help="Multiple-comparison correction method for 3+ run pairwise tests (default: holm)",
    )
    parser.add_argument(
        "--bootstrap-resamples",
        type=int,
        default=10_000,
        help="Bootstrap resample count for effect-size confidence intervals (default: 10000)",
    )
    parser.add_argument(
        "--confidence-level",
        type=float,
        default=0.95,
        help="Bootstrap confidence level for effect-size intervals (default: 0.95)",
    )
    parser.add_argument(
        "--random-seed",
        type=int,
        default=20260406,
        help="Random seed used for bootstrap resampling (default: 20260406)",
    )
    return parser.parse_args(argv)


def resolve_output_paths(input_path: Path, json_output: str | None, markdown_output: str | None) -> OutputPaths:
    stem = input_path.with_suffix("")
    return OutputPaths(
        json_path=Path(json_output) if json_output else stem.with_name(f"{stem.name}.report.json"),
        markdown_path=Path(markdown_output) if markdown_output else stem.with_name(f"{stem.name}.report.md"),
    )


def load_bundle(path: Path) -> dict[str, Any]:
    bundle = json.loads(path.read_text(encoding="utf-8"))
    required_top_level = {"datasetName", "taskType", "primaryMetricName", "runs", "rows"}
    missing = sorted(required_top_level.difference(bundle.keys()))
    if missing:
        raise AnalysisError(f"Bundle '{path}' is missing required top-level field(s): {', '.join(missing)}")
    return bundle


def analyze_bundle(
    bundle: dict[str, Any],
    *,
    alpha: float,
    correction_method: str,
    bootstrap_resamples: int,
    confidence_level: float,
    random_seed: int,
) -> dict[str, Any]:
    rows_df = pd.DataFrame(bundle["rows"])
    runs_df = pd.DataFrame(bundle["runs"])
    run_input_records = runs_df.to_dict(orient="records")

    required_row_fields = {"pairingKey", "runName", "kicktippPoints"}
    missing_row_fields = sorted(required_row_fields.difference(rows_df.columns))
    if missing_row_fields:
        raise AnalysisError(f"Bundle rows are missing required field(s): {', '.join(missing_row_fields)}")

    required_run_fields = {"runName", "model", "primaryMetricValue", "aggregateScores"}
    missing_run_fields = sorted(required_run_fields.difference(runs_df.columns))
    if missing_run_fields:
        raise AnalysisError(f"Bundle runs are missing required field(s): {', '.join(missing_run_fields)}")

    duplicate_rows = rows_df.duplicated(subset=["runName", "pairingKey"], keep=False)
    if duplicate_rows.any():
        duplicates = rows_df.loc[duplicate_rows, ["runName", "pairingKey"]].drop_duplicates()
        duplicate_descriptions = [f"{row.runName}:{row.pairingKey}" for row in duplicates.itertuples(index=False)]
        raise AnalysisError(
            "Bundle contains duplicate run/pairing rows: " + ", ".join(duplicate_descriptions)
        )

    run_names = [str(name) for name in runs_df["runName"].tolist()]
    row_run_names = set(rows_df["runName"])
    missing_row_runs = sorted(set(run_names).difference(row_run_names))
    extra_row_runs = sorted(row_run_names.difference(set(run_names)))
    if missing_row_runs or extra_row_runs:
        message_parts: list[str] = []
        if missing_row_runs:
            message_parts.append("missing row data for " + ", ".join(missing_row_runs))
        if extra_row_runs:
            message_parts.append("unexpected row data for " + ", ".join(extra_row_runs))
        raise AnalysisError("Run list and row list do not match: " + "; ".join(message_parts))

    paired_scores = (
        rows_df.pivot(index="pairingKey", columns="runName", values="kicktippPoints")
        .reindex(columns=run_names)
        .sort_index()
    )
    if paired_scores.isnull().any().any():
        incomplete_pairings = paired_scores[paired_scores.isnull().any(axis=1)].index.tolist()
        raise AnalysisError(
            "Comparable analysis requires complete pairings across all runs. Missing pairing(s): "
            + ", ".join(str(key) for key in incomplete_pairings)
        )

    ranking_df = runs_df.sort_values(["primaryMetricValue", "runName"], ascending=[False, True]).reset_index(drop=True)
    ranking_input_records = ranking_df.to_dict(orient="records")
    ranking_records = [
        {
            "rank": index + 1,
            "runName": str(row["runName"]),
            "model": str(row["model"]),
            "promptKey": row.get("promptKey"),
            "sliceKey": row.get("sliceKey"),
            "sliceKind": row.get("sliceKind"),
            "primaryMetricValue": float(row["primaryMetricValue"]),
            "aggregateScores": row["aggregateScores"],
            "rowCount": int(row.get("rowCount", len(paired_scores))),
        }
        for index, row in enumerate(ranking_input_records, start=0)
    ]

    primary_metric_by_run = {
        str(row["runName"]): float(row["primaryMetricValue"])
        for row in run_input_records
    }

    report: dict[str, Any] = {
        "datasetName": bundle["datasetName"],
        "taskType": bundle["taskType"],
        "primaryMetricName": bundle["primaryMetricName"],
        "runCount": len(run_names),
        "pairingCount": int(len(paired_scores)),
        "alpha": alpha,
        "correctionMethod": correction_method,
        "runs": ranking_records,
    }

    if len(run_names) == 2:
        report["comparison"] = analyze_two_run_comparison(
            ranking_input_records,
            paired_scores,
            bundle["primaryMetricName"],
            alpha=alpha,
            bootstrap_resamples=bootstrap_resamples,
            confidence_level=confidence_level,
            random_seed=random_seed,
        )
    else:
        report["friedman"] = analyze_friedman(paired_scores)
        report["pairwiseComparisons"] = analyze_pairwise_comparisons(
            ranking_input_records,
            primary_metric_by_run,
            paired_scores,
            bundle["primaryMetricName"],
            alpha=alpha,
            correction_method=correction_method,
            bootstrap_resamples=bootstrap_resamples,
            confidence_level=confidence_level,
            random_seed=random_seed,
        )

    return report


def analyze_two_run_comparison(
    ranking_runs: list[dict[str, Any]],
    paired_scores: pd.DataFrame,
    primary_metric_name: str,
    *,
    alpha: float,
    bootstrap_resamples: int,
    confidence_level: float,
    random_seed: int,
) -> dict[str, Any]:
    better_row = ranking_runs[0]
    other_row = ranking_runs[1]
    better_name = str(better_row["runName"])
    other_name = str(other_row["runName"])

    left = paired_scores[better_name].to_numpy(dtype=float)
    right = paired_scores[other_name].to_numpy(dtype=float)
    differences = left - right

    return {
        "betterRunName": better_name,
        "otherRunName": other_name,
        "primaryMetricName": primary_metric_name,
        "primaryMetricDelta": float(better_row["primaryMetricValue"] - other_row["primaryMetricValue"]),
        "meanDifference": float(np.mean(differences)),
        "medianDifference": float(np.median(differences)),
        "meanDifferenceConfidenceInterval": bootstrap_confidence_interval(
            differences,
            statistic_name="mean_difference",
            statistic_function=np.mean,
            bootstrap_resamples=bootstrap_resamples,
            confidence_level=confidence_level,
            random_seed=random_seed,
        ),
        "medianDifferenceConfidenceInterval": bootstrap_confidence_interval(
            differences,
            statistic_name="median_difference",
            statistic_function=np.median,
            bootstrap_resamples=bootstrap_resamples,
            confidence_level=confidence_level,
            random_seed=random_seed + 1,
        ),
        "perItemOutcomeCounts": compute_per_item_outcome_counts(left, right),
        "wilcoxon": run_wilcoxon_test(differences, alpha=alpha),
    }


def analyze_friedman(paired_scores: pd.DataFrame) -> dict[str, Any]:
    sample_arrays = [paired_scores[column].to_numpy(dtype=float) for column in paired_scores.columns]
    try:
        result = scipy_stats.friedmanchisquare(*sample_arrays)
    except ValueError as exc:
        return {
            "statistic": None,
            "pValue": None,
            "computed": False,
            "error": str(exc),
        }

    return {
        "statistic": float(getattr(result, "statistic")),
        "pValue": float(getattr(result, "pvalue")),
        "computed": True,
    }


def analyze_pairwise_comparisons(
    ranking_runs: list[dict[str, Any]],
    primary_metric_by_run: dict[str, float],
    paired_scores: pd.DataFrame,
    primary_metric_name: str,
    *,
    alpha: float,
    correction_method: str,
    bootstrap_resamples: int,
    confidence_level: float,
    random_seed: int,
) -> list[dict[str, Any]]:
    pairwise_results: list[dict[str, Any]] = []
    ranking_names = [str(run["runName"]) for run in ranking_runs]

    for index, (left_name, right_name) in enumerate(combinations(ranking_names, 2)):
        left_scores = paired_scores[left_name].to_numpy(dtype=float)
        right_scores = paired_scores[right_name].to_numpy(dtype=float)
        differences = left_scores - right_scores
        left_metric = primary_metric_by_run[left_name]
        right_metric = primary_metric_by_run[right_name]

        pairwise_results.append(
            {
                "runAName": left_name,
                "runBName": right_name,
                "primaryMetricName": primary_metric_name,
                "primaryMetricDelta": float(left_metric - right_metric),
                "meanDifference": float(np.mean(differences)),
                "medianDifference": float(np.median(differences)),
                "meanDifferenceConfidenceInterval": bootstrap_confidence_interval(
                    differences,
                    statistic_name="mean_difference",
                    statistic_function=np.mean,
                    bootstrap_resamples=bootstrap_resamples,
                    confidence_level=confidence_level,
                    random_seed=random_seed + index * 2,
                ),
                "medianDifferenceConfidenceInterval": bootstrap_confidence_interval(
                    differences,
                    statistic_name="median_difference",
                    statistic_function=np.median,
                    bootstrap_resamples=bootstrap_resamples,
                    confidence_level=confidence_level,
                    random_seed=random_seed + index * 2 + 1,
                ),
                "perItemOutcomeCounts": compute_per_item_outcome_counts(left_scores, right_scores),
                "wilcoxon": run_wilcoxon_test(differences, alpha=alpha),
            }
        )

    valid_pvalue_indices = [
        index
        for index, comparison in enumerate(pairwise_results)
        if comparison["wilcoxon"]["pValue"] is not None
    ]
    if valid_pvalue_indices:
        raw_pvalues = [pairwise_results[index]["wilcoxon"]["pValue"] for index in valid_pvalue_indices]
        reject, corrected_pvalues, _, _ = multipletests(raw_pvalues, alpha=alpha, method=correction_method)
        for reject_flag, corrected_pvalue, result_index in zip(reject, corrected_pvalues, valid_pvalue_indices):
            pairwise_results[result_index]["wilcoxon"]["adjustedPValue"] = float(corrected_pvalue)
            pairwise_results[result_index]["wilcoxon"]["significantAfterCorrection"] = bool(reject_flag)

    for comparison in pairwise_results:
        comparison["wilcoxon"].setdefault("adjustedPValue", comparison["wilcoxon"]["pValue"])
        comparison["wilcoxon"].setdefault("significantAfterCorrection", False)

    return pairwise_results


def run_wilcoxon_test(differences: np.ndarray, *, alpha: float) -> dict[str, Any]:
    effective_pairs = int(np.count_nonzero(differences))
    if differences.size == 0:
        return {
            "statistic": None,
            "pValue": None,
            "alpha": alpha,
            "effectivePairs": 0,
            "allDifferencesZero": False,
            "computed": False,
            "error": "No paired observations were available.",
        }

    if effective_pairs == 0:
        return {
            "statistic": 0.0,
            "pValue": 1.0,
            "alpha": alpha,
            "effectivePairs": 0,
            "allDifferencesZero": True,
            "computed": True,
            "significant": False,
            "method": "all_zero_short_circuit",
        }

    try:
        result = scipy_stats.wilcoxon(
            differences,
            zero_method="wilcox",
            alternative="two-sided",
            method="auto",
        )
    except ValueError as exc:
        return {
            "statistic": None,
            "pValue": None,
            "alpha": alpha,
            "effectivePairs": effective_pairs,
            "allDifferencesZero": False,
            "computed": False,
            "error": str(exc),
        }

    pvalue = float(getattr(result, "pvalue"))
    return {
        "statistic": float(getattr(result, "statistic")),
        "pValue": pvalue,
        "alpha": alpha,
        "effectivePairs": effective_pairs,
        "allDifferencesZero": False,
        "computed": True,
        "significant": bool(pvalue < alpha),
        "method": "wilcoxon_two_sided_auto",
    }


def bootstrap_confidence_interval(
    differences: np.ndarray,
    *,
    statistic_name: str,
    statistic_function: Callable[[np.ndarray], float],
    bootstrap_resamples: int,
    confidence_level: float,
    random_seed: int,
) -> dict[str, Any]:
    point_estimate = float(statistic_function(differences)) if differences.size else math.nan
    if differences.size == 0 or np.allclose(differences, differences[0]):
        return {
            "statisticName": statistic_name,
            "pointEstimate": point_estimate,
            "low": point_estimate,
            "high": point_estimate,
            "confidenceLevel": confidence_level,
        }

    def vectorized_statistic(sample: np.ndarray, axis: int = -1) -> np.ndarray:
        return statistic_function(sample, axis=axis)

    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        result = scipy_stats.bootstrap(
            (differences,),
            vectorized_statistic,
            confidence_level=confidence_level,
            n_resamples=bootstrap_resamples,
            rng=np.random.default_rng(random_seed),
            vectorized=True,
            method="basic",
        )

    return {
        "statisticName": statistic_name,
        "pointEstimate": point_estimate,
        "low": float(result.confidence_interval.low),
        "high": float(result.confidence_interval.high),
        "confidenceLevel": confidence_level,
    }


def compute_per_item_outcome_counts(left_scores: np.ndarray, right_scores: np.ndarray) -> dict[str, int]:
    return {
        "wins": int(np.sum(left_scores > right_scores)),
        "ties": int(np.sum(left_scores == right_scores)),
        "losses": int(np.sum(left_scores < right_scores)),
    }


def render_markdown(report: dict[str, Any]) -> str:
    lines = ["# Experiment Analysis Report", ""]
    lines.append(f"- Dataset: `{report['datasetName']}`")
    lines.append(f"- Task Type: `{report['taskType']}`")
    lines.append(f"- Primary Metric: `{report['primaryMetricName']}`")
    lines.append(f"- Runs: {report['runCount']}")
    lines.append(f"- Pairings: {report['pairingCount']}")
    lines.append("")
    lines.append("## Run Ranking")
    lines.append("")
    lines.extend(
        render_table(
            ["Rank", "Run", "Model", "Primary Metric"],
            [
                [str(run["rank"]), run["runName"], run["model"], format_number(run["primaryMetricValue"])]
                for run in report["runs"]
            ],
        )
    )
    lines.append("")

    if report["runCount"] == 2:
        comparison = report["comparison"]
        wilcoxon = comparison["wilcoxon"]
        lines.append("## Two-Run Comparison")
        lines.append("")
        lines.append(f"- Better Run: `{comparison['betterRunName']}`")
        lines.append(f"- Other Run: `{comparison['otherRunName']}`")
        lines.append(f"- {comparison['primaryMetricName']} Delta: {format_number(comparison['primaryMetricDelta'])}")
        lines.append(f"- Mean Difference: {format_number(comparison['meanDifference'])}")
        lines.append(f"- Median Difference: {format_number(comparison['medianDifference'])}")
        lines.append(
            "- Per-Item Win/Tie/Loss Counts: "
            f"{comparison['perItemOutcomeCounts']['wins']}/"
            f"{comparison['perItemOutcomeCounts']['ties']}/"
            f"{comparison['perItemOutcomeCounts']['losses']}"
        )
        lines.append(
            f"- Wilcoxon p-value: {format_number(wilcoxon['pValue']) if wilcoxon['pValue'] is not None else 'n/a'} "
            f"(significant: {'yes' if wilcoxon.get('significant') else 'no'})"
        )
        lines.append("")
        lines.append("## Effect Size Confidence Intervals")
        lines.append("")
        lines.extend(
            render_table(
                ["Statistic", "Point Estimate", "Low", "High"],
                [
                    [
                        "Mean Difference",
                        format_number(comparison["meanDifferenceConfidenceInterval"]["pointEstimate"]),
                        format_number(comparison["meanDifferenceConfidenceInterval"]["low"]),
                        format_number(comparison["meanDifferenceConfidenceInterval"]["high"]),
                    ],
                    [
                        "Median Difference",
                        format_number(comparison["medianDifferenceConfidenceInterval"]["pointEstimate"]),
                        format_number(comparison["medianDifferenceConfidenceInterval"]["low"]),
                        format_number(comparison["medianDifferenceConfidenceInterval"]["high"]),
                    ],
                ],
            )
        )
    else:
        friedman = report["friedman"]
        lines.append("## Multi-Run Comparison")
        lines.append("")
        lines.append(
            f"- Friedman p-value: {format_number(friedman['pValue']) if friedman['pValue'] is not None else 'n/a'}"
        )
        lines.append("")
        lines.append("## Pairwise Comparisons")
        lines.append("")
        lines.extend(
            render_table(
                [
                    "Run A",
                    "Run B",
                    "Primary Delta",
                    "Adj. p-value",
                    "Significant",
                    "Per-Item W/T/L",
                ],
                [
                    [
                        comparison["runAName"],
                        comparison["runBName"],
                        format_number(comparison["primaryMetricDelta"]),
                        format_number(comparison["wilcoxon"]["adjustedPValue"]) if comparison["wilcoxon"]["adjustedPValue"] is not None else "n/a",
                        "yes" if comparison["wilcoxon"].get("significantAfterCorrection") else "no",
                        (
                            f"{comparison['perItemOutcomeCounts']['wins']}/"
                            f"{comparison['perItemOutcomeCounts']['ties']}/"
                            f"{comparison['perItemOutcomeCounts']['losses']}"
                        ),
                    ]
                    for comparison in report["pairwiseComparisons"]
                ],
            )
        )

    lines.append("")
    lines.append("Per-item win/tie/loss counts compare paired Kicktipp points for the listed run ordering on each prepared dataset item.")
    return "\n".join(lines) + "\n"


def render_table(headers: list[str], rows: list[list[str]]) -> list[str]:
    header_line = "| " + " | ".join(headers) + " |"
    separator_line = "| " + " | ".join(["---"] * len(headers)) + " |"
    row_lines = ["| " + " | ".join(row) + " |" for row in rows]
    return [header_line, separator_line, *row_lines]


def format_number(value: float | None) -> str:
    if value is None or math.isnan(value):
        return "n/a"
    return f"{value:.4f}"


def write_outputs(report: dict[str, Any], output_paths: OutputPaths) -> str:
    markdown = render_markdown(report)
    output_paths.json_path.parent.mkdir(parents=True, exist_ok=True)
    output_paths.markdown_path.parent.mkdir(parents=True, exist_ok=True)
    output_paths.json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    output_paths.markdown_path.write_text(markdown, encoding="utf-8")
    return markdown


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    input_path = Path(args.input)
    output_paths = resolve_output_paths(input_path, args.json_output, args.markdown_output)

    try:
        bundle = load_bundle(input_path)
        report = analyze_bundle(
            bundle,
            alpha=args.alpha,
            correction_method=args.correction_method,
            bootstrap_resamples=args.bootstrap_resamples,
            confidence_level=args.confidence_level,
            random_seed=args.random_seed,
        )
        markdown = write_outputs(report, output_paths)
    except AnalysisError as exc:
        print(f"Error: {exc}")
        return 1

    print(markdown)
    print(f"JSON report: {output_paths.json_path}")
    print(f"Markdown report: {output_paths.markdown_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())