from __future__ import annotations

import csv
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

import duckdb


SKILL_ROOT = Path(__file__).resolve().parents[1]


def load_script(name: str):
    spec = importlib.util.spec_from_file_location(name, SKILL_ROOT / "scripts" / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


enrich = load_script("enrich_lineup_source")
generate = load_script("generate_lineup_payloads")


class EnrichLineupSourceTests(unittest.TestCase):
    def test_enriches_player_by_transfermarkt_player_id(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            paths = TestPaths(Path(temp))
            create_duckdb(paths.database)
            write_seed(
                paths.seed,
                [
                    seed_row(
                        name="Official Name",
                        national_team_id="100",
                        player_id="10",
                    )
                ],
            )

            enrich.main(["--input", str(paths.seed), "--duckdb", str(paths.database), "--output", str(paths.output)])

            rows = read_csv(paths.output)
            self.assertEqual("Official Name", rows[0]["Name"])
            self.assertEqual("26", rows[0]["Age"])
            self.assertEqual("Defender", rows[0]["Position"])
            self.assertEqual("15000000", rows[0]["Market_Value_EUR"])

    def test_enriches_player_by_unambiguous_name_and_national_team_id(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            paths = TestPaths(Path(temp))
            create_duckdb(paths.database)
            write_seed(paths.seed, [seed_row(name="Ana Example", national_team_id="100")])

            enrich.main(["--input", str(paths.seed), "--duckdb", str(paths.database), "--output", str(paths.output)])

            rows = read_csv(paths.output)
            self.assertEqual("Ana Example", rows[0]["Name"])
            self.assertEqual("24", rows[0]["Age"])
            self.assertEqual("Midfield", rows[0]["Position"])
            self.assertEqual("N/A", rows[0]["Market_Value_EUR"])

    def test_fails_when_name_match_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            paths = TestPaths(Path(temp))
            create_duckdb(paths.database)
            write_seed(paths.seed, [seed_row(name="Missing Player", national_team_id="100")])

            with self.assertRaises(SystemExit) as context:
                enrich.main(["--input", str(paths.seed), "--duckdb", str(paths.database), "--output", str(paths.output)])

            self.assertIn("was not found", str(context.exception))

    def test_fails_when_name_match_is_ambiguous(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            paths = TestPaths(Path(temp))
            create_duckdb(paths.database, duplicate_name=True)
            write_seed(paths.seed, [seed_row(name="Ana Example", national_team_id="100")])

            with self.assertRaises(SystemExit) as context:
                enrich.main(["--input", str(paths.seed), "--duckdb", str(paths.database), "--output", str(paths.output)])

            self.assertIn("matched multiple Transfermarkt players", str(context.exception))

    def test_fills_coach_name_from_national_teams_when_mapped(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            paths = TestPaths(Path(temp))
            create_duckdb(paths.database)
            write_seed(paths.seed, [seed_row(role="Coach", name="", national_team_id="100")])

            enrich.main(["--input", str(paths.seed), "--duckdb", str(paths.database), "--output", str(paths.output)])

            rows = read_csv(paths.output)
            self.assertEqual("Casey Coach", rows[0]["Name"])
            self.assertEqual("Coach", rows[0]["Position"])
            self.assertEqual("", rows[0]["Market_Value_EUR"])


class GenerateLineupPayloadsTests(unittest.TestCase):
    def test_rejects_zero_player_market_value(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "lineups.csv"
            write_generator_source(
                source,
                [
                    generator_row(role="Coach", name="Coach One", age="50", position="Coach", market_value=""),
                    generator_row(role="Player", name="Player One", age="25", position="GK", market_value="0"),
                ],
            )

            with self.assertRaises(SystemExit) as context:
                generate.read_rows(source, "provisional")

            self.assertIn("Market_Value_EUR must use N/A instead of 0", str(context.exception))

    def test_generates_payload_with_rendering_and_without_source_only_fields(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "lineups.csv"
            write_generator_source(
                source,
                [
                    generator_row(role="Coach", name="Coach One", age="", position="Coach", market_value=""),
                    generator_row(role="Player", name="Player One", age="25", position="GK", market_value="15000000"),
                    generator_row(role="Player", name="Player Two", age="24", position="MF", market_value="N/A"),
                ],
                extra_fields={"Transfermarkt_Player_Id": "10"},
            )

            generate.main(
                [
                    "--input",
                    str(source),
                    "--community-context",
                    "ehonda-dev-wm26",
                    "--status",
                    "provisional",
                    "--output-root",
                    str(root / "artifacts"),
                    "--kpi-output-root",
                    str(root / "kpi"),
                ]
            )

            payload_path = root / "artifacts" / "context-documents" / "ehonda-dev-wm26" / "lineup-exampleland.csv.json"
            payload = json.loads(payload_path.read_text(encoding="utf-8"))
            content = payload["content"]

            self.assertTrue(content.endswith("\r\n"))
            self.assertIn("15.000.000", content)
            self.assertIn("N/A", content)
            self.assertIn("Exampleland,2026-05-25,provisional,Coach,Coach One,,Coach,", content)
            self.assertNotIn("Transfermarkt_Player_Id", content)


class TestPaths:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.database = root / "transfermarkt-datasets.duckdb"
        self.seed = root / "seed.csv"
        self.output = root / "enriched.csv"


def create_duckdb(path: Path, duplicate_name: bool = False) -> None:
    with duckdb.connect(str(path)) as connection:
        connection.execute(
            """
            create table players (
                player_id integer,
                name varchar,
                date_of_birth date,
                position varchar,
                market_value_in_eur integer,
                current_national_team_id integer
            )
            """
        )
        connection.execute(
            """
            insert into players values
                (10, 'Player Ten', '2000-05-25', 'Defender', 15000000, 100),
                (11, 'Ana Example', '2001-05-26', 'Midfield', null, 100)
            """
        )
        if duplicate_name:
            connection.execute("insert into players values (12, 'Ana Example', '2002-01-01', 'Attack', 1000000, 100)")

        connection.execute(
            """
            create table national_teams (
                national_team_id integer,
                coach_name varchar
            )
            """
        )
        connection.execute("insert into national_teams values (100, 'Casey Coach')")


def seed_row(
    role: str = "Player",
    name: str = "Player Ten",
    national_team_id: str = "",
    player_id: str = "",
) -> dict[str, str]:
    return {
        "Team_Slug": "exampleland",
        "Team": "Exampleland",
        "Data_Collected_At": "2026-05-25",
        "Squad_Status": "provisional",
        "Role": role,
        "Name": name,
        "Transfermarkt_National_Team_Id": national_team_id,
        "Transfermarkt_Player_Id": player_id,
    }


def write_seed(path: Path, rows: list[dict[str, str]]) -> None:
    write_csv(path, enrich.REQUIRED_SEED_COLUMNS, rows)


def generator_row(role: str, name: str, age: str, position: str, market_value: str) -> dict[str, str]:
    return {
        "Team_Slug": "exampleland",
        "Team": "Exampleland",
        "Data_Collected_At": "2026-05-25",
        "Squad_Status": "provisional",
        "Role": role,
        "Name": name,
        "Age": age,
        "Position": position,
        "Market_Value_EUR": market_value,
    }


def write_generator_source(
    path: Path,
    rows: list[dict[str, str]],
    extra_fields: dict[str, str] | None = None,
) -> None:
    fieldnames = ["Team_Slug", *generate.OUTPUT_COLUMNS]
    if extra_fields:
        fieldnames += list(extra_fields)
        rows = [{**row, **extra_fields} for row in rows]
    write_csv(path, fieldnames, rows)


def write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\r\n")
        writer.writeheader()
        writer.writerows(rows)


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


if __name__ == "__main__":
    unittest.main()
