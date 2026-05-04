"""Collect usage records and calculate KicktippAi experiment cost estimates.

The estimate intentionally treats every input token as uncached. This makes the
base rows suitable for slice experiments and conservative for repeated-match
experiments where prompt caching may occur during data collection.
"""

from __future__ import annotations

import argparse
import base64
import csv
import email.utils
import json
import os
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
FLEX_PRICE_MULTIPLIER = Decimal("0.5")


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

    base_row = subparsers.add_parser(
        "base-row", help="Calculate a base estimate table row from compact usage."
    )
    base_row.add_argument("--input", required=True, help="Compact usage JSON input.")
    base_row.add_argument("--group", default="repeated-measured")
    base_row.add_argument("--expect-count", type=int, default=20)
    base_row.add_argument("--model", required=True)
    base_row.add_argument("--reasoning-effort", required=True)
    base_row.add_argument("--prompt-route", required=True)
    base_row.add_argument("--model-knowledge-cutoff", required=True)
    base_row.add_argument("--sampling-cutoff", required=True)
    base_row.add_argument("--max-output-tokens", type=int, required=True)
    base_row.add_argument("--source", required=True)
    base_row.add_argument(
        "--pricing-source",
        default=str(DEFAULT_PRICING_SOURCE),
        help="C# pricing source to read model prices from.",
    )
    base_row.add_argument(
        "--service-tier",
        choices=("flex", "standard"),
        default="flex",
        help="Pricing tier to use for the estimate.",
    )
    base_row.add_argument("--report-json", help="Optional machine-readable report.")

    estimate = subparsers.add_parser(
        "estimate", help="Estimate an experiment total from the base table."
    )
    estimate.add_argument("--count", type=int, required=True)
    estimate.add_argument(
        "--table",
        default=".agents/skills/estimate-experiment-cost-skill/references/base-estimate-table.md",
        help="Markdown base estimate table.",
    )
    estimate.add_argument("--model", help="Model name for table lookup.")
    estimate.add_argument("--reasoning-effort", help="Reasoning effort for table lookup.")
    estimate.add_argument(
        "--max-output-tokens",
        type=int,
        help="Optional max output token qualifier for table lookup.",
    )
    estimate.add_argument(
        "--prompt-route-contains",
        help="Optional prompt-route substring qualifier for table lookup.",
    )
    estimate.add_argument(
        "--average-cost-per-match",
        type=Decimal,
        help="Use this average instead of looking up a table row.",
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
            write_json(Path(args.report_json), report)
        return 0
    if args.command == "estimate":
        report = calculate_estimate(args)
        emit_estimate(report)
        if args.report_json:
            write_json(Path(args.report_json), report)
        return 0

    raise AssertionError(f"Unhandled command {args.command!r}.")


def collect_records(args: argparse.Namespace) -> list[dict[str, Any]]:
    if not args.group:
        raise SystemExit("--group is required.")

    records: list[dict[str, Any]] = []
    seen_trace_ids: set[str] = set()
    env_values = load_env_file(Path(args.env))

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
    for expectation in expectations:
        group, count_text = parse_pair(expectation)
        expected = int(count_text)
        actual = len(filter_group(records, group))
        if actual != expected:
            raise SystemExit(
                f"Expected {expected} records for group '{group}', found {actual}."
            )


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
    print("Base estimate table row:")
    print()
    print(
        "| Model | Reasoning effort | Prompt route | Model knowledge cutoff date | "
        "Sampling cutoff used | Max output tokens | Base sample observations | "
        "Total input tokens | Estimated uncached input cost (USD) | "
        "Total output tokens | Estimated output cost (USD) | "
        "Estimated total cost (USD) | Average cost per match prediction (USD) | Source |"
    )
    print(
        "| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | "
        "---: | ---: | ---: | ---: | --- |"
    )
    print(markdown_row(report))


def markdown_row(report: dict[str, Any]) -> str:
    values = [
        code(report["model"]),
        code(report["reasoningEffort"]),
        report["promptRoute"],
        code(report["modelKnowledgeCutoffDate"]),
        code(report["samplingCutoffUsed"]),
        str(report["maxOutputTokens"]),
        str(report["baseSampleObservations"]),
        str(report["totalInputTokens"]),
        report["estimatedUncachedInputCostUsd"],
        str(report["totalOutputTokens"]),
        report["estimatedOutputCostUsd"],
        report["estimatedTotalCostUsd"],
        report["averageCostPerMatchPredictionUsd"],
        report["source"],
    ]
    return "| " + " | ".join(values) + " |"


def calculate_estimate(args: argparse.Namespace) -> dict[str, Any]:
    if args.count < 1:
        raise SystemExit("--count must be at least 1.")

    if args.average_cost_per_match is not None:
        average = args.average_cost_per_match
        row = None
    else:
        if not args.model or not args.reasoning_effort:
            raise SystemExit(
                "--model and --reasoning-effort are required unless --average-cost-per-match is provided."
            )
        row = lookup_table_row(
            Path(args.table),
            args.model,
            args.reasoning_effort,
            args.max_output_tokens,
            args.prompt_route_contains,
        )
        average = Decimal(row["Average cost per match prediction (USD)"])

    total = average * Decimal(args.count)
    report = {
        "matchPredictionCount": args.count,
        "averageCostPerMatchPredictionUsd": money_text(average),
        "estimatedTotalCostUsd": money_text(total),
    }
    if row:
        report["tableRow"] = row
    return report


def lookup_table_row(
    path: Path,
    model: str,
    reasoning_effort: str,
    max_output_tokens: int | None,
    prompt_route_contains: str | None,
) -> dict[str, str]:
    rows = read_markdown_table(path)
    matches = [
        row
        for row in rows
        if clean_cell(row.get("Model", "")) == model
        and clean_cell(row.get("Reasoning effort", "")) == reasoning_effort
    ]
    if max_output_tokens is not None:
        matches = [
            row
            for row in matches
            if int(clean_cell(row.get("Max output tokens", "0"))) == max_output_tokens
        ]
    if prompt_route_contains:
        matches = [
            row
            for row in matches
            if prompt_route_contains.lower() in clean_cell(row.get("Prompt route", "")).lower()
        ]

    if not matches:
        raise SystemExit("No matching base estimate table row found.")
    if len(matches) > 1:
        raise SystemExit(
            "More than one matching table row found; add prompt route or max-output qualifier."
        )
    return matches[0]


def read_markdown_table(path: Path) -> list[dict[str, str]]:
    table_lines = [
        line
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.startswith("| ")
    ]
    if len(table_lines) < 3:
        raise RuntimeError(f"No markdown table found in {path}.")
    header = split_markdown_row(table_lines[0])
    rows = []
    for line in table_lines[2:]:
        cells = split_markdown_row(line)
        if len(cells) == len(header):
            rows.append(dict(zip(header, cells)))
    return rows


def split_markdown_row(line: str) -> list[str]:
    cells = next(csv.reader([line.strip().strip("|")], delimiter="|", skipinitialspace=True))
    return [cell.strip() for cell in cells]


def clean_cell(value: str) -> str:
    return value.strip().strip("`").strip()


def emit_estimate(report: dict[str, Any]) -> None:
    print(f"Match predictions: {report['matchPredictionCount']}")
    print(f"Average cost per match prediction: ${report['averageCostPerMatchPredictionUsd']}")
    print(f"Estimated total cost: ${report['estimatedTotalCostUsd']}")


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


def code(value: Any) -> str:
    return f"`{value}`"


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


if __name__ == "__main__":
    sys.exit(main())
