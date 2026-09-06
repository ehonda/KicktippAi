#!/usr/bin/env python3
"""Verify the frozen P1 context-refresh investigation snapshot."""

from __future__ import annotations

import argparse
import csv
from datetime import datetime
import json
import math
import pathlib
from typing import Any


EXPECTED_GENERATED_AT = "2026-09-06T20:21:49.3708897Z"
EXPECTED_CUTOFF = "2026-09-06T20:21:56.196Z"
EXPECTED_RUN_ID = "01a07449-de77-7ae0-ac4a-8f5330c43121"
EXPECTED_FINAL_COMMIT = "c99e1635428bcfea48271e4169767b38f014148c"
EXPECTED_COMMON_TIP = "6cce46a162ec1a5b756173f6046b1666a48332eb"


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def verify_data_dir(data_dir: pathlib.Path) -> None:
    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    findings = json.loads((data_dir / "curated-findings.json").read_text(encoding="utf-8"))

    require(analysis["generated_at"] == EXPECTED_GENERATED_AT, "generation timestamp drifted")
    require(parse_time(analysis["source"]["event_cutoff_at"]) == parse_time(EXPECTED_CUTOFF), "cutoff drifted")
    require(analysis["summary"]["root_thread_id"] == EXPECTED_RUN_ID, "run ID drifted")
    require(derived["snapshot"]["final_integrated_commit"] == EXPECTED_FINAL_COMMIT, "final commit drifted")
    require(derived["snapshot"]["common_runtime_tip"] == EXPECTED_COMMON_TIP, "common tip drifted")
    require(derived["snapshot"]["run_status"] == "active", "snapshot must remain explicitly in-flight")

    expected = {
        "delivery.common_review_cycles": 17,
        "delivery.common_review_blocks": 17,
        "delivery.tasks_completed": 0,
        "coordination.subagent_threads": 52,
        "coordination.thread_limit_errors": 0,
        "preview.frozen_bytes": 67073,
        "preview.patch_events": 22,
        "recovery.compactions": 5,
        "recovery.validated_hook_injections": 5,
        "recovery.maximum_resume_context_percent": 64.6,
        "memory.heavy_samples": 45,
        "memory.heavy_denied": 13,
        "models.architecture_threads": 2,
    }
    for path, wanted in expected.items():
        value: Any = derived
        for segment in path.split("."):
            value = value[segment]
        require(value == wanted, f"{path} drifted: expected {wanted}, found {value}")

    for group in ("recovery", "memory", "models", "cost"):
        for key, value in derived[group].items():
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                require(finite_number(value), f"{group}.{key} must be finite")

    with (data_dir / "compactions.csv").open(encoding="utf-8", newline="") as handle:
        compactions = list(csv.DictReader(handle))
    require(len(compactions) == 5, "expected five compactions")
    require(max(float(row["resume_context_percent"]) for row in compactions) == 64.6, "maximum recovery context drifted")
    require(all(row["hook_context_chars"] != "0" for row in compactions), "missing validated hook injection")

    with (data_dir / "review-cycles.csv").open(encoding="utf-8", newline="") as handle:
        reviews = list(csv.DictReader(handle))
    require(len(reviews) == 17, "expected seventeen review cycles")
    require(all(row["verdict"] == "blocked" for row in reviews), "a frozen review verdict changed")
    require(len(findings["findings"]) == 7, "curated finding count drifted")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=pathlib.Path, default=pathlib.Path(__file__).parent / "data")
    args = parser.parse_args()
    verify_data_dir(args.data_dir)
    print(f"Verified frozen snapshot in {args.data_dir}")


if __name__ == "__main__":
    main()
