from __future__ import annotations

import argparse
import html
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
    html_path: Path | None


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
    html_output_group = parser.add_mutually_exclusive_group()
    html_output_group.add_argument(
        "--html-output",
        help=(
            "Optional output path for a browser-friendly HTML report. "
            "Defaults to a stable path under experiment-analysis/."
        ),
    )
    html_output_group.add_argument(
        "--no-html-output",
        action="store_true",
        help="Disable HTML report generation.",
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


def resolve_output_paths(
    bundle: dict[str, Any],
    input_path: Path,
    json_output: str | None,
    markdown_output: str | None,
    html_output: str | None,
    no_html_output: bool,
) -> OutputPaths:
    stem = input_path.with_suffix("")
    return OutputPaths(
        json_path=Path(json_output) if json_output else stem.with_name(f"{stem.name}.report.json"),
        markdown_path=Path(markdown_output) if markdown_output else stem.with_name(f"{stem.name}.report.md"),
        html_path=None
        if no_html_output
        else Path(html_output)
        if html_output
        else default_html_output_path(bundle, input_path),
    )


def default_html_output_path(bundle: dict[str, Any], input_path: Path) -> Path:
    stem = input_path.with_suffix("")
    dataset_segments = [segment.strip() for segment in str(bundle["datasetName"]).split("/") if segment.strip()]

    if len(dataset_segments) >= 4 and dataset_segments[0] == "match-predictions":
        site_segments = [dataset_segments[3], dataset_segments[2], *dataset_segments[4:]]
    else:
        site_segments = [str(bundle["taskType"]), *dataset_segments]

    return Path("experiment-analysis", *site_segments, f"{stem.name}.report.html")


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
                    "Raw p-value",
                    "Adj. p-value",
                    "Significant",
                    "Per-Item W/T/L",
                ],
                [
                    [
                        comparison["runAName"],
                        comparison["runBName"],
                        format_number(comparison["primaryMetricDelta"]),
                        format_number(comparison["wilcoxon"]["pValue"]) if comparison["wilcoxon"]["pValue"] is not None else "n/a",
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


def render_html(report: dict[str, Any]) -> str:
        title = f"Experiment Analysis - {report['datasetName']}"
        run_rows = "\n".join(
                render_html_table_row(
                        [
                                str(run["rank"]),
                                str(run["runName"]),
                                str(run["model"]),
                                format_number(run["primaryMetricValue"]),
                        ]
                )
                for run in report["runs"]
        )

        if report["runCount"] == 2:
                comparison = report["comparison"]
                wilcoxon = comparison["wilcoxon"]
                detail_section = f"""
                <section class=\"panel\">
                    <div class=\"panel-header\">
                        <h2>Two-run comparison</h2>
                        <span class=\"pill {'pill-good' if wilcoxon.get('significant') else 'pill-neutral'}\">{'significant' if wilcoxon.get('significant') else 'not significant'}</span>
                    </div>
                    <div class=\"metric-grid\">
                        {render_metric_card('Better run', comparison['betterRunName'])}
                        {render_metric_card('Other run', comparison['otherRunName'])}
                        {render_metric_card(comparison['primaryMetricName'] + ' delta', format_number(comparison['primaryMetricDelta']))}
                        {render_metric_card('Wilcoxon p-value', format_number(wilcoxon['pValue']))}
                        {render_metric_card('Mean difference', format_number(comparison['meanDifference']))}
                        {render_metric_card('Median difference', format_number(comparison['medianDifference']))}
                        {render_metric_card('Per-item W/T/L', format_outcome_counts(comparison['perItemOutcomeCounts']))}
                    </div>
                    <div class=\"panel-subsection\">
                        <h3>Effect size confidence intervals</h3>
                        <table>
                            <thead>
                                <tr>
                                    <th>Statistic</th>
                                    <th>Point estimate</th>
                                    <th>Low</th>
                                    <th>High</th>
                                </tr>
                            </thead>
                            <tbody>
                                {render_html_table_row([
                                        'Mean difference',
                                        format_number(comparison['meanDifferenceConfidenceInterval']['pointEstimate']),
                                        format_number(comparison['meanDifferenceConfidenceInterval']['low']),
                                        format_number(comparison['meanDifferenceConfidenceInterval']['high']),
                                ])}
                                {render_html_table_row([
                                        'Median difference',
                                        format_number(comparison['medianDifferenceConfidenceInterval']['pointEstimate']),
                                        format_number(comparison['medianDifferenceConfidenceInterval']['low']),
                                        format_number(comparison['medianDifferenceConfidenceInterval']['high']),
                                ])}
                            </tbody>
                        </table>
                    </div>
                </section>
                """
        else:
                friedman = report["friedman"]
                pairwise_rows = "\n".join(
                        render_html_table_row(
                                [
                                        str(comparison["runAName"]),
                                        str(comparison["runBName"]),
                                        format_number(comparison["primaryMetricDelta"]),
                                    format_number(comparison["wilcoxon"]["pValue"]),
                                        format_number(comparison["wilcoxon"]["adjustedPValue"]),
                                        "yes" if comparison["wilcoxon"].get("significantAfterCorrection") else "no",
                                        format_outcome_counts(comparison["perItemOutcomeCounts"]),
                                ]
                        )
                        for comparison in report["pairwiseComparisons"]
                )
                detail_section = f"""
                <section class=\"panel\">
                    <div class=\"panel-header\">
                        <h2>Multi-run comparison</h2>
                        <span class=\"pill pill-neutral\">Friedman p-value {format_number(friedman['pValue'])}</span>
                    </div>
                    <table>
                        <thead>
                            <tr>
                                <th>Run A</th>
                                <th>Run B</th>
                                <th>Primary delta</th>
                                <th>Raw p-value</th>
                                <th>Adjusted p-value</th>
                                <th>Significant</th>
                                <th>Per-item W/T/L</th>
                            </tr>
                        </thead>
                        <tbody>
                            {pairwise_rows}
                        </tbody>
                    </table>
                </section>
                """

        return f"""<!DOCTYPE html>
<html lang=\"en\">
<head>
    <meta charset=\"utf-8\">
    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">
    <title>{escape_html(title)}</title>
    <style>
        :root {{
            --bg: #f4efe6;
            --panel: rgba(255, 252, 246, 0.94);
            --panel-strong: #fffaf1;
            --text: #1e1a16;
            --muted: #6d6258;
            --border: rgba(86, 69, 53, 0.16);
            --accent: #b5532f;
            --accent-soft: rgba(181, 83, 47, 0.12);
            --good: #1c7b5b;
            --good-soft: rgba(28, 123, 91, 0.14);
            --shadow: 0 24px 70px rgba(70, 45, 26, 0.12);
        }}

        * {{ box-sizing: border-box; }}

        body {{
            margin: 0;
            font-family: "Segoe UI", "Trebuchet MS", sans-serif;
            color: var(--text);
            background:
                radial-gradient(circle at top left, rgba(181, 83, 47, 0.12), transparent 34%),
                radial-gradient(circle at top right, rgba(28, 123, 91, 0.10), transparent 28%),
                linear-gradient(180deg, #f7f1e8 0%, var(--bg) 100%);
        }}

        .page {{
            max-width: 1160px;
            margin: 0 auto;
            padding: 32px 20px 48px;
        }}

        .hero {{
            background: linear-gradient(140deg, rgba(255, 250, 241, 0.97), rgba(247, 238, 228, 0.94));
            border: 1px solid var(--border);
            border-radius: 28px;
            padding: 28px;
            box-shadow: var(--shadow);
            margin-bottom: 24px;
        }}

        .eyebrow {{
            margin: 0 0 8px;
            color: var(--accent);
            font-size: 0.78rem;
            font-weight: 700;
            letter-spacing: 0.14em;
            text-transform: uppercase;
        }}

        h1, h2, h3 {{ margin: 0; }}

        h1 {{
            font-size: clamp(1.9rem, 4vw, 3.2rem);
            line-height: 1.05;
            margin-bottom: 12px;
        }}

        .hero-meta {{
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
            margin-top: 16px;
        }}

        .pill {{
            display: inline-flex;
            align-items: center;
            padding: 6px 10px;
            border-radius: 999px;
            background: var(--accent-soft);
            color: var(--text);
            font-size: 0.9rem;
            font-weight: 600;
        }}

        .pill-good {{
            background: var(--good-soft);
            color: var(--good);
        }}

        .pill-neutral {{
            background: rgba(52, 68, 84, 0.08);
            color: #344454;
        }}

        .panel {{
            background: var(--panel);
            border: 1px solid var(--border);
            border-radius: 24px;
            padding: 24px;
            box-shadow: var(--shadow);
            margin-bottom: 20px;
        }}

        .panel-header {{
            display: flex;
            justify-content: space-between;
            gap: 12px;
            align-items: center;
            margin-bottom: 18px;
        }}

        .summary-grid,
        .metric-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 14px;
        }}

        .metric-card {{
            background: var(--panel-strong);
            border: 1px solid var(--border);
            border-radius: 18px;
            padding: 16px;
        }}

        .metric-label {{
            display: block;
            color: var(--muted);
            font-size: 0.82rem;
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 8px;
        }}

        .metric-value {{
            font-size: 1.28rem;
            font-weight: 700;
            line-height: 1.2;
            overflow-wrap: anywhere;
        }}

        .panel-subsection {{
            margin-top: 22px;
        }}

        table {{
            width: 100%;
            border-collapse: collapse;
            overflow: hidden;
            border-radius: 16px;
            border: 1px solid var(--border);
            background: var(--panel-strong);
        }}

        thead {{
            background: rgba(30, 26, 22, 0.06);
        }}

        th,
        td {{
            padding: 12px 14px;
            text-align: left;
            border-bottom: 1px solid var(--border);
            vertical-align: top;
        }}

        th {{
            font-size: 0.83rem;
            text-transform: uppercase;
            letter-spacing: 0.06em;
            color: var(--muted);
        }}

        tbody tr:last-child td {{
            border-bottom: none;
        }}

        .footnote {{
            margin: 0;
            color: var(--muted);
            font-size: 0.95rem;
            line-height: 1.55;
        }}

        @media (max-width: 720px) {{
            .page {{ padding: 20px 14px 32px; }}
            .hero, .panel {{ padding: 18px; border-radius: 20px; }}
            .panel-header {{ align-items: flex-start; flex-direction: column; }}
            table, thead, tbody, tr, th, td {{ display: block; }}
            thead {{ display: none; }}
            tr {{ border-bottom: 1px solid var(--border); padding: 10px 0; }}
            tr:last-child {{ border-bottom: none; }}
            td {{ border: none; padding: 6px 0; }}
            td::before {{
                content: attr(data-label);
                display: block;
                color: var(--muted);
                font-size: 0.76rem;
                text-transform: uppercase;
                letter-spacing: 0.08em;
                margin-bottom: 4px;
            }}
        }}
    </style>
</head>
<body>
    <main class=\"page\">
        <header class=\"hero\">
            <p class=\"eyebrow\">KicktippAi experiment analysis</p>
            <h1>{escape_html(report['datasetName'])}</h1>
            <div class=\"hero-meta\">
                <span class=\"pill\">Task: {escape_html(str(report['taskType']))}</span>
                <span class=\"pill\">Primary metric: {escape_html(str(report['primaryMetricName']))}</span>
                <span class=\"pill\">Runs: {report['runCount']}</span>
                <span class=\"pill\">Pairings: {report['pairingCount']}</span>
            </div>
        </header>

        <section class=\"panel\">
            <div class=\"panel-header\">
                <h2>Summary</h2>
            </div>
            <div class=\"summary-grid\">
                {render_metric_card('Dataset', report['datasetName'])}
                {render_metric_card('Task type', report['taskType'])}
                {render_metric_card('Primary metric', report['primaryMetricName'])}
                {render_metric_card('Alpha', format_number(report['alpha']))}
            </div>
        </section>

        <section class=\"panel\">
            <div class=\"panel-header\">
                <h2>Run ranking</h2>
            </div>
            <table>
                <thead>
                    <tr>
                        <th>Rank</th>
                        <th>Run</th>
                        <th>Model</th>
                        <th>Primary metric</th>
                    </tr>
                </thead>
                <tbody>
                    {run_rows}
                </tbody>
            </table>
        </section>

        {detail_section}

        <section class=\"panel\">
            <p class=\"footnote\">Per-item win/tie/loss counts compare paired Kicktipp points for the listed run ordering on each prepared dataset item.</p>
        </section>
    </main>
</body>
</html>
"""


def render_metric_card(label: str, value: Any) -> str:
        return (
                "<article class=\"metric-card\">"
                f"<span class=\"metric-label\">{escape_html(label)}</span>"
                f"<span class=\"metric-value\">{escape_html(str(value))}</span>"
                "</article>"
        )


def render_html_table_row(values: Sequence[Any]) -> str:
        cells = "".join(f"<td>{escape_html(str(value))}</td>" for value in values)
        return f"<tr>{cells}</tr>"


def format_outcome_counts(counts: dict[str, int]) -> str:
        return f"{counts['wins']}/{counts['ties']}/{counts['losses']}"


def escape_html(value: str) -> str:
        return html.escape(value, quote=True)


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
    if output_paths.html_path is not None:
        output_paths.html_path.parent.mkdir(parents=True, exist_ok=True)
        output_paths.html_path.write_text(render_html(report), encoding="utf-8")
    return markdown


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    input_path = Path(args.input)

    try:
        bundle = load_bundle(input_path)
        output_paths = resolve_output_paths(
            bundle,
            input_path,
            args.json_output,
            args.markdown_output,
            args.html_output,
            args.no_html_output,
        )
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
    if output_paths.html_path is not None:
        print(f"HTML report: {output_paths.html_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
