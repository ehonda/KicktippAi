#!/usr/bin/env python3
"""Validate and atomically update the Codex session-analysis focus registry."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import re
import unicodedata
import uuid
from datetime import date
from typing import Any, Iterable


SCHEMA_VERSION = 1
CADENCES = {"once", "standing"}
STATUSES = {"active", "retired", "superseded"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
REPOSITORY_PATH = re.compile(r"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$")
STOP_WORDS = {
    "a", "an", "and", "are", "as", "at", "be", "by", "did", "do",
    "does", "for", "from", "how", "in", "is", "it", "of", "on",
    "or", "our", "the", "this", "to", "was", "were", "what", "when",
    "whether", "with",
}


class RegistryError(ValueError):
    """Raised when a registry or requested transition violates its contract."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RegistryError(message)


def require_date(value: Any, field: str) -> None:
    require(isinstance(value, str), f"{field} must be an ISO date string")
    try:
        date.fromisoformat(value)
    except ValueError as error:
        raise RegistryError(f"{field} must be an ISO date: {value}") from error


def load_json(path: pathlib.Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise RegistryError(f"Cannot read valid JSON from {path}: {error}") from error
    require(isinstance(value, dict), f"{path} must contain a JSON object")
    return value


def validate_report_pointer(value: Any, field: str) -> None:
    if value is None:
        return
    require(isinstance(value, dict), f"{field} must be null or an object")
    require(
        set(value) == {"report_id", "manifest", "covered_at"},
        f"{field} must contain report_id, manifest, and covered_at",
    )
    require(
        isinstance(value["report_id"], str) and ID_PATTERN.fullmatch(value["report_id"]),
        f"{field}.report_id must be a stable kebab-case ID",
    )
    require(
        isinstance(value["manifest"], str) and
        REPOSITORY_PATH.fullmatch(value["manifest"]) is not None and
        value["manifest"].endswith(".json"),
        f"{field}.manifest must be a normalized repository-relative JSON path",
    )
    require_date(value["covered_at"], f"{field}.covered_at")


def validate_focus(focus: Any, index: int) -> None:
    label = f"focuses[{index}]"
    require(isinstance(focus, dict), f"{label} must be an object")
    required = {
        "id", "questions", "created_at", "applicability", "cadence",
        "status", "evidence_source_hints", "resolution",
    }
    require(set(focus) == required, f"{label} fields must be exactly {sorted(required)}")
    require(
        isinstance(focus["id"], str) and ID_PATTERN.fullmatch(focus["id"]),
        f"{label}.id must be a stable kebab-case ID",
    )
    questions = focus["questions"]
    require(
        isinstance(questions, list) and questions and
        all(isinstance(question, str) and question.strip() for question in questions),
        f"{label}.questions must be a non-empty string array",
    )
    require(
        len({normalize_text(question) for question in questions}) == len(questions),
        f"{label}.questions contains duplicates",
    )
    require_date(focus["created_at"], f"{label}.created_at")

    applicability = focus["applicability"]
    require(isinstance(applicability, dict), f"{label}.applicability must be an object")
    require(
        set(applicability) == {"session_kinds", "description"},
        f"{label}.applicability must contain session_kinds and description",
    )
    require(
        isinstance(applicability["session_kinds"], list) and
        applicability["session_kinds"] and
        all(isinstance(kind, str) and kind.strip() for kind in applicability["session_kinds"]),
        f"{label}.applicability.session_kinds must be a non-empty string array",
    )
    require(
        len(set(applicability["session_kinds"])) == len(applicability["session_kinds"]),
        f"{label}.applicability.session_kinds contains duplicates",
    )
    require(
        isinstance(applicability["description"], str) and applicability["description"].strip(),
        f"{label}.applicability.description must be non-empty",
    )
    require(focus["cadence"] in CADENCES, f"{label}.cadence must be once or standing")
    require(focus["status"] in STATUSES, f"{label}.status is invalid")
    require(
        isinstance(focus["evidence_source_hints"], list) and
        focus["evidence_source_hints"] and
        all(isinstance(hint, str) and hint.strip() for hint in focus["evidence_source_hints"]),
        f"{label}.evidence_source_hints must be a non-empty string array",
    )

    resolution = focus["resolution"]
    require(isinstance(resolution, dict), f"{label}.resolution must be an object")
    require(
        set(resolution) == {"last_covered_by", "retired_by", "superseded_by"},
        f"{label}.resolution fields are invalid",
    )
    validate_report_pointer(resolution["last_covered_by"], f"{label}.resolution.last_covered_by")
    validate_report_pointer(resolution["retired_by"], f"{label}.resolution.retired_by")
    superseded_by = resolution["superseded_by"]
    require(
        superseded_by is None or
        (isinstance(superseded_by, str) and ID_PATTERN.fullmatch(superseded_by)),
        f"{label}.resolution.superseded_by must be null or a stable ID",
    )

    if focus["status"] == "active":
        require(resolution["retired_by"] is None, f"{label}: active focus cannot be retired")
        require(resolution["superseded_by"] is None, f"{label}: active focus cannot be superseded")
        if focus["cadence"] == "once":
            require(
                resolution["last_covered_by"] is None,
                f"{label}: an addressed one-off focus must retire",
            )
    elif focus["status"] == "retired":
        require(resolution["retired_by"] is not None, f"{label}: retired focus needs a report pointer")
        require(resolution["superseded_by"] is None, f"{label}: retired focus cannot be superseded")
    else:
        require(resolution["superseded_by"] is not None, f"{label}: superseded focus needs a successor")
        require(resolution["retired_by"] is None, f"{label}: superseded focus cannot be retired")


def validate_registry(registry: dict[str, Any]) -> None:
    require(
        set(registry) == {"schema_version", "updated_at", "focuses"},
        "registry fields must be schema_version, updated_at, and focuses",
    )
    require(registry["schema_version"] == SCHEMA_VERSION, "unsupported registry schema_version")
    require_date(registry["updated_at"], "updated_at")
    require(isinstance(registry["focuses"], list), "focuses must be an array")
    for index, focus in enumerate(registry["focuses"]):
        validate_focus(focus, index)

    ids = [focus["id"] for focus in registry["focuses"]]
    require(len(ids) == len(set(ids)), "focus IDs must be unique")
    require(ids == sorted(ids), "focuses must be sorted by ID")
    known = set(ids)
    for focus in registry["focuses"]:
        successor = focus["resolution"]["superseded_by"]
        require(successor is None or successor in known, f"unknown successor focus: {successor}")
        require(successor != focus["id"], f"focus {focus['id']} cannot supersede itself")


def read_registry(path: pathlib.Path) -> dict[str, Any]:
    registry = load_json(path)
    validate_registry(registry)
    return registry


def atomic_write_registry(path: pathlib.Path, registry: dict[str, Any]) -> None:
    registry["focuses"] = sorted(registry["focuses"], key=lambda focus: focus["id"])
    validate_registry(registry)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp")
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(registry, indent=2, ensure_ascii=False) + "\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def normalize_text(value: str) -> str:
    return " ".join(re.findall(r"[a-z0-9]+", value.lower()))


def question_tokens(questions: Iterable[str]) -> set[str]:
    return {
        token for question in questions for token in normalize_text(question).split()
        if token not in STOP_WORDS and len(token) > 1
    }


def overlap_score(left: Iterable[str], right: Iterable[str]) -> float:
    left_tokens = question_tokens(left)
    right_tokens = question_tokens(right)
    if not left_tokens or not right_tokens:
        return 0.0
    return len(left_tokens & right_tokens) / len(left_tokens | right_tokens)


def find_overlaps(
    registry: dict[str, Any], questions: list[str], excluded_id: str | None = None
) -> list[tuple[str, float]]:
    normalized = {normalize_text(question) for question in questions}
    matches: list[tuple[str, float]] = []
    for focus in registry["focuses"]:
        if focus["id"] == excluded_id:
            continue
        existing = {normalize_text(question) for question in focus["questions"]}
        score = 1.0 if normalized & existing else overlap_score(questions, focus["questions"])
        if score >= 0.60:
            matches.append((focus["id"], round(score, 3)))
    return sorted(matches, key=lambda item: (-item[1], item[0]))


def infer_focus_id(question: str) -> str:
    ascii_text = unicodedata.normalize("NFKD", question).encode("ascii", "ignore").decode()
    words = re.findall(r"[a-z0-9]+", ascii_text.lower())
    require(bool(words), "cannot infer a stable focus ID from the question")
    return "-".join(words)[:72].strip("-")


def focus_by_id(registry: dict[str, Any], focus_id: str) -> dict[str, Any]:
    matches = [focus for focus in registry["focuses"] if focus["id"] == focus_id]
    require(len(matches) == 1, f"unknown focus ID: {focus_id}")
    return matches[0]


def ensure_no_overlap(
    registry: dict[str, Any], questions: list[str], excluded_id: str | None, allow_overlap: bool
) -> None:
    overlaps = find_overlaps(registry, questions, excluded_id)
    if overlaps and not allow_overlap:
        rendered = ", ".join(f"{focus_id} ({score:.3f})" for focus_id, score in overlaps)
        raise RegistryError(f"likely duplicate/overlapping active focus: {rendered}")


def make_report_pointer(report_id: str, manifest: str, covered_at: str) -> dict[str, str]:
    require(ID_PATTERN.fullmatch(report_id) is not None, "report ID must be kebab-case")
    require(
        REPOSITORY_PATH.fullmatch(manifest) is not None and manifest.endswith(".json"),
        "manifest must be a normalized repository-relative JSON path",
    )
    require_date(covered_at, "covered_at")
    return {"report_id": report_id, "manifest": manifest, "covered_at": covered_at}


def add_focus(registry: dict[str, Any], args: argparse.Namespace) -> str:
    focus_id = args.id or infer_focus_id(args.question[0])
    require(ID_PATTERN.fullmatch(focus_id) is not None, "focus ID must be kebab-case")
    require(all(focus["id"] != focus_id for focus in registry["focuses"]), f"duplicate focus ID: {focus_id}")
    ensure_no_overlap(registry, args.question, None, args.allow_overlap)
    created_at = args.created_at or date.today().isoformat()
    require_date(created_at, "created_at")
    registry["focuses"].append({
        "id": focus_id,
        "questions": args.question,
        "created_at": created_at,
        "applicability": {
            "session_kinds": sorted(set(args.session_kind)),
            "description": args.applicability,
        },
        "cadence": args.cadence,
        "status": "active",
        "evidence_source_hints": sorted(set(args.evidence_source)),
        "resolution": {
            "last_covered_by": None,
            "retired_by": None,
            "superseded_by": None,
        },
    })
    registry["updated_at"] = created_at
    return focus_id


def update_focus(registry: dict[str, Any], args: argparse.Namespace) -> str:
    focus = focus_by_id(registry, args.id)
    require(focus["status"] == "active", "only active focuses can be updated")
    questions = args.question or focus["questions"]
    ensure_no_overlap(registry, questions, args.id, args.allow_overlap)
    if args.cadence and args.cadence != focus["cadence"]:
        require(
            focus["resolution"]["last_covered_by"] is None,
            "cannot change cadence after a focus has been covered",
        )
        focus["cadence"] = args.cadence
    focus["questions"] = questions
    if args.session_kind:
        focus["applicability"]["session_kinds"] = sorted(set(args.session_kind))
    if args.applicability:
        focus["applicability"]["description"] = args.applicability
    if args.evidence_source:
        focus["evidence_source_hints"] = sorted(set(args.evidence_source))
    registry["updated_at"] = args.updated_at or date.today().isoformat()
    return focus["id"]


def supersede_focus(registry: dict[str, Any], args: argparse.Namespace) -> str:
    focus = focus_by_id(registry, args.id)
    successor = focus_by_id(registry, args.by)
    require(focus["status"] == "active", "only active focuses can be superseded")
    require(successor["status"] == "active", "successor focus must be active")
    focus["status"] = "superseded"
    focus["resolution"]["superseded_by"] = successor["id"]
    registry["updated_at"] = args.updated_at or date.today().isoformat()
    return focus["id"]


def retire_focus(registry: dict[str, Any], args: argparse.Namespace) -> str:
    focus = focus_by_id(registry, args.id)
    require(focus["status"] == "active", "only active focuses can be retired")
    pointer = make_report_pointer(args.report_id, args.manifest, args.covered_at)
    focus["status"] = "retired"
    focus["resolution"]["last_covered_by"] = pointer
    focus["resolution"]["retired_by"] = pointer
    registry["updated_at"] = args.covered_at
    return focus["id"]


def cover_from_manifest(
    registry: dict[str, Any], manifest: dict[str, Any], manifest_path: pathlib.Path
) -> list[str]:
    report_id = manifest.get("id")
    covered_at = manifest.get("published_at")
    focus_ids = manifest.get("focus_ids")
    require(isinstance(report_id, str), "report manifest id is required")
    require(ID_PATTERN.fullmatch(report_id) is not None, "report manifest id must be kebab-case")
    require_date(covered_at, "report manifest published_at")
    require(
        isinstance(focus_ids, list) and all(isinstance(value, str) for value in focus_ids),
        "report manifest focus_ids must be a string array",
    )
    require(len(focus_ids) == len(set(focus_ids)), "report manifest focus_ids contains duplicates")
    require(focus_ids == sorted(focus_ids), "report manifest focus_ids must be sorted")
    pointer = make_report_pointer(report_id, manifest_path.as_posix(), covered_at)
    changed: list[str] = []
    for focus_id in focus_ids:
        focus = focus_by_id(registry, focus_id)
        require(focus["status"] == "active", f"covered focus is not active: {focus_id}")
        focus["resolution"]["last_covered_by"] = pointer
        if focus["cadence"] == "once":
            focus["status"] = "retired"
            focus["resolution"]["retired_by"] = pointer
        changed.append(focus_id)
    registry["updated_at"] = covered_at
    return changed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--registry", type=pathlib.Path,
        default=pathlib.Path("docs/codex/session-analysis/focuses.json"),
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("validate")
    list_parser = subparsers.add_parser("list")
    list_parser.add_argument("--status", choices=sorted(STATUSES))

    add_parser = subparsers.add_parser("add")
    add_parser.add_argument("--id")
    add_parser.add_argument("--question", action="append", required=True)
    add_parser.add_argument("--created-at")
    add_parser.add_argument("--cadence", choices=sorted(CADENCES), required=True)
    add_parser.add_argument("--session-kind", action="append", required=True)
    add_parser.add_argument("--applicability", required=True)
    add_parser.add_argument("--evidence-source", action="append", required=True)
    add_parser.add_argument("--allow-overlap", action="store_true")

    update_parser = subparsers.add_parser("update")
    update_parser.add_argument("--id", required=True)
    update_parser.add_argument("--question", action="append")
    update_parser.add_argument("--updated-at")
    update_parser.add_argument("--cadence", choices=sorted(CADENCES))
    update_parser.add_argument("--session-kind", action="append")
    update_parser.add_argument("--applicability")
    update_parser.add_argument("--evidence-source", action="append")
    update_parser.add_argument("--allow-overlap", action="store_true")

    supersede_parser = subparsers.add_parser("supersede")
    supersede_parser.add_argument("--id", required=True)
    supersede_parser.add_argument("--by", required=True)
    supersede_parser.add_argument("--updated-at")

    retire_parser = subparsers.add_parser("retire")
    retire_parser.add_argument("--id", required=True)
    retire_parser.add_argument("--report-id", required=True)
    retire_parser.add_argument("--manifest", required=True)
    retire_parser.add_argument("--covered-at", required=True)

    cover_parser = subparsers.add_parser("cover")
    cover_parser.add_argument("--manifest", type=pathlib.Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> None:
    args = build_parser().parse_args(argv)
    registry = read_registry(args.registry)
    if args.command == "validate":
        print(f"Valid focus registry: {len(registry['focuses'])} focus(es)")
        return
    if args.command == "list":
        rows = [
            focus for focus in registry["focuses"]
            if args.status is None or focus["status"] == args.status
        ]
        print(json.dumps(rows, indent=2, ensure_ascii=False))
        return
    if args.command == "add":
        changed = [add_focus(registry, args)]
    elif args.command == "update":
        changed = [update_focus(registry, args)]
    elif args.command == "supersede":
        changed = [supersede_focus(registry, args)]
    elif args.command == "retire":
        changed = [retire_focus(registry, args)]
    elif args.command == "cover":
        manifest = load_json(args.manifest)
        changed = cover_from_manifest(registry, manifest, args.manifest)
    else:
        raise AssertionError(args.command)
    atomic_write_registry(args.registry, registry)
    print(json.dumps({"updated": changed, "registry": args.registry.as_posix()}))


if __name__ == "__main__":
    try:
        main()
    except RegistryError as error:
        raise SystemExit(str(error)) from error
