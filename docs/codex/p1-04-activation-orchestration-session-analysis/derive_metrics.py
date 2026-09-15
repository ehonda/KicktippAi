#!/usr/bin/env python3
"""Derive report-specific metrics without publishing transcript message text."""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import statistics
import subprocess
from datetime import datetime
from typing import Any


USAGE_KEYS = (
    "input_tokens",
    "cached_input_tokens",
    "cache_write_input_tokens",
    "output_tokens",
    "reasoning_output_tokens",
    "total_tokens",
)


EXPECTED_OUTCOMES = {
    "/root/p1_preview_audit": ["audited"],
    "/root/c3_writer": ["delivered", "delivered", "delivered", "delivered"],
    "/root/c3_implementation_review": ["rejected", "rejected", "rejected"],
    "/root/c3_review_diagnosis": ["diagnosed"],
    "/root/c3_implementation_review_fresh": ["accepted"],
    "/root/c3_cumulative_validation": ["failed"],
    "/root/c3_cumulative_failure_diagnosis": ["diagnosed"],
    "/root/c3_reslice_spec_review": ["rejected"],
    "/root/c3_reslice_spec_review_v2": ["accepted"],
    "/root/c3_firebase_writer": ["delivered", "delivered", "delivered"],
    "/root/c3_firebase_review": ["rejected", "accepted", "accepted"],
    "/root/c3_coordinator_writer": ["delivered", "delivered", "delivered"],
    "/root/c3_coordinator_review": ["rejected", "accepted", "accepted"],
    "/root/c3_handoff_writer": ["delivered"],
    "/root/c3_handoff_review": ["accepted"],
    "/root/c3_cumulative_validation_v2": ["passed"],
    "/root/c3_final_acceptance": ["rejected"],
    "/root/c3_cumulative_validation_v3": ["passed"],
    "/root/c3_final_acceptance_v2": ["rejected"],
    "/root/c3_cumulative_validation_v4": ["passed"],
    "/root/c3_final_acceptance_v3": ["accepted"],
    "/root/c3_ci_monitor": ["passed"],
    "/root/e1_writer": ["delivered"],
    "/root/e1_reviewer": ["accepted"],
    "/root/e1_cumulative_validation": ["failed", "passed"],
    "/root/e1_final_acceptance": ["accepted"],
    "/root/e1_ci_monitor": ["passed"],
    "/root/closeout_writer": ["delivered"],
    "/root/closeout_reviewer": ["accepted"],
    "/root/closeout_ci_monitor": ["passed"],
    "/root/p1_04_05_review_brief": ["delivered"],
}


HEAVY_OBSERVATIONS = [
    {
        "label": "C3 cumulative validation 1",
        "agent_path": "/root/c3_cumulative_validation",
        "final_ordinal": 0,
        "outcome": "failed-tests",
        "started_available_gib": 3.427,
        "minimum_available_gib": 2.109,
        "finished_available_gib": 2.398,
        "queue_delay_seconds": 0,
        "memory_symptoms": "one transient paging sample; no OOM or termination",
        "expected_fragments": ["2.109", "queue delay 0s"],
    },
    {
        "label": "C3 handoff slice",
        "agent_path": "/root/c3_handoff_writer",
        "final_ordinal": 0,
        "outcome": "passed",
        "started_available_gib": 2.268,
        "minimum_available_gib": 1.142,
        "finished_available_gib": 1.577,
        "queue_delay_seconds": 0,
        "memory_symptoms": "three transient paging spikes; no OOM or termination",
        "expected_fragments": ["1.142", "queue delay 0s"],
    },
    {
        "label": "C3 cumulative validation 2",
        "agent_path": "/root/c3_cumulative_validation_v2",
        "final_ordinal": 0,
        "outcome": "passed",
        "started_available_gib": 2.797,
        "minimum_available_gib": 1.324,
        "finished_available_gib": 1.888,
        "queue_delay_seconds": 0,
        "memory_symptoms": "two transient paging spikes; no sustained paging, OOM, or termination",
        "expected_fragments": ["1.324", "queue delay: 0 seconds"],
    },
    {
        "label": "C3 cumulative validation 3",
        "agent_path": "/root/c3_cumulative_validation_v3",
        "final_ordinal": 0,
        "outcome": "passed",
        "started_available_gib": 2.500,
        "minimum_available_gib": 1.187,
        "finished_available_gib": 1.836,
        "queue_delay_seconds": 0,
        "memory_symptoms": "healthy paging; no resource symptoms",
        "expected_fragments": ["1.187", "queue delay: 0"],
    },
    {
        "label": "C3 cumulative validation 4",
        "agent_path": "/root/c3_cumulative_validation_v4",
        "final_ordinal": 0,
        "outcome": "passed",
        "started_available_gib": 2.448,
        "minimum_available_gib": 1.101,
        "finished_available_gib": 1.529,
        "queue_delay_seconds": 0,
        "memory_symptoms": "transient paging spikes; no sustained pressure, OOM, or termination",
        "expected_fragments": ["1.101", "Queue delay: 0s"],
    },
    {
        "label": "E1 writer aggregate",
        "agent_path": "/root/e1_writer",
        "final_ordinal": 0,
        "outcome": "passed-with-missed-escalation",
        "started_available_gib": None,
        "minimum_available_gib": 0.444,
        "finished_available_gib": 1.455,
        "queue_delay_seconds": None,
        "memory_symptoms": "absolute-floor breach discovered late; no OOM or abnormal termination",
        "expected_fragments": ["0.444", "missed"],
    },
    {
        "label": "E1 cumulative validation 1",
        "agent_path": "/root/e1_cumulative_validation",
        "final_ordinal": 0,
        "outcome": "resource-stop",
        "started_available_gib": 2.574,
        "minimum_available_gib": 1.049,
        "finished_available_gib": 1.290,
        "queue_delay_seconds": 0,
        "memory_symptoms": "severe sustained paging; stopped before suite completion; no OOM",
        "expected_fragments": ["1.049", "severe sustained paging"],
    },
    {
        "label": "E1 cumulative validation reserved retry",
        "agent_path": "/root/e1_cumulative_validation",
        "final_ordinal": 1,
        "outcome": "passed",
        "started_available_gib": 2.282,
        "minimum_available_gib": 1.565,
        "finished_available_gib": 1.985,
        "queue_delay_seconds": None,
        "memory_symptoms": "one isolated paging spike; no sustained pressure, OOM, or termination",
        "expected_fragments": ["1.565", "Reserved retry passed"],
    },
]


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def median(values: list[float]) -> float | None:
    return round(statistics.median(values), 3) if values else None


def zero_usage() -> dict[str, int]:
    return {key: 0 for key in USAGE_KEYS}


def add_usage(target: dict[str, int], source: dict[str, Any]) -> None:
    for key in USAGE_KEYS:
        target[key] += int(source.get(key) or 0)


def git(repo: pathlib.Path, *args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=repo, check=True, capture_output=True,
        text=True, encoding="utf-8", errors="replace",
    ).stdout.strip()


def read_records(root_log: pathlib.Path, cutoff: datetime) -> list[dict[str, Any]]:
    records = []
    with root_log.open("rb") as handle:
        for raw in handle:
            record = json.loads(raw.decode("utf-8"))
            if parse_time(record["timestamp"]) <= cutoff:
                records.append(record)
    return records


def extract_root_metrics(records: list[dict[str, Any]]) -> dict[str, Any]:
    calls: dict[str, dict[str, Any]] = {}
    actions: list[str] = []
    previous_total: dict[str, int] | None = None
    wait_usage = zero_usage()
    pure_wait_usage = zero_usage()
    wait_response_count = 0
    pure_wait_response_count = 0
    operational_counts: collections.Counter[str] = collections.Counter()

    for record in records:
        timestamp = parse_time(record["timestamp"])
        payload = record.get("payload", {})
        if record["type"] == "response_item" and payload.get("type") == "function_call":
            name = payload.get("name", "unknown")
            actions.append(name)
            if name == "wait_agent":
                arguments = json.loads(payload.get("arguments") or "{}")
                calls[payload["call_id"]] = {
                    "called_at": timestamp,
                    "requested_timeout_ms": arguments.get("timeout_ms"),
                }
        elif record["type"] == "response_item" and payload.get("type") == "custom_tool_call":
            actions.append(payload.get("name", "unknown"))
            source = payload.get("input") or ""
            for pattern, label in (
                (r"Get-OrchestrationRecoverySnapshot\.ps1", "recovery_snapshot_execs"),
                (r"Invoke-OrchestrationCapsuleHook\.ps1", "capsule_hook_execs"),
                (r"New-OrchestrationRecoveryManifest\.ps1", "manifest_builder_execs"),
                (r"Get-OrchestrationResourceSnapshot\.ps1", "resource_snapshot_execs"),
                (r"tools\.apply_patch\s*\(", "nested_apply_patch_mentions"),
            ):
                operational_counts[label] += len(re.findall(pattern, source))
        elif record["type"] == "response_item" and payload.get("type") == "function_call_output":
            call = calls.get(payload.get("call_id"))
            if call is not None:
                call["returned_at"] = timestamp
                output = payload.get("output")
                try:
                    parsed = json.loads(output) if isinstance(output, str) else output
                except json.JSONDecodeError:
                    parsed = {}
                call["timed_out"] = bool(parsed.get("timed_out")) if isinstance(parsed, dict) else False
                call["message"] = parsed.get("message") if isinstance(parsed, dict) else None
        elif record["type"] == "event_msg" and payload.get("type") == "token_count":
            total = (payload.get("info") or {}).get("total_token_usage")
            if total:
                normalized = {key: int(total.get(key) or 0) for key in USAGE_KEYS}
                if previous_total is None or normalized["total_tokens"] < previous_total["total_tokens"]:
                    delta = normalized
                else:
                    delta = {key: normalized[key] - previous_total[key] for key in USAGE_KEYS}
                if "wait_agent" in actions:
                    add_usage(wait_usage, delta)
                    wait_response_count += 1
                    if set(actions) == {"wait_agent"}:
                        add_usage(pure_wait_usage, delta)
                        pure_wait_response_count += 1
                previous_total = normalized
            actions = []

    rows = [call for call in calls.values() if "returned_at" in call]
    for call in rows:
        call["duration_seconds"] = (call["returned_at"] - call["called_at"]).total_seconds()
    requested = [call["requested_timeout_ms"] / 1000 for call in rows
                 if isinstance(call.get("requested_timeout_ms"), (int, float))]
    durations = [call["duration_seconds"] for call in rows]
    distribution = collections.Counter(
        str(int(call["requested_timeout_ms"] / 1000)) for call in rows
        if isinstance(call.get("requested_timeout_ms"), (int, float))
    )
    by_timeout = {}
    for timeout in sorted({int(call["requested_timeout_ms"] / 1000) for call in rows
                           if isinstance(call.get("requested_timeout_ms"), (int, float))}):
        selected = [call for call in rows if int(call.get("requested_timeout_ms", 0) / 1000) == timeout]
        by_timeout[str(timeout)] = {
            "calls": len(selected),
            "timed_out_no_change_calls": sum(call.get("timed_out", False) for call in selected),
            "early_return_calls": sum(not call.get("timed_out", False) for call in selected),
            "median_actual_wait_seconds": median([call["duration_seconds"] for call in selected]),
        }
    messages = collections.Counter(call.get("message") or "unknown" for call in rows)
    return {
        "wait_agent": {
            "completed_calls": len(rows),
            "pending_calls_at_cutoff": len(calls) - len(rows),
            "timed_out_no_change_calls": sum(call.get("timed_out", False) for call in rows),
            "early_return_calls": sum(not call.get("timed_out", False) for call in rows),
            "actual_wait_seconds": round(sum(durations), 3),
            "median_requested_timeout_seconds": median(requested),
            "median_actual_wait_seconds": median(durations),
            "requested_timeout_distribution_seconds": dict(sorted(distribution.items(), key=lambda row: int(row[0]))),
            "by_requested_timeout_seconds": by_timeout,
            "result_messages": dict(sorted(messages.items())),
            "responses_with_wait_action": wait_response_count,
            "usage_for_responses_with_wait_action": wait_usage,
            "pure_wait_responses": pure_wait_response_count,
            "usage_for_pure_wait_responses": pure_wait_usage,
            "usage_attribution_note": "Cumulative usage delta for responses containing wait_agent; mixed-action responses are excluded from the pure-wait subtotal.",
        },
        "root_operational_command_counts": dict(sorted(operational_counts.items())),
    }


def extract_compactions(records: list[dict[str, Any]]) -> dict[str, Any]:
    rows = []
    for index, record in enumerate(records):
        if record["type"] != "compacted":
            continue
        timestamp = parse_time(record["timestamp"])
        previous_token = next((
            candidate for candidate in reversed(records[:index])
            if candidate["type"] == "event_msg" and candidate.get("payload", {}).get("type") == "token_count"
        ), None)
        next_token = next((
            candidate for candidate in records[index + 1:]
            if candidate["type"] == "event_msg" and candidate.get("payload", {}).get("type") == "token_count"
        ), None)
        next_call = next((
            candidate for candidate in records[index + 1:]
            if candidate["type"] == "response_item" and
            candidate.get("payload", {}).get("type") in {"custom_tool_call", "function_call"}
        ), None)
        completion = None
        if next_call is not None:
            call_id = next_call.get("payload", {}).get("call_id")
            completion = next((
                candidate for candidate in records[index + 1:]
                if candidate["type"] == "response_item" and
                candidate.get("payload", {}).get("type") in {"custom_tool_call_output", "function_call_output"} and
                candidate.get("payload", {}).get("call_id") == call_id
            ), None)
        source = (next_call or {}).get("payload", {}).get("input", "") or ""
        if "Get-OrchestrationRecoverySnapshot.ps1" in source:
            recovery_shape = "compact-recovery-snapshot"
        elif "rg --files .agents" in source:
            recovery_shape = "broad-orchestration-rediscovery"
        else:
            recovery_shape = "task-specific-broad-read"
        previous_info = (previous_token or {}).get("payload", {}).get("info", {}).get("last_token_usage", {})
        next_info = (next_token or {}).get("payload", {}).get("info", {}).get("last_token_usage", {})
        call_at = parse_time(next_call["timestamp"]) if next_call else None
        completed_at = parse_time(completion["timestamp"]) if completion else None
        rows.append({
            "compacted_at": record["timestamp"],
            "pre_compaction_input_tokens": previous_info.get("input_tokens"),
            "post_compaction_context_tokens": next_info.get("total_tokens"),
            "first_tool_call_at": next_call.get("timestamp") if next_call else None,
            "seconds_to_first_tool_call": round((call_at - timestamp).total_seconds(), 3) if call_at else None,
            "seconds_to_first_tool_result": round((completed_at - timestamp).total_seconds(), 3) if completed_at else None,
            "recovery_shape": recovery_shape,
        })
    return {
        "count": len(rows),
        "compact_snapshot_recoveries": sum(row["recovery_shape"] == "compact-recovery-snapshot" for row in rows),
        "broad_rediscoveries": sum(row["recovery_shape"] == "broad-orchestration-rediscovery" for row in rows),
        "median_seconds_to_first_tool_call": median([row["seconds_to_first_tool_call"] for row in rows]),
        "events": rows,
    }


def role_for(agent_path: str) -> str:
    value = agent_path.lower()
    if "writer" in value or "review_brief" in value:
        return "writer-or-artifact"
    if "diagnosis" in value:
        return "diagnosis"
    if "review" in value or "acceptance" in value or "audit" in value:
        return "review-or-specification"
    if "validation" in value or value.endswith("_ci_monitor"):
        return "validation-or-ci"
    return "other"


def phase_for(agent_path: str) -> str:
    if "p1_preview" in agent_path:
        return "preview"
    if "review_brief" in agent_path:
        return "review brief"
    if agent_path.startswith("/root/e1_"):
        return "E1 implementation"
    if agent_path.startswith("/root/closeout_"):
        return "documentation closeout"
    if any(value in agent_path for value in ("firebase_", "coordinator_", "handoff_")):
        return "C3 sliced remediation"
    if "cumulative" in agent_path or "final_acceptance" in agent_path or agent_path.endswith("ci_monitor"):
        return "C3 validation and acceptance"
    return "C3 initial correction"


def aggregate_agents(analysis: dict[str, Any]) -> dict[str, Any]:
    agents = [thread for thread in analysis["threads"] if thread["kind"] == "subagent"]
    roles: dict[str, dict[str, Any]] = collections.defaultdict(
        lambda: {"threads": 0, "turns": 0, "active_seconds": 0.0, "total_tokens": 0, "api_cost_equivalent_usd": 0.0}
    )
    phases: dict[str, dict[str, Any]] = collections.defaultdict(
        lambda: {"threads": 0, "turns": 0, "active_seconds": 0.0, "api_cost_equivalent_usd": 0.0}
    )
    for agent in agents:
        for bucket, key in ((roles, role_for(agent["agent_path"])), (phases, phase_for(agent["agent_path"]))):
            bucket[key]["threads"] += 1
            bucket[key]["turns"] += agent["turn_count"]
            bucket[key]["active_seconds"] += agent["active_seconds"]
            bucket[key]["api_cost_equivalent_usd"] += agent["api_cost_equivalent_usd"] or 0
        roles[role_for(agent["agent_path"])]["total_tokens"] += agent["usage"]["total_tokens"]
    for bucket in (roles, phases):
        for row in bucket.values():
            row["active_seconds"] = round(row["active_seconds"], 3)
            row["api_cost_equivalent_usd"] = round(row["api_cost_equivalent_usd"], 6)
    return {
        "by_role": dict(sorted(roles.items())),
        "by_phase": dict(sorted(phases.items())),
        "multi_turn_threads": sum(agent["turn_count"] > 1 for agent in agents),
        "followup_turns": sum(agent["turn_count"] for agent in agents) - len(agents),
        "maximum_turns_on_one_thread": max(agent["turn_count"] for agent in agents),
    }


def load_final_messages(
    analysis: dict[str, Any], sessions_dir: pathlib.Path, cutoff: datetime,
) -> dict[str, list[dict[str, Any]]]:
    result: dict[str, list[dict[str, Any]]] = {}
    for thread in analysis["threads"]:
        if thread["kind"] != "subagent":
            continue
        rows = []
        with (sessions_dir / thread["log_file"]).open("rb") as handle:
            for raw in handle:
                record = json.loads(raw.decode("utf-8"))
                if parse_time(record["timestamp"]) > cutoff:
                    continue
                payload = record.get("payload", {})
                if (
                    record["type"] == "response_item" and payload.get("type") == "message" and
                    payload.get("role") == "assistant" and payload.get("phase") == "final_answer"
                ):
                    text = "\n".join(
                        str(item.get("text", "")) for item in payload.get("content", [])
                        if isinstance(item, dict)
                    )
                    rows.append({"timestamp": record["timestamp"], "text": text})
        result[thread["agent_path"]] = rows
    return result


def derive_outcomes(analysis: dict[str, Any], messages: dict[str, list[dict[str, Any]]]) -> dict[str, Any]:
    actual_paths = set(messages)
    expected_paths = set(EXPECTED_OUTCOMES)
    if actual_paths != expected_paths:
        raise SystemExit(f"agent outcome paths drifted: missing={expected_paths - actual_paths}, extra={actual_paths - expected_paths}")
    turn_hashes = collections.defaultdict(list)
    for thread in analysis["threads"]:
        for turn in thread.get("turns", []):
            if thread["kind"] == "subagent":
                turn_hashes[thread["agent_path"]].append(turn.get("result_sha256"))
    rows = []
    counts: collections.Counter[str] = collections.Counter()
    for path, expected in EXPECTED_OUTCOMES.items():
        finals = messages[path]
        if len(finals) != len(expected):
            raise SystemExit(f"final message count drifted for {path}: expected {len(expected)}, got {len(finals)}")
        for index, (message, outcome) in enumerate(zip(finals, expected)):
            counts[outcome] += 1
            rows.append({
                "agent_path": path,
                "final_ordinal": index,
                "timestamp": message["timestamp"],
                "outcome_class": outcome,
                "result_sha256": turn_hashes[path][index],
            })
    return {"counts": dict(sorted(counts.items())), "turns": sorted(rows, key=lambda row: row["timestamp"])}


def verify_heavy_observations(messages: dict[str, list[dict[str, Any]]]) -> list[dict[str, Any]]:
    output = []
    for source in HEAVY_OBSERVATIONS:
        row = dict(source)
        expected = row.pop("expected_fragments")
        text = messages[row["agent_path"]][row["final_ordinal"]]
        for fragment in expected:
            if fragment.lower() not in text["text"].lower():
                raise SystemExit(f"heavy observation drifted: {row['label']} missing {fragment!r}")
        row["observed_at"] = text["timestamp"]
        output.append(row)
    return output


def aggregate_models(analysis: dict[str, Any]) -> list[dict[str, Any]]:
    rows: dict[tuple[str, str, str], dict[str, Any]] = collections.defaultdict(
        lambda: {"responses": 0, "total_tokens": 0, "api_cost_equivalent_usd": 0.0}
    )
    for thread in analysis["threads"]:
        for item in thread["model_effort_usage"]:
            key = (thread["kind"], item["model"], item["reasoning_effort"])
            rows[key]["responses"] += item["responses"]
            rows[key]["total_tokens"] += item["usage"]["total_tokens"]
            rows[key]["api_cost_equivalent_usd"] += item["api_cost_equivalent_usd"] or 0
    return [
        {
            "kind": key[0], "model": key[1], "reasoning_effort": key[2],
            "responses": value["responses"], "total_tokens": value["total_tokens"],
            "api_cost_equivalent_usd": round(value["api_cost_equivalent_usd"], 6),
        }
        for key, value in sorted(rows.items())
    ]


def diff_metrics(repo: pathlib.Path, base: str, final: str, session_started_at: str) -> dict[str, Any]:
    rows = git(repo, "diff", "--numstat", f"{base}..{final}").splitlines()
    insertions = deletions = 0
    for row in rows:
        added, removed, _ = row.split("\t", 2)
        if added.isdigit():
            insertions += int(added)
        if removed.isdigit():
            deletions += int(removed)
    commits = git(repo, "log", "--format=%H%x09%cI", f"{base}..{final}").splitlines()
    started = parse_time(session_started_at)
    authored_in_window = sum(parse_time(row.split("\t", 1)[1]) >= started for row in commits)
    return {
        "files": len(rows),
        "insertions": insertions,
        "deletions": deletions,
        "range_commits": len(commits),
        "committed_during_session_window": authored_in_window,
    }


def powershell_probe() -> dict[str, Any]:
    script = r'''
$json = '{"updated_at_utc":"2026-09-14T22:44:39.9720000Z"}'
$default = $json | ConvertFrom-Json
$stringKind = $json | ConvertFrom-Json -DateKind String
$parsedDefault = [DateTimeOffset]::MinValue
$parsedString = [DateTimeOffset]::MinValue
[pscustomobject]@{
  ps_version = $PSVersionTable.PSVersion.ToString()
  culture = [Globalization.CultureInfo]::CurrentCulture.Name
  default_type = $default.updated_at_utc.GetType().FullName
  default_string = [string]$default.updated_at_utc
  default_try_parse = [DateTimeOffset]::TryParse([string]$default.updated_at_utc,[ref]$parsedDefault)
  string_kind_type = $stringKind.updated_at_utc.GetType().FullName
  string_kind_value = [string]$stringKind.updated_at_utc
  string_kind_try_parse = [DateTimeOffset]::TryParse([string]$stringKind.updated_at_utc,[ref]$parsedString)
} | ConvertTo-Json -Compress
'''
    completed = subprocess.run(
        ["pwsh", "-NoProfile", "-Command", script], check=True,
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    return json.loads(completed.stdout)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=pathlib.Path, required=True)
    parser.add_argument("--analysis", type=pathlib.Path, required=True)
    parser.add_argument("--sessions-dir", type=pathlib.Path, required=True)
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    config = manifest["analysis"]
    candidates = list(args.sessions_dir.rglob(config["root_log_name"]))
    if len(candidates) != 1:
        raise SystemExit(f"expected one root log, found {len(candidates)}")
    cutoff = parse_time(config["event_cutoff_at"])
    records = read_records(candidates[0], cutoff)
    messages = load_final_messages(analysis, args.sessions_dir, cutoff)
    policy_path = args.repo / ".agents/skills/orchestrate/resources/resource-policy.json"
    policy = json.loads(policy_path.read_text(encoding="utf-8"))
    orchestrate_files = list((args.repo / ".agents/skills/orchestrate").rglob("*"))
    docker_mentions = 0
    for path in orchestrate_files:
        if path.is_file() and path.suffix.lower() in {".md", ".ps1", ".json", ".yml", ".yaml"}:
            docker_mentions += len(re.findall(r"\bdocker\b", path.read_text(encoding="utf-8", errors="replace"), flags=re.IGNORECASE))

    result = {
        "schema_version": 1,
        "source_analysis_generated_at": analysis["generated_at"],
        "session_boundary": config["repository"],
        "root": extract_root_metrics(records),
        "compactions": extract_compactions(records),
        "subagents": aggregate_agents(analysis),
        "coordination_outcomes": derive_outcomes(analysis, messages),
        "heavy_observations": verify_heavy_observations(messages),
        "model_usage": aggregate_models(analysis),
        "git": diff_metrics(
            args.repo, config["repository"]["base_commit"], config["repository"]["final_commit"],
            analysis["summary"]["session_started_at"],
        ),
        "policy": {
            "worktree": policy["worktree"],
            "heavy_operation": {
                "preferred_available_memory_floor_gib": policy["heavyOperation"]["preferredAvailableMemoryFloorGiB"],
                "absolute_available_memory_floor_gib": policy["heavyOperation"]["absoluteAvailableMemoryFloorGiB"],
                "warning_available_memory_gib": policy["heavyOperation"]["warningAvailableMemoryGiB"],
                "degraded_maximum_concurrent_profiles": policy["heavyOperation"]["degraded"]["maximumConcurrentProfiles"],
            },
            "docker_mentions_in_orchestrate_contract": docker_mentions,
        },
        "powershell_compatibility_probe": powershell_probe(),
    }
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
