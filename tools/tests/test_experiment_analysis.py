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
            self.assertIn("Multi-run comparison", html_output.read_text(encoding="utf-8"))

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
