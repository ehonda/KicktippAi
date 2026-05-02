"""Collect and analyze Langfuse token usage for cost-estimator experiments.

The script intentionally writes a compact usage file that excludes prompt bodies
and model outputs. It keeps only the run/item identity plus token and cost
fields needed for reproducible comparisons.
"""

from __future__ import annotations

import argparse
import base64
import email.utils
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

import numpy as np


DEFAULT_METRICS = ("inputTokens", "outputTokens")


def main() -> int:
    args = parse_args()

    if args.input:
        records = load_json(Path(args.input))
    else:
        records = collect_records(args)
        if args.output:
            write_json(Path(args.output), records)

    left = filter_group(records, args.left_group)
    right = filter_group(records, args.right_group)

    if not left:
        raise SystemExit(f"No records matched left group '{args.left_group}'.")
    if not right:
        raise SystemExit(f"No records matched right group '{args.right_group}'.")

    report = analyze_groups(
        left,
        right,
        metrics=args.metrics,
        bootstrap_resamples=args.bootstrap_resamples,
        seed=args.seed,
    )

    if args.report_json:
        write_json(Path(args.report_json), report)

    print_markdown_report(report)
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Collect compact Langfuse usage records and compare two groups."
    )
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument(
        "--input",
        help="Existing compact usage JSON file to analyze.",
    )
    source.add_argument(
        "--env",
        help="Langfuse env file used by the installed langfuse CLI.",
    )
    parser.add_argument(
        "--group",
        action="append",
        default=[],
        metavar="GROUP=RUN_NAME",
        help=(
            "Group/run pair to collect. Repeat this option to combine several "
            "runs into one group."
        ),
    )
    parser.add_argument(
        "--output",
        help="Optional compact usage JSON output path when collecting from Langfuse.",
    )
    parser.add_argument(
        "--left-group",
        default="slice-measured",
        help="Measured group name for the left side of the comparison.",
    )
    parser.add_argument(
        "--right-group",
        default="repeated-measured",
        help="Measured group name for the right side of the comparison.",
    )
    parser.add_argument(
        "--metrics",
        nargs="+",
        default=list(DEFAULT_METRICS),
        help="Usage metrics to compare.",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=20260501,
        help="Seed for bootstrap confidence intervals.",
    )
    parser.add_argument(
        "--bootstrap-resamples",
        type=int,
        default=30_000,
        help="Number of seeded bootstrap resamples for confidence intervals.",
    )
    parser.add_argument(
        "--report-json",
        help="Optional path for a machine-readable analysis report.",
    )
    parser.add_argument(
        "--expect",
        action="append",
        default=[],
        metavar="GROUP=N",
        help="Expected record count for a group. Repeat for multiple groups.",
    )
    parser.add_argument(
        "--langfuse-sleep-seconds",
        type=float,
        default=0.0,
        help="Delay after each Langfuse detail request or batched run collection.",
    )
    parser.add_argument(
        "--collection-mode",
        choices=("observations", "trace-details"),
        default="observations",
        help=(
            "Use the fast v2 observations list endpoint, or the older "
            "per-trace detail fallback."
        ),
    )
    parser.add_argument(
        "--langfuse-command",
        default="langfuse",
        help="Langfuse CLI command name or path.",
    )
    parser.add_argument(
        "--langfuse-max-retries",
        type=int,
        default=8,
        help="Maximum retries for transient Langfuse CLI/API failures.",
    )
    return parser.parse_args()


def collect_records(args: argparse.Namespace) -> list[dict[str, Any]]:
    if not args.group:
        raise SystemExit("--group is required when collecting from Langfuse.")

    if args.collection_mode == "trace-details":
        return collect_records_from_trace_details(args)

    return collect_records_from_observations(args)


def collect_records_from_observations(args: argparse.Namespace) -> list[dict[str, Any]]:
    output_path = Path(args.output) if args.output else None
    records: list[dict[str, Any]] = []
    if output_path and output_path.exists():
        records = load_json(output_path)

    seen_trace_ids = {record.get("traceId") for record in records}
    env_values = load_env_file(Path(args.env))

    for group_arg in args.group:
        group, run_name = parse_group(group_arg)
        observations = list_run_observations(args, env_values, run_name)
        for observation in observations:
            trace_id = observation.get("traceId")
            if trace_id in seen_trace_ids:
                continue
            record = extract_usage_record_from_observation(group, run_name, observation)
            records.append(record)
            seen_trace_ids.add(record.get("traceId"))
            print(
                f"Collected {group} trace {record.get('traceId')}",
                file=sys.stderr,
                flush=True,
            )
            sort_records(records)
            if output_path:
                write_json(output_path, records)
        if args.langfuse_sleep_seconds > 0:
            time.sleep(args.langfuse_sleep_seconds)

    sort_records(records)
    validate_expectations(records, args.expect)
    return records


def collect_records_from_trace_details(args: argparse.Namespace) -> list[dict[str, Any]]:
    output_path = Path(args.output) if args.output else None
    records: list[dict[str, Any]] = []
    if output_path and output_path.exists():
        records = load_json(output_path)

    seen_trace_ids = {record.get("traceId") for record in records}
    for group_arg in args.group:
        group, run_name = parse_group(group_arg)
        traces = list_traces(args, run_name)
        for trace in traces:
            if trace.get("id") in seen_trace_ids:
                continue
            detail = get_trace(args, trace["id"])
            record = extract_usage_record(group, run_name, detail)
            records.append(record)
            seen_trace_ids.add(record.get("traceId"))
            print(
                f"Collected {group} trace {record.get('traceId')}",
                file=sys.stderr,
                flush=True,
            )
            sort_records(records)
            if output_path:
                write_json(output_path, records)
            if args.langfuse_sleep_seconds > 0:
                time.sleep(args.langfuse_sleep_seconds)

    sort_records(records)
    validate_expectations(records, args.expect)
    return records


def load_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    with path.open("r", encoding="utf-8") as stream:
        for line in stream:
            stripped = line.strip()
            if not stripped or stripped.startswith("#") or "=" not in stripped:
                continue
            key, value = stripped.split("=", 1)
            values[key.strip()] = value.strip().strip("'\"")
    return values


def list_run_observations(
    args: argparse.Namespace, env_values: dict[str, str], run_name: str
) -> list[dict[str, Any]]:
    observations: list[dict[str, Any]] = []
    cursor = None
    while True:
        filters = [
            {
                "type": "string",
                "column": "sessionId",
                "operator": "=",
                "value": run_name,
            },
            {
                "type": "string",
                "column": "name",
                "operator": "=",
                "value": "predict-match",
            },
            {
                "type": "string",
                "column": "type",
                "operator": "=",
                "value": "GENERATION",
            },
        ]
        query = {
            "limit": "1000",
            "fields": "basic,metadata,model,usage",
            "filter": json.dumps(filters, separators=(",", ":")),
        }
        if cursor:
            query["cursor"] = cursor

        body = langfuse_get_json(args, env_values, "v2/observations", query)
        observations.extend(body.get("data", []))
        cursor = (body.get("meta") or {}).get("cursor")
        if not cursor:
            break

    return observations


def langfuse_get_json(
    args: argparse.Namespace,
    env_values: dict[str, str],
    relative_path: str,
    query: dict[str, str],
) -> dict[str, Any]:
    base_url = env_values.get("LANGFUSE_BASE_URL") or "https://cloud.langfuse.com"
    base_url = base_url.rstrip("/")
    public_key = env_values.get("LANGFUSE_PUBLIC_KEY")
    secret_key = env_values.get("LANGFUSE_SECRET_KEY")
    if not public_key or not secret_key:
        raise RuntimeError("LANGFUSE_PUBLIC_KEY and LANGFUSE_SECRET_KEY are required.")

    auth = base64.b64encode(f"{public_key}:{secret_key}".encode("utf-8")).decode(
        "ascii"
    )
    url = f"{base_url}/api/public/{relative_path}?{urlencode(query)}"

    for attempt in range(args.langfuse_max_retries + 1):
        request = Request(
            url,
            headers={
                "Accept": "application/json",
                "Authorization": f"Basic {auth}",
            },
        )
        try:
            with urlopen(request, timeout=120) as response:
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as ex:
            if ex.code == 429 and attempt < args.langfuse_max_retries:
                time.sleep(resolve_retry_after(ex, args.langfuse_sleep_seconds))
                continue
            body = ex.read().decode("utf-8", errors="replace")
            raise RuntimeError(
                f"Langfuse API request failed with HTTP {ex.code}: {relative_path}\n{body}"
            ) from ex

    raise RuntimeError(f"Langfuse rate limit persisted: {relative_path}")


def resolve_retry_after(error: HTTPError, fallback_seconds: float) -> float:
    retry_after = error.headers.get("Retry-After")
    if retry_after:
        try:
            return max(float(retry_after), 0.0)
        except ValueError:
            retry_at = email.utils.parsedate_to_datetime(retry_after)
            if retry_at.tzinfo is None:
                retry_at = retry_at.replace(tzinfo=timezone.utc)
            return max((retry_at - datetime.now(timezone.utc)).total_seconds(), 0.0)
    return max(fallback_seconds, 1.0)


def sort_records(records: list[dict[str, Any]]) -> None:
    records.sort(
        key=lambda record: (
            record.get("measuredGroup", ""),
            record.get("runName", ""),
            record.get("datasetItemId", ""),
        )
    )


def validate_expectations(records: list[dict[str, Any]], expectations: list[str]) -> None:
    for expectation in expectations:
        group, count_text = parse_group(expectation)
        expected = int(count_text)
        actual = len(filter_group(records, group))
        if actual != expected:
            raise SystemExit(
                f"Expected {expected} records for group '{group}', found {actual}."
            )


def parse_group(value: str) -> tuple[str, str]:
    if "=" not in value:
        raise SystemExit(f"Expected GROUP=RUN_NAME for --group, got '{value}'.")
    group, run_name = value.split("=", 1)
    group = group.strip()
    run_name = run_name.strip()
    if not group or not run_name:
        raise SystemExit(f"Expected GROUP=RUN_NAME for --group, got '{value}'.")
    return group, run_name


def list_traces(args: argparse.Namespace, run_name: str) -> list[dict[str, Any]]:
    traces: list[dict[str, Any]] = []
    page = 1
    total_pages = 1
    while page <= total_pages:
        response = run_langfuse(
            args,
            "api",
            "traces",
            "list",
            "--session-id",
            run_name,
            "--limit",
            "100",
            "--page",
            str(page),
            "--fields",
            "core,io,metrics",
            "--json",
        )
        body = response["body"]
        traces.extend(body.get("data", []))
        meta = body.get("meta", {})
        total_pages = int(meta.get("totalPages", 1))
        page += 1
    return traces


def get_trace(args: argparse.Namespace, trace_id: str) -> dict[str, Any]:
    response = run_langfuse(
        args,
        "api",
        "traces",
        "get",
        trace_id,
        "--fields",
        "core,io,observations,metrics",
        "--json",
    )
    return response["body"]


def run_langfuse(args: argparse.Namespace, *cli_args: str) -> dict[str, Any]:
    env = os.environ.copy()
    env.setdefault("HOME", env.get("USERPROFILE", ""))

    langfuse_command = resolve_command(args.langfuse_command)
    command = [langfuse_command, "--env", args.env, *cli_args]

    for attempt in range(args.langfuse_max_retries + 1):
        completed = subprocess.run(
            command,
            env=env,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        payload = parse_langfuse_stdout(completed.stdout)
        combined_output = f"{completed.stdout}\n{completed.stderr}"

        if is_rate_limit(payload, combined_output):
            if attempt >= args.langfuse_max_retries:
                break
            retry_sleep = max(args.langfuse_sleep_seconds, min(60, 2**attempt))
            time.sleep(retry_sleep)
            continue

        if completed.returncode != 0:
            raise RuntimeError(
                "Langfuse command failed with exit code "
                f"{completed.returncode}: {' '.join(command)}\n{combined_output}"
            )

        if payload is None:
            raise RuntimeError(
                f"Langfuse command did not emit JSON: {' '.join(command)}\n"
                f"stdout:\n{completed.stdout}\nstderr:\n{completed.stderr}"
            )

        if not payload.get("ok", False):
            raise RuntimeError(f"Langfuse API error: {payload}")

        return payload

    raise RuntimeError(f"Langfuse rate limit persisted: {' '.join(command)}")


def parse_langfuse_stdout(stdout: str) -> dict[str, Any] | None:
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        return None


def is_rate_limit(payload: dict[str, Any] | None, output: str) -> bool:
    if payload and payload.get("status") == 429:
        return True
    return "rate limit" in output.lower() or "429" in output


def resolve_command(command: str) -> str:
    resolved = shutil.which(command)
    if resolved:
        return resolved

    if os.name == "nt" and not command.lower().endswith(".cmd"):
        resolved = shutil.which(f"{command}.cmd")
        if resolved:
            return resolved

    return command


def extract_usage_record(
    measured_group: str, run_name: str, trace: dict[str, Any]
) -> dict[str, Any]:
    prediction = None
    for observation in trace.get("observations", []):
        if observation.get("name") == "predict-match" and observation.get(
            "usageDetails"
        ):
            prediction = observation
            break

    if prediction is None:
        raise RuntimeError(f"Trace {trace.get('id')} has no predict-match usage.")

    usage = prediction.get("usageDetails", {})
    cost = prediction.get("costDetails", {})
    trace_metadata = trace.get("metadata") or {}
    observation_metadata = prediction.get("metadata") or {}
    trace_input = trace.get("input") or {}

    input_tokens = int(usage.get("input", 0))
    cached_input_tokens = int(usage.get("cache_read_input_tokens", 0))
    output_tokens = int(usage.get("output", 0))
    reasoning_tokens = int(usage.get("reasoning_tokens", 0))
    total_tokens = int(usage.get("total", input_tokens + output_tokens))
    dataset_item_id = (
        trace_metadata.get("datasetItemId")
        or trace_metadata.get("dataset_item_id")
        or observation_metadata.get("attributes", {}).get("langfuse.experiment.item.id")
    )

    return {
        "datasetType": dataset_type_for_group(measured_group),
        "measuredGroup": measured_group,
        "runName": run_name,
        "traceId": trace.get("id"),
        "observationId": prediction.get("id"),
        "datasetItemId": dataset_item_id,
        "sourceDatasetItemId": trace_metadata.get("sourceDatasetItemId"),
        "fixture": trace_input.get("fixture")
        or observation_metadata.get("match")
        or trace_metadata.get("selectedMatch"),
        "startsAt": trace_input.get("startsAt"),
        "repetition": parse_repetition(dataset_item_id),
        "inputTokens": input_tokens,
        "cachedInputTokens": cached_input_tokens,
        "uncachedInputTokens": input_tokens - cached_input_tokens,
        "outputTokens": output_tokens,
        "reasoningTokens": reasoning_tokens,
        "totalTokens": total_tokens,
        "totalCost": float(cost.get("total", 0.0) or 0.0),
        "model": prediction.get("model") or trace_metadata.get("model"),
        "reasoningEffort": observation_metadata.get("openaiReasoningEffort")
        or trace_metadata.get("reasoningEffort"),
        "serviceTier": (prediction.get("modelParameters") or {}).get("service_tier")
        or observation_metadata.get("openaiFinalServiceTier"),
    }


def extract_usage_record_from_observation(
    measured_group: str, run_name: str, observation: dict[str, Any]
) -> dict[str, Any]:
    usage = observation.get("usageDetails") or nested_metadata(
        observation, "attributes.langfuse.observation.usage_details"
    )
    cost = observation.get("costDetails") or nested_metadata(
        observation, "attributes.langfuse.observation.cost_details"
    )
    if not usage:
        raise RuntimeError(
            f"Observation {observation.get('id')} has no usageDetails payload."
        )

    metadata = observation.get("metadata") or {}
    model_parameters = observation.get("modelParameters") or {}
    dataset_item_id = metadata.get(
        "attributes.langfuse.experiment.item.id"
    ) or metadata.get("langfuse.experiment.item.id")

    input_tokens = int(usage.get("input", 0))
    cached_input_tokens = int(usage.get("cache_read_input_tokens", 0))
    output_tokens = int(usage.get("output", 0))
    reasoning_tokens = int(usage.get("reasoning_tokens", 0))
    total_tokens = int(usage.get("total", input_tokens + output_tokens))

    return {
        "datasetType": dataset_type_for_group(measured_group),
        "measuredGroup": measured_group,
        "runName": run_name,
        "traceId": observation.get("traceId"),
        "observationId": observation.get("id"),
        "datasetItemId": dataset_item_id,
        "sourceDatasetItemId": None,
        "fixture": metadata.get("match"),
        "startsAt": None,
        "repetition": parse_repetition(dataset_item_id),
        "inputTokens": input_tokens,
        "cachedInputTokens": cached_input_tokens,
        "uncachedInputTokens": input_tokens - cached_input_tokens,
        "outputTokens": output_tokens,
        "reasoningTokens": reasoning_tokens,
        "totalTokens": total_tokens,
        "totalCost": float((cost or {}).get("total", 0.0) or 0.0),
        "model": observation.get("model")
        or observation.get("providedModelName")
        or metadata.get("model"),
        "reasoningEffort": metadata.get("openaiReasoningEffort")
        or metadata.get("reasoningEffort")
        or model_parameters.get("reasoning_effort"),
        "serviceTier": model_parameters.get("service_tier")
        or metadata.get("openaiFinalServiceTier"),
    }


def nested_metadata(observation: dict[str, Any], key: str) -> Any:
    metadata = observation.get("metadata") or {}
    return metadata.get(key)


def dataset_type_for_group(measured_group: str) -> str:
    if measured_group.startswith("slice"):
        return "slice"
    if measured_group.startswith("repeated"):
        return "repeated-match"
    return measured_group


def parse_repetition(dataset_item_id: str | None) -> int | None:
    if not dataset_item_id:
        return None
    match = re.search(r"__(\d+)$", dataset_item_id)
    if not match:
        return None
    return int(match.group(1))


def filter_group(records: list[dict[str, Any]], group: str) -> list[dict[str, Any]]:
    return [
        record
        for record in records
        if record.get("measuredGroup") == group or record.get("datasetType") == group
    ]


def analyze_groups(
    left: list[dict[str, Any]],
    right: list[dict[str, Any]],
    *,
    metrics: list[str],
    bootstrap_resamples: int,
    seed: int,
) -> dict[str, Any]:
    result = {
        "leftGroup": left[0].get("measuredGroup"),
        "rightGroup": right[0].get("measuredGroup"),
        "leftN": len(left),
        "rightN": len(right),
        "metrics": [],
    }

    for offset, metric in enumerate(metrics):
        left_values = [record[metric] for record in left]
        right_values = [record[metric] for record in right]
        test = exact_permutation_test(left_values, right_values)
        ci = bootstrap_ci(
            left_values,
            right_values,
            resamples=bootstrap_resamples,
            seed=seed + offset,
        )
        result["metrics"].append(
            {
                "metric": metric,
                "left": describe(left_values),
                "right": describe(right_values),
                "differenceMeanLeftMinusRight": test["observedDifference"],
                "permutation": test,
                "bootstrapMeanDifference95Ci": ci,
            }
        )

    return result


def describe(values: list[float]) -> dict[str, float]:
    array = np.asarray(values, dtype=float)
    return {
        "n": int(array.size),
        "mean": float(np.mean(array)),
        "sd": float(np.std(array, ddof=1)) if array.size > 1 else 0.0,
        "min": float(np.min(array)),
        "max": float(np.max(array)),
    }


def exact_permutation_test(
    left_values: list[float], right_values: list[float]
) -> dict[str, Any]:
    values = [*left_values, *right_values]
    if any(not is_integer_like(value) for value in values):
        raise ValueError("Exact dynamic-programming permutation test requires integers.")

    int_values = [int(value) for value in values]
    left_n = len(left_values)
    right_n = len(right_values)
    total_sum = sum(int_values)
    observed = float(np.mean(left_values) - np.mean(right_values))
    total_partitions = math.comb(left_n + right_n, left_n)

    subset_sum_counts = [Counter() for _ in range(left_n + 1)]
    subset_sum_counts[0][0] = 1

    seen = 0
    for value in int_values:
        seen += 1
        max_k = min(seen, left_n)
        for k in range(max_k, 0, -1):
            previous = subset_sum_counts[k - 1]
            current = subset_sum_counts[k]
            for subset_sum, count in previous.items():
                current[subset_sum + value] += count

    extreme_count = 0
    for left_sum, count in subset_sum_counts[left_n].items():
        right_sum = total_sum - left_sum
        difference = (left_sum / left_n) - (right_sum / right_n)
        if abs(difference) >= abs(observed) - 1e-12:
            extreme_count += count

    return {
        "method": "two-sided exact permutation test over difference in means",
        "observedDifference": observed,
        "totalPartitions": total_partitions,
        "extremePartitions": extreme_count,
        "pValue": extreme_count / total_partitions,
    }


def is_integer_like(value: float) -> bool:
    return float(value).is_integer()


def bootstrap_ci(
    left_values: list[float],
    right_values: list[float],
    *,
    resamples: int,
    seed: int,
) -> dict[str, Any]:
    rng = np.random.default_rng(seed)
    left = np.asarray(left_values, dtype=float)
    right = np.asarray(right_values, dtype=float)
    differences = np.empty(resamples, dtype=float)

    for index in range(resamples):
        left_sample = rng.choice(left, size=left.size, replace=True)
        right_sample = rng.choice(right, size=right.size, replace=True)
        differences[index] = np.mean(left_sample) - np.mean(right_sample)

    low, high = np.percentile(differences, [2.5, 97.5])
    return {
        "method": "seeded bootstrap percentile interval",
        "resamples": resamples,
        "low": float(low),
        "high": float(high),
    }


def print_markdown_report(report: dict[str, Any]) -> None:
    print(
        f"Groups: {report['leftGroup']} (n={report['leftN']}) vs "
        f"{report['rightGroup']} (n={report['rightN']})"
    )
    print()
    print(
        "| Metric | Left mean | Right mean | Difference | Permutation p | "
        "Bootstrap 95% CI |"
    )
    print("| --- | ---: | ---: | ---: | ---: | --- |")
    for metric in report["metrics"]:
        ci = metric["bootstrapMeanDifference95Ci"]
        print(
            f"| {metric['metric']} | "
            f"{metric['left']['mean']:.3f} | "
            f"{metric['right']['mean']:.3f} | "
            f"{metric['differenceMeanLeftMinusRight']:.3f} | "
            f"{metric['permutation']['pValue']:.6f} | "
            f"[{ci['low']:.3f}, {ci['high']:.3f}] |"
        )


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(payload, stream, indent=2, ensure_ascii=False)
        stream.write("\n")


if __name__ == "__main__":
    sys.exit(main())
