#!/usr/bin/env python3
"""Derive workflow, cost, resource, review, and Git evidence for the frozen run."""

from __future__ import annotations

import argparse
import csv
import json
import pathlib
import re
import subprocess
from collections import Counter, defaultdict
from datetime import datetime, timedelta
from typing import Any


RUN_ID = "01a06e72-0c2f-76a3-8d2b-d48f9131a1d5"
BASE_COMMIT = "55491b2ec642666938eb2517eaa150cb3695d048"
FINAL_COMMIT = "538c30c53870faa608cf0d6e6a9dbf20f8d833d3"
BASELINE_DATA = pathlib.Path("docs/codex/p1-orchestration-follow-up-investigation/data")
OWNER_WAITS = [
    {
        "started_at": "2026-09-05T03:42:24.892Z",
        "ended_at": "2026-09-05T07:39:03.283Z",
        "reason": "direct production-dispatch approval",
    },
    {
        "started_at": "2026-09-05T10:11:47.914Z",
        "ended_at": "2026-09-05T10:34:10.309Z",
        "reason": "standing retry authority after a safe pre-generation failure",
    },
    {
        "started_at": "2026-09-05T12:21:57.303Z",
        "ended_at": "2026-09-05T13:26:05.973Z",
        "reason": "owner pause before resuming P1 prioritization and grilling",
    },
]
PATCH_TARGET = re.compile(r"\*\*\* (?:Add|Update|Delete) File: (.+?)(?:\\n|\r?\n)")
NESTED_TOOL = re.compile(r"\btools\.([A-Za-z0-9_]+)\s*\(")
WALL_TIME = re.compile(r"Wall time(?:_seconds)?[\s\":]+([0-9.]+)", re.IGNORECASE)
MEMORY = re.compile(r'"AvailableMemoryGiB"\s*:\s*([0-9.]+)', re.IGNORECASE)


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def write_csv(path: pathlib.Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows({field: row.get(field) for field in fields} for row in rows)


def git(repo: pathlib.Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", *arguments],
        cwd=repo,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    return result.stdout


def classify_role(path: str) -> str:
    if "spec_review" in path or "review" in path:
        return "review"
    if "architecture" in path:
        return "architecture"
    if any(word in path for word in ("source_repair", "implementation", "completion")):
        return "writer"
    if any(word in path for word in ("preflight", "watch", "inventory")):
        return "audit-monitor"
    return "other"


def classify_tool(source: str, nested: list[str]) -> str:
    lowered = source.lower()
    if "get-orchestrationresourcesnapshot.ps1" in lowered:
        return "resource-admission"
    # Patch bodies often quote validation commands. Classify the operation that
    # actually ran before inspecting command text embedded in the patch.
    if "tools.apply_patch" in source:
        return "patch"
    if "dotnet run" in lowered or "dotnet build" in lowered:
        return "dotnet"
    if "gh run" in lowered or "gh api" in lowered or "gh pr" in lowered:
        return "github"
    if any(token in lowered for token in ("git push", "git commit", "git worktree", "git merge")):
        return "git-release"
    if "git " in lowered:
        return "git-read"
    if any(token in lowered for token in ("get-content", "rg ", "get-childitem")):
        return "read-search"
    return "+".join(nested) if nested else "other"


def output_text(payload: dict[str, Any]) -> str:
    value = payload.get("output") or []
    if isinstance(value, list):
        return "\n".join(
            str(item.get("text", "")) for item in value if isinstance(item, dict)
        )
    return str(value)


def sanitize_local_path(value: str) -> str:
    home = str(pathlib.Path.home())
    sanitized = value.replace(home, "%USERPROFILE%")
    sanitized = sanitized.replace(home.replace("\\", "\\\\"), "%USERPROFILE%")
    sanitized = sanitized.replace(home.replace("\\", "/"), "%USERPROFILE%")
    return sanitized


def usage_cost(usage: dict[str, int], rates: dict[str, float]) -> float:
    cached = usage["cached_input_tokens"]
    writes = usage["cache_write_input_tokens"]
    uncached = usage["input_tokens"] - cached - writes
    return (
        uncached * rates["input"]
        + cached * rates["cached_input"]
        + writes * rates["cache_write_input"]
        + usage["output_tokens"] * rates["output"]
    ) / 1_000_000


def overlap_metrics(turns: list[dict[str, Any]]) -> dict[str, Any]:
    boundaries: list[tuple[datetime, int]] = []
    for turn in turns:
        if not turn.get("completed_at"):
            continue
        boundaries.append((parse_time(turn["started_at"]), 1))
        boundaries.append((parse_time(turn["completed_at"]), -1))
    boundaries.sort(key=lambda item: (item[0], item[1]))
    previous = None
    current = maximum = 0
    seconds = defaultdict(float)
    for timestamp, delta in boundaries:
        if previous is not None:
            seconds[current] += (timestamp - previous).total_seconds()
        current += delta
        maximum = max(maximum, current)
        previous = timestamp
    active = sum(value for key, value in seconds.items() if key > 0)
    two_plus = sum(value for key, value in seconds.items() if key >= 2)
    return {
        "maximum": maximum,
        "active_seconds": round(active, 3),
        "two_plus_seconds": round(two_plus, 3),
        "two_plus_share": round(two_plus / active, 4) if active else 0,
    }


def review_verdict(excerpt: str) -> str:
    start = excerpt.strip().lower().lstrip("*_`# ")
    if re.match(r"^(accept|approve|approved|no findings)", start):
        if "with-fix" in start or "bounded" in start[:100]:
            return "accept-with-fixes"
        return "accepted"
    if re.match(r"^(reject|not accepted|blocked|concrete blocker|one remaining blocker)", start):
        return "findings"
    return "other"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--analysis", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data" / "analysis.json")
    parser.add_argument("--output-dir", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data")
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3])
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex" / "sessions")
    args = parser.parse_args()

    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    cutoff = parse_time(analysis["source"]["event_cutoff_at"] or analysis["generated_at"])
    root = analysis["threads"][0]
    tool_rows: list[dict[str, Any]] = []
    resource_rows: list[dict[str, Any]] = []
    collaboration_outputs: list[dict[str, Any]] = []
    execution_markers: list[dict[str, Any]] = []

    for thread in analysis["threads"]:
        log = args.sessions_dir / thread["log_file"]
        thread_floor = (
            parse_time(thread["spawned_at"]) - timedelta(seconds=2)
            if thread["kind"] != "root" and thread.get("spawned_at")
            else None
        )
        custom_calls: dict[str, dict[str, Any]] = {}
        function_calls: dict[str, dict[str, Any]] = {}
        with log.open("r", encoding="utf-8") as handle:
            for line in handle:
                record = json.loads(line)
                timestamp = record.get("timestamp")
                if not timestamp or parse_time(timestamp) > cutoff:
                    continue
                if thread_floor is not None and parse_time(timestamp) < thread_floor:
                    continue
                if record.get("type") != "response_item":
                    continue
                payload = record.get("payload", {})
                subtype = payload.get("type")
                call_id = payload.get("call_id")
                if subtype == "message" and payload.get("role") == "assistant" and thread["kind"] == "root":
                    text = "\n".join(
                        str(item.get("text", ""))
                        for item in payload.get("content", [])
                        if isinstance(item, dict)
                    )
                    if "EXECUTION START" in text:
                        execution_markers.append({"timestamp": timestamp, "text": " ".join(text.split())[:400]})
                elif subtype == "custom_tool_call" and call_id:
                    source = payload.get("input") or ""
                    nested = NESTED_TOOL.findall(source)
                    targets = PATCH_TARGET.findall(source)
                    if not targets and "tools.apply_patch" in source:
                        targets = re.findall(r"(?:Add|Update|Delete) File: ([^\\\r\n]+)", source)
                    patch_class = ""
                    normalized = [target.replace("\\", "/").lower() for target in targets]
                    if any(f".tmp/orchestration/{RUN_ID}/state.md" in target for target in normalized):
                        patch_class = "orchestration-ledger"
                    elif any(f".tmp/orchestration/{RUN_ID}/preview.md" in target for target in normalized):
                        patch_class = "orchestration-preview"
                    elif targets:
                        patch_class = "repository-or-other"
                    custom_calls[call_id] = {
                        "thread_id": thread["thread_id"],
                        "agent_path": thread["agent_path"],
                        "role": "root" if thread["kind"] == "root" else classify_role(thread["agent_path"]),
                        "call_id": call_id,
                        "started_at": timestamp,
                        "nested_tools": ";".join(nested),
                        "tool_class": classify_tool(source, nested),
                        "patch_target_class": patch_class,
                        "patch_targets": ";".join(sanitize_local_path(target) for target in targets),
                    }
                elif subtype == "custom_tool_call_output" and call_id in custom_calls:
                    row = custom_calls.pop(call_id)
                    text = output_text(payload)
                    row["completed_at"] = timestamp
                    row["elapsed_seconds"] = round(
                        (parse_time(timestamp) - parse_time(row["started_at"])).total_seconds(), 3
                    )
                    match = WALL_TIME.search(text)
                    row["reported_wall_seconds"] = float(match.group(1)) if match else None
                    tool_rows.append(row)
                    if row["tool_class"] == "resource-admission":
                        normalized = text.replace("\\r\\n", "\n").replace('\\"', '"')
                        memories = [float(value) for value in MEMORY.findall(normalized)]
                        modes = re.findall(r'"AdmissionMode"\s*:\s*"([^"]+)"', normalized)
                        worktree_allowed = re.findall(
                            r'"WorktreeAdmission"\s*:\s*\{.*?"Allowed"\s*:\s*(true|false)',
                            normalized,
                            flags=re.IGNORECASE | re.DOTALL,
                        )
                        heavy_allowed = re.findall(
                            r'"HeavyOperationAdmission"\s*:\s*\{.*?"Allowed"\s*:\s*(true|false)',
                            normalized,
                            flags=re.IGNORECASE | re.DOTALL,
                        )
                        mode = modes[-1] if modes else None
                        worktree_ok = worktree_allowed[-1].lower() == "true" if worktree_allowed else None
                        heavy_ok = heavy_allowed[-1].lower() == "true" if heavy_allowed else None
                        denied = (
                            (mode == "Worktree" and worktree_ok is False)
                            or (mode in {"Heavy", "HeavyOperation"} and heavy_ok is False)
                        )
                        resource_rows.append({
                            "timestamp": timestamp,
                            "agent_path": thread["agent_path"],
                            "admission_mode": mode,
                            "available_memory_gib": memories[-1] if memories else None,
                            "memory_warning": bool(memories and memories[-1] < 1.5),
                            "worktree_allowed": worktree_ok,
                            "heavy_allowed": heavy_ok,
                            "denied": denied,
                        })
                elif subtype == "function_call" and call_id:
                    function_calls[call_id] = {
                        "name": payload.get("name", "unknown"),
                        "timestamp": timestamp,
                    }
                elif subtype == "function_call_output" and call_id in function_calls:
                    call = function_calls.pop(call_id)
                    text = output_text(payload)
                    if call["name"] in {"spawn_agent", "followup_task", "send_message", "list_agents"}:
                        collaboration_outputs.append({
                            **call,
                            "completed_at": timestamp,
                            "thread_limit_error": "agent thread limit reached" in text.lower(),
                            "is_error": bool(payload.get("is_error")) or '"isError":true' in text.replace(" ", ""),
                        })

    write_csv(
        args.output_dir / "tool-timings.csv",
        tool_rows,
        ["thread_id", "agent_path", "role", "call_id", "started_at", "completed_at", "elapsed_seconds", "reported_wall_seconds", "nested_tools", "tool_class", "patch_target_class", "patch_targets"],
    )
    write_csv(
        args.output_dir / "resource-samples.csv",
        resource_rows,
        ["timestamp", "agent_path", "admission_mode", "available_memory_gib", "memory_warning", "worktree_allowed", "heavy_allowed", "denied"],
    )

    commit_rows = []
    for commit in analysis["commits"]:
        stats = git(args.repo, "show", "--format=", "--numstat", commit["sha"])
        files = insertions = deletions = 0
        for line in stats.splitlines():
            parts = line.split("\t", 2)
            if len(parts) != 3:
                continue
            files += 1
            insertions += int(parts[0]) if parts[0].isdigit() else 0
            deletions += int(parts[1]) if parts[1].isdigit() else 0
        commit_rows.append({**commit, "files_changed": files, "insertions": insertions, "deletions": deletions})
    write_csv(
        args.output_dir / "commit-stats.csv",
        commit_rows,
        ["sha", "short_sha", "authored_at", "committed_at", "author", "subject", "files_changed", "insertions", "deletions"],
    )

    review_rows = []
    for thread in analysis["threads"]:
        if "review" not in thread["agent_path"]:
            continue
        for turn in thread["turns"]:
            if turn["outcome"] != "completed":
                continue
            excerpt = turn.get("result_excerpt") or ""
            review_rows.append({
                "agent_path": thread["agent_path"],
                "started_at": turn["started_at"],
                "duration_seconds": turn["duration_seconds"],
                "verdict": review_verdict(excerpt),
                "result_sha256": turn["result_sha256"],
                "result_excerpt": excerpt,
            })
    write_csv(
        args.output_dir / "review-turns.csv",
        review_rows,
        ["agent_path", "started_at", "duration_seconds", "verdict", "result_sha256", "result_excerpt"],
    )

    by_tool = defaultdict(lambda: {"calls": 0, "elapsed_seconds": 0.0})
    for row in tool_rows:
        by_tool[row["tool_class"]]["calls"] += 1
        by_tool[row["tool_class"]]["elapsed_seconds"] += row["elapsed_seconds"] or 0

    root_usage = {key: int(value) for key, value in analysis["summary"]["root_usage"].items()}
    astra_rates = {"input": 10.0, "cached_input": 1.0, "cache_write_input": 12.5, "output": 50.0}
    sol_rates = {"input": 4.0, "cached_input": 0.4, "cache_write_input": 5.0, "output": 20.0}
    astra_cost = usage_cost(root_usage, astra_rates)
    sol_counterfactual = usage_cost(root_usage, sol_rates)
    old = json.loads((args.repo / BASELINE_DATA / "analysis.json").read_text(encoding="utf-8"))
    old_comparison = json.loads((args.repo / BASELINE_DATA / "comparison-metrics.json").read_text(encoding="utf-8"))
    old_root = old["summary"]["root_usage"]

    owner_wait_seconds = round(sum(
        (parse_time(item["ended_at"]) - parse_time(item["started_at"])).total_seconds()
        for item in OWNER_WAITS
    ), 3)
    wall_seconds = analysis["summary"]["session_wall_span_seconds"]
    effective_seconds = wall_seconds - owner_wait_seconds
    old_effective_seconds = old_comparison["snapshot"]["new_effective_seconds"]

    writer_paths = {
        "/root/source_repair",
        "/root/cl_bonus_implementation",
        "/root/cl_completion",
    }
    writer_turns = [
        turn
        for thread in analysis["threads"]
        if thread["agent_path"] in writer_paths
        for turn in thread["turns"]
    ]
    dotnet_rows = [row for row in tool_rows if row["tool_class"] == "dotnet"]
    dotnet_intervals = [
        {"started_at": row["started_at"], "completed_at": row["completed_at"]}
        for row in dotnet_rows
    ]

    net_stat = git(args.repo, "diff", "--shortstat", f"{BASE_COMMIT}..{FINAL_COMMIT}").strip()
    numstat = git(args.repo, "diff", "--numstat", f"{BASE_COMMIT}..{FINAL_COMMIT}")
    net_files = net_insertions = net_deletions = 0
    for line in numstat.splitlines():
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        net_files += 1
        net_insertions += int(parts[0]) if parts[0].isdigit() else 0
        net_deletions += int(parts[1]) if parts[1].isdigit() else 0

    resource_values = [row["available_memory_gib"] for row in resource_rows if row["available_memory_gib"] is not None]
    root_patches = Counter(
        row["patch_target_class"] for row in tool_rows
        if row["role"] == "root" and row["patch_target_class"]
    )
    verdicts = Counter(row["verdict"] for row in review_rows)
    current_concurrency = analysis["summary"]["concurrency"]
    current_active = current_concurrency["wall_seconds_with_any_subagent"]
    two_plus_share = (
        current_concurrency["wall_seconds_with_two_or_more_subagents"] / current_active
        if current_active else 0
    )

    metrics = {
        "schema_version": 1,
        "session_boundary": {
            "run_id": RUN_ID,
            "base_commit": BASE_COMMIT,
            "final_commit": FINAL_COMMIT,
            "event_cutoff_at": analysis["source"]["event_cutoff_at"],
            "wall_seconds": wall_seconds,
            "owner_wait_seconds": owner_wait_seconds,
            "effective_seconds": round(effective_seconds, 3),
            "owner_waits": OWNER_WAITS,
        },
        "delivery": {
            "commits": len(commit_rows),
            "net_files": net_files,
            "net_insertions": net_insertions,
            "net_deletions": net_deletions,
            "git_shortstat": net_stat,
            "completed_task_files": analysis["summary"]["task_files_completed_in_session"],
        },
        "cost": {
            "pricing_as_of": "2026-09-05",
            "root_astra_medium_usd": round(astra_cost, 6),
            "same_usage_sol_xhigh_usd": round(sol_counterfactual, 6),
            "astra_increment_usd": round(astra_cost - sol_counterfactual, 6),
            "astra_multiple_same_tokens": round(astra_cost / sol_counterfactual, 4),
            "previous_sol_xhigh_root_usd": old["summary"]["root_api_cost_equivalent_usd"],
            "actual_session_root_cost_ratio": round(astra_cost / old["summary"]["root_api_cost_equivalent_usd"], 4),
            "current_root_total_tokens": root_usage["total_tokens"],
            "previous_root_total_tokens": old_root["total_tokens"],
            "root_token_change": round(root_usage["total_tokens"] / old_root["total_tokens"] - 1, 4),
            "current_root_cost_per_effective_hour": round(astra_cost * 3600 / effective_seconds, 4),
            "same_usage_sol_cost_per_effective_hour": round(sol_counterfactual * 3600 / effective_seconds, 4),
            "previous_sol_cost_per_effective_hour": round(old["summary"]["root_api_cost_equivalent_usd"] * 3600 / old_effective_seconds, 4),
            "root_cache_hit_share": round(root_usage["cached_input_tokens"] / root_usage["input_tokens"], 5),
            "priced_agent_portfolio_usd": analysis["summary"]["api_cost_equivalent_usd"],
            "priced_portfolio_if_root_sol_usd": round(analysis["summary"]["api_cost_equivalent_usd"] - astra_cost + sol_counterfactual, 6),
            "astra_increment_share_of_priced_portfolio": round((astra_cost - sol_counterfactual) / analysis["summary"]["api_cost_equivalent_usd"], 4),
            "scope_note": "API list-price equivalent; reasoning effort changes token use, not published per-token rates; Codex subscription quota is separate.",
        },
        "workflow": {
            "subagent_threads": analysis["summary"]["subagent_threads"],
            "subagent_turns": analysis["summary"]["subagent_turns"],
            "turns_per_thread": round(analysis["summary"]["subagent_turns"] / analysis["summary"]["subagent_threads"], 3),
            "followup_calls": analysis["summary"]["root_function_calls"].get("followup_task", 0),
            "spawn_calls": analysis["summary"]["root_function_calls"].get("spawn_agent", 0),
            "send_message_calls": analysis["summary"]["root_function_calls"].get("send_message", 0),
            "wait_calls": analysis["summary"]["root_function_calls"].get("wait_agent", 0),
            "waits_per_effective_hour": round(analysis["summary"]["root_function_calls"].get("wait_agent", 0) * 3600 / effective_seconds, 2),
            "previous_waits_per_effective_hour": old_comparison["coordination"]["new_waits_per_effective_hour"],
            "thread_limit_errors": sum(row["thread_limit_error"] for row in collaboration_outputs),
            "execution_start_markers": len(execution_markers),
            "execution_markers": execution_markers,
            "root_ledger_patches": root_patches.get("orchestration-ledger", 0),
            "root_ledger_patches_per_effective_hour": round(root_patches.get("orchestration-ledger", 0) * 3600 / effective_seconds, 2),
            "previous_ledger_patches_per_effective_hour": old_comparison["coordination"]["new_ledger_patches_per_effective_hour"],
            "maximum_concurrent_subagents": current_concurrency["maximum_concurrent_subagents"],
            "average_concurrency_while_active": current_concurrency["average_concurrency_while_active"],
            "two_plus_share": round(two_plus_share, 4),
            "previous_average_concurrency_while_active": old_comparison["parallelism"]["new_average_while_active"],
            "previous_two_plus_share": old_comparison["parallelism"]["new_two_plus_share"],
            "writer_concurrency": overlap_metrics(writer_turns),
            "dotnet_concurrency": overlap_metrics(dotnet_intervals),
            "resource_samples": len(resource_rows),
            "minimum_observed_available_memory_gib": min(resource_values) if resource_values else None,
            "samples_below_warning_floor": sum(value < 1.5 for value in resource_values),
            "admitted_heavy_samples_below_warning_floor": sum(
                row["admission_mode"] in {"Heavy", "HeavyOperation"}
                and row["heavy_allowed"] is True
                and row["available_memory_gib"] is not None
                and row["available_memory_gib"] < 1.5
                for row in resource_rows
            ),
            "denied_resource_samples": sum(row["denied"] for row in resource_rows),
            "review_verdicts": dict(verdicts),
            "review_turns": len(review_rows),
            "root_compactions": analysis["summary"]["root_compactions"],
            "root_token_share": round(root_usage["total_tokens"] / analysis["summary"]["usage"]["total_tokens"], 4),
        },
        "tool_time": {
            "calls": len(tool_rows),
            "by_class": {
                key: {"calls": value["calls"], "elapsed_seconds": round(value["elapsed_seconds"], 3)}
                for key, value in sorted(by_tool.items())
            },
        },
    }
    (args.output_dir / "derived-metrics.json").write_text(
        json.dumps(metrics, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
