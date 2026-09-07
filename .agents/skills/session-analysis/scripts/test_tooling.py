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
from unittest import mock

import focus_registry
import privacy_checks
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


def analysis_config(source_path: str) -> dict:
    return {
        "root_thread_id": "11111111-1111-1111-1111-111111111111",
        "root_log_name": "rollout-11111111-1111-1111-1111-111111111111.jsonl",
        "event_cutoff_at": "2026-09-09T12:00:00Z",
        "generated_at": "2026-09-09T12:05:00Z",
        "timezone": "Europe/Berlin",
        "privacy": {"include_bounded_excerpts": False},
        "snapshot_lock": f"{source_path}/data/snapshot-lock.json",
        "repository": {"base_commit": "0" * 40, "final_commit": "1" * 40},
        "pricing": {"as_of": "2026-09-09", "models": {}},
        "classification": {"agent_rules": [], "default_group": "other", "turn_code_regex": None},
        "injected_message_prefixes": [],
        "user_message_kinds": {},
        "task_files": {
            "globs": [],
            "code_regex": r"(?P<prefix>p)-(?P<number>\d+)",
        },
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

    def test_add_allows_question_previously_retired(self) -> None:
        prior = focus("prior-question", "once")
        pointer = {
            "report_id": "old-report",
            "manifest": "docs/codex/session-analysis/reports/old-report.report.json",
            "covered_at": "2026-09-08",
        }
        prior["status"] = "retired"
        prior["resolution"]["last_covered_by"] = pointer
        prior["resolution"]["retired_by"] = pointer
        value = registry(prior)
        value["focuses"][0]["questions"] = ["Should a completed question be asked again?"]
        args = argparse.Namespace(
            id="reintroduced-question",
            question=["Should a completed question be asked again?"],
            created_at="2026-09-09", cadence="once",
            session_kind=["orchestration"], applicability="A later run.",
            evidence_source=["transcript"], allow_overlap=False,
        )
        self.assertEqual(focus_registry.add_focus(value, args), "reintroduced-question")

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

    def test_report_coverage_rejects_stale_date(self) -> None:
        item = focus("future-question", "once")
        item["created_at"] = "2026-09-10"
        value = registry(item)
        manifest = {
            "id": "old-report", "published_at": "2026-09-09",
            "focus_ids": ["future-question"],
        }
        with self.assertRaisesRegex(focus_registry.RegistryError, "predates focus creation"):
            focus_registry.cover_from_manifest(
                value, manifest,
                pathlib.Path("docs/codex/session-analysis/reports/old-report.report.json"),
            )


class ReportManifestTests(unittest.TestCase):
    def test_relative_paths_reject_traversal_components(self) -> None:
        for value in ("../outside", "source/./file.json", "source/../outside.json"):
            with self.subTest(value=value):
                with self.assertRaises(focus_registry.RegistryError):
                    report_manifest.require_relative_path(value, "test.path")

    def test_normalized_text_digest_ignores_line_endings(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "contract.json"
            path.write_bytes(b'{\n  "value": true\n}\n')
            expected = report_manifest.normalized_text_sha256(path)
            path.write_bytes(b'{\r\n  "value": true\r\n}\r\n')
            self.assertEqual(report_manifest.normalized_text_sha256(path), expected)

    def test_future_report_cannot_claim_legacy_or_null_analysis(self) -> None:
        manifest = {
            "schema_version": 1,
            "id": "future-report",
            "title": "Future report",
            "summary": "Future analysis.",
            "eyebrow": "Analysis",
            "published_at": "2026-09-09",
            "session_kind": "orchestration",
            "source_path": "docs/codex/future-report",
            "site_path": "session-analysis/future-report",
            "html_file": "index.html",
            "focus_ids": [],
            "legacy": True,
            "analysis_file": None,
            "analysis": None,
        }
        with self.assertRaisesRegex(focus_registry.RegistryError, "fixed legacy report allowlist"):
            report_manifest.validate_manifest(manifest)
        manifest["legacy"] = False
        with self.assertRaisesRegex(focus_registry.RegistryError, "future reports require analysis"):
            report_manifest.validate_manifest(manifest)

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
            config = analysis_config("docs/codex/test-investigation")
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
                "legacy": False,
                "analysis_file": "docs/codex/test-investigation/data/analysis.json",
                "analysis": config,
            }
            manifest_path = manifests / "test-report.report.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            html.write_text(create_report_shell.render_report_shell(manifest), encoding="utf-8")
            lock_path = repo / config["snapshot_lock"]
            lock_path.parent.mkdir(parents=True)
            lock_path.write_text(json.dumps({
                "schema_version": 1,
                "root_thread_id": config["root_thread_id"],
                "event_cutoff_at": config["event_cutoff_at"],
                "threads": [{
                    "thread_id": config["root_thread_id"],
                    "log_file": config["root_log_name"],
                    "included_bytes": 0,
                    "included_records": 0,
                    "sha256": "0" * 64,
                }],
            }) + "\n", encoding="utf-8")
            analysis_path = repo / manifest["analysis_file"]
            artifact = {
                "generated_at": config["generated_at"],
                "source": {
                    "complete_message_bodies_included": False,
                    "report_manifest": manifest_path.relative_to(repo).as_posix(),
                    "report_manifest_sha256": report_manifest.normalized_text_sha256(manifest_path),
                    "focus_ids": manifest["focus_ids"],
                    "root_thread_id": config["root_thread_id"],
                    "root_log": config["root_log_name"],
                    "event_cutoff_at": config["event_cutoff_at"],
                    "repository": config["repository"],
                    "bounded_excerpts_included": False,
                    "snapshot_lock": config["snapshot_lock"],
                    "snapshot_lock_sha256": report_manifest.normalized_text_sha256(lock_path),
                }
            }
            analysis_path.write_text(json.dumps(artifact), encoding="utf-8")
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
            original_registry = registry_path.read_bytes()
            artifact["source"]["focus_ids"] = []
            analysis_path.write_text(json.dumps(artifact), encoding="utf-8")
            with self.assertRaisesRegex(focus_registry.RegistryError, "focus IDs drifted"):
                focus_registry.main([
                    "--registry", str(registry_path), "--repo", str(repo),
                    "cover", "--manifest", str(manifest_path.relative_to(repo)),
                ])
            self.assertEqual(registry_path.read_bytes(), original_registry)

            artifact["source"]["focus_ids"] = manifest["focus_ids"]
            analysis_path.write_text(json.dumps(artifact), encoding="utf-8")
            focus_registry.main([
                "--registry", str(registry_path), "--repo", str(repo),
                "cover", "--manifest", str(manifest_path.relative_to(repo)),
            ])
            report_manifest.verify_manifest_set(
                manifests, registry_path, repo, verify_html=True
            )

    def test_self_contained_html_accepts_embedded_assets(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><head><meta http-equiv='Content-Security-Policy' content=\""
                + report_manifest.OFFLINE_CSP + "\"><style>body{color:black}</style></head>"
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

    def test_self_contained_html_requires_offline_csp(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text("<!doctype html><html><body>offline</body></html>", encoding="utf-8")
            with self.assertRaisesRegex(focus_registry.RegistryError, "Content-Security-Policy"):
                report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_duplicate_csp_directive(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><head><meta http-equiv='Content-Security-Policy' "
                f"content=\"{report_manifest.OFFLINE_CSP}; connect-src https:\"></head>"
                "<body>offline</body></html>",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(focus_registry.RegistryError, "duplicate CSP directive"):
                report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_executable_content_before_csp(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            path.write_text(
                "<!doctype html><html><head><script>new Image().src='https://example.com/x'</script>"
                "<meta http-equiv='Content-Security-Policy' "
                f"content=\"{report_manifest.OFFLINE_CSP}\"></head><body>offline</body></html>",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(focus_registry.RegistryError, "must precede"):
                report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_csp_in_inert_or_reopened_head(self) -> None:
        payloads = (
            "<html><head><template><meta http-equiv='Content-Security-Policy' "
            f"content=\"{report_manifest.OFFLINE_CSP}\"></template>"
            "<script>new Image().src='https://example.com/x'</script></head><body></body></html>",
            "<html><head></head><head><meta http-equiv='Content-Security-Policy' "
            f"content=\"{report_manifest.OFFLINE_CSP}\"></head><body></body></html>",
        )
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            for payload in payloads:
                with self.subTest(payload=payload):
                    path.write_text("<!doctype html>" + payload, encoding="utf-8")
                    with self.assertRaisesRegex(focus_registry.RegistryError, "Content-Security-Policy"):
                        report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_text_or_duplicate_attributes_before_csp(self) -> None:
        payloads = (
            "<html><head>text<meta http-equiv='Content-Security-Policy' "
            f"content=\"{report_manifest.OFFLINE_CSP}\"><script>void 0</script></head>"
            "<body></body></html>",
            "<html><head><meta http-equiv='ignored' "
            "http-equiv='Content-Security-Policy' "
            f"content=\"{report_manifest.OFFLINE_CSP}\"><script>void 0</script></head>"
            "<body></body></html>",
        )
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            for payload in payloads:
                with self.subTest(payload=payload):
                    path.write_text("<!doctype html>" + payload, encoding="utf-8")
                    with self.assertRaises(focus_registry.RegistryError):
                        report_manifest.verify_self_contained_html(path)

    def test_self_contained_html_rejects_network_bypass_shapes(self) -> None:
        csp = (
            "<!doctype html><html><head><meta http-equiv='Content-Security-Policy' "
            f"content=\"{report_manifest.OFFLINE_CSP}\"></head><body>"
        )
        payloads = (
            "<img srcset='https://example.com/chart.png 1x'>",
            "<object data='data:text/html,hello'></object>",
            "<script>new WebSocket('wss://example.com')</script>",
        )
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "index.html"
            for payload in payloads:
                with self.subTest(payload=payload):
                    path.write_text(csp + payload + "</body></html>", encoding="utf-8")
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

    def test_output_privacy_scans_unlisted_text_formats_and_generic_homes(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "diagram.svg"
            values = (
                r"C:\Users\another-user\private\session.jsonl",
                "/home/another-user/private/session.jsonl",
                "/root/private",
                "/root/.codex/sessions/private.jsonl",
                "/var/root",
                "/var/root/.codex/sessions/private.jsonl",
            )
            for value in values:
                with self.subTest(value=value):
                    path.write_text(f"<text>{value}</text>", encoding="utf-8")
                    with self.assertRaisesRegex(focus_registry.RegistryError, "private user-home"):
                        privacy_checks.verify_text_privacy([path.parent])

    def test_root_runtime_distinguishes_agent_ids_from_private_home_paths(self) -> None:
        with temporary_directory() as directory:
            path = pathlib.Path(directory) / "analysis.json"
            with mock.patch("privacy_checks.pathlib.Path.home", return_value=pathlib.Path("/root")):
                for value in ("/root", "/root/task", "/root/task/subtask"):
                    with self.subTest(agent_path=value):
                        path.write_text(json.dumps({"agent_path": value}), encoding="utf-8")
                        privacy_checks.verify_text_privacy([path])
                for value in ("/root/private", "/root/.codex/session.jsonl", "/root/key.pem"):
                    with self.subTest(private_path=value):
                        path.write_text(json.dumps({"path": value}), encoding="utf-8")
                        with self.assertRaisesRegex(focus_registry.RegistryError, "private user-home"):
                            privacy_checks.verify_text_privacy([path])


class ExtractorTests(unittest.TestCase):
    def test_snapshot_lock_detects_cutoff_bounded_family_drift(self) -> None:
        with temporary_directory() as directory:
            repo = pathlib.Path(directory).resolve()
            sessions = repo / "copied-sessions"
            thread_id = "22222222-2222-2222-2222-222222222222"
            log = sessions / f"rollout-2026-09-09-{thread_id}.jsonl"
            sessions.mkdir()
            log.write_text(
                json.dumps({"timestamp": "2026-09-09T12:00:00Z", "type": "session_meta"}) + "\n",
                encoding="utf-8",
            )
            module = run_analysis.load_engine()
            module.ROOT_THREAD_ID = thread_id
            module.ROOT_LOG_NAME = log.name
            module.EVENT_CUTOFF_UTC = module.parse_time("2026-09-09T12:01:00Z")
            config = {
                "root_thread_id": thread_id,
                "root_log_name": log.name,
                "event_cutoff_at": "2026-09-09T12:01:00Z",
                "snapshot_lock": "docs/codex/test/data/snapshot-lock.json",
            }
            lock = run_analysis.verify_snapshot_lock(
                module, sessions.resolve(), repo, config, create=True
            )
            self.assertTrue(lock.is_file())
            run_analysis.verify_snapshot_lock(
                module, sessions.resolve(), repo, config, create=False
            )
            with log.open("a", encoding="utf-8") as handle:
                handle.write(
                    json.dumps({"timestamp": "2026-09-09T12:00:30Z", "type": "event_msg"})
                    + "\n"
                )
            with self.assertRaisesRegex(focus_registry.RegistryError, "bytes drifted"):
                run_analysis.verify_snapshot_lock(
                    module, sessions.resolve(), repo, config, create=False
                )

    def test_custom_sessions_root_and_error_privacy(self) -> None:
        with temporary_directory() as directory:
            sessions = pathlib.Path(directory) / "copied-sessions"
            log = sessions / "nested" / "rollout-test.jsonl"
            log.parent.mkdir(parents=True)
            records = [
                {"timestamp": "2026-09-09T12:00:00Z", "type": "session_meta", "payload": {}},
                {
                    "timestamp": "2026-09-09T12:00:01Z", "type": "event_msg",
                    "payload": {
                        "type": "task_started", "turn_id": "turn-1",
                        "started_at": "2026-09-09T12:00:01Z",
                    },
                },
                {
                    "timestamp": "2026-09-09T12:00:02Z", "type": "event_msg",
                    "payload": {
                        "type": "task_complete", "turn_id": "turn-1",
                        "completed_at": "2026-09-09T12:00:02Z",
                        "error": "private failure detail " * 30,
                    },
                },
            ]
            log.write_text(
                "".join(json.dumps(record) + "\n" for record in records),
                encoding="utf-8",
            )
            module = run_analysis.load_engine()
            module.SESSION_LOG_ROOT = sessions.resolve()
            module.EVENT_CUTOFF_UTC = module.parse_time("2026-09-09T12:01:00Z")
            meta = {
                "thread_id": "test-thread", "kind": "root", "parent_thread_id": None,
                "depth": 0, "agent_path": "/root", "nickname": "root",
                "role": "orchestrator", "spawned_at": None, "path": log,
            }
            module.INCLUDE_BOUNDED_EXCERPTS = False
            parsed = module.parse_thread(meta, set())
            turn = parsed["turns"][0]
            self.assertEqual(parsed["log_file"].replace("\\", "/"), "nested/rollout-test.jsonl")
            self.assertIsNone(turn["error"])
            self.assertEqual(turn["error_category"], "task-error")
            self.assertRegex(turn["error_sha256"], r"^[0-9a-f]{64}$")

            module.INCLUDE_BOUNDED_EXCERPTS = True
            included = module.parse_thread(meta, set())["turns"][0]
            self.assertEqual(len(included["error"]), 300)


if __name__ == "__main__":
    unittest.main()
