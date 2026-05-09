from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace


def load_estimator_module():
    script_path = Path(
        ".agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py"
    )
    spec = importlib.util.spec_from_file_location("experiment_cost_estimator", script_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {script_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class ExperimentCostEstimatorTests(unittest.TestCase):
    def test_flex_service_tier_prices_mixed_observations_as_flex_and_reports_retry_rates(self) -> None:
        estimator = load_estimator_module()

        report = estimator.calculate_base_row(
            SimpleNamespace(
                input="tools/tests/fixtures/experiment_cost_usage_mixed_tiers.json",
                group="repeated-measured",
                expect_count=2,
                model="gpt-5.5",
                reasoning_effort="low",
                prompt_route="test",
                model_knowledge_cutoff="2025-12-01",
                sampling_cutoff="2025-12-03T00:00:00 Europe/Berlin (+01)",
                max_output_tokens=10000,
                source="unit test",
                pricing_source="src/OpenAiIntegration/CostCalculationService.cs",
                service_tier="flex",
            )
        )

        self.assertEqual(report["serviceTier"], "flex")
        self.assertEqual(report["observedServiceTierCounts"], {"default": 1, "flex": 1})
        self.assertEqual(report["nonFlexRequestCount"], 1)
        self.assertEqual(report["nonFlexRequestRate"], "0.5000")
        self.assertEqual(report["nonFlexRequestPercent"], "50.00%")
        self.assertEqual(report["nonFlexRetryCount"], 1)
        self.assertEqual(report["nonFlexRetryRate"], "0.5000")
        self.assertEqual(report["nonFlexRetryPercent"], "50.00%")
        self.assertEqual(report["estimatedTotalCostUsd"], "0.008000000000")
        self.assertEqual(report["averageCostPerMatchPredictionUsd"], "0.004000000000")

    def test_observed_service_tier_prices_mixed_flex_and_non_flex_retry_records(self) -> None:
        estimator = load_estimator_module()
        report = estimator.calculate_base_row(
            SimpleNamespace(
                input="tools/tests/fixtures/experiment_cost_usage_mixed_tiers.json",
                group="repeated-measured",
                expect_count=2,
                model="gpt-5.5",
                reasoning_effort="low",
                prompt_route="test",
                model_knowledge_cutoff="2025-12-01",
                sampling_cutoff="2025-12-03T00:00:00 Europe/Berlin (+01)",
                max_output_tokens=10000,
                source="unit test",
                pricing_source="src/OpenAiIntegration/CostCalculationService.cs",
                service_tier="observed",
            )
        )

        self.assertEqual(report["observedServiceTierCounts"], {"default": 1, "flex": 1})
        self.assertEqual(report["nonFlexRequestCount"], 1)
        self.assertEqual(report["nonFlexRetryCount"], 1)
        self.assertEqual(report["estimatedTotalCostUsd"], "0.012000000000")
        self.assertEqual(report["averageCostPerMatchPredictionUsd"], "0.006000000000")


if __name__ == "__main__":
    unittest.main()
