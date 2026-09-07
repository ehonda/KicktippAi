#!/usr/bin/env python3
"""Run the stable session extractor from a declarative report manifest."""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import importlib.util
import json
import pathlib
import re
import subprocess
import sys
from typing import Any
from zoneinfo import ZoneInfo

from focus_registry import RegistryError, require
from report_manifest import read_manifest


SECRET_PATTERNS = (
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"\b(?:sk-(?:proj-)?|ghp_|github_pat_|glpat-)[A-Za-z0-9_-]{16,}"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bBearer\s+[A-Za-z0-9._~+/=-]{20,}", flags=re.IGNORECASE),
)


def load_engine() -> Any:
    path = pathlib.Path(__file__).with_name("extractor_core.py")
    spec = importlib.util.spec_from_file_location("session_analysis_extractor_core", path)
    if spec is None or spec.loader is None:
        raise RegistryError(f"Cannot load extraction core at {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def format_code(match: re.Match[str]) -> str:
    return f"{match.group('prefix').upper()}-{int(match.group('number')):02d}"


def configure_classification(module: Any, config: dict[str, Any]) -> None:
    classification = config["classification"]
    rules = [
        (re.compile(rule["pattern"], flags=re.IGNORECASE), rule["group"])
        for rule in classification["agent_rules"]
    ]
    turn_regex = (
        re.compile(classification["turn_code_regex"], flags=re.IGNORECASE)
        if classification["turn_code_regex"] else None
    )

    def task_group(agent_path: str) -> str:
        normalized = agent_path.lower().replace("-", "_")
        for pattern, group in rules:
            if pattern.search(normalized):
                return group
        return classification["default_group"]

    def turn_task_group(agent_path: str, result: str) -> str:
        base = task_group(agent_path)
        if turn_regex is None:
            return base
        raw_matches = list(turn_regex.finditer(result))
        codes: list[str] = []
        for match in raw_matches:
            code = format_code(match)
            if code not in codes:
                codes.append(code)
        if raw_matches and raw_matches[0].start() < 80:
            return format_code(raw_matches[0])
        if len(codes) == 1:
            return codes[0]
        return base

    module.task_group = task_group
    module.turn_task_group = turn_task_group


def configure_user_messages(module: Any, config: dict[str, Any]) -> None:
    prefixes = config["injected_message_prefixes"]
    kinds = {int(key): value for key, value in config["user_message_kinds"].items()}

    def classify_root_user_message(text: str) -> str:
        for item in prefixes:
            if text.startswith(item["prefix"]):
                return item["category"]
        return "user"

    def annotate_user_messages(messages: list[dict[str, Any]]) -> None:
        ordinal = 0
        for message in messages:
            if message["category"] != "user":
                continue
            ordinal += 1
            message["user_message_ordinal"] = ordinal
            message["intervention_kind"] = kinds.get(ordinal, "owner-intervention")

    module.classify_root_user_message = classify_root_user_message
    module.annotate_user_messages = annotate_user_messages


def git(repo: pathlib.Path, *arguments: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *arguments], cwd=repo, capture_output=True, text=True,
        encoding="utf-8", errors="replace", check=False,
    )
    if check and result.returncode:
        raise RegistryError(
            f"git {' '.join(arguments)} failed ({result.returncode}): {result.stderr.strip()}"
        )
    return result.stdout


def configure_task_files(module: Any, config: dict[str, Any]) -> None:
    task_config = config["task_files"]
    patterns = task_config["globs"]
    code_regex = re.compile(task_config["code_regex"], flags=re.IGNORECASE)
    base_commit = config["repository"]["base_commit"]
    final_commit = config["repository"]["final_commit"]

    def extract_task_files(repo: pathlib.Path) -> list[dict[str, Any]]:
        final_paths = git(repo, "ls-tree", "-r", "--name-only", final_commit).splitlines()
        relative_paths = sorted({
            path for path in final_paths
            if any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)
        })
        rows: list[dict[str, Any]] = []
        for relative in relative_paths:
            final_content = module.file_at_commit(repo, final_commit, relative)
            if final_content is None:
                continue
            base_content = module.file_at_commit(repo, base_commit, relative)
            base_status = module.status_from_content(base_content)
            final_status = module.status_from_content(final_content)
            code_match = code_regex.search(pathlib.PurePosixPath(relative).name)
            code = format_code(code_match) if code_match else "unknown"
            log_text = module.git(
                repo, "log", "--reverse", "--first-parent",
                "--format=%H%x1f%cI%x1f%s", f"{base_commit}..{final_commit}",
                "--", relative,
            )
            commits: list[dict[str, Any]] = []
            prior_complete = module.is_complete_status(base_status)
            completion_transitions: list[dict[str, Any]] = []
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
                "task_key": pathlib.PurePosixPath(relative).stem,
                "task_code": code,
                "title": final_content.splitlines()[0].removeprefix("# ").strip(),
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

    module.extract_task_files = extract_task_files


def configure_engine(module: Any, analysis: dict[str, Any]) -> None:
    module.ROOT_THREAD_ID = analysis["root_thread_id"]
    module.ROOT_LOG_NAME = analysis["root_log_name"]
    module.EVENT_CUTOFF_UTC = module.parse_time(analysis["event_cutoff_at"])
    module.ANALYSIS_GENERATED_AT_UTC = analysis["generated_at"]
    module.SESSION_BASE_COMMIT = analysis["repository"]["base_commit"]
    module.SESSION_FINAL_COMMIT = analysis["repository"]["final_commit"]
    module.LOCAL_ZONE = ZoneInfo(analysis["timezone"])
    module.PRICING_AS_OF = analysis["pricing"]["as_of"]
    module.MODEL_PRICING = analysis["pricing"]["models"]
    module.INCLUDE_BOUNDED_EXCERPTS = analysis["privacy"]["include_bounded_excerpts"]
    configure_classification(module, analysis)
    configure_user_messages(module, analysis)
    configure_task_files(module, analysis)


def verify_commits(repo: pathlib.Path, analysis: dict[str, Any]) -> None:
    for field in ("base_commit", "final_commit"):
        commit = analysis["repository"][field]
        git(repo, "cat-file", "-e", f"{commit}^{{commit}}")
    ancestor = subprocess.run(
        ["git", "merge-base", "--is-ancestor", analysis["repository"]["base_commit"],
         analysis["repository"]["final_commit"]],
        cwd=repo, check=False,
    )
    require(ancestor.returncode == 0, "base_commit must be an ancestor of final_commit")


def annotate_output(
    output_dir: pathlib.Path, manifest_path: pathlib.Path, manifest_pointer: str,
    manifest: dict[str, Any],
) -> None:
    analysis_path = output_dir / "analysis.json"
    analysis = json.loads(analysis_path.read_text(encoding="utf-8"))
    require(
        analysis["source"].get("complete_message_bodies_included") is False,
        "extractor must not include complete message bodies",
    )
    manifest_bytes = manifest_path.read_bytes()
    analysis["source"]["report_manifest"] = manifest_pointer
    analysis["source"]["report_manifest_sha256"] = hashlib.sha256(manifest_bytes).hexdigest()
    analysis["source"]["focus_ids"] = manifest["focus_ids"]
    analysis["source"]["bounded_excerpts_included"] = (
        manifest["analysis"]["privacy"]["include_bounded_excerpts"]
    )
    analysis_path.write_text(
        json.dumps(analysis, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def verify_output_privacy(output_dir: pathlib.Path) -> None:
    home = str(pathlib.Path.home())
    home_variants = {home.casefold(), home.replace("\\", "/").casefold()}
    for path in output_dir.iterdir():
        if not path.is_file():
            continue
        content = path.read_text(encoding="utf-8", errors="replace")
        folded_content = content.casefold()
        require(
            not any(value and value in folded_content for value in home_variants),
            f"private user-home path remains in extracted output: {path.name}",
        )
        require(
            not any(pattern.search(content) for pattern in SECRET_PATTERNS),
            f"possible credential remains in extracted output: {path.name}",
        )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=pathlib.Path, required=True)
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex" / "sessions")
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    parser.add_argument("--output-dir", type=pathlib.Path)
    parser.add_argument("--quiet", action="store_true")
    parser.add_argument("--parity-output", action="store_true", help=argparse.SUPPRESS)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    manifest_path = args.manifest.resolve()
    manifest = read_manifest(manifest_path)
    analysis = manifest["analysis"]
    require(analysis is not None, "report manifest has no extraction configuration")
    repo = args.repo.resolve()
    try:
        manifest_pointer = manifest_path.relative_to(repo).as_posix()
    except ValueError as error:
        raise RegistryError("report manifest must be inside the repository") from error
    verify_commits(repo, analysis)
    output_dir = args.output_dir or repo / manifest["source_path"] / "data"
    output_dir = output_dir.resolve()
    module = load_engine()
    configure_engine(module, analysis)
    original_argv = sys.argv
    sys.argv = [
        str(pathlib.Path(module.__file__)),
        "--sessions-dir", str(args.sessions_dir.resolve()),
        "--output-dir", str(output_dir),
        "--repo", str(repo),
    ] + (["--quiet"] if args.quiet else [])
    try:
        module.main()
    finally:
        sys.argv = original_argv
    verify_output_privacy(output_dir)
    if not args.parity_output:
        annotate_output(output_dir, manifest_path, manifest_pointer, manifest)
    print(f"Session analysis extracted to {output_dir}")


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error
