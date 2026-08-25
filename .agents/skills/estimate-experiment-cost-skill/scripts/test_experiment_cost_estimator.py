from __future__ import annotations

import contextlib
import io
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import experiment_cost_estimator as estimator


REPO_ROOT = Path(__file__).resolve().parents[4]
AUTHORITATIVE_STORE = (
    REPO_ROOT
    / ".agents/skills/estimate-experiment-cost-skill/references/base-estimates.json"
)


class BudgetGateTests(unittest.TestCase):
    def test_reuses_luna_none_row_and_accepts_repeated_candidates(self) -> None:
        report = estimator.calculate_budget_gate(
            budget_args(
                AUTHORITATIVE_STORE,
                candidates=[
                    "gpt-5.6-luna,none,10",
                    "gpt-5.6-luna,none,5",
                ],
                observed="0.1",
                reservations=["langfuse-ingestion=0.03"],
                retries=["gpt-5.6-luna,none,2"],
                ceiling="1",
            )
        )

        self.assertEqual("0.003809400000", report["projectedWaveCostUsd"])
        self.assertEqual("0.000507920000", report["retryReserveTotalUsd"])
        self.assertEqual("0.134317320000", report["allInProjectedTotalUsd"])
        self.assertEqual("0.865682680000", report["remainingUsd"])
        self.assertEqual("allowed", report["result"])
        self.assertEqual(2, len(report["candidates"]))

    def test_aggregates_multiple_authoritative_rows(self) -> None:
        with PatchedStore(
            [
                row("model-a", "low", "0.10"),
                row("model-b", "high", "0.25"),
            ]
        ) as store:
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,2", "model-b,high,3"],
                    observed="1.00",
                    reservations=["in-flight=0.05"],
                    retries=["model-a,low,1"],
                    ceiling="3.00",
                )
            )

        self.assertEqual("0.950000000000", report["projectedWaveCostUsd"])
        self.assertEqual("0.100000000000", report["retryReserveTotalUsd"])
        self.assertEqual("0.150000000000", report["reservesTotalUsd"])
        self.assertEqual("2.100000000000", report["allInProjectedTotalUsd"])
        self.assertTrue(report["strictlyInsideCeiling"])

    def test_missing_row_fails_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            with self.assertRaisesRegex(SystemExit, "No matching base estimate"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["missing,low,1"],
                        observed="0",
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="1",
                    )
                )

    def test_ambiguous_row_fails_closed(self) -> None:
        duplicate_rows = [
            row("model-a", "low", "0.10"),
            row("model-a", "low", "0.11"),
        ]
        with PatchedStore(duplicate_rows) as store:
            with self.assertRaisesRegex(SystemExit, "More than one matching"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed="0",
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="1",
                    )
                )

    def test_equality_to_ceiling_is_blocked(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,1"],
                    observed="0.8",
                    reservations=[],
                    retries=["model-a,low,1"],
                    ceiling="1.0",
                )
            )

        self.assertEqual("1.000000000000", report["allInProjectedTotalUsd"])
        self.assertEqual("0.000000000000", report["remainingUsd"])
        self.assertFalse(report["strictlyInsideCeiling"])
        self.assertEqual("blocked", report["result"])

    def test_decimal_just_below_ceiling_is_allowed_without_float_error(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,1"],
                    observed="0.799999999999",
                    reservations=[],
                    retries=["model-a,low,1"],
                    ceiling="1.0",
                )
            )

        self.assertEqual("0.999999999999", report["allInProjectedTotalUsd"])
        self.assertEqual("0.000000000001", report["remainingUsd"])
        self.assertTrue(report["strictlyInsideCeiling"])
        self.assertEqual("allowed", report["result"])

    def test_decimal_precision_beyond_twelve_places_is_not_rounded_for_gate(self) -> None:
        with PatchedStore([row("model-a", "low", "0.0000000000004")]) as store:
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,1"],
                    observed="0",
                    reservations=[],
                    retries=["model-a,low,1"],
                    ceiling="0.000000000001",
                )
            )

        self.assertEqual("0.0000000000008", report["allInProjectedTotalUsd"])
        self.assertEqual("0.0000000000002", report["remainingUsd"])
        self.assertEqual("allowed", report["result"])

    def test_invalid_counts_fail_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            for invalid in ("0", "-1", "1.5", "not-a-count"):
                with self.subTest(invalid=invalid):
                    with self.assertRaises(SystemExit):
                        estimator.calculate_budget_gate(
                            budget_args(
                                store,
                                candidates=[f"model-a,low,{invalid}"],
                                observed="0",
                                reservations=[],
                                retries=["model-a,low,1"],
                                ceiling="1",
                            )
                        )

    def test_invalid_amounts_fail_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            for invalid in ("-0.01", "NaN", "Infinity", "not-an-amount"):
                with self.subTest(field="observed", invalid=invalid):
                    with self.assertRaises(SystemExit):
                        estimator.calculate_budget_gate(
                            budget_args(
                                store,
                                candidates=["model-a,low,1"],
                                observed=invalid,
                                reservations=[],
                                retries=["model-a,low,1"],
                                ceiling="1",
                            )
                        )
                with self.subTest(field="reservation", invalid=invalid):
                    with self.assertRaises(SystemExit):
                        estimator.calculate_budget_gate(
                            budget_args(
                                store,
                                candidates=["model-a,low,1"],
                                observed="0",
                                reservations=[f"pending={invalid}"],
                                retries=["model-a,low,1"],
                                ceiling="1",
                            )
                        )
                with self.subTest(field="ceiling", invalid=invalid):
                    with self.assertRaises(SystemExit):
                        estimator.calculate_budget_gate(
                            budget_args(
                                store,
                                candidates=["model-a,low,1"],
                                observed="0",
                                reservations=[],
                                retries=["model-a,low,1"],
                                ceiling=invalid,
                            )
                        )

    def test_zero_ceiling_and_empty_retry_reserve_fail_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            with self.assertRaisesRegex(SystemExit, "greater than zero"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed="0",
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="0",
                    )
                )
            with self.assertRaisesRegex(SystemExit, "provided at least once"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed="0",
                        reservations=[],
                        retries=[],
                        ceiling="1",
                    )
                )

    def test_invalid_authoritative_average_fails_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "NaN")]) as store:
            with self.assertRaisesRegex(SystemExit, "finite non-negative"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed="0",
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="1",
                    )
                )

    def test_blocked_cli_emits_stable_result_and_nonzero_exit(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            argv = [
                "experiment_cost_estimator.py",
                "budget-gate",
                "--candidate",
                "model-a,low,1",
                "--observed-spend-usd",
                "0.8",
                "--retry-reserve",
                "model-a,low,1",
                "--ceiling-usd",
                "1",
                "--store",
                str(store),
            ]
            output = io.StringIO()
            with patch.object(sys, "argv", argv), contextlib.redirect_stdout(output):
                exit_code = estimator.main()

        self.assertEqual(2, exit_code)
        self.assertIn("Projected wave cost: $0.100000000000", output.getvalue())
        self.assertIn("All-in projected total: $1.000000000000", output.getvalue())
        self.assertIn("Result: BLOCKED", output.getvalue())


def budget_args(
    store: Path,
    *,
    candidates: list[str],
    observed: str,
    reservations: list[str],
    retries: list[str],
    ceiling: str,
) -> SimpleNamespace:
    return SimpleNamespace(
        store=str(store),
        candidate=candidates,
        observed_spend_usd=observed,
        reservation=reservations,
        retry_reserve=retries,
        ceiling_usd=ceiling,
    )


def row(model: str, effort: str, average: str) -> dict[str, str]:
    return {
        "model": model,
        "reasoningEffort": effort,
        "averageCostPerMatchPredictionUsd": average,
    }


class PatchedStore:
    def __init__(self, rows: list[dict[str, str]]) -> None:
        self.rows = rows
        self.patcher = patch.object(
            estimator,
            "load_base_estimate_store",
            return_value={"schemaVersion": 1, "baseEstimates": rows},
        )

    def __enter__(self) -> Path:
        self.patcher.start()
        return Path("patched-base-estimates.json")

    def __exit__(self, *args: object) -> None:
        self.patcher.stop()


if __name__ == "__main__":
    unittest.main()
