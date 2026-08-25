from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


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
    def test_exact_dataset_run_binding_excludes_stale_same_name_attempt_and_binds_manifest(self) -> None:
        estimator = load_estimator_module()
        group = "repeated-match-slice-measured"
        run_name = "same-run-name"
        dataset_id = "dataset-1"
        dataset_run_id = "current-run-id"
        current_item_ids = [f"current-item-{index:02d}" for index in range(20)]
        observations = [
            self._usage_observation(f"old-trace-{index:02d}", f"old-item-{index:02d}")
            for index in range(20)
        ] + [
            self._usage_observation(f"current-trace-{index:02d}", item_id)
            for index, item_id in enumerate(current_item_ids)
        ]
        run_items = [
            {
                "id": f"run-item-{index:02d}",
                "datasetRunId": dataset_run_id,
                "datasetRunName": run_name,
                "datasetItemId": item_id,
                "traceId": f"current-trace-{index:02d}",
            }
            for index, item_id in enumerate(current_item_ids)
        ]

        def fake_get(_args, _env, relative_path, _query):
            if relative_path == "v2/observations":
                return {"data": observations, "meta": {}}
            if relative_path == "dataset-run-items":
                return {
                    "data": run_items,
                    "meta": {"page": 1, "limit": 100, "totalItems": 20, "totalPages": 1},
                }
            raise AssertionError(f"Unexpected Langfuse path: {relative_path}")

        manifest_path = Path("ignored-slice-manifest.json")
        manifest_bytes = json.dumps(
            {
                "sampleSize": 20,
                "items": [
                    {"sliceDatasetItemId": item_id}
                    for item_id in current_item_ids
                ],
            }
        ).encode("utf-8")
        args = self._collect_args(
            group,
            run_name,
            dataset_id=[f"{group}={dataset_id}"],
            dataset_run_id=[f"{group}={dataset_run_id}"],
            manifest=[f"{group}={manifest_path}"],
        )

        with mock.patch.object(estimator, "load_env_file", return_value={}), mock.patch.object(
            estimator, "langfuse_get_json", side_effect=fake_get
        ), mock.patch.object(Path, "read_bytes", return_value=manifest_bytes):
            records = estimator.collect_records(args)

        self.assertEqual(len(records), 20)
        self.assertEqual(
            {record["traceId"] for record in records},
            {f"current-trace-{index:02d}" for index in range(20)},
        )
        self.assertEqual({record["datasetRunId"] for record in records}, {dataset_run_id})
        self.assertEqual({record["datasetId"] for record in records}, {dataset_id})
        self.assertEqual(
            {record["preparedManifestSampleSize"] for record in records}, {20}
        )
        self.assertEqual(len({record["preparedManifestSha256"] for record in records}), 1)

        with mock.patch.object(estimator, "load_json", return_value=records):
            row = estimator.calculate_base_row(
                SimpleNamespace(
                    input="ignored-usage.json",
                    group=group,
                    expect_count=20,
                    model="gpt-5.6-luna",
                    reasoning_effort="none",
                    prompt_route="test",
                    model_knowledge_cutoff="2026-02-16",
                    sampling_cutoff="2026-02-18T00:00:00 Europe/Berlin (+01)",
                    max_output_tokens=10000,
                    source="unit test",
                    pricing_source="src/OpenAiIntegration/CostCalculationService.cs",
                    service_tier="flex",
                )
            )
        self.assertEqual(row["datasetRunId"], dataset_run_id)
        self.assertEqual(row["datasetId"], dataset_id)
        self.assertEqual(row["preparedManifestSampleSize"], 20)
        self.assertEqual(row["preparedManifestSha256"], records[0]["preparedManifestSha256"])

    def test_run_name_only_overcount_fails_closed_with_dataset_run_guidance(self) -> None:
        estimator = load_estimator_module()
        group = "repeated-match-slice-measured"
        observations = [
            self._usage_observation(f"trace-{index:02d}", f"item-{index:02d}")
            for index in range(40)
        ]
        args = self._collect_args(group, "same-run-name")

        with mock.patch.object(estimator, "load_env_file", return_value={}), mock.patch.object(
            estimator,
            "langfuse_get_json",
            return_value={"data": observations, "meta": {}},
        ):
            with self.assertRaises(SystemExit) as raised:
                estimator.collect_records(args)

        message = str(raised.exception)
        self.assertIn(f"{group}=40/20", message)
        self.assertIn("--dataset-run-id", message)
        self.assertIn("Do not truncate", message)

    def test_exact_dataset_run_binding_rejects_duplicate_item_links(self) -> None:
        estimator = load_estimator_module()
        group = "repeated-match-slice-measured"
        args = self._collect_args(
            group,
            "same-run-name",
            dataset_id=[f"{group}=dataset-1"],
            dataset_run_id=[f"{group}=current-run-id"],
            manifest=[f"{group}=ignored-slice-manifest.json"],
        )
        duplicate_links = [
            {
                "id": "run-item-1",
                "datasetRunId": "current-run-id",
                "datasetRunName": "same-run-name",
                "datasetItemId": "item-1",
                "traceId": "trace-1",
            },
            {
                "id": "run-item-2",
                "datasetRunId": "current-run-id",
                "datasetRunName": "same-run-name",
                "datasetItemId": "item-1",
                "traceId": "trace-2",
            },
        ]

        def fake_get(_args, _env, relative_path, _query):
            if relative_path == "v2/observations":
                return {"data": [], "meta": {}}
            return {
                "data": duplicate_links,
                "meta": {"page": 1, "limit": 100, "totalItems": 2, "totalPages": 1},
            }

        with mock.patch.object(estimator, "load_env_file", return_value={}), mock.patch.object(
            estimator, "langfuse_get_json", side_effect=fake_get
        ):
            with self.assertRaisesRegex(RuntimeError, "duplicate dataset item ID"):
                estimator.collect_records(args)

    def test_exact_dataset_run_binding_requires_manifest(self) -> None:
        estimator = load_estimator_module()
        group = "repeated-match-slice-measured"
        args = self._collect_args(
            group,
            "same-run-name",
            dataset_id=[f"{group}=dataset-1"],
            dataset_run_id=[f"{group}=current-run-id"],
        )

        with self.assertRaisesRegex(SystemExit, "requires --manifest"):
            estimator.validate_collect_bindings(args)

    def test_exact_dataset_run_binding_requires_expected_count(self) -> None:
        estimator = load_estimator_module()
        group = "repeated-match-slice-measured"
        args = self._collect_args(
            group,
            "same-run-name",
            dataset_id=[f"{group}=dataset-1"],
            dataset_run_id=[f"{group}=current-run-id"],
            manifest=[f"{group}=ignored-slice-manifest.json"],
        )
        args.expect = []

        with self.assertRaisesRegex(SystemExit, "requires --expect"):
            estimator.validate_collect_bindings(args)

    def test_base_row_rejects_partial_dataset_run_provenance(self) -> None:
        estimator = load_estimator_module()
        records = [
            {
                **self._compact_usage_record(index),
                "datasetId": "dataset-1",
            }
            for index in range(2)
        ]

        with self.assertRaisesRegex(SystemExit, "partial dataset-run provenance"):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_whole_manifest_provenance_omission(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        for record in records:
            record.pop("preparedManifestSha256")
            record.pop("preparedManifestSampleSize")

        with self.assertRaisesRegex(SystemExit, "requires preparedManifestSha256"):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_manifest_hash_only(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        for record in records:
            record.pop("preparedManifestSampleSize")

        with self.assertRaisesRegex(SystemExit, "requires preparedManifestSha256"):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_manifest_sample_size_only(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        for record in records:
            record.pop("preparedManifestSha256")

        with self.assertRaisesRegex(SystemExit, "requires preparedManifestSha256"):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_per_record_manifest_field_omission(self) -> None:
        estimator = load_estimator_module()
        for missing_field in (
            "preparedManifestSha256",
            "preparedManifestSampleSize",
        ):
            with self.subTest(missing_field=missing_field):
                records = self._dataset_bound_records()
                records[1].pop(missing_field)
                with self.assertRaisesRegex(
                    SystemExit, "requires preparedManifestSha256"
                ):
                    self._calculate_base_row(estimator, records)

    def test_base_row_rejects_per_record_manifest_hash_drift(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        records[1]["preparedManifestSha256"] = "different-manifest-hash"

        with self.assertRaisesRegex(SystemExit, "multiple prepared manifest hashes"):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_per_record_manifest_sample_size_drift(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        records[1]["preparedManifestSampleSize"] = 3

        with self.assertRaisesRegex(
            SystemExit, "multiple prepared manifest sample sizes"
        ):
            self._calculate_base_row(estimator, records)

    def test_base_row_rejects_manifest_sample_size_count_mismatch(self) -> None:
        estimator = load_estimator_module()
        records = self._dataset_bound_records()
        for record in records:
            record["preparedManifestSampleSize"] = 3

        with self.assertRaisesRegex(SystemExit, "does not equal the accepted"):
            self._calculate_base_row(estimator, records)

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

    def test_repeated_match_slice_groups_are_recognized_without_confusing_repeated_match(self) -> None:
        estimator = load_estimator_module()

        self.assertEqual(
            estimator.dataset_type_for_group("repeated-match-slice-measured"),
            "repeated-match-slice",
        )
        self.assertEqual(
            estimator.dataset_type_for_group("repeated-slice-measured"),
            "repeated-match-slice",
        )
        self.assertEqual(
            estimator.parse_repetition(
                "bundesliga-2025-26__test-community__ts123__repeated-match-slice__random-2x3-seed-42__m01__03"
            ),
            3,
        )

    @staticmethod
    def _usage_observation(trace_id: str, dataset_item_id: str) -> dict:
        return {
            "id": f"observation-{trace_id}",
            "traceId": trace_id,
            "model": "gpt-5.6-luna",
            "usageDetails": {
                "input": 100,
                "output": 10,
                "total": 110,
                "reasoning_tokens": 0,
            },
            "costDetails": {"total": "0.00002"},
            "metadata": {
                "attributes.langfuse.experiment.item.id": dataset_item_id,
                "match": "Home vs Away",
                "openaiReasoningEffort": "none",
                "openaiRequestedServiceTier": "flex",
                "openaiFinalServiceTier": "flex",
                "openaiExecutionStrategy": "flex-first-standard-fallback",
                "openaiServiceTierFallbackUsed": False,
            },
            "modelParameters": {"service_tier": "flex"},
        }

    @staticmethod
    def _compact_usage_record(index: int) -> dict:
        return {
            "datasetType": "repeated-match-slice",
            "measuredGroup": "repeated-match-slice-measured",
            "runName": "same-run-name",
            "traceId": f"trace-{index}",
            "observationId": f"observation-{index}",
            "datasetItemId": f"item-{index}",
            "inputTokens": 100,
            "cachedInputTokens": 0,
            "uncachedInputTokens": 100,
            "outputTokens": 10,
            "reasoningTokens": 0,
            "totalTokens": 110,
            "observedTotalCost": "0.00002",
            "model": "gpt-5.6-luna",
            "reasoningEffort": "none",
            "requestedServiceTier": "flex",
            "serviceTier": "flex",
            "serviceTierFallbackUsed": False,
        }

    @classmethod
    def _dataset_bound_records(cls) -> list[dict]:
        return [
            {
                **cls._compact_usage_record(index),
                "datasetId": "dataset-1",
                "datasetRunId": "current-run-id",
                "datasetRunName": "same-run-name",
                "preparedManifestSha256": "manifest-hash",
                "preparedManifestSampleSize": 2,
            }
            for index in range(2)
        ]

    @staticmethod
    def _calculate_base_row(estimator, records: list[dict]):
        with mock.patch.object(estimator, "load_json", return_value=records):
            return estimator.calculate_base_row(
                SimpleNamespace(
                    input="ignored-usage.json",
                    group="repeated-match-slice-measured",
                    expect_count=2,
                    model="gpt-5.6-luna",
                    reasoning_effort="none",
                    prompt_route="test",
                    model_knowledge_cutoff="2026-02-16",
                    sampling_cutoff="2026-02-18T00:00:00 Europe/Berlin (+01)",
                    max_output_tokens=10000,
                    source="unit test",
                    pricing_source="src/OpenAiIntegration/CostCalculationService.cs",
                    service_tier="flex",
                )
            )

    @staticmethod
    def _collect_args(
        group: str,
        run_name: str,
        *,
        dataset_id: list[str] | None = None,
        dataset_run_id: list[str] | None = None,
        manifest: list[str] | None = None,
    ) -> SimpleNamespace:
        return SimpleNamespace(
            group=[f"{group}={run_name}"],
            expect=[f"{group}=20"],
            dataset_id=dataset_id or [],
            dataset_run_id=dataset_run_id or [],
            manifest=manifest or [],
            env="unused.env",
            wait_timeout_seconds=0,
            wait_interval_seconds=0,
            no_wait_for_expectations=True,
            langfuse_sleep_seconds=0,
            langfuse_max_retries=0,
        )


if __name__ == "__main__":
    unittest.main()
