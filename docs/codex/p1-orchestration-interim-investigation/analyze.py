#!/usr/bin/env python3
"""Extract a reproducible interim snapshot of the August 2026 P1 run.

The mature P0 extractor owns the transcript parser. This adapter supplies the
P1 session boundary and task classifier, then preserves the same normalized
JSON/CSV schema so the two investigations remain directly comparable.
"""

from __future__ import annotations

import importlib.util
import pathlib
import re
import sys
from typing import Any


ROOT_THREAD_ID = "01a04fd8-ffcf-7263-944f-98d1bc53c645"
ROOT_LOG_NAME = (
    "rollout-2026-08-30T01-26-56-"
    "01a04fd8-ffcf-7263-944f-98d1bc53c645.jsonl"
)
SESSION_BASE_COMMIT = "c4669aaa1badcccbedbbc1f63c35c412c06a34e8"
SESSION_FINAL_COMMIT = "04a6d855bac305c0e35c39d747a5b140a2b65fff"


def load_shared_extractor() -> Any:
    path = pathlib.Path(__file__).parents[1] / "p0-closeout-session-investigation" / "analyze.py"
    spec = importlib.util.spec_from_file_location("p0_session_analyzer", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load shared transcript extractor at {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def classify_task_group(agent_path: str) -> str:
    normalized = agent_path.lower().replace("-", "_")
    match = re.search(r"p1_(\d{1,2})(?:_|$)", normalized)
    if match:
        return f"P1-{int(match.group(1)):02d}"
    if any(word in normalized for word in ("ci", "reconcile", "status")):
        return "orchestration-gates"
    if any(word in normalized for word in ("lookahead", "scope", "audit")):
        return "orchestration-discovery"
    return "cross-cutting"


def classify_turn_task_group(agent_path: str, result: str) -> str:
    raw_matches = list(re.finditer(r"\bP1[-_ ]?(\d{1,2})\b", result, flags=re.IGNORECASE))
    codes: list[str] = []
    for match in raw_matches:
        code = f"P1-{int(match.group(1)):02d}"
        if code not in codes:
            codes.append(code)
    if raw_matches and raw_matches[0].start() < 80:
        return f"P1-{int(raw_matches[0].group(1)):02d}"
    if len(codes) == 1:
        return codes[0]
    return classify_task_group(agent_path)


def annotate_user_messages(messages: list[dict[str, Any]]) -> None:
    ordinal = 0
    for message in messages:
        if message["category"] != "user":
            continue
        ordinal += 1
        message["user_message_ordinal"] = ordinal
        message["intervention_kind"] = "kickoff" if ordinal == 1 else "user-intervention"


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
    module.task_group = classify_task_group
    module.turn_task_group = classify_turn_task_group
    module.classify_root_user_message = classify_root_user_message
    module.annotate_user_messages = annotate_user_messages
    module.extract_task_files = lambda repo: extract_p1_task_files(module, repo)
    module.main()


if __name__ == "__main__":
    main()
