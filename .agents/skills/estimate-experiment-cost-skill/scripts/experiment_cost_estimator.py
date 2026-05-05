"""Collect usage records and calculate KicktippAi experiment cost estimates.

The estimate intentionally treats every input token as uncached. This makes the
base rows suitable for slice experiments and conservative for repeated-match
experiments where prompt caching may occur during data collection.
"""

from __future__ import annotations

import argparse
import base64
import email.utils
import json
import re
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


DEFAULT_PRICING_SOURCE = Path("src/OpenAiIntegration/CostCalculationService.cs")
DEFAULT_BASE_ESTIMATES_SOURCE = Path(
    ".agents/skills/estimate-experiment-cost-skill/references/base-estimates.json"
)
FLEX_PRICE_MULTIPLIER = Decimal("0.5")
DEFAULT_COLLECT_WAIT_TIMEOUT_SECONDS = 900.0
DEFAULT_COLLECT_WAIT_INTERVAL_SECONDS = 30.0


@dataclass(frozen=True)
class ModelPricing:
    input_price: Decimal
    output_price: Decimal


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Collect Langfuse usage and calculate uncached-input cost estimates."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    collect = subparsers.add_parser("collect", help="Collect compact usage JSON.")
    collect.add_argument("--env", required=True, help="Langfuse env file.")
    collect.add_argument(
        "--group",
        action="append",
        default=[],
        metavar="GROUP=RUN_NAME",
        help="Group/run pair to collect. Repeat to combine runs.",
    )
    collect.add_argument("--output", required=True, help="Compact usage JSON output.")
    collect.add_argument(
        "--expect",
        action="append",
        default=[],
        metavar="GROUP=N",
        help="Expected record count for a group.",
    )
    collect.add_argument(
        "--langfuse-sleep-seconds",
        type=float,
        default=0.0,
        help="Delay after each batched run collection.",
    )
    collect.add_argument(
        "--langfuse-max-retries",
        type=int,
        default=8,
        help="Maximum retries for transient Langfuse API failures.",
    )
    collect.add_argument(
        "--wait-timeout-seconds",
        type=float,
        default=DEFAULT_COLLECT_WAIT_TIMEOUT_SECONDS,
        help="Maximum time to wait for expected Langfuse observations to appear.",
    )
    collect.add_argument(
        "--wait-interval-seconds",
        type=float,
        default=DEFAULT_COLLECT_WAIT_INTERVAL_SECONDS,
        help="Polling interval while waiting for expected Langfuse observations.",
    )
    collect.add_argument(
        "--no-wait-for-expectations",
        action="store_true",
        help="Validate expectations once instead of polling for Langfuse ingestion.",
    )

    base_row = subparsers.add_parser(
        "base-row", help="Calculate a base estimate row from compact usage."
    )
    add_base_row_arguments(base_row)
    base_row.add_argument("--report-json", help="Optional machine-readable report.")

    upsert_row = subparsers.add_parser(
        "upsert-row",
        help="Calculate and upsert an authoritative JSON base estimate row.",
    )
    add_base_row_arguments(upsert_row)
    upsert_row.add_argument(
        "--store",
        default=str(DEFAULT_BASE_ESTIMATES_SOURCE),
        help="JSON base estimate store to update.",
    )
    upsert_row.add_argument(
        "--replace",
        action="store_true",
        help="Replace an existing row for the same model and reasoning effort.",
    )

    estimate = subparsers.add_parser(
        "estimate", help="Estimate experiment totals from the JSON base estimate store."
    )
    estimate.add_argument(
        "--counts",
        help="Comma-separated match prediction counts, for example 20,60,100.",
    )
    estimate.add_argument(
        "--count",
        action="append",
        type=int,
        default=[],
        help=argparse.SUPPRESS,
    )
    estimate.add_argument(
        "--store",
        default=str(DEFAULT_BASE_ESTIMATES_SOURCE),
        help="JSON base estimate store.",
    )
    estimate.add_argument("--model", required=True, help="Model name for JSON lookup.")
    estimate.add_argument(
        "--reasoning-effort",
        required=True,
        help="Reasoning effort for JSON lookup.",
    )
    estimate.add_argument("--report-json", help="Optional machine-readable report.")

    args = parser.parse_args()
    if args.command == "collect":
        records = collect_records(args)
        write_json(Path(args.output), records)
        print(f"Wrote {len(records)} compact usage records to {args.output}.")
        return 0
    if args.command == "base-row":
        report = calculate_base_row(args)
        emit_base_row(report)
        if args.report_json:
            try_write_optional_json(Path(args.report_json), report)
        return 0
    if args.command == "upsert-row":
        report = calculate_base_row(args)
        action = upsert_base_estimate(
            Path(args.store), report, replace_existing=args.replace
        )
        print(
            f"{action.capitalize()} base estimate row for "
            f"{report['model']} {report['reasoningEffort']} in {args.store}."
        )
        emit_base_estimate_summary(report)
        return 0
    if args.command == "estimate":
        report = calculate_estimate(args)
        emit_estimate(report)
        if args.report_json:
            try_write_optional_json(Path(args.report_json), report)
        return 0

    raise AssertionError(f"Unhandled command {args.command!r}.")


def add_base_row_arguments(command: argparse.ArgumentParser) -> None:
    command.add_argument("--input", required=True, help="Compact usage JSON input.")
    command.add_argument("--group", default="repeated-measured")
    command.add_argument("--expect-count", type=int, default=20)
    command.add_argument("--model", required=True)
    command.add_argument("--reasoning-effort", required=True)
    command.add_argument("--prompt-route", required=True)
    command.add_argument("--model-knowledge-cutoff", required=True)
    command.add_argument("--sampling-cutoff", required=True)
    command.add_argument("--max-output-tokens", type=int, required=True)
    command.add_argument("--source", required=True)
    command.add_argument(
        "--pricing-source",
        default=str(DEFAULT_PRICING_SOURCE),
        help="C# pricing source to read model prices from.",
    )
    command.add_argument(
        "--service-tier",
        choices=("flex", "standard"),
        default="flex",
        help="Pricing tier to use for the estimate.",
    )


def collect_records(args: argparse.Namespace) -> list[dict[str, Any]]:
    if not args.group:
        raise SystemExit("--group is required.")

    env_values = load_env_file(Path(args.env))
    expected_counts = parse_expectations(args.expect)
    deadline = time.monotonic() + max(args.wait_timeout_seconds, 0.0)

    while True:
        records = collect_records_once(args, env_values)
        sort_records(records)

        if not expected_counts:
            return records

        status = expectation_status_text(records, expected_counts)
        if expectations_met(records, expected_counts):
            print(f"Langfuse expectation counts satisfied: {status}.")
            return records

        if args.no_wait_for_expectations:
            validate_expectations(records, args.expect)
            return records

        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise SystemExit(
                "Expected observations were not visible in Langfuse before timeout: "
                f"{status}. Treat this as an ingestion timeout, not prediction failure, "
                "unless Orchestrator logs show failed items or cap exhaustion."
            )

        sleep_seconds = min(max(args.wait_interval_seconds, 0.0), remaining)
        print(
            "Langfuse ingestion pending: "
            f"{status}. Waiting {sleep_seconds:.1f}s before polling again."
        )
        if sleep_seconds > 0:
            time.sleep(sleep_seconds)


def collect_records_once(
    args: argparse.Namespace, env_values: dict[str, str]
) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    seen_trace_ids: set[str] = set()

    for group_arg in args.group:
        group, run_name = parse_pair(group_arg)
        observations = list_run_observations(args, env_values, run_name)
        for observation in observations:
            trace_id = observation.get("traceId")
            if trace_id in seen_trace_ids:
                continue
            record = extract_usage_record(group, run_name, observation)
            records.append(record)
            seen_trace_ids.add(record.get("traceId", ""))
        if args.langfuse_sleep_seconds > 0:
            time.sleep(args.langfuse_sleep_seconds)

    sort_records(records)
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
    public_key = env_values.get("LANGFUSE_PUBLIC_KEY")
    secret_key = env_values.get("LANGFUSE_SECRET_KEY")
    if not public_key or not secret_key:
        raise RuntimeError("LANGFUSE_PUBLIC_KEY and LANGFUSE_SECRET_KEY are required.")

    auth = base64.b64encode(f"{public_key}:{secret_key}".encode("utf-8")).decode(
        "ascii"
    )
    url = f"{base_url.rstrip('/')}/api/public/{relative_path}?{urlencode(query)}"

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


def extract_usage_record(measured_group: str, run_name: str, observation: dict[str, Any]) -> dict[str, Any]:
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
        "fixture": metadata.get("match"),
        "repetition": parse_repetition(dataset_item_id),
        "inputTokens": input_tokens,
        "cachedInputTokens": cached_input_tokens,
        "uncachedInputTokens": input_tokens - cached_input_tokens,
        "outputTokens": output_tokens,
        "reasoningTokens": reasoning_tokens,
        "totalTokens": total_tokens,
        "observedTotalCost": str((cost or {}).get("total", "0") or "0"),
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
    return (observation.get("metadata") or {}).get(key)


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
    return int(match.group(1)) if match else None


def sort_records(records: list[dict[str, Any]]) -> None:
    records.sort(
        key=lambda record: (
            record.get("measuredGroup", ""),
            record.get("runName", ""),
            record.get("datasetItemId", ""),
        )
    )


def validate_expectations(records: list[dict[str, Any]], expectations: list[str]) -> None:
    for group, expected in parse_expectations(expectations).items():
        actual = len(filter_group(records, group))
        if actual != expected:
            raise SystemExit(
                f"Expected {expected} records for group '{group}', found {actual}."
            )


def parse_expectations(expectations: list[str]) -> dict[str, int]:
    parsed: dict[str, int] = {}
    for expectation in expectations:
        group, count_text = parse_pair(expectation)
        expected = int(count_text)
        if expected < 0:
            raise SystemExit(f"Expected count must be non-negative, got {expected}.")
        parsed[group] = expected
    return parsed


def expectations_met(records: list[dict[str, Any]], expected_counts: dict[str, int]) -> bool:
    return all(
        len(filter_group(records, group)) == expected
        for group, expected in expected_counts.items()
    )


def expectation_status_text(
    records: list[dict[str, Any]], expected_counts: dict[str, int]
) -> str:
    statuses = []
    for group, expected in expected_counts.items():
        actual = len(filter_group(records, group))
        statuses.append(f"{group}={actual}/{expected}")
    return ", ".join(statuses)


def calculate_base_row(args: argparse.Namespace) -> dict[str, Any]:
    records = filter_group(load_json(Path(args.input)), args.group)
    if len(records) != args.expect_count:
        raise SystemExit(
            f"Expected {args.expect_count} records for group '{args.group}', found {len(records)}."
        )

    non_flex = [
        record
        for record in records
        if str(record.get("serviceTier", "")).strip().lower() != "flex"
    ]
    if args.service_tier == "flex" and non_flex:
        raise SystemExit(
            f"Expected all records to use flex service tier; found {len(non_flex)} non-flex record(s)."
        )

    cap_hits = [
        record
        for record in records
        if int(record.get("outputTokens", 0)) >= args.max_output_tokens
    ]
    if cap_hits:
        raise SystemExit(
            f"{len(cap_hits)} record(s) reached the max output token cap; rerun with a higher cap."
        )

    pricing = load_pricing(Path(args.pricing_source))[args.model]
    input_price = pricing.input_price
    output_price = pricing.output_price
    if args.service_tier == "flex":
        input_price *= FLEX_PRICE_MULTIPLIER
        output_price *= FLEX_PRICE_MULTIPLIER

    total_input_tokens = sum(int(record.get("inputTokens", 0)) for record in records)
    total_output_tokens = sum(int(record.get("outputTokens", 0)) for record in records)
    total_cached_input_tokens = sum(
        int(record.get("cachedInputTokens", 0)) for record in records
    )
    total_reasoning_tokens = sum(
        int(record.get("reasoningTokens", 0)) for record in records
    )
    observed_total_cost = sum(
        Decimal(str(record.get("totalCost", record.get("observedTotalCost", "0"))))
        for record in records
    )

    input_cost = cost_for_tokens(total_input_tokens, input_price)
    output_cost = cost_for_tokens(total_output_tokens, output_price)
    total_cost = input_cost + output_cost
    average_cost = total_cost / Decimal(len(records))

    return {
        "model": args.model,
        "reasoningEffort": args.reasoning_effort,
        "promptRoute": args.prompt_route,
        "modelKnowledgeCutoffDate": args.model_knowledge_cutoff,
        "samplingCutoffUsed": args.sampling_cutoff,
        "maxOutputTokens": args.max_output_tokens,
        "baseSampleObservations": len(records),
        "serviceTier": args.service_tier,
        "standardInputPricePerMillionUsd": decimal_text(pricing.input_price),
        "standardOutputPricePerMillionUsd": decimal_text(pricing.output_price),
        "effectiveInputPricePerMillionUsd": decimal_text(input_price),
        "effectiveOutputPricePerMillionUsd": decimal_text(output_price),
        "totalInputTokens": total_input_tokens,
        "totalCachedInputTokensObserved": total_cached_input_tokens,
        "totalOutputTokens": total_output_tokens,
        "totalReasoningTokens": total_reasoning_tokens,
        "estimatedUncachedInputCostUsd": money_text(input_cost),
        "estimatedOutputCostUsd": money_text(output_cost),
        "estimatedTotalCostUsd": money_text(total_cost),
        "averageCostPerMatchPredictionUsd": money_text(average_cost),
        "observedLangfuseCostTotalUsd": money_text(observed_total_cost),
        "source": args.source,
    }


def load_pricing(path: Path) -> dict[str, ModelPricing]:
    source = path.read_text(encoding="utf-8")
    pattern = re.compile(r'\["([^"]+)"\]\s*=\s*new\(([^)]*)\)')
    pricing: dict[str, ModelPricing] = {}
    for match in pattern.finditer(source):
        model = match.group(1)
        args = [part.strip() for part in match.group(2).split(",")]
        if len(args) < 2:
            continue
        pricing[model] = ModelPricing(
            input_price=parse_csharp_decimal(args[0]),
            output_price=parse_csharp_decimal(args[1]),
        )
    if not pricing:
        raise RuntimeError(f"No model pricing entries found in {path}.")
    return pricing


def parse_csharp_decimal(value: str) -> Decimal:
    return Decimal(value.lower().replace("m", "").replace("_", "").strip())


def cost_for_tokens(tokens: int, price_per_million: Decimal) -> Decimal:
    return (Decimal(tokens) / Decimal("1000000")) * price_per_million


def emit_base_row(report: dict[str, Any]) -> None:
    print("Base estimate row:")
    emit_base_estimate_summary(report)
    print()
    print("JSON payload:")
    print(json.dumps(report, indent=2, ensure_ascii=True))


def calculate_estimate(args: argparse.Namespace) -> dict[str, Any]:
    counts = parse_counts(args)
    for count in counts:
        if count < 1:
            raise SystemExit("Every count must be at least 1.")

    store = load_base_estimate_store(Path(args.store))
    row = lookup_base_estimate_row(
        store["baseEstimates"], args.model, args.reasoning_effort
    )
    average = Decimal(row["averageCostPerMatchPredictionUsd"])
    estimates = []
    for count in counts:
        total = average * Decimal(count)
        estimates.append(
            {
                "matchPredictionCount": count,
                "averageCostPerMatchPredictionUsd": money_text(average),
                "estimatedTotalCostUsd": money_text(total),
            }
        )

    return {
        "model": args.model,
        "reasoningEffort": args.reasoning_effort,
        "counts": counts,
        "averageCostPerMatchPredictionUsd": money_text(average),
        "baseEstimate": row,
        "estimates": estimates,
    }


def parse_counts(args: argparse.Namespace) -> list[int]:
    values: list[str] = []
    if args.counts:
        values.extend(part.strip() for part in args.counts.split(","))
    values.extend(str(count) for count in args.count)
    try:
        counts = [int(value) for value in values if value]
    except ValueError as ex:
        raise SystemExit("--counts must contain integers, for example 20,60,100.") from ex
    if not counts:
        raise SystemExit("--counts is required, for example --counts 20,60,100.")
    return counts


def load_base_estimate_store(path: Path) -> dict[str, Any]:
    store = load_json(path)
    if not isinstance(store, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
    if store.get("schemaVersion") != 1:
        raise RuntimeError(f"Unsupported base estimate schemaVersion in {path}.")
    rows = store.get("baseEstimates")
    if not isinstance(rows, list):
        raise RuntimeError(f"Expected baseEstimates array in {path}.")
    return store


def lookup_base_estimate_row(
    rows: list[dict[str, Any]], model: str, reasoning_effort: str
) -> dict[str, Any]:
    matches = [
        row
        for row in rows
        if row.get("model") == model and row.get("reasoningEffort") == reasoning_effort
    ]
    if not matches:
        raise SystemExit(
            "No matching base estimate JSON row found for "
            f"model={model!r}, reasoningEffort={reasoning_effort!r}."
        )
    if len(matches) > 1:
        raise SystemExit(
            "More than one matching base estimate JSON row found for "
            f"model={model!r}, reasoningEffort={reasoning_effort!r}. "
            "This estimator does not guess between prompt route or max-output "
            "qualifiers; add explicit qualifier support before estimating."
        )
    return matches[0]


def upsert_base_estimate(
    path: Path, row: dict[str, Any], replace_existing: bool
) -> str:
    if path.exists():
        store = load_base_estimate_store(path)
    else:
        store = {"schemaVersion": 1, "updatedAt": today_text(), "baseEstimates": []}

    rows = store["baseEstimates"]
    matches = [
        index
        for index, existing in enumerate(rows)
        if existing.get("model") == row["model"]
        and existing.get("reasoningEffort") == row["reasoningEffort"]
    ]
    if len(matches) > 1:
        raise SystemExit(
            "More than one existing row matches "
            f"model={row['model']!r}, reasoningEffort={row['reasoningEffort']!r}."
        )

    if not matches:
        rows.append(row)
        action = "added"
    else:
        index = matches[0]
        if rows[index] == row:
            return "unchanged"
        if not replace_existing:
            raise SystemExit(
                "An existing base estimate row differs for "
                f"model={row['model']!r}, reasoningEffort={row['reasoningEffort']!r}. "
                "Re-run with --replace to update it."
            )
        rows[index] = row
        action = "replaced"

    store["updatedAt"] = today_text()
    write_json(path, store)
    return action


def emit_base_estimate_summary(report: dict[str, Any]) -> None:
    print(
        "Base estimate: "
        f"model={report['model']}, "
        f"reasoningEffort={report['reasoningEffort']}, "
        f"sample={report['baseSampleObservations']}, "
        f"maxOutputTokens={report['maxOutputTokens']}, "
        f"averageCostPerMatch=${report['averageCostPerMatchPredictionUsd']}"
    )
    print(
        "Source: "
        f"{report['source']} "
        f"(knowledge cutoff {report['modelKnowledgeCutoffDate']}, "
        f"sampling cutoff {report['samplingCutoffUsed']})"
    )


def emit_estimate(report: dict[str, Any]) -> None:
    row = report["baseEstimate"]
    print("Base estimate source:")
    print(
        f"Model: {row['model']} | Reasoning effort: {row['reasoningEffort']} | "
        f"Average cost per match prediction: "
        f"${row['averageCostPerMatchPredictionUsd']}"
    )
    print(
        f"Sample: {row['baseSampleObservations']} | "
        f"Max output tokens: {row['maxOutputTokens']} | "
        f"Model knowledge cutoff: {row['modelKnowledgeCutoffDate']} | "
        f"Sampling cutoff: {row['samplingCutoffUsed']}"
    )
    print(f"Prompt route: {row['promptRoute']}")
    print(f"Source: {row['source']}")
    print()
    print("Estimates:")
    for estimate in report["estimates"]:
        print(
            f"N={estimate['matchPredictionCount']}: "
            f"${estimate['estimatedTotalCostUsd']}"
        )


def filter_group(records: list[dict[str, Any]], group: str) -> list[dict[str, Any]]:
    return [
        record
        for record in records
        if record.get("measuredGroup") == group or record.get("datasetType") == group
    ]


def parse_pair(value: str) -> tuple[str, str]:
    if "=" not in value:
        raise SystemExit(f"Expected KEY=VALUE, got '{value}'.")
    key, parsed_value = value.split("=", 1)
    key = key.strip()
    parsed_value = parsed_value.strip()
    if not key or not parsed_value:
        raise SystemExit(f"Expected KEY=VALUE, got '{value}'.")
    return key, parsed_value


def money_text(value: Decimal) -> str:
    return format(
        value.quantize(Decimal("0.000000000001"), rounding=ROUND_HALF_UP), "f"
    )


def decimal_text(value: Decimal) -> str:
    return format(value.normalize(), "f")


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(payload, stream, indent=2, ensure_ascii=True)
        stream.write("\n")


def try_write_optional_json(path: Path, payload: Any) -> None:
    try:
        write_json(path, payload)
    except OSError as ex:
        print(
            f"WARNING: optional --report-json output could not be written to {path}: {ex}",
            file=sys.stderr,
        )


def today_text() -> str:
    return datetime.now(timezone.utc).date().isoformat()


if __name__ == "__main__":
    sys.exit(main())
