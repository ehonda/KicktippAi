from __future__ import annotations

import argparse
import json
import re
import unicodedata
from difflib import SequenceMatcher

import duckdb


NON_ALNUM = re.compile(r"[^a-z0-9]+")


def normalize(value: str) -> str:
    ascii_value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    return NON_ALNUM.sub("", ascii_value.lower())


def tokenize(value: str) -> list[str]:
    ascii_value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii").lower()
    return [token for token in NON_ALNUM.split(ascii_value) if token]


def score_candidate(search_name: str, club: str, row: tuple[str, str, str, str, str]) -> tuple[float, dict[str, object]]:
    player_id, name, national_team_id, club_name, citizenship = row
    name_score = SequenceMatcher(None, normalize(search_name), normalize(name or "")).ratio()
    search_tokens = tokenize(search_name)
    name_tokens = set(tokenize(name or ""))
    overlap = len([token for token in search_tokens if token in name_tokens])
    club_score = 0.0
    if club:
        club_score = SequenceMatcher(None, normalize(club), normalize(club_name or "")).ratio()
    total = name_score + overlap * 0.08 + club_score * 0.6
    return total, {
        "player_id": player_id,
        "name": name,
        "current_national_team_id": national_team_id,
        "current_club_name": club_name,
        "country_of_citizenship": citizenship,
        "name_score": round(name_score, 3),
        "club_score": round(club_score, 3),
        "score": round(total, 3),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Heuristic helper for WM26 lineup DuckDB research. "
            "Use a copied read-only DuckDB when the live cache file is locked."
        )
    )
    parser.add_argument("--db", required=True, help="Path to transfermarkt-datasets.duckdb")
    parser.add_argument("--name", required=True, help="Player name from the WM26 seed")
    parser.add_argument("--team-id", help="Transfermarkt national team id from the WM26 seed")
    parser.add_argument("--club", help="Current club gathered from web research")
    parser.add_argument("--limit", type=int, default=12, help="Maximum candidates to print")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    name_tokens = tokenize(args.name)
    club_tokens = tokenize(args.club or "")
    where_clauses: list[str] = []
    parameters: list[str] = []

    if args.team_id:
        where_clauses.append("cast(current_national_team_id as varchar) = ?")
        parameters.append(args.team_id)

    token_clauses: list[str] = []
    for token in name_tokens:
        token_clauses.append("lower(name) like ?")
        parameters.append(f"%{token}%")
    for token in club_tokens:
        token_clauses.append("lower(current_club_name) like ?")
        parameters.append(f"%{token}%")

    if token_clauses:
        where_clauses.append("(" + " or ".join(token_clauses) + ")")

    query = """
        select
            cast(player_id as varchar),
            name,
            cast(current_national_team_id as varchar),
            current_club_name,
            country_of_citizenship
        from players
    """
    if where_clauses:
        query += "\nwhere " + " and ".join(where_clauses)

    con = duckdb.connect(database=args.db, read_only=True)
    rows = con.execute(query, parameters).fetchall()

    candidates: list[dict[str, object]] = []
    for row in rows:
        _, name, national_team_id, club_name, _ = row
        if args.team_id and national_team_id != args.team_id:
            if not club_tokens:
                continue
        normalized_name = normalize(name or "")
        search_name = normalize(args.name)
        if search_name != normalized_name:
            candidate_name_tokens = tokenize(name or "")
            if not any(token in candidate_name_tokens for token in name_tokens):
                if not club_tokens:
                    continue
        if club_tokens and not any(token in tokenize(club_name or "") for token in club_tokens):
            if args.team_id and national_team_id != args.team_id:
                continue
        score, payload = score_candidate(args.name, args.club or "", row)
        payload["score"] = round(score, 3)
        candidates.append(payload)

    candidates.sort(key=lambda item: item["score"], reverse=True)
    print(json.dumps(candidates[: args.limit], ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
