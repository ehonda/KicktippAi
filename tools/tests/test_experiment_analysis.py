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
            self.assertEqual(report_json["comparison"]["betterRunDisplayName"], "o3")
            self.assertEqual(report_json["comparison"]["perItemOutcomeCounts"], {"wins": 2, "ties": 2, "losses": 0})
            self.assertIn("Per-Item Win/Tie/Loss Counts", report_markdown)
            self.assertIn("| 1 | o3 | o3 | 10.0000 |", report_markdown)
            self.assertIn("Two-run comparison", report_html)
            self.assertIn("<td>o3</td>", report_html)
            self.assertNotIn("<td>slice__test-community__o3</td>", report_html)

    def test_repeated_match_report_displays_metadata_methods_short_labels_and_badges(self) -> None:
        bundle = {
            "datasetName": (
                "match-predictions/bundesliga-2025-26/pes-squad/repeated-match/"
                "md26-vfb-stuttgart-vs-rb-leipzig/repeat-25"
            ),
            "datasetDescription": (
                "Stuttgart's 1-0 Matchday 26 win over Leipzig was a close top-four clash "
                "where Stuttgart leapfrogged Leipzig."
            ),
            "datasetMetadata": {
                "fixture": "VfB Stuttgart vs RB Leipzig",
                "actualResult": "1:0",
                "actualResultDisplay": "VfB Stuttgart 1 - 0 RB Leipzig",
                "matchday": 26,
                "repetitionCount": 25,
                "interestingBecause": "Close top-four clash where Stuttgart leapfrogged Leipzig.",
            },
            "taskType": "repeated-match",
            "primaryMetricName": "avg_kicktipp_points",
            "runs": [
                {
                    "runName": "repeated-match__pes-squad__o3__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                    "model": "o3",
                    "runSubjectDisplayName": "o3",
                    "primaryMetricValue": 2.5,
                    "aggregateScores": {"total_kicktipp_points": 10.0, "avg_kicktipp_points": 2.5},
                },
                {
                    "runName": "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                    "model": "gpt-5-nano",
                    "runSubjectDisplayName": "gpt-5-nano",
                    "primaryMetricValue": 1.0,
                    "aggregateScores": {"total_kicktipp_points": 4.0, "avg_kicktipp_points": 1.0},
                },
            ],
            "rows": [
                {
                    "pairingKey": f"repeat-{index}",
                    "runName": run_name,
                    "kicktippPoints": points,
                }
                for index, (o3_points, nano_points) in enumerate([(4, 2), (2, 2), (0, 0), (4, 0)], start=1)
                for run_name, points in [
                    (
                        "repeated-match__pes-squad__o3__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                        o3_points,
                    ),
                    (
                        "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                        nano_points,
                    ),
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

        self.assertEqual(report_json["datasetMetadata"]["fixture"], "VfB Stuttgart vs RB Leipzig")
        self.assertIn("## Dataset Metadata", report_markdown)
        self.assertIn("| Fixture | VfB Stuttgart vs RB Leipzig |", report_markdown)
        self.assertIn("| Actual Result | VfB Stuttgart 1 - 0 RB Leipzig |", report_markdown)
        self.assertIn("| Repetitions | 25 |", report_markdown)
        self.assertIn("Paired Wilcoxon signed-rank test", report_markdown)
        self.assertIn("Wilcoxon p-value", report_markdown)
        self.assertIn("Effect Size Confidence Intervals", report_markdown)
        self.assertIn("- Better Run: `o3`", report_markdown)
        self.assertIn("- Other Run: `gpt-5-nano`", report_markdown)
        self.assertNotIn("repeated-match__pes-squad__o3__prompt-v1", report_markdown)
        self.assertIn("<h2>Dataset metadata</h2>", report_html)
        self.assertIn("VfB Stuttgart 1 - 0 RB Leipzig", report_html)
        self.assertIn("Two-run comparison", report_html)
        self.assertIn("pill-", report_html)
        self.assertNotIn("<td>repeated-match__pes-squad__o3__prompt-v1", report_html)

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
        comparison_matrix = report.build_standings_comparison_matrix(report_json)
        self.assertEqual(
            comparison_matrix["community__alice-1"]["community__bob-2"]["significance"],
            "worse",
        )
        self.assertEqual(
            comparison_matrix["community__bob-2"]["community__alice-1"]["significance"],
            "better",
        )

        self.assertIn("## Community Standings", report_markdown)
        self.assertIn("| Rank | Participant | Kicktipp Points |", report_markdown)
        self.assertIn("| 1 | Alice | 24 |", report_markdown)
        self.assertNotIn("| Rank | Run | Model | Primary Metric |", report_markdown)
        self.assertIn("<h2>Community standings</h2>", report_html)
        self.assertIn("<th>Participant</th>", report_html)
        self.assertIn("<th>Kicktipp Points</th>", report_html)
        self.assertIn("<th>p-value vs baseline</th>", report_html)
        self.assertIn("<td>Alice</td>", report_html)
        self.assertIn("data-standings-table", report_html)
        self.assertIn('data-default-baseline="community__alice-1"', report_html)
        self.assertIn("data-standings-pvalue", report_html)
        self.assertIn('id="standings-comparison-data"', report_html)
        self.assertIn("selectStandingsBaseline", report_html)
        self.assertIn("pvalue-badge-worse", report_html)
        self.assertIn("--baseline: #b03a93;", report_html)
        self.assertIn("background: var(--baseline-soft);", report_html)
        self.assertIn("box-shadow: inset 4px 0 0 var(--baseline);", report_html)
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
