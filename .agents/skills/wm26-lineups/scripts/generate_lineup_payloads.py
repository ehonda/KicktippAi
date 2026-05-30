#!/usr/bin/env python3
"""Generate WM26 lineup context and KPI upload payloads."""

from __future__ import annotations

import argparse
import csv
import io
import json
from collections import OrderedDict
from pathlib import Path
from typing import Iterable, Sequence


SKILL_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_TEAMS_PATH = SKILL_ROOT / "references" / "wm26-teams.csv"
MISSING_VALUE = "N/A"
OUTPUT_COLUMNS = [
    "Team",
    "Data_Collected_At",
    "Role",
    "Name",
    "Age",
    "Position",
    "Market_Value_EUR",
]
SOURCE_COLUMNS = ["Team_Slug", *OUTPUT_COLUMNS]
TEAM_COLUMNS = ["Team_Slug", "Team"]
VALID_STATUSES = {"provisional", "official"}
VALID_ROLES = {"Player", "Coach"}


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate WM26 lineup Firestore upload payloads.")
    parser.add_argument("--input", required=True, help="Source CSV path")
    parser.add_argument("--community-context", required=True, help="Target community context")
    parser.add_argument("--status", required=True, choices=sorted(VALID_STATUSES), help="Expected lineup source status")
    parser.add_argument("--output-root", default=".agents/skills/wm26-lineups/artifacts", help="Ignored artifact root")
    parser.add_argument("--kpi-output-root", default="kpi-documents/output", help="Ignored KPI document output root")
    parser.add_argument(
        "--teams",
        default=str(DEFAULT_TEAMS_PATH),
        help="Required team manifest CSV; missing teams get header-only lineup payloads. Use an empty value to disable.",
    )
    args = parser.parse_args(argv)

    input_path = Path(args.input)
    team_manifest = read_team_manifest(args.teams)
    rows = read_rows(input_path, allow_empty=bool(team_manifest))
    grouped, team_names = group_by_slug(rows, team_manifest)
    validate_coaches(grouped)

    output_root = Path(args.output_root)
    context_output_dir = output_root / "context-documents" / args.community_context
    context_output_dir.mkdir(parents=True, exist_ok=True)

    kpi_output_dir = Path(args.kpi_output_root) / args.community_context
    kpi_output_dir.mkdir(parents=True, exist_ok=True)

    generated_context_paths = []
    for slug, team_rows in grouped.items():
        content = render_csv(team_rows)
        team_name = team_rows[0]["Team"] if team_rows else team_names[slug]
        document_name = f"lineup-{slug}.csv"
        payload = {
            "documentName": document_name,
            "content": content,
            "description": f"WM26 {args.status} lineup context for {team_name}.",
            "communityContext": args.community_context,
        }
        path = context_output_dir / f"{document_name}.json"
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        generated_context_paths.append(path)

    aggregate_content = render_csv(row for team_rows in grouped.values() for row in team_rows)
    kpi_payload = {
        "documentName": "lineups",
        "content": aggregate_content,
        "description": f"WM26 {args.status} lineups for all participants, used for the top scorer team bonus question.",
        "communityContext": args.community_context,
    }
    kpi_path = kpi_output_dir / "lineups.json"
    kpi_path.write_text(json.dumps(kpi_payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"Generated {len(generated_context_paths)} context payload(s) in {context_output_dir}")
    print(f"Generated KPI payload: {kpi_path}")
    print_header_only_context_report(grouped, team_names)
    print_missing_source_data_report(rows)
    print(f"Lineup source status: {args.status}")
    return 0


def read_team_manifest(value: str | None) -> OrderedDict[str, str]:
    if value is None or not value.strip():
        return OrderedDict()

    teams_path = Path(value)
    if not teams_path.exists():
        raise SystemExit(f"Team manifest CSV not found: {teams_path}")

    with teams_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        missing_columns = [column for column in TEAM_COLUMNS if column not in (reader.fieldnames or [])]
        if missing_columns:
            raise SystemExit(f"Team manifest CSV is missing required column(s): {', '.join(missing_columns)}")

        teams: OrderedDict[str, str] = OrderedDict()
        for line_number, row in enumerate(reader, start=2):
            slug = (row.get("Team_Slug") or "").strip()
            team = (row.get("Team") or "").strip()
            if not slug:
                raise SystemExit(f"Team manifest line {line_number}: missing Team_Slug")
            if not team:
                raise SystemExit(f"Team manifest line {line_number}: missing Team")
            if slug in teams:
                raise SystemExit(f"Team manifest line {line_number}: duplicate Team_Slug {slug!r}")
            teams[slug] = team

    return teams


def read_rows(input_path: Path, allow_empty: bool = False) -> list[dict[str, str]]:
    if not input_path.exists():
        raise SystemExit(f"Input CSV not found: {input_path}")

    with input_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        missing_columns = [column for column in SOURCE_COLUMNS if column not in (reader.fieldnames or [])]
        if missing_columns:
            raise SystemExit(f"Input CSV is missing required column(s): {', '.join(missing_columns)}")

        rows = []
        for line_number, row in enumerate(reader, start=2):
            normalized = {column: (row.get(column) or "").strip() for column in SOURCE_COLUMNS}
            validate_row(normalized, line_number)
            rows.append(normalized)

    if not rows and not allow_empty:
        raise SystemExit("Input CSV has no lineup rows")

    return rows


def validate_row(row: dict[str, str], line_number: int) -> None:
    for column in SOURCE_COLUMNS:
        if row["Role"] == "Coach" and column in {"Age", "Market_Value_EUR"}:
            continue
        if not row[column]:
            raise SystemExit(f"Line {line_number}: missing {column}")

    if row["Role"] not in VALID_ROLES:
        raise SystemExit(f"Line {line_number}: unsupported Role {row['Role']!r}")

    if row["Age"] and row["Age"].upper() != MISSING_VALUE and not row["Age"].isdigit():
        raise SystemExit(f"Line {line_number}: Age must be numeric or N/A")

    market_value = row["Market_Value_EUR"]
    normalized_market_value = market_value.replace(".", "")
    if market_value and market_value.upper() != MISSING_VALUE and not normalized_market_value.isdigit():
        raise SystemExit(f"Line {line_number}: Market_Value_EUR must be numeric, N/A, or empty")
    if row["Role"] == "Player" and normalized_market_value == "0":
        raise SystemExit(f"Line {line_number}: Market_Value_EUR must use N/A instead of 0 when unavailable")


def group_by_slug(
    rows: Iterable[dict[str, str]],
    team_manifest: OrderedDict[str, str] | None = None,
) -> tuple[OrderedDict[str, list[dict[str, str]]], OrderedDict[str, str]]:
    grouped: OrderedDict[str, list[dict[str, str]]] = OrderedDict()
    team_names: OrderedDict[str, str] = OrderedDict()

    for slug, team in (team_manifest or {}).items():
        grouped[slug] = []
        team_names[slug] = team

    for row in rows:
        slug = row["Team_Slug"]
        if slug not in grouped:
            grouped[slug] = []
            team_names[slug] = row["Team"]
        output_row = {column: row[column] for column in OUTPUT_COLUMNS}
        output_row["Market_Value_EUR"] = format_market_value(output_row["Market_Value_EUR"])
        grouped[slug].append(output_row)
    return grouped, team_names


def validate_coaches(grouped: OrderedDict[str, list[dict[str, str]]]) -> None:
    for slug, rows in grouped.items():
        if rows and not any(row["Role"] == "Coach" for row in rows):
            raise SystemExit(f"Team {slug} has no Coach row")


def print_header_only_context_report(
    grouped: OrderedDict[str, list[dict[str, str]]],
    team_names: OrderedDict[str, str],
) -> None:
    header_only_slugs = [slug for slug, rows in grouped.items() if not rows]
    if not header_only_slugs:
        print("Header-only lineup context payloads: none")
        return

    print(f"Header-only lineup context payloads: {len(header_only_slugs)}")
    for slug in header_only_slugs:
        print(f"  - {team_names[slug]} ({slug})")


def print_missing_source_data_report(rows: Iterable[dict[str, str]]) -> None:
    missing_by_slug: OrderedDict[str, dict[str, object]] = OrderedDict()
    for row in rows:
        if row["Role"] != "Player":
            continue

        missing_fields = [
            field
            for field in ("Age", "Position", "Market_Value_EUR")
            if row[field].upper() == MISSING_VALUE
        ]
        if not missing_fields:
            continue

        slug = row["Team_Slug"]
        entry = missing_by_slug.setdefault(
            slug,
            {
                "team": row["Team"],
                "players": [],
            },
        )
        entry["players"].append(f"{row['Name']} ({', '.join(missing_fields)})")

    if not missing_by_slug:
        print("Missing lineup source data: none")
        return

    print("Missing lineup source data detected:")
    for slug, entry in missing_by_slug.items():
        players = entry["players"]
        player_names = ", ".join(players)
        plural = "player" if len(players) == 1 else "players"
        print(f"  - {entry['team']} ({slug}): supplemental data missing for {len(players)} {plural}: {player_names}")


def render_csv(rows: Iterable[dict[str, str]]) -> str:
    buffer = io.StringIO()
    writer = csv.DictWriter(buffer, fieldnames=OUTPUT_COLUMNS, lineterminator="\r\n")
    writer.writeheader()
    writer.writerows(rows)
    return buffer.getvalue()


def format_market_value(value: str) -> str:
    if not value:
        return value

    if value.upper() == MISSING_VALUE:
        return MISSING_VALUE

    digits = value.replace(".", "")
    return f"{int(digits):,}".replace(",", ".")


if __name__ == "__main__":
    raise SystemExit(main())
