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

    def test_reasoning_effort_is_displayed_next_to_model_when_no_subject_label_exists(self) -> None:
        bundle = {
            "datasetName": (
                "match-predictions/bundesliga-2025-26/pes-squad/repeated-match/"
                "md26-vfb-stuttgart-vs-rb-leipzig/repeat-2"
            ),
            "taskType": "repeated-match",
            "primaryMetricName": "avg_kicktipp_points",
            "runs": [
                {
                    "runName": "gpt-55-none",
                    "model": "gpt-5.5",
                    "reasoningEffort": "none",
                    "primaryMetricValue": 2.0,
                    "aggregateScores": {"total_kicktipp_points": 4.0, "avg_kicktipp_points": 2.0},
                },
                {
                    "runName": "gpt-55-xhigh",
                    "model": "gpt-5.5",
                    "reasoningEffort": "xhigh",
                    "primaryMetricValue": 1.0,
                    "aggregateScores": {"total_kicktipp_points": 2.0, "avg_kicktipp_points": 1.0},
                },
            ],
            "rows": [
                {"pairingKey": "repeat-1", "runName": "gpt-55-none", "kicktippPoints": 2},
                {"pairingKey": "repeat-1", "runName": "gpt-55-xhigh", "kicktippPoints": 1},
                {"pairingKey": "repeat-2", "runName": "gpt-55-none", "kicktippPoints": 2},
                {"pairingKey": "repeat-2", "runName": "gpt-55-xhigh", "kicktippPoints": 1},
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

        self.assertEqual(report_json["reportTitle"], "gpt-5.5 (none) vs gpt-5.5 (xhigh)")
        self.assertEqual(report_json["comparison"]["betterRunDisplayName"], "gpt-5.5 (none)")
        self.assertEqual(report_json["comparison"]["otherRunDisplayName"], "gpt-5.5 (xhigh)")
        self.assertIn("gpt-5.5 (none)", report_markdown)
        self.assertIn("gpt-5.5 (xhigh)", report_html)
        self.assertIn(
            '<meta name="kicktippai-report-title" content="gpt-5.5 (none) vs gpt-5.5 (xhigh)">',
            report_html,
        )
        self.assertIn("<h1>gpt-5.5 (none) vs gpt-5.5 (xhigh)</h1>", report_html)

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
                    "homeTeam": "VfB Stuttgart",
                    "awayTeam": "RB Leipzig",
                    "startsAt": "2026-03-15T19:30:00 Europe/Berlin (+01)",
                    "matchday": 26,
                    "predictedHomeGoals": predicted_home_goals,
                    "predictedAwayGoals": predicted_away_goals,
                    "expectedHomeGoals": 1,
                    "expectedAwayGoals": 0,
                }
                for index, (o3_points, nano_points, o3_prediction, nano_prediction) in enumerate(
                    [
                        (4, 0, (1, 0), (1, 2)),
                        (3, 0, (1, 0), (1, 2)),
                        (2, 0, (1, 0), (1, 2)),
                        (4, 0, (1, 0), (1, 2)),
                        (3, 0, (1, 0), (1, 2)),
                        (2, 0, (1, 0), (0, 1)),
                        (4, 0, (2, 1), (0, 1)),
                        (3, 0, (2, 1), (0, 1)),
                    ],
                    start=1,
                )
                for run_name, points, (predicted_home_goals, predicted_away_goals) in [
                    (
                        "repeated-match__pes-squad__o3__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                        o3_points,
                        o3_prediction,
                    ),
                    (
                        "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__2026-03-15t12-00-00z",
                        nano_points,
                        nano_prediction,
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
        self.assertEqual(report_json["matchSummary"]["fixture"], "VfB Stuttgart vs RB Leipzig")
        self.assertEqual(report_json["matchSummary"]["actualResultDisplay"], "VfB Stuttgart 1 - 0 RB Leipzig")
        self.assertEqual(report_json["predictionDistributions"][0]["scoreCounts"][0]["score"], "1:0")
        self.assertEqual(report_json["predictionDistributions"][0]["scoreCounts"][0]["count"], 6)
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
        self.assertIn("<h2>At a glance</h2>", report_html)
        self.assertIn("Match to predict", report_html)
        self.assertIn("Compact head to head", report_html)
        self.assertIn("Prediction distribution", report_html)
        self.assertIn("model-winner", report_html)
        self.assertIn("model-loser", report_html)
        self.assertIn('<details class="panel collapsible-panel">', report_html)
        self.assertIn("<h2>Dataset metadata</h2>", report_html)
        self.assertIn("VfB Stuttgart 1 - 0 RB Leipzig", report_html)
        self.assertIn("Two-run comparison", report_html)
        self.assertIn("pill-", report_html)
        self.assertNotIn("<td>repeated-match__pes-squad__o3__prompt-v1", report_html)

    def test_single_run_knowledge_cutoff_follow_up_renders_exact_score_signal(self) -> None:
        run_name = (
            "repeated-match__pes-squad__o3__langfuse-o3-poc__reasoning-medium__"
            "repeat-100-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-30t20-25-39z"
        )
        bundle = {
            "datasetName": (
                "match-predictions/bundesliga-2025-26/pes-squad/repeated-match/"
                "md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1"
            ),
            "datasetMetadata": {
                "fixture": "FC Bayern München vs RB Leipzig",
                "actualResult": "6:0",
                "actualResultDisplay": "FC Bayern München 6 - 0 RB Leipzig",
                "matchday": 1,
                "repetitionCount": 100,
            },
            "taskType": "repeated-match",
            "primaryMetricName": "avg_kicktipp_points",
            "runs": [
                {
                    "runName": run_name,
                    "model": "o3",
                    "promptKey": "langfuse-o3-poc",
                    "promptSource": "langfuse",
                    "langfusePromptName": "kicktippai/predict-one-match-o3-poc",
                    "langfusePromptLabel": "poc",
                    "reasoningEffort": "medium",
                    "maxOutputTokens": 10000,
                    "selectedItemIdsHash": "hash-123",
                    "batchCount": 9,
                    "evaluationTime": "2025-08-22T12:00:00 Europe/Berlin (+02)",
                    "primaryMetricValue": 3.0,
                    "aggregateScores": {"total_kicktipp_points": 6.0, "avg_kicktipp_points": 3.0},
                }
            ],
            "rows": [
                {
                    "pairingKey": "repeat-1",
                    "runName": run_name,
                    "kicktippPoints": 4,
                    "homeTeam": "FC Bayern München",
                    "awayTeam": "RB Leipzig",
                    "startsAt": "2025-08-22T21:30:00 Europe/Berlin (+02)",
                    "matchday": 1,
                    "predictedHomeGoals": 6,
                    "predictedAwayGoals": 0,
                    "expectedHomeGoals": 6,
                    "expectedAwayGoals": 0,
                },
                {
                    "pairingKey": "repeat-2",
                    "runName": run_name,
                    "kicktippPoints": 2,
                    "homeTeam": "FC Bayern München",
                    "awayTeam": "RB Leipzig",
                    "startsAt": "2025-08-22T21:30:00 Europe/Berlin (+02)",
                    "matchday": 1,
                    "predictedHomeGoals": 3,
                    "predictedAwayGoals": 1,
                    "expectedHomeGoals": 6,
                    "expectedAwayGoals": 0,
                },
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
        report_html = report.render_html(report_json)

        self.assertEqual(report_json["reportTitle"], "o3 (medium) 2x knowledge cutoff follow-up")
        self.assertEqual(report_json["singleRunSummary"]["exactPredictionCount"], 1)
        self.assertEqual(report_json["singleRunSummary"]["predictionCount"], 2)
        self.assertIn("Single-run follow-up", report_html)
        self.assertIn("Exact 6:0 Signal", report_html)
        self.assertIn("6:0 predictions", report_html)
        self.assertIn("1 / 2", report_html)
        self.assertIn("FC Bayern München 6 - 0 RB Leipzig", report_html)
        self.assertIn("Prediction Distribution", report_html)
        self.assertIn("Run Metadata", report_html)
        self.assertIn("kicktippai/predict-one-match-o3-poc", report_html)

    def test_repeated_match_slice_groups_rows_by_repetition_totals(self) -> None:
        run_a = "repeated-match-slice__test-community__a"
        run_b = "repeated-match-slice__test-community__b"
        bundle = {
            "datasetName": (
                "match-predictions/bundesliga-2025-26/test-community/repeated-match-slices/"
                "all-matchdays/random-2x2-seed-42"
            ),
            "datasetMetadata": {
                "matchCount": 2,
                "repetitions": 2,
                "predictionCount": 4,
            },
            "taskType": "repeated-match-slice",
            "primaryMetricName": "avg_kicktipp_points",
            "runs": [
                {
                    "runName": run_a,
                    "model": "model-a",
                    "runSubjectDisplayName": "Model A",
                    "primaryMetricValue": 4.0,
                    "aggregateScores": {"total_kicktipp_points": 8.0, "avg_kicktipp_points": 4.0},
                },
                {
                    "runName": run_b,
                    "model": "model-b",
                    "runSubjectDisplayName": "Model B",
                    "primaryMetricValue": 3.0,
                    "aggregateScores": {"total_kicktipp_points": 6.0, "avg_kicktipp_points": 3.0},
                },
            ],
            "rows": [
                {
                    "pairingKey": f"{run_name}-fixture-{fixture_index}-rep-{repetition_index}",
                    "runName": run_name,
                    "kicktippPoints": points,
                    "sourceDatasetItemId": f"bundesliga-2025-26__test-community__fixture-{fixture_index}",
                    "fixtureIndex": fixture_index,
                    "repetitionIndex": repetition_index,
                    "homeTeam": f"Home {fixture_index}",
                    "awayTeam": f"Away {fixture_index}",
                    "startsAt": "2026-03-15T15:30:00 Europe/Berlin (+01)",
                    "matchday": fixture_index,
                    "predictedHomeGoals": 1,
                    "predictedAwayGoals": 0,
                    "expectedHomeGoals": 1,
                    "expectedAwayGoals": 0,
                }
                for run_name, scores in [
                    (run_a, {(1, 1): 4, (2, 1): 0, (1, 2): 2, (2, 2): 2}),
                    (run_b, {(1, 1): 2, (2, 1): 0, (1, 2): 4, (2, 2): 0}),
                ]
                for (fixture_index, repetition_index), points in scores.items()
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

        self.assertEqual(report_json["primaryMetricName"], "avg_kicktipp_points")
        self.assertEqual(report_json["pairingCount"], 2)
        self.assertEqual(report_json["comparison"]["betterRunName"], run_a)
        self.assertEqual(report_json["comparison"]["perItemOutcomeCounts"], {"wins": 1, "ties": 1, "losses": 0})
        self.assertEqual(len(report_json["matchBreakdown"]), 2)
        self.assertEqual(report_json["matchBreakdown"][0]["fixture"], "Home 1 vs Away 1")
        self.assertEqual(report_json["matchBreakdown"][0]["actualResultDisplay"], "Home 1 1 - 0 Away 1")
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][0]["predictionCount"], 2)
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][0]["averageKicktippPoints"], 3.0)
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][0]["scoreCounts"][0]["score"], "1:0")
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][0]["scoreCounts"][0]["count"], 2)
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][0]["scoreCounts"][0]["averageKicktippPoints"], 3.0)
        self.assertIn("| Matches | 2 |", report_markdown)
        self.assertIn("| Repetitions | 2 |", report_markdown)
        self.assertIn("| Predictions | 4 |", report_markdown)
        self.assertIn('<details class="panel collapsible-panel matches-panel">', report_html)
        self.assertIn("<h2>Matches</h2>", report_html)
        self.assertIn("2 fixtures", report_html)
        self.assertIn("Individual matches do not run significance tests", report_html)
        self.assertIn('<h3 class="match-fixture match-title">', report_html)
        self.assertIn('<span>Home 1</span><span class="match-title-score">1:0</span><span>Away 1</span>', report_html)
        self.assertNotIn("Source item:", report_html)
        self.assertIn("points-badge points-badge-3", report_html)
        self.assertIn("score-color-3", report_html)
        self.assertIn("background: linear-gradient(90deg, #aeb7c5, #40546f);", report_html)
        self.assertIn("background: #e4efff;", report_html)
        self.assertIn("background: #f6e8bd;", report_html)
        self.assertIn("background: #dcefd8;", report_html)
        self.assertIn(">3pt</span>", report_html)
        self.assertIn("Model A", report_html)

    def test_repeated_match_slice_match_breakdown_supports_multi_run_html(self) -> None:
        run_a = "repeated-match-slice__test-community__a"
        run_b = "repeated-match-slice__test-community__b"
        run_c = "repeated-match-slice__test-community__c"
        run_names = [run_a, run_b, run_c]
        display_names = {
            run_a: "Model A",
            run_b: "Model B",
            run_c: "Model C",
        }
        score_by_run_fixture_repetition = {
            run_a: {
                (1, 1): (4, (1, 0)),
                (1, 2): (2, (2, 1)),
                (1, 3): (4, (1, 0)),
                (2, 1): (0, (0, 1)),
                (2, 2): (2, (1, 1)),
                (2, 3): (2, (1, 2)),
            },
            run_b: {
                (1, 1): (2, (1, 1)),
                (1, 2): (2, (1, 1)),
                (1, 3): (2, (1, 1)),
                (2, 1): (1, (0, 2)),
                (2, 2): (1, (0, 2)),
                (2, 3): (1, (0, 2)),
            },
            run_c: {
                (1, 1): (1, (0, 0)),
                (1, 2): (1, (0, 0)),
                (1, 3): (1, (0, 0)),
                (2, 1): (1, (1, 3)),
                (2, 2): (1, (1, 3)),
                (2, 3): (1, (1, 3)),
            },
        }
        bundle = {
            "datasetName": (
                "match-predictions/bundesliga-2025-26/test-community/repeated-match-slices/"
                "all-matchdays/random-2x3-seed-42"
            ),
            "datasetMetadata": {
                "matchCount": 2,
                "repetitions": 3,
                "predictionCount": 6,
            },
            "taskType": "repeated-match-slice",
            "primaryMetricName": "avg_kicktipp_points",
            "runs": [
                {
                    "runName": run_name,
                    "model": display_names[run_name].lower().replace(" ", "-"),
                    "runSubjectDisplayName": display_names[run_name],
                    "primaryMetricValue": 4.0 - run_index,
                    "aggregateScores": {"total_kicktipp_points": 12.0 - run_index, "avg_kicktipp_points": 4.0 - run_index},
                }
                for run_index, run_name in enumerate(run_names)
            ],
            "rows": [
                {
                    "pairingKey": f"{run_name}-fixture-{fixture_index}-rep-{repetition_index}",
                    "runName": run_name,
                    "kicktippPoints": points,
                    "sourceDatasetItemId": f"bundesliga-2025-26__test-community__fixture-{fixture_index}",
                    "fixtureIndex": fixture_index,
                    "repetitionIndex": repetition_index,
                    "homeTeam": f"Home {fixture_index}",
                    "awayTeam": f"Away {fixture_index}",
                    "startsAt": "2026-03-15T15:30:00 Europe/Berlin (+01)",
                    "matchday": fixture_index,
                    "predictedHomeGoals": predicted_home_goals,
                    "predictedAwayGoals": predicted_away_goals,
                    "expectedHomeGoals": 1,
                    "expectedAwayGoals": 0,
                }
                for run_name, scores in score_by_run_fixture_repetition.items()
                for (fixture_index, repetition_index), (points, (predicted_home_goals, predicted_away_goals)) in scores.items()
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
        report_html = report.render_html(report_json)

        self.assertEqual(report_json["runCount"], 3)
        self.assertEqual(len(report_json["matchBreakdown"]), 2)
        self.assertEqual(len(report_json["matchBreakdown"][0]["runs"]), 3)
        self.assertEqual(report_json["matchBreakdown"][0]["runs"][2]["runDisplayName"], "Model C")
        self.assertIn("Multi-run comparison", report_html)
        self.assertIn('<details class="panel collapsible-panel matches-panel">', report_html)
        self.assertIn("2 fixtures", report_html)
        self.assertIn("Model C", report_html)
        self.assertIn('<span>Home 2</span><span class="match-title-score">1:0</span><span>Away 2</span>', report_html)
        self.assertIn("points-badge", report_html)

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
