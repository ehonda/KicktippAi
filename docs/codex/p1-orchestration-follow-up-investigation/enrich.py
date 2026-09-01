#!/usr/bin/env python3
"""Derive comparative workflow evidence for the successor P1 snapshot."""

from __future__ import annotations

import csv
import argparse
import importlib.util
import json
import pathlib
import re
import subprocess
import sys
from collections import Counter
from datetime import datetime
from typing import Any


RUN_ID = "01a054ee-67b4-7ab2-a0b4-c9ffabc2da2e"
BASE_COMMIT = "71637cc154cfdcbe2436069470b5e04b0d4f753d"
FINAL_COMMIT = "5891d480ed1ac117c955a29b02dd940fd2d6187f"
PAUSE_SECONDS = 37_818.245
OLD_PAUSE_SECONDS = 20_574


def load_shared_enricher() -> Any:
    path = pathlib.Path(__file__).parents[1] / "p1-orchestration-interim-investigation" / "enrich.py"
    spec = importlib.util.spec_from_file_location("p1_session_enricher", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load shared enricher at {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def git(repo: pathlib.Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", *arguments], cwd=repo, capture_output=True, text=True,
        encoding="utf-8", errors="replace", check=False,
    )
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    return result.stdout


def write_csv(path: pathlib.Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows({field: row.get(field) for field in fields} for row in rows)


def classify_role(path: str) -> str:
    if "architecture" in path:
        return "architecture-lead"
    if "spec_review" in path:
        return "specification-review"
    if "review" in path:
        return "correctness-review"
    if any(word in path for word in ("ci_reconcile", "run_reconcile", "validation")):
        return "validation-reconciliation"
    if any(word in path for word in ("audit", "analysis", "preflight")):
        return "audit-analysis"
    if any(word in path for word in ("docs", "runtime", "fix")):
        return "writer"
    return "other"


def review_kind(path: str, excerpt: str) -> str:
    if path.endswith("p1_spec_review") or path.endswith("p1_full_branch_spec_review"):
        return "design-specification"
    if path.endswith("p1_10_next_wave_spec_review"):
        if any(token in excerpt for token in ("b9e6077", "711595a", "426fc4f", "5891d48")):
            return "R1-artifact"
        if any(token in excerpt for token in ("d0854f3", "d0578a1")):
            return "R0-artifact"
        return "design-specification"
    if path.endswith("recovery_docs_review"):
        return "recovery-docs-artifact"
    if path.endswith("recovery_b_review"):
        return "recovery-runtime-artifact"
    if path.endswith("p1_12_ci_fix_review"):
        return "P1-12-artifact"
    if path.endswith("p1_full_branch_seed_review"):
        return "full-branch-seed-artifact"
    if path.endswith("p1_seed_context_fix_review"):
        return "portable-test-artifact"
    return "other-review"


def review_verdict(excerpt: str) -> str:
    prefix = excerpt[:120].upper()
    if prefix.startswith("REJECT") or prefix.startswith("FINDINGS"):
        return "findings"
    if "ACCEPT-WITH-FIXES" in prefix:
        return "accept-with-fixes"
    if prefix.startswith("ACCEPT") or prefix.startswith("APPROVED") or prefix.startswith("NO FINDINGS"):
        return "accepted"
    return "other"


def rate(value: float, seconds: float) -> float:
    return value / (seconds / 3600) if seconds else 0.0


def pct_change(old: float, new: float) -> float:
    return (new / old - 1) if old else 0.0


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--analysis", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data" / "analysis.json")
    parser.add_argument("--output-dir", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data")
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3])
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex" / "sessions")
    args = parser.parse_args()
    data_dir = args.output_dir
    repo = args.repo
    old_data = pathlib.Path(__file__).parents[1] / "p1-orchestration-interim-investigation" / "data"

    module = load_shared_enricher()
    module.RUN_ID = RUN_ID
    module.BASE_COMMIT = BASE_COMMIT
    module.P1_10_BASE_COMMIT = BASE_COMMIT
    module.FINAL_COMMIT = FINAL_COMMIT
    module.classify_role = classify_role
    original_classify_tool = module.classify_tool

    def classify_tool(source: str, nested: list[str]) -> str:
        if "Get-OrchestrationResourceSnapshot" in source:
            return "resource-admission"
        return original_classify_tool(source, nested)

    module.classify_tool = classify_tool
    module.main()

    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    cutoff = parse_time(
        analysis.get("source", {}).get("event_cutoff_at")
        or analysis["generated_at"]
    )
    old_analysis = json.loads((old_data / "analysis.json").read_text(encoding="utf-8"))
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    old_derived = json.loads((old_data / "derived-metrics.json").read_text(encoding="utf-8"))

    branch_snapshot = json.loads((data_dir / "branch-snapshot.json").read_text(encoding="utf-8"))
    branch_rows = branch_snapshot["rows"]
    if branch_snapshot.get("schema_version") != 1:
        raise ValueError("branch-snapshot.json must use schema 1")
    if parse_time(branch_snapshot["captured_at"]) < cutoff:
        raise ValueError("branch snapshot predates the event cutoff")
    if any(parse_time(row["committed_at"]) > cutoff for row in branch_rows):
        raise ValueError("branch snapshot contains a post-cutoff commit")
    write_csv(data_dir / "branches.csv", branch_rows, ["ref", "sha", "committed_at", "subject"])

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
                "review_kind": review_kind(thread["agent_path"], excerpt),
                "verdict": review_verdict(excerpt),
                "result_sha256": turn["result_sha256"],
                "result_excerpt": excerpt,
            })
    write_csv(
        data_dir / "review-turns.csv", review_rows,
        ["agent_path", "started_at", "duration_seconds", "review_kind", "verdict", "result_sha256", "result_excerpt"],
    )

    old_summary = old_analysis["summary"]
    new_summary = analysis["summary"]
    old_effective = old_summary["session_wall_span_seconds"] - OLD_PAUSE_SECONDS
    new_effective = new_summary["session_wall_span_seconds"] - PAUSE_SECONDS
    old_usage = old_summary["usage"]
    new_usage = new_summary["usage"]
    old_non_cached = old_usage["input_tokens"] - old_usage["cached_input_tokens"] + old_usage["output_tokens"]
    new_non_cached = new_usage["input_tokens"] - new_usage["cached_input_tokens"] + new_usage["output_tokens"]
    old_worker_usage = old_summary["subagent_usage"]
    new_worker_usage = new_summary["subagent_usage"]
    old_worker_non_cached = old_worker_usage["input_tokens"] - old_worker_usage["cached_input_tokens"] + old_worker_usage["output_tokens"]
    new_worker_non_cached = new_worker_usage["input_tokens"] - new_worker_usage["cached_input_tokens"] + new_worker_usage["output_tokens"]
    old_worker_active_seconds = sum(thread["active_seconds"] for thread in old_analysis["threads"] if thread["kind"] == "subagent")
    new_worker_active_seconds = sum(thread["active_seconds"] for thread in analysis["threads"] if thread["kind"] == "subagent")

    with (data_dir / "tool-timings.csv").open(encoding="utf-8", newline="") as handle:
        tools = list(csv.DictReader(handle))
    resource_calls = [row for row in tools if row["tool_class"] == "resource-admission"]
    dotnet_calls = [row for row in tools if row["tool_class"] == "dotnet"]
    events: list[tuple[datetime, int]] = []
    for row in dotnet_calls:
        if row["started_at"] and row["completed_at"]:
            events.append((parse_time(row["started_at"]), 1))
            events.append((parse_time(row["completed_at"]), -1))
    active = maximum_dotnet_overlap = 0
    for _, delta in sorted(events, key=lambda item: (item[0], item[1])):
        active += delta
        maximum_dotnet_overlap = max(maximum_dotnet_overlap, active)

    verdicts = Counter(row["verdict"] for row in review_rows)
    artifact_rows = [row for row in review_rows if row["review_kind"] != "design-specification"]
    artifact_verdicts = Counter(row["verdict"] for row in artifact_rows)
    design_rows = [row for row in review_rows if row["review_kind"] == "design-specification"]
    design_verdicts = Counter(row["verdict"] for row in design_rows)
    ci_rows = list(csv.DictReader((data_dir / "ci-runs.csv").open(encoding="utf-8", newline="")))
    main_ci = [row for row in ci_rows if row["event"] == "push"]
    pr_ci = [row for row in ci_rows if row["event"] == "pull_request"]

    old_tokens_per_hour = rate(old_usage["total_tokens"], old_effective)
    new_tokens_per_hour = rate(new_usage["total_tokens"], new_effective)
    old_non_cached_per_hour = rate(old_non_cached, old_effective)
    new_non_cached_per_hour = rate(new_non_cached, new_effective)
    old_worker_non_cached_per_hour = rate(old_worker_non_cached, old_worker_active_seconds)
    new_worker_non_cached_per_hour = rate(new_worker_non_cached, new_worker_active_seconds)

    comparison = {
        "schema_version": 2,
        "snapshot": {
            "old_run": old_summary["root_thread_id"],
            "new_run": new_summary["root_thread_id"],
            "old_effective_seconds": round(old_effective, 3),
            "new_effective_seconds": round(new_effective, 3),
            "old_pause_seconds": OLD_PAUSE_SECONDS,
            "new_pause_seconds": PAUSE_SECONDS,
        },
        "parallelism": {
            "old_average_while_active": old_summary["concurrency"]["average_concurrency_while_active"],
            "new_average_while_active": new_summary["concurrency"]["average_concurrency_while_active"],
            "old_two_plus_share": round(old_summary["concurrency"]["wall_seconds_with_two_or_more_subagents"] / old_summary["concurrency"]["wall_seconds_with_any_subagent"], 4),
            "new_two_plus_share": round(new_summary["concurrency"]["wall_seconds_with_two_or_more_subagents"] / new_summary["concurrency"]["wall_seconds_with_any_subagent"], 4),
            "old_maximum": old_summary["concurrency"]["maximum_concurrent_subagents"],
            "new_maximum": new_summary["concurrency"]["maximum_concurrent_subagents"],
            "old_worker_seconds": old_summary["concurrency"]["aggregate_worker_seconds"],
            "new_worker_seconds": new_summary["concurrency"]["aggregate_worker_seconds"],
            "old_worker_seconds_per_effective_wall_second": round(old_summary["concurrency"]["aggregate_worker_seconds"] / old_effective, 4),
            "new_worker_seconds_per_effective_wall_second": round(new_summary["concurrency"]["aggregate_worker_seconds"] / new_effective, 4),
        },
        "efficiency": {
            "old_logged_tokens_per_effective_hour": round(old_tokens_per_hour),
            "new_logged_tokens_per_effective_hour": round(new_tokens_per_hour),
            "logged_token_rate_change": round(pct_change(old_tokens_per_hour, new_tokens_per_hour), 4),
            "old_non_cached_plus_output_per_effective_hour": round(old_non_cached_per_hour),
            "new_non_cached_plus_output_per_effective_hour": round(new_non_cached_per_hour),
            "non_cached_plus_output_rate_change": round(pct_change(old_non_cached_per_hour, new_non_cached_per_hour), 4),
            "old_worker_tokens_per_worker_hour": round(rate(old_summary["subagent_usage"]["total_tokens"], old_summary["concurrency"]["aggregate_worker_seconds"])),
            "new_worker_tokens_per_worker_hour": round(rate(new_summary["subagent_usage"]["total_tokens"], new_summary["concurrency"]["aggregate_worker_seconds"])),
            "old_worker_non_cached_plus_output_tokens_per_worker_hour": round(old_worker_non_cached_per_hour),
            "new_worker_non_cached_plus_output_tokens_per_worker_hour": round(new_worker_non_cached_per_hour),
            "worker_non_cached_plus_output_rate_change": round(pct_change(old_worker_non_cached_per_hour, new_worker_non_cached_per_hour), 4),
            "root_share_old": round(old_summary["root_usage"]["total_tokens"] / old_usage["total_tokens"], 4),
            "root_share_new": round(new_summary["root_usage"]["total_tokens"] / new_usage["total_tokens"], 4),
            "old_root_tokens_per_effective_hour": round(rate(old_summary["root_usage"]["total_tokens"], old_effective)),
            "new_root_tokens_per_effective_hour": round(rate(new_summary["root_usage"]["total_tokens"], new_effective)),
        },
        "coordination": {
            "old_threads": old_summary["subagent_threads"],
            "new_threads": new_summary["subagent_threads"],
            "old_turns_per_thread": round(old_summary["subagent_turns"] / old_summary["subagent_threads"], 3),
            "new_turns_per_thread": round(new_summary["subagent_turns"] / new_summary["subagent_threads"], 3),
            "old_waits_per_effective_hour": round(rate(old_summary["root_function_calls"]["wait_agent"], old_effective), 2),
            "new_waits_per_effective_hour": round(rate(new_summary["root_function_calls"]["wait_agent"], new_effective), 2),
            "old_ledger_patches_per_effective_hour": round(rate(old_derived["root_patch_target_counts"]["orchestration-ledger"], old_effective), 2),
            "new_ledger_patches_per_effective_hour": round(rate(derived["root_patch_target_counts"].get("orchestration-ledger", 0), new_effective), 2),
            "old_compactions_per_effective_hour": round(rate(old_summary["root_compactions"], old_effective), 3),
            "new_compactions_per_effective_hour": round(rate(new_summary["root_compactions"], new_effective), 3),
        },
        "publication": {
            "old_ci_runs": old_derived["ci_run_counts"]["session"]["runs"],
            "new_ci_runs": len(ci_rows),
            "new_main_ci_runs": len(main_ci),
            "new_pr_ci_runs": len(pr_ci),
            "old_ci_seconds": old_derived["ci_run_counts"]["session"]["seconds"],
            "new_ci_seconds": sum(int(row["duration_seconds"]) for row in ci_rows),
            "new_remote_run_branches": sum(row["ref"].startswith("origin/") for row in branch_rows),
            "new_draft_prs": 1,
            "old_session_commits": old_summary["session_commits"],
            "new_session_commits": new_summary["session_commits"],
        },
        "reviews": {
            "all": dict(verdicts),
            "design": dict(design_verdicts),
            "artifact": dict(artifact_verdicts),
            "R1_turns": sum(row["review_kind"] == "R1-artifact" for row in review_rows),
            "R1_findings": sum(row["review_kind"] == "R1-artifact" and row["verdict"] == "findings" for row in review_rows),
        },
        "resources": {
            "resource_admission_calls": len(resource_calls),
            "dotnet_calls": len(dotnet_calls),
            "maximum_overlapping_classified_dotnet_calls": maximum_dotnet_overlap,
            "maximum_linked_task_worktrees_observed_in_ledger": 2,
            "heavy_operation_limit": 1,
            "latest_memory_admission_denied": True,
        },
    }
    (data_dir / "comparison-metrics.json").write_text(
        json.dumps(comparison, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    derived["current_run_branches"] = len(branch_rows)
    derived["review_verdict_counts"] = dict(verdicts)
    derived["review_kind_counts"] = dict(Counter(row["review_kind"] for row in review_rows))
    derived["resource_admission_calls"] = len(resource_calls)
    derived["maximum_overlapping_classified_dotnet_calls"] = maximum_dotnet_overlap
    (data_dir / "derived-metrics.json").write_text(
        json.dumps(derived, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
