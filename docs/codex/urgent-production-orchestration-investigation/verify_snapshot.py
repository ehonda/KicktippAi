#!/usr/bin/env python3
"""Verify the frozen urgent-production orchestration report data."""

from __future__ import annotations

import argparse
import csv
from datetime import datetime
import json
import math
import pathlib
from typing import Any


EXPECTED_GENERATED_AT = "2026-09-05T14:44:28.3992644Z"
EXPECTED_LAST_EVENT_AT = "2026-09-05T14:44:05.022Z"
EXPECTED_FINAL_COMMIT = "538c30c53870faa608cf0d6e6a9dbf20f8d833d3"
EXPECTED_ROOT_MODEL = "gpt-6-astra"
EXPECTED_ROOT_EFFORT = "medium"
EXPECTED = {
    "delivery.commits": 18,
    "cost.root_astra_medium_usd": 219.249134,
    "cost.same_usage_sol_xhigh_usd": 87.699654,
    "cost.astra_multiple_same_tokens": 2.5,
    "workflow.subagent_threads": 11,
    "workflow.thread_limit_errors": 0,
    "workflow.maximum_concurrent_subagents": 4,
    "workflow.root_ledger_patches": 207,
    "workflow.admitted_heavy_samples_below_warning_floor": 20,
    "workflow.denied_resource_samples": 26,
    "workflow.dotnet_concurrency.maximum": 1,
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
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    facts = json.loads((data_dir / "session-facts.json").read_text(encoding="utf-8"))
    cutoff = parse_time(EXPECTED_LAST_EVENT_AT)

    require(analysis["generated_at"] == EXPECTED_GENERATED_AT, "snapshot generation timestamp drifted")
    require(parse_time(analysis["source"]["event_cutoff_at"]) == cutoff, "extractor cutoff drifted")
    require(parse_time(analysis["summary"]["session_last_event_at"]) == cutoff, "root last event drifted")
    require(derived["session_boundary"]["final_commit"] == EXPECTED_FINAL_COMMIT, "final commit drifted")

    root = analysis["threads"][0]
    require(root["agent_path"] == "/root", "root thread moved")
    require(len(root["model_effort_usage"]) == 1, "root used more than one model/effort pair")
    pair = root["model_effort_usage"][0]
    require(pair["model"] == EXPECTED_ROOT_MODEL, "root model drifted")
    require(pair["reasoning_effort"] == EXPECTED_ROOT_EFFORT, "root reasoning effort drifted")
    require(root["responses_over_272k_input"] == 0, "long-context pricing tier applies")

    for path, expected in EXPECTED.items():
        actual = nested(derived, path)
        if isinstance(expected, float):
            require(isinstance(actual, (int, float)) and math.isclose(actual, expected, abs_tol=1e-9), f"{path} drifted")
        else:
            require(actual == expected, f"{path} drifted: expected {expected}, found {actual}")

    require(len(facts["workflow_scorecard"]) == 8, "workflow scorecard coverage drifted")
    require(facts["production_outcome"]["champions_league_answers_verified"] == 3, "CL outcome drifted")

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

    with (data_dir / "ci-runs.csv").open(encoding="utf-8", newline="") as handle:
        ci_rows = list(csv.DictReader(handle))
    require(len(ci_rows) == 9, "CI run count drifted")
    require(sum(row["conclusion"] == "success" for row in ci_rows) == 8, "CI success count drifted")
    require(ci_rows[-1]["head_sha"] == EXPECTED_FINAL_COMMIT, "final CI SHA drifted")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data")
    args = parser.parse_args()
    verify_data_dir(args.data_dir)
    print(f"Verified frozen snapshot in {args.data_dir}")


if __name__ == "__main__":
    main()
