#!/usr/bin/env python3
"""Run the stable session extractor from a declarative report manifest."""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys
import uuid
from typing import Any
from zoneinfo import ZoneInfo

from focus_registry import RegistryError, require
from privacy_checks import verify_text_privacy
from report_manifest import normalized_text_sha256, read_manifest, resolve_within


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


def build_snapshot_lock(
    module: Any, sessions_dir: pathlib.Path, analysis: dict[str, Any]
) -> dict[str, Any]:
    root_candidates = list(sessions_dir.rglob(analysis["root_log_name"]))
    require(len(root_candidates) == 1, f"expected one root log, found {len(root_candidates)}")
    family, guardians = module.discover_family(sessions_dir, root_candidates[0])
    entries: list[dict[str, Any]] = []
    for meta in family + guardians:
        path = meta["path"].resolve()
        try:
            relative = path.relative_to(sessions_dir).as_posix()
        except ValueError as error:
            raise RegistryError(f"session log is outside the supplied sessions root: {path}") from error
        digest = hashlib.sha256()
        included_bytes = 0
        included_records = 0
        with path.open("rb") as handle:
            for raw_line in handle:
                record = json.loads(raw_line.decode("utf-8"))
                record_time = module.parse_time(record.get("timestamp"))
                if record_time is None or record_time > module.EVENT_CUTOFF_UTC:
                    continue
                digest.update(raw_line)
                included_bytes += len(raw_line)
                included_records += 1
        entries.append({
            "thread_id": meta["thread_id"],
            "log_file": relative,
            "included_bytes": included_bytes,
            "included_records": included_records,
            "sha256": digest.hexdigest(),
        })
    entries.sort(key=lambda entry: (entry["log_file"], entry["thread_id"]))
    return {
        "schema_version": 1,
        "root_thread_id": analysis["root_thread_id"],
        "event_cutoff_at": analysis["event_cutoff_at"],
        "threads": entries,
    }


def write_json_atomic(path: pathlib.Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp")
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def verify_snapshot_lock(
    module: Any, sessions_dir: pathlib.Path, repo: pathlib.Path,
    analysis: dict[str, Any], create: bool,
) -> pathlib.Path:
    lock_path = resolve_within(repo, analysis["snapshot_lock"], "analysis.snapshot_lock")
    actual = build_snapshot_lock(module, sessions_dir, analysis)
    if not lock_path.exists():
        require(create, f"snapshot lock is missing: {analysis['snapshot_lock']}")
        write_json_atomic(lock_path, actual)
    expected = json.loads(lock_path.read_text(encoding="utf-8"))
    require(expected == actual, f"transcript family or cutoff-bounded bytes drifted from {analysis['snapshot_lock']}")
    return lock_path


def annotate_output(
    output_dir: pathlib.Path, manifest_path: pathlib.Path, manifest_pointer: str,
    manifest: dict[str, Any], repo: pathlib.Path, snapshot_lock: pathlib.Path,
) -> None:
    analysis_path = output_dir / "analysis.json"
    analysis = json.loads(analysis_path.read_text(encoding="utf-8"))
    require(
        analysis["source"].get("complete_message_bodies_included") is False,
        "extractor must not include complete message bodies",
    )
    analysis["source"]["report_manifest"] = manifest_pointer
    analysis["source"]["report_manifest_sha256"] = normalized_text_sha256(manifest_path)
    analysis["source"]["focus_ids"] = manifest["focus_ids"]
    analysis["source"]["bounded_excerpts_included"] = (
        manifest["analysis"]["privacy"]["include_bounded_excerpts"]
    )
    analysis["source"]["event_cutoff_at"] = manifest["analysis"]["event_cutoff_at"]
    analysis["source"]["repository"] = manifest["analysis"]["repository"]
    analysis["source"]["snapshot_lock"] = snapshot_lock.relative_to(repo).as_posix()
    analysis["source"]["snapshot_lock_sha256"] = normalized_text_sha256(snapshot_lock)
    analysis_path.write_text(
        json.dumps(analysis, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def verify_output_privacy(output_dir: pathlib.Path) -> None:
    verify_text_privacy([output_dir])


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=pathlib.Path, required=True)
    parser.add_argument("--sessions-dir", type=pathlib.Path, default=pathlib.Path.home() / ".codex" / "sessions")
    parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    parser.add_argument("--output-dir", type=pathlib.Path)
    parser.add_argument("--quiet", action="store_true")
    parser.add_argument("--create-snapshot-lock", action="store_true")
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
    source_path = resolve_within(repo, manifest["source_path"], "manifest.source_path")
    expected_analysis_path = resolve_within(
        repo, manifest["analysis_file"], "manifest.analysis_file"
    )
    try:
        expected_analysis_path.relative_to(source_path)
    except ValueError as error:
        raise RegistryError("manifest.analysis_file resolves outside source_path") from error
    output_dir = args.output_dir or expected_analysis_path.parent
    output_dir = output_dir.resolve()
    module = load_engine()
    configure_engine(module, analysis)
    sessions_dir = args.sessions_dir.resolve()
    expected_snapshot_lock = resolve_within(
        repo, analysis["snapshot_lock"], "analysis.snapshot_lock"
    )
    try:
        expected_snapshot_lock.relative_to(source_path)
    except ValueError as error:
        raise RegistryError("analysis.snapshot_lock resolves outside source_path") from error
    snapshot_lock = verify_snapshot_lock(
        module, sessions_dir, repo, analysis, args.create_snapshot_lock
    )
    if not args.parity_output:
        require(
            output_dir / "analysis.json" == expected_analysis_path,
            "output directory must match the manifest analysis_file parent",
        )
    original_argv = sys.argv
    sys.argv = [
        str(pathlib.Path(module.__file__)),
        "--sessions-dir", str(sessions_dir),
        "--output-dir", str(output_dir),
        "--repo", str(repo),
    ] + (["--quiet"] if args.quiet else [])
    try:
        module.main()
    finally:
        sys.argv = original_argv
    verify_snapshot_lock(module, sessions_dir, repo, analysis, create=False)
    if not args.parity_output:
        annotate_output(
            output_dir, manifest_path, manifest_pointer, manifest, repo, snapshot_lock
        )
    verify_output_privacy(output_dir)
    print(f"Session analysis extracted to {output_dir}")


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error
