from __future__ import annotations

import contextlib
import hashlib
import io
import json
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
                observed_attempts=["luna-preflight=0.04", "luna-base-row=0.06"],
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
        self.assertEqual(
            [
                {"name": "luna-preflight", "amountUsd": "0.040000000000"},
                {"name": "luna-base-row", "amountUsd": "0.060000000000"},
            ],
            report["observedAttempts"],
        )
        self.assertEqual("0.100000000000", report["observedSpendToDateUsd"])

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

    def test_zero_ceiling_fails_and_empty_retry_reserve_is_allowed(self) -> None:
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
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,1"],
                    observed="0",
                    reservations=[],
                    retries=[],
                    ceiling="1",
                )
            )
            self.assertEqual([], report["retryReserves"])
            self.assertEqual("0.000000000000", report["retryReserveTotalUsd"])
            self.assertEqual("allowed", report["result"])

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

    def test_duplicate_observed_attempt_names_fail_closed(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            with self.assertRaisesRegex(SystemExit, "provided twice"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed_attempts=["Attempt-A=0.10", "attempt-a=0.20"],
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="1",
                    )
                )

    def test_scalar_observed_compatibility_is_explicit_and_not_combinable(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            report = estimator.calculate_budget_gate(
                budget_args(
                    store,
                    candidates=["model-a,low,1"],
                    legacy_observed="0.20",
                    reservations=[],
                    retries=["model-a,low,1"],
                    ceiling="1",
                )
            )
            self.assertEqual(
                [
                    {
                        "name": "legacy-observed-spend-usd",
                        "amountUsd": "0.200000000000",
                    }
                ],
                report["observedAttempts"],
            )

            with self.assertRaisesRegex(SystemExit, "cannot be combined"):
                estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,1"],
                        observed_attempts=["attempt=0.10"],
                        legacy_observed="0.20",
                        reservations=[],
                        retries=["model-a,low,1"],
                        ceiling="1",
                    )
                )

    def test_mixes_authoritative_and_provisional_candidates_and_retries(self) -> None:
        provisional = provisional_report()
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            with PatchedProvisionalReport(provisional) as (path, source_bytes):
                report = estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=["model-a,low,2"],
                        provisional_candidates=[f"{path},4"],
                        observed_attempts=["preflight=0.10", "prior-row=0.20"],
                        reservations=[],
                        retries=["model-a,low,1"],
                        provisional_retries=[f"{path},2"],
                        ceiling="3",
                    )
                )

        self.assertEqual("1.200000000000", report["projectedWaveCostUsd"])
        self.assertEqual("0.600000000000", report["retryReserveTotalUsd"])
        self.assertEqual("2.100000000000", report["allInProjectedTotalUsd"])
        provisional_candidate = report["candidates"][1]
        self.assertEqual(
            "provisional-one-item-base-row-report",
            provisional_candidate["estimateBasis"],
        )
        provenance = provisional_candidate["provisionalReport"]
        self.assertEqual(str(path.resolve()), provenance["path"])
        self.assertEqual(hashlib.sha256(source_bytes).hexdigest(), provenance["sha256"])
        self.assertEqual("model-p", provenance["model"])
        self.assertEqual("high", provenance["reasoningEffort"])
        self.assertEqual(10000, provenance["maxOutputTokens"])
        self.assertEqual("preflight attempt", provenance["source"])
        self.assertEqual(
            "provisional-one-item-base-row-report",
            report["retryReserves"][1]["estimateBasis"],
        )
        self.assertEqual(provenance, report["retryReserves"][1]["provisionalReport"])

    def test_malformed_and_multi_observation_provisional_reports_fail_closed(self) -> None:
        with PatchedStore([]) as store:
            with PatchedProvisionalReport(raw=b"{not-json") as (path, _):
                with self.assertRaisesRegex(SystemExit, "not valid JSON"):
                    estimator.calculate_budget_gate(
                        budget_args(
                            store,
                            candidates=[],
                            provisional_candidates=[f"{path},20"],
                            observed="0",
                            reservations=[],
                            retries=[],
                            provisional_retries=[f"{path},1"],
                            ceiling="3",
                        )
                    )

            with PatchedProvisionalReport(
                provisional_report(baseSampleObservations=2)
            ) as (path, _):
                with self.assertRaisesRegex(SystemExit, "baseSampleObservations=1"):
                    estimator.calculate_budget_gate(
                        budget_args(
                            store,
                            candidates=[],
                            provisional_candidates=[f"{path},20"],
                            observed="0",
                            reservations=[],
                            retries=[],
                            provisional_retries=[f"{path},1"],
                            ceiling="3",
                        )
                    )

    def test_provisional_report_requires_cap_average_and_provenance(self) -> None:
        invalid_reports = (
            ({**provisional_report(), "maxOutputTokens": 0}, "maxOutputTokens"),
            (
                {**provisional_report(), "averageCostPerMatchPredictionUsd": "0"},
                "greater than zero",
            ),
            ({**provisional_report(), "source": ""}, "source"),
        )
        with PatchedStore([]) as store:
            for invalid_report, message in invalid_reports:
                with self.subTest(message=message):
                    with PatchedProvisionalReport(invalid_report) as (path, _):
                        with self.assertRaisesRegex(SystemExit, message):
                            estimator.calculate_budget_gate(
                                budget_args(
                                    store,
                                    candidates=[],
                                    provisional_candidates=[f"{path},20"],
                                    observed="0",
                                    reservations=[],
                                    retries=[],
                                    provisional_retries=[f"{path},1"],
                                    ceiling="3",
                                )
                            )

    def test_bootstrap_preflight_allows_zero_attempts_and_no_retry_reserve(self) -> None:
        spec = planned_preflight_spec()
        with PatchedStore([]) as store:
            with PatchedPlannedPreflightFiles([spec]) as files:
                report = estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=[],
                        planned_preflights=[str(files.spec_paths[0])],
                        reservations=[],
                        retries=[],
                        ceiling="0.012",
                        pricing_source=files.pricing_path,
                    )
                )

        self.assertEqual([], report["observedAttempts"])
        self.assertEqual("0.000000000000", report["observedSpendToDateUsd"])
        self.assertEqual([], report["retryReserves"])
        self.assertEqual("0.000000000000", report["retryReserveTotalUsd"])
        self.assertEqual("0.011000000000", report["projectedWaveCostUsd"])
        self.assertEqual("allowed", report["result"])

        entry = report["candidates"][0]
        self.assertEqual("planned-preflight-conservative-full-cap", entry["estimateBasis"])
        self.assertEqual(spec["name"], entry["name"])
        self.assertEqual(spec["model"], entry["model"])
        self.assertEqual(spec["reasoningEffort"], entry["reasoningEffort"])
        self.assertEqual(1, entry["matchPredictionCount"])
        self.assertEqual(spec["inputTokenBound"], entry["inputTokenBound"])
        self.assertEqual(spec["maxOutputTokens"], entry["maxOutputTokens"])
        self.assertEqual("flex", entry["serviceTier"])
        self.assertEqual("0.5", entry["appliedPriceMultiplier"])
        self.assertEqual("2", entry["standardInputPricePerMillionUsd"])
        self.assertEqual("10", entry["standardOutputPricePerMillionUsd"])
        self.assertEqual("1", entry["effectiveInputPricePerMillionUsd"])
        self.assertEqual("5", entry["effectiveOutputPricePerMillionUsd"])
        self.assertEqual("0.001000000000", entry["estimatedUncachedInputCostUsd"])
        self.assertEqual("0.010000000000", entry["estimatedFullCapOutputCostUsd"])
        self.assertEqual(
            hashlib.sha256(files.pricing_bytes).hexdigest(),
            entry["pricingSource"]["sha256"],
        )
        self.assertEqual(str(files.pricing_path.resolve()), entry["pricingSource"]["path"])
        self.assertEqual(
            hashlib.sha256(files.spec_bytes[0]).hexdigest(),
            entry["plannedPreflightSpec"]["sha256"],
        )
        self.assertEqual(
            str(files.spec_paths[0].resolve()),
            entry["plannedPreflightSpec"]["path"],
        )
        self.assertEqual(spec["boundEvidence"], entry["boundEvidence"])
        self.assertEqual(spec["source"], entry["source"])

    def test_bootstrap_preflight_blocks_at_exact_ceiling(self) -> None:
        with PatchedStore([]) as store:
            with PatchedPlannedPreflightFiles([planned_preflight_spec()]) as files:
                report = estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=[],
                        planned_preflights=[str(files.spec_paths[0])],
                        reservations=[],
                        retries=[],
                        ceiling="0.011",
                        pricing_source=files.pricing_path,
                    )
                )

        self.assertEqual("0.011000000000", report["allInProjectedTotalUsd"])
        self.assertEqual("blocked", report["result"])

    def test_standard_bootstrap_uses_unmultiplied_unit_prices(self) -> None:
        spec = planned_preflight_spec(serviceTier="standard")
        with PatchedStore([]) as store:
            with PatchedPlannedPreflightFiles([spec]) as files:
                report = estimator.calculate_budget_gate(
                    budget_args(
                        store,
                        candidates=[],
                        planned_preflights=[str(files.spec_paths[0])],
                        reservations=[],
                        retries=[],
                        ceiling="1",
                        pricing_source=files.pricing_path,
                    )
                )

        entry = report["candidates"][0]
        self.assertEqual("1", entry["appliedPriceMultiplier"])
        self.assertEqual("2", entry["effectiveInputPricePerMillionUsd"])
        self.assertEqual("10", entry["effectiveOutputPricePerMillionUsd"])
        self.assertEqual("0.022000000000", entry["estimatedTotalCostUsd"])

    def test_planned_preflight_invalid_specs_fail_closed(self) -> None:
        invalid_specs = (
            (planned_preflight_spec(name=""), "name"),
            (planned_preflight_spec(reasoningEffort=""), "reasoningEffort"),
            (planned_preflight_spec(serviceTier="batch"), "unsupported serviceTier"),
            (planned_preflight_spec(model="missing"), "unknown pricing model"),
            (planned_preflight_spec(inputTokenBound=0), "inputTokenBound"),
            (planned_preflight_spec(inputTokenBound="1000"), "inputTokenBound"),
            (
                planned_preflight_spec(
                    inputTokenBound=estimator.SHORT_CONTEXT_INPUT_TOKEN_LIMIT + 1
                ),
                "exceeds the repository short-context pricing limit",
            ),
            (planned_preflight_spec(maxOutputTokens=0), "maxOutputTokens"),
            (planned_preflight_spec(boundEvidence=""), "boundEvidence"),
            (planned_preflight_spec(source=""), "source"),
        )
        with PatchedStore([]) as store:
            for invalid_spec, message in invalid_specs:
                with self.subTest(message=message):
                    with PatchedPlannedPreflightFiles([invalid_spec]) as files:
                        with self.assertRaisesRegex(SystemExit, message):
                            estimator.calculate_budget_gate(
                                budget_args(
                                    store,
                                    candidates=[],
                                    planned_preflights=[str(files.spec_paths[0])],
                                    reservations=[],
                                    retries=[],
                                    ceiling="1",
                                    pricing_source=files.pricing_path,
                                )
                            )

    def test_planned_preflight_pricing_parse_and_duplicate_names_fail_closed(self) -> None:
        with PatchedStore([]) as store:
            with PatchedPlannedPreflightFiles(
                [planned_preflight_spec()], pricing_source=b"not pricing"
            ) as files:
                with self.assertRaisesRegex(SystemExit, "Could not parse pricing"):
                    estimator.calculate_budget_gate(
                        budget_args(
                            store,
                            candidates=[],
                            planned_preflights=[str(files.spec_paths[0])],
                            reservations=[],
                            retries=[],
                            ceiling="1",
                            pricing_source=files.pricing_path,
                        )
                    )

            duplicate_specs = [
                planned_preflight_spec(name="Bootstrap-A"),
                planned_preflight_spec(name="bootstrap-a"),
            ]
            with PatchedPlannedPreflightFiles(duplicate_specs) as files:
                with self.assertRaisesRegex(SystemExit, "provided twice"):
                    estimator.calculate_budget_gate(
                        budget_args(
                            store,
                            candidates=[],
                            planned_preflights=[str(path) for path in files.spec_paths],
                            reservations=[],
                            retries=[],
                            ceiling="1",
                            pricing_source=files.pricing_path,
                        )
                    )

    def test_blocked_cli_emits_stable_result_and_nonzero_exit(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            argv = [
                "experiment_cost_estimator.py",
                "budget-gate",
                "--candidate",
                "model-a,low,1",
                "--observed-attempt",
                "prior-attempt=0.8",
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

    def test_report_json_write_failure_blocks_and_returns_nonzero(self) -> None:
        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            argv = [
                "experiment_cost_estimator.py",
                "budget-gate",
                "--candidate",
                "model-a,low,1",
                "--observed-attempt",
                "prior-attempt=0.1",
                "--retry-reserve",
                "model-a,low,1",
                "--ceiling-usd",
                "1",
                "--store",
                str(store),
                "--report-json",
                "unwritable-report.json",
            ]
            stdout = io.StringIO()
            stderr = io.StringIO()
            with (
                patch.object(sys, "argv", argv),
                patch.object(estimator, "write_json", side_effect=OSError("denied")),
                contextlib.redirect_stdout(stdout),
                contextlib.redirect_stderr(stderr),
            ):
                exit_code = estimator.main()

        self.assertEqual(3, exit_code)
        self.assertIn("Result: BLOCKED", stdout.getvalue())
        self.assertNotIn("ALLOWED", stdout.getvalue())
        self.assertIn("could not be written", stdout.getvalue())
        self.assertIn("could not be written", stderr.getvalue())

    def test_report_json_receives_named_observed_attempts(self) -> None:
        captured: list[tuple[Path, dict[str, object]]] = []

        def capture_report(path: Path, payload: dict[str, object]) -> None:
            captured.append((path, payload))

        with PatchedStore([row("model-a", "low", "0.10")]) as store:
            argv = [
                "experiment_cost_estimator.py",
                "budget-gate",
                "--candidate",
                "model-a,low,1",
                "--observed-attempt",
                "preflight=0.1",
                "--observed-attempt",
                "base-row=0.2",
                "--retry-reserve",
                "model-a,low,1",
                "--ceiling-usd",
                "1",
                "--store",
                str(store),
                "--report-json",
                "budget-gate.json",
            ]
            with (
                patch.object(sys, "argv", argv),
                patch.object(estimator, "write_json", side_effect=capture_report),
                contextlib.redirect_stdout(io.StringIO()),
            ):
                exit_code = estimator.main()

        self.assertEqual(0, exit_code)
        self.assertEqual(Path("budget-gate.json"), captured[0][0])
        self.assertEqual(
            [
                {"name": "preflight", "amountUsd": "0.100000000000"},
                {"name": "base-row", "amountUsd": "0.200000000000"},
            ],
            captured[0][1]["observedAttempts"],
        )
        self.assertEqual("0.300000000000", captured[0][1]["observedSpendToDateUsd"])


def budget_args(
    store: Path,
    *,
    candidates: list[str],
    reservations: list[str],
    retries: list[str],
    ceiling: str,
    observed: str | None = None,
    observed_attempts: list[str] | None = None,
    legacy_observed: str | None = None,
    provisional_candidates: list[str] | None = None,
    provisional_retries: list[str] | None = None,
    planned_preflights: list[str] | None = None,
    pricing_source: Path | None = None,
) -> SimpleNamespace:
    if observed_attempts is None:
        observed_attempts = [] if observed is None else [f"attempt-1={observed}"]
    return SimpleNamespace(
        store=str(store),
        candidate=candidates,
        provisional_candidate=provisional_candidates or [],
        planned_preflight=planned_preflights or [],
        observed_attempt=observed_attempts,
        observed_spend_usd=legacy_observed,
        reservation=reservations,
        retry_reserve=retries,
        provisional_retry_reserve=provisional_retries or [],
        ceiling_usd=ceiling,
        pricing_source=str(pricing_source or estimator.DEFAULT_PRICING_SOURCE),
    )


def row(model: str, effort: str, average: str) -> dict[str, str]:
    return {
        "model": model,
        "reasoningEffort": effort,
        "averageCostPerMatchPredictionUsd": average,
    }


def provisional_report(**overrides: object) -> dict[str, object]:
    report: dict[str, object] = {
        "model": "model-p",
        "reasoningEffort": "high",
        "promptRoute": "hosted prompt v2",
        "modelKnowledgeCutoffDate": "2026-02-16",
        "samplingCutoffUsed": "2026-02-18T00:00:00 Europe/Berlin (+01)",
        "maxOutputTokens": 10000,
        "baseSampleObservations": 1,
        "serviceTier": "flex",
        "averageCostPerMatchPredictionUsd": "0.25",
        "estimatedTotalCostUsd": "0.25",
        "source": "preflight attempt",
    }
    report.update(overrides)
    return report


def planned_preflight_spec(**overrides: object) -> dict[str, object]:
    spec: dict[str, object] = {
        "name": "model-p-high-preflight",
        "model": "model-p",
        "reasoningEffort": "high",
        "serviceTier": "flex",
        "inputTokenBound": 1000,
        "maxOutputTokens": 2000,
        "boundEvidence": "Upper bound from serialized fixture and context byte count.",
        "source": "P0 bootstrap preregistration",
    }
    spec.update(overrides)
    return spec


SYNTHETIC_PRICING_SOURCE = b'["model-p"] = new(2.00m, 10.00m, 0.20m),\n'


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


class PatchedProvisionalReport:
    def __init__(
        self,
        report: dict[str, object] | None = None,
        *,
        raw: bytes | None = None,
    ) -> None:
        self.path = Path("provisional-base-row-report.json")
        self.source_bytes = (
            raw
            if raw is not None
            else json.dumps(report, sort_keys=True, separators=(",", ":")).encode(
                "utf-8"
            )
        )
        self.patcher = patch.object(
            Path, "read_bytes", autospec=True, return_value=self.source_bytes
        )

    def __enter__(self) -> tuple[Path, bytes]:
        self.patcher.start()
        return self.path, self.source_bytes

    def __exit__(self, *args: object) -> None:
        self.patcher.stop()


class PatchedPlannedPreflightFiles:
    def __init__(
        self,
        specs: list[dict[str, object]],
        *,
        pricing_source: bytes = SYNTHETIC_PRICING_SOURCE,
    ) -> None:
        self.spec_paths = [
            Path(f"planned-preflight-{index}.json")
            for index in range(1, len(specs) + 1)
        ]
        self.pricing_path = Path("synthetic-pricing.cs")
        self.spec_bytes = [
            json.dumps(spec, sort_keys=True, separators=(",", ":")).encode("utf-8")
            for spec in specs
        ]
        self.pricing_bytes = pricing_source
        self.files = dict(zip(self.spec_paths, self.spec_bytes, strict=True))
        self.files[self.pricing_path] = self.pricing_bytes
        self.patcher = patch.object(
            Path,
            "read_bytes",
            autospec=True,
            side_effect=self.read_bytes,
        )

    def read_bytes(self, path: Path) -> bytes:
        try:
            return self.files[path]
        except KeyError as ex:
            raise OSError(f"unexpected test path {path}") from ex

    def __enter__(self) -> PatchedPlannedPreflightFiles:
        self.patcher.start()
        return self

    def __exit__(self, *args: object) -> None:
        self.patcher.stop()


if __name__ == "__main__":
    unittest.main()
