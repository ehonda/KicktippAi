#!/usr/bin/env python3
"""Validate session-analysis report manifests and publication artifacts."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
from datetime import date, datetime
from typing import Any

from focus_registry import ID_PATTERN, RegistryError, load_json, read_registry, require


SCHEMA_VERSION = 1
RELATIVE_PATH = re.compile(r"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$")
COMMIT = re.compile(r"^[0-9a-f]{40}$")


def require_timestamp(value: Any, field: str) -> None:
    require(isinstance(value, str), f"{field} must be an ISO timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise RegistryError(f"{field} must be an ISO timestamp: {value}") from error
    require(parsed.tzinfo is not None, f"{field} must include a timezone")


def require_date(value: Any, field: str) -> None:
    require(isinstance(value, str), f"{field} must be an ISO date")
    try:
        date.fromisoformat(value)
    except ValueError as error:
        raise RegistryError(f"{field} must be an ISO date: {value}") from error


def require_relative_path(value: Any, field: str) -> None:
    require(
        isinstance(value, str) and RELATIVE_PATH.fullmatch(value) is not None,
        f"{field} must be a normalized repository-relative path without '..'",
    )


def validate_analysis_config(config: Any, label: str) -> None:
    if config is None:
        return
    require(isinstance(config, dict), f"{label} must be null or an object")
    required = {
        "root_thread_id", "root_log_name", "event_cutoff_at", "generated_at",
        "timezone", "repository", "pricing", "classification",
        "privacy", "injected_message_prefixes", "user_message_kinds", "task_files",
    }
    require(set(config) == required, f"{label} fields must be exactly {sorted(required)}")
    require(
        isinstance(config["root_thread_id"], str) and config["root_thread_id"].strip(),
        f"{label}.root_thread_id is required",
    )
    require(
        isinstance(config["root_log_name"], str) and
        config["root_log_name"].endswith(".jsonl") and
        config["root_thread_id"] in config["root_log_name"],
        f"{label}.root_log_name must be a JSONL name containing root_thread_id",
    )
    require_timestamp(config["event_cutoff_at"], f"{label}.event_cutoff_at")
    require_timestamp(config["generated_at"], f"{label}.generated_at")
    require(isinstance(config["timezone"], str) and config["timezone"], f"{label}.timezone is required")

    privacy = config["privacy"]
    require(
        isinstance(privacy, dict) and set(privacy) == {"include_bounded_excerpts"} and
        isinstance(privacy["include_bounded_excerpts"], bool),
        f"{label}.privacy must contain the boolean include_bounded_excerpts",
    )

    repository = config["repository"]
    require(
        isinstance(repository, dict) and set(repository) == {"base_commit", "final_commit"},
        f"{label}.repository must contain base_commit and final_commit",
    )
    require(COMMIT.fullmatch(repository["base_commit"]) is not None, f"{label}.base_commit must be full SHA")
    require(COMMIT.fullmatch(repository["final_commit"]) is not None, f"{label}.final_commit must be full SHA")

    pricing = config["pricing"]
    require(
        isinstance(pricing, dict) and set(pricing) == {"as_of", "models"},
        f"{label}.pricing must contain as_of and models",
    )
    require_date(pricing["as_of"], f"{label}.pricing.as_of")
    require(isinstance(pricing["models"], dict), f"{label}.pricing.models must be an object")
    for model, rates in pricing["models"].items():
        require(isinstance(model, str) and model, f"{label}: empty model name")
        require(isinstance(rates, dict), f"{label}.pricing.models.{model} must be an object")
        require(
            {"input", "cached_input", "output", "source_url"} <= set(rates) <=
            {"input", "cached_input", "cache_write_input", "output", "source_url"},
            f"{label}.pricing.models.{model} fields are invalid",
        )
        require(
            all(isinstance(rates[key], (int, float)) and rates[key] >= 0 for key in rates if key != "source_url"),
            f"{label}.pricing.models.{model} rates must be non-negative numbers",
        )
        require(
            isinstance(rates["source_url"], str) and rates["source_url"].startswith("https://"),
            f"{label}.pricing.models.{model}.source_url must use HTTPS",
        )

    classification = config["classification"]
    require(
        isinstance(classification, dict) and
        set(classification) == {"agent_rules", "default_group", "turn_code_regex"},
        f"{label}.classification fields are invalid",
    )
    require(isinstance(classification["default_group"], str), f"{label}.default_group is required")
    require(isinstance(classification["agent_rules"], list), f"{label}.agent_rules must be an array")
    for rule_index, rule in enumerate(classification["agent_rules"]):
        require(
            isinstance(rule, dict) and set(rule) == {"pattern", "group"},
            f"{label}.agent_rules[{rule_index}] fields are invalid",
        )
        try:
            re.compile(rule["pattern"], flags=re.IGNORECASE)
        except (TypeError, re.error) as error:
            raise RegistryError(f"invalid agent rule regex: {error}") from error
        require(isinstance(rule["group"], str) and rule["group"], "agent rule group is required")
    turn_regex = classification["turn_code_regex"]
    if turn_regex is not None:
        try:
            compiled = re.compile(turn_regex, flags=re.IGNORECASE)
        except (TypeError, re.error) as error:
            raise RegistryError(f"invalid turn_code_regex: {error}") from error
        require(
            {"prefix", "number"} <= set(compiled.groupindex),
            "turn_code_regex must define named prefix and number groups",
        )

    prefixes = config["injected_message_prefixes"]
    require(isinstance(prefixes, list), f"{label}.injected_message_prefixes must be an array")
    for prefix in prefixes:
        require(
            isinstance(prefix, dict) and set(prefix) == {"prefix", "category"} and
            all(isinstance(prefix[key], str) and prefix[key] for key in prefix),
            f"{label}: invalid injected-message prefix",
        )
    kinds = config["user_message_kinds"]
    require(isinstance(kinds, dict), f"{label}.user_message_kinds must be an object")
    require(
        all(key.isdigit() and int(key) > 0 and isinstance(value, str) and value for key, value in kinds.items()),
        f"{label}.user_message_kinds keys must be positive ordinals",
    )

    tasks = config["task_files"]
    require(
        isinstance(tasks, dict) and set(tasks) == {"globs", "code_regex"},
        f"{label}.task_files fields are invalid",
    )
    require(
        isinstance(tasks["globs"], list) and
        all(isinstance(pattern, str) and pattern for pattern in tasks["globs"]),
        f"{label}.task_files.globs must be a string array",
    )
    try:
        code_regex = re.compile(tasks["code_regex"], flags=re.IGNORECASE)
    except (TypeError, re.error) as error:
        raise RegistryError(f"invalid task code_regex: {error}") from error
    require(
        {"prefix", "number"} <= set(code_regex.groupindex),
        "task code_regex must define named prefix and number groups",
    )


def validate_manifest(manifest: dict[str, Any], label: str = "manifest") -> None:
    required = {
        "schema_version", "id", "title", "summary", "eyebrow", "published_at",
        "session_kind", "source_path", "site_path", "html_file", "focus_ids", "analysis",
    }
    require(set(manifest) == required, f"{label} fields must be exactly {sorted(required)}")
    require(manifest["schema_version"] == SCHEMA_VERSION, f"{label}: unsupported schema_version")
    require(
        isinstance(manifest["id"], str) and ID_PATTERN.fullmatch(manifest["id"]),
        f"{label}.id must be kebab-case",
    )
    for field in ("title", "summary", "eyebrow", "session_kind"):
        require(isinstance(manifest[field], str) and manifest[field].strip(), f"{label}.{field} is required")
    require_date(manifest["published_at"], f"{label}.published_at")
    require_relative_path(manifest["source_path"], f"{label}.source_path")
    require_relative_path(manifest["site_path"], f"{label}.site_path")
    require(manifest["site_path"].startswith("session-analysis/"), f"{label}.site_path must be under session-analysis/")
    require_relative_path(manifest["html_file"], f"{label}.html_file")
    require(manifest["html_file"].endswith(".html"), f"{label}.html_file must be HTML")
    focus_ids = manifest["focus_ids"]
    require(
        isinstance(focus_ids, list) and
        all(isinstance(focus_id, str) and ID_PATTERN.fullmatch(focus_id) for focus_id in focus_ids),
        f"{label}.focus_ids must be stable IDs",
    )
    require(len(focus_ids) == len(set(focus_ids)), f"{label}.focus_ids contains duplicates")
    require(focus_ids == sorted(focus_ids), f"{label}.focus_ids must be sorted")
    validate_analysis_config(manifest["analysis"], f"{label}.analysis")


def read_manifest(path: pathlib.Path) -> dict[str, Any]:
    manifest = load_json(path)
    validate_manifest(manifest, path.as_posix())
    return manifest


def read_manifests(directory: pathlib.Path) -> list[tuple[pathlib.Path, dict[str, Any]]]:
    paths = sorted(directory.glob("*.report.json"))
    require(bool(paths), f"no *.report.json files found in {directory}")
    manifests = [(path, read_manifest(path)) for path in paths]
    ids = [manifest["id"] for _, manifest in manifests]
    sites = [manifest["site_path"] for _, manifest in manifests]
    require(len(ids) == len(set(ids)), "report manifest IDs must be unique")
    require(len(sites) == len(set(sites)), "report manifest site paths must be unique")
    return manifests


def verify_manifest_set(
    directory: pathlib.Path, registry_path: pathlib.Path, repo: pathlib.Path,
    verify_html: bool, allow_unconsumed_focuses: bool = False,
) -> None:
    manifests = read_manifests(directory)
    registry = read_registry(registry_path)
    focuses = {focus["id"]: focus for focus in registry["focuses"]}
    manifest_by_id = {manifest["id"]: (path, manifest) for path, manifest in manifests}
    coverage: dict[str, list[tuple[pathlib.Path, dict[str, Any]]]] = {}
    for path, manifest in manifests:
        for focus_id in manifest["focus_ids"]:
            require(focus_id in focuses, f"{path}: unknown focus ID {focus_id}")
            coverage.setdefault(focus_id, []).append((path, manifest))
        require((repo / manifest["source_path"]).is_dir(), f"{path}: source_path does not exist")
        if verify_html:
            html_path = repo / manifest["site_path"] / manifest["html_file"]
            verify_self_contained_html(html_path)

    for focus in focuses.values():
        for field in ("last_covered_by", "retired_by"):
            pointer = focus["resolution"][field]
            if pointer is None:
                continue
            require(pointer["report_id"] in manifest_by_id, f"focus {focus['id']}: unknown report pointer")
            pointed_path, pointed_manifest = manifest_by_id[pointer["report_id"]]
            require(focus["id"] in pointed_manifest["focus_ids"], f"focus {focus['id']}: report omits focus ID")
            try:
                pointed_manifest_path = pointed_path.resolve().relative_to(repo).as_posix()
            except ValueError as error:
                raise RegistryError(f"report manifest is outside the repository: {pointed_path}") from error
            require(pointer["manifest"] == pointed_manifest_path, f"focus {focus['id']}: manifest pointer drifted")

    if allow_unconsumed_focuses:
        return

    for focus_id, reports in coverage.items():
        focus = focuses[focus_id]
        latest_path, latest = max(
            reports, key=lambda item: (item[1]["published_at"], item[1]["id"])
        )
        latest_pointer = focus["resolution"]["last_covered_by"]
        require(latest_pointer is not None, f"focus {focus_id}: report coverage was not consumed")
        try:
            manifest_pointer = latest_path.resolve().relative_to(repo).as_posix()
        except ValueError as error:
            raise RegistryError(f"report manifest is outside the repository: {latest_path}") from error
        expected = {
            "report_id": latest["id"],
            "manifest": manifest_pointer,
            "covered_at": latest["published_at"],
        }
        require(latest_pointer == expected, f"focus {focus_id}: latest coverage pointer drifted")
        if focus["cadence"] == "once":
            require(len(reports) == 1, f"one-off focus {focus_id} is covered by multiple reports")
            require(focus["status"] == "retired", f"covered one-off focus remains active: {focus_id}")
            require(
                focus["resolution"]["retired_by"] == expected,
                f"one-off focus {focus_id}: retirement pointer drifted",
            )


def verify_self_contained_html(path: pathlib.Path) -> None:
    require(path.is_file(), f"published report HTML does not exist: {path}")
    html = path.read_text(encoding="utf-8")
    require(re.search(r"<html\b", html, flags=re.IGNORECASE) is not None, f"not an HTML document: {path}")
    external_runtime = [
        r"<script\b[^>]*\bsrc\s*=",
        r"<link\b[^>]*\brel\s*=\s*['\"]?stylesheet\b",
        r"<(?:img|iframe|audio|video|source)\b[^>]*\bsrc\s*=\s*['\"](?!data:)",
        r"<link\b[^>]*\bhref\s*=\s*['\"](?!data:)",
        r"\burl\s*\(\s*['\"]?(?!(?:data:|#))",
        r"@import\b",
        r"\bfetch\s*\(",
        r"\bXMLHttpRequest\b",
    ]
    for pattern in external_runtime:
        require(re.search(pattern, html, flags=re.IGNORECASE) is None, f"external runtime asset in {path}")


def select_focuses(registry_path: pathlib.Path, session_kind: str) -> list[dict[str, Any]]:
    registry = read_registry(registry_path)
    return [
        focus for focus in registry["focuses"]
        if focus["status"] == "active" and
        ("any" in focus["applicability"]["session_kinds"] or
         session_kind in focus["applicability"]["session_kinds"])
    ]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    validate_parser = subparsers.add_parser("validate")
    validate_parser.add_argument("--manifest-dir", type=pathlib.Path, required=True)
    validate_parser.add_argument("--registry", type=pathlib.Path, required=True)
    validate_parser.add_argument("--repo", type=pathlib.Path, default=pathlib.Path("."))
    validate_parser.add_argument("--verify-html", action="store_true")
    validate_parser.add_argument("--allow-unconsumed-focuses", action="store_true")
    select_parser = subparsers.add_parser("select-focuses")
    select_parser.add_argument("--registry", type=pathlib.Path, required=True)
    select_parser.add_argument("--session-kind", required=True)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    if args.command == "validate":
        verify_manifest_set(
            args.manifest_dir, args.registry, args.repo.resolve(), args.verify_html,
            args.allow_unconsumed_focuses,
        )
        print(f"Valid session-analysis manifests in {args.manifest_dir}")
    else:
        print(json.dumps(select_focuses(args.registry, args.session_kind), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error
