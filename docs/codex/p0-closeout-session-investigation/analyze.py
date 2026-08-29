#!/usr/bin/env python3
"""Extract reproducible metrics from the August 2026 P0 Codex session family.

The source transcripts remain in the user's local Codex data directory. This
script writes normalized metrics, hashes, excerpts, and timelines; it never
copies complete prompts, messages, reasoning, or tool output into the repo.
"""

from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import json
import pathlib
import re
import subprocess
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any, Iterable
from zoneinfo import ZoneInfo


ROOT_THREAD_ID = "01a02485-f0b3-7241-a6c7-c6f58fe44509"
ROOT_LOG_NAME = (
    "rollout-2026-08-21T15-32-33-"
    "01a02485-f0b3-7241-a6c7-c6f58fe44509.jsonl"
)
LOCAL_ZONE = ZoneInfo("Europe/Berlin")
SESSION_BASE_COMMIT = "6d0fca3"
SESSION_FINAL_COMMIT = "2c824c8"

# Filled from official OpenAI model pages and intentionally date-stamped in
# the generated output. Rates are USD per one million tokens.
PRICING_AS_OF = "2026-08-29"
MODEL_PRICING: dict[str, dict[str, float]] = {
    "gpt-5.6-sol": {
        "input": 4.0,
        "cached_input": 0.4,
        "cache_write_input": 5.0,
        "output": 20.0,
        "source_url": "https://developers.openai.com/api/docs/models/gpt-5.6-sol",
    },
}

# Human annotations over the immutable 45-message sequence. The first message
# is the kickoff; every subsequent message is an intervention. These categories
# answer different questions than the transcript-level injected-context filter.
USER_MESSAGE_KINDS_BY_ORDINAL = {
    1: "kickoff",
    **{index: "authorization-or-external-unblock" for index in (
        2, 4, 5, 6, 10, 11, 19, 20, 23, 24, 25, 28, 29, 31, 32, 33, 34,
        35, 36, 37, 38, 39, 40, 41,
    )},
    **{index: "scope-correction-or-clarification" for index in (
        7, 8, 9, 13, 14, 15, 16, 17, 18, 21, 22, 30, 42, 43, 44, 45,
    )},
    **{index: "status-or-process-question" for index in (3, 12, 26, 27)},
}

UUID_AT_END = re.compile(r"([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})$")


def parse_time(value: str | int | float | None) -> datetime | None:
    if not value:
        return None
    if isinstance(value, (int, float)):
        seconds = float(value)
        while seconds > 100_000_000_000:
            seconds /= 1000
        return datetime.fromtimestamp(seconds, tz=timezone.utc)
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def iso_utc(value: datetime | None) -> str | None:
    if value is None:
        return None
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def iso_local(value: datetime | None) -> str | None:
    if value is None:
        return None
    return value.astimezone(LOCAL_ZONE).isoformat()


def seconds_between(start: datetime | None, end: datetime | None) -> float | None:
    if start is None or end is None:
        return None
    return (end - start).total_seconds()


def actual_thread_id(path: pathlib.Path) -> str:
    match = UUID_AT_END.search(path.stem)
    if not match:
        raise ValueError(f"Cannot infer thread id from {path}")
    return match.group(1)


def read_related_meta(path: pathlib.Path) -> dict[str, Any] | None:
    with path.open("r", encoding="utf-8") as handle:
        for _ in range(3):
            line = handle.readline()
            if not line:
                break
            record = json.loads(line)
            if record.get("type") != "session_meta":
                continue
            payload = record.get("payload", {})
            source = payload.get("source")
            if isinstance(source, dict):
                spawn = source.get("subagent", {}).get("thread_spawn")
                if spawn:
                    return {
                        "kind": "subagent",
                        "thread_id": actual_thread_id(path),
                        "parent_thread_id": spawn.get("parent_thread_id"),
                        "depth": spawn.get("depth"),
                        "agent_path": spawn.get("agent_path"),
                        "nickname": spawn.get("agent_nickname"),
                        "role": spawn.get("agent_role"),
                        "spawned_at": payload.get("timestamp") or record.get("timestamp"),
                        "path": path,
                    }
                if (
                    source.get("subagent", {}).get("other") == "guardian"
                    and (payload.get("session_id") or payload.get("id")) == ROOT_THREAD_ID
                ):
                    return {
                        "kind": "guardian",
                        "thread_id": actual_thread_id(path),
                        "parent_thread_id": ROOT_THREAD_ID,
                        "depth": 1,
                        "agent_path": "/guardian/auto-review",
                        "nickname": "auto-review",
                        "role": "approval-review",
                        "spawned_at": payload.get("timestamp") or record.get("timestamp"),
                        "path": path,
                    }
    return None


def discover_family(
    sessions_dir: pathlib.Path, root_log: pathlib.Path
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    children_by_parent: dict[str, list[dict[str, Any]]] = collections.defaultdict(list)
    guardians: list[dict[str, Any]] = []
    for path in sessions_dir.rglob("rollout-*.jsonl"):
        try:
            meta = read_related_meta(path)
        except (json.JSONDecodeError, OSError, ValueError):
            continue
        if meta and meta["kind"] == "subagent":
            children_by_parent[meta["parent_thread_id"]].append(meta)
        elif meta and meta["kind"] == "guardian":
            guardians.append(meta)

    family: list[dict[str, Any]] = [{
        "kind": "root",
        "thread_id": ROOT_THREAD_ID,
        "parent_thread_id": None,
        "depth": 0,
        "agent_path": "/root",
        "nickname": "orchestrator",
        "role": "orchestrator",
        "spawned_at": None,
        "path": root_log,
    }]
    queue = [ROOT_THREAD_ID]
    seen = {ROOT_THREAD_ID}
    while queue:
        parent = queue.pop(0)
        for child in sorted(
            children_by_parent.get(parent, []),
            key=lambda item: (item.get("spawned_at") or "", item["thread_id"]),
        ):
            if child["thread_id"] in seen:
                continue
            seen.add(child["thread_id"])
            family.append(child)
            queue.append(child["thread_id"])
    return family, sorted(guardians, key=lambda item: item.get("spawned_at") or "")


def message_text(payload: dict[str, Any]) -> str:
    return "\n".join(
        str(item.get("text", ""))
        for item in payload.get("content", [])
        if isinstance(item, dict)
    ).strip()


def classify_root_user_message(text: str) -> str:
    if text.startswith("# AGENTS.md instructions for "):
        return "injected-repository-context"
    if text.startswith("<environment_context>"):
        return "injected-environment-context"
    if text.startswith('<codex_internal_context source="goal">'):
        return "automatic-goal-continuation"
    return "user"


def summarize_user_message(text: str) -> str:
    plain = re.sub(r"<image[^>]*>.*?</image>", "[image]", text, flags=re.DOTALL)
    plain = sanitize_text(plain)
    return plain[:240].rstrip()


def sanitize_text(text: str) -> str:
    """Keep useful excerpts without publishing the workstation user path."""
    home = str(pathlib.Path.home())
    redacted = text.replace(home, "%USERPROFILE%").replace(home.replace("\\", "/"), "%USERPROFILE%")
    return " ".join(redacted.split())


def task_group(agent_path: str) -> str:
    normalized = agent_path.lower().replace("-", "_")
    match = re.search(r"p0_(\d{1,2})(?:_|$)", normalized)
    if match:
        return f"P0-{int(match.group(1)):02d}"
    if "schadens" in normalized:
        return "schadensfresse-closeout"
    if "roster_enrichment" in normalized:
        return "P0-25"
    if normalized.startswith("/root/ci_78f4d3b"):
        return "P0-23"
    if any(word in normalized for word in ("sol_max_quality", "sol_max_exact")):
        return "P0-23"
    if "closeout_owner_selection" in normalized:
        return "P0-06"
    if "live_arena_ladder" in normalized:
        return "P0-21"
    if any(word in normalized for word in ("production", "schedule", "activation")):
        return "P0-21"
    if any(word in normalized for word in ("cost", "experiment", "quality")):
        return "P0-23"
    if any(word in normalized for word in ("status", "code_seams", "orchestration")):
        return "orchestration"
    return "cross-cutting"


def turn_task_group(agent_path: str, result: str) -> str:
    base = task_group(agent_path)
    matches = []
    raw_matches = list(
        re.finditer(r"\bP0[-_ ]?(\d{1,2})\b", result, flags=re.IGNORECASE)
    )
    for match in raw_matches:
        raw = match.group(1)
        code = f"P0-{int(raw):02d}"
        if code not in matches:
            matches.append(code)
    if raw_matches and raw_matches[0].start() < 80:
        return f"P0-{int(raw_matches[0].group(1)):02d}"
    if len(matches) == 1:
        return matches[0]
    return base


def zero_usage() -> dict[str, int]:
    return {
        "input_tokens": 0,
        "cached_input_tokens": 0,
        "cache_write_input_tokens": 0,
        "output_tokens": 0,
        "reasoning_output_tokens": 0,
        "total_tokens": 0,
    }


def add_usage(target: dict[str, int], source: dict[str, Any]) -> None:
    for key in target:
        target[key] += int(source.get(key) or 0)


def cost_for_usage(model: str, usage: dict[str, int]) -> float | None:
    pricing = MODEL_PRICING.get(model)
    if not pricing:
        return None
    cache_write = usage["cache_write_input_tokens"]
    cached = usage["cached_input_tokens"]
    uncached = usage["input_tokens"] - cached - cache_write
    if uncached < 0:
        raise ValueError(f"Negative uncached input for {model}: {usage}")
    return (
        uncached * pricing["input"]
        + cached * pricing["cached_input"]
        + cache_write * pricing.get("cache_write_input", pricing["input"] * 1.25)
        + usage["output_tokens"] * pricing["output"]
    ) / 1_000_000


@dataclass
class ActiveTurn:
    turn_id: str
    started_at: datetime


def parse_thread(meta: dict[str, Any], root_turn_ids: set[str]) -> dict[str, Any]:
    path: pathlib.Path = meta["path"]
    spawn_time = parse_time(meta.get("spawned_at"))
    top_types: collections.Counter[str] = collections.Counter()
    event_types: collections.Counter[str] = collections.Counter()
    response_types: collections.Counter[str] = collections.Counter()
    function_calls: collections.Counter[str] = collections.Counter()
    custom_tool_calls: collections.Counter[str] = collections.Counter()
    nested_tool_calls: collections.Counter[str] = collections.Counter()
    usages_by_model_effort: dict[str, dict[str, int]] = collections.defaultdict(zero_usage)
    response_counts_by_model_effort: collections.Counter[str] = collections.Counter()
    actual_starts: dict[str, dict[str, Any]] = {}
    completions: dict[str, dict[str, Any]] = {}
    aborts: dict[str, dict[str, Any]] = {}
    contexts: dict[str, dict[str, Any]] = {}
    current_turn_id: str | None = None
    current_model = "unknown"
    current_effort = "unknown"
    first_actual_line: int | None = None
    last_token_total: dict[str, int] | None = None
    previous_token_total: dict[str, int] | None = None
    token_counter_segments = 0
    user_messages: list[dict[str, Any]] = []
    assistant_messages = 0
    first_timestamp: datetime | None = None
    last_timestamp: datetime | None = None
    line_count = 0
    inbound_agent_messages = 0
    compactions = 0
    max_input_tokens_per_response = 0
    responses_over_272k_input = 0
    spawn_agent_calls: list[dict[str, Any]] = []

    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            line_count = line_number
            record = json.loads(line)
            record_time = parse_time(record.get("timestamp"))
            if record_time:
                first_timestamp = min(first_timestamp, record_time) if first_timestamp else record_time
                last_timestamp = max(last_timestamp, record_time) if last_timestamp else record_time
            record_type = record.get("type")
            payload = record.get("payload", {})
            top_types[record_type] += 1

            if record_type == "event_msg":
                subtype = payload.get("type")
                event_types[subtype] += 1
                if subtype == "task_started":
                    turn_id = payload.get("turn_id")
                    started_at = parse_time(payload.get("started_at")) or record_time
                    is_actual = meta["kind"] in {"root", "guardian"} or (
                        turn_id not in root_turn_ids
                        and started_at is not None
                        and spawn_time is not None
                        # Logged task times have one-second precision while the
                        # spawn metadata has milliseconds.
                        and started_at >= spawn_time - timedelta(seconds=2)
                    )
                    if is_actual and turn_id and started_at:
                        actual_starts[turn_id] = {
                            "turn_id": turn_id,
                            "started_at": started_at,
                            "start_line": line_number,
                        }
                        current_turn_id = turn_id
                        first_actual_line = first_actual_line or line_number
                elif subtype == "task_complete":
                    turn_id = payload.get("turn_id")
                    if turn_id in actual_starts:
                        completions[turn_id] = {
                            "completed_at": parse_time(payload.get("completed_at")) or record_time,
                            "duration_ms": payload.get("duration_ms"),
                            "time_to_first_token_ms": payload.get("time_to_first_token_ms"),
                            "error": payload.get("error"),
                            "last_agent_message": payload.get("last_agent_message"),
                        }
                        if current_turn_id == turn_id:
                            current_turn_id = None
                elif subtype == "turn_aborted":
                    turn_id = payload.get("turn_id") or current_turn_id
                    if turn_id in actual_starts:
                        aborts[turn_id] = {
                            "timestamp": record_time,
                            "reason": payload.get("reason"),
                        }
                elif subtype == "token_count":
                    info = payload.get("info") or {}
                    last_usage = info.get("last_token_usage")
                    total_usage = info.get("total_token_usage")
                    if total_usage:
                        normalized_total = {
                            key: int(total_usage.get(key) or 0) for key in zero_usage()
                        }
                        counter_reset = (
                            previous_token_total is None
                            or normalized_total["total_tokens"]
                            < previous_token_total["total_tokens"]
                        )
                        if counter_reset:
                            token_counter_segments += 1
                            delta = normalized_total
                        else:
                            delta = {
                                key: normalized_total[key] - previous_token_total[key]
                                for key in normalized_total
                            }
                        # Some transcript resumes re-emit the last usage event.
                        # Cumulative deltas de-duplicate those records.
                        if any(delta.values()):
                            key = f"{current_model}|{current_effort}"
                            add_usage(usages_by_model_effort[key], delta)
                            response_counts_by_model_effort[key] += 1
                            if last_usage:
                                input_tokens = int(last_usage.get("input_tokens") or 0)
                                max_input_tokens_per_response = max(
                                    max_input_tokens_per_response, input_tokens
                                )
                                if input_tokens > 272_000:
                                    responses_over_272k_input += 1
                        previous_token_total = normalized_total
                        last_token_total = normalized_total
                    elif last_usage:
                        key = f"{current_model}|{current_effort}"
                        add_usage(usages_by_model_effort[key], last_usage)
                        response_counts_by_model_effort[key] += 1
                        input_tokens = int(last_usage.get("input_tokens") or 0)
                        max_input_tokens_per_response = max(
                            max_input_tokens_per_response, input_tokens
                        )
                        if input_tokens > 272_000:
                            responses_over_272k_input += 1

            elif record_type == "turn_context":
                turn_id = payload.get("turn_id")
                settings = (payload.get("collaboration_mode") or {}).get("settings") or {}
                model = payload.get("model") or settings.get("model") or "unknown"
                effort = payload.get("effort") or settings.get("reasoning_effort") or "unknown"
                if turn_id:
                    contexts[turn_id] = {"model": model, "effort": effort}
                current_model = model
                current_effort = effort

            elif record_type == "response_item":
                subtype = payload.get("type")
                response_types[subtype] += 1
                if subtype == "message":
                    role = payload.get("role", "unknown")
                    response_types[f"message:{role}"] += 1
                    if role == "assistant" and (first_actual_line is None or line_number >= first_actual_line):
                        assistant_messages += 1
                    elif role == "user" and meta["kind"] == "root":
                        text = message_text(payload)
                        category = classify_root_user_message(text)
                        user_messages.append({
                            "timestamp": iso_utc(record_time),
                            "timestamp_local": iso_local(record_time),
                            "line": line_number,
                            "category": category,
                            "characters": len(text),
                            "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                            "excerpt": summarize_user_message(text) if category == "user" else None,
                        })
                elif subtype == "function_call" and (
                    meta["kind"] in {"root", "guardian"}
                    or first_actual_line is None or line_number >= first_actual_line
                ):
                    function_name = payload.get("name", "unknown")
                    function_calls[function_name] += 1
                    if meta["kind"] == "root" and function_name == "spawn_agent":
                        raw_arguments = payload.get("arguments") or {}
                        try:
                            arguments = (
                                json.loads(raw_arguments)
                                if isinstance(raw_arguments, str)
                                else raw_arguments
                            )
                        except json.JSONDecodeError:
                            arguments = {}
                        if not isinstance(arguments, dict):
                            arguments = {}
                        spawn_agent_calls.append({
                            "timestamp": iso_utc(record_time),
                            "timestamp_local": iso_local(record_time),
                            "call_id": payload.get("call_id"),
                            "task_name": arguments.get("task_name"),
                            "fork_turns": str(arguments.get("fork_turns", "all")),
                            "explicit_model": "model" in arguments,
                            "model": arguments.get("model"),
                            "explicit_reasoning_effort": "reasoning_effort" in arguments,
                            "reasoning_effort": arguments.get("reasoning_effort"),
                        })
                elif subtype == "custom_tool_call" and (
                    meta["kind"] in {"root", "guardian"}
                    or first_actual_line is None or line_number >= first_actual_line
                ):
                    custom_tool_calls[payload.get("name", "unknown")] += 1
                    source = payload.get("input") or ""
                    for nested_name in re.findall(r"\btools\.([A-Za-z0-9_]+)\s*\(", source):
                        nested_tool_calls[nested_name] += 1
                elif subtype == "agent_message":
                    inbound_agent_messages += 1
            elif record_type == "compacted":
                compactions += 1

    turns: list[dict[str, Any]] = []
    for turn_id, start in sorted(actual_starts.items(), key=lambda item: item[1]["started_at"]):
        completion = completions.get(turn_id, {})
        aborted = aborts.get(turn_id)
        completed_at = completion.get("completed_at") or (aborted or {}).get("timestamp")
        duration_ms = completion.get("duration_ms")
        if duration_ms is None and completed_at:
            duration_ms = int((completed_at - start["started_at"]).total_seconds() * 1000)
        outcome = "completed" if turn_id in completions else "aborted" if aborted else "incomplete"
        context = contexts.get(turn_id, {})
        last_message = completion.get("last_agent_message") or ""
        turns.append({
            "thread_id": meta["thread_id"],
            "agent_path": meta["agent_path"],
            "task_group": turn_task_group(meta["agent_path"], last_message),
            "turn_id": turn_id,
            "started_at": iso_utc(start["started_at"]),
            "started_at_local": iso_local(start["started_at"]),
            "completed_at": iso_utc(completed_at),
            "completed_at_local": iso_local(completed_at),
            "duration_seconds": round(duration_ms / 1000, 3) if duration_ms is not None else None,
            "time_to_first_token_seconds": round((completion.get("time_to_first_token_ms") or 0) / 1000, 3)
            if completion.get("time_to_first_token_ms") is not None else None,
            "outcome": outcome,
            "model": context.get("model", "unknown"),
            "reasoning_effort": context.get("effort", "unknown"),
            "error": sanitize_text(str(completion.get("error") or (aborted or {}).get("reason")))
            if completion.get("error") or (aborted or {}).get("reason") else None,
            "result_sha256": hashlib.sha256(last_message.encode("utf-8")).hexdigest() if last_message else None,
            "result_excerpt": sanitize_text(last_message)[:300].rstrip() if last_message else None,
        })

    summed_usage = zero_usage()
    model_effort_rows = []
    total_cost = 0.0
    all_priced = True
    for key, usage in sorted(usages_by_model_effort.items()):
        model, effort = key.split("|", 1)
        add_usage(summed_usage, usage)
        cost = cost_for_usage(model, usage)
        if cost is None:
            all_priced = False
        else:
            total_cost += cost
        model_effort_rows.append({
            "model": model,
            "reasoning_effort": effort,
            "responses": response_counts_by_model_effort[key],
            "usage": usage,
            "api_cost_equivalent_usd": round(cost, 6) if cost is not None else None,
        })

    completed_turns = [turn for turn in turns if turn["outcome"] == "completed"]
    active_seconds = sum(turn["duration_seconds"] or 0 for turn in turns)
    return {
        "thread_id": meta["thread_id"],
        "kind": meta["kind"],
        "parent_thread_id": meta["parent_thread_id"],
        "depth": meta["depth"],
        "agent_path": meta["agent_path"],
        "nickname": meta["nickname"],
        "role": meta["role"],
        "task_group": task_group(meta["agent_path"]),
        "log_file": str(path.relative_to(pathlib.Path.home() / ".codex" / "sessions")),
        "log_bytes": path.stat().st_size,
        "log_lines": line_count,
        "spawned_at": iso_utc(spawn_time or first_timestamp),
        "spawned_at_local": iso_local(spawn_time or first_timestamp),
        "last_event_at": iso_utc(last_timestamp),
        "last_event_at_local": iso_local(last_timestamp),
        "wall_span_seconds": seconds_between(spawn_time or first_timestamp, last_timestamp),
        "turns": turns,
        "turn_count": len(turns),
        "completed_turns": len(completed_turns),
        "aborted_turns": sum(turn["outcome"] == "aborted" for turn in turns),
        "incomplete_turns": sum(turn["outcome"] == "incomplete" for turn in turns),
        "active_seconds": round(active_seconds, 3),
        "assistant_messages": assistant_messages,
        "inbound_agent_messages": inbound_agent_messages,
        "compactions": compactions,
        "function_calls": dict(function_calls),
        "custom_tool_calls": dict(custom_tool_calls),
        "nested_tool_calls": dict(nested_tool_calls),
        "top_types": dict(top_types),
        "event_types": dict(event_types),
        "response_types": dict(response_types),
        "model_effort_usage": model_effort_rows,
        "max_input_tokens_per_response": max_input_tokens_per_response,
        "responses_over_272k_input": responses_over_272k_input,
        "usage": summed_usage,
        "token_counter_segments": token_counter_segments,
        "final_counter_segment_usage": last_token_total,
        "previous_counter_segments_usage": {
            key: summed_usage[key] - (last_token_total or {}).get(key, 0)
            for key in summed_usage
        },
        "api_cost_equivalent_usd": round(total_cost, 6) if all_priced else None,
        "user_messages": user_messages,
        "spawn_agent_calls": spawn_agent_calls,
    }


def concurrency_metrics(turns: Iterable[dict[str, Any]]) -> dict[str, Any]:
    boundaries: list[tuple[datetime, int]] = []
    for turn in turns:
        start = parse_time(turn.get("started_at"))
        end = parse_time(turn.get("completed_at"))
        if not start or not end or end < start:
            continue
        boundaries.append((start, 1))
        boundaries.append((end, -1))
    boundaries.sort(key=lambda item: (item[0], item[1]))
    current = 0
    previous: datetime | None = None
    seconds_by_concurrency: collections.Counter[int] = collections.Counter()
    maximum = 0
    for timestamp, delta in boundaries:
        if previous is not None:
            seconds_by_concurrency[current] += (timestamp - previous).total_seconds()
        current += delta
        maximum = max(maximum, current)
        previous = timestamp
    active_seconds = sum(value for key, value in seconds_by_concurrency.items() if key > 0)
    worker_seconds = sum(key * value for key, value in seconds_by_concurrency.items() if key > 0)
    return {
        "maximum_concurrent_subagents": maximum,
        "wall_seconds_with_any_subagent": round(active_seconds, 3),
        "wall_seconds_with_two_or_more_subagents": round(
            sum(value for key, value in seconds_by_concurrency.items() if key >= 2), 3
        ),
        "wall_seconds_with_three_or_more_subagents": round(
            sum(value for key, value in seconds_by_concurrency.items() if key >= 3), 3
        ),
        "aggregate_worker_seconds": round(worker_seconds, 3),
        "average_concurrency_while_active": round(worker_seconds / active_seconds, 4)
        if active_seconds else 0,
        "seconds_by_concurrency": {
            str(key): round(value, 3) for key, value in sorted(seconds_by_concurrency.items())
        },
    }


def annotate_user_messages(messages: list[dict[str, Any]]) -> None:
    ordinal = 0
    for message in messages:
        if message["category"] != "user":
            continue
        ordinal += 1
        message["user_message_ordinal"] = ordinal
        message["intervention_kind"] = USER_MESSAGE_KINDS_BY_ORDINAL.get(
            ordinal, "unclassified"
        )
    if ordinal != len(USER_MESSAGE_KINDS_BY_ORDINAL):
        raise ValueError(
            f"Expected {len(USER_MESSAGE_KINDS_BY_ORDINAL)} real user messages, found {ordinal}"
        )


def user_message_metrics(messages: list[dict[str, Any]]) -> dict[str, Any]:
    real = [message for message in messages if message["category"] == "user"]
    counts_by_kind = collections.Counter(
        message["intervention_kind"] for message in real
    )
    counts_by_local_date = collections.Counter(
        message["timestamp_local"][:10] for message in real
    )
    bursts = 0
    previous: datetime | None = None
    for message in real:
        timestamp = parse_time(message["timestamp"])
        if previous is None or timestamp - previous > timedelta(minutes=10):
            bursts += 1
        previous = timestamp
    return {
        "including_initial": len(real),
        "interventions_after_initial": max(0, len(real) - 1),
        "intervention_bursts_10_minute_gap": bursts,
        "counts_by_kind_including_kickoff": dict(counts_by_kind),
        "counts_by_local_date": dict(counts_by_local_date),
        "automatic_goal_continuations": sum(
            message["category"] == "automatic-goal-continuation"
            for message in messages
        ),
        "injected_context_messages": sum(
            message["category"].startswith("injected-") for message in messages
        ),
    }


def git(repo: pathlib.Path, *arguments: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *arguments],
        cwd=repo,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if check and result.returncode:
        raise RuntimeError(
            f"git {' '.join(arguments)} failed ({result.returncode}): {result.stderr.strip()}"
        )
    return result.stdout


def file_at_commit(repo: pathlib.Path, commit: str, relative_path: str) -> str | None:
    result = subprocess.run(
        ["git", "show", f"{commit}:{relative_path}"],
        cwd=repo,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    return result.stdout if result.returncode == 0 else None


def status_from_content(content: str | None) -> str | None:
    if content is None:
        return None
    match = re.search(r"^- Status:\s*(.+)$", content, flags=re.MULTILINE)
    return match.group(1).strip() if match else None


def is_complete_status(status: str | None) -> bool:
    return bool(status and status.startswith("Complete"))


def extract_task_files(repo: pathlib.Path) -> list[dict[str, Any]]:
    task_dir = repo / "plans" / "bundesliga-2026-27" / "tasks"
    rows: list[dict[str, Any]] = []
    for path in sorted(task_dir.glob("p0-*.md")):
        relative = path.relative_to(repo).as_posix()
        base_content = file_at_commit(repo, SESSION_BASE_COMMIT, relative)
        final_content = file_at_commit(repo, SESSION_FINAL_COMMIT, relative)
        if final_content is None:
            continue
        base_status = status_from_content(base_content)
        final_status = status_from_content(final_content)
        title = final_content.splitlines()[0].removeprefix("# ").strip()
        code_match = re.match(r"p0-(\d{1,2})", path.name)
        code = f"P0-{int(code_match.group(1)):02d}" if code_match else "unknown"
        log_text = git(
            repo,
            "log",
            "--reverse",
            "--first-parent",
            "--format=%H%x1f%cI%x1f%s",
            f"{SESSION_BASE_COMMIT}..{SESSION_FINAL_COMMIT}",
            "--",
            relative,
        )
        commits = []
        prior_complete = is_complete_status(base_status)
        completion_transitions = []
        for line in log_text.splitlines():
            if not line:
                continue
            sha, committed_at, subject = line.split("\x1f", 2)
            status = status_from_content(file_at_commit(repo, sha, relative))
            now_complete = is_complete_status(status)
            commit = {
                "sha": sha,
                "committed_at": committed_at,
                "subject": subject,
                "status_after": status,
            }
            commits.append(commit)
            if now_complete and not prior_complete:
                completion_transitions.append(commit)
            prior_complete = now_complete
        completion = completion_transitions[-1] if completion_transitions else None
        rows.append({
            "task_key": path.stem,
            "task_code": code,
            "title": title,
            "path": relative,
            "base_status": base_status,
            "final_status": final_status,
            "session_completed": is_complete_status(final_status)
            and not is_complete_status(base_status),
            "commits_touching": commits,
            "commit_count": len(commits),
            "completion_commit": completion["sha"] if completion else None,
            "completion_committed_at": completion["committed_at"] if completion else None,
            "completion_subject": completion["subject"] if completion else None,
        })
    return rows


def extract_session_commits(repo: pathlib.Path) -> list[dict[str, Any]]:
    output = git(
        repo,
        "log",
        "--reverse",
        "--first-parent",
        "--format=%H%x1f%aI%x1f%cI%x1f%an%x1f%s",
        f"{SESSION_BASE_COMMIT}..{SESSION_FINAL_COMMIT}",
    )
    rows = []
    for line in output.splitlines():
        if not line:
            continue
        sha, authored_at, committed_at, author, subject = line.split("\x1f", 4)
        rows.append({
            "sha": sha,
            "short_sha": sha[:7],
            "authored_at": authored_at,
            "committed_at": committed_at,
            "author": author,
            "subject": subject,
        })
    return rows


def build_task_groups(
    task_files: list[dict[str, Any]], agent_turns: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    completed = [row for row in task_files if row["session_completed"]]
    rows = []
    for code in sorted({row["task_code"] for row in completed}):
        files = [row for row in completed if row["task_code"] == code]
        turns = [turn for turn in agent_turns if turn["task_group"] == code]
        starts = [parse_time(turn["started_at"]) for turn in turns if turn["started_at"]]
        completion_times = [
            parse_time(row["completion_committed_at"])
            for row in files
            if row["completion_committed_at"]
        ]
        observed_start = min(starts) if starts else None
        ledger_completed_at = max(completion_times) if completion_times else None
        turn_ends = [
            parse_time(turn["completed_at"])
            for turn in turns
            if turn["completed_at"]
        ]
        observed_finish_candidates = [
            value for value in [ledger_completed_at, *turn_ends] if value is not None
        ]
        observed_finish = (
            max(observed_finish_candidates) if observed_finish_candidates else None
        )
        commit_shas = {
            commit["sha"] for row in files for commit in row["commits_touching"]
        }
        rows.append({
            "task_group": code,
            "completed_task_files": len(files),
            "task_keys": "; ".join(row["task_key"] for row in files),
            "observed_agent_start": iso_utc(observed_start),
            "observed_agent_start_local": iso_local(observed_start),
            "ledger_completed_at": iso_utc(ledger_completed_at),
            "ledger_completed_at_local": iso_local(ledger_completed_at),
            "observed_finish": iso_utc(observed_finish),
            "observed_finish_local": iso_local(observed_finish),
            "elapsed_seconds": seconds_between(observed_start, observed_finish),
            "agent_worker_seconds": round(
                sum(turn["duration_seconds"] or 0 for turn in turns), 3
            ),
            "agent_turns": len(turns),
            "agent_threads": len({turn["thread_id"] for turn in turns}),
            "agent_paths": "; ".join(sorted({turn["agent_path"] for turn in turns})),
            "commits_touching_task_files": len(commit_shas),
            "completion_commits": "; ".join(
                row["completion_commit"][:7]
                for row in files
                if row["completion_commit"]
            ),
        })

    schadens_turns = [
        turn for turn in agent_turns
        if turn["task_group"] == "schadensfresse-closeout"
    ]
    if schadens_turns:
        start = min(parse_time(turn["started_at"]) for turn in schadens_turns)
        end = max(parse_time(turn["completed_at"]) for turn in schadens_turns)
        rows.append({
            "task_group": "P0-19/21 schadensfresse closeout",
            "completed_task_files": 1,
            "task_keys": "p0-19-schadensfresse-production-copy-workflow-triad",
            "observed_agent_start": iso_utc(start),
            "observed_agent_start_local": iso_local(start),
            "ledger_completed_at": None,
            "ledger_completed_at_local": None,
            "observed_finish": iso_utc(end),
            "observed_finish_local": iso_local(end),
            "elapsed_seconds": seconds_between(start, end),
            "agent_worker_seconds": round(
                sum(turn["duration_seconds"] or 0 for turn in schadens_turns), 3
            ),
            "agent_turns": len(schadens_turns),
            "agent_threads": len({turn["thread_id"] for turn in schadens_turns}),
            "agent_paths": "; ".join(
                sorted({turn["agent_path"] for turn in schadens_turns})
            ),
            "commits_touching_task_files": None,
            "completion_commits": None,
        })
    return rows


def write_csv(path: pathlib.Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field) for field in fields})


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--sessions-dir",
        type=pathlib.Path,
        default=pathlib.Path.home() / ".codex" / "sessions",
    )
    parser.add_argument(
        "--output-dir",
        type=pathlib.Path,
        default=pathlib.Path(__file__).parent / "data",
    )
    parser.add_argument(
        "--repo",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[3],
    )
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    root_candidates = list(args.sessions_dir.rglob(ROOT_LOG_NAME))
    if len(root_candidates) != 1:
        raise SystemExit(f"Expected exactly one root log, found {len(root_candidates)}")
    root_log = root_candidates[0]
    family, guardian_metas = discover_family(args.sessions_dir, root_log)
    if not args.quiet:
        print(f"Discovered {len(family) - 1} descendant agent threads", flush=True)
        print(f"Discovered {len(guardian_metas)} auto-review guardian threads", flush=True)

    root_meta = family[0]
    root = parse_thread(root_meta, set())
    root_turn_ids = {turn["turn_id"] for turn in root["turns"]}
    threads = [root]
    for index, meta in enumerate(family[1:], 1):
        if not args.quiet:
            print(
                f"[{index}/{len(family) - 1}] {meta['agent_path']} ({meta['thread_id']})",
                flush=True,
            )
        threads.append(parse_thread(meta, root_turn_ids))

    guardians = []
    for index, meta in enumerate(guardian_metas, 1):
        if not args.quiet:
            print(
                f"[guardian {index}/{len(guardian_metas)}] {meta['thread_id']}",
                flush=True,
            )
        guardians.append(parse_thread(meta, root_turn_ids))

    subagents = threads[1:]
    agent_turns = [turn for thread in subagents for turn in thread["turns"]]
    task_files = extract_task_files(args.repo)
    task_groups = build_task_groups(task_files, agent_turns)
    commits = extract_session_commits(args.repo)
    all_usage = zero_usage()
    subagent_usage = zero_usage()
    priced_total = 0.0
    all_priced = True
    for thread in threads:
        add_usage(all_usage, thread["usage"])
        if thread["api_cost_equivalent_usd"] is None:
            all_priced = False
        else:
            priced_total += thread["api_cost_equivalent_usd"]
    for thread in subagents:
        add_usage(subagent_usage, thread["usage"])

    annotate_user_messages(root["user_messages"])
    message_metrics = user_message_metrics(root["user_messages"])
    spawn_calls = root["spawn_agent_calls"]
    spawn_fork_turns = collections.Counter(call["fork_turns"] for call in spawn_calls)
    dedicated_ci_threads = [
        thread
        for thread in subagents
        if re.search(r"(?:^|/)ci_|_ci$", thread["agent_path"])
    ]
    guardian_usage = zero_usage()
    for guardian in guardians:
        add_usage(guardian_usage, guardian["usage"])
    summary = {
        "root_thread_id": ROOT_THREAD_ID,
        "session_started_at": root["spawned_at"],
        "session_started_at_local": root["spawned_at_local"],
        "session_last_event_at": root["last_event_at"],
        "session_last_event_at_local": root["last_event_at_local"],
        "session_wall_span_seconds": root["wall_span_seconds"],
        "root_turns": root["turn_count"],
        "root_active_seconds": root["active_seconds"],
        "root_assistant_messages": root["assistant_messages"],
        "root_compactions": root["compactions"],
        "user_messages": message_metrics,
        "subagent_threads": len(subagents),
        "subagent_turns": len(agent_turns),
        "subagent_completed_turns": sum(turn["outcome"] == "completed" for turn in agent_turns),
        "subagent_aborted_turns": sum(turn["outcome"] == "aborted" for turn in agent_turns),
        "subagent_incomplete_turns": sum(turn["outcome"] == "incomplete" for turn in agent_turns),
        "session_commits": len(commits),
        "task_files_completed_in_session": sum(
            task["session_completed"] for task in task_files
        ),
        "root_function_calls": root["function_calls"],
        "root_custom_tool_calls": root["custom_tool_calls"],
        "root_nested_tool_calls": root["nested_tool_calls"],
        "subagent_spawn_selection": {
            "calls": len(spawn_calls),
            "fork_turns_counts": dict(spawn_fork_turns),
            "full_history_inheriting_calls": spawn_fork_turns.get("all", 0),
            "override_compatible_calls": len(spawn_calls) - spawn_fork_turns.get("all", 0),
            "explicit_model_overrides": sum(call["explicit_model"] for call in spawn_calls),
            "explicit_reasoning_effort_overrides": sum(
                call["explicit_reasoning_effort"] for call in spawn_calls
            ),
        },
        "dedicated_ci_agent_lower_bound": {
            "classification": "Agent paths matching (?:^|/)ci_|_ci$ only",
            "threads": len(dedicated_ci_threads),
            "turns": sum(thread["turn_count"] for thread in dedicated_ci_threads),
            "usage": {
                key: sum(thread["usage"][key] for thread in dedicated_ci_threads)
                for key in zero_usage()
            },
            "api_cost_equivalent_usd": round(
                sum(thread["api_cost_equivalent_usd"] or 0 for thread in dedicated_ci_threads),
                6,
            ),
            "agent_paths": [thread["agent_path"] for thread in dedicated_ci_threads],
        },
        "root_usage": root["usage"],
        "root_api_cost_equivalent_usd": root["api_cost_equivalent_usd"],
        "subagent_usage": subagent_usage,
        "subagent_api_cost_equivalent_usd": round(
            priced_total - (root["api_cost_equivalent_usd"] or 0), 6
        ) if all_priced else None,
        "usage": all_usage,
        "api_cost_equivalent_usd": round(priced_total, 6) if all_priced else None,
        "guardian_threads": len(guardians),
        "guardian_turns": sum(guardian["turn_count"] for guardian in guardians),
        "guardian_usage": guardian_usage,
        "concurrency": concurrency_metrics(agent_turns),
    }

    output = {
        "schema_version": 3,
        "generated_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "source": {
            "root_thread_id": ROOT_THREAD_ID,
            "root_log": str(root_log.relative_to(args.sessions_dir)),
            "session_family_rule": "Recursive thread_spawn.parent_thread_id descendants only",
            "complete_message_bodies_included": False,
        },
        "pricing": {
            "as_of": PRICING_AS_OF,
            "currency": "USD",
            "unit": "per 1M tokens",
            "models": MODEL_PRICING,
            "interpretation": "API list-price equivalent, not a Codex subscription charge",
        },
        "summary": summary,
        "user_messages": root["user_messages"],
        "commits": commits,
        "task_files": task_files,
        "task_groups": task_groups,
        "threads": threads,
        "guardians": guardians,
    }
    args.output_dir.mkdir(parents=True, exist_ok=True)
    json_path = args.output_dir / "analysis.json"
    json_path.write_text(json.dumps(output, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    agent_rows = []
    for thread in subagents:
        models = "; ".join(
            f"{item['model']}/{item['reasoning_effort']}:{item['responses']}"
            for item in thread["model_effort_usage"]
        )
        agent_rows.append({
            "thread_id": thread["thread_id"],
            "parent_thread_id": thread["parent_thread_id"],
            "depth": thread["depth"],
            "agent_path": thread["agent_path"],
            "nickname": thread["nickname"],
            "task_group": thread["task_group"],
            "spawned_at": thread["spawned_at"],
            "last_event_at": thread["last_event_at"],
            "wall_span_seconds": thread["wall_span_seconds"],
            "turn_count": thread["turn_count"],
            "completed_turns": thread["completed_turns"],
            "aborted_turns": thread["aborted_turns"],
            "incomplete_turns": thread["incomplete_turns"],
            "active_seconds": thread["active_seconds"],
            "models_and_responses": models,
            **thread["usage"],
            "api_cost_equivalent_usd": thread["api_cost_equivalent_usd"],
            "log_bytes": thread["log_bytes"],
            "log_lines": thread["log_lines"],
        })
    write_csv(
        args.output_dir / "agents.csv",
        agent_rows,
        [
            "thread_id", "parent_thread_id", "depth", "agent_path", "nickname",
            "task_group", "spawned_at", "last_event_at", "wall_span_seconds",
            "turn_count", "completed_turns", "aborted_turns", "incomplete_turns",
            "active_seconds", "models_and_responses", "input_tokens",
            "cached_input_tokens", "cache_write_input_tokens", "output_tokens",
            "reasoning_output_tokens", "total_tokens", "api_cost_equivalent_usd",
            "log_bytes", "log_lines",
        ],
    )
    model_rows = []
    for thread in [*threads, *guardians]:
        for row in thread["model_effort_usage"]:
            model_rows.append({
                "thread_id": thread["thread_id"],
                "kind": thread["kind"],
                "agent_path": thread["agent_path"],
                "model": row["model"],
                "reasoning_effort": row["reasoning_effort"],
                "responses": row["responses"],
                **row["usage"],
                "api_cost_equivalent_usd": row["api_cost_equivalent_usd"],
            })
    write_csv(
        args.output_dir / "model-usage.csv",
        model_rows,
        [
            "thread_id", "kind", "agent_path", "model", "reasoning_effort",
            "responses", "input_tokens", "cached_input_tokens",
            "cache_write_input_tokens", "output_tokens", "reasoning_output_tokens",
            "total_tokens", "api_cost_equivalent_usd",
        ],
    )
    write_csv(
        args.output_dir / "agent-turns.csv",
        agent_turns,
        [
            "thread_id", "agent_path", "task_group", "turn_id", "started_at",
            "started_at_local", "completed_at", "completed_at_local",
            "duration_seconds", "time_to_first_token_seconds", "outcome", "model",
            "reasoning_effort", "error", "result_sha256", "result_excerpt",
        ],
    )
    write_csv(
        args.output_dir / "user-messages.csv",
        root["user_messages"],
        [
            "timestamp", "timestamp_local", "line", "category", "characters",
            "sha256", "user_message_ordinal", "intervention_kind", "excerpt",
        ],
    )
    task_file_rows = []
    for task in task_files:
        task_file_rows.append({
            key: task.get(key) for key in (
                "task_key", "task_code", "title", "path", "base_status",
                "final_status", "session_completed", "commit_count",
                "completion_commit", "completion_committed_at", "completion_subject",
            )
        })
    write_csv(
        args.output_dir / "task-files.csv",
        task_file_rows,
        [
            "task_key", "task_code", "title", "path", "base_status", "final_status",
            "session_completed", "commit_count", "completion_commit",
            "completion_committed_at", "completion_subject",
        ],
    )
    write_csv(
        args.output_dir / "task-groups.csv",
        task_groups,
        [
            "task_group", "completed_task_files", "task_keys",
            "observed_agent_start", "observed_agent_start_local",
            "ledger_completed_at", "ledger_completed_at_local",
            "observed_finish", "observed_finish_local",
            "elapsed_seconds",
            "agent_worker_seconds", "agent_turns", "agent_threads", "agent_paths",
            "commits_touching_task_files", "completion_commits",
        ],
    )
    write_csv(
        args.output_dir / "commits.csv",
        commits,
        [
            "sha", "short_sha", "authored_at", "committed_at", "author", "subject",
        ],
    )
    if not args.quiet:
        print(f"Wrote {json_path}", flush=True)


if __name__ == "__main__":
    main()
