#!/usr/bin/env python3
"""Enrich WM26 lineup seed rows from the dcaribou Transfermarkt DuckDB dataset."""

from __future__ import annotations

import argparse
import csv
import re
import sys
import unicodedata
from dataclasses import dataclass
from datetime import date, datetime
from pathlib import Path
from typing import Iterable, Sequence

import duckdb


OUTPUT_COLUMNS = [
    "Team_Slug",
    "Team",
    "Data_Collected_At",
    "Squad_Status",
    "Role",
    "Name",
    "Age",
    "Position",
    "Market_Value_EUR",
]
REQUIRED_SEED_COLUMNS = [
    "Team_Slug",
    "Team",
    "Data_Collected_At",
    "Squad_Status",
    "Role",
    "Name",
    "Transfermarkt_National_Team_Id",
    "Transfermarkt_Player_Id",
]
OPTIONAL_SEED_COLUMNS = ["Age", "Position", "Market_Value_EUR"]
VALID_STATUSES = {"provisional", "official"}
VALID_ROLES = {"Player", "Coach"}


@dataclass(frozen=True)
class PlayerRecord:
    player_id: str
    name: str
    date_of_birth: object
    position: str
    market_value_in_eur: object
    current_national_team_id: str


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Enrich WM26 lineup source rows from transfermarkt-datasets DuckDB."
    )
    parser.add_argument("--input", required=True, help="Seed CSV path")
    parser.add_argument("--duckdb", required=True, help="Local transfermarkt-datasets DuckDB path")
    parser.add_argument("--output", required=True, help="Enriched source CSV output path")
    parser.add_argument("--status", choices=sorted(VALID_STATUSES), help="Expected Squad_Status for every row")
    args = parser.parse_args(argv)

    seed_path = Path(args.input)
    duckdb_path = Path(args.duckdb)
    output_path = Path(args.output)

    rows = read_seed_rows(seed_path, args.status)
    if not duckdb_path.exists():
        raise SystemExit(f"DuckDB database not found: {duckdb_path}")

    with duckdb.connect(str(duckdb_path), read_only=True) as connection:
        enriched = enrich_rows(connection, rows)

    write_rows(output_path, enriched)
    print(f"Wrote {len(enriched)} enriched lineup row(s): {output_path}")
    return 0


def read_seed_rows(input_path: Path, expected_status: str | None) -> list[dict[str, str]]:
    if not input_path.exists():
        raise SystemExit(f"Seed CSV not found: {input_path}")

    with input_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames or []
        missing_columns = [column for column in REQUIRED_SEED_COLUMNS if column not in fieldnames]
        if missing_columns:
            raise SystemExit(f"Seed CSV is missing required column(s): {', '.join(missing_columns)}")

        rows: list[dict[str, str]] = []
        for line_number, row in enumerate(reader, start=2):
            normalized = {column: (row.get(column) or "").strip() for column in REQUIRED_SEED_COLUMNS}
            for column in OPTIONAL_SEED_COLUMNS:
                normalized[column] = (row.get(column) or "").strip()
            validate_seed_row(normalized, expected_status, line_number)
            rows.append(normalized)

    if not rows:
        raise SystemExit("Seed CSV has no lineup rows")

    return rows


def validate_seed_row(row: dict[str, str], expected_status: str | None, line_number: int) -> None:
    for column in ["Team_Slug", "Team", "Data_Collected_At", "Squad_Status", "Role"]:
        if not row[column]:
            raise SystemExit(f"Line {line_number}: missing {column}")

    status = row["Squad_Status"].lower()
    if status not in VALID_STATUSES:
        raise SystemExit(f"Line {line_number}: unsupported Squad_Status {row['Squad_Status']!r}")
    if expected_status is not None and status != expected_status:
        raise SystemExit(
            f"Line {line_number}: Squad_Status is {row['Squad_Status']!r}, expected {expected_status!r}"
        )

    if row["Role"] not in VALID_ROLES:
        raise SystemExit(f"Line {line_number}: unsupported Role {row['Role']!r}")

    if row["Role"] == "Player" and not row["Name"] and not row["Transfermarkt_Player_Id"]:
        raise SystemExit(f"Line {line_number}: Player row needs Name or Transfermarkt_Player_Id")

    if row["Age"] and not row["Age"].isdigit():
        raise SystemExit(f"Line {line_number}: Age must be numeric when provided")


def enrich_rows(connection: duckdb.DuckDBPyConnection, rows: Iterable[dict[str, str]]) -> list[dict[str, str]]:
    errors: list[str] = []
    enriched: list[dict[str, str]] = []

    for line_offset, row in enumerate(rows, start=2):
        try:
            if row["Role"] == "Coach":
                enriched.append(enrich_coach_row(connection, row))
            else:
                enriched.append(enrich_player_row(connection, row))
        except ValueError as ex:
            errors.append(f"Line {line_offset}: {ex}")

    if errors:
        raise SystemExit("Lineup enrichment failed:\n" + "\n".join(f"- {error}" for error in errors))

    return enriched


def enrich_player_row(connection: duckdb.DuckDBPyConnection, row: dict[str, str]) -> dict[str, str]:
    player = resolve_player(connection, row)
    collected_at = parse_collection_date(row["Data_Collected_At"])

    return {
        "Team_Slug": row["Team_Slug"],
        "Team": row["Team"],
        "Data_Collected_At": row["Data_Collected_At"],
        "Squad_Status": row["Squad_Status"].lower(),
        "Role": "Player",
        "Name": row["Name"] or player.name,
        "Age": row["Age"] or calculate_age(player.date_of_birth, collected_at),
        "Position": row["Position"] or player.position,
        "Market_Value_EUR": row["Market_Value_EUR"] or format_market_value(player.market_value_in_eur),
    }


def enrich_coach_row(connection: duckdb.DuckDBPyConnection, row: dict[str, str]) -> dict[str, str]:
    coach_name = row["Name"]
    if not coach_name and row["Transfermarkt_National_Team_Id"]:
        coach_name = get_coach_name(connection, row["Transfermarkt_National_Team_Id"])

    if not coach_name:
        raise ValueError("Coach row needs Name or Transfermarkt_National_Team_Id with national_teams.coach_name")

    return {
        "Team_Slug": row["Team_Slug"],
        "Team": row["Team"],
        "Data_Collected_At": row["Data_Collected_At"],
        "Squad_Status": row["Squad_Status"].lower(),
        "Role": "Coach",
        "Name": coach_name,
        "Age": row["Age"],
        "Position": row["Position"] or "Coach",
        "Market_Value_EUR": "",
    }


def resolve_player(connection: duckdb.DuckDBPyConnection, row: dict[str, str]) -> PlayerRecord:
    if row["Transfermarkt_Player_Id"]:
        player = get_player_by_id(connection, row["Transfermarkt_Player_Id"])
        if player is None:
            raise ValueError(f"Transfermarkt_Player_Id {row['Transfermarkt_Player_Id']} was not found")
        return player

    national_team_id = row["Transfermarkt_National_Team_Id"]
    if not national_team_id:
        raise ValueError(f"Player {row['Name']!r} needs Transfermarkt_National_Team_Id for name matching")

    candidates = get_players_by_national_team_id(connection, national_team_id)
    normalized_name = normalize_name(row["Name"])
    matches = [candidate for candidate in candidates if normalize_name(candidate.name) == normalized_name]

    if not matches:
        raise ValueError(
            f"Player {row['Name']!r} was not found in current_national_team_id {national_team_id}; "
            "add Transfermarkt_Player_Id to the seed CSV"
        )

    if len(matches) > 1:
        ids = ", ".join(match.player_id for match in matches)
        raise ValueError(f"Player {row['Name']!r} matched multiple Transfermarkt players: {ids}")

    return matches[0]


def get_player_by_id(connection: duckdb.DuckDBPyConnection, player_id: str) -> PlayerRecord | None:
    result = connection.execute(
        """
        select
            player_id,
            name,
            date_of_birth,
            position,
            market_value_in_eur,
            current_national_team_id
        from players
        where cast(player_id as varchar) = ?
        """,
        [player_id],
    ).fetchone()

    return player_from_result(result) if result is not None else None


def get_players_by_national_team_id(
    connection: duckdb.DuckDBPyConnection, national_team_id: str
) -> list[PlayerRecord]:
    results = connection.execute(
        """
        select
            player_id,
            name,
            date_of_birth,
            position,
            market_value_in_eur,
            current_national_team_id
        from players
        where cast(current_national_team_id as varchar) = ?
        """,
        [national_team_id],
    ).fetchall()

    return [player_from_result(result) for result in results]


def get_coach_name(connection: duckdb.DuckDBPyConnection, national_team_id: str) -> str:
    result = connection.execute(
        """
        select coach_name
        from national_teams
        where cast(national_team_id as varchar) = ?
        """,
        [national_team_id],
    ).fetchone()

    return "" if result is None or result[0] is None else str(result[0]).strip()


def player_from_result(result: Sequence[object]) -> PlayerRecord:
    return PlayerRecord(
        player_id=str(result[0]),
        name="" if result[1] is None else str(result[1]).strip(),
        date_of_birth=result[2],
        position="" if result[3] is None else str(result[3]).strip(),
        market_value_in_eur=result[4],
        current_national_team_id="" if result[5] is None else str(result[5]).strip(),
    )


def parse_collection_date(value: str) -> date:
    try:
        return datetime.strptime(value, "%Y-%m-%d").date()
    except ValueError as ex:
        raise ValueError(f"Data_Collected_At must use YYYY-MM-DD, got {value!r}") from ex


def calculate_age(date_of_birth: object, collected_at: date) -> str:
    if date_of_birth is None or date_of_birth == "":
        raise ValueError("matched player has no date_of_birth")

    if isinstance(date_of_birth, datetime):
        born = date_of_birth.date()
    elif isinstance(date_of_birth, date):
        born = date_of_birth
    else:
        text = str(date_of_birth).strip()
        try:
            born = datetime.strptime(text[:10], "%Y-%m-%d").date()
        except ValueError as ex:
            raise ValueError(f"matched player has unsupported date_of_birth {text!r}") from ex

    age = collected_at.year - born.year - ((collected_at.month, collected_at.day) < (born.month, born.day))
    if age < 0:
        raise ValueError(f"matched player date_of_birth {born.isoformat()} is after Data_Collected_At")

    return str(age)


def format_market_value(value: object) -> str:
    if value is None or value == "":
        return "N/A"

    try:
        market_value = int(value)
    except (TypeError, ValueError) as ex:
        raise ValueError(f"matched player has unsupported market_value_in_eur {value!r}") from ex

    return "N/A" if market_value == 0 else str(market_value)


def normalize_name(value: str) -> str:
    normalized = unicodedata.normalize("NFKD", value)
    ascii_text = "".join(character for character in normalized if not unicodedata.combining(character))
    return re.sub(r"[^a-z0-9]+", " ", ascii_text.lower()).strip()


def write_rows(output_path: Path, rows: list[dict[str, str]]) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=OUTPUT_COLUMNS, lineterminator="\r\n")
        writer.writeheader()
        writer.writerows(rows)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except duckdb.Error as ex:
        raise SystemExit(f"DuckDB query failed: {ex}") from ex
