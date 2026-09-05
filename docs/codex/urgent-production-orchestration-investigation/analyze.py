#!/usr/bin/env python3
"""Extract the frozen urgent-production orchestration session family."""

from __future__ import annotations

import importlib.util
import pathlib
import re
import sys
from typing import Any


ROOT_THREAD_ID = "01a06e72-0c2f-76a3-8d2b-d48f9131a1d5"
ROOT_LOG_NAME = (
    "rollout-2026-09-05T00-02-43-"
    "01a06e72-0c2f-76a3-8d2b-d48f9131a1d5.jsonl"
)
SESSION_BASE_COMMIT = "55491b2ec642666938eb2517eaa150cb3695d048"
SESSION_FINAL_COMMIT = "538c30c53870faa608cf0d6e6a9dbf20f8d833d3"
SESSION_EVENT_CUTOFF = "2026-09-05T14:44:05.022Z"
SNAPSHOT_GENERATED_AT = "2026-09-05T14:44:28.3992644Z"


def load_shared_extractor() -> Any:
    path = pathlib.Path(__file__).parents[1] / "p0-closeout-session-investigation" / "analyze.py"
    spec = importlib.util.spec_from_file_location("shared_session_analyzer", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load shared transcript extractor at {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def classify_task_group(agent_path: str) -> str:
    normalized = agent_path.lower()
    if "source_repair" in normalized or "restoration" in normalized:
        return "history-restoration"
    if "cl_" in normalized or "champions" in normalized:
        return "champions-league-bonus"
    if "natural_watch" in normalized:
        return "production-validation"
    if "p1_inventory" in normalized:
        return "P1-planning"
    if "urgent_architecture" in normalized:
        return "architecture"
    return "cross-cutting"


def classify_turn_task_group(agent_path: str, result: str) -> str:
    if re.search(r"\bP1[-_ ]?0?[45]\b", result, flags=re.IGNORECASE):
        return "P1-04/05-planning"
    return classify_task_group(agent_path)


def classify_root_user_message(text: str) -> str:
    if text.startswith("# AGENTS.md instructions for "):
        return "injected-repository-context"
    if text.startswith("<environment_context>"):
        return "injected-environment-context"
    if text.startswith('<codex_internal_context source="goal">'):
        return "automatic-goal-continuation"
    if text.startswith("<skill>"):
        return "injected-skill-context"
    return "user"


def annotate_user_messages(messages: list[dict[str, Any]]) -> None:
    ordinal = 0
    for message in messages:
        if message["category"] != "user":
            continue
        ordinal += 1
        message["user_message_ordinal"] = ordinal
        message["intervention_kind"] = "kickoff" if ordinal == 1 else "owner-intervention"


def extract_p1_task_files(module: Any, repo: pathlib.Path) -> list[dict[str, Any]]:
    task_dir = repo / "plans" / "bundesliga-2026-27" / "tasks"
    rows: list[dict[str, Any]] = []
    for path in sorted(task_dir.glob("p1-*.md")):
        relative = path.relative_to(repo).as_posix()
        base_content = module.file_at_commit(repo, SESSION_BASE_COMMIT, relative)
        final_content = module.file_at_commit(repo, SESSION_FINAL_COMMIT, relative)
        if final_content is None:
            continue
        base_status = module.status_from_content(base_content)
        final_status = module.status_from_content(final_content)
        title = final_content.splitlines()[0].removeprefix("# ").strip()
        code_match = re.match(r"p1-(\d{1,2})", path.name)
        code = f"P1-{int(code_match.group(1)):02d}" if code_match else "unknown"
        log_text = module.git(
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
        prior_complete = module.is_complete_status(base_status)
        completion_transitions = []
        for line in log_text.splitlines():
            if not line:
                continue
            sha, committed_at, subject = line.split("\x1f", 2)
            status = module.status_from_content(module.file_at_commit(repo, sha, relative))
            now_complete = module.is_complete_status(status)
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
            "session_completed": module.is_complete_status(final_status)
            and not module.is_complete_status(base_status),
            "commits_touching": commits,
            "commit_count": len(commits),
            "completion_commit": completion["sha"] if completion else None,
            "completion_committed_at": completion["committed_at"] if completion else None,
            "completion_subject": completion["subject"] if completion else None,
        })
    return rows


def main() -> None:
    module = load_shared_extractor()
    module.ROOT_THREAD_ID = ROOT_THREAD_ID
    module.ROOT_LOG_NAME = ROOT_LOG_NAME
    module.SESSION_BASE_COMMIT = SESSION_BASE_COMMIT
    module.SESSION_FINAL_COMMIT = SESSION_FINAL_COMMIT
    module.EVENT_CUTOFF_UTC = (
        module.parse_time(SESSION_EVENT_CUTOFF) if SESSION_EVENT_CUTOFF else None
    )
    module.ANALYSIS_GENERATED_AT_UTC = SNAPSHOT_GENERATED_AT
    module.PRICING_AS_OF = "2026-09-05"
    module.MODEL_PRICING = {
        "gpt-6-astra": {
            "input": 10.0,
            "cached_input": 1.0,
            "cache_write_input": 12.5,
            "output": 50.0,
            "source_url": "https://developers.openai.com/api/docs/pricing",
        },
        "gpt-5.6-sol": {
            "input": 4.0,
            "cached_input": 0.4,
            "cache_write_input": 5.0,
            "output": 20.0,
            "source_url": "https://developers.openai.com/api/docs/pricing",
        },
        "gpt-5.6-terra": {
            "input": 2.0,
            "cached_input": 0.2,
            "cache_write_input": 2.5,
            "output": 12.0,
            "source_url": "https://developers.openai.com/api/docs/pricing",
        },
        "gpt-5.6-luna": {
            "input": 0.2,
            "cached_input": 0.02,
            "cache_write_input": 0.25,
            "output": 1.2,
            "source_url": "https://developers.openai.com/api/docs/pricing",
        },
    }
    module.task_group = classify_task_group
    module.turn_task_group = classify_turn_task_group
    module.classify_root_user_message = classify_root_user_message
    module.annotate_user_messages = annotate_user_messages
    module.extract_task_files = lambda repo: extract_p1_task_files(module, repo)
    module.main()


if __name__ == "__main__":
    main()
