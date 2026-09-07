#!/usr/bin/env python3
"""Deterministic forward tests for session-analysis manifests and focuses."""

from __future__ import annotations

import argparse
import contextlib
import json
import pathlib
import shutil
import unittest
import uuid
from collections.abc import Iterator

import focus_registry
import report_manifest
import run_analysis
import create_report_shell


REPO_TMP = pathlib.Path(__file__).resolve().parents[4] / ".tmp"


@contextlib.contextmanager
def temporary_directory() -> Iterator[str]:
    REPO_TMP.mkdir(parents=True, exist_ok=True)
    path = REPO_TMP / f"session-analysis-test-{uuid.uuid4().hex}"
    path.mkdir()
    try:
        yield str(path)
    finally:
        shutil.rmtree(path)


def focus(focus_id: str, cadence: str = "standing") -> dict:
    return {
        "id": focus_id,
        "questions": [f"How should {focus_id} be evaluated?"],
        "created_at": "2026-09-07",
        "applicability": {
            "session_kinds": ["orchestration"],
            "description": "Test orchestration sessions.",
        },
        "cadence": cadence,
        "status": "active",
        "evidence_source_hints": ["transcript"],
        "resolution": {
            "last_covered_by": None,
            "retired_by": None,
            "superseded_by": None,
        },
    }


def registry(*focuses: dict) -> dict:
    return {
        "schema_version": 1,
        "updated_at": "2026-09-07",
        "focuses": sorted(focuses, key=lambda item: item["id"]),
    }


class FocusRegistryTests(unittest.TestCase):
    def test_atomic_round_trip_and_validation(self) -> None:
        value = registry(focus("standing-question"))
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "focuses.json"
            focus_registry.atomic_write_registry(path, value)
            self.assertEqual(focus_registry.read_registry(path), value)

    def test_add_infers_id_and_manual_retire_records_pointer(self) -> None:
        value = registry()
        add_args = argparse.Namespace(
            id=None, question=["Should the writer throttle change after the pilot?"],
            created_at="2026-09-07", cadence="once",
            session_kind=["orchestration"], applicability="The next pilot run.",
            evidence_source=["transcript"], allow_overlap=False,
        )
        focus_id = focus_registry.add_focus(value, add_args)
        self.assertEqual(focus_id, "should-the-writer-throttle-change-after-the-pilot")
        retire_args = argparse.Namespace(
            id=focus_id, report_id="pilot-report",
            manifest="docs/codex/session-analysis/reports/pilot-report.report.json",
            covered_at="2026-09-09",
        )
        focus_registry.retire_focus(value, retire_args)
        focus_registry.validate_registry(value)
        retired = focus_registry.focus_by_id(value, focus_id)
        self.assertEqual(retired["status"], "retired")
        self.assertEqual(retired["resolution"]["retired_by"]["report_id"], "pilot-report")

    def test_add_rejects_likely_duplicate(self) -> None:
        value = registry(focus("existing-question"))
        value["focuses"][0]["questions"] = ["How much useful parallel work happened?"]
        args = argparse.Namespace(
            id="parallel-work", question=["How much useful parallel work happened in the run?"],
            created_at="2026-09-07", cadence="standing",
            session_kind=["orchestration"], applicability="All orchestration runs.",
            evidence_source=["transcript"], allow_overlap=False,
        )
        with self.assertRaisesRegex(focus_registry.RegistryError, "overlapping"):
            focus_registry.add_focus(value, args)

    def test_update_and_supersede(self) -> None:
        value = registry(focus("old-question"), focus("new-question"))
        update_args = argparse.Namespace(
            id="new-question", question=["What changed after reconciliation?"],
            cadence=None, session_kind=None, applicability=None,
            evidence_source=["git", "review"], updated_at="2026-09-08",
            allow_overlap=False,
        )
        focus_registry.update_focus(value, update_args)
        supersede_args = argparse.Namespace(
            id="old-question", by="new-question", updated_at="2026-09-08"
        )
        focus_registry.supersede_focus(value, supersede_args)
        focus_registry.validate_registry(value)
        self.assertEqual(
            focus_registry.focus_by_id(value, "old-question")["resolution"]["superseded_by"],
            "new-question",
        )

    def test_report_coverage_retires_once_and_keeps_standing_active(self) -> None:
        value = registry(focus("one-off", "once"), focus("standing"))
        manifest = {
            "id": "test-report",
            "published_at": "2026-09-09",
            "focus_ids": ["one-off", "standing"],
        }
        changed = focus_registry.cover_from_manifest(
            value, manifest, pathlib.Path("docs/codex/session-analysis/reports/test-report.report.json")
        )
        self.assertEqual(changed, ["one-off", "standing"])
        self.assertEqual(focus_registry.focus_by_id(value, "one-off")["status"], "retired")
        self.assertEqual(focus_registry.focus_by_id(value, "standing")["status"], "active")
        self.assertEqual(
            focus_registry.focus_by_id(value, "standing")["resolution"]["last_covered_by"]["report_id"],
            "test-report",
        )
        focus_registry.validate_registry(value)


class ReportManifestTests(unittest.TestCase):
    def test_report_shell_is_self_contained_and_escapes_metadata(self) -> None:
        manifest = {
            "title": "A <bounded> report",
            "summary": "Question & evidence",
            "eyebrow": "Analysis",
            "published_at": "2026-09-09",
        }
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            rendered = create_report_shell.render_report_shell(manifest)
            self.assertIn("A &lt;bounded&gt; report", rendered)
            self.assertIn("Question &amp; evidence", rendered)
            path.write_text(rendered, encoding="utf-8")
            report_manifest.verify_self_contained_html(path)

    def test_manifest_consumption_is_required_then_validated(self) -> None:
        with temporary_directory() as directory:
            repo = pathlib.Path(directory).resolve()
            source = repo / "docs" / "codex" / "test-investigation"
            html = repo / "session-analysis" / "test-report" / "index.html"
            manifests = repo / "docs" / "codex" / "session-analysis" / "reports"
            source.mkdir(parents=True)
            html.parent.mkdir(parents=True)
            manifests.mkdir(parents=True)
            html.write_text("<!doctype html><html><body>report</body></html>", encoding="utf-8")
            manifest = {
                "schema_version": 1,
                "id": "test-report",
                "title": "Test report",
                "summary": "A forward-test report.",
                "eyebrow": "Test",
                "published_at": "2026-09-09",
                "session_kind": "orchestration",
                "source_path": "docs/codex/test-investigation",
                "site_path": "session-analysis/test-report",
                "html_file": "index.html",
                "focus_ids": ["one-off"],
                "analysis": None,
            }
            manifest_path = manifests / "test-report.report.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            registry_path = repo / "docs" / "codex" / "session-analysis" / "focuses.json"
            value = registry(focus("one-off", "once"))
            focus_registry.atomic_write_registry(registry_path, value)

            with self.assertRaisesRegex(focus_registry.RegistryError, "not consumed"):
                report_manifest.verify_manifest_set(
                    manifests, registry_path, repo, verify_html=True
                )
            report_manifest.verify_manifest_set(
                manifests, registry_path, repo, verify_html=True,
                allow_unconsumed_focuses=True,
            )
            focus_registry.cover_from_manifest(
                value, manifest, manifest_path.relative_to(repo)
            )
            focus_registry.atomic_write_registry(registry_path, value)
            report_manifest.verify_manifest_set(
                manifests, registry_path, repo, verify_html=True
            )

    def test_self_contained_html_accepts_embedded_assets(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><head><style>body{color:black}</style></head>"
                "<body><img src='data:image/png;base64,AA=='><a href='https://example.com/source'>"
                "source</a><script>void 0</script></body></html>",
                encoding="utf-8",
            )
            report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_runtime_script(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><body><script src='https://example.com/app.js'></script></body></html>",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(focus_registry.RegistryError, "external runtime"):
                report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_external_image(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><body><img src='chart.png'></body></html>",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(focus_registry.RegistryError, "external runtime"):
                report_manifest.verify_self_contained_html(path)

    def test_output_privacy_rejects_high_confidence_credential(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "analysis.json"
            path.write_text(
                '{"token":"sk-proj-abcdefghijklmnopqrstuvwxyz"}',
                encoding="utf-8",
            )
            with self.assertRaisesRegex(focus_registry.RegistryError, "possible credential"):
                run_analysis.verify_output_privacy(path.parent)


if __name__ == "__main__":
    unittest.main()
