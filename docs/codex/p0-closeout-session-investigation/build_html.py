#!/usr/bin/env python3
"""Build the self-contained P0 closeout session investigation site."""

from __future__ import annotations

import argparse
from html.parser import HTMLParser
import json
import pathlib
from typing import Any


BASELINE = {
    "threads": 18,
    "turns": 45,
    "worker_seconds": 17_808,
    "active_wall_seconds": 17_388,
    "two_plus_seconds": 420,
    "two_plus_share": 2.4,
    "average_concurrency": 1.02,
    "maximum_concurrency": 2,
    "waits_per_turn": 6.0,
    "tokens": 165_400_000,
}

TASK_DESCRIPTIONS = {
    "P0-05": "Hosted prompt route and historical-local compatibility",
    "P0-06": "Model ledger, candidate selection, and cost baseline",
    "P0-13": "Bonus-context baseline",
    "P0-14": "Profile-driven context collection",
    "P0-15": "Context hygiene and immutable provenance",
    "P0-16": "Question-aware bounded bonus context",
    "P0-17": "Community and credential topology",
    "P0-18": "Reusable workflow support",
    "P0-19": "Eight concrete workflow triads",
    "P0-20": "Seed and non-dry development validation",
    "P0-21": "Production activation and natural schedule evidence",
    "P0-22": "Played-date history reconstruction",
    "P0-23": "Bounded GPT-5.6 candidate evidence",
    "P0-24": "Bonus reference-copy compatibility",
    "P0-25": "Roster enrichment and derived team totals",
    "P0-19/21 schadensfresse closeout": "Overlapping Schadensfresse readiness and live-onboarding subset",
}

KEY_INTERVENTION_ORDINALS = [7, 8, 9, 12, 13, 17, 18, 19, 21, 28, 29, 30, 31, 42, 43, 44, 45]


class ReportHtmlInspector(HTMLParser):
    """Collect structural invariants without adding a publication dependency."""

    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.duplicate_ids: set[str] = set()
        self.fragment_links: set[str] = set()
        self.external_assets: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        element_id = values.get("id")
        if element_id:
            if element_id in self.ids:
                self.duplicate_ids.add(element_id)
            self.ids.add(element_id)

        href = values.get("href")
        if tag == "a" and href and href.startswith("#"):
            self.fragment_links.add(href[1:])

        asset = values.get("src") if tag in {"script", "img", "iframe", "video", "audio"} else None
        if tag == "link" and "stylesheet" in (values.get("rel") or "").split():
            asset = href
        if asset:
            self.external_assets.append(asset)


def validate_output(output: str, payload: dict[str, Any]) -> None:
    if "__REPORT_DATA__" in output:
        raise ValueError("Report data placeholder was not replaced")

    inspector = ReportHtmlInspector()
    inspector.feed(output)
    missing_fragments = inspector.fragment_links - inspector.ids
    if inspector.duplicate_ids:
        raise ValueError(f"Duplicate HTML ids: {sorted(inspector.duplicate_ids)}")
    if missing_fragments:
        raise ValueError(f"Missing fragment targets: {sorted(missing_fragments)}")
    if inspector.external_assets:
        raise ValueError(f"Runtime asset dependencies violate self-containment: {inspector.external_assets}")
    if len(payload["agents"]) != payload["summary"]["threads"]:
        raise ValueError("Embedded agent records do not match the reported thread count")
    if len(payload["interventions"]) != payload["summary"]["user"]["including_initial"]:
        raise ValueError("Embedded intervention records do not match the reported message count")


def compact_usage(usage: dict[str, Any]) -> dict[str, int]:
    return {key: int(value) for key, value in usage.items()}


def build_payload(analysis: dict[str, Any], curated: dict[str, Any]) -> dict[str, Any]:
    summary = analysis["summary"]
    interventions = [
        {
            "ordinal": message["user_message_ordinal"],
            "kind": message["intervention_kind"],
            "timestamp": message["timestamp_local"],
            "excerpt": message["excerpt"],
            "key": message["user_message_ordinal"] in KEY_INTERVENTION_ORDINALS,
        }
        for message in analysis["user_messages"]
        if message["category"] == "user"
    ]
    task_groups = []
    for task in analysis["task_groups"]:
        task_groups.append({
            "group": task["task_group"],
            "description": TASK_DESCRIPTIONS.get(task["task_group"], task["task_group"]),
            "files": task["completed_task_files"],
            "start": task["observed_agent_start_local"],
            "ledger": task["ledger_completed_at_local"],
            "finish": task["observed_finish_local"],
            "elapsed": task["elapsed_seconds"],
            "worker": task["agent_worker_seconds"],
            "turns": task["agent_turns"],
            "threads": task["agent_threads"],
            "overlay": task["task_group"].startswith("P0-19/21"),
        })
    agents = [
        {
            "path": thread["agent_path"],
            "nickname": thread["nickname"],
            "group": thread["task_group"],
            "turns": thread["turn_count"],
            "completed": thread["completed_turns"],
            "aborted": thread["aborted_turns"],
            "seconds": thread["active_seconds"],
            "tokens": thread["usage"]["total_tokens"],
            "input": thread["usage"]["input_tokens"],
            "output": thread["usage"]["output_tokens"],
            "cost": thread["api_cost_equivalent_usd"],
        }
        for thread in analysis["threads"]
        if thread["kind"] == "subagent"
    ]
    agents.sort(key=lambda agent: agent["cost"] or 0, reverse=True)
    return {
        "meta": {
            "thread": summary["root_thread_id"],
            "start": summary["session_started_at_local"],
            "end": summary["session_last_event_at_local"],
            "baseCommit": "6d0fca3",
            "finalCommit": "2c824c8",
            "reportCommit": "092d854",
            "pricingAsOf": analysis["pricing"]["as_of"],
        },
        "summary": {
            "wallSeconds": summary["session_wall_span_seconds"],
            "rootTurns": summary["root_turns"],
            "rootSeconds": summary["root_active_seconds"],
            "rootMessages": summary["root_assistant_messages"],
            "compactions": summary["root_compactions"],
            "threads": summary["subagent_threads"],
            "turns": summary["subagent_turns"],
            "completedTurns": summary["subagent_completed_turns"],
            "abortedTurns": summary["subagent_aborted_turns"],
            "workerSeconds": summary["concurrency"]["aggregate_worker_seconds"],
            "commits": summary["session_commits"],
            "completedTasks": summary["task_files_completed_in_session"],
            "user": summary["user_messages"],
            "rootCalls": summary["root_function_calls"],
            "rootTools": summary["root_nested_tool_calls"],
            "rootUsage": compact_usage(summary["root_usage"]),
            "subagentUsage": compact_usage(summary["subagent_usage"]),
            "familyUsage": compact_usage(summary["usage"]),
            "rootCost": summary["root_api_cost_equivalent_usd"],
            "subagentCost": summary["subagent_api_cost_equivalent_usd"],
            "familyCost": summary["api_cost_equivalent_usd"],
            "guardianThreads": summary["guardian_threads"],
            "guardianTurns": summary["guardian_turns"],
            "guardianUsage": compact_usage(summary["guardian_usage"]),
            "concurrency": summary["concurrency"],
        },
        "baseline": BASELINE,
        "tasks": task_groups,
        "agents": agents,
        "interventions": interventions,
        "phases": curated["phases"],
        "findings": curated["autonomous_solutions"],
    }


HTML_TEMPLATE = r'''<!doctype html>
<html lang="en" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="Interactive investigation of the Codex session that closed KicktippAi Bundesliga 2026/27 P0.">
  <meta name="kicktippai-report-title" content="P0 closeout Codex session investigation">
  <title>P0 Closeout — Codex Session Investigation</title>
  <style>
    :root {
      color-scheme: dark;
      --bg: #0a0d11;
      --bg-elevated: #0f141b;
      --panel: #131a23;
      --panel-2: #18212c;
      --panel-3: #1d2834;
      --ink: #f3f0e8;
      --muted: #9aa6b5;
      --faint: #687585;
      --line: #293543;
      --line-strong: #3b4b5c;
      --amber: #ffb45c;
      --amber-soft: rgba(255, 180, 92, .13);
      --mint: #68d7c4;
      --mint-soft: rgba(104, 215, 196, .13);
      --coral: #ff7766;
      --coral-soft: rgba(255, 119, 102, .13);
      --violet: #a99cff;
      --violet-soft: rgba(169, 156, 255, .13);
      --blue: #70a7ff;
      --shadow: 0 24px 70px rgba(0, 0, 0, .28);
      --radius: 22px;
      --mono: "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      --sans: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    html[data-theme="light"] {
      color-scheme: light;
      --bg: #ece9e1;
      --bg-elevated: #f5f2eb;
      --panel: #fffdf8;
      --panel-2: #f6f1e8;
      --panel-3: #ede6da;
      --ink: #161a1f;
      --muted: #596473;
      --faint: #7a8490;
      --line: #d8d2c8;
      --line-strong: #bcb4a8;
      --amber: #a9580f;
      --amber-soft: rgba(169, 88, 15, .10);
      --mint: #087c6b;
      --mint-soft: rgba(8, 124, 107, .10);
      --coral: #c64132;
      --coral-soft: rgba(198, 65, 50, .10);
      --violet: #6657ce;
      --violet-soft: rgba(102, 87, 206, .10);
      --blue: #2869c7;
      --shadow: 0 24px 70px rgba(61, 50, 36, .12);
    }

    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body {
      margin: 0;
      background:
        radial-gradient(circle at 82% -10%, rgba(112, 167, 255, .11), transparent 32rem),
        radial-gradient(circle at 12% 10%, rgba(255, 180, 92, .09), transparent 28rem),
        var(--bg);
      color: var(--ink);
      font-family: var(--sans);
      line-height: 1.55;
    }
    a { color: inherit; }
    button, input, select { font: inherit; }
    button, select { cursor: pointer; }
    :focus-visible { outline: 3px solid var(--blue); outline-offset: 3px; }

    .topbar {
      position: sticky;
      inset-block-start: 0;
      z-index: 30;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 20px;
      min-height: 64px;
      padding: 10px clamp(16px, 3vw, 40px);
      border-bottom: 1px solid var(--line);
      background: color-mix(in srgb, var(--bg) 86%, transparent);
      backdrop-filter: blur(18px);
    }
    .brand { display: flex; align-items: center; gap: 12px; min-width: max-content; }
    .brand-mark {
      display: grid; place-items: center; width: 34px; height: 34px;
      border: 1px solid color-mix(in srgb, var(--amber) 55%, var(--line));
      border-radius: 10px; background: var(--amber-soft); color: var(--amber);
      font: 800 12px/1 var(--mono); letter-spacing: -.08em;
    }
    .brand-copy { display: grid; line-height: 1.1; }
    .brand-copy strong { font-size: .88rem; letter-spacing: .02em; }
    .brand-copy span { color: var(--muted); font: 600 .68rem/1.4 var(--mono); text-transform: uppercase; letter-spacing: .12em; }
    .nav { display: flex; gap: 4px; overflow-x: auto; scrollbar-width: none; }
    .nav a {
      border-radius: 999px; padding: 7px 10px; color: var(--muted);
      font-size: .78rem; font-weight: 700; text-decoration: none; white-space: nowrap;
    }
    .nav a:hover, .nav a.active { color: var(--ink); background: var(--panel-2); }
    .top-actions { display: flex; align-items: center; gap: 8px; }
    .icon-button {
      display: grid; place-items: center; width: 38px; height: 38px;
      border: 1px solid var(--line); border-radius: 12px;
      background: var(--panel); color: var(--ink);
    }
    .icon-button:hover { border-color: var(--line-strong); background: var(--panel-2); }

    main { width: min(1240px, calc(100% - 32px)); margin: 0 auto; padding: 42px 0 72px; }
    section { scroll-margin-top: 86px; margin-block: 0 72px; }
    .eyebrow {
      display: inline-flex; align-items: center; gap: 8px; margin: 0 0 12px;
      color: var(--amber); font: 750 .74rem/1 var(--mono); letter-spacing: .13em; text-transform: uppercase;
    }
    .eyebrow::before { content: ""; width: 22px; height: 2px; border-radius: 2px; background: currentColor; }
    h1, h2, h3, p { margin-top: 0; }
    h1 { margin-bottom: 18px; max-width: 900px; font-size: clamp(3rem, 7.5vw, 6.6rem); line-height: .91; letter-spacing: -.067em; }
    h1 em { color: var(--amber); font-style: normal; }
    h2 { margin-bottom: 12px; font-size: clamp(1.9rem, 4vw, 3.4rem); line-height: 1; letter-spacing: -.045em; }
    h3 { font-size: 1rem; letter-spacing: -.01em; }
    .section-head { display: grid; grid-template-columns: minmax(0, 1fr) minmax(280px, 470px); gap: 30px; align-items: end; margin-bottom: 24px; }
    .section-head p { margin: 0; color: var(--muted); }

    .hero { position: relative; min-height: 610px; display: grid; align-content: center; padding: 48px 0 36px; overflow: hidden; }
    .hero::after {
      content: ""; position: absolute; z-index: -1; width: min(47vw, 620px); aspect-ratio: 1;
      inset-inline-end: -3%; inset-block-start: 20px; border: 1px solid var(--line); border-radius: 50%;
      box-shadow: inset 0 0 0 70px color-mix(in srgb, var(--panel) 45%, transparent), inset 0 0 0 71px var(--line), inset 0 0 0 150px color-mix(in srgb, var(--bg) 72%, transparent), inset 0 0 0 151px var(--line);
      opacity: .75;
    }
    .hero-lede { max-width: 690px; margin-bottom: 28px; color: var(--muted); font-size: clamp(1rem, 1.8vw, 1.22rem); }
    .hero-meta { display: flex; flex-wrap: wrap; gap: 10px; }
    .chip { display: inline-flex; align-items: center; gap: 8px; border: 1px solid var(--line); border-radius: 999px; padding: 7px 11px; background: var(--panel); color: var(--muted); font: 650 .74rem/1 var(--mono); }
    .chip strong { color: var(--ink); }
    .status-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--mint); box-shadow: 0 0 0 5px var(--mint-soft); }
    .hero-orbit {
      position: absolute; inset-inline-end: 4.5%; inset-block-start: 165px; width: min(36vw, 440px); aspect-ratio: 1;
      display: grid; place-items: center; pointer-events: none;
    }
    .orbit-core { text-align: center; }
    .orbit-core strong { display: block; font-size: clamp(3rem, 7vw, 5.4rem); line-height: .9; letter-spacing: -.06em; }
    .orbit-core span { color: var(--muted); font: 700 .72rem/1.4 var(--mono); letter-spacing: .12em; text-transform: uppercase; }
    .orbit-note { position: absolute; padding: 7px 10px; border: 1px solid var(--line); border-radius: 9px; background: var(--bg-elevated); color: var(--muted); font: 650 .69rem/1 var(--mono); }
    .orbit-note.one { inset-block-start: 9%; inset-inline-start: 8%; }
    .orbit-note.two { inset-block-end: 14%; inset-inline-end: 0; }
    .orbit-note.three { inset-block-end: 2%; inset-inline-start: 13%; }

    .kpi-grid { display: grid; grid-template-columns: repeat(6, 1fr); gap: 12px; margin-top: -18px; position: relative; z-index: 2; }
    .kpi { min-height: 150px; padding: 18px; border: 1px solid var(--line); border-radius: 18px; background: color-mix(in srgb, var(--panel) 93%, transparent); box-shadow: var(--shadow); }
    .kpi:nth-child(1), .kpi:nth-child(2) { grid-column: span 2; }
    .kpi:nth-child(n+3) { grid-column: span 1; }
    .kpi-label { display: block; min-height: 34px; color: var(--muted); font-size: .73rem; font-weight: 700; text-transform: uppercase; letter-spacing: .08em; }
    .kpi strong { display: block; margin-top: 16px; font-size: clamp(1.55rem, 3vw, 2.6rem); line-height: 1; letter-spacing: -.05em; }
    .kpi small { display: block; margin-top: 8px; color: var(--faint); font-size: .72rem; }
    .kpi.accent { border-color: color-mix(in srgb, var(--amber) 45%, var(--line)); background: linear-gradient(145deg, var(--amber-soft), var(--panel)); }

    .verdict { display: grid; grid-template-columns: 1.15fr .85fr; gap: 14px; margin-top: 14px; }
    .verdict-card { padding: 24px; border: 1px solid var(--line); border-radius: var(--radius); background: var(--panel); }
    .verdict-card p { margin: 0; color: var(--muted); }
    .verdict-card strong.big { display: block; margin-bottom: 10px; color: var(--mint); font-size: 1.55rem; letter-spacing: -.03em; }
    .verdict-card.caution strong.big { color: var(--amber); }

    .panel { border: 1px solid var(--line); border-radius: var(--radius); background: var(--panel); box-shadow: var(--shadow); }
    .phase-strip { display: grid; grid-template-columns: repeat(5, 1fr); overflow: hidden; margin-bottom: 16px; }
    .phase { position: relative; min-height: 166px; padding: 20px; border-inline-end: 1px solid var(--line); background: var(--panel); }
    .phase:last-child { border-inline-end: 0; }
    .phase::after { content: ""; position: absolute; inset: auto 0 0; height: 4px; background: var(--phase-color, var(--amber)); }
    .phase time { color: var(--faint); font: 650 .69rem/1 var(--mono); text-transform: uppercase; }
    .phase strong { display: block; margin: 14px 0 8px; font-size: .97rem; }
    .phase p { margin: 0; color: var(--muted); font-size: .78rem; line-height: 1.45; }

    .timeline-panel { overflow: hidden; }
    .timeline-head { display: grid; grid-template-columns: 92px minmax(680px, 1fr) 90px; gap: 14px; padding: 15px 18px; border-bottom: 1px solid var(--line); color: var(--faint); font: 650 .67rem/1 var(--mono); text-transform: uppercase; letter-spacing: .08em; }
    .timeline-scroll { overflow-x: auto; }
    .timeline-inner { min-width: 930px; }
    .timeline-axis { display: grid; grid-template-columns: 92px minmax(680px, 1fr) 90px; gap: 14px; padding: 12px 18px 4px; }
    .axis-track { display: flex; justify-content: space-between; color: var(--faint); font: 600 .64rem/1 var(--mono); }
    .task-row { display: grid; grid-template-columns: 92px minmax(680px, 1fr) 90px; gap: 14px; align-items: center; min-height: 52px; padding: 6px 18px; border-top: 1px solid color-mix(in srgb, var(--line) 65%, transparent); }
    .task-row:hover { background: var(--panel-2); }
    .task-label { display: grid; }
    .task-label strong { font: 750 .78rem/1.2 var(--mono); }
    .task-label small { color: var(--faint); font-size: .64rem; }
    .task-track { position: relative; height: 22px; border-radius: 7px; background: linear-gradient(90deg, transparent 0 19.8%, var(--line) 20%, transparent 20.2% 39.8%, var(--line) 40%, transparent 40.2% 59.8%, var(--line) 60%, transparent 60.2% 79.8%, var(--line) 80%, transparent 80.2%); }
    .task-bar { position: absolute; inset-block: 4px; min-width: 5px; border-radius: 999px; background: linear-gradient(90deg, var(--amber), color-mix(in srgb, var(--amber) 35%, var(--mint))); box-shadow: 0 0 0 1px color-mix(in srgb, var(--amber) 60%, transparent); }
    .task-row.overlay .task-bar { background: repeating-linear-gradient(135deg, var(--violet) 0 6px, color-mix(in srgb, var(--violet) 60%, var(--panel)) 6px 11px); }
    .ledger-dot { position: absolute; inset-block-start: 50%; width: 9px; height: 9px; border: 2px solid var(--panel); border-radius: 50%; background: var(--mint); transform: translate(-50%, -50%); box-shadow: 0 0 0 2px var(--mint); }
    .task-stat { text-align: end; }
    .task-stat strong { display: block; font-size: .78rem; }
    .task-stat small { color: var(--faint); font-size: .64rem; }
    .timeline-note { padding: 14px 18px; border-top: 1px solid var(--line); color: var(--muted); font-size: .75rem; }

    .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .card { padding: 22px; }
    .card-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
    .card-head h3 { margin: 0; }
    .card-head p { margin: 4px 0 0; color: var(--muted); font-size: .78rem; }
    .badge { border: 1px solid var(--line); border-radius: 999px; padding: 5px 8px; color: var(--muted); font: 650 .66rem/1 var(--mono); white-space: nowrap; }
    .stack { display: flex; height: 54px; overflow: hidden; border: 1px solid var(--line); border-radius: 13px; background: var(--panel-2); }
    .stack-part { display: grid; place-items: center; min-width: 44px; color: #091013; font: 800 .72rem/1 var(--mono); }
    .stack-part.one { background: var(--amber); }
    .stack-part.two { background: var(--mint); }
    .stack-part.three { background: var(--violet); }
    .legend { display: flex; flex-wrap: wrap; gap: 13px; margin-top: 14px; color: var(--muted); font-size: .72rem; }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .swatch { width: 9px; height: 9px; border-radius: 3px; background: var(--swatch); }
    .comparison { display: grid; gap: 14px; }
    .compare-row { display: grid; grid-template-columns: 128px 1fr 64px; gap: 10px; align-items: center; }
    .compare-row span { color: var(--muted); font-size: .72rem; }
    .bar-pair { display: grid; gap: 4px; }
    .mini-bar { height: 8px; border-radius: 999px; background: var(--panel-3); overflow: hidden; }
    .mini-bar i { display: block; height: 100%; border-radius: inherit; background: var(--faint); }
    .mini-bar.current i { background: var(--mint); }
    .compare-row strong { text-align: end; font: 700 .72rem/1 var(--mono); }
    .call-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .call { padding: 12px; border: 1px solid var(--line); border-radius: 12px; background: var(--panel-2); }
    .call strong { display: block; font: 760 1.3rem/1 var(--mono); letter-spacing: -.04em; }
    .call span { display: block; margin-top: 7px; color: var(--faint); font-size: .66rem; }
    .model-card { display: grid; grid-template-columns: auto 1fr; gap: 16px; align-items: center; margin-top: 16px; padding: 18px; border: 1px solid var(--line); border-radius: 15px; background: var(--panel-2); }
    .model-glyph { display: grid; place-items: center; width: 54px; height: 54px; border-radius: 16px; background: var(--violet-soft); color: var(--violet); font: 800 1.1rem/1 var(--mono); }
    .model-card strong { display: block; }
    .model-card p { margin: 4px 0 0; color: var(--muted); font-size: .76rem; }

    .agent-panel { margin-top: 16px; overflow: hidden; }
    .agent-toolbar { display: grid; grid-template-columns: minmax(220px, 1fr) 190px 180px auto; gap: 10px; padding: 16px; border-bottom: 1px solid var(--line); }
    .control { width: 100%; min-height: 40px; border: 1px solid var(--line); border-radius: 11px; background: var(--panel-2); color: var(--ink); padding: 8px 11px; }
    .control::placeholder { color: var(--faint); }
    .agent-summary { align-self: center; color: var(--muted); font: 650 .7rem/1 var(--mono); text-align: end; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 12px 16px; border-bottom: 1px solid var(--line); text-align: start; }
    th { color: var(--faint); font: 700 .66rem/1 var(--mono); text-transform: uppercase; letter-spacing: .07em; }
    td { font-size: .78rem; }
    td.mono { font-family: var(--mono); font-size: .72rem; }
    .agent-name { display: grid; gap: 3px; }
    .agent-name small { color: var(--faint); font: 600 .62rem/1 var(--sans); }
    td.num, th.num { text-align: end; }
    tbody tr:hover { background: var(--panel-2); }
    .group-tag { display: inline-block; border-radius: 999px; padding: 4px 7px; background: var(--amber-soft); color: var(--amber); font: 700 .64rem/1 var(--mono); }
    .show-more { display: block; width: calc(100% - 32px); margin: 14px 16px 16px; border: 1px solid var(--line); border-radius: 11px; padding: 10px; background: var(--panel-2); color: var(--ink); font-weight: 700; }

    .cost-layout { display: grid; grid-template-columns: 330px 1fr; gap: 24px; align-items: center; }
    .donut-wrap { position: relative; width: min(100%, 290px); aspect-ratio: 1; margin: auto; }
    .donut { width: 100%; height: 100%; transform: rotate(-90deg); }
    .donut circle { fill: none; stroke-width: 18; }
    .donut .track { stroke: var(--panel-3); }
    .donut .root { stroke: var(--amber); }
    .donut .agents { stroke: var(--mint); }
    .donut-center { position: absolute; inset: 0; display: grid; place-content: center; text-align: center; }
    .donut-center strong { font-size: 2.25rem; letter-spacing: -.06em; }
    .donut-center span { max-width: 110px; color: var(--muted); font: 650 .68rem/1.4 var(--mono); text-transform: uppercase; }
    .cost-lines { display: grid; gap: 12px; }
    .cost-line { display: grid; grid-template-columns: 12px 1fr auto; gap: 10px; align-items: center; padding-bottom: 12px; border-bottom: 1px solid var(--line); }
    .cost-line i { width: 10px; height: 10px; border-radius: 3px; background: var(--color); }
    .cost-line span { color: var(--muted); font-size: .78rem; }
    .cost-line strong { font: 750 .88rem/1 var(--mono); }
    .guardian-note { margin-top: 16px; padding: 14px; border: 1px dashed var(--line-strong); border-radius: 13px; color: var(--muted); font-size: .76rem; }
    .token-strip { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 10px; margin-top: 18px; }
    .token-stat { padding: 14px; border-radius: 13px; background: var(--panel-2); }
    .token-stat strong { display: block; font: 750 1.05rem/1 var(--mono); }
    .token-stat span { display: block; margin-top: 7px; color: var(--faint); font-size: .66rem; }

    .intervention-layout { display: grid; grid-template-columns: 320px 1fr; gap: 16px; }
    .category-buttons { display: grid; gap: 8px; }
    .category-button { display: grid; grid-template-columns: 1fr auto; gap: 12px; align-items: center; width: 100%; padding: 14px; border: 1px solid var(--line); border-radius: 13px; background: var(--panel-2); color: var(--ink); text-align: start; }
    .category-button[aria-pressed="true"] { border-color: var(--category); background: color-mix(in srgb, var(--category) 12%, var(--panel-2)); }
    .category-button span { font-size: .76rem; font-weight: 700; }
    .category-button strong { font: 750 1.05rem/1 var(--mono); color: var(--category); }
    .burst { margin-top: 14px; padding: 16px; border-radius: 13px; background: var(--amber-soft); color: var(--muted); font-size: .78rem; }
    .burst strong { color: var(--amber); }
    .message-list { display: grid; gap: 8px; max-height: 480px; overflow-y: auto; padding-right: 4px; }
    .message { display: grid; grid-template-columns: 56px 1fr; gap: 12px; padding: 13px; border: 1px solid var(--line); border-radius: 13px; background: var(--panel-2); }
    .message.key { border-inline-start: 3px solid var(--amber); }
    .message time { color: var(--faint); font: 650 .63rem/1.35 var(--mono); }
    .message p { margin: 0; color: var(--muted); font-size: .75rem; }
    .message .message-kind { display: block; margin-bottom: 5px; color: var(--ink); font-size: .64rem; font-weight: 800; text-transform: uppercase; letter-spacing: .06em; }
    .message-empty { padding: 30px; color: var(--muted); text-align: center; }

    .findings { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }
    .finding { border: 1px solid var(--line); border-radius: 16px; background: var(--panel); overflow: hidden; }
    .finding[open] { border-color: var(--line-strong); }
    .finding summary { list-style: none; display: grid; grid-template-columns: auto 1fr auto; gap: 12px; align-items: center; padding: 17px; cursor: pointer; }
    .finding summary::-webkit-details-marker { display: none; }
    .finding-index { color: var(--amber); font: 800 .73rem/1 var(--mono); }
    .finding-title { font-size: .86rem; font-weight: 750; }
    .finding-task { border-radius: 999px; padding: 4px 7px; background: var(--mint-soft); color: var(--mint); font: 700 .62rem/1 var(--mono); }
    .finding-body { padding: 0 17px 17px 51px; }
    .finding-body dl { margin: 0; }
    .finding-body dt { margin-top: 10px; color: var(--faint); font: 700 .64rem/1 var(--mono); text-transform: uppercase; letter-spacing: .07em; }
    .finding-body dd { margin: 5px 0 0; color: var(--muted); font-size: .76rem; }
    .commit-list { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 12px; }
    .commit-list a { border: 1px solid var(--line); border-radius: 7px; padding: 4px 6px; color: var(--blue); font: 650 .63rem/1 var(--mono); text-decoration: none; }

    .method-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
    .method-card { padding: 18px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel); }
    .method-card strong { display: block; margin-bottom: 8px; color: var(--amber); font: 750 .76rem/1 var(--mono); }
    .method-card p { margin: 0; color: var(--muted); font-size: .76rem; }
    .source-box { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; margin-top: 14px; padding: 18px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel-2); }
    .source-box code { color: var(--mint); font: 650 .7rem/1.5 var(--mono); overflow-wrap: anywhere; }
    .source-links { display: flex; flex-wrap: wrap; gap: 8px; }
    .source-links a { border: 1px solid var(--line); border-radius: 10px; padding: 8px 10px; color: var(--ink); font-size: .72rem; font-weight: 700; text-decoration: none; }

    footer { display: flex; justify-content: space-between; gap: 20px; padding: 24px 0 0; border-top: 1px solid var(--line); color: var(--faint); font-size: .7rem; }

    @media (max-width: 1050px) {
      .nav { display: none; }
      .kpi-grid { grid-template-columns: repeat(3, 1fr); }
      .kpi, .kpi:nth-child(1), .kpi:nth-child(2), .kpi:nth-child(n+3) { grid-column: span 1; }
      .phase-strip { grid-template-columns: 1fr 1fr; }
      .phase:last-child { grid-column: span 2; }
      .cost-layout { grid-template-columns: 280px 1fr; }
    }
    @media (max-width: 780px) {
      main { width: min(100% - 22px, 1240px); padding-top: 24px; }
      section { margin-bottom: 54px; }
      .brand-copy span { display: none; }
      .topbar { min-height: 56px; padding-inline: 12px; }
      .hero { min-height: auto; padding: 52px 0 30px; }
      .hero::after, .hero-orbit { display: none; }
      h1 { font-size: clamp(3.3rem, 17vw, 5.2rem); }
      .section-head, .two-col, .verdict, .cost-layout, .intervention-layout { grid-template-columns: 1fr; }
      .section-head { gap: 8px; }
      .kpi-grid { grid-template-columns: 1fr 1fr; margin-top: 0; }
      .phase-strip { grid-template-columns: 1fr; }
      .phase, .phase:last-child { grid-column: auto; min-height: auto; border-inline-end: 0; border-bottom: 1px solid var(--line); }
      .agent-toolbar { grid-template-columns: 1fr 1fr; }
      .agent-toolbar input { grid-column: span 2; }
      .agent-summary { text-align: start; }
      .findings, .method-grid { grid-template-columns: 1fr; }
      .cost-layout { gap: 4px; }
    }
    @media (max-width: 500px) {
      .kpi-grid, .call-grid, .token-strip { grid-template-columns: 1fr; }
      .kpi { min-height: 118px; }
      .agent-toolbar { grid-template-columns: 1fr; }
      .agent-toolbar input { grid-column: auto; }
      th:nth-child(2), td:nth-child(2), th:nth-child(4), td:nth-child(4) { display: none; }
      th, td { padding: 11px 10px; }
      .finding summary { grid-template-columns: auto 1fr; }
      .finding-task { grid-column: 2; justify-self: start; }
      .finding-body { padding-left: 17px; }
      footer { display: grid; }
    }
    @media (prefers-reduced-motion: reduce) { html { scroll-behavior: auto; } * { transition: none !important; } }
    @media print {
      :root { color-scheme: light; --bg: #fff; --panel: #fff; --panel-2: #f5f5f5; --ink: #111; --muted: #444; --line: #ccc; }
      .topbar, .agent-toolbar, .show-more { display: none !important; }
      body { background: #fff; }
      main { width: 100%; padding: 0; }
      section { break-inside: avoid; margin-bottom: 34px; }
      .panel, .kpi { box-shadow: none; }
    }
  </style>
</head>
<body>
  <header class="topbar">
    <div class="brand">
      <div class="brand-mark" aria-hidden="true">KA</div>
      <div class="brand-copy"><strong>P0 Closeout</strong><span>Session dossier · Aug 2026</span></div>
    </div>
    <nav class="nav" aria-label="Report sections">
      <a href="#overview">Overview</a><a href="#timeline">Timeline</a><a href="#orchestration">Orchestration</a><a href="#cost">Cost</a><a href="#interventions">Interventions</a><a href="#discoveries">Discoveries</a><a href="#method">Method</a>
    </nav>
    <div class="top-actions">
      <button class="icon-button" id="theme-toggle" type="button" aria-label="Switch to light theme" title="Toggle theme"><span aria-hidden="true">◐</span></button>
    </div>
  </header>

  <main>
    <section class="hero" id="overview">
      <p class="eyebrow">Investigation 01 · Deadline closeout</p>
      <h1>Six days.<br><em>One P0.</em><br>102 agents.</h1>
      <p class="hero-lede">A forensic view of the Codex session that took Bundesliga 2026/27 from a partially open P0 graph to a green natural production run—what it delivered, what it cost, and where autonomy actually paid off.</p>
      <div class="hero-meta">
        <span class="chip"><span class="status-dot"></span><strong>P0 closed</strong></span>
        <span class="chip">Aug 21–28, 2026 · CEST</span>
        <span class="chip"><strong>6d0fca3</strong> → <strong>2c824c8</strong></span>
      </div>
      <div class="hero-orbit" aria-hidden="true">
        <div class="orbit-core"><strong>64h</strong><span>root-turn<br>duration</span></div>
        <span class="orbit-note one">124 commits</span><span class="orbit-note two">16-job run</span><span class="orbit-note three">32 compactions</span>
      </div>
    </section>

    <div class="kpi-grid" aria-label="Headline metrics">
      <article class="kpi accent"><span class="kpi-label">API list-price equivalent</span><strong id="kpi-cost">—</strong><small>Root + real task agents; not an invoice</small></article>
      <article class="kpi"><span class="kpi-label">Logged task-family tokens</span><strong id="kpi-tokens">—</strong><small>97.7% of input was cached</small></article>
      <article class="kpi"><span class="kpi-label">User interventions</span><strong id="kpi-interventions">—</strong><small>After the kickoff</small></article>
      <article class="kpi"><span class="kpi-label">Agent turns</span><strong id="kpi-turns">—</strong><small>402 complete · 12 aborted</small></article>
      <article class="kpi"><span class="kpi-label">Tasks completed</span><strong id="kpi-tasks">—</strong><small>22 in-session · 32 final concrete</small></article>
      <article class="kpi"><span class="kpi-label">Concurrent overlap</span><strong id="kpi-overlap">—</strong><small>Share of subagent-active wall time at 2+</small></article>
    </div>

    <div class="verdict">
      <article class="verdict-card"><strong class="big">Delivery: exceptional</strong><p>The deadline outcome is conclusive: all concrete P0 records complete, exact-head CI green, the intentionally gated production topology active, and the first natural 16-job run audited without writes or model spend.</p></article>
      <article class="verdict-card caution"><strong class="big">Efficiency: unresolved</strong><p>Every task ran on Sol/xhigh. Parallelism improved dramatically, but 3,527 waits, 102 realized threads, and nearly one billion root tokens left a large control-plane bill.</p></article>
    </div>

    <section id="timeline">
      <div class="section-head"><div><p class="eyebrow">01 · Sequence</p><h2>The closeout unfolded in five waves.</h2></div><p>Ledger transitions give the durable completion point. Later task-attributed reviews and repairs extend the evidence window—sometimes by days.</p></div>
      <div class="phase-strip panel" id="phase-strip"></div>
      <div class="timeline-panel panel">
        <div class="timeline-head"><span>Task</span><span>Aug 21 → Aug 28</span><span>Worker time</span></div>
        <div class="timeline-scroll"><div class="timeline-inner"><div class="timeline-axis"><span></span><div class="axis-track" id="timeline-axis"></div><span></span></div><div id="task-timeline"></div></div></div>
        <div class="timeline-note">Bars show first attributed child work through last evidence. The dot marks the Git ledger’s final transition to <code>Complete</code>. The striped Schadensfresse row overlaps P0-19/P0-21 and is not additive.</div>
      </div>
    </section>

    <section id="orchestration">
      <div class="section-head"><div><p class="eyebrow">02 · Control plane</p><h2>Parallelism worked. Coordination ballooned.</h2></div><p>The accepted worktree design created meaningful overlap. The orchestrator then spent heavily on polling, messaging, and strongest-tier reasoning.</p></div>
      <div class="two-col">
        <article class="panel card"><div class="card-head"><div><h3>Concurrency while agents were active</h3><p>57h29m with at least one descendant working</p></div><span class="badge">average 1.51</span></div><div class="stack" id="concurrency-stack" aria-label="Concurrency distribution"></div><div class="legend"><span><i class="swatch" style="--swatch:var(--amber)"></i>one agent</span><span><i class="swatch" style="--swatch:var(--mint)"></i>two agents</span><span><i class="swatch" style="--swatch:var(--violet)"></i>three agents</span></div></article>
        <article class="panel card"><div class="card-head"><div><h3>Versus the earlier baseline</h3><p>Directional comparison; task scope differs</p></div><span class="badge">max 3 vs 2</span></div><div class="comparison" id="baseline-comparison"></div></article>
        <article class="panel card"><div class="card-head"><div><h3>Root collaboration traffic</h3><p>39 root turns · 1,600 assistant-message records</p></div><span class="badge">32 compactions</span></div><div class="call-grid" id="call-grid"></div></article>
        <article class="panel card"><div class="card-head"><div><h3>Agent model allocation</h3><p>Application experiment models are a separate domain</p></div><span class="badge">no tiering</span></div><div class="model-card"><div class="model-glyph">S/x</div><div><strong>gpt-5.6-sol · xhigh</strong><p>Root plus all 102 realized task-agent threads. The auto-review guardian used an internal <code>codex-auto-review/low</code> model.</p></div></div></article>
      </div>

      <div class="agent-panel panel">
        <div class="agent-toolbar">
          <input class="control" id="agent-search" type="search" placeholder="Search agent path…" aria-label="Search agent paths">
          <select class="control" id="agent-group" aria-label="Filter agents by task group"><option value="all">All task groups</option></select>
          <select class="control" id="agent-sort" aria-label="Sort agents"><option value="cost">Highest equivalent cost</option><option value="tokens">Most tokens</option><option value="seconds">Longest active time</option><option value="turns">Most turns</option></select>
          <span class="agent-summary" id="agent-summary"></span>
        </div>
        <div class="table-wrap"><table><thead><tr><th>Agent path</th><th>Group</th><th class="num">Turns</th><th class="num">Active</th><th class="num">Tokens</th><th class="num">API equiv.</th></tr></thead><tbody id="agent-body"></tbody></table></div>
        <button class="show-more" id="agent-more" type="button">Show all matching agents</button>
      </div>
    </section>

    <section id="cost">
      <div class="section-head"><div><p class="eyebrow">03 · Usage economics</p><h2>$1,582 to run the task family—at API rates.</h2></div><p>That number is an equivalence calculation using official OpenAI list prices, not a statement about the user’s Codex subscription charge.</p></div>
      <div class="panel card cost-layout">
        <div class="donut-wrap"><svg class="donut" viewBox="0 0 120 120" role="img" aria-label="Equivalent cost split between root and task agents"><circle class="track" cx="60" cy="60" r="45"></circle><circle class="root" id="cost-root-ring" cx="60" cy="60" r="45" pathLength="100"></circle><circle class="agents" id="cost-agent-ring" cx="60" cy="60" r="45" pathLength="100"></circle></svg><div class="donut-center"><strong id="cost-center">—</strong><span>public API equivalent</span></div></div>
        <div><div class="cost-lines" id="cost-lines"></div><div class="guardian-note" id="guardian-note"></div><div class="token-strip" id="token-strip"></div></div>
      </div>
    </section>

    <section id="interventions">
      <div class="section-head"><div><p class="eyebrow">04 · Human steering</p><h2>44 interventions, but not 44 rescues.</h2></div><p>Most messages supplied authorization, external state, budget, or stop/go decisions. Sixteen corrected scope or intent; four asked about status or process.</p></div>
      <div class="intervention-layout">
        <aside class="panel card"><div class="card-head"><div><h3>Filter the record</h3><p>Hand-annotated message purpose</p></div><span class="badge">45 incl. kickoff</span></div><div class="category-buttons" id="category-buttons"></div><div class="burst"><strong>25 interaction bursts</strong><br>Rapid follow-ups within ten minutes count as one return to the session.</div></aside>
        <div class="panel card"><div class="card-head"><div><h3>Message timeline</h3><p>Key corrections have an amber edge</p></div><span class="badge" id="message-count"></span></div><div class="message-list" id="message-list"></div></div>
      </div>
    </section>

    <section id="discoveries">
      <div class="section-head"><div><p class="eyebrow">05 · Autonomous repair</p><h2>The best autonomy happened inside bounded loops.</h2></div><p>Agents found concrete failures in tests, traces, live workflows, or independent review—then carried them through repair and durable evidence.</p></div>
      <div class="findings" id="findings"></div>
    </section>

    <section id="method">
      <div class="section-head"><div><p class="eyebrow">06 · Evidence contract</p><h2>Forensic, reproducible, deliberately bounded.</h2></div><p>The page embeds only normalized, publication-oriented data. Raw transcripts, complete prompts, reasoning, secrets, and full tool output stay local.</p></div>
      <div class="method-grid">
        <article class="method-card"><strong>Family boundary</strong><p>Only recursive <code>thread_spawn.parent_thread_id</code> descendants count as task agents. Textual thread mentions do not.</p></article>
        <article class="method-card"><strong>Time boundary</strong><p>Turn duration includes tool execution and waits. Task windows overlap and are evidence envelopes, not timesheets.</p></article>
        <article class="method-card"><strong>Price boundary</strong><p>Normal Sol rates apply because the largest logged input was 237,299 tokens, below the 272K long-context threshold.</p></article>
      </div>
      <div class="source-box"><code id="source-line"></code><div class="source-links"><a href="https://github.com/ehonda/KicktippAi/tree/main/docs/codex/p0-closeout-session-investigation">Source &amp; normalized data</a><a href="https://developers.openai.com/api/docs/models/gpt-5.6-sol">Official Sol pricing</a><a href="https://github.com/ehonda/KicktippAi/actions/runs/33143114280">Natural 16-job run</a></div></div>
    </section>

    <footer><span>KicktippAi · P0 closeout session investigation</span><span>Generated from schema v2 normalized evidence · 2026-08-29</span></footer>
  </main>

  <script id="report-data" type="application/json">__REPORT_DATA__</script>
  <script>
    (() => {
      "use strict";
      const data = JSON.parse(document.getElementById("report-data").textContent);
      const $ = (selector) => document.querySelector(selector);
      const esc = (value) => String(value ?? "").replace(/[&<>"']/g, char => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[char]));
      const number = new Intl.NumberFormat("en-US");
      const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", minimumFractionDigits: 2, maximumFractionDigits: 2 });
      const compact = new Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 2 });
      const fmtDuration = seconds => {
        const totalMinutes = Math.round(Number(seconds) / 60);
        const days = Math.floor(totalMinutes / 1440);
        const hours = Math.floor((totalMinutes % 1440) / 60);
        const minutes = totalMinutes % 60;
        if (days) return `${days}d ${hours}h ${minutes}m`;
        if (hours) return `${hours}h ${minutes}m`;
        return `${minutes}m`;
      };
      const fmtDate = value => value ? new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Europe/Berlin" }).format(new Date(value)) : "—";
      const categoryMeta = {
        "kickoff": { label: "Kickoff", color: "var(--mint)" },
        "authorization-or-external-unblock": { label: "Authorization / external control", color: "var(--amber)" },
        "scope-correction-or-clarification": { label: "Scope correction / clarification", color: "var(--coral)" },
        "status-or-process-question": { label: "Status / process", color: "var(--violet)" },
      };

      const initTheme = () => {
        let saved = null;
        try { saved = localStorage.getItem("p0-report-theme"); } catch (_) {}
        if (saved === "light" || saved === "dark") document.documentElement.dataset.theme = saved;
        const button = $("#theme-toggle");
        const sync = () => button.setAttribute("aria-label", `Switch to ${document.documentElement.dataset.theme === "dark" ? "light" : "dark"} theme`);
        sync();
        button.addEventListener("click", () => {
          document.documentElement.dataset.theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
          try { localStorage.setItem("p0-report-theme", document.documentElement.dataset.theme); } catch (_) {}
          sync();
        });
      };

      const renderHeadline = () => {
        $("#kpi-cost").textContent = money.format(data.summary.familyCost);
        $("#kpi-tokens").textContent = `${(data.summary.familyUsage.total_tokens / 1e9).toFixed(3)}B`;
        $("#kpi-interventions").textContent = number.format(data.summary.user.interventions_after_initial);
        $("#kpi-turns").textContent = number.format(data.summary.turns);
        $("#kpi-tasks").textContent = number.format(data.summary.completedTasks);
        const c = data.summary.concurrency;
        $("#kpi-overlap").textContent = `${(100 * c.wall_seconds_with_two_or_more_subagents / c.wall_seconds_with_any_subagent).toFixed(1)}%`;
      };

      const renderPhases = () => {
        const colors = ["var(--amber)", "var(--mint)", "var(--violet)", "var(--coral)", "var(--blue)"];
        $("#phase-strip").innerHTML = data.phases.map((phase, index) => `<article class="phase" style="--phase-color:${colors[index % colors.length]}"><time>${esc(fmtDate(phase.start_local).split(",")[0])}</time><strong>${esc(phase.id.replaceAll("-", " "))}</strong><p>${esc(phase.focus)}</p></article>`).join("");
      };

      const renderTimeline = () => {
        const start = new Date(data.meta.start).getTime();
        const end = new Date(data.meta.end).getTime();
        const span = end - start;
        const pct = value => Math.max(0, Math.min(100, 100 * (new Date(value).getTime() - start) / span));
        const dates = [];
        for (let index = 0; index < 6; index += 1) dates.push(new Date(start + span * index / 5));
        $("#timeline-axis").innerHTML = dates.map(date => `<span>${esc(new Intl.DateTimeFormat("en-US", {month:"short", day:"numeric", timeZone:"Europe/Berlin"}).format(date))}</span>`).join("");
        $("#task-timeline").innerHTML = data.tasks.map(task => {
          const left = pct(task.start);
          const finish = pct(task.finish);
          const width = Math.max(.6, finish - left);
          const ledger = task.ledger ? pct(task.ledger) : null;
          const taskSummary = `${task.description} · ${fmtDate(task.start)} to ${fmtDate(task.finish)} · ${fmtDuration(task.worker)} worker time`;
          return `<div class="task-row${task.overlay ? " overlay" : ""}" tabindex="0" aria-label="${esc(taskSummary)}" title="${esc(taskSummary)}"><span class="task-label"><strong>${esc(task.group === "P0-19/21 schadensfresse closeout" ? "Schadens" : task.group)}</strong><small>${task.files} file${task.files === 1 ? "" : "s"}</small></span><div class="task-track"><i class="task-bar" style="left:${left.toFixed(3)}%;width:${width.toFixed(3)}%"></i>${ledger === null ? "" : `<i class="ledger-dot" style="left:${ledger.toFixed(3)}%"></i>`}</div><span class="task-stat"><strong>${esc(fmtDuration(task.worker))}</strong><small>${task.turns}t / ${task.threads}a</small></span></div>`;
        }).join("");
      };

      const renderOrchestration = () => {
        const c = data.summary.concurrency;
        const active = c.wall_seconds_with_any_subagent;
        const segments = [
          { cls: "one", value: c.seconds_by_concurrency["1"], label: "1" },
          { cls: "two", value: c.seconds_by_concurrency["2"], label: "2" },
          { cls: "three", value: c.seconds_by_concurrency["3"], label: "3" },
        ];
        $("#concurrency-stack").innerHTML = segments.map(item => `<div class="stack-part ${item.cls}" style="width:${(100 * item.value / active).toFixed(3)}%" title="${item.label} active: ${fmtDuration(item.value)}">${(100 * item.value / active).toFixed(1)}%</div>`).join("");
        const comparisons = [
          { label: "Avg concurrency", old: data.baseline.average_concurrency, now: c.average_concurrency_while_active, fmt: value => value.toFixed(2) },
          { label: "2+ active share", old: data.baseline.two_plus_share, now: 100 * c.wall_seconds_with_two_or_more_subagents / active, fmt: value => `${value.toFixed(1)}%` },
          { label: "Max concurrent", old: data.baseline.maximum_concurrency, now: c.maximum_concurrent_subagents, fmt: value => String(value) },
          { label: "Waits / turn", old: data.baseline.waits_per_turn, now: data.summary.rootCalls.wait_agent / data.summary.turns, fmt: value => value.toFixed(2) },
        ];
        $("#baseline-comparison").innerHTML = comparisons.map(item => {
          const scale = Math.max(item.old, item.now);
          return `<div class="compare-row"><span>${esc(item.label)}</span><div class="bar-pair"><div class="mini-bar"><i style="width:${100 * item.old / scale}%"></i></div><div class="mini-bar current"><i style="width:${100 * item.now / scale}%"></i></div></div><strong>${esc(item.fmt(item.now))}</strong></div>`;
        }).join("");
        const calls = [["wait_agent", "waits"], ["send_message", "messages"], ["followup_task", "follow-ups"], ["list_agents", "agent polls"], ["spawn_agent", "spawn attempts"], ["interrupt_agent", "interrupts"]];
        $("#call-grid").innerHTML = calls.map(([key, label]) => `<div class="call"><strong>${number.format(data.summary.rootCalls[key])}</strong><span>${esc(label)}</span></div>`).join("");
      };

      let agentExpanded = false;
      const renderAgents = () => {
        const query = $("#agent-search").value.trim().toLowerCase();
        const group = $("#agent-group").value;
        const sort = $("#agent-sort").value;
        const rows = data.agents.filter(agent => (!query || agent.path.toLowerCase().includes(query)) && (group === "all" || agent.group === group)).sort((a, b) => (b[sort] || 0) - (a[sort] || 0));
        const visible = agentExpanded ? rows : rows.slice(0, 12);
        $("#agent-body").innerHTML = visible.map(agent => `<tr><td class="mono"><span class="agent-name"><span>${esc(agent.path)}</span><small>${esc(agent.nickname || "unnamed")}</small></span></td><td><span class="group-tag">${esc(agent.group)}</span></td><td class="num mono">${number.format(agent.turns)}</td><td class="num mono">${esc(fmtDuration(agent.seconds))}</td><td class="num mono" title="${number.format(agent.tokens)} tokens">${esc(compact.format(agent.tokens))}</td><td class="num mono">${money.format(agent.cost || 0)}</td></tr>`).join("");
        $("#agent-summary").textContent = `${visible.length} of ${rows.length}`;
        $("#agent-more").hidden = rows.length <= 12;
        $("#agent-more").textContent = agentExpanded ? "Show the first 12 matching agents" : `Show all ${rows.length} matching agents`;
      };
      const initAgents = () => {
        const groups = [...new Set(data.agents.map(agent => agent.group))].sort();
        $("#agent-group").insertAdjacentHTML("beforeend", groups.map(group => `<option value="${esc(group)}">${esc(group)}</option>`).join(""));
        ["#agent-search", "#agent-group", "#agent-sort"].forEach(selector => $(selector).addEventListener(selector === "#agent-search" ? "input" : "change", () => { agentExpanded = false; renderAgents(); }));
        $("#agent-more").addEventListener("click", () => { agentExpanded = !agentExpanded; renderAgents(); });
        renderAgents();
      };

      const renderCost = () => {
        const total = data.summary.familyCost;
        const rootShare = 100 * data.summary.rootCost / total;
        const circumference = 100;
        $("#cost-root-ring").style.strokeDasharray = `${rootShare} ${circumference - rootShare}`;
        $("#cost-root-ring").style.strokeDashoffset = "0";
        $("#cost-agent-ring").style.strokeDasharray = `${100 - rootShare} ${rootShare}`;
        $("#cost-agent-ring").style.strokeDashoffset = `${-rootShare}`;
        $("#cost-center").textContent = money.format(total);
        const lines = [
          { label: "Root orchestrator", cost: data.summary.rootCost, color: "var(--amber)", tokens: data.summary.rootUsage.total_tokens },
          { label: "102 real task agents", cost: data.summary.subagentCost, color: "var(--mint)", tokens: data.summary.subagentUsage.total_tokens },
        ];
        $("#cost-lines").innerHTML = lines.map(line => `<div class="cost-line"><i style="--color:${line.color}"></i><span>${esc(line.label)} · ${compact.format(line.tokens)} tokens</span><strong>${money.format(line.cost)}</strong></div>`).join("");
        $("#guardian-note").innerHTML = `<strong>${number.format(data.summary.guardianThreads)} internal guardians</strong> added ${compact.format(data.summary.guardianUsage.total_tokens)} tokens across ${number.format(data.summary.guardianTurns)} turns. Their internal model has no public list price, so it is shown as overhead and excluded from the dollar total.`;
        const uncached = data.summary.familyUsage.input_tokens - data.summary.familyUsage.cached_input_tokens;
        const cacheShare = 100 * data.summary.familyUsage.cached_input_tokens / data.summary.familyUsage.input_tokens;
        $("#token-strip").innerHTML = `<div class="token-stat"><strong>${cacheShare.toFixed(2)}%</strong><span>cached input share</span></div><div class="token-stat"><strong>${compact.format(uncached)}</strong><span>uncached input tokens</span></div><div class="token-stat"><strong>${compact.format(data.summary.familyUsage.output_tokens)}</strong><span>output tokens</span></div>`;
      };

      let selectedCategory = "all";
      const renderMessages = () => {
        const rows = data.interventions.filter(message => selectedCategory === "all" || message.kind === selectedCategory);
        $("#message-count").textContent = `${rows.length} message${rows.length === 1 ? "" : "s"}`;
        $("#message-list").innerHTML = rows.length ? rows.map(message => `<article class="message${message.key ? " key" : ""}"><time>#${message.ordinal}<br>${esc(fmtDate(message.timestamp))}</time><p><span class="message-kind">${esc(categoryMeta[message.kind].label)}</span>${esc(message.excerpt)}</p></article>`).join("") : `<div class="message-empty">No messages in this category.</div>`;
        document.querySelectorAll(".category-button").forEach(button => button.setAttribute("aria-pressed", String(button.dataset.category === selectedCategory)));
      };
      const initMessages = () => {
        const categories = ["all", "authorization-or-external-unblock", "scope-correction-or-clarification", "status-or-process-question", "kickoff"];
        $("#category-buttons").innerHTML = categories.map(category => {
          const meta = category === "all" ? { label: "All genuine messages", color: "var(--blue)" } : categoryMeta[category];
          const count = category === "all" ? data.interventions.length : data.interventions.filter(message => message.kind === category).length;
          return `<button type="button" class="category-button" data-category="${esc(category)}" aria-pressed="${category === "all"}" style="--category:${meta.color}"><span>${esc(meta.label)}</span><strong>${count}</strong></button>`;
        }).join("");
        document.querySelectorAll(".category-button").forEach(button => button.addEventListener("click", () => { selectedCategory = button.dataset.category; renderMessages(); }));
        renderMessages();
      };

      const renderFindings = () => {
        $("#findings").innerHTML = data.findings.map((finding, index) => `<details class="finding"${index === 0 ? " open" : ""}><summary><span class="finding-index">${String(index + 1).padStart(2, "0")}</span><span class="finding-title">${esc(finding.id.replaceAll("-", " "))}</span><span class="finding-task">${esc(finding.task)}</span></summary><div class="finding-body"><dl><dt>Discovery</dt><dd>${esc(finding.discovery)}</dd><dt>Resolution</dt><dd>${esc(finding.resolution)}</dd></dl><div class="commit-list">${finding.commits.map(commit => `<a href="https://github.com/ehonda/KicktippAi/commit/${esc(commit)}">${esc(commit)}</a>`).join("")}</div></div></details>`).join("");
      };

      const initNavigation = () => {
        const links = [...document.querySelectorAll(".nav a")];
        const sections = links.map(link => document.querySelector(link.getAttribute("href"))).filter(Boolean);
        if (!("IntersectionObserver" in window)) return;
        const observer = new IntersectionObserver(entries => {
          const visible = entries.filter(entry => entry.isIntersecting).sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
          if (!visible) return;
          links.forEach(link => link.classList.toggle("active", link.getAttribute("href") === `#${visible.target.id}`));
        }, { rootMargin: "-15% 0px -70%", threshold: [0, .2, .5] });
        sections.forEach(section => observer.observe(section));
      };

      const renderSource = () => {
        $("#source-line").textContent = `${data.meta.thread} · ${data.meta.baseCommit}..${data.meta.finalCommit} · pricing ${data.meta.pricingAsOf}`;
      };

      initTheme(); renderHeadline(); renderPhases(); renderTimeline(); renderOrchestration(); initAgents(); renderCost(); initMessages(); renderFindings(); renderSource(); initNavigation();
    })();
  </script>
</body>
</html>
'''


def main() -> None:
    parser = argparse.ArgumentParser()
    source_dir = pathlib.Path(__file__).parent / "data"
    parser.add_argument("--analysis", type=pathlib.Path, default=source_dir / "analysis.json")
    parser.add_argument("--curated", type=pathlib.Path, default=source_dir / "curated-findings.json")
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[3] / "session-analysis" / "p0-closeout" / "index.html",
    )
    args = parser.parse_args()
    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    curated = json.loads(args.curated.read_text(encoding="utf-8"))
    payload = build_payload(analysis, curated)
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    output = HTML_TEMPLATE.replace("__REPORT_DATA__", encoded)
    validate_output(output, payload)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8", newline="\n")
    print(f"Wrote {args.output} ({args.output.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
