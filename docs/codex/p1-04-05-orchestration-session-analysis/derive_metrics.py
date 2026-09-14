#!/usr/bin/env python3
"""Derive report-specific aggregates without publishing transcript text."""

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


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def zero_usage() -> dict[str, int]:
    return {key: 0 for key in USAGE_KEYS}


def add_usage(target: dict[str, int], source: dict[str, Any]) -> None:
    for key in USAGE_KEYS:
        target[key] += int(source.get(key) or 0)


def median(values: list[float]) -> float | None:
    return round(statistics.median(values), 3) if values else None


def git(repo: pathlib.Path, *args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=repo, check=True, capture_output=True,
        text=True, encoding="utf-8", errors="replace",
    ).stdout.strip()


def role_for(agent_path: str) -> str:
    value = agent_path.lower()
    if "writer" in value:
        return "writer"
    if re.search(r"architecture|_arch", value):
        return "architecture"
    if "diagnosis" in value:
        return "diagnosis"
    if re.search(r"review|acceptance|_spec", value):
        return "review-or-specification"
    if re.search(r"_ci$|validation|verifier", value):
        return "validation-or-ci"
    return "other"


def aggregate_agents(analysis: dict[str, Any]) -> dict[str, Any]:
    agents = [thread for thread in analysis["threads"] if thread["kind"] == "subagent"]
    roles: dict[str, dict[str, Any]] = collections.defaultdict(
        lambda: {"threads": 0, "turns": 0, "active_seconds": 0.0,
                 "total_tokens": 0, "api_cost_equivalent_usd": 0.0}
    )
    groups: dict[str, dict[str, Any]] = collections.defaultdict(
        lambda: {"threads": 0, "turns": 0, "active_seconds": 0.0,
                 "api_cost_equivalent_usd": 0.0}
    )
    for agent in agents:
        for bucket, key in ((roles, role_for(agent["agent_path"])),
                            (groups, agent["task_group"])):
            bucket[key]["threads"] += 1
            bucket[key]["turns"] += agent["turn_count"]
            bucket[key]["active_seconds"] += agent["active_seconds"]
            bucket[key]["api_cost_equivalent_usd"] += agent["api_cost_equivalent_usd"] or 0
        roles[role_for(agent["agent_path"])]["total_tokens"] += agent["usage"]["total_tokens"]
    for bucket in (roles, groups):
        for row in bucket.values():
            row["active_seconds"] = round(row["active_seconds"], 3)
            row["api_cost_equivalent_usd"] = round(row["api_cost_equivalent_usd"], 6)
    return {
        "by_role": dict(sorted(roles.items())),
        "by_task_group": dict(sorted(groups.items())),
        "multi_turn_threads": sum(agent["turn_count"] > 1 for agent in agents),
        "followup_turns": sum(agent["turn_count"] for agent in agents) - len(agents),
        "maximum_turns_on_one_thread": max(agent["turn_count"] for agent in agents),
        "threads_over_four_turns": sum(agent["turn_count"] > 4 for agent in agents),
    }


def extract_root_metrics(root_log: pathlib.Path, cutoff: datetime,
                         adjustment_at: datetime) -> dict[str, Any]:
    calls: dict[str, dict[str, Any]] = {}
    actions: list[str] = []
    previous_total: dict[str, int] | None = None
    wait_usage = zero_usage()
    pure_wait_usage = zero_usage()
    wait_response_count = 0
    pure_wait_response_count = 0
    operational_counts: collections.Counter[str] = collections.Counter()

    with root_log.open("rb") as handle:
        for raw in handle:
            record = json.loads(raw.decode("utf-8"))
            timestamp = parse_time(record["timestamp"])
            if timestamp > cutoff:
                continue
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
                name = payload.get("name", "unknown")
                actions.append(name)
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

    def period(selected: list[dict[str, Any]]) -> dict[str, Any]:
        requested = [call["requested_timeout_ms"] / 1000 for call in selected
                     if isinstance(call.get("requested_timeout_ms"), (int, float))]
        durations = [call["duration_seconds"] for call in selected]
        distribution = collections.Counter(
            str(int(call["requested_timeout_ms"] / 1000))
            for call in selected if isinstance(call.get("requested_timeout_ms"), (int, float))
        )
        return {
            "completed_calls": len(selected),
            "timed_out_calls": sum(call.get("timed_out", False) for call in selected),
            "early_return_calls": sum(not call.get("timed_out", False) for call in selected),
            "median_requested_timeout_seconds": median(requested),
            "median_actual_wait_seconds": median(durations),
            "actual_wait_seconds": round(sum(durations), 3),
            "requested_timeout_distribution_seconds": dict(sorted(distribution.items(), key=lambda item: int(item[0]))),
        }

    before = [call for call in rows if call["called_at"] < adjustment_at]
    after = [call for call in rows if call["called_at"] >= adjustment_at]
    return {
        "wait_agent": {
            "all": period(rows),
            "before_owner_adjustment": period(before),
            "after_owner_adjustment": period(after),
            "owner_adjustment_at": adjustment_at.isoformat().replace("+00:00", "Z"),
            "responses_with_wait_action": wait_response_count,
            "usage_for_responses_with_wait_action": wait_usage,
            "pure_wait_responses": pure_wait_response_count,
            "usage_for_pure_wait_responses": pure_wait_usage,
            "usage_attribution_note": "Usage is the cumulative-counter delta for responses containing a wait action; mixed-action responses are included in the broad total and excluded from pure-wait totals.",
        },
        "root_operational_command_counts": dict(sorted(operational_counts.items())),
    }


def diff_metrics(repo: pathlib.Path, base: str, final: str, main_tip: str) -> dict[str, Any]:
    def numstat(left: str, right: str) -> dict[str, int]:
        rows = git(repo, "diff", "--numstat", f"{left}..{right}").splitlines()
        insertions = deletions = 0
        for row in rows:
            added, removed, _ = row.split("\t", 2)
            if added.isdigit():
                insertions += int(added)
            if removed.isdigit():
                deletions += int(removed)
        return {"files": len(rows), "insertions": insertions, "deletions": deletions}
    return {
        "complete_session_range": numstat(base, final),
        "integrated_main_range": numstat(base, main_tip),
        "p1_04_draft_pr_range": numstat(main_tip, final),
        "first_parent_commits": int(git(repo, "rev-list", "--count", f"{base}..{final}")),
    }


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
    adjustment = next(
        parse_time(row["timestamp"]) for row in analysis["user_messages"]
        if row.get("intervention_kind") == "process-adjustment"
    )
    result = {
        "schema_version": 1,
        "source_analysis_generated_at": analysis["generated_at"],
        "session_boundary": config["repository"],
        "root": extract_root_metrics(candidates[0], parse_time(config["event_cutoff_at"]), adjustment),
        "subagents": aggregate_agents(analysis),
        "git": diff_metrics(args.repo, config["repository"]["base_commit"],
                            config["repository"]["final_commit"],
                            "e8d05e6cb3233d28200143422296f01b141b2536"),
    }
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
