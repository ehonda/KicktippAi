#!/usr/bin/env python3
"""Fail if a reproduction drifts beyond the frozen successor cutoff."""

from __future__ import annotations

import argparse
import csv
from datetime import datetime
import json
import math
import pathlib
from typing import Any


EXPECTED_GENERATED_AT = "2026-08-31T20:05:11.408139Z"
EXPECTED_LAST_EVENT_AT = "2026-08-31T20:04:43.872Z"
EXPECTED_FINAL_COMMIT = "5891d480ed1ac117c955a29b02dd940fd2d6187f"
EXPECTED_COMPARISON = {
    "parallelism.old_average_while_active": 1.521,
    "parallelism.new_average_while_active": 1.1213,
    "parallelism.old_two_plus_share": 0.521,
    "parallelism.new_two_plus_share": 0.1182,
    "parallelism.old_worker_seconds_per_effective_wall_second": 1.4281,
    "parallelism.new_worker_seconds_per_effective_wall_second": 0.8379,
    "efficiency.old_logged_tokens_per_effective_hour": 50070435,
    "efficiency.new_logged_tokens_per_effective_hour": 28932033,
    "efficiency.old_worker_non_cached_plus_output_tokens_per_worker_hour": 771080,
    "efficiency.new_worker_non_cached_plus_output_tokens_per_worker_hour": 728054,
    "efficiency.worker_non_cached_plus_output_rate_change": -0.0558,
    "publication.new_remote_run_branches": 2,
}


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def nested(payload: dict[str, Any], path: str) -> Any:
    value: Any = payload
    for segment in path.split("."):
        value = value[segment]
    return value


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def verify_data_dir(data_dir: pathlib.Path) -> None:
    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    comparison = json.loads((data_dir / "comparison-metrics.json").read_text(encoding="utf-8"))
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    corrections = json.loads((data_dir / "corrections.json").read_text(encoding="utf-8"))

    cutoff = parse_time(EXPECTED_LAST_EVENT_AT)
    require(analysis["generated_at"] == EXPECTED_GENERATED_AT, "snapshot generation timestamp drifted")
    require(parse_time(analysis["source"]["event_cutoff_at"]) == cutoff, "extractor cutoff drifted")
    require(parse_time(analysis["summary"]["session_last_event_at"]) == cutoff, "root last event drifted")
    require(
        derived["session_boundary"]["final_commit"] == EXPECTED_FINAL_COMMIT,
        "final commit drifted",
    )
    preserved = corrections["preserved_snapshot"]
    require(preserved["generated_at"] == EXPECTED_GENERATED_AT, "correction generation boundary drifted")
    require(parse_time(preserved["last_event_at"]) == cutoff, "correction event boundary drifted")
    require(preserved["final_commit"] == EXPECTED_FINAL_COMMIT, "correction commit boundary drifted")

    require(comparison.get("schema_version") == 2, "comparison-metrics.json must use schema 2")
    for path, expected in EXPECTED_COMPARISON.items():
        actual = nested(comparison, path)
        require(
            isinstance(actual, (int, float)) and not isinstance(actual, bool) and math.isfinite(actual),
            f"{path} must be a finite number",
        )
        require(actual == expected, f"{path} drifted: expected {expected}, found {actual}")

    for thread in [*analysis["threads"], *analysis.get("guardians", [])]:
        require(parse_time(thread["last_event_at"]) <= cutoff, f"post-cutoff thread event: {thread['agent_path']}")
        for turn in thread["turns"]:
            require(parse_time(turn["started_at"]) <= cutoff, f"post-cutoff turn start: {thread['agent_path']}")
            if turn["completed_at"]:
                require(parse_time(turn["completed_at"]) <= cutoff, f"post-cutoff turn completion: {thread['agent_path']}")

    with (data_dir / "tool-timings.csv").open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            require(parse_time(row["started_at"]) <= cutoff, f"post-cutoff tool start: {row['call_id']}")
            require(parse_time(row["completed_at"]) <= cutoff, f"post-cutoff tool completion: {row['call_id']}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--data-dir",
        type=pathlib.Path,
        default=pathlib.Path(__file__).parent / "data",
    )
    args = parser.parse_args()
    verify_data_dir(args.data_dir)
    print(f"Verified frozen snapshot in {args.data_dir}")


if __name__ == "__main__":
    main()
