#!/usr/bin/env python3
"""Derive recovery, preview, resource, role, and review metrics for the snapshot."""

from __future__ import annotations

import argparse
import collections
import csv
from datetime import datetime
import json
import pathlib
import re
import statistics
import subprocess
from typing import Any, Iterable


RUN_ID = "01a07449-de77-7ae0-ac4a-8f5330c43121"
ROOT_LOG_NAME = f"rollout-2026-09-06T03-16-33-{RUN_ID}.jsonl"
CUTOFF = "2026-09-06T20:21:56.196Z"
BASE_COMMIT = "a1e333f1923a69cff8b99ecdfb500547a2790b3f"
FINAL_COMMIT = "c99e1635428bcfea48271e4169767b38f014148c"
COMMON_TIP = "6cce46a162ec1a5b756173f6046b1666a48332eb"
COMMON_WORKTREE = pathlib.Path(".tmp/worktrees/p1-04-05-common")
PREVIEW_RELATIVE = f".tmp/orchestration/{RUN_ID}/preview.md"
CAPSULE_RELATIVE = f".tmp/orchestration/{RUN_ID}/capsule.json"
FROZEN_PREVIEW_BYTES = 67_073
FROZEN_PREVIEW_LINES = 663
FROZEN_PREVIEW_HEADINGS = 17

COORDINATION_CALLS = {
    "spawn_agent",
    "followup_task",
    "send_message",
    "wait_agent",
    "interrupt_agent",
}


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def write_csv(path: pathlib.Path, rows: Iterable[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field) for field in fields})


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
        raise RuntimeError(result.stderr.strip() or "git failed")
    return result.stdout


def strings(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)
    elif isinstance(value, dict):
        for item in value.values():
            yield from strings(item)


def flattened_text(value: Any) -> str:
    return "\n".join(strings(value))


def normalize_nested_json(text: str) -> str:
    return (
        text.replace("\\r\\n", "\n")
        .replace("\\n", "\n")
        .replace('\\"', '"')
        .replace("\\\\", "\\")
    )


def load_records(path: pathlib.Path) -> list[dict[str, Any]]:
    cutoff = parse_time(CUTOFF)
    records = []
    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            if parse_time(record["timestamp"]) <= cutoff:
                records.append(record)
    return records


def find_root_log(sessions_dir: pathlib.Path) -> pathlib.Path:
    paths = list(sessions_dir.rglob(ROOT_LOG_NAME))
    if len(paths) != 1:
        raise RuntimeError(f"Expected one root log; found {len(paths)}")
    return paths[0]


def model_cost(usage: dict[str, int], model: str) -> float:
    rates = {
        "gpt-6-astra": (10.0, 1.0, 12.5, 50.0),
        "gpt-5.6-sol": (4.0, 0.4, 5.0, 20.0),
        "gpt-5.6-terra": (2.0, 0.2, 2.5, 12.0),
        "gpt-5.6-luna": (0.2, 0.02, 0.25, 1.2),
    }
    uncached_rate, cached_rate, write_rate, output_rate = rates[model]
    cached = usage["cached_input_tokens"]
    cache_write = usage["cache_write_input_tokens"]
    uncached = usage["input_tokens"] - cached - cache_write
    return (
        uncached * uncached_rate
        + cached * cached_rate
        + cache_write * write_rate
        + usage["output_tokens"] * output_rate
    ) / 1_000_000


def classify_role(agent_path: str) -> str:
    value = agent_path.lower()
    if "common_runtime_review" in value:
        return "implementation-review"
    if "common_runtime_writer" in value or "common_runtime_fix" in value:
        return "implementation-writer"
    if "evidence" in value or "audit" in value or "github_state" in value:
        return "evidence"
    if "architecture" in value:
        return "architecture"
    if "spec_review" in value or "seam_review" in value or "packet_commit_review" in value:
        return "specification-review"
    if "seam_revision" in value or "packet_writer" in value or "packet_contract_fix" in value:
        return "planning-writer"
    if "posttest" in value:
        return "mechanical-check"
    return "other"


def read_context_paths(source: str) -> list[str]:
    candidates = re.findall(
        r"(?:[A-Za-z]:[/\\][^\"'`\s]+|(?:\.agents|\.codex|plans|docs|src|tests|\.tmp)[/\\][^\"'`\s,;\)\]]+)",
        source,
        flags=re.IGNORECASE,
    )
    cleaned = []
    for candidate in candidates:
        path = candidate.rstrip(".:)}]").replace("\\", "/")
        if re.search(r"\.(?:md|json|ps1)$", path, flags=re.IGNORECASE):
            lowered = path.lower()
            anchors = (".agents/", ".codex/", "plans/", "docs/", "src/", "tests/", ".tmp/")
            offsets = [lowered.find(anchor) for anchor in anchors if lowered.find(anchor) >= 0]
            cleaned.append(path[min(offsets):] if offsets else path)
    return sorted(set(cleaned))


def extract_resource_samples(tool_outputs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    samples: dict[str, dict[str, Any]] = {}
    for output in tool_outputs:
        text = normalize_nested_json(flattened_text(output["payload"].get("output")))
        positions = [match.start() for match in re.finditer(r'"GeneratedAtUtc"\s*:', text)]
        for index, start in enumerate(positions):
            segment = text[start : positions[index + 1] if index + 1 < len(positions) else len(text)]
            timestamp = re.search(r'"GeneratedAtUtc"\s*:\s*"([^"]+)"', segment)
            mode = re.search(r'"AdmissionMode"\s*:\s*"([^"]+)"', segment)
            memory = re.search(r'"AvailableMemoryGiB"\s*:\s*([0-9.]+)', segment)
            if not (timestamp and mode and memory):
                continue
            heavy_section = segment.split('"HeavyOperationAdmission"', 1)[-1]
            worktree_section = segment.split('"WorktreeAdmission"', 1)[-1].split('"HeavyOperationAdmission"', 1)[0]
            heavy_allowed = re.search(r'"Allowed"\s*:\s*(true|false)', heavy_section, flags=re.I)
            worktree_allowed = re.search(r'"Allowed"\s*:\s*(true|false)', worktree_section, flags=re.I)
            key = timestamp.group(1)
            samples[key] = {
                "timestamp": key,
                "timestamp_local": parse_time(key).astimezone().isoformat(),
                "admission_mode": mode.group(1),
                "available_memory_gib": float(memory.group(1)),
                "heavy_allowed": heavy_allowed.group(1).lower() == "true" if heavy_allowed else None,
                "worktree_allowed": worktree_allowed.group(1).lower() == "true" if worktree_allowed else None,
            }
    return sorted(samples.values(), key=lambda row: row["timestamp"])


def denied_episodes(samples: list[dict[str, Any]]) -> list[dict[str, Any]]:
    heavy = [row for row in samples if row["admission_mode"] in {"Heavy", "HeavyOperation"}]
    episodes = []
    start = None
    denied_count = 0
    minimum = None
    for row in heavy:
        if row["heavy_allowed"] is False:
            if start is None:
                start = row
                denied_count = 0
                minimum = row["available_memory_gib"]
            denied_count += 1
            minimum = min(minimum, row["available_memory_gib"])
        elif start is not None:
            elapsed = (parse_time(row["timestamp"]) - parse_time(start["timestamp"])).total_seconds()
            episodes.append({
                "started_at": start["timestamp"],
                "ended_at": row["timestamp"],
                "elapsed_seconds": round(elapsed, 3),
                "denied_samples": denied_count,
                "minimum_memory_gib": minimum,
                "admitted_memory_gib": row["available_memory_gib"],
            })
            start = None
    return episodes


def review_tags(message: str) -> list[str]:
    lowered = message.lower()
    tags = []
    patterns = {
        "replay-recovery": r"replay|rerun|late cycle|aborted|supersession|recovery|interrupted",
        "health-freshness": r"health|freshness|stale|staleness|watermark|condition|issue",
        "receipt-provenance": r"receipt|fallback|provenance|selected origin|selectedorigin",
        "persistence-schema": r"firestore|transaction|integer|double|persist|descriptor",
        "bundle-integrity": r"bundle|digest|canonical|hash|symlink|handoff",
    }
    for tag, pattern in patterns.items():
        if re.search(pattern, lowered):
            tags.append(tag)
    return tags


def last_messages(thread: dict[str, Any], sessions_dir: pathlib.Path) -> list[str]:
    path = sessions_dir / thread["log_file"]
    messages = []
    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            if parse_time(record["timestamp"]) > parse_time(CUTOFF):
                continue
            payload = record.get("payload", {})
            if record.get("type") == "event_msg" and payload.get("type") == "task_complete":
                message = payload.get("last_agent_message")
                if message:
                    messages.append(message)
    return messages


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--analysis", type=pathlib.Path, required=True)
    parser.add_argument("--output-dir", type=pathlib.Path, required=True)
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex/sessions")
    args = parser.parse_args()
    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    root_log = find_root_log(args.sessions_dir)
    records = load_records(root_log)

    custom_calls: dict[str, dict[str, Any]] = {}
    custom_outputs = []
    collaboration_calls: dict[str, dict[str, Any]] = {}
    thread_limit_errors = 0
    for record in records:
        payload = record.get("payload", {})
        if record.get("type") != "response_item":
            continue
        subtype = payload.get("type")
        call_id = payload.get("call_id")
        if subtype == "custom_tool_call" and call_id:
            custom_calls[call_id] = record
        elif subtype == "custom_tool_call_output" and call_id:
            custom_outputs.append(record)
        elif subtype == "function_call" and call_id:
            collaboration_calls[call_id] = record
        elif subtype == "function_call_output" and call_id in collaboration_calls:
            text = flattened_text(payload.get("output"))
            if "agent thread limit reached" in text.lower():
                thread_limit_errors += 1

    # Reconstruct the cost and context burden of each full recovery preflight.
    compaction_rows = []
    for compact in [record for record in records if record.get("type") == "compacted"]:
        ordinal = compact["ordinal"]
        later = [record for record in records if record["ordinal"] > ordinal]
        resume = next(
            record
            for record in later
            if record.get("type") == "response_item"
            and record.get("payload", {}).get("type") == "function_call"
            and record.get("payload", {}).get("name") in COORDINATION_CALLS
        )
        segment = [record for record in later if record["ordinal"] <= resume["ordinal"]]
        token_events = [
            record
            for record in segment
            if record.get("type") == "event_msg"
            and record.get("payload", {}).get("type") == "token_count"
            and int(((record["payload"].get("info") or {}).get("last_token_usage") or {}).get("input_tokens") or 0) > 0
        ]
        last_token = token_events[-1]["payload"]["info"]
        context_window = int(last_token["model_context_window"])
        input_tokens = int(last_token["last_token_usage"]["input_tokens"])
        segment_tools = [
            record for record in segment
            if record.get("type") == "response_item"
            and record.get("payload", {}).get("type") == "custom_tool_call"
        ]
        segment_outputs = [
            record for record in segment
            if record.get("type") == "response_item"
            and record.get("payload", {}).get("type") == "custom_tool_call_output"
        ]
        all_paths = sorted({
            path
            for record in segment_tools
            for path in read_context_paths(str(record["payload"].get("input") or ""))
        })
        hook_messages = [
            flattened_text(record.get("payload", {}).get("content"))
            for record in segment
            if record.get("type") == "response_item"
            and record.get("payload", {}).get("type") == "message"
            and record.get("payload", {}).get("role") == "developer"
            and "VALIDATED ORCHESTRATION CAPSULE" in flattened_text(record.get("payload", {}).get("content"))
        ]
        replacement_chars = len(json.dumps(compact["payload"].get("replacement_history", []), separators=(",", ":")))
        output_chars = sum(len(json.dumps(record["payload"].get("output"), separators=(",", ":"))) for record in segment_outputs)
        compaction_rows.append({
            "compacted_at": compact["timestamp"],
            "compacted_at_local": parse_time(compact["timestamp"]).astimezone().isoformat(),
            "resumed_at": resume["timestamp"],
            "resumed_at_local": parse_time(resume["timestamp"]).astimezone().isoformat(),
            "resume_call": resume["payload"]["name"],
            "recovery_seconds": round((parse_time(resume["timestamp"]) - parse_time(compact["timestamp"])).total_seconds(), 3),
            "replacement_items": len(compact["payload"].get("replacement_history", [])),
            "replacement_json_chars": replacement_chars,
            "hook_context_chars": len(hook_messages[0]) if hook_messages else 0,
            "custom_tool_calls": len(segment_tools),
            "tool_output_json_chars": output_chars,
            "distinct_context_paths": len(all_paths),
            "context_paths": ";".join(all_paths),
            "resume_input_tokens": input_tokens,
            "model_context_window": context_window,
            "resume_context_percent": round(100 * input_tokens / context_window, 2),
        })
    write_csv(
        args.output_dir / "compactions.csv",
        compaction_rows,
        [
            "compacted_at", "compacted_at_local", "resumed_at", "resumed_at_local",
            "resume_call", "recovery_seconds", "replacement_items",
            "replacement_json_chars", "hook_context_chars", "custom_tool_calls",
            "tool_output_json_chars", "distinct_context_paths", "context_paths",
            "resume_input_tokens", "model_context_window", "resume_context_percent",
        ],
    )

    resource_rows = extract_resource_samples(custom_outputs)
    for row in resource_rows:
        row["denied_by_1_10_floor"] = (
            row["admission_mode"] in {"Heavy", "HeavyOperation"}
            and row["heavy_allowed"] is False
            and row["available_memory_gib"] < 1.10
        )
    write_csv(
        args.output_dir / "resource-samples.csv",
        resource_rows,
        [
            "timestamp", "timestamp_local", "admission_mode", "available_memory_gib",
            "heavy_allowed", "worktree_allowed", "denied_by_1_10_floor",
        ],
    )
    episodes = denied_episodes(resource_rows)
    write_csv(
        args.output_dir / "memory-denial-episodes.csv",
        episodes,
        ["started_at", "ended_at", "elapsed_seconds", "denied_samples", "minimum_memory_gib", "admitted_memory_gib"],
    )

    preview_events = []
    preview_additions = preview_deletions = capsule_events = 0
    for record in records:
        payload = record.get("payload", {})
        item = payload.get("item") or {}
        if record.get("type") != "event_msg" or payload.get("type") != "item_completed" or item.get("type") != "FileChange":
            continue
        for path, change in (item.get("changes") or {}).items():
            normalized = path.replace("\\", "/").lower()
            diff = change.get("unified_diff") or ""
            additions = sum(line.startswith("+") and not line.startswith("+++") for line in diff.splitlines())
            deletions = sum(line.startswith("-") and not line.startswith("---") for line in diff.splitlines())
            if normalized.endswith(PREVIEW_RELATIVE.lower()):
                preview_additions += additions
                preview_deletions += deletions
                preview_events.append({
                    "timestamp": record["timestamp"],
                    "timestamp_local": parse_time(record["timestamp"]).astimezone().isoformat(),
                    "change_type": change.get("type"),
                    "additions": additions,
                    "deletions": deletions,
                })
            elif normalized.endswith(CAPSULE_RELATIVE.lower()):
                capsule_events += 1
    write_csv(
        args.output_dir / "preview-churn.csv",
        preview_events,
        ["timestamp", "timestamp_local", "change_type", "additions", "deletions"],
    )

    # Aggregate threads into stable roles and retain the exact model mix.
    role_accumulator: dict[str, dict[str, Any]] = collections.defaultdict(
        lambda: {
            "threads": 0,
            "turns": 0,
            "active_seconds": 0.0,
            "input_tokens": 0,
            "cached_input_tokens": 0,
            "cache_write_input_tokens": 0,
            "output_tokens": 0,
            "total_tokens": 0,
            "api_cost_equivalent_usd": 0.0,
            "models": collections.Counter(),
        }
    )
    for thread in analysis["threads"][1:]:
        role = classify_role(thread["agent_path"])
        row = role_accumulator[role]
        row["threads"] += 1
        row["turns"] += thread["turn_count"]
        row["active_seconds"] += thread["active_seconds"]
        for field in ("input_tokens", "cached_input_tokens", "cache_write_input_tokens", "output_tokens", "total_tokens"):
            row[field] += thread["usage"][field]
        row["api_cost_equivalent_usd"] += thread["api_cost_equivalent_usd"] or 0
        for mix in thread["model_effort_usage"]:
            row["models"][f"{mix['model']}/{mix['reasoning_effort']}"] += mix["responses"]
    role_rows = []
    for role, values in sorted(role_accumulator.items()):
        role_rows.append({
            **{key: round(value, 6) if isinstance(value, float) else value for key, value in values.items() if key != "models"},
            "role": role,
            "models": ";".join(f"{key}:{value}" for key, value in sorted(values["models"].items())),
        })
    write_csv(
        args.output_dir / "role-usage.csv",
        role_rows,
        [
            "role", "threads", "turns", "active_seconds", "models", "input_tokens",
            "cached_input_tokens", "cache_write_input_tokens", "output_tokens",
            "total_tokens", "api_cost_equivalent_usd",
        ],
    )

    review_rows = []
    tag_counts: collections.Counter[str] = collections.Counter()
    for thread in analysis["threads"][1:]:
        if "common_runtime_review" not in thread["agent_path"]:
            continue
        messages = last_messages(thread, args.sessions_dir)
        message = messages[-1] if messages else ""
        tags = review_tags(message)
        tag_counts.update(tags)
        review_rows.append({
            "agent_path": thread["agent_path"],
            "started_at": thread["turns"][0]["started_at"],
            "completed_at": thread["turns"][-1]["completed_at"],
            "active_seconds": thread["active_seconds"],
            "total_tokens": thread["usage"]["total_tokens"],
            "api_cost_equivalent_usd": thread["api_cost_equivalent_usd"],
            "verdict": "blocked" if re.search(r"reject|block|changes required|not approved|changes requested", message, flags=re.I) else "approved",
            "tags": ";".join(tags),
        })
    write_csv(
        args.output_dir / "review-cycles.csv",
        review_rows,
        ["agent_path", "started_at", "completed_at", "active_seconds", "total_tokens", "api_cost_equivalent_usd", "verdict", "tags"],
    )

    heavy_samples = [row for row in resource_rows if row["admission_mode"] in {"Heavy", "HeavyOperation"}]
    heavy_values = [row["available_memory_gib"] for row in heavy_samples]
    allowed_values = [row["available_memory_gib"] for row in heavy_samples if row["heavy_allowed"]]
    denied_values = [row["available_memory_gib"] for row in heavy_samples if row["heavy_allowed"] is False]

    common_commit_count = int(git(args.repo, "-C", str(COMMON_WORKTREE), "rev-list", "--count", f"{FINAL_COMMIT}..{COMMON_TIP}").strip())
    common_numstat = git(args.repo, "-C", str(COMMON_WORKTREE), "diff", "--numstat", f"{FINAL_COMMIT}..{COMMON_TIP}")
    common_files = common_insertions = common_deletions = 0
    for line in common_numstat.splitlines():
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        common_files += 1
        common_insertions += int(parts[0]) if parts[0].isdigit() else 0
        common_deletions += int(parts[1]) if parts[1].isdigit() else 0

    effective_wait_start = parse_time("2026-09-06T01:47:36.624Z")
    effective_wait_end = parse_time("2026-09-06T07:17:27.890Z")
    owner_wait_seconds = (effective_wait_end - effective_wait_start).total_seconds()
    wall_seconds = analysis["summary"]["session_wall_span_seconds"]
    recovery_seconds = sum(row["recovery_seconds"] for row in compaction_rows)

    design_roles = {"architecture", "specification-review", "planning-writer"}
    design_rows = [row for row in role_rows if row["role"] in design_roles]
    design_usage = {
        field: sum(int(row[field]) for row in design_rows)
        for field in ("input_tokens", "cached_input_tokens", "cache_write_input_tokens", "output_tokens", "total_tokens")
    }
    design_actual_cost = sum(float(row["api_cost_equivalent_usd"]) for row in design_rows)
    design_astra_cost = model_cost(design_usage, "gpt-6-astra")
    architecture_row = next(row for row in role_rows if row["role"] == "architecture")
    architecture_usage = {
        field: int(architecture_row[field])
        for field in ("input_tokens", "cached_input_tokens", "cache_write_input_tokens", "output_tokens", "total_tokens")
    }
    architecture_actual_cost = float(architecture_row["api_cost_equivalent_usd"])
    architecture_astra_cost = model_cost(architecture_usage, "gpt-6-astra")
    capsule_seals = sum(
        "invoke-orchestrationcapsulehook.ps1" in str(record["payload"].get("input") or "").lower()
        and re.search(r"(?:^|\s)-Mode\s+Seal(?:\s|$)", str(record["payload"].get("input") or ""), flags=re.I)
        is not None
        for record in custom_calls.values()
    )

    metrics = {
        "schema_version": 1,
        "snapshot": {
            "run_id": RUN_ID,
            "base_commit": BASE_COMMIT,
            "final_integrated_commit": FINAL_COMMIT,
            "common_runtime_tip": COMMON_TIP,
            "cutoff_utc": CUTOFF,
            "cutoff_local": parse_time(CUTOFF).astimezone().isoformat(),
            "wall_seconds": wall_seconds,
            "owner_wait_seconds": owner_wait_seconds,
            "effective_seconds": wall_seconds - owner_wait_seconds,
            "run_status": "active",
        },
        "startup": {
            "hook_confirmation_delay_seconds": (
                parse_time("2026-09-06T01:20:49.954Z") - parse_time("2026-09-06T01:17:20.211Z")
            ).total_seconds(),
            "preventable_owner_stall_seconds": owner_wait_seconds,
            "initial_stop_reason": "P1-04 source evidence declared unavailable; P1-05 incorrectly sequenced behind P1-04.",
        },
        "delivery": {
            "integrated_plan_commits": analysis["summary"]["session_commits"],
            "common_runtime_commits": common_commit_count,
            "common_runtime_files": common_files,
            "common_runtime_insertions": common_insertions,
            "common_runtime_deletions": common_deletions,
            "tasks_completed": analysis["summary"]["task_files_completed_in_session"],
            "common_review_cycles": len(review_rows),
            "common_review_blocks": sum(row["verdict"] == "blocked" for row in review_rows),
            "review_tag_counts": dict(tag_counts),
            "runtime_loop_seconds_at_cutoff": (
                parse_time(CUTOFF) - parse_time("2026-09-06T09:27:32.368Z")
            ).total_seconds(),
        },
        "coordination": {
            "subagent_threads": analysis["summary"]["subagent_threads"],
            "subagent_turns": analysis["summary"]["subagent_turns"],
            "thread_limit_errors": thread_limit_errors,
            "maximum_concurrent_agents": analysis["summary"]["concurrency"]["maximum_concurrent_subagents"],
            "average_concurrency_while_active": analysis["summary"]["concurrency"]["average_concurrency_while_active"],
            "two_plus_share": (
                analysis["summary"]["concurrency"]["wall_seconds_with_two_or_more_subagents"]
                / analysis["summary"]["concurrency"]["wall_seconds_with_any_subagent"]
            ),
            "wait_calls": analysis["summary"]["root_function_calls"].get("wait_agent", 0),
        },
        "preview": {
            "frozen_bytes": FROZEN_PREVIEW_BYTES,
            "frozen_lines": FROZEN_PREVIEW_LINES,
            "frozen_headings": FROZEN_PREVIEW_HEADINGS,
            "patch_events": len(preview_events),
            "cumulative_line_additions": preview_additions,
            "cumulative_line_deletions": preview_deletions,
            "capsule_patch_events": capsule_events,
            "capsule_seals": capsule_seals,
        },
        "recovery": {
            "compactions": len(compaction_rows),
            "validated_hook_injections": sum(row["hook_context_chars"] > 0 for row in compaction_rows),
            "total_recovery_seconds": recovery_seconds,
            "mean_recovery_seconds": statistics.mean(row["recovery_seconds"] for row in compaction_rows),
            "median_recovery_seconds": statistics.median(row["recovery_seconds"] for row in compaction_rows),
            "mean_resume_context_percent": statistics.mean(row["resume_context_percent"] for row in compaction_rows),
            "median_resume_context_percent": statistics.median(row["resume_context_percent"] for row in compaction_rows),
            "maximum_resume_context_percent": max(row["resume_context_percent"] for row in compaction_rows),
            "maximum_recovery_tool_calls": max(row["custom_tool_calls"] for row in compaction_rows),
            "maximum_recovery_paths": max(row["distinct_context_paths"] for row in compaction_rows),
            "recovery_share_of_effective_time": recovery_seconds / (wall_seconds - owner_wait_seconds),
        },
        "memory": {
            "resource_samples": len(resource_rows),
            "heavy_samples": len(heavy_samples),
            "heavy_allowed": sum(row["heavy_allowed"] is True for row in heavy_samples),
            "heavy_denied": sum(row["heavy_allowed"] is False for row in heavy_samples),
            "denied_by_1_10_floor": sum(row["denied_by_1_10_floor"] for row in heavy_samples),
            "denial_episodes": len(episodes),
            "minimum_observed_gib": min(heavy_values) if heavy_values else None,
            "minimum_admitted_gib": min(allowed_values) if allowed_values else None,
            "maximum_denied_gib": max(denied_values) if denied_values else None,
            "median_admitted_gib": statistics.median(allowed_values) if allowed_values else None,
            "samples_below_1_50_gib": sum(value < 1.5 for value in heavy_values),
            "admitted_samples_below_1_50_gib": sum(value < 1.5 for value in allowed_values),
            "denied_samples_at_or_above_1_00_gib": sum(value >= 1.0 for value in denied_values),
            "observed_ready_work_block_seconds": 55,
            "ongoing_denial_at_cutoff_seconds": 183,
            "causal_note": "Only the 55-second 11:33 denial was closed and demonstrably delayed ready heavy work. The 22:18 denial was still open at cutoff; other sample-to-sample intervals include unrelated work and are not throughput loss.",
        },
        "models": {
            "design_threads": sum(int(row["threads"]) for row in design_rows),
            "design_turns": sum(int(row["turns"]) for row in design_rows),
            "design_active_seconds": sum(float(row["active_seconds"]) for row in design_rows),
            "design_total_tokens": design_usage["total_tokens"],
            "design_actual_cost_usd": round(design_actual_cost, 6),
            "design_same_tokens_astra_cost_usd": round(design_astra_cost, 6),
            "design_astra_premium_usd": round(design_astra_cost - design_actual_cost, 6),
            "architecture_threads": int(architecture_row["threads"]),
            "architecture_turns": int(architecture_row["turns"]),
            "architecture_total_tokens": architecture_usage["total_tokens"],
            "architecture_actual_cost_usd": round(architecture_actual_cost, 6),
            "architecture_same_tokens_astra_cost_usd": round(architecture_astra_cost, 6),
            "architecture_astra_premium_usd": round(architecture_astra_cost - architecture_actual_cost, 6),
            "recommendation": "Pilot gpt-6-astra/high for the architecture lead only; retain an independent gpt-5.6-sol/xhigh specification reviewer.",
        },
        "cost": {
            "api_list_price_equivalent_usd": analysis["summary"]["api_cost_equivalent_usd"],
            "root_api_list_price_equivalent_usd": analysis["summary"]["root_api_cost_equivalent_usd"],
            "subagent_api_list_price_equivalent_usd": analysis["summary"]["subagent_api_cost_equivalent_usd"],
        },
    }
    (args.output_dir / "derived-metrics.json").write_text(
        json.dumps(metrics, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
