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


def resolve_run_display_name(run: dict[str, Any]) -> str:
    run_subject_display_name = normalize_optional_string(run.get("runSubjectDisplayName"))
    if run_subject_display_name is not None:
        return run_subject_display_name

    model = normalize_optional_string(run.get("model"))
    reasoning_effort = normalize_optional_string(run.get("reasoningEffort"))
    if model is not None:
        return f"{model} ({reasoning_effort})" if reasoning_effort is not None else model

    for field_name in ("runName",):
        value = normalize_optional_string(run.get(field_name))
        if value is not None:
            return value
    return ""


def normalize_optional_string(value: Any) -> str | None:
    if value is None:
        return None
    if isinstance(value, (float, np.floating)) and math.isnan(float(value)):
        return None

    text = str(value).strip()
    return text if text else None


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
            "runDisplayName": resolve_run_display_name(row),
            "model": str(row["model"]),
            "promptKey": row.get("promptKey"),
            "reasoningEffort": normalize_optional_string(row.get("reasoningEffort")),
            "sliceKey": row.get("sliceKey"),
            "sliceKind": row.get("sliceKind"),
            "runSubjectKind": normalize_optional_string(row.get("runSubjectKind")),
            "runSubjectId": normalize_optional_string(row.get("runSubjectId")),
            "runSubjectDisplayName": normalize_optional_string(row.get("runSubjectDisplayName")),
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
        "datasetDescription": normalize_optional_string(bundle.get("datasetDescription")),
        "datasetMetadata": bundle.get("datasetMetadata") if isinstance(bundle.get("datasetMetadata"), dict) else {},
        "taskType": bundle["taskType"],
        "primaryMetricName": bundle["primaryMetricName"],
        "runCount": len(run_names),
        "pairingCount": int(len(paired_scores)),
        "alpha": alpha,
        "correctionMethod": correction_method,
        "methodDescription": build_statistical_method_description(len(run_names), correction_method),
        "runs": ranking_records,
    }

    match_summary = build_match_summary(rows_df, report["datasetMetadata"])
    if match_summary is not None:
        report["matchSummary"] = match_summary

    prediction_distributions = build_prediction_distributions(rows_df, ranking_records)
    if prediction_distributions:
        report["predictionDistributions"] = prediction_distributions

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
        "betterRunDisplayName": resolve_run_display_name(better_row),
        "otherRunDisplayName": resolve_run_display_name(other_row),
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
    display_name_by_run = {
        str(run["runName"]): resolve_run_display_name(run)
        for run in ranking_runs
    }

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
                "runADisplayName": display_name_by_run[left_name],
                "runBDisplayName": display_name_by_run[right_name],
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


def build_statistical_method_description(run_count: int, correction_method: str) -> str:
    if run_count == 2:
        return (
            "Paired Wilcoxon signed-rank test on per-item Kicktipp-point differences; "
            "bootstrap confidence intervals summarize mean and median paired differences."
        )

    return (
        "Friedman test across all paired runs; pairwise Wilcoxon signed-rank tests use "
        f"{correction_method} correction, with bootstrap confidence intervals for paired differences."
    )


def build_match_summary(rows_df: pd.DataFrame, metadata: dict[str, Any]) -> dict[str, str] | None:
    home_teams = unique_column_strings(rows_df, "homeTeam")
    away_teams = unique_column_strings(rows_df, "awayTeam")
    expected_home_goals = unique_column_ints(rows_df, "expectedHomeGoals")
    expected_away_goals = unique_column_ints(rows_df, "expectedAwayGoals")

    fixture = normalize_optional_string(metadata.get("fixture"))
    if fixture is None and len(home_teams) == 1 and len(away_teams) == 1:
        fixture = f"{home_teams[0]} vs {away_teams[0]}"

    actual_result = normalize_optional_string(metadata.get("actualResult"))
    if actual_result is None and len(expected_home_goals) == 1 and len(expected_away_goals) == 1:
        actual_result = f"{expected_home_goals[0]}:{expected_away_goals[0]}"

    actual_result_display = normalize_optional_string(metadata.get("actualResultDisplay"))
    if actual_result_display is None and actual_result is not None:
        if len(home_teams) == 1 and len(away_teams) == 1 and len(expected_home_goals) == 1 and len(expected_away_goals) == 1:
            actual_result_display = (
                f"{home_teams[0]} {expected_home_goals[0]} - {expected_away_goals[0]} {away_teams[0]}"
            )
        else:
            actual_result_display = actual_result

    matchday = normalize_optional_string(metadata.get("matchday"))
    if matchday is None:
        matchdays = unique_column_strings(rows_df, "matchday")
        matchday = matchdays[0] if len(matchdays) == 1 else None

    starts_at_values = unique_column_strings(rows_df, "startsAt")
    starts_at = starts_at_values[0] if len(starts_at_values) == 1 else None

    summary = {
        "fixture": fixture,
        "actualResult": actual_result,
        "actualResultDisplay": actual_result_display,
        "matchday": matchday,
        "startsAt": starts_at,
    }
    populated_summary = {
        key: value
        for key, value in summary.items()
        if value is not None
    }
    return populated_summary or None


def build_prediction_distributions(
    rows_df: pd.DataFrame,
    ranking_records: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    required_columns = {"runName", "predictedHomeGoals", "predictedAwayGoals"}
    if required_columns.difference(rows_df.columns):
        return []

    distributions: list[dict[str, Any]] = []
    rows_by_run = {
        str(run_name): run_rows
        for run_name, run_rows in rows_df.groupby("runName", sort=False)
    }

    for run in ranking_records:
        run_name = str(run["runName"])
        run_rows = rows_by_run.get(run_name)
        if run_rows is None:
            continue

        counts_by_score: dict[tuple[int, int], int] = {}
        for row in run_rows.to_dict(orient="records"):
            home_goals = normalize_optional_int(row.get("predictedHomeGoals"))
            away_goals = normalize_optional_int(row.get("predictedAwayGoals"))
            if home_goals is None or away_goals is None:
                continue

            key = (home_goals, away_goals)
            counts_by_score[key] = counts_by_score.get(key, 0) + 1

        total_count = sum(counts_by_score.values())
        if total_count == 0:
            continue

        score_counts = [
            {
                "score": f"{home_goals}:{away_goals}",
                "homeGoals": home_goals,
                "awayGoals": away_goals,
                "count": count,
                "share": count / total_count,
            }
            for (home_goals, away_goals), count in counts_by_score.items()
        ]
        score_counts.sort(key=lambda item: (-int(item["count"]), int(item["homeGoals"]), int(item["awayGoals"])))
        distributions.append(
            {
                "runName": run_name,
                "runDisplayName": run["runDisplayName"],
                "model": run["model"],
                "totalCount": total_count,
                "scoreCounts": score_counts,
            }
        )

    return distributions


def unique_column_strings(rows_df: pd.DataFrame, column_name: str) -> list[str]:
    if column_name not in rows_df.columns:
        return []

    values: list[str] = []
    for raw_value in rows_df[column_name].tolist():
        value = normalize_optional_string(raw_value)
        if value is not None and value not in values:
            values.append(value)
    return values


def unique_column_ints(rows_df: pd.DataFrame, column_name: str) -> list[int]:
    if column_name not in rows_df.columns:
        return []

    values: list[int] = []
    for raw_value in rows_df[column_name].tolist():
        value = normalize_optional_int(raw_value)
        if value is not None and value not in values:
            values.append(value)
    return values


def normalize_optional_int(value: Any) -> int | None:
    if value is None:
        return None
    if isinstance(value, (float, np.floating)) and math.isnan(float(value)):
        return None

    try:
        number = float(value)
    except (TypeError, ValueError):
        return None

    if math.isnan(number) or not number.is_integer():
        return None
    return int(number)


def dataset_metadata_items(report: dict[str, Any]) -> list[tuple[str, str]]:
    metadata = report.get("datasetMetadata")
    if not isinstance(metadata, dict):
        return []

    labels = {
        "fixture": "Fixture",
        "actualResult": "Actual Result",
        "actualResultDisplay": "Actual Result",
        "matchday": "Matchday",
        "repetitionCount": "Repetitions",
        "interestingBecause": "Why Interesting",
        "datasetDescription": "Description",
        "competition": "Competition",
        "communityContext": "Community",
        "season": "Season",
        "sliceKey": "Slice",
        "sliceKind": "Slice Kind",
        "sampleMethod": "Sample Method",
        "sampleSize": "Sample Size",
        "sourcePoolKey": "Source Pool",
        "sourceDatasetName": "Source Dataset",
    }
    preferred_order = [
        "fixture",
        "actualResultDisplay",
        "matchday",
        "repetitionCount",
        "interestingBecause",
        "competition",
        "communityContext",
        "season",
        "sliceKey",
        "sourcePoolKey",
        "sampleSize",
    ]
    hidden_when_duplicate = {"actualResult", "datasetDescription"}
    ordered_keys = [
        key
        for key in preferred_order
        if key in metadata and key not in hidden_when_duplicate
    ]
    ordered_keys.extend(
        key
        for key in sorted(metadata)
        if key not in ordered_keys and key not in hidden_when_duplicate and metadata.get(key) is not None
    )

    return [
        (labels.get(key, humanize_metadata_key(key)), format_metadata_value(metadata[key]))
        for key in ordered_keys
        if format_metadata_value(metadata[key])
    ]


def humanize_metadata_key(key: str) -> str:
    text = key.replace("_", " ").replace("-", " ").strip()
    if not text:
        return key

    words: list[str] = []
    current = text[0]
    for character in text[1:]:
        if character.isupper() and current[-1].islower():
            words.append(current)
            current = character
        else:
            current += character
    words.append(current)
    return " ".join(word.capitalize() for word in words)


def format_metadata_value(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "yes" if value else "no"
    if isinstance(value, (dict, list)):
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return str(value)


def render_markdown(report: dict[str, Any]) -> str:
    lines = ["# Experiment Analysis Report", ""]
    lines.append(f"- Dataset: `{report['datasetName']}`")
    lines.append(f"- Task Type: `{report['taskType']}`")
    lines.append(f"- Primary Metric: `{report['primaryMetricName']}`")
    lines.append(f"- Runs: {report['runCount']}")
    lines.append(f"- Pairings: {report['pairingCount']}")
    if report.get("datasetDescription"):
        lines.append(f"- Dataset Description: {report['datasetDescription']}")
    lines.append(f"- Method: {report['methodDescription']}")
    metadata_items = dataset_metadata_items(report)
    if metadata_items:
        lines.append("")
        lines.append("## Dataset Metadata")
        lines.append("")
        lines.extend(render_table(["Field", "Value"], [[label, value] for label, value in metadata_items]))
    lines.append("")
    lines.append("## Community Standings" if is_community_standings_report(report) else "## Run Ranking")
    lines.append("")
    if is_community_standings_report(report):
        lines.extend(
            render_table(
                ["Rank", "Participant", "Kicktipp Points"],
                [
                    [str(run["rank"]), run["runDisplayName"], format_kicktipp_points(run["primaryMetricValue"])]
                    for run in report["runs"]
                ],
            )
        )
    else:
        lines.extend(
            render_table(
                ["Rank", "Run", "Model", "Primary Metric"],
                [
                    [str(run["rank"]), run["runDisplayName"], run["model"], format_number(run["primaryMetricValue"])]
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
        lines.append(f"- Better Run: `{comparison['betterRunDisplayName']}`")
        lines.append(f"- Other Run: `{comparison['otherRunDisplayName']}`")
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
                        comparison["runADisplayName"],
                        comparison["runBDisplayName"],
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
        at_a_glance_section = render_at_a_glance_section(report)
        metadata_items = dataset_metadata_items(report)
        dataset_description = report.get("datasetDescription")
        dataset_description_html = (
                f'<p class="footnote">{escape_html(str(dataset_description))}</p>'
                if dataset_description
                else ""
        )
        dataset_metadata_section = ""
        if metadata_items or dataset_description_html:
                metadata_rows = "\n".join(
                        render_html_table_row([label, value])
                        for label, value in metadata_items
                )
                metadata_table = f"""
                    <table>
                        <thead>
                            <tr>
                                <th>Field</th>
                                <th>Value</th>
                            </tr>
                        </thead>
                        <tbody>
                            {metadata_rows}
                        </tbody>
                    </table>
                """ if metadata_items else ""
                dataset_metadata_section = f"""
        <details class=\"panel collapsible-panel\">
            <summary class=\"collapsible-summary\">
                <h2>Dataset metadata</h2>
            </summary>
            <div class=\"collapsible-body\">
                {dataset_description_html}
                {metadata_table}
            </div>
        </details>
                """
        summary_section = f"""
        <details class=\"panel collapsible-panel\">
            <summary class=\"collapsible-summary\">
                <h2>Summary</h2>
            </summary>
            <div class=\"collapsible-body\">
                <div class=\"summary-grid\">
                    {render_metric_card('Dataset', report['datasetName'])}
                    {render_metric_card('Task type', report['taskType'])}
                    {render_metric_card('Primary metric', report['primaryMetricName'])}
                    {render_metric_card('Alpha', format_number(report['alpha']))}
                </div>
                <div class=\"panel-subsection\">
                    <p class=\"footnote\">{escape_html(str(report['methodDescription']))}</p>
                </div>
            </div>
        </details>
        """
        if is_community_standings_report(report):
                ranking_heading = "Community standings"
                default_baseline = str(report["runs"][0]["runName"]) if report["runs"] else ""
                ranking_table_attributes = (
                        ' class="standings-table" data-standings-table '
                        f'data-default-baseline="{escape_html(default_baseline)}"'
                )
                ranking_headers = "\n".join(
                        [
                                "                    <tr>",
                                "                        <th>Rank</th>",
                                "                        <th>Participant</th>",
                                "                        <th>Kicktipp Points</th>",
                                "                        <th>p-value vs baseline</th>",
                                "                    </tr>",
                        ]
                )
                run_rows = "\n".join(
                        render_standings_html_table_row(run)
                        for run in report["runs"]
                )
                standings_comparison_json = render_standings_comparison_json(report)
        else:
                ranking_heading = "Run ranking"
                ranking_table_attributes = ""
                ranking_headers = "\n".join(
                        [
                                "                    <tr>",
                                "                        <th>Rank</th>",
                                "                        <th>Run</th>",
                                "                        <th>Model</th>",
                                "                        <th>Primary metric</th>",
                                "                    </tr>",
                        ]
                )
                run_rows = "\n".join(
                        render_html_table_row(
                                [
                                        str(run["rank"]),
                                        str(run["runDisplayName"]),
                                        str(run["model"]),
                                        format_number(run["primaryMetricValue"]),
                                ]
                        )
                        for run in report["runs"]
                )
                standings_comparison_json = "{}"

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
                        {render_metric_card('Better run', comparison['betterRunDisplayName'])}
                        {render_metric_card('Other run', comparison['otherRunDisplayName'])}
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
                        render_pairwise_html_table_row(comparison)
                        for comparison in report["pairwiseComparisons"]
                )
                detail_section = f"""
                <section class=\"panel\">
                    <div class=\"panel-header\">
                        <h2>Multi-run comparison</h2>
                        <span class=\"pill pill-neutral\">Friedman p-value {format_number(friedman['pValue'])}</span>
                    </div>
                    <table class=\"sortable-table\" data-sortable-table>
                        <thead>
                            <tr>
                                {render_sortable_header('Run A', 'text')}
                                {render_sortable_header('Run B', 'text')}
                                {render_sortable_header('Primary delta', 'number')}
                                {render_sortable_header('Raw p-value', 'number')}
                                {render_sortable_header('Adjusted p-value', 'number')}
                                {render_sortable_header('Significant', 'boolean')}
                                {render_sortable_header('Per-item W/T/L', 'outcome')}
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
            --good-row: rgba(28, 123, 91, 0.08);
            --bad: #b33d3d;
            --bad-soft: rgba(179, 61, 61, 0.14);
            --bad-row: rgba(179, 61, 61, 0.08);
            --baseline: #b03a93;
            --baseline-soft: rgba(176, 58, 147, 0.16);
            --baseline-row: rgba(176, 58, 147, 0.12);
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

        .at-a-glance-panel {{
            border-color: rgba(28, 123, 91, 0.24);
        }}

        .glance-grid {{
            display: grid;
            grid-template-columns: minmax(260px, 1fr) minmax(320px, 1.35fr);
            gap: 16px;
            align-items: stretch;
        }}

        .glance-card {{
            background: var(--panel-strong);
            border: 1px solid var(--border);
            border-radius: 18px;
            padding: 18px;
            min-width: 0;
        }}

        .glance-label {{
            display: block;
            color: var(--muted);
            font-size: 0.78rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 8px;
        }}

        .match-fixture {{
            font-size: 1.45rem;
            line-height: 1.15;
            overflow-wrap: anywhere;
        }}

        .match-meta {{
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin: 14px 0;
        }}

        .meta-chip {{
            border: 1px solid var(--border);
            border-radius: 999px;
            color: var(--muted);
            display: inline-flex;
            font-size: 0.84rem;
            font-weight: 600;
            padding: 5px 9px;
        }}

        .actual-result {{
            margin-top: 12px;
        }}

        .actual-result-value {{
            display: block;
            font-size: 1.22rem;
            font-weight: 800;
            line-height: 1.2;
            overflow-wrap: anywhere;
        }}

        .head-to-head-card {{
            display: flex;
            flex-direction: column;
            gap: 14px;
        }}

        .head-to-head-topline {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
        }}

        .pvalue-summary {{
            border-radius: 12px;
            padding: 8px 10px;
            text-align: right;
        }}

        .pvalue-summary strong {{
            display: block;
            font-size: 1.05rem;
        }}

        .head-to-head-models {{
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
        }}

        .model-result {{
            border: 1px solid var(--border);
            border-radius: 14px;
            padding: 14px;
            min-width: 0;
            background: rgba(255, 255, 255, 0.45);
        }}

        .model-name {{
            font-size: 1.14rem;
            font-weight: 800;
            line-height: 1.15;
            overflow-wrap: anywhere;
        }}

        .model-points {{
            display: block;
            font-size: 1.8rem;
            font-weight: 800;
            line-height: 1;
            margin-top: 10px;
        }}

        .model-subtext {{
            display: block;
            color: var(--muted);
            font-size: 0.84rem;
            font-weight: 700;
            margin-top: 4px;
            text-transform: uppercase;
            letter-spacing: 0.06em;
        }}

        .head-to-head-neutral {{
            color: #53606c;
        }}

        .head-to-head-neutral .model-result {{
            background: rgba(52, 68, 84, 0.06);
            color: #53606c;
        }}

        .model-winner {{
            background: var(--good-soft);
            border-color: rgba(28, 123, 91, 0.36);
        }}

        .model-winner .model-name,
        .model-winner .model-points,
        .model-winner .model-subtext {{
            color: var(--good);
        }}

        .model-loser {{
            background: var(--bad-soft);
            border-color: rgba(179, 61, 61, 0.36);
        }}

        .model-loser .model-name,
        .model-loser .model-points,
        .model-loser .model-subtext {{
            color: var(--bad);
        }}

        .prediction-section {{
            margin-top: 18px;
        }}

        .prediction-section h3 {{
            font-size: 1.05rem;
            margin-bottom: 12px;
        }}

        .histogram-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
            gap: 14px;
        }}

        .histogram-card {{
            background: rgba(255, 250, 241, 0.76);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 14px;
            min-width: 0;
        }}

        .histogram-title {{
            display: flex;
            justify-content: space-between;
            gap: 10px;
            margin-bottom: 10px;
            font-weight: 800;
        }}

        .histogram-total {{
            color: var(--muted);
            flex: 0 0 auto;
            font-weight: 700;
        }}

        .histogram-row {{
            display: grid;
            grid-template-columns: 3.4rem minmax(90px, 1fr) 2.2rem;
            gap: 10px;
            align-items: center;
            min-height: 26px;
        }}

        .histogram-score,
        .histogram-count {{
            font-variant-numeric: tabular-nums;
            font-weight: 700;
        }}

        .histogram-count {{
            text-align: right;
        }}

        .histogram-track {{
            height: 10px;
            overflow: hidden;
            border-radius: 999px;
            background: rgba(30, 26, 22, 0.08);
        }}

        .histogram-bar {{
            display: block;
            height: 100%;
            min-width: 6px;
            border-radius: inherit;
            background: linear-gradient(90deg, var(--accent), var(--good));
        }}

        .collapsible-panel {{
            padding: 0;
            overflow: hidden;
        }}

        .collapsible-summary {{
            align-items: center;
            cursor: pointer;
            display: flex;
            justify-content: space-between;
            gap: 12px;
            list-style: none;
            padding: 22px 24px;
        }}

        .collapsible-summary::-webkit-details-marker {{
            display: none;
        }}

        .collapsible-summary::after {{
            align-items: center;
            background: rgba(52, 68, 84, 0.08);
            border-radius: 999px;
            content: "+";
            display: inline-flex;
            flex: 0 0 30px;
            font-size: 1.2rem;
            font-weight: 800;
            height: 30px;
            justify-content: center;
            line-height: 1;
            width: 30px;
        }}

        .collapsible-panel[open] .collapsible-summary::after {{
            content: "-";
        }}

        .collapsible-body {{
            padding: 0 24px 24px;
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

        .standings-table tbody tr {{
            cursor: pointer;
            transition: background 120ms ease;
        }}

        .standings-table tbody tr:hover,
        .standings-table tbody tr:focus-visible {{
            background: rgba(181, 83, 47, 0.08);
            outline: none;
        }}

        .standings-row-baseline {{
            background: linear-gradient(90deg, var(--baseline-row), rgba(255, 250, 241, 0.88));
        }}

        .standings-row-baseline td:first-child {{
            box-shadow: inset 4px 0 0 var(--baseline);
        }}

        .standings-row-better {{
            background: linear-gradient(90deg, var(--good-row), rgba(255, 250, 241, 0.86));
        }}

        .standings-row-better td:first-child {{
            box-shadow: inset 4px 0 0 var(--good);
        }}

        .standings-row-worse {{
            background: linear-gradient(90deg, var(--bad-row), rgba(255, 250, 241, 0.86));
        }}

        .standings-row-worse td:first-child {{
            box-shadow: inset 4px 0 0 var(--bad);
        }}

        .sortable-table th {{
            padding: 0;
        }}

        .sort-button {{
            width: 100%;
            border: 0;
            background: transparent;
            color: inherit;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: flex-start;
            gap: 8px;
            padding: 12px 14px;
            font: inherit;
            font-weight: 700;
            text-align: left;
            text-transform: inherit;
            letter-spacing: inherit;
        }}

        .sort-button:hover,
        .sort-button:focus-visible {{
            background: rgba(181, 83, 47, 0.08);
            outline: none;
        }}

        .sort-indicator {{
            position: relative;
            width: 8px;
            height: 13px;
            flex: 0 0 8px;
        }}

        .sort-indicator::before,
        .sort-indicator::after {{
            content: "";
            position: absolute;
            left: 0;
            border-left: 4px solid transparent;
            border-right: 4px solid transparent;
            opacity: 0.28;
        }}

        .sort-indicator::before {{
            top: 1px;
            border-bottom: 5px solid currentColor;
        }}

        .sort-indicator::after {{
            bottom: 1px;
            border-top: 5px solid currentColor;
        }}

        .sortable-table th[aria-sort="ascending"] .sort-indicator::before,
        .sortable-table th[aria-sort="descending"] .sort-indicator::after {{
            opacity: 1;
            color: var(--accent);
        }}

        .pairwise-row-significant {{
            background: linear-gradient(90deg, var(--good-row), rgba(255, 250, 241, 0.84));
        }}

        .pairwise-row-significant td:first-child {{
            box-shadow: inset 4px 0 0 var(--good);
        }}

        .significance-badge {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 2.6rem;
            border-radius: 999px;
            padding: 3px 9px;
            font-weight: 700;
            line-height: 1.2;
        }}

        .significance-badge-yes {{
            background: var(--good-soft);
            color: var(--good);
        }}

        .significance-badge-no {{
            background: rgba(52, 68, 84, 0.08);
            color: #344454;
        }}

        .pvalue-badge {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 4.4rem;
            border-radius: 999px;
            padding: 3px 9px;
            font-weight: 700;
            line-height: 1.2;
        }}

        .pvalue-badge-baseline {{
            background: var(--baseline-soft);
            color: var(--baseline);
        }}

        .pvalue-badge-better {{
            background: var(--good-soft);
            color: var(--good);
        }}

        .pvalue-badge-worse {{
            background: var(--bad-soft);
            color: var(--bad);
        }}

        .pvalue-badge-neutral {{
            background: rgba(52, 68, 84, 0.08);
            color: #344454;
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
            .collapsible-panel {{ padding: 0; }}
            .collapsible-summary {{ padding: 18px; }}
            .collapsible-body {{ padding: 0 18px 18px; }}
            .panel-header {{ align-items: flex-start; flex-direction: column; }}
            .glance-grid, .head-to-head-models {{ grid-template-columns: 1fr; }}
            .head-to-head-topline {{ align-items: flex-start; flex-direction: column; }}
            .pvalue-summary {{ text-align: left; }}
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

        {at_a_glance_section}

        {summary_section}

        {dataset_metadata_section}

        <section class=\"panel\">
            <div class=\"panel-header\">
                <h2>{ranking_heading}</h2>
            </div>
            <table{ranking_table_attributes}>
                <thead>
                    {ranking_headers}
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
    <script type=\"application/json\" id=\"standings-comparison-data\">{standings_comparison_json}</script>
    <script>
        (() => {{
            const comparisonDataElement = document.getElementById("standings-comparison-data");
            const standingsComparisons = comparisonDataElement
                ? JSON.parse(comparisonDataElement.textContent || "{{}}")
                : {{}};

            const readNumber = (cell) => {{
                const raw = cell?.dataset.sortValue ?? cell?.textContent?.trim() ?? "";
                const value = Number(raw);
                return {{ missing: raw === "" || Number.isNaN(value), value }};
            }};

            const compareNumbers = (leftCell, rightCell, direction) => {{
                const left = readNumber(leftCell);
                const right = readNumber(rightCell);
                if (left.missing && right.missing) {{
                    return 0;
                }}
                if (left.missing) {{
                    return 1;
                }}
                if (right.missing) {{
                    return -1;
                }}
                return direction === "asc" ? left.value - right.value : right.value - left.value;
            }};

            const compareText = (leftCell, rightCell, direction) => {{
                const left = (leftCell?.dataset.sortValue ?? leftCell?.textContent ?? "").trim().toLocaleLowerCase();
                const right = (rightCell?.dataset.sortValue ?? rightCell?.textContent ?? "").trim().toLocaleLowerCase();
                const result = left.localeCompare(right, undefined, {{ numeric: true, sensitivity: "base" }});
                return direction === "asc" ? result : -result;
            }};

            const readOutcome = (cell) => ({{
                wins: Number(cell?.dataset.sortWins ?? 0),
                ties: Number(cell?.dataset.sortTies ?? 0),
                losses: Number(cell?.dataset.sortLosses ?? 0),
            }});

            const compareOutcome = (leftCell, rightCell, direction) => {{
                const left = readOutcome(leftCell);
                const right = readOutcome(rightCell);
                const result =
                    left.wins - right.wins ||
                    left.ties - right.ties ||
                    left.losses - right.losses;
                return direction === "asc" ? result : -result;
            }};

            const compareCells = (leftCell, rightCell, sortType, direction) => {{
                if (sortType === "number" || sortType === "boolean") {{
                    return compareNumbers(leftCell, rightCell, direction);
                }}
                if (sortType === "outcome") {{
                    return compareOutcome(leftCell, rightCell, direction);
                }}
                return compareText(leftCell, rightCell, direction);
            }};

            const clearStandingsState = (row) => {{
                row.classList.remove("standings-row-baseline", "standings-row-better", "standings-row-worse");
            }};

            const setPValueBadge = (cell, text, state) => {{
                cell.textContent = "";
                const badge = document.createElement("span");
                badge.className = `pvalue-badge pvalue-badge-${{state}}`;
                badge.textContent = text;
                cell.appendChild(badge);
            }};

            const selectStandingsBaseline = (table, baselineRunName) => {{
                const rows = Array.from(table.querySelectorAll("[data-standings-row]"));
                const baselineComparisons = standingsComparisons[baselineRunName] || {{}};

                rows.forEach((row) => {{
                    const runName = row.dataset.runName;
                    const pvalueCell = row.querySelector("[data-standings-pvalue]");
                    const comparison = baselineComparisons[runName];
                    clearStandingsState(row);
                    row.setAttribute("aria-selected", runName === baselineRunName ? "true" : "false");

                    if (!pvalueCell) {{
                        return;
                    }}

                    if (runName === baselineRunName) {{
                        row.classList.add("standings-row-baseline");
                        setPValueBadge(pvalueCell, "baseline", "baseline");
                        return;
                    }}

                    if (!comparison) {{
                        setPValueBadge(pvalueCell, "n/a", "neutral");
                        return;
                    }}

                    const state = comparison.significance || "neutral";
                    if (state === "better" || state === "worse") {{
                        row.classList.add(`standings-row-${{state}}`);
                    }}
                    setPValueBadge(pvalueCell, comparison.displayPValue || "n/a", state);
                }});
            }};

            document.querySelectorAll("[data-sortable-table]").forEach((table) => {{
                const body = table.querySelector("tbody");
                const headers = Array.from(table.querySelectorAll("thead th"));
                if (!body) {{
                    return;
                }}

                headers.forEach((header, columnIndex) => {{
                    const button = header.querySelector(".sort-button");
                    if (!button) {{
                        return;
                    }}

                    button.addEventListener("click", () => {{
                        const direction = header.getAttribute("aria-sort") === "ascending" ? "desc" : "asc";
                        const sortType = button.dataset.sortType ?? "text";
                        const rows = Array.from(body.querySelectorAll("tr"))
                            .map((row, originalIndex) => ({{ row, originalIndex }}));

                        rows.sort((left, right) => {{
                            const result = compareCells(
                                left.row.cells[columnIndex],
                                right.row.cells[columnIndex],
                                sortType,
                                direction,
                            );
                            return result || left.originalIndex - right.originalIndex;
                        }});

                        headers.forEach((candidate) => candidate.setAttribute("aria-sort", "none"));
                        header.setAttribute("aria-sort", direction === "asc" ? "ascending" : "descending");
                        rows.forEach((entry) => body.appendChild(entry.row));
                    }});
                }});
            }});

            document.querySelectorAll("[data-standings-table]").forEach((table) => {{
                const rows = Array.from(table.querySelectorAll("[data-standings-row]"));
                const defaultBaseline = table.dataset.defaultBaseline || rows[0]?.dataset.runName;
                rows.forEach((row) => {{
                    row.addEventListener("click", () => selectStandingsBaseline(table, row.dataset.runName));
                    row.addEventListener("keydown", (event) => {{
                        if (event.key !== "Enter" && event.key !== " ") {{
                            return;
                        }}

                        event.preventDefault();
                        selectStandingsBaseline(table, row.dataset.runName);
                    }});
                }});

                if (defaultBaseline) {{
                    selectStandingsBaseline(table, defaultBaseline);
                }}
            }});
        }})();
    </script>
</body>
</html>
"""


def render_at_a_glance_section(report: dict[str, Any]) -> str:
        match_card = render_match_card(report.get("matchSummary"))
        head_to_head_card = render_compact_head_to_head_card(report)
        prediction_distribution_section = render_prediction_distribution_section(
                report.get("predictionDistributions", [])
        )
        if not match_card and not head_to_head_card and not prediction_distribution_section:
                return ""

        header_badge = ""
        if report.get("runCount") == 2:
                wilcoxon = report["comparison"]["wilcoxon"]
                is_significant = bool(wilcoxon.get("significant"))
                header_badge = (
                        f'<span class="pill {"pill-good" if is_significant else "pill-neutral"}">'
                        f'{"significant" if is_significant else "not significant"}'
                        f" · p-value {format_number(wilcoxon.get('pValue'))}</span>"
                )

        glance_cards = "\n".join(card for card in [match_card, head_to_head_card] if card)
        glance_grid = f'<div class="glance-grid">{glance_cards}</div>' if glance_cards else ""

        return f"""
        <section class=\"panel at-a-glance-panel\">
            <div class=\"panel-header\">
                <h2>At a glance</h2>
                {header_badge}
            </div>
            {glance_grid}
            {prediction_distribution_section}
        </section>
        """


def render_match_card(match_summary: Any) -> str:
        if not isinstance(match_summary, dict):
                return ""

        fixture = normalize_optional_string(match_summary.get("fixture"))
        actual_result = normalize_optional_string(match_summary.get("actualResultDisplay"))
        if actual_result is None:
                actual_result = normalize_optional_string(match_summary.get("actualResult"))
        matchday = normalize_optional_string(match_summary.get("matchday"))
        starts_at = normalize_optional_string(match_summary.get("startsAt"))
        if fixture is None and actual_result is None and matchday is None and starts_at is None:
                return ""

        meta_chips = []
        if matchday is not None:
                meta_chips.append(f'<span class="meta-chip">Matchday {escape_html(matchday)}</span>')
        if starts_at is not None:
                meta_chips.append(f'<span class="meta-chip">{escape_html(starts_at)}</span>')
        meta_html = f'<div class="match-meta">{"".join(meta_chips)}</div>' if meta_chips else ""

        return f"""
                <article class=\"glance-card match-card\">
                    <span class=\"glance-label\">Match to predict</span>
                    <h3 class=\"match-fixture\">{escape_html(fixture or "n/a")}</h3>
                    {meta_html}
                    <div class=\"actual-result\">
                        <span class=\"glance-label\">Actual outcome</span>
                        <span class=\"actual-result-value\">{escape_html(actual_result or "n/a")}</span>
                    </div>
                </article>
        """


def render_compact_head_to_head_card(report: dict[str, Any]) -> str:
        if report.get("runCount") != 2:
                return ""

        comparison = report["comparison"]
        wilcoxon = comparison["wilcoxon"]
        is_significant = bool(wilcoxon.get("significant"))
        runs_by_name = {
                str(run["runName"]): run
                for run in report["runs"]
        }
        better_run = runs_by_name.get(str(comparison["betterRunName"]))
        other_run = runs_by_name.get(str(comparison["otherRunName"]))
        if better_run is None or other_run is None:
                return ""

        state_class = "head-to-head-significant" if is_significant else "head-to-head-neutral"
        status_text = "Significant" if is_significant else "Not significant"
        model_cards = "\n".join(
                [
                        render_head_to_head_model_card(
                                better_run,
                                report,
                                "model-winner" if is_significant else "model-neutral",
                        ),
                        render_head_to_head_model_card(
                                other_run,
                                report,
                                "model-loser" if is_significant else "model-neutral",
                        ),
                ]
        )

        return f"""
                <article class=\"glance-card head-to-head-card {state_class}\">
                    <div class=\"head-to-head-topline\">
                        <div>
                            <span class=\"glance-label\">Compact head to head</span>
                            <h3>{escape_html(status_text)}</h3>
                        </div>
                        <div class=\"pvalue-summary\">
                            <span class=\"glance-label\">p-value</span>
                            <strong>{escape_html(format_number(wilcoxon.get("pValue")))}</strong>
                        </div>
                    </div>
                    <div class=\"head-to-head-models\">
                        {model_cards}
                    </div>
                </article>
        """


def render_head_to_head_model_card(run: dict[str, Any], report: dict[str, Any], state_class: str) -> str:
        average_points = resolve_average_points(run, report)
        return f"""
                        <div class=\"model-result {state_class}\">
                            <div class=\"model-name\">{escape_html(str(run["runDisplayName"]))}</div>
                            <span class=\"model-points\">{escape_html(format_number(average_points))}</span>
                            <span class=\"model-subtext\">avg points</span>
                        </div>
        """


def resolve_average_points(run: dict[str, Any], report: dict[str, Any]) -> float | None:
        aggregate_scores = run.get("aggregateScores")
        if isinstance(aggregate_scores, dict):
                average_points = normalize_optional_float(aggregate_scores.get("avg_kicktipp_points"))
                if average_points is not None:
                        return average_points

                total_points = normalize_optional_float(aggregate_scores.get("total_kicktipp_points"))
                pairing_count = normalize_optional_float(report.get("pairingCount"))
                if total_points is not None and pairing_count is not None and pairing_count > 0:
                        return total_points / pairing_count

        if report.get("primaryMetricName") == "avg_kicktipp_points":
                return normalize_optional_float(run.get("primaryMetricValue"))
        return None


def normalize_optional_float(value: Any) -> float | None:
        if value is None:
                return None
        try:
                number = float(value)
        except (TypeError, ValueError):
                return None
        if math.isnan(number):
                return None
        return number


def render_prediction_distribution_section(distributions: Any) -> str:
        if not isinstance(distributions, list) or not distributions:
                return ""

        max_count = max(
                int(score_count["count"])
                for distribution in distributions
                for score_count in distribution.get("scoreCounts", [])
        )
        histogram_cards = "\n".join(
                render_prediction_histogram_card(distribution, max_count)
                for distribution in distributions
        )
        return f"""
            <div class=\"prediction-section\">
                <h3>Prediction distribution</h3>
                <div class=\"histogram-grid\">
                    {histogram_cards}
                </div>
            </div>
        """


def render_prediction_histogram_card(distribution: dict[str, Any], max_count: int) -> str:
        rows = "\n".join(
                render_prediction_histogram_row(score_count, max_count)
                for score_count in distribution.get("scoreCounts", [])
        )
        return f"""
                    <article class=\"histogram-card\">
                        <div class=\"histogram-title\">
                            <span>{escape_html(str(distribution["runDisplayName"]))}</span>
                            <span class=\"histogram-total\">n={escape_html(str(distribution["totalCount"]))}</span>
                        </div>
                        {rows}
                    </article>
        """


def render_prediction_histogram_row(score_count: dict[str, Any], max_count: int) -> str:
        count = int(score_count["count"])
        width = 100 if max_count <= 0 else max(6, round(count / max_count * 100))
        return f"""
                        <div class=\"histogram-row\">
                            <span class=\"histogram-score\">{escape_html(str(score_count["score"]))}</span>
                            <span class=\"histogram-track\" aria-hidden=\"true\">
                                <span class=\"histogram-bar\" style=\"width: {width}%\"></span>
                            </span>
                            <span class=\"histogram-count\">{count}</span>
                        </div>
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


def render_standings_html_table_row(run: dict[str, Any]) -> str:
        cells = [
                f"<td>{escape_html(str(run['rank']))}</td>",
                f"<td>{escape_html(str(run['runDisplayName']))}</td>",
                f"<td>{escape_html(format_kicktipp_points(run['primaryMetricValue']))}</td>",
                '<td data-standings-pvalue data-label="p-value vs baseline"></td>',
        ]
        return (
                '<tr class="standings-row" data-standings-row '
                f'data-run-name="{escape_html(str(run["runName"]))}" '
                f'data-run-display-name="{escape_html(str(run["runDisplayName"]))}" '
                'tabindex="0" aria-selected="false">'
                f'{"".join(cells)}</tr>'
        )


def render_sortable_header(label: str, sort_type: str) -> str:
        return (
                '<th aria-sort="none">'
                f'<button class="sort-button" type="button" data-sort-type="{escape_html(sort_type)}">'
                f"<span>{escape_html(label)}</span>"
                '<span class="sort-indicator" aria-hidden="true"></span>'
                "</button>"
                "</th>"
        )


def render_pairwise_html_table_row(comparison: dict[str, Any]) -> str:
        significant = bool(comparison["wilcoxon"].get("significantAfterCorrection"))
        significant_text = "yes" if significant else "no"
        row_class = "pairwise-row pairwise-row-significant" if significant else "pairwise-row"
        counts = comparison["perItemOutcomeCounts"]
        significant_badge = (
                f'<span class="significance-badge significance-badge-{significant_text}">'
                f"{significant_text}</span>"
        )

        cells = [
                render_html_table_cell(
                        comparison["runADisplayName"],
                        label="Run A",
                        sort_value=comparison["runADisplayName"],
                ),
                render_html_table_cell(
                        comparison["runBDisplayName"],
                        label="Run B",
                        sort_value=comparison["runBDisplayName"],
                ),
                render_html_table_cell(
                        format_number(comparison["primaryMetricDelta"]),
                        label="Primary delta",
                        sort_value=comparison["primaryMetricDelta"],
                ),
                render_html_table_cell(
                        format_number(comparison["wilcoxon"]["pValue"]),
                        label="Raw p-value",
                        sort_value=comparison["wilcoxon"]["pValue"],
                ),
                render_html_table_cell(
                        format_number(comparison["wilcoxon"]["adjustedPValue"]),
                        label="Adjusted p-value",
                        sort_value=comparison["wilcoxon"]["adjustedPValue"],
                ),
                render_html_table_cell(
                        significant_text,
                        label="Significant",
                        sort_value=1 if significant else 0,
                        html_value=significant_badge,
                ),
                render_html_table_cell(
                        format_outcome_counts(counts),
                        label="Per-item W/T/L",
                        sort_value=format_outcome_counts(counts),
                        extra_attributes={
                                "data-sort-wins": counts["wins"],
                                "data-sort-ties": counts["ties"],
                                "data-sort-losses": counts["losses"],
                        },
                ),
        ]
        return f'<tr class="{row_class}">{"".join(cells)}</tr>'


def render_html_table_cell(
        value: Any,
        *,
        label: str,
        sort_value: Any,
        html_value: str | None = None,
        extra_attributes: dict[str, Any] | None = None,
) -> str:
        attributes: dict[str, Any] = {
                "data-label": label,
                "data-sort-value": sort_value,
        }
        if extra_attributes is not None:
                attributes.update(extra_attributes)

        rendered_attributes = " ".join(
                f'{name}="{escape_html(str(attribute_value))}"'
                for name, attribute_value in attributes.items()
                if attribute_value is not None
        )
        content = html_value if html_value is not None else escape_html(str(value))
        return f"<td {rendered_attributes}>{content}</td>"


def format_outcome_counts(counts: dict[str, int]) -> str:
        return f"{counts['wins']}/{counts['ties']}/{counts['losses']}"


def escape_html(value: str) -> str:
        return html.escape(value, quote=True)


def render_standings_comparison_json(report: dict[str, Any]) -> str:
        matrix = build_standings_comparison_matrix(report)
        return (
                json.dumps(matrix, separators=(",", ":"), sort_keys=True)
                .replace("&", "\\u0026")
                .replace("<", "\\u003c")
                .replace(">", "\\u003e")
        )


def build_standings_comparison_matrix(report: dict[str, Any]) -> dict[str, dict[str, dict[str, Any]]]:
        runs = [str(run["runName"]) for run in report["runs"]]
        matrix: dict[str, dict[str, dict[str, Any]]] = {
                run_name: {} for run_name in runs
        }

        for run_name in runs:
                matrix[run_name][run_name] = {
                        "displayPValue": "baseline",
                        "pValue": None,
                        "significance": "baseline",
                }

        if report["runCount"] == 2:
                comparison = report["comparison"]
                add_standings_comparison(
                        matrix,
                        str(comparison["betterRunName"]),
                        str(comparison["otherRunName"]),
                        comparison["wilcoxon"].get("pValue"),
                        bool(comparison["wilcoxon"].get("significant")),
                        float(comparison["primaryMetricDelta"]),
                )
                return matrix

        for comparison in report.get("pairwiseComparisons", []):
                add_standings_comparison(
                        matrix,
                        str(comparison["runAName"]),
                        str(comparison["runBName"]),
                        comparison["wilcoxon"].get("adjustedPValue"),
                        bool(comparison["wilcoxon"].get("significantAfterCorrection")),
                        float(comparison["primaryMetricDelta"]),
                )

        return matrix


def add_standings_comparison(
        matrix: dict[str, dict[str, dict[str, Any]]],
        better_run_name: str,
        other_run_name: str,
        pvalue: float | None,
        significant: bool,
        better_minus_other_delta: float,
) -> None:
        add_directed_standings_comparison(
                matrix,
                baseline_run_name=better_run_name,
                compared_run_name=other_run_name,
                pvalue=pvalue,
                significant=significant,
                compared_minus_baseline_delta=-better_minus_other_delta,
        )
        add_directed_standings_comparison(
                matrix,
                baseline_run_name=other_run_name,
                compared_run_name=better_run_name,
                pvalue=pvalue,
                significant=significant,
                compared_minus_baseline_delta=better_minus_other_delta,
        )


def add_directed_standings_comparison(
        matrix: dict[str, dict[str, dict[str, Any]]],
        *,
        baseline_run_name: str,
        compared_run_name: str,
        pvalue: float | None,
        significant: bool,
        compared_minus_baseline_delta: float,
) -> None:
        if baseline_run_name not in matrix or compared_run_name not in matrix:
                return

        significance = "neutral"
        if significant and compared_minus_baseline_delta > 0:
                significance = "better"
        elif significant and compared_minus_baseline_delta < 0:
                significance = "worse"

        matrix[baseline_run_name][compared_run_name] = {
                "displayPValue": format_number(pvalue),
                "pValue": pvalue,
                "significance": significance,
        }


def is_community_standings_report(report: dict[str, Any]) -> bool:
        return str(report.get("taskType", "")).lower() == "community-to-date"


def render_table(headers: list[str], rows: list[list[str]]) -> list[str]:
    header_line = "| " + " | ".join(headers) + " |"
    separator_line = "| " + " | ".join(["---"] * len(headers)) + " |"
    row_lines = ["| " + " | ".join(row) + " |" for row in rows]
    return [header_line, separator_line, *row_lines]


def format_number(value: float | None) -> str:
    if value is None or math.isnan(value):
        return "n/a"
    return f"{value:.4f}"


def format_kicktipp_points(value: float | None) -> str:
    if value is None or math.isnan(value):
        return "n/a"

    return str(int(value)) if float(value).is_integer() else format_number(value)


def write_outputs(report: dict[str, Any], output_paths: OutputPaths) -> str:
    markdown = strip_trailing_whitespace(render_markdown(report))
    output_paths.json_path.parent.mkdir(parents=True, exist_ok=True)
    output_paths.markdown_path.parent.mkdir(parents=True, exist_ok=True)
    output_paths.json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    output_paths.markdown_path.write_text(markdown, encoding="utf-8")
    if output_paths.html_path is not None:
        output_paths.html_path.parent.mkdir(parents=True, exist_ok=True)
        output_paths.html_path.write_text(strip_trailing_whitespace(render_html(report)), encoding="utf-8")
    return markdown


def strip_trailing_whitespace(value: str) -> str:
    suffix = "\n" if value.endswith("\n") else ""
    return "\n".join(line.rstrip() for line in value.splitlines()) + suffix


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
