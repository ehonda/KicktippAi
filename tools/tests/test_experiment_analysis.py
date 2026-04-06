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

            with redirect_stdout(io.StringIO()):
                exit_code = report.main(
                    [
                        "--input",
                        str(fixture),
                        "--json-output",
                        str(json_output),
                        "--markdown-output",
                        str(markdown_output),
                        "--bootstrap-resamples",
                        "500",
                    ]
                )

            self.assertEqual(exit_code, 0)
            report_json = json.loads(json_output.read_text(encoding="utf-8"))
            report_markdown = markdown_output.read_text(encoding="utf-8")

            self.assertEqual(report_json["primaryMetricName"], "total_kicktipp_points")
            self.assertEqual(report_json["comparison"]["betterRunName"], "slice__test-community__o3")
            self.assertEqual(report_json["comparison"]["perItemOutcomeCounts"], {"wins": 2, "ties": 2, "losses": 0})
            self.assertIn("Per-Item Win/Tie/Loss Counts", report_markdown)

    def test_three_run_bundle_produces_friedman_and_pairwise_comparisons(self) -> None:
        fixture = Path("tools/tests/fixtures/three_run_repeated_match_bundle.json")
        with tempfile.TemporaryDirectory() as temp_directory:
            json_output = Path(temp_directory) / "report.json"
            markdown_output = Path(temp_directory) / "report.md"

            with redirect_stdout(io.StringIO()):
                exit_code = report.main(
                    [
                        "--input",
                        str(fixture),
                        "--json-output",
                        str(json_output),
                        "--markdown-output",
                        str(markdown_output),
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


if __name__ == "__main__":
    unittest.main()