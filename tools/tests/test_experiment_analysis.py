from __future__ import annotations

import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

from tools.experiment_analysis import report


class ExperimentAnalysisReportTests(unittest.TestCase):
    def test_two_run_slice_bundle_produces_primary_metric_and_per_item_outcomes(self) -> None:
        fixture = Path("tools/tests/fixtures/two_run_slice_bundle.json")
        with tempfile.TemporaryDirectory() as temp_directory:
            json_output = Path(temp_directory) / "report.json"
            markdown_output = Path(temp_directory) / "report.md"
            html_output = Path(temp_directory) / "report.html"

            with redirect_stdout(io.StringIO()):
                exit_code = report.main(
                    [
                        "--input",
                        str(fixture),
                        "--json-output",
                        str(json_output),
                        "--markdown-output",
                        str(markdown_output),
                        "--html-output",
                        str(html_output),
                        "--bootstrap-resamples",
                        "500",
                    ]
                )

            self.assertEqual(exit_code, 0)
            report_json = json.loads(json_output.read_text(encoding="utf-8"))
            report_markdown = markdown_output.read_text(encoding="utf-8")
            report_html = html_output.read_text(encoding="utf-8")

            self.assertEqual(report_json["primaryMetricName"], "total_kicktipp_points")
            self.assertEqual(report_json["comparison"]["betterRunName"], "slice__test-community__o3")
            self.assertEqual(report_json["comparison"]["perItemOutcomeCounts"], {"wins": 2, "ties": 2, "losses": 0})
            self.assertIn("Per-Item Win/Tie/Loss Counts", report_markdown)
            self.assertIn("Two-run comparison", report_html)
            self.assertIn("slice__test-community__o3", report_html)

    def test_three_run_bundle_produces_friedman_and_pairwise_comparisons(self) -> None:
        fixture = Path("tools/tests/fixtures/three_run_repeated_match_bundle.json")
        with tempfile.TemporaryDirectory() as temp_directory:
            json_output = Path(temp_directory) / "report.json"
            markdown_output = Path(temp_directory) / "report.md"
            html_output = Path(temp_directory) / "report.html"

            with redirect_stdout(io.StringIO()):
                exit_code = report.main(
                    [
                        "--input",
                        str(fixture),
                        "--json-output",
                        str(json_output),
                        "--markdown-output",
                        str(markdown_output),
                        "--html-output",
                        str(html_output),
                        "--bootstrap-resamples",
                        "500",
                    ]
                )

            self.assertEqual(exit_code, 0)
            report_json = json.loads(json_output.read_text(encoding="utf-8"))
            report_markdown = markdown_output.read_text(encoding="utf-8")

            self.assertEqual(report_json["primaryMetricName"], "avg_kicktipp_points")
            self.assertIn("friedman", report_json)
            self.assertEqual(len(report_json["pairwiseComparisons"]), 3)
            self.assertIn("Pairwise Comparisons", report_markdown)
            self.assertIn("Raw p-value", report_markdown)
            report_html = html_output.read_text(encoding="utf-8")
            self.assertIn("Multi-run comparison", report_html)
            self.assertIn("Raw p-value", report_html)

    def test_multi_run_html_uses_display_names_and_marks_significant_sortable_rows(self) -> None:
        bundle = {
            "datasetName": "match-predictions/bundesliga-2025-26/test/community-to-date/through-md1/display-test",
            "taskType": "community-to-date",
            "primaryMetricName": "total_kicktipp_points",
            "runs": [
                {
                    "runName": "community__alice-1",
                    "model": "alice-model",
                    "runSubjectDisplayName": "Alice",
                    "primaryMetricValue": 24.0,
                    "aggregateScores": {"total_kicktipp_points": 24.0},
                },
                {
                    "runName": "community__bob-2",
                    "model": "bob-model",
                    "runSubjectDisplayName": "Bob",
                    "primaryMetricValue": 16.0,
                    "aggregateScores": {"total_kicktipp_points": 16.0},
                },
                {
                    "runName": "community__cara-3",
                    "model": "cara-model",
                    "runSubjectDisplayName": "Cara",
                    "primaryMetricValue": 8.0,
                    "aggregateScores": {"total_kicktipp_points": 8.0},
                },
            ],
            "rows": [
                {"pairingKey": f"match-{index}", "runName": run_name, "kicktippPoints": points}
                for index in range(8)
                for run_name, points in [
                    ("community__alice-1", 3),
                    ("community__bob-2", 2),
                    ("community__cara-3", 1),
                ]
            ],
        }

        report_json = report.analyze_bundle(
            bundle,
            alpha=0.05,
            correction_method="holm",
            bootstrap_resamples=100,
            confidence_level=0.95,
            random_seed=20260406,
        )
        report_markdown = report.render_markdown(report_json)
        report_html = report.render_html(report_json)

        first_comparison = report_json["pairwiseComparisons"][0]
        self.assertEqual(first_comparison["runAName"], "community__alice-1")
        self.assertEqual(first_comparison["runBName"], "community__bob-2")
        self.assertEqual(first_comparison["runADisplayName"], "Alice")
        self.assertEqual(first_comparison["runBDisplayName"], "Bob")
        self.assertTrue(first_comparison["wilcoxon"]["significantAfterCorrection"])

        self.assertIn("| Alice | Bob |", report_markdown)
        self.assertNotIn("| community__alice-1 | community__bob-2 |", report_markdown)
        self.assertIn('<td data-label="Run A" data-sort-value="Alice">Alice</td>', report_html)
        self.assertIn('<td data-label="Run B" data-sort-value="Bob">Bob</td>', report_html)
        self.assertNotIn(
            '<td data-label="Run A" data-sort-value="community__alice-1">community__alice-1</td>',
            report_html,
        )
        self.assertIn('class="pairwise-row pairwise-row-significant"', report_html)
        self.assertIn('class="significance-badge significance-badge-yes"', report_html)
        self.assertIn("data-sortable-table", report_html)
        self.assertIn('data-sort-type="outcome"', report_html)
        self.assertIn('querySelectorAll("[data-sortable-table]")', report_html)

    def test_default_html_output_path_targets_experiment_analysis_site_tree(self) -> None:
        fixture = Path("tools/tests/fixtures/two_run_slice_bundle.json")
        bundle = report.load_bundle(fixture)
        input_path = Path(
            "artifacts/langfuse-experiments/analysis/slice/"
            "match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260403/"
            "comparison-2026-04-06t00-00-00z.json"
        )

        output_paths = report.resolve_output_paths(bundle, input_path, None, None, None, False)

        self.assertEqual(
            output_paths.html_path,
            Path(
                "experiment-analysis/slices/test-community/all-matchdays/random-4-seed-20260405/"
                "comparison-2026-04-06t00-00-00z.report.html"
            ),
        )


if __name__ == "__main__":
    unittest.main()
