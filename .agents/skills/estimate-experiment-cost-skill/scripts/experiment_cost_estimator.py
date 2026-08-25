"""Collect usage records and calculate KicktippAi experiment cost estimates.

The estimate intentionally treats every input token as uncached. This makes the
base rows suitable for slice experiments and conservative for repeated-match
experiments where prompt caching may occur during data collection.
"""

from __future__ import annotations

import argparse
import base64
import email.utils
import hashlib
import json
import re
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation, ROUND_HALF_UP
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
SHORT_CONTEXT_INPUT_TOKEN_LIMIT = 272_000
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
        "--dataset-id",
        action="append",
        default=[],
        metavar="GROUP=DATASET_ID",
        help=(
            "Bind a group to an exact Langfuse dataset. Must be paired with "
            "--dataset-run-id for the same group."
        ),
    )
    collect.add_argument(
        "--dataset-run-id",
        action="append",
        default=[],
        metavar="GROUP=DATASET_RUN_ID",
        help=(
            "Collect only observations linked to this exact Langfuse dataset run. "
            "Must be paired with --dataset-id for the same group."
        ),
    )
    collect.add_argument(
        "--manifest",
        action="append",
        default=[],
        metavar="GROUP=PATH",
        help=(
            "Required for a dataset-run-bound group. Validates the exact "
            "prepared manifest item identities."
        ),
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

    budget_gate = subparsers.add_parser(
        "budget-gate",
        help=(
            "Aggregate a candidate quality wave, spend, and reserves against a "
            "strict USD ceiling."
        ),
    )
    budget_gate.add_argument(
        "--candidate",
        action="append",
        default=[],
        metavar="MODEL,REASONING_EFFORT,COUNT",
        help=(
            "Candidate prediction count to estimate from an exact authoritative "
            "row. Repeat for every wave entry, including repeated configurations."
        ),
    )
    budget_gate.add_argument(
        "--provisional-candidate",
        action="append",
        default=[],
        metavar="REPORT_JSON,COUNT",
        help=(
            "Candidate prediction count projected from an exact one-observation "
            "base-row JSON report. Repeat for every provisional wave entry."
        ),
    )
    budget_gate.add_argument(
        "--planned-preflight",
        action="append",
        default=[],
        metavar="SPEC_JSON",
        help=(
            "One-call bootstrap preflight specification JSON. Repeat for every "
            "planned preflight."
        ),
    )
    budget_gate.add_argument(
        "--observed-attempt",
        action="append",
        default=[],
        metavar="NAME=USD",
        help=(
            "Named settled experiment attempt and its observed USD spend. Repeat "
            "for every attempt; names must be unique."
        ),
    )
    budget_gate.add_argument(
        "--observed-spend-usd",
        help=(
            "Compatibility-only scalar settled spend. Cannot be combined with "
            "--observed-attempt."
        ),
    )
    budget_gate.add_argument(
        "--reservation",
        action="append",
        default=[],
        metavar="NAME=USD",
        help="Unsettled USD reservation. Repeat for every reservation.",
    )
    budget_gate.add_argument(
        "--retry-reserve",
        action="append",
        default=[],
        metavar="MODEL,REASONING_EFFORT,COUNT",
        help=(
            "Retry prediction reserve estimated from the same authoritative rows. "
            "Repeat for every reserve entry."
        ),
    )
    budget_gate.add_argument(
        "--provisional-retry-reserve",
        action="append",
        default=[],
        metavar="REPORT_JSON,COUNT",
        help=(
            "Retry prediction reserve projected from an exact one-observation "
            "base-row JSON report. Repeat for every provisional reserve entry."
        ),
    )
    budget_gate.add_argument(
        "--ceiling-usd",
        required=True,
        help="Strict all-in experiment ceiling in USD.",
    )
    budget_gate.add_argument(
        "--store",
        default=str(DEFAULT_BASE_ESTIMATES_SOURCE),
        help="JSON base estimate store.",
    )
    budget_gate.add_argument(
        "--pricing-source",
        default=str(DEFAULT_PRICING_SOURCE),
        help="C# short-context standard pricing source for planned preflights.",
    )
    budget_gate.add_argument(
        "--report-json",
        help="Machine-readable admission report; its write is mandatory when set.",
    )

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
    if args.command == "budget-gate":
        report = calculate_budget_gate(args)
        if args.report_json:
            try:
                write_json(Path(args.report_json), report)
            except OSError as ex:
                report["result"] = "blocked"
                report["admissionErrors"] = [
                    f"Requested budget-gate JSON could not be written: {ex}"
                ]
                emit_budget_gate(report)
                print(report["admissionErrors"][0], file=sys.stderr)
                return 3
        emit_budget_gate(report)
        return 0 if report["result"] == "allowed" else 2

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
        choices=("flex", "standard", "observed"),
        default="flex",
        help=(
            "Pricing tier to use for the estimate. Use observed when flex-first "
            "runs may include non-flex retry fallbacks."
        ),
    )


def collect_records(args: argparse.Namespace) -> list[dict[str, Any]]:
    if not args.group:
        raise SystemExit("--group is required.")

    env_values = load_env_file(Path(args.env))
    expected_counts = parse_expectations(args.expect)
    validate_collect_bindings(args)
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

        overcounts = {
            group: (len(filter_group(records, group)), expected)
            for group, expected in expected_counts.items()
            if len(filter_group(records, group)) > expected
        }
        if overcounts:
            detail = ", ".join(
                f"{group}={actual}/{expected}"
                for group, (actual, expected) in overcounts.items()
            )
            raise SystemExit(
                "Langfuse collection returned more observations than expected: "
                f"{detail}. Do not truncate or select observations by timestamp. "
                "For a replaced or retried run name, bind the exact current run with "
                "--dataset-id GROUP=DATASET_ID and "
                "--dataset-run-id GROUP=DATASET_RUN_ID, and pass the required "
                "--manifest GROUP=PATH."
            )

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
    dataset_ids = parse_unique_pairs(getattr(args, "dataset_id", []), "--dataset-id")
    dataset_run_ids = parse_unique_pairs(
        getattr(args, "dataset_run_id", []), "--dataset-run-id"
    )
    manifests = parse_unique_pairs(getattr(args, "manifest", []), "--manifest")
    expected_counts = parse_expectations(getattr(args, "expect", []))

    for group_arg in args.group:
        group, run_name = parse_pair(group_arg)
        observations = list_run_observations(args, env_values, run_name)
        provenance: dict[str, Any] = {}
        if group in dataset_run_ids:
            observations, provenance = select_dataset_run_observations(
                args,
                env_values,
                run_name,
                dataset_ids[group],
                dataset_run_ids[group],
                manifests.get(group),
                expected_counts.get(group),
                observations,
            )

        for observation in observations:
            trace_id = observation.get("traceId")
            if trace_id in seen_trace_ids:
                continue
            record = extract_usage_record(group, run_name, observation)
            record.update(provenance)
            records.append(record)
            seen_trace_ids.add(record.get("traceId", ""))
        if args.langfuse_sleep_seconds > 0:
            time.sleep(args.langfuse_sleep_seconds)

    sort_records(records)
    return records


def validate_collect_bindings(args: argparse.Namespace) -> None:
    groups = {parse_pair(value)[0] for value in args.group}
    dataset_ids = parse_unique_pairs(getattr(args, "dataset_id", []), "--dataset-id")
    dataset_run_ids = parse_unique_pairs(
        getattr(args, "dataset_run_id", []), "--dataset-run-id"
    )
    manifests = parse_unique_pairs(getattr(args, "manifest", []), "--manifest")
    expected_counts = parse_expectations(getattr(args, "expect", []))

    if dataset_ids.keys() != dataset_run_ids.keys():
        raise SystemExit(
            "--dataset-id and --dataset-run-id must be provided together for the "
            "same groups."
        )

    unknown = (dataset_ids.keys() | manifests.keys()) - groups
    if unknown:
        raise SystemExit(
            "Collector binding references unknown group(s): "
            + ", ".join(sorted(unknown))
            + "."
        )

    manifest_without_run = manifests.keys() - dataset_run_ids.keys()
    if manifest_without_run:
        raise SystemExit(
            "--manifest requires an exact --dataset-id/--dataset-run-id binding for "
            "group(s): "
            + ", ".join(sorted(manifest_without_run))
            + "."
        )

    missing_manifests = dataset_run_ids.keys() - manifests.keys()
    if missing_manifests:
        raise SystemExit(
            "Exact dataset-run collection requires --manifest for group(s): "
            + ", ".join(sorted(missing_manifests))
            + "."
        )

    missing_expectations = dataset_run_ids.keys() - expected_counts.keys()
    if missing_expectations:
        raise SystemExit(
            "Exact dataset-run collection requires --expect for group(s): "
            + ", ".join(sorted(missing_expectations))
            + "."
        )


def parse_unique_pairs(values: list[str], option_name: str) -> dict[str, str]:
    parsed: dict[str, str] = {}
    for value in values:
        key, parsed_value = parse_pair(value)
        if key in parsed:
            raise SystemExit(f"{option_name} was provided more than once for '{key}'.")
        parsed[key] = parsed_value
    return parsed


def select_dataset_run_observations(
    args: argparse.Namespace,
    env_values: dict[str, str],
    run_name: str,
    dataset_id: str,
    dataset_run_id: str,
    manifest_path: str | None,
    expected_count: int | None,
    observations: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    run_items = list_dataset_run_items(args, env_values, dataset_id, run_name)
    selected_items = [
        item for item in run_items if item.get("datasetRunId") == dataset_run_id
    ]
    provenance = {
        "datasetId": dataset_id,
        "datasetRunId": dataset_run_id,
        "datasetRunName": run_name,
    }
    if not selected_items and expected_count is None:
        raise RuntimeError(
            f"Dataset run '{dataset_run_id}' has no items for dataset '{dataset_id}' "
            f"and run name '{run_name}'."
        )

    dataset_item_to_trace: dict[str, str] = {}
    trace_to_dataset_item: dict[str, str] = {}
    for item in selected_items:
        item_run_name = str(item.get("datasetRunName") or "").strip()
        dataset_item_id = str(item.get("datasetItemId") or "").strip()
        trace_id = str(item.get("traceId") or "").strip()
        if item_run_name != run_name:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' item has run name "
                f"'{item_run_name}', expected '{run_name}'."
            )
        if not dataset_item_id or not trace_id:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' contains an item without a "
                "dataset item ID or trace ID."
            )
        if dataset_item_id in dataset_item_to_trace:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' contains duplicate dataset item "
                f"ID '{dataset_item_id}'."
            )
        if trace_id in trace_to_dataset_item:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' links trace '{trace_id}' more than once."
            )
        dataset_item_to_trace[dataset_item_id] = trace_id
        trace_to_dataset_item[trace_id] = dataset_item_id

    if expected_count is not None and len(selected_items) > expected_count:
        raise RuntimeError(
            f"Dataset run '{dataset_run_id}' links {len(selected_items)} distinct "
            f"items, expected {expected_count}."
        )

    run_links_complete = expected_count is None or len(selected_items) == expected_count
    if manifest_path and run_links_complete:
        manifest_provenance = validate_manifest_item_identity(
            Path(manifest_path), dataset_item_to_trace.keys(), expected_count
        )
        provenance.update(manifest_provenance)

    observations_by_trace: dict[str, dict[str, Any]] = {}
    for observation in observations:
        trace_id = str(observation.get("traceId") or "").strip()
        if trace_id not in trace_to_dataset_item:
            continue
        if trace_id in observations_by_trace:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' trace '{trace_id}' has more than one "
                "predict-match generation observation."
            )
        observation_dataset_item_id = extract_dataset_item_id(observation)
        linked_dataset_item_id = trace_to_dataset_item[trace_id]
        if observation_dataset_item_id != linked_dataset_item_id:
            raise RuntimeError(
                f"Dataset run '{dataset_run_id}' trace '{trace_id}' reports dataset "
                f"item '{observation_dataset_item_id}', but the immutable run link "
                f"targets '{linked_dataset_item_id}'."
            )
        observations_by_trace[trace_id] = observation

    return list(observations_by_trace.values()), provenance


def validate_manifest_item_identity(
    path: Path, dataset_item_ids: Any, expected_count: int | None
) -> dict[str, Any]:
    manifest_bytes = path.read_bytes()
    manifest = json.loads(manifest_bytes.decode("utf-8"))
    manifest_items = manifest.get("items") or []
    manifest_item_ids = [
        str(item.get("sliceDatasetItemId") or "").strip() for item in manifest_items
    ]
    if not manifest_item_ids or any(not item_id for item_id in manifest_item_ids):
        raise RuntimeError(f"Prepared manifest '{path}' has missing item identities.")
    if len(manifest_item_ids) != len(set(manifest_item_ids)):
        raise RuntimeError(f"Prepared manifest '{path}' has duplicate item identities.")
    sample_size = manifest.get("sampleSize")
    if sample_size != len(manifest_item_ids):
        raise RuntimeError(
            f"Prepared manifest '{path}' binds sampleSize={sample_size}, but contains "
            f"{len(manifest_item_ids)} items."
        )
    if expected_count is not None and len(manifest_item_ids) != expected_count:
        raise RuntimeError(
            f"Prepared manifest '{path}' contains {len(manifest_item_ids)} items, "
            f"expected {expected_count}."
        )
    linked_item_ids = set(dataset_item_ids)
    if set(manifest_item_ids) != linked_item_ids:
        missing = sorted(set(manifest_item_ids) - linked_item_ids)
        unexpected = sorted(linked_item_ids - set(manifest_item_ids))
        raise RuntimeError(
            f"Dataset-run item identity differs from prepared manifest '{path}': "
            f"missing={missing}, unexpected={unexpected}."
        )
    return {
        "preparedManifestSha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "preparedManifestSampleSize": len(manifest_item_ids),
    }


def extract_dataset_item_id(observation: dict[str, Any]) -> str:
    metadata = observation.get("metadata") or {}
    return str(
        metadata.get("attributes.langfuse.experiment.item.id")
        or metadata.get("langfuse.experiment.item.id")
        or ""
    ).strip()


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


def list_dataset_run_items(
    args: argparse.Namespace,
    env_values: dict[str, str],
    dataset_id: str,
    run_name: str,
) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    page = 1
    page_size = 100
    while True:
        query = {
            "datasetId": dataset_id,
            "runName": run_name,
            "page": str(page),
            "limit": str(page_size),
        }
        body = langfuse_get_json(args, env_values, "dataset-run-items", query)
        page_items = body.get("data", [])
        items.extend(page_items)
        meta = body.get("meta") or {}
        total_pages = int(meta.get("totalPages") or 0)
        total_items = int(meta.get("totalItems") or 0)
        if total_pages:
            if page >= total_pages:
                break
        elif total_items:
            if len(items) >= total_items:
                break
        elif len(page_items) < page_size:
            break
        page += 1

    return items


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

    final_service_tier = metadata.get("openaiFinalServiceTier") or model_parameters.get(
        "service_tier"
    )
    requested_service_tier = metadata.get(
        "openaiRequestedServiceTier"
    ) or model_parameters.get("service_tier")

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
        "requestedServiceTier": requested_service_tier,
        "serviceTier": final_service_tier,
        "openaiExecutionStrategy": metadata.get("openaiExecutionStrategy"),
        "serviceTierFallbackUsed": parse_bool(
            metadata.get("openaiServiceTierFallbackUsed")
        ),
    }


def nested_metadata(observation: dict[str, Any], key: str) -> Any:
    return (observation.get("metadata") or {}).get(key)


def parse_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    return str(value).strip().lower() == "true"


def dataset_type_for_group(measured_group: str) -> str:
    if measured_group.startswith("repeated-match-slice") or measured_group.startswith(
        "repeated-slice"
    ):
        return "repeated-match-slice"
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

    service_tier_counts = count_service_tiers(records)
    non_flex = [
        record
        for record in records
        if normalized_service_tier(record.get("serviceTier")) != "flex"
    ]

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

    input_cost = sum_input_cost(records, pricing, args.service_tier)
    output_cost = sum_output_cost(records, pricing, args.service_tier)
    total_cost = input_cost + output_cost
    average_cost = total_cost / Decimal(len(records))
    non_flex_retry_count = sum(
        1
        for record in records
        if normalized_service_tier(record.get("serviceTier")) != "flex"
        and parse_bool(record.get("serviceTierFallbackUsed"))
    )
    fallback_used_count = sum(
        1 for record in records if parse_bool(record.get("serviceTierFallbackUsed"))
    )
    observed_flex_count = service_tier_counts.get("flex", 0)
    collection_provenance = validate_collection_provenance(records)

    row = {
        "model": args.model,
        "reasoningEffort": args.reasoning_effort,
        "promptRoute": args.prompt_route,
        "modelKnowledgeCutoffDate": args.model_knowledge_cutoff,
        "samplingCutoffUsed": args.sampling_cutoff,
        "maxOutputTokens": args.max_output_tokens,
        "baseSampleObservations": len(records),
        "serviceTier": args.service_tier,
        "observedServiceTierCounts": service_tier_counts,
        "observedFlexRequestCount": observed_flex_count,
        "nonFlexRequestCount": len(non_flex),
        "nonFlexRequestRate": rate_text(len(non_flex), len(records)),
        "nonFlexRequestPercent": percent_text(len(non_flex), len(records)),
        "nonFlexRetryCount": non_flex_retry_count,
        "nonFlexRetryRate": rate_text(non_flex_retry_count, len(records)),
        "nonFlexRetryPercent": percent_text(non_flex_retry_count, len(records)),
        "serviceTierFallbackUsedCount": fallback_used_count,
        "serviceTierFallbackUsedRate": rate_text(fallback_used_count, len(records)),
        "serviceTierFallbackUsedPercent": percent_text(
            fallback_used_count,
            len(records),
        ),
        "standardInputPricePerMillionUsd": decimal_text(pricing.input_price),
        "standardOutputPricePerMillionUsd": decimal_text(pricing.output_price),
        "effectiveInputPricePerMillionUsd": decimal_or_text(
            effective_input_price_for_summary(pricing, args.service_tier)
        ),
        "effectiveOutputPricePerMillionUsd": decimal_or_text(
            effective_output_price_for_summary(pricing, args.service_tier)
        ),
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
    row.update(collection_provenance)
    return row


def validate_collection_provenance(records: list[dict[str, Any]]) -> dict[str, Any]:
    required_fields = ("datasetId", "datasetRunId", "datasetRunName")
    manifest_fields = (
        "preparedManifestSha256",
        "preparedManifestSampleSize",
    )
    if not any(
        record.get(field)
        for record in records
        for field in required_fields + manifest_fields
    ):
        return {}

    provenance: dict[str, Any] = {}
    for field in required_fields:
        values = [record.get(field) for record in records]
        if any(value is None or str(value).strip() == "" for value in values):
            raise SystemExit(
                f"Compact usage has partial dataset-run provenance for '{field}'."
            )
        distinct = {str(value) for value in values}
        if len(distinct) != 1:
            raise SystemExit(
                f"Compact usage spans multiple values for '{field}': "
                + ", ".join(sorted(distinct))
                + "."
            )
        provenance[field] = values[0]

    for field in ("datasetItemId", "traceId"):
        identities = [str(record.get(field) or "").strip() for record in records]
        if any(not identity for identity in identities):
            raise SystemExit(
                f"Dataset-run-bound compact usage has a missing '{field}'."
            )
        if len(set(identities)) != len(identities):
            raise SystemExit(
                f"Dataset-run-bound compact usage has duplicate '{field}' values."
            )

    manifest_hashes = [
        str(record.get("preparedManifestSha256") or "").strip()
        for record in records
    ]
    raw_sample_sizes = [record.get("preparedManifestSampleSize") for record in records]
    if any(not manifest_hash for manifest_hash in manifest_hashes) or any(
        value is None or str(value).strip() == "" for value in raw_sample_sizes
    ):
        raise SystemExit(
            "Dataset-run-bound compact usage requires preparedManifestSha256 and "
            "preparedManifestSampleSize on every record."
        )

    distinct_hashes = set(manifest_hashes)
    if len(distinct_hashes) != 1:
        raise SystemExit(
            "Compact usage spans multiple prepared manifest hashes: "
            + ", ".join(sorted(distinct_hashes))
            + "."
        )

    if any(
        isinstance(value, bool) or not isinstance(value, int)
        for value in raw_sample_sizes
    ):
        raise SystemExit(
            "Compact usage has a non-integer prepared manifest sample size."
        )
    sample_sizes = raw_sample_sizes
    if len(set(sample_sizes)) != 1:
        raise SystemExit(
            "Compact usage spans multiple prepared manifest sample sizes."
        )
    if sample_sizes[0] != len(records):
        raise SystemExit(
            "Prepared manifest sample size "
            f"{sample_sizes[0]} does not equal the accepted compact usage count "
            f"{len(records)}."
        )

    provenance["preparedManifestSha256"] = manifest_hashes[0]
    provenance["preparedManifestSampleSize"] = sample_sizes[0]

    return provenance


def count_service_tiers(records: list[dict[str, Any]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for record in records:
        tier = normalized_service_tier(record.get("serviceTier"))
        counts[tier] = counts.get(tier, 0) + 1
    return dict(sorted(counts.items()))


def normalized_service_tier(value: Any) -> str:
    text = "" if value is None else str(value).strip().lower()
    return text or "standard"


def sum_input_cost(
    records: list[dict[str, Any]], pricing: ModelPricing, service_tier: str
) -> Decimal:
    return sum(
        cost_for_tokens(
            int(record.get("inputTokens", 0)),
            input_price_for_tier(pricing, pricing_tier_for_record(record, service_tier)),
        )
        for record in records
    )


def sum_output_cost(
    records: list[dict[str, Any]], pricing: ModelPricing, service_tier: str
) -> Decimal:
    return sum(
        cost_for_tokens(
            int(record.get("outputTokens", 0)),
            output_price_for_tier(pricing, pricing_tier_for_record(record, service_tier)),
        )
        for record in records
    )


def pricing_tier_for_record(record: dict[str, Any], service_tier: str) -> str:
    if service_tier != "observed":
        return service_tier
    return normalized_service_tier(record.get("serviceTier"))


def input_price_for_tier(pricing: ModelPricing, service_tier: str) -> Decimal:
    if service_tier == "flex":
        return pricing.input_price * FLEX_PRICE_MULTIPLIER
    return pricing.input_price


def output_price_for_tier(pricing: ModelPricing, service_tier: str) -> Decimal:
    if service_tier == "flex":
        return pricing.output_price * FLEX_PRICE_MULTIPLIER
    return pricing.output_price


def effective_input_price_for_summary(
    pricing: ModelPricing, service_tier: str
) -> Decimal | str:
    if service_tier == "observed":
        return "observed"
    return input_price_for_tier(pricing, service_tier)


def effective_output_price_for_summary(
    pricing: ModelPricing, service_tier: str
) -> Decimal | str:
    if service_tier == "observed":
        return "observed"
    return output_price_for_tier(pricing, service_tier)


def load_pricing(path: Path) -> dict[str, ModelPricing]:
    source = path.read_text(encoding="utf-8")
    return parse_pricing_source(source, path)


def parse_pricing_source(source: str, path: Path) -> dict[str, ModelPricing]:
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


def calculate_budget_gate(args: argparse.Namespace) -> dict[str, Any]:
    store = load_base_estimate_store(Path(args.store))
    candidates, authoritative_wave_cost = calculate_authoritative_budget_entries(
        getattr(args, "candidate", []),
        store["baseEstimates"],
        "--candidate",
    )
    provisional_candidates, provisional_wave_cost = (
        calculate_provisional_budget_entries(
            getattr(args, "provisional_candidate", []),
            "--provisional-candidate",
            start_index=len(candidates) + 1,
        )
    )
    candidates.extend(provisional_candidates)
    planned_preflights, planned_preflight_cost = (
        calculate_planned_preflight_entries(
            getattr(args, "planned_preflight", []),
            Path(getattr(args, "pricing_source", DEFAULT_PRICING_SOURCE)),
            start_index=len(candidates) + 1,
        )
    )
    candidates.extend(planned_preflights)
    if not candidates:
        raise SystemExit(
            "At least one --candidate, --provisional-candidate, or "
            "--planned-preflight is required."
        )
    projected_wave_cost = (
        authoritative_wave_cost + provisional_wave_cost + planned_preflight_cost
    )

    retry_reserves, authoritative_retry_cost = (
        calculate_authoritative_budget_entries(
            getattr(args, "retry_reserve", []),
            store["baseEstimates"],
            "--retry-reserve",
        )
    )
    provisional_retries, provisional_retry_cost = (
        calculate_provisional_budget_entries(
            getattr(args, "provisional_retry_reserve", []),
            "--provisional-retry-reserve",
            start_index=len(retry_reserves) + 1,
        )
    )
    retry_reserves.extend(provisional_retries)
    retry_reserve_total = authoritative_retry_cost + provisional_retry_cost

    observed_attempts, observed_spend = calculate_observed_attempts(
        getattr(args, "observed_attempt", []),
        getattr(args, "observed_spend_usd", None),
    )
    ceiling = parse_non_negative_decimal(args.ceiling_usd, "--ceiling-usd")
    if ceiling == 0:
        raise SystemExit("--ceiling-usd must be greater than zero.")

    reservations = []
    unsettled_reservation_total = Decimal("0")
    for value in args.reservation:
        name, amount_text = parse_pair(value)
        amount = parse_non_negative_decimal(amount_text, "--reservation")
        reservations.append(
            {
                "name": name,
                "amountUsd": budget_money_text(amount),
            }
        )
        unsettled_reservation_total += amount

    reserves_total = unsettled_reservation_total + retry_reserve_total
    all_in_total = observed_spend + projected_wave_cost + reserves_total
    remaining = ceiling - all_in_total
    allowed = all_in_total < ceiling

    return {
        "schemaVersion": 1,
        "ceilingUsd": budget_money_text(ceiling),
        "observedAttempts": observed_attempts,
        "observedSpendToDateUsd": budget_money_text(observed_spend),
        "candidates": candidates,
        "projectedWaveCostUsd": budget_money_text(projected_wave_cost),
        "unsettledReservations": reservations,
        "unsettledReservationTotalUsd": budget_money_text(
            unsettled_reservation_total
        ),
        "retryReserves": retry_reserves,
        "retryReserveTotalUsd": budget_money_text(retry_reserve_total),
        "reservesTotalUsd": budget_money_text(reserves_total),
        "allInProjectedTotalUsd": budget_money_text(all_in_total),
        "remainingUsd": budget_money_text(remaining),
        "strictlyInsideCeiling": allowed,
        "admissionErrors": [],
        "result": "allowed" if allowed else "blocked",
    }


def calculate_observed_attempts(
    values: list[str], legacy_observed_spend: Any
) -> tuple[list[dict[str, str]], Decimal]:
    if values and legacy_observed_spend is not None:
        raise SystemExit(
            "--observed-spend-usd cannot be combined with --observed-attempt; "
            "use named attempts only."
        )
    if legacy_observed_spend is not None:
        amount = parse_non_negative_decimal(
            legacy_observed_spend, "--observed-spend-usd"
        )
        return (
            [
                {
                    "name": "legacy-observed-spend-usd",
                    "amountUsd": budget_money_text(amount),
                }
            ],
            amount,
        )
    if not values:
        return [], Decimal("0")

    attempts = []
    seen_names: set[str] = set()
    total = Decimal("0")
    for value in values:
        name, amount_text = parse_pair(value)
        normalized_name = name.casefold()
        if normalized_name in seen_names:
            raise SystemExit(f"--observed-attempt name {name!r} was provided twice.")
        seen_names.add(normalized_name)
        amount = parse_non_negative_decimal(amount_text, "--observed-attempt")
        attempts.append({"name": name, "amountUsd": budget_money_text(amount)})
        total += amount
    return attempts, total


def calculate_authoritative_budget_entries(
    values: list[str], rows: list[dict[str, Any]], option_name: str
) -> tuple[list[dict[str, Any]], Decimal]:
    entries = []
    entries_total = Decimal("0")
    for index, value in enumerate(values, start=1):
        model, reasoning_effort, count = parse_budget_entry(value, option_name)
        row = lookup_base_estimate_row(rows, model, reasoning_effort)
        average = parse_non_negative_decimal(
            row.get("averageCostPerMatchPredictionUsd"),
            (
                "authoritative averageCostPerMatchPredictionUsd for "
                f"model={model!r}, reasoningEffort={reasoning_effort!r}"
            ),
        )
        total = average * Decimal(count)
        entries_total += total
        entries.append(
            {
                "entry": index,
                "model": model,
                "reasoningEffort": reasoning_effort,
                "matchPredictionCount": count,
                "averageCostPerMatchPredictionUsd": budget_money_text(average),
                "estimatedTotalCostUsd": budget_money_text(total),
                "estimateBasis": "authoritative-base-estimate",
                "baseEstimate": row,
            }
        )
    return entries, entries_total


def calculate_provisional_budget_entries(
    values: list[str], option_name: str, start_index: int
) -> tuple[list[dict[str, Any]], Decimal]:
    entries = []
    entries_total = Decimal("0")
    for offset, value in enumerate(values):
        path, count = parse_provisional_budget_entry(value, option_name)
        report, provenance = load_provisional_base_row_report(path, option_name)
        average = parse_positive_decimal(
            report.get("averageCostPerMatchPredictionUsd"),
            f"{option_name} averageCostPerMatchPredictionUsd in {path}",
        )
        total = average * Decimal(count)
        entries_total += total
        entries.append(
            {
                "entry": start_index + offset,
                "model": provenance["model"],
                "reasoningEffort": provenance["reasoningEffort"],
                "matchPredictionCount": count,
                "averageCostPerMatchPredictionUsd": budget_money_text(average),
                "estimatedTotalCostUsd": budget_money_text(total),
                "estimateBasis": "provisional-one-item-base-row-report",
                "provisionalReport": provenance,
            }
        )
    return entries, entries_total


def calculate_planned_preflight_entries(
    values: list[str], pricing_source: Path, start_index: int
) -> tuple[list[dict[str, Any]], Decimal]:
    if not values:
        return [], Decimal("0")

    pricing, pricing_provenance = load_hashed_pricing(pricing_source)
    entries = []
    entries_total = Decimal("0")
    seen_names: set[str] = set()
    for offset, value in enumerate(values):
        spec_path = Path(value)
        spec, spec_provenance = load_planned_preflight_spec(spec_path)
        name = require_nonempty_spec_text(spec, "name", spec_path)
        normalized_name = name.casefold()
        if normalized_name in seen_names:
            raise SystemExit(f"--planned-preflight name {name!r} was provided twice.")
        seen_names.add(normalized_name)

        model = require_nonempty_spec_text(spec, "model", spec_path)
        reasoning_effort = require_nonempty_spec_text(
            spec, "reasoningEffort", spec_path
        )
        service_tier = require_nonempty_spec_text(
            spec, "serviceTier", spec_path
        ).lower()
        if service_tier not in ("flex", "standard"):
            raise SystemExit(
                f"--planned-preflight spec {spec_path} has unsupported serviceTier "
                f"{service_tier!r}; expected 'flex' or 'standard'."
            )
        input_token_bound = require_positive_spec_integer(
            spec, "inputTokenBound", spec_path
        )
        if input_token_bound > SHORT_CONTEXT_INPUT_TOKEN_LIMIT:
            raise SystemExit(
                f"--planned-preflight spec {spec_path} inputTokenBound "
                f"{input_token_bound} exceeds the repository short-context pricing "
                f"limit {SHORT_CONTEXT_INPUT_TOKEN_LIMIT}."
            )
        max_output_tokens = require_positive_spec_integer(
            spec, "maxOutputTokens", spec_path
        )
        bound_evidence = require_nonempty_spec_text(
            spec, "boundEvidence", spec_path
        )
        source = require_nonempty_spec_text(spec, "source", spec_path)

        model_pricing = pricing.get(model)
        if model_pricing is None:
            raise SystemExit(
                f"--planned-preflight spec {spec_path} references unknown pricing "
                f"model {model!r}."
            )
        effective_input_price = input_price_for_tier(model_pricing, service_tier)
        effective_output_price = output_price_for_tier(model_pricing, service_tier)
        input_cost = cost_for_tokens(input_token_bound, effective_input_price)
        output_cost = cost_for_tokens(max_output_tokens, effective_output_price)
        total = input_cost + output_cost
        entries_total += total

        entries.append(
            {
                "entry": start_index + offset,
                "name": name,
                "model": model,
                "reasoningEffort": reasoning_effort,
                "matchPredictionCount": 1,
                "inputTokenBound": input_token_bound,
                "maxOutputTokens": max_output_tokens,
                "serviceTier": service_tier,
                "appliedPriceMultiplier": decimal_text(
                    FLEX_PRICE_MULTIPLIER
                    if service_tier == "flex"
                    else Decimal("1")
                ),
                "boundEvidence": bound_evidence,
                "source": source,
                "standardInputPricePerMillionUsd": decimal_text(
                    model_pricing.input_price
                ),
                "standardOutputPricePerMillionUsd": decimal_text(
                    model_pricing.output_price
                ),
                "effectiveInputPricePerMillionUsd": decimal_text(
                    effective_input_price
                ),
                "effectiveOutputPricePerMillionUsd": decimal_text(
                    effective_output_price
                ),
                "estimatedUncachedInputCostUsd": budget_money_text(input_cost),
                "estimatedFullCapOutputCostUsd": budget_money_text(output_cost),
                "estimatedTotalCostUsd": budget_money_text(total),
                "shortContextInputTokenLimit": SHORT_CONTEXT_INPUT_TOKEN_LIMIT,
                "estimateBasis": "planned-preflight-conservative-full-cap",
                "plannedPreflightSpec": spec_provenance,
                "pricingSource": pricing_provenance,
            }
        )
    return entries, entries_total


def load_planned_preflight_spec(
    path: Path,
) -> tuple[dict[str, Any], dict[str, str]]:
    try:
        source_bytes = path.read_bytes()
    except OSError as ex:
        raise SystemExit(f"--planned-preflight could not read {path}: {ex}") from ex
    try:
        spec = json.loads(source_bytes)
    except (json.JSONDecodeError, UnicodeDecodeError) as ex:
        raise SystemExit(f"--planned-preflight spec {path} is not valid JSON.") from ex
    if not isinstance(spec, dict):
        raise SystemExit(
            f"--planned-preflight spec {path} must contain a JSON object."
        )
    return spec, {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(source_bytes).hexdigest(),
    }


def require_nonempty_spec_text(
    spec: dict[str, Any], field: str, path: Path
) -> str:
    value = spec.get(field)
    if not isinstance(value, str) or not value.strip():
        raise SystemExit(
            f"--planned-preflight spec {path} must have nonempty string field "
            f"{field!r}."
        )
    return value.strip()


def require_positive_spec_integer(
    spec: dict[str, Any], field: str, path: Path
) -> int:
    value = spec.get(field)
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise SystemExit(
            f"--planned-preflight spec {path} must have positive integer field "
            f"{field!r}."
        )
    return value


def load_hashed_pricing(
    path: Path,
) -> tuple[dict[str, ModelPricing], dict[str, str]]:
    try:
        source_bytes = path.read_bytes()
    except OSError as ex:
        raise SystemExit(f"Could not read pricing source {path}: {ex}") from ex
    try:
        source = source_bytes.decode("utf-8")
    except UnicodeDecodeError as ex:
        raise SystemExit(f"Pricing source {path} is not valid UTF-8.") from ex
    try:
        pricing = parse_pricing_source(source, path)
    except (InvalidOperation, RuntimeError, ValueError) as ex:
        raise SystemExit(f"Could not parse pricing source {path}: {ex}") from ex
    return pricing, {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(source_bytes).hexdigest(),
    }


def parse_provisional_budget_entry(value: str, option_name: str) -> tuple[Path, int]:
    if "," not in value:
        raise SystemExit(f"{option_name} must be REPORT_JSON,COUNT, got {value!r}.")
    path_text, count_text = (part.strip() for part in value.rsplit(",", 1))
    if not path_text or not count_text:
        raise SystemExit(f"{option_name} must be REPORT_JSON,COUNT, got {value!r}.")
    return Path(path_text), parse_positive_count(count_text, option_name)


def load_provisional_base_row_report(
    path: Path, option_name: str
) -> tuple[dict[str, Any], dict[str, Any]]:
    try:
        source_bytes = path.read_bytes()
    except OSError as ex:
        raise SystemExit(f"{option_name} could not read {path}: {ex}") from ex
    try:
        report = json.loads(source_bytes)
    except (json.JSONDecodeError, UnicodeDecodeError) as ex:
        raise SystemExit(f"{option_name} report {path} is not valid JSON.") from ex
    if not isinstance(report, dict):
        raise SystemExit(f"{option_name} report {path} must contain a JSON object.")

    observations = report.get("baseSampleObservations")
    if (
        isinstance(observations, bool)
        or not isinstance(observations, int)
        or observations != 1
    ):
        raise SystemExit(
            f"{option_name} report {path} must have baseSampleObservations=1."
        )

    required_text_fields = (
        "model",
        "reasoningEffort",
        "promptRoute",
        "modelKnowledgeCutoffDate",
        "samplingCutoffUsed",
        "serviceTier",
        "source",
    )
    required_text = {
        field: require_nonempty_report_text(report, field, path, option_name)
        for field in required_text_fields
    }

    max_output_tokens = report.get("maxOutputTokens")
    if (
        isinstance(max_output_tokens, bool)
        or not isinstance(max_output_tokens, int)
        or max_output_tokens < 1
    ):
        raise SystemExit(
            f"{option_name} report {path} must have a positive integer "
            "maxOutputTokens."
        )

    average = parse_positive_decimal(
        report.get("averageCostPerMatchPredictionUsd"),
        f"{option_name} averageCostPerMatchPredictionUsd in {path}",
    )
    estimated_total = parse_positive_decimal(
        report.get("estimatedTotalCostUsd"),
        f"{option_name} estimatedTotalCostUsd in {path}",
    )
    if estimated_total != average:
        raise SystemExit(
            f"{option_name} report {path} has one observation but its "
            "estimatedTotalCostUsd differs from averageCostPerMatchPredictionUsd."
        )
    validate_optional_provisional_dataset_provenance(report, path, option_name)

    provenance = {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(source_bytes).hexdigest(),
        "model": required_text["model"],
        "reasoningEffort": required_text["reasoningEffort"],
        "maxOutputTokens": max_output_tokens,
        "source": required_text["source"],
        "baseSampleObservations": 1,
        "promptRoute": required_text["promptRoute"],
        "modelKnowledgeCutoffDate": required_text["modelKnowledgeCutoffDate"],
        "samplingCutoffUsed": required_text["samplingCutoffUsed"],
        "serviceTier": required_text["serviceTier"],
    }
    for field in (
        "datasetId",
        "datasetRunId",
        "datasetRunName",
        "preparedManifestSha256",
        "preparedManifestSampleSize",
    ):
        if field in report:
            provenance[field] = report[field]
    return report, provenance


def require_nonempty_report_text(
    report: dict[str, Any], field: str, path: Path, option_name: str
) -> str:
    value = report.get(field)
    if not isinstance(value, str) or not value.strip():
        raise SystemExit(
            f"{option_name} report {path} must have nonempty string field {field!r}."
        )
    return value.strip()


def validate_optional_provisional_dataset_provenance(
    report: dict[str, Any], path: Path, option_name: str
) -> None:
    text_fields = (
        "datasetId",
        "datasetRunId",
        "datasetRunName",
        "preparedManifestSha256",
    )
    sample_size_field = "preparedManifestSampleSize"
    present = [field for field in text_fields if field in report]
    if sample_size_field in report:
        present.append(sample_size_field)
    if not present:
        return
    missing = [
        field
        for field in text_fields + (sample_size_field,)
        if field not in report
    ]
    if missing:
        raise SystemExit(
            f"{option_name} report {path} has partial dataset provenance; "
            f"missing {', '.join(missing)}."
        )
    for field in text_fields:
        require_nonempty_report_text(report, field, path, option_name)
    manifest_hash = str(report["preparedManifestSha256"])
    if not re.fullmatch(r"[0-9a-f]{64}", manifest_hash):
        raise SystemExit(
            f"{option_name} report {path} has invalid preparedManifestSha256."
        )
    sample_size = report[sample_size_field]
    if (
        isinstance(sample_size, bool)
        or not isinstance(sample_size, int)
        or sample_size != 1
    ):
        raise SystemExit(
            f"{option_name} report {path} must have "
            "preparedManifestSampleSize=1 when dataset provenance is present."
        )


def parse_budget_entry(value: str, option_name: str) -> tuple[str, str, int]:
    parts = [part.strip() for part in value.split(",")]
    if len(parts) != 3 or not parts[0] or not parts[1] or not parts[2]:
        raise SystemExit(
            f"{option_name} must be MODEL,REASONING_EFFORT,COUNT, got {value!r}."
        )
    return parts[0], parts[1], parse_positive_count(parts[2], option_name)


def parse_positive_count(value: str, option_name: str) -> int:
    try:
        count = int(value)
    except ValueError as ex:
        raise SystemExit(
            f"{option_name} count must be an integer, got {value!r}."
        ) from ex
    if count < 1:
        raise SystemExit(f"{option_name} count must be at least 1, got {count}.")
    return count


def parse_non_negative_decimal(value: Any, field_name: str) -> Decimal:
    try:
        parsed = Decimal(str(value))
    except (InvalidOperation, ValueError) as ex:
        raise SystemExit(f"{field_name} must be a valid decimal USD amount.") from ex
    if not parsed.is_finite() or parsed < 0:
        raise SystemExit(f"{field_name} must be a finite non-negative USD amount.")
    return Decimal("0") if parsed == 0 else parsed


def parse_positive_decimal(value: Any, field_name: str) -> Decimal:
    parsed = parse_non_negative_decimal(value, field_name)
    if parsed == 0:
        raise SystemExit(f"{field_name} must be greater than zero.")
    return parsed


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
        f"serviceTier={report['serviceTier']}, "
        f"maxOutputTokens={report['maxOutputTokens']}, "
        f"averageCostPerMatch=${report['averageCostPerMatchPredictionUsd']}"
    )
    if "nonFlexRetryCount" in report:
        print(
            "Service tiers: "
            f"observed={report.get('observedServiceTierCounts', {})}, "
            f"nonFlexRequests={report.get('nonFlexRequestCount', 0)} "
            f"({report.get('nonFlexRequestPercent', '0%')}), "
            f"nonFlexRetryRequests={report.get('nonFlexRetryCount', 0)} "
            f"({report.get('nonFlexRetryPercent', '0%')})"
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
        f"Service tier: {row.get('serviceTier', 'unknown')} | "
        f"Max output tokens: {row['maxOutputTokens']} | "
        f"Model knowledge cutoff: {row['modelKnowledgeCutoffDate']} | "
        f"Sampling cutoff: {row['samplingCutoffUsed']}"
    )
    if "nonFlexRetryCount" in row:
        print(
            "Observed service tiers: "
            f"{row.get('observedServiceTierCounts', {})} | "
            f"Non-flex retry requests: {row.get('nonFlexRetryCount', 0)} "
            f"({row.get('nonFlexRetryPercent', '0%')})"
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


def emit_budget_gate(report: dict[str, Any]) -> None:
    print("Cumulative experiment budget gate:")
    print(f"Ceiling: ${report['ceilingUsd']}")
    print("Observed attempts:")
    if report["observedAttempts"]:
        for index, attempt in enumerate(report["observedAttempts"], start=1):
            print(f"{index}. {attempt['name']} | ${attempt['amountUsd']}")
    else:
        print("none")
    print(f"Observed spend to date: ${report['observedSpendToDateUsd']}")
    print()
    print("Candidate wave estimates:")
    for entry in report["candidates"]:
        if entry["estimateBasis"] == "planned-preflight-conservative-full-cap":
            print(
                f"{entry['entry']}. {entry['name']} | {entry['model']} "
                f"{entry['reasoningEffort']} | N=1 | "
                f"inputBound={entry['inputTokenBound']} | "
                f"outputCap={entry['maxOutputTokens']} | "
                f"estimated=${entry['estimatedTotalCostUsd']} | "
                f"basis={entry['estimateBasis']}"
            )
        else:
            print(
                f"{entry['entry']}. {entry['model']} {entry['reasoningEffort']} | "
                f"N={entry['matchPredictionCount']} | "
                f"average=${entry['averageCostPerMatchPredictionUsd']} | "
                f"estimated=${entry['estimatedTotalCostUsd']} | "
                f"basis={entry['estimateBasis']}"
            )
    print(f"Projected wave cost: ${report['projectedWaveCostUsd']}")
    print()
    print("Unsettled reservations:")
    if report["unsettledReservations"]:
        for index, reservation in enumerate(
            report["unsettledReservations"], start=1
        ):
            print(f"{index}. {reservation['name']} | ${reservation['amountUsd']}")
    else:
        print("none")
    print(
        "Unsettled reservation total: "
        f"${report['unsettledReservationTotalUsd']}"
    )
    print()
    print("Retry reserves:")
    if report["retryReserves"]:
        for entry in report["retryReserves"]:
            print(
                f"{entry['entry']}. {entry['model']} {entry['reasoningEffort']} | "
                f"N={entry['matchPredictionCount']} | "
                f"average=${entry['averageCostPerMatchPredictionUsd']} | "
                f"estimated=${entry['estimatedTotalCostUsd']} | "
                f"basis={entry['estimateBasis']}"
            )
    else:
        print("none")
    print(f"Retry reserve total: ${report['retryReserveTotalUsd']}")
    print(f"All reserves: ${report['reservesTotalUsd']}")
    print()
    print(f"All-in projected total: ${report['allInProjectedTotalUsd']}")
    print(f"Remaining before ceiling: ${report['remainingUsd']}")
    for error in report.get("admissionErrors", []):
        print(f"Admission error: {error}")
    if report.get("admissionErrors"):
        reason = "requested admission evidence was not persisted"
    else:
        reason = "all-in projected total must be strictly less than the ceiling"
    print(f"Result: {report['result'].upper()} ({reason})")


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


def budget_money_text(value: Decimal) -> str:
    text = format(value, "f")
    if "." not in text:
        return text + ".000000000000"
    whole, fractional = text.split(".", 1)
    return whole + "." + fractional.ljust(12, "0")


def rate_text(numerator: int, denominator: int) -> str:
    if denominator <= 0:
        return "0"
    return format(
        (Decimal(numerator) / Decimal(denominator)).quantize(
            Decimal("0.0001"),
            rounding=ROUND_HALF_UP,
        ),
        "f",
    )


def percent_text(numerator: int, denominator: int) -> str:
    if denominator <= 0:
        return "0.00%"
    percent = (Decimal(numerator) / Decimal(denominator)) * Decimal("100")
    return (
        format(
            percent.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP),
            "f",
        )
        + "%"
    )


def decimal_text(value: Decimal) -> str:
    return format(value.normalize(), "f")


def decimal_or_text(value: Decimal | str) -> str:
    return value if isinstance(value, str) else decimal_text(value)


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
