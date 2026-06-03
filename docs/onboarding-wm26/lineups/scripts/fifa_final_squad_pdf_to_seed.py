from __future__ import annotations

import argparse
import csv
import difflib
import re
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


FIELDS = [
    "Team_Slug",
    "Team",
    "Data_Collected_At",
    "Role",
    "Name",
    "Transfermarkt_National_Team_Id",
    "Transfermarkt_Player_Id",
    "Age",
    "Position",
    "Market_Value_EUR",
]

POSITION_MAP = {
    "GK": "Goalkeeper",
    "DF": "Defender",
    "MF": "Midfield",
    "FW": "Attack",
}

WM26_TEAM_LABEL_TO_SLUG = {
    "Algeria": "algerien",
    "Argentina": "argentinien",
    "Australia": "australien",
    "Austria": "osterreich",
    "Belgium": "belgien",
    "Bosnia And Herzegovina": "bosnien-herzegowina",
    "Brazil": "brasilien",
    "Cabo Verde": "kap-verde",
    "Canada": "kanada",
    "Colombia": "kolumbien",
    "Congo DR": "dr-kongo",
    "Côte D'Ivoire": "elfenbeinkuste",
    "Croatia": "kroatien",
    "Curaçao": "curacao",
    "Czechia": "tschechien",
    "Ecuador": "ecuador",
    "Egypt": "agypten",
    "England": "england",
    "France": "frankreich",
    "Germany": "deutschland",
    "Ghana": "ghana",
    "Haiti": "haiti",
    "IR Iran": "iran",
    "Iraq": "irak",
    "Japan": "japan",
    "Jordan": "jordanien",
    "Korea Republic": "sudkorea",
    "Mexico": "mexiko",
    "Morocco": "marokko",
    "Netherlands": "niederlande",
    "New Zealand": "neuseeland",
    "Norway": "norwegen",
    "Panama": "panama",
    "Paraguay": "paraguay",
    "Portugal": "portugal",
    "Qatar": "katar",
    "Saudi Arabia": "saudi-arabien",
    "Scotland": "schottland",
    "Senegal": "senegal",
    "South Africa": "sudafrika",
    "Spain": "spanien",
    "Sweden": "schweden",
    "Switzerland": "schweiz",
    "Tunisia": "tunesien",
    "Türkiye": "turkei",
    "Uruguay": "uruguay",
    "USA": "usa",
    "Uzbekistan": "usbekistan",
}

WM26_EXTRA_NATIONAL_TEAM_IDS = {
    "algerien": "3614",
    "australien": "3433",
    "ecuador": "5750",
    "mexiko": "6303",
    "paraguay": "3581",
    "uruguay": "3449",
}

WM26_PDF_NAME_FIXES = {
    ("algerien", "Ra K Belghali"): ("Rafik Belghali", "864306"),
    ("brasilien", "Neymar Jr"): ("Neymar Jr", "68290"),
    ("kanada", "Al E Jones"): ("Alfie Jones", "340990"),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Build a KicktippAi lineup seed from a FIFA final squad-list PDF "
            "after extracting layout-preserved text with pdftotext -layout."
        )
    )
    parser.add_argument("--pdf-text", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--collected-at", required=True)
    parser.add_argument("--previous-seed", type=Path)
    parser.add_argument("--duckdb-path", type=Path)
    parser.add_argument("--team-map", type=Path)
    parser.add_argument("--name-fixes", type=Path)
    parser.add_argument("--expected-teams", type=int, default=48)
    parser.add_argument("--expected-players-per-team", type=int, default=26)
    parser.add_argument(
        "--no-built-in-wm26-map",
        action="store_true",
        help="Do not use the built-in WM26 FIFA-label to Kicktipp slug map.",
    )
    parser.add_argument(
        "--allow-partial-manifest",
        action="store_true",
        help="Allow final source rows for fewer teams than the manifest contains.",
    )
    return parser.parse_args()


def normalize(value: str) -> str:
    decomposed = unicodedata.normalize("NFKD", value)
    stripped = "".join(
        ch for ch in decomposed if unicodedata.category(ch) != "Mn"
    ).lower()
    return re.sub(r"[^a-z0-9]+", " ", stripped).strip()


def display_case(value: str) -> str:
    words = []
    for word in value.split():
        words.append("-".join(part.capitalize() for part in word.split("-")))
    return " ".join(words)


def natural_person_name(inverted_name: str, first_names: str, last_names: str) -> str:
    if not any(character.islower() for character in inverted_name):
        return display_case(inverted_name)

    tokens = inverted_name.split()
    first_tokens = first_names.split()
    given_count = 1

    for count in range(len(tokens) - 1, 0, -1):
        if normalize(" ".join(tokens[-count:])) == normalize(first_names):
            given_count = count
            break
    else:
        for count in range(min(len(tokens) - 1, len(first_tokens)), 0, -1):
            if normalize(" ".join(tokens[-count:])) == normalize(
                " ".join(first_tokens[:count])
            ):
                given_count = count
                break

    given = " ".join(tokens[-given_count:])
    family = " ".join(tokens[:-given_count])
    if family:
        return display_case(f"{given} {family}")

    if first_names and last_names:
        return display_case(f"{first_names} {last_names}")
    return display_case(inverted_name)


def to_iso_date(value: str) -> str:
    day, month, year = value.split("/")
    return f"{year}-{month}-{day}"


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def require_columns(rows: list[dict[str, str]], columns: list[str], path: Path) -> None:
    if not rows:
        raise ValueError(f"{path} has no rows")

    missing = [column for column in columns if column not in rows[0]]
    if missing:
        raise ValueError(f"{path} is missing required columns: {', '.join(missing)}")


def load_team_map(args: argparse.Namespace) -> tuple[dict[str, str], dict[str, str]]:
    team_label_to_slug: dict[str, str] = {}
    team_ids: dict[str, str] = {}

    if not args.no_built_in_wm26_map:
        team_label_to_slug.update(WM26_TEAM_LABEL_TO_SLUG)
        team_ids.update(WM26_EXTRA_NATIONAL_TEAM_IDS)

    if args.team_map is not None:
        rows = read_csv(args.team_map)
        require_columns(rows, ["Fifa_Team_Label", "Team_Slug"], args.team_map)
        for row in rows:
            label = row["Fifa_Team_Label"].strip()
            slug = row["Team_Slug"].strip()
            if not label or not slug:
                continue

            team_label_to_slug[label] = slug
            team_id = row.get("Transfermarkt_National_Team_Id", "").strip()
            if team_id:
                team_ids[slug] = team_id

    return team_label_to_slug, team_ids


def load_name_fixes(args: argparse.Namespace) -> dict[tuple[str, str], tuple[str, str]]:
    fixes = dict(WM26_PDF_NAME_FIXES)
    if args.name_fixes is None:
        return fixes

    rows = read_csv(args.name_fixes)
    require_columns(rows, ["Team_Slug", "Pdf_Name", "Name"], args.name_fixes)
    for row in rows:
        team_slug = row["Team_Slug"].strip()
        pdf_name = row["Pdf_Name"].strip()
        name = row["Name"].strip()
        if not team_slug or not pdf_name or not name:
            continue

        fixes[(team_slug, pdf_name)] = (
            name,
            row.get("Transfermarkt_Player_Id", "").strip(),
        )

    return fixes


def parse_pdf_pages(
    pdf_text: Path,
    team_label_to_slug: dict[str, str],
    expected_players_per_team: int,
) -> dict[str, dict[str, Any]]:
    text = pdf_text.read_text(encoding="utf-8", errors="replace")
    pages = [page for page in text.split("\f") if page.strip()]
    parsed: dict[str, dict[str, Any]] = {}

    for page_index, page in enumerate(pages, 1):
        team_label = None
        players: list[dict[str, Any]] = []
        coach = None

        for line in page.splitlines():
            team_match = re.search(
                r"([A-Za-zÀ-ÖØ-öø-ÿ .'’&-]+) \(([A-Z]{3})\)\s*$",
                line.strip(),
            )
            if team_match:
                team_label = team_match.group(1).strip()

            if re.match(r"^\s*[1-9][0-9]?\s+(?:GK|DF|MF|FW)\s+", line):
                parts = re.split(r"\s{2,}", line.strip())
                if len(parts) != 9:
                    raise ValueError(
                        f"Page {page_index}: expected 9 player fields, "
                        f"got {len(parts)}: {parts}"
                    )

                (
                    number,
                    position_code,
                    player_name,
                    first_names,
                    last_names,
                    _shirt,
                    dob,
                    _club,
                    _height,
                ) = parts
                players.append(
                    {
                        "number": int(number),
                        "position": POSITION_MAP[position_code],
                        "name": natural_person_name(
                            player_name,
                            first_names,
                            last_names,
                        ),
                        "dob_iso": to_iso_date(dob),
                    }
                )

            if line.strip().startswith("Head coach"):
                parts = re.split(r"\s{2,}", line.strip())
                if len(parts) >= 4:
                    coach = natural_person_name(parts[1], parts[2], parts[3])
                elif len(parts) >= 2:
                    coach = display_case(parts[1])

        if team_label is None:
            raise ValueError(f"Page {page_index}: no team label found")
        if team_label not in team_label_to_slug:
            raise ValueError(f"Page {page_index}: unmapped FIFA team label {team_label}")
        if len(players) != expected_players_per_team:
            raise ValueError(
                f"Page {page_index}: {team_label} has {len(players)} players; "
                f"expected {expected_players_per_team}"
            )
        if coach is None:
            raise ValueError(f"Page {page_index}: {team_label} has no head coach")

        parsed[team_label_to_slug[team_label]] = {
            "label": team_label,
            "coach": coach,
            "players": players,
        }

    return parsed


def load_duckdb_players(
    duckdb_path: Path | None,
    national_team_ids: set[str],
) -> tuple[dict[str, list[dict[str, str]]], list[dict[str, str]]]:
    if duckdb_path is None:
        return {}, []

    try:
        import duckdb
    except ImportError as exc:
        raise RuntimeError(
            "duckdb is required when --duckdb-path is provided. "
            "Run with `uv --cache-dir .uv-cache run --with duckdb python ...`."
        ) from exc

    connection = duckdb.connect(str(duckdb_path), read_only=True)

    players_by_team: dict[str, list[dict[str, str]]] = defaultdict(list)
    if national_team_ids:
        rows = connection.execute(
            """
            select
                cast(player_id as varchar) as player_id,
                name,
                cast(current_national_team_id as varchar) as national_team_id
            from players
            where cast(current_national_team_id as varchar) in (
                select unnest(?)
            )
            """,
            [sorted(national_team_ids)],
        ).fetchall()

        for player_id, name, national_team_id in rows:
            players_by_team[national_team_id].append(
                {
                    "player_id": player_id,
                    "name": name.strip(),
                    "normalized": normalize(name),
                    "date_of_birth": "",
                }
            )

    all_rows = connection.execute(
        """
        select
            cast(player_id as varchar) as player_id,
            name,
            coalesce(strftime(date_of_birth, '%Y-%m-%d'), '') as date_of_birth
        from players
        where date_of_birth is not null
        """
    ).fetchall()

    all_players = [
        {
            "player_id": player_id,
            "name": name.strip(),
            "normalized": normalize(name),
            "date_of_birth": date_of_birth,
        }
        for player_id, name, date_of_birth in all_rows
    ]

    return players_by_team, all_players


def build_seed(args: argparse.Namespace) -> tuple[list[dict[str, str]], Counter[str], list[str], list[str]]:
    manifest = read_csv(args.manifest)
    require_columns(manifest, ["Team_Slug", "Team"], args.manifest)
    manifest_by_slug = {row["Team_Slug"]: row["Team"] for row in manifest}

    team_label_to_slug, team_ids = load_team_map(args)
    name_fixes = load_name_fixes(args)
    pdf_data = parse_pdf_pages(
        args.pdf_text,
        team_label_to_slug,
        args.expected_players_per_team,
    )

    if args.expected_teams and len(pdf_data) != args.expected_teams:
        raise ValueError(
            f"Expected {args.expected_teams} mapped teams, got {len(pdf_data)}"
        )

    if not args.allow_partial_manifest and set(pdf_data) != set(manifest_by_slug):
        missing = sorted(set(manifest_by_slug) - set(pdf_data))
        extra = sorted(set(pdf_data) - set(manifest_by_slug))
        raise ValueError(f"PDF/manifest mismatch; missing={missing}; extra={extra}")

    old_seed = read_csv(args.previous_seed) if args.previous_seed is not None else []
    old_players: dict[tuple[str, str], dict[str, str]] = {}
    old_players_by_slug: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in old_seed:
        team_id = row.get("Transfermarkt_National_Team_Id", "").strip()
        if team_id and row["Team_Slug"] not in team_ids:
            team_ids[row["Team_Slug"]] = team_id

        if row.get("Role") == "Player":
            old_players[(row["Team_Slug"], normalize(row["Name"]))] = row
            old_players_by_slug[row["Team_Slug"]].append(row)

    duckdb_players, all_duckdb_players = load_duckdb_players(
        args.duckdb_path,
        {team_id for team_id in team_ids.values() if team_id},
    )
    global_players_by_name_and_dob: dict[
        tuple[str, str],
        list[dict[str, str]],
    ] = defaultdict(list)
    global_players_by_dob: dict[str, list[dict[str, str]]] = defaultdict(list)
    for candidate in all_duckdb_players:
        global_players_by_name_and_dob[
            (candidate["normalized"], candidate["date_of_birth"])
        ].append(candidate)
        global_players_by_dob[candidate["date_of_birth"]].append(candidate)

    output_rows: list[dict[str, str]] = []
    match_stats: Counter[str] = Counter()
    fuzzy_matches: list[str] = []
    unmatched: list[str] = []
    used_old_rows: set[tuple[str, str]] = set()

    for manifest_row in manifest:
        slug = manifest_row["Team_Slug"]
        if slug not in pdf_data:
            continue

        team = manifest_row["Team"]
        team_id = team_ids.get(slug, "")
        team_pdf = pdf_data[slug]

        output_rows.append(
            {
                "Team_Slug": slug,
                "Team": team,
                "Data_Collected_At": args.collected_at,
                "Role": "Coach",
                "Name": str(team_pdf["coach"]),
                "Transfermarkt_National_Team_Id": team_id,
                "Transfermarkt_Player_Id": "",
                "Age": "",
                "Position": "Coach",
                "Market_Value_EUR": "",
            }
        )

        candidates = duckdb_players.get(team_id, [])
        candidates_by_normalized = {
            candidate["normalized"]: candidate for candidate in candidates
        }

        for player in team_pdf["players"]:
            pdf_name = str(player["name"])
            fixed_name = name_fixes.get((slug, pdf_name))
            forced_player_id = ""
            if fixed_name is not None:
                pdf_name, forced_player_id = fixed_name
                match_stats["pdf_name_fix"] += 1

            normalized_name = normalize(pdf_name)
            transfermarkt_player_id = forced_player_id
            output_name = pdf_name

            if forced_player_id:
                match_stats["forced_id"] += 1
            else:
                old_row = old_players.get((slug, normalized_name))
                old_key = (slug, normalized_name) if old_row is not None else None

                if old_row is not None:
                    output_name = old_row["Name"]
                    transfermarkt_player_id = old_row.get("Transfermarkt_Player_Id", "")
                    match_stats["old_seed"] += 1
                    used_old_rows.add(old_key)
                else:
                    old_candidates = [
                        row
                        for row in old_players_by_slug.get(slug, [])
                        if (slug, normalize(row["Name"])) not in used_old_rows
                    ]
                    if old_candidates:
                        best_old = max(
                            old_candidates,
                            key=lambda row: difflib.SequenceMatcher(
                                None,
                                normalized_name,
                                normalize(row["Name"]),
                            ).ratio(),
                        )
                        old_ratio = difflib.SequenceMatcher(
                            None,
                            normalized_name,
                            normalize(best_old["Name"]),
                        ).ratio()
                        if old_ratio >= 0.92:
                            old_row = best_old
                            old_key = (slug, normalize(old_row["Name"]))
                            transfermarkt_player_id = old_row.get(
                                "Transfermarkt_Player_Id",
                                "",
                            )
                            match_stats["old_seed_fuzzy"] += 1
                            used_old_rows.add(old_key)
                            fuzzy_matches.append(
                                f"{slug}: {pdf_name} -> {old_row['Name']} "
                                f"({old_ratio:.3f}, previous seed)"
                            )

                    if old_row is None:
                        candidate = candidates_by_normalized.get(normalized_name)
                        if candidate is None and candidates:
                            best = max(
                                candidates,
                                key=lambda item: difflib.SequenceMatcher(
                                    None,
                                    normalized_name,
                                    item["normalized"],
                                ).ratio(),
                            )
                            ratio = difflib.SequenceMatcher(
                                None,
                                normalized_name,
                                best["normalized"],
                            ).ratio()
                            if ratio >= 0.94:
                                candidate = best
                                fuzzy_matches.append(
                                    f"{slug}: {pdf_name} -> {candidate['name']} "
                                    f"({ratio:.3f}, team)"
                                )

                        if candidate is not None:
                            transfermarkt_player_id = candidate["player_id"]
                            match_stats["duckdb_team"] += 1
                        else:
                            exact_dob_candidates = global_players_by_name_and_dob.get(
                                (normalized_name, str(player["dob_iso"])),
                                [],
                            )
                            if len(exact_dob_candidates) == 1:
                                candidate = exact_dob_candidates[0]
                                transfermarkt_player_id = candidate["player_id"]
                                match_stats["duckdb_global_dob"] += 1
                            else:
                                same_dob_candidates = global_players_by_dob.get(
                                    str(player["dob_iso"]),
                                    [],
                                )
                                scored = [
                                    (
                                        difflib.SequenceMatcher(
                                            None,
                                            normalized_name,
                                            item["normalized"],
                                        ).ratio(),
                                        item,
                                    )
                                    for item in same_dob_candidates
                                ]
                                scored.sort(key=lambda entry: entry[0])
                                if scored:
                                    best_ratio, best_candidate = scored[-1]
                                    second_ratio = scored[-2][0] if len(scored) > 1 else 0
                                    if best_ratio >= 0.86 and best_ratio - second_ratio >= 0.04:
                                        output_name = best_candidate["name"]
                                        transfermarkt_player_id = best_candidate["player_id"]
                                        match_stats["duckdb_global_fuzzy_dob"] += 1
                                        fuzzy_matches.append(
                                            f"{slug}: {pdf_name} -> {output_name} "
                                            f"({best_ratio:.3f}, DOB {player['dob_iso']})"
                                        )

                            if not transfermarkt_player_id:
                                unmatched.append(f"{slug}: {pdf_name}")
                                match_stats["unmatched"] += 1

            output_rows.append(
                {
                    "Team_Slug": slug,
                    "Team": team,
                    "Data_Collected_At": args.collected_at,
                    "Role": "Player",
                    "Name": output_name,
                    "Transfermarkt_National_Team_Id": team_id,
                    "Transfermarkt_Player_Id": transfermarkt_player_id,
                    "Age": "",
                    "Position": str(player["position"]),
                    "Market_Value_EUR": "",
                }
            )

    return output_rows, match_stats, fuzzy_matches, unmatched


def write_seed(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDS, lineterminator="\r\n")
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    args = parse_args()
    rows, match_stats, fuzzy_matches, unmatched = build_seed(args)

    counts_by_slug = Counter(row["Team_Slug"] for row in rows)
    expected_rows_per_team = args.expected_players_per_team + 1
    bad_counts = {
        slug: count
        for slug, count in counts_by_slug.items()
        if count != expected_rows_per_team
    }
    if bad_counts:
        raise ValueError(
            f"Expected {expected_rows_per_team} rows per team, got {bad_counts}"
        )

    write_seed(args.output, rows)

    print(f"wrote {args.output}")
    print(
        "rows={rows} coaches={coaches} players={players}".format(
            rows=len(rows),
            coaches=sum(row["Role"] == "Coach" for row in rows),
            players=sum(row["Role"] == "Player" for row in rows),
        )
    )
    print(
        "team_ids={resolved}/{total}".format(
            resolved=sum(
                1
                for slug in counts_by_slug
                if any(
                    row["Team_Slug"] == slug
                    and row["Transfermarkt_National_Team_Id"]
                    for row in rows
                )
            ),
            total=len(counts_by_slug),
        )
    )
    print(
        "player_match_stats="
        + ", ".join(f"{key}:{value}" for key, value in sorted(match_stats.items()))
    )
    print(f"fuzzy_matches={len(fuzzy_matches)}")
    for item in fuzzy_matches[:30]:
        print(f"  fuzzy {item}")
    if len(fuzzy_matches) > 30:
        print(f"  ... {len(fuzzy_matches) - 30} more fuzzy matches")

    print(f"unmatched={len(unmatched)}")
    for item in unmatched[:60]:
        print(f"  unmatched {item}")
    if len(unmatched) > 60:
        print(f"  ... {len(unmatched) - 60} more unmatched")


if __name__ == "__main__":
    main()
