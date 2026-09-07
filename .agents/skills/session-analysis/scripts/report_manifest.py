#!/usr/bin/env python3
"""Validate session-analysis report manifests and publication artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
from datetime import date, datetime
from html.parser import HTMLParser
from typing import Any

from focus_registry import (
    ID_PATTERN, RegistryError, load_json, read_registry, require, validate_registry,
)
from privacy_checks import verify_text_privacy


SCHEMA_VERSION = 1
RELATIVE_PATH = re.compile(r"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$")
COMMIT = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
THREAD_ID = re.compile(r"^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$")
LEGACY_REPORT_IDS = {
    "p0-closeout",
    "p1-context-refresh",
    "p1-orchestration-follow-up",
    "p1-orchestration-interim",
    "urgent-production-orchestration",
}
OFFLINE_CSP = (
    "default-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; "
    "base-uri 'none'; form-action 'none'; worker-src 'none'; script-src 'unsafe-inline'; "
    "style-src 'unsafe-inline'; img-src data:; font-src data:; media-src data:"
)
REQUIRED_CSP = {
    "default-src": {"'none'"},
    "connect-src": {"'none'"},
    "object-src": {"'none'"},
    "frame-src": {"'none'"},
    "base-uri": {"'none'"},
    "form-action": {"'none'"},
    "worker-src": {"'none'"},
    "script-src": {"'unsafe-inline'"},
    "style-src": {"'unsafe-inline'"},
    "img-src": {"data:"},
    "font-src": {"data:"},
    "media-src": {"data:"},
}


class OfflineHtmlParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.csp_values: list[str] = []
        self.violations: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        tag = tag.lower()
        values = {name.lower(): value or "" for name, value in attrs}
        if tag == "meta" and values.get("http-equiv", "").lower() == "content-security-policy":
            self.csp_values.append(values.get("content", ""))
        if tag == "meta" and values.get("http-equiv", "").lower() == "refresh":
            self.violations.append("meta refresh")
        if tag in {"embed", "iframe", "object"}:
            self.violations.append(f"<{tag}>")
        for name, value in values.items():
            lowered = value.strip().lower()
            if name == "srcset" and lowered:
                self.violations.append(f"{tag}[srcset]")
            elif name in {"src", "poster", "data", "background"} and lowered:
                if tag == "script" or not lowered.startswith("data:"):
                    self.violations.append(f"{tag}[{name}]")
            elif name in {"action", "formaction"} and lowered:
                self.violations.append(f"{tag}[{name}]")
            elif name == "href" and lowered:
                if tag == "a":
                    if lowered.startswith(("javascript:", "data:")):
                        self.violations.append("a[href]")
                elif not (tag == "use" and lowered.startswith("#")):
                    self.violations.append(f"{tag}[href]")


def parse_csp(value: str) -> dict[str, set[str]]:
    directives: dict[str, set[str]] = {}
    for clause in value.split(";"):
        parts = clause.strip().lower().split()
        if parts:
            require(parts[0] not in directives, f"duplicate CSP directive: {parts[0]}")
            directives[parts[0]] = set(parts[1:])
    return directives


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


def normalized_text_sha256(path: pathlib.Path) -> str:
    content = path.read_text(encoding="utf-8-sig")
    normalized = content.replace("\r\n", "\n").replace("\r", "\n").encode("utf-8")
    return hashlib.sha256(normalized).hexdigest()


def validate_analysis_config(config: Any, label: str) -> None:
    if config is None:
        return
    require(isinstance(config, dict), f"{label} must be null or an object")
    required = {
        "root_thread_id", "root_log_name", "event_cutoff_at", "generated_at",
        "timezone", "repository", "pricing", "classification",
        "privacy", "snapshot_lock", "injected_message_prefixes",
        "user_message_kinds", "task_files",
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
    require_relative_path(config["snapshot_lock"], f"{label}.snapshot_lock")
    require(config["snapshot_lock"].endswith(".json"), f"{label}.snapshot_lock must be JSON")

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
        "session_kind", "source_path", "site_path", "html_file", "focus_ids",
        "legacy", "analysis_file", "analysis",
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
    require(isinstance(manifest["legacy"], bool), f"{label}.legacy must be boolean")
    expected_legacy = manifest["id"] in LEGACY_REPORT_IDS
    require(
        manifest["legacy"] is expected_legacy,
        f"{label}.legacy must match the fixed legacy report allowlist",
    )
    if expected_legacy:
        require(manifest["analysis"] is None, f"{label}: legacy analysis must be null")
        require(manifest["analysis_file"] is None, f"{label}: legacy analysis_file must be null")
        require(not focus_ids, f"{label}: legacy reports cannot claim focus coverage")
    else:
        require(manifest["analysis"] is not None, f"{label}: future reports require analysis")
        require_relative_path(manifest["analysis_file"], f"{label}.analysis_file")
        require(manifest["analysis_file"].endswith(".json"), f"{label}.analysis_file must be JSON")
        require(
            manifest["analysis_file"].startswith(manifest["source_path"] + "/"),
            f"{label}.analysis_file must be under source_path",
        )
    validate_analysis_config(manifest["analysis"], f"{label}.analysis")
    if manifest["analysis"] is not None:
        require(
            manifest["analysis"]["snapshot_lock"].startswith(manifest["source_path"] + "/"),
            f"{label}.analysis.snapshot_lock must be under source_path",
        )


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


def verify_analysis_artifact(
    manifest_path: pathlib.Path, manifest: dict[str, Any], repo: pathlib.Path,
) -> None:
    if manifest["legacy"]:
        return
    analysis_path = repo / manifest["analysis_file"]
    analysis = load_json(analysis_path)
    source = analysis.get("source")
    require(isinstance(source, dict), f"{analysis_path}: source object is required")
    require(
        source.get("complete_message_bodies_included") is False,
        f"{analysis_path}: complete message bodies must be excluded",
    )
    manifest_pointer = manifest_path.resolve().relative_to(repo).as_posix()
    manifest_sha = normalized_text_sha256(manifest_path)
    expected = manifest["analysis"]
    require(analysis.get("generated_at") == expected["generated_at"], f"{analysis_path}: generation time drifted")
    require(source.get("report_manifest") == manifest_pointer, f"{analysis_path}: manifest pointer drifted")
    require(source.get("report_manifest_sha256") == manifest_sha, f"{analysis_path}: manifest digest drifted")
    require(source.get("focus_ids") == manifest["focus_ids"], f"{analysis_path}: focus IDs drifted")
    require(source.get("root_thread_id") == expected["root_thread_id"], f"{analysis_path}: root thread drifted")
    root_log = str(source.get("root_log", "")).replace("\\", "/").split("/")[-1]
    require(root_log == expected["root_log_name"], f"{analysis_path}: root log drifted")
    require(source.get("event_cutoff_at") == expected["event_cutoff_at"], f"{analysis_path}: cutoff drifted")
    require(source.get("repository") == expected["repository"], f"{analysis_path}: Git range drifted")
    require(
        source.get("bounded_excerpts_included") == expected["privacy"]["include_bounded_excerpts"],
        f"{analysis_path}: privacy mode drifted",
    )
    lock_path = repo / expected["snapshot_lock"]
    require(lock_path.is_file(), f"{manifest_path}: snapshot lock is missing")
    lock = load_json(lock_path)
    require(
        set(lock) == {"schema_version", "root_thread_id", "event_cutoff_at", "threads"} and
        lock["schema_version"] == 1,
        f"{lock_path}: invalid snapshot-lock envelope",
    )
    require(lock["root_thread_id"] == expected["root_thread_id"], f"{lock_path}: root thread drifted")
    require(lock["event_cutoff_at"] == expected["event_cutoff_at"], f"{lock_path}: cutoff drifted")
    require(isinstance(lock["threads"], list) and lock["threads"], f"{lock_path}: threads are required")
    thread_ids: list[str] = []
    log_files: list[str] = []
    for index, thread in enumerate(lock["threads"]):
        label = f"{lock_path}: threads[{index}]"
        require(
            isinstance(thread, dict) and set(thread) == {
                "thread_id", "log_file", "included_bytes", "included_records", "sha256",
            },
            f"{label} fields are invalid",
        )
        require(
            isinstance(thread["thread_id"], str) and
            THREAD_ID.fullmatch(thread["thread_id"]) is not None,
            f"{label}.thread_id is invalid",
        )
        require_relative_path(thread["log_file"], f"{label}.log_file")
        require(thread["log_file"].endswith(".jsonl"), f"{label}.log_file must be JSONL")
        require(
            isinstance(thread["included_bytes"], int) and thread["included_bytes"] >= 0,
            f"{label}.included_bytes is invalid",
        )
        require(
            isinstance(thread["included_records"], int) and thread["included_records"] >= 0,
            f"{label}.included_records is invalid",
        )
        require(
            isinstance(thread["sha256"], str) and
            SHA256.fullmatch(thread["sha256"]) is not None,
            f"{label}.sha256 is invalid",
        )
        thread_ids.append(thread["thread_id"])
        log_files.append(thread["log_file"])
    require(len(thread_ids) == len(set(thread_ids)), f"{lock_path}: duplicate thread IDs")
    require(len(log_files) == len(set(log_files)), f"{lock_path}: duplicate log files")
    require(thread_ids.count(expected["root_thread_id"]) == 1, f"{lock_path}: root thread entry is missing")
    root_index = thread_ids.index(expected["root_thread_id"])
    require(
        pathlib.PurePosixPath(log_files[root_index]).name == expected["root_log_name"],
        f"{lock_path}: root log entry drifted",
    )
    lock_sha = normalized_text_sha256(lock_path)
    require(source.get("snapshot_lock") == expected["snapshot_lock"], f"{analysis_path}: lock path drifted")
    require(source.get("snapshot_lock_sha256") == lock_sha, f"{analysis_path}: lock digest drifted")


def verify_manifest_set(
    directory: pathlib.Path, registry_path: pathlib.Path, repo: pathlib.Path,
    verify_html: bool, allow_unconsumed_focuses: bool = False,
    registry_override: dict[str, Any] | None = None,
) -> None:
    manifests = read_manifests(directory)
    registry = registry_override if registry_override is not None else read_registry(registry_path)
    validate_registry(registry)
    focuses = {focus["id"]: focus for focus in registry["focuses"]}
    manifest_by_id = {manifest["id"]: (path, manifest) for path, manifest in manifests}
    coverage: dict[str, list[tuple[pathlib.Path, dict[str, Any]]]] = {}
    for path, manifest in manifests:
        for focus_id in manifest["focus_ids"]:
            require(focus_id in focuses, f"{path}: unknown focus ID {focus_id}")
            coverage.setdefault(focus_id, []).append((path, manifest))
        require((repo / manifest["source_path"]).is_dir(), f"{path}: source_path does not exist")
        verify_analysis_artifact(path, manifest, repo)
        if verify_html:
            html_path = repo / manifest["site_path"] / manifest["html_file"]
            verify_self_contained_html(html_path, require_offline_csp=not manifest["legacy"])
            if not manifest["legacy"]:
                verify_text_privacy([repo / manifest["source_path"], html_path])

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


def verify_self_contained_html(path: pathlib.Path, require_offline_csp: bool = True) -> None:
    require(path.is_file(), f"published report HTML does not exist: {path}")
    html = path.read_text(encoding="utf-8")
    require(re.search(r"<html\b", html, flags=re.IGNORECASE) is not None, f"not an HTML document: {path}")
    parser = OfflineHtmlParser()
    parser.feed(html)
    require(not parser.violations, f"external runtime asset in {path}: {parser.violations[:1]}")
    if require_offline_csp:
        require(len(parser.csp_values) == 1, f"one offline Content-Security-Policy is required in {path}")
        directives = parse_csp(parser.csp_values[0])
        for directive, expected in REQUIRED_CSP.items():
            require(
                directives.get(directive) == expected,
                f"offline Content-Security-Policy has unsafe {directive} in {path}",
            )
    external_runtime = [
        r"<script\b[^>]*\bsrc\s*=",
        r"<link\b[^>]*\brel\s*=\s*['\"]?stylesheet\b",
        r"<(?:img|iframe|audio|video|source)\b[^>]*\bsrc\s*=\s*['\"](?!data:)",
        r"<link\b[^>]*\bhref\s*=\s*['\"](?!data:)",
        r"\burl\s*\(\s*['\"]?(?!(?:data:|#))",
        r"@import\b",
        r"\bfetch\s*\(",
        r"\bXMLHttpRequest\b",
        r"\bWebSocket\b",
        r"\bEventSource\b",
        r"\bsendBeacon\b",
        r"\bimport\s*\(",
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
