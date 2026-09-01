#!/usr/bin/env python3
"""Derive orchestration, tool-time, Git, and review-cycle evidence for P1."""

from __future__ import annotations

import argparse
import csv
import json
import pathlib
import re
import subprocess
from collections import Counter, defaultdict
from datetime import datetime
from typing import Any


RUN_ID = "01a04fd8-ffcf-7263-944f-98d1bc53c645"
BASE_COMMIT = "c4669aaa1badcccbedbbc1f63c35c412c06a34e8"
P1_10_BASE_COMMIT = "3a2ba35529b262327a3ec08e6bde47b186c8e5b2"
FINAL_COMMIT = "04a6d855bac305c0e35c39d747a5b140a2b65fff"
PATCH_TARGET = re.compile(r"\*\*\* (?:Add|Update|Delete) File: (.+?)(?:\\n|\r?\n)")
NESTED_TOOL = re.compile(r"\btools\.([A-Za-z0-9_]+)\s*\(")
WALL_TIME = re.compile(r"Wall time ([0-9.]+) seconds")


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def classify_role(path: str) -> str:
    if re.search(r"(?:^|/)ci_|_ci$", path):
        return "ci"
    if "review" in path:
        return "review"
    if any(word in path for word in ("audit", "research", "inventory", "monitor", "readiness", "diagnosis", "evidence")):
        return "research-audit"
    if any(word in path for word in ("writer", "foundation", "persistence", "validator", "removal", "join", "bridge", "resolver", "quarantine")):
        return "writer"
    return "other"


def classify_tool(source: str, nested: list[str]) -> str:
    lowered = source.lower()
    if "gh run" in lowered or "gh api" in lowered or "gh pr" in lowered:
        return "github-ci"
    if "dotnet run" in lowered:
        return "dotnet"
    if "tools.apply_patch" in source:
        return "patch"
    if any(token in lowered for token in ("git push", "git merge", "git commit", "git worktree", "new-agentworktree")):
        return "git-worktree-release"
    if "git " in lowered:
        return "git-read"
    if any(token in lowered for token in ("get-content", "rg ", "get-childitem")):
        return "read-search"
    return "+".join(nested) if nested else "other"


def classify_patch_target(targets: list[str]) -> str:
    normalized = [
        re.sub(r"/+", "/", target.replace("\\", "/")).lower()
        for target in targets
    ]
    if any(f".tmp/orchestration/{RUN_ID}/state.md" in target for target in normalized):
        return "orchestration-ledger"
    if any(f".tmp/orchestration/{RUN_ID}/evidence/" in target for target in normalized):
        return "orchestration-evidence"
    if any("new-agentworktree" in target for target in normalized):
        return "worktree-helper"
    return "repository-or-other"


def write_csv(path: pathlib.Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows({field: row.get(field) for field in fields} for row in rows)


def git(repo: pathlib.Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", *arguments], cwd=repo, capture_output=True, text=True,
        encoding="utf-8", errors="replace", check=False,
    )
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    return result.stdout


def git_diff_metrics(repo: pathlib.Path, base: str, final: str) -> dict[str, int]:
    files = insertions = deletions = 0
    for line in git(repo, "diff", "--numstat", f"{base}..{final}").splitlines():
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        files += 1
        if parts[0].isdigit():
            insertions += int(parts[0])
        if parts[1].isdigit():
            deletions += int(parts[1])
    return {"files": files, "insertions": insertions, "deletions": deletions}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--analysis", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data" / "analysis.json")
    parser.add_argument("--output-dir", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data")
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3])
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex" / "sessions")
    args = parser.parse_args()

    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    cutoff = parse_time(
        analysis.get("source", {}).get("event_cutoff_at")
        or analysis["generated_at"]
    )
    tool_rows: list[dict[str, Any]] = []
    for thread in analysis["threads"]:
        log = args.sessions_dir / thread["log_file"]
        calls: dict[str, dict[str, Any]] = {}
        with log.open("r", encoding="utf-8") as handle:
            for line in handle:
                record = json.loads(line)
                record_timestamp = record.get("timestamp")
                if not record_timestamp or parse_time(record_timestamp) > cutoff:
                    continue
                if record.get("type") != "response_item":
                    continue
                payload = record.get("payload", {})
                subtype = payload.get("type")
                call_id = payload.get("call_id")
                if not call_id:
                    continue
                if subtype == "custom_tool_call":
                    source = payload.get("input") or ""
                    nested = NESTED_TOOL.findall(source)
                    targets = PATCH_TARGET.findall(source)
                    calls[call_id] = {
                        "thread_id": thread["thread_id"],
                        "agent_path": thread["agent_path"],
                        "task_group": thread["task_group"],
                        "role": "root" if thread["kind"] == "root" else classify_role(thread["agent_path"]),
                        "call_id": call_id,
                        "started_at": record.get("timestamp"),
                        "nested_tools": ";".join(nested),
                        "tool_class": classify_tool(source, nested),
                        "patch_target_class": classify_patch_target(targets) if targets else "",
                        "patch_targets": ";".join(targets),
                    }
                elif subtype == "custom_tool_call_output" and call_id in calls:
                    output = payload.get("output") or []
                    text = "\n".join(
                        item.get("text", "") for item in output if isinstance(item, dict)
                    ) if isinstance(output, list) else str(output)
                    row = calls.pop(call_id)
                    completed = record.get("timestamp")
                    row["completed_at"] = completed
                    row["elapsed_seconds"] = round(
                        (parse_time(completed) - parse_time(row["started_at"])).total_seconds(), 3
                    ) if completed and row["started_at"] else None
                    match = WALL_TIME.search(text)
                    row["reported_wall_seconds"] = float(match.group(1)) if match else None
                    tool_rows.append(row)

    write_csv(
        args.output_dir / "tool-timings.csv", tool_rows,
        ["thread_id", "agent_path", "task_group", "role", "call_id", "started_at", "completed_at", "elapsed_seconds", "reported_wall_seconds", "nested_tools", "tool_class", "patch_target_class", "patch_targets"],
    )

    commit_rows: list[dict[str, Any]] = []
    for commit in analysis["commits"]:
        stat = git(args.repo, "show", "--format=", "--numstat", commit["sha"])
        files = insertions = deletions = 0
        for line in stat.splitlines():
            parts = line.split("\t", 2)
            if len(parts) != 3:
                continue
            files += 1
            if parts[0].isdigit():
                insertions += int(parts[0])
            if parts[1].isdigit():
                deletions += int(parts[1])
        commit_rows.append({
            **commit,
            "files_changed": files,
            "insertions": insertions,
            "deletions": deletions,
        })
    write_csv(
        args.output_dir / "commit-stats.csv", commit_rows,
        ["sha", "short_sha", "authored_at", "committed_at", "author", "subject", "files_changed", "insertions", "deletions"],
    )

    refs = git(
        args.repo, "for-each-ref", "--format=%(refname:short)%09%(objectname)%09%(committerdate:iso-strict)%09%(subject)",
        "refs/remotes/origin/codex/p1-*", "refs/heads/codex/p1-*",
    )
    branch_rows = []
    for line in refs.splitlines():
        if not line:
            continue
        ref, sha, committed_at, subject = line.split("\t", 3)
        if parse_time(committed_at) > cutoff:
            continue
        branch_rows.append({"ref": ref, "sha": sha, "committed_at": committed_at, "subject": subject})
    write_csv(args.output_dir / "branches.csv", branch_rows, ["ref", "sha", "committed_at", "subject"])

    review_rows = []
    for thread in analysis["threads"]:
        if not re.match(r"^/root/p1_10.*review", thread["agent_path"]):
            continue
        for turn in thread["turns"]:
            if turn["outcome"] != "completed":
                continue
            excerpt = turn.get("result_excerpt") or ""
            prefix = excerpt[:100]
            approved = bool(re.search(r"(?i)^approved|^no findings|^findings:\s*none|^## findings\s+no findings", prefix))
            review_rows.append({
                "agent_path": thread["agent_path"],
                "started_at": turn["started_at"],
                "duration_seconds": turn["duration_seconds"],
                "verdict": "approved" if approved else "rejected-or-findings",
                "result_sha256": turn["result_sha256"],
                "result_excerpt": excerpt,
            })
    write_csv(
        args.output_dir / "review-turns.csv", review_rows,
        ["agent_path", "started_at", "duration_seconds", "verdict", "result_sha256", "result_excerpt"],
    )

    by_tool = defaultdict(lambda: {"calls": 0, "elapsed_seconds": 0.0, "reported_wall_seconds": 0.0})
    for row in tool_rows:
        bucket = by_tool[row["tool_class"]]
        bucket["calls"] += 1
        bucket["elapsed_seconds"] += row["elapsed_seconds"] or 0
        bucket["reported_wall_seconds"] += row["reported_wall_seconds"] or 0
    patch_targets = Counter(
        row["patch_target_class"]
        for row in tool_rows
        if row["role"] == "root" and row["patch_target_class"]
    )
    root_ledger_rows = [
        row for row in tool_rows
        if row["role"] == "root" and row["patch_target_class"] == "orchestration-ledger"
    ]
    subagent_tool_seconds = sum(row["elapsed_seconds"] or 0 for row in tool_rows if row["role"] != "root")
    subagent_active_seconds = analysis["summary"]["concurrency"]["aggregate_worker_seconds"]
    review_verdicts = Counter(row["verdict"] for row in review_rows)
    reviews_by_lane: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in review_rows:
        reviews_by_lane[row["agent_path"]].append(row)
    approval_complete_lanes = [
        rows for rows in reviews_by_lane.values()
        if any(row["verdict"] == "approved" for row in rows)
    ]

    session_net_changes = git_diff_metrics(args.repo, BASE_COMMIT, FINAL_COMMIT)
    p1_10_net_changes = git_diff_metrics(args.repo, P1_10_BASE_COMMIT, FINAL_COMMIT)
    p1_10_commits = set(
        git(args.repo, "rev-list", f"{P1_10_BASE_COMMIT}..{FINAL_COMMIT}").splitlines()
    )
    with (args.output_dir / "ci-runs.csv").open(encoding="utf-8", newline="") as handle:
        ci_rows = list(csv.DictReader(handle))
    p1_10_ci_rows = [row for row in ci_rows if row["head_sha"] in p1_10_commits]

    def ci_metrics(rows: list[dict[str, str]]) -> dict[str, int]:
        return {
            "runs": len(rows),
            "seconds": sum(int(row["duration_seconds"]) for row in rows),
            "failures": sum(row["conclusion"] != "success" for row in rows),
        }

    derived = {
        "schema_version": 2,
        "source_analysis_generated_at": analysis["generated_at"],
        "session_boundary": {"base_commit": BASE_COMMIT, "final_commit": FINAL_COMMIT},
        "p1_10_boundary": {"base_commit": P1_10_BASE_COMMIT, "final_commit": FINAL_COMMIT},
        "tool_calls": len(tool_rows),
        "tool_time_by_class": {
            key: {
                "calls": value["calls"],
                "elapsed_seconds": round(value["elapsed_seconds"], 3),
                "reported_wall_seconds": round(value["reported_wall_seconds"], 3),
            }
            for key, value in sorted(by_tool.items())
        },
        "root_patch_target_counts": dict(sorted(patch_targets.items())),
        "root_ledger_elapsed_seconds": round(
            sum(row["elapsed_seconds"] or 0 for row in root_ledger_rows), 3
        ),
        "subagent_tool_elapsed_seconds": round(subagent_tool_seconds, 3),
        "subagent_active_seconds": subagent_active_seconds,
        "subagent_tool_share_of_active_time": round(subagent_tool_seconds / subagent_active_seconds, 4) if subagent_active_seconds else None,
        "review_verdict_counts": dict(review_verdicts),
        "review_lane_counts": {
            "with_completed_turn": len(reviews_by_lane),
            "approval_complete": len(approval_complete_lanes),
            "approval_complete_with_multiple_turns": sum(
                len(rows) > 1 for rows in approval_complete_lanes
            ),
        },
        "commits": len(commit_rows),
        "p1_10_commits": len(p1_10_commits),
        "net_changes": {
            "session": session_net_changes,
            "p1_10": p1_10_net_changes,
        },
        "ci_run_counts": {
            "session": ci_metrics(ci_rows),
            "p1_10": ci_metrics(p1_10_ci_rows),
        },
        "commit_files_changed_sum": sum(row["files_changed"] for row in commit_rows),
        "commit_insertions_sum": sum(row["insertions"] for row in commit_rows),
        "commit_deletions_sum": sum(row["deletions"] for row in commit_rows),
        "local_and_remote_branch_refs": len(branch_rows),
    }
    (args.output_dir / "derived-metrics.json").write_text(
        json.dumps(derived, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
