#!/usr/bin/env python3
"""Build the self-contained P1 orchestration follow-up report."""

from __future__ import annotations

import argparse
import csv
from collections import defaultdict
from html.parser import HTMLParser
import json
import pathlib
import re
from typing import Any


class Inspector(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.duplicates: set[str] = set()
        self.fragments: set[str] = set()
        self.assets: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        element_id = values.get("id")
        if element_id:
            if element_id in self.ids:
                self.duplicates.add(element_id)
            self.ids.add(element_id)
        href = values.get("href")
        if tag == "a" and href and href.startswith("#"):
            self.fragments.add(href[1:])
        if tag in {"script", "img", "iframe", "video", "audio"} and values.get("src"):
            self.assets.append(values["src"] or "")
        if tag == "link" and "stylesheet" in (values.get("rel") or "").split():
            self.assets.append(href or "")


def read_csv(path: pathlib.Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def role_for(path: str) -> str:
    if "architecture" in path:
        return "Architecture & re-freeze"
    if "spec_review" in path:
        return "Specification & milestone review"
    if "review" in path:
        return "Correctness review"
    if any(word in path for word in ("ci_reconcile", "run_reconcile", "validation")):
        return "Validation & reconciliation"
    if path.endswith("p1_seed_context_failure_analysis"):
        return "Failure analysis & high-risk writer"
    if any(word in path for word in ("audit", "analysis", "preflight")):
        return "Audit & analysis"
    if any(word in path for word in ("docs", "runtime", "fix")):
        return "Writer"
    return "Other"


def build_payload(data_dir: pathlib.Path, old_data_dir: pathlib.Path) -> dict[str, Any]:
    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    old_analysis = json.loads((old_data_dir / "analysis.json").read_text(encoding="utf-8"))
    comparison = json.loads((data_dir / "comparison-metrics.json").read_text(encoding="utf-8"))
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    findings = json.loads((data_dir / "curated-findings.json").read_text(encoding="utf-8"))["findings"]

    subagents = [thread for thread in analysis["threads"] if thread["kind"] == "subagent"]
    model_buckets: dict[tuple[str, str], dict[str, Any]] = defaultdict(
        lambda: {"threads": 0, "turns": 0, "seconds": 0.0, "tokens": 0}
    )
    role_buckets: dict[str, dict[str, Any]] = defaultdict(
        lambda: {"threads": 0, "turns": 0, "seconds": 0.0, "tokens": 0}
    )
    agents = []
    for thread in subagents:
        role = role_for(thread["agent_path"])
        role_bucket = role_buckets[role]
        role_bucket["threads"] += 1
        role_bucket["turns"] += thread["turn_count"]
        role_bucket["seconds"] += thread["active_seconds"]
        role_bucket["tokens"] += thread["usage"]["total_tokens"]
        model_labels = []
        for usage in thread["model_effort_usage"]:
            key = (usage["model"], usage["reasoning_effort"])
            bucket = model_buckets[key]
            bucket["threads"] += 1
            bucket["turns"] += thread["turn_count"]
            bucket["seconds"] += thread["active_seconds"]
            bucket["tokens"] += usage["usage"]["total_tokens"]
            model_labels.append(f"{usage['model'].replace('gpt-5.6-', '')}/{usage['reasoning_effort']}")
        agents.append({
            "path": thread["agent_path"],
            "role": role,
            "task": thread["task_group"],
            "turns": thread["turn_count"],
            "completed": thread["completed_turns"],
            "seconds": thread["active_seconds"],
            "tokens": thread["usage"]["total_tokens"],
            "model": "; ".join(model_labels),
        })
    agents.sort(key=lambda row: row["tokens"], reverse=True)
    models = [
        {"model": model, "effort": effort, **values}
        for (model, effort), values in model_buckets.items()
    ]
    models.sort(key=lambda row: row["tokens"], reverse=True)
    roles = [{"role": role, **values} for role, values in role_buckets.items()]
    roles.sort(key=lambda row: row["tokens"], reverse=True)

    commits = read_csv(data_dir / "commit-stats.csv")
    reviews = read_csv(data_dir / "review-turns.csv")
    ci_runs = read_csv(data_dir / "ci-runs.csv")
    old_summary = old_analysis["summary"]
    new_summary = analysis["summary"]

    improvements = [
        {"name": "Whole-phase preview", "verdict": "Worked", "tone": "good", "evidence": "Architecture, status, and independent specification audits ran before implementation; five later P1 nodes remained needs-interview.", "caveat": "The owner still had to ask whether implementation had begun, so the preview-to-execution boundary was not sufficiently visible."},
        {"name": "Production continuity", "verdict": "Strongest win", "tone": "good", "evidence": "Recovered main stayed separate from draft PR #97, and the exact recovered SHA passed a full 16-job production-live run.", "caveat": "Much of this snapshot is recovery work caused by the earlier run, so it is valuable output but not ordinary feature throughput."},
        {"name": "Milestone publication", "verdict": "Worked", "tone": "good", "evidence": "Two main CI runs and three PR milestone runs replaced fourteen baseline main runs; flawed R1 commits stayed local.", "caveat": "The draft branch is not merge-ready, so the final release cadence remains untested."},
        {"name": "Machine admission", "verdict": "Worked", "tone": "good", "evidence": "One heavy lease, no classified dotnet overlap, at most two linked task worktrees, and a fail-closed low-memory denial.", "caveat": "Thirty-eight admission samples are defensible but frequent; the final analysis should check whether every sample marked a real transition."},
        {"name": "Ledger coalescing", "verdict": "Material gain", "tone": "good", "evidence": "106 versus 375 patches; 10.2 versus 28.4 patches per adjusted hour; current state is roughly one-third the prior size.", "caveat": "Six compactions still occurred and root wait frequency was unchanged."},
        {"name": "Architecture + spec roles", "verdict": "Valuable, costly", "tone": "mixed", "evidence": "All six design-review turns found concrete corrections; architecture/review prevented unsafe or underspecified work from crossing gates.", "caveat": "The xhigh portfolio used 45.6M tokens and later absorbed artifact review, while R1 still needed three correction cycles."},
        {"name": "Persistent role reuse", "verdict": "Needs tuning", "tone": "warn", "evidence": "Turns per thread rose from 1.90 to 2.59 and realized threads fell from 78 to 27.", "caveat": "Three persistent threads exhausted task-agent capacity, blocked a Luna monitor, and pushed polling back to the root."},
        {"name": "Bounded Git authority", "verdict": "Worked", "tone": "good", "evidence": "Pushes and draft-PR updates proceeded without the baseline's surprise publication-approval pause, with explicit exact-target preflights.", "caveat": "This evaluates the allowed main/branch/PR path only; merge authority was correctly not exercised."},
        {"name": "Compaction recovery", "verdict": "Worked", "tone": "good", "evidence": "Six root compactions did not lose run identity, ownership, exact SHAs, resource leases, or the deferred interview frontier.", "caveat": "Compactions per adjusted hour increased slightly, so recovery became safer rather than rarer."},
        {"name": "Root control plane", "verdict": "Disciplined, expensive", "tone": "mixed", "evidence": "Root source changes stayed at orchestration/integration boundaries and worker work remained delegated.", "caveat": "Root share rose from 37.6% to 59.1% of tokens; wait frequency stayed flat at about 47 per adjusted hour."},
    ]

    return {
        "meta": {
            "generated": analysis["generated_at"],
            "start": new_summary["session_started_at"],
            "end": new_summary["session_last_event_at"],
            "oldRun": old_summary["root_thread_id"],
            "newRun": new_summary["root_thread_id"],
            "base": derived["session_boundary"]["base_commit"],
            "final": derived["session_boundary"]["final_commit"],
            "status": "interim; successor session active with R2a incomplete",
        },
        "old": old_summary,
        "new": new_summary,
        "comparison": comparison,
        "derived": derived,
        "models": models,
        "roles": roles,
        "agents": agents,
        "commits": commits,
        "reviews": reviews,
        "ci": ci_runs,
        "findings": findings,
        "improvements": improvements,
    }


def validate(output: str, payload: dict[str, Any]) -> None:
    if "__REPORT_DATA__" in output:
        raise ValueError("Report payload was not embedded")
    inspector = Inspector()
    inspector.feed(output)
    if inspector.duplicates:
        raise ValueError(f"Duplicate IDs: {sorted(inspector.duplicates)}")
    missing = inspector.fragments - inspector.ids
    if missing:
        raise ValueError(f"Missing fragment targets: {sorted(missing)}")
    if inspector.assets:
        raise ValueError(f"External assets break self-containment: {inspector.assets}")
    if payload["new"]["subagent_threads"] != len(payload["agents"]):
        raise ValueError("Agent row count mismatch")


HTML = r'''<!doctype html>
<html lang="en" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="kicktippai-report-title" content="P1 orchestration follow-up">
  <title>P1 orchestration follow-up</title>
  <style>
    :root{--bg:#0b1020;--panel:#121a2c;--panel2:#18233a;--text:#eef4ff;--muted:#9facbf;--line:#283752;--cyan:#68d8e8;--mint:#71e0b5;--amber:#ffc46b;--red:#ff8494;--violet:#b7a2ff;--shadow:0 22px 70px rgba(0,0,0,.28)}
    :root[data-theme="light"]{--bg:#f3f0e9;--panel:#fffdf8;--panel2:#f2ece1;--text:#172033;--muted:#687286;--line:#ddd3c3;--shadow:0 20px 55px rgba(58,48,35,.12)}
    *{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;color:var(--text);font-family:Inter,"Segoe UI",sans-serif;line-height:1.55;background:radial-gradient(circle at 8% 0,rgba(104,216,232,.13),transparent 30%),radial-gradient(circle at 92% 8%,rgba(183,162,255,.13),transparent 28%),var(--bg)}a{color:var(--cyan)}button,input,select{font:inherit}.wrap{width:min(1220px,calc(100% - 34px));margin:auto}.top{position:sticky;top:0;z-index:9;border-bottom:1px solid var(--line);background:color-mix(in srgb,var(--bg) 86%,transparent);backdrop-filter:blur(15px)}.top .wrap{display:flex;align-items:center;gap:18px;min-height:60px}.brand{font-weight:900;margin-right:auto}.nav{display:flex;gap:16px}.nav a{text-decoration:none;color:var(--muted);font-size:.84rem}.theme{border:1px solid var(--line);background:var(--panel);color:var(--text);border-radius:999px;padding:7px 12px;cursor:pointer}.hero{padding:74px 0 30px}.eyebrow{color:var(--cyan);font-weight:900;text-transform:uppercase;letter-spacing:.16em;font-size:.76rem}.hero h1{font-size:clamp(2.8rem,7vw,6rem);line-height:.94;letter-spacing:-.06em;margin:15px 0 22px;max-width:1050px}.hero .lead{font-size:clamp(1.08rem,2vw,1.32rem);color:var(--muted);max-width:920px}.verdict{display:grid;grid-template-columns:auto 1fr;gap:18px;align-items:start;margin-top:30px;padding:22px;border-radius:20px;border:1px solid color-mix(in srgb,var(--cyan) 42%,var(--line));background:linear-gradient(115deg,rgba(104,216,232,.12),rgba(183,162,255,.08))}.verdict b{font-size:1.45rem;color:var(--cyan)}.chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:18px}.chip{border:1px solid var(--line);border-radius:999px;background:var(--panel);padding:7px 10px;color:var(--muted);font-size:.8rem}.kpis{display:grid;grid-template-columns:repeat(6,1fr);gap:11px;margin:26px 0 66px}.kpi{border:1px solid var(--line);border-radius:17px;background:var(--panel);padding:17px;box-shadow:var(--shadow)}.kpi strong{display:block;font-size:clamp(1.4rem,3vw,2.15rem);line-height:1;margin-bottom:8px}.kpi span{color:var(--muted);font-size:.8rem}.section{padding:35px 0 50px;scroll-margin-top:72px}.section-head{display:grid;grid-template-columns:minmax(0,1fr) minmax(260px,.58fr);gap:25px;align-items:end;margin-bottom:23px}.section h2{font-size:clamp(2rem,4vw,3.25rem);letter-spacing:-.045em;line-height:1;margin:0}.section-head p{margin:0;color:var(--muted)}.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.card{border:1px solid var(--line);border-radius:20px;background:var(--panel);padding:22px;box-shadow:var(--shadow)}.card h3{margin:0 0 8px}.card p{margin:0;color:var(--muted)}.signal{display:inline-flex;border-radius:999px;padding:4px 9px;margin-bottom:13px;font-size:.71rem;font-weight:900;text-transform:uppercase;letter-spacing:.08em}.good{color:var(--mint);background:rgba(113,224,181,.12)}.mixed{color:var(--amber);background:rgba(255,196,107,.12)}.warn{color:var(--red);background:rgba(255,132,148,.12)}.compare{display:grid;gap:14px}.metric{display:grid;grid-template-columns:200px 1fr 88px;gap:13px;align-items:center}.metric-label strong{display:block}.metric-label small{color:var(--muted)}.duo{display:grid;gap:6px}.track{height:12px;background:var(--panel2);border-radius:999px;overflow:hidden}.track i{display:block;height:100%;border-radius:inherit}.track.old i{background:var(--violet)}.track.new i{background:var(--cyan)}.delta{text-align:right;font-variant-numeric:tabular-nums;font-weight:900}.legend{display:flex;gap:14px;color:var(--muted);font-size:.78rem;margin-bottom:16px}.legend i{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:5px}.legend .old{background:var(--violet)}.legend .new{background:var(--cyan)}.big-stat{font-size:clamp(2.2rem,5vw,4.4rem);line-height:1;font-weight:900;letter-spacing:-.05em}.big-stat.down{color:var(--mint)}.facts{display:grid;grid-template-columns:repeat(3,1fr);gap:9px;margin-top:16px}.fact{background:var(--panel2);border-radius:14px;padding:12px}.fact strong{display:block;font-size:1.22rem}.fact span{color:var(--muted);font-size:.74rem}.callout{border-left:4px solid var(--amber);padding:13px 0 13px 16px;color:var(--muted);margin-top:16px}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:18px;background:var(--panel)}table{border-collapse:collapse;width:100%;min-width:760px}th,td{padding:12px 14px;border-bottom:1px solid var(--line);text-align:left;font-size:.82rem}th{position:sticky;top:0;background:var(--panel2);color:var(--muted);font-size:.7rem;text-transform:uppercase;letter-spacing:.08em}td.num{text-align:right;font-variant-numeric:tabular-nums}.mono{font-family:"Cascadia Code",Consolas,monospace}.role-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.role-card{position:relative;overflow:hidden}.role-card:before{content:"";position:absolute;inset:0 auto 0 0;width:4px;background:var(--cyan)}.role-card:nth-child(2):before{background:var(--violet)}.role-card:nth-child(3):before{background:var(--amber)}.role-card:nth-child(4):before{background:var(--mint)}.role-card strong{font-size:1.45rem}.bar-list{display:grid;gap:12px}.bar-row{display:grid;grid-template-columns:165px 1fr 84px;gap:12px;align-items:center}.bar-row small{color:var(--muted)}.bar-row .track{height:13px}.filters{display:flex;gap:9px;margin-bottom:12px}.filters input,.filters select{border:1px solid var(--line);background:var(--panel);color:var(--text);padding:9px 11px;border-radius:10px}.filters input{flex:1}.scorecard{display:grid;gap:10px}.improvement{display:grid;grid-template-columns:210px 130px 1fr;gap:16px;align-items:start;padding:17px;border:1px solid var(--line);border-radius:16px;background:var(--panel)}.improvement h3{font-size:1rem;margin:0}.improvement p{margin:0;color:var(--muted);font-size:.85rem}.improvement p + p{margin-top:6px}.badge{width:max-content;border-radius:999px;padding:4px 8px;font-size:.7rem;font-weight:900;text-transform:uppercase}.recommendations{counter-reset:rec}.rec{display:grid;grid-template-columns:48px 1fr;gap:13px;padding:18px 0;border-bottom:1px solid var(--line)}.rec:last-child{border-bottom:0}.rec:before{counter-increment:rec;content:counter(rec);display:grid;place-items:center;width:38px;height:38px;border-radius:50%;background:var(--panel2);color:var(--cyan);font-weight:900}.rec h3{margin:0 0 5px}.rec p{margin:0;color:var(--muted)}.method{font-size:.87rem;color:var(--muted)}.source-links{display:flex;flex-wrap:wrap;gap:12px;margin-top:14px}footer{border-top:1px solid var(--line);padding:34px 0 56px;color:var(--muted);font-size:.8rem}
    @media(max-width:980px){.kpis{grid-template-columns:repeat(3,1fr)}.grid3,.role-grid{grid-template-columns:1fr}.section-head{grid-template-columns:1fr}.improvement{grid-template-columns:170px 120px 1fr}.nav{display:none}}
    @media(max-width:680px){.wrap{width:min(100% - 20px,1220px)}.hero{padding-top:50px}.kpis{grid-template-columns:repeat(2,1fr)}.grid2{grid-template-columns:1fr}.metric{grid-template-columns:1fr}.delta{text-align:left}.facts{grid-template-columns:1fr}.improvement{grid-template-columns:1fr}.filters{flex-direction:column}.verdict{grid-template-columns:1fr}.bar-row{grid-template-columns:120px 1fr 70px}}
  </style>
</head>
<body>
  <header class="top"><div class="wrap"><div class="brand">KicktippAi · Codex</div><nav class="nav"><a href="#parallelism">Parallelism</a><a href="#efficiency">Efficiency</a><a href="#roles">Roles</a><a href="#improvements">Improvements</a><a href="#recommendations">Next</a><a href="#method">Method</a></nav><button class="theme" id="theme" type="button">Theme</button></div></header>
  <main class="wrap">
    <section class="hero">
      <div class="eyebrow">P1 orchestration follow-up · 31 Aug 2026</div>
      <h1>Less parallel.<br>More efficient.</h1>
      <p class="lead">An early read on the first successor session: machine-aware admission reduced heavy overlap, milestone gates cut release ceremony, and architecture moved forward. The tradeoff is a much more sequential pipeline and a root control plane that now dominates usage.</p>
      <div class="verdict"><b>Verdict</b><div><strong>The new regime is directionally better, but the gain is consolidation—not increased throughput from parallel workers.</strong> Sustained concurrency fell sharply while logged token intensity fell by roughly two-fifths. The next tuning target is task-agent slot and root-polling pressure, not relaxing the heavy-operation limit.</div></div>
      <div class="chips"><span class="chip" id="snapshot"></span><span class="chip"><strong>Run:</strong> <span class="mono" id="run"></span></span><span class="chip"><strong>Boundary:</strong> <span class="mono" id="boundary"></span></span><span class="chip"><strong>Status:</strong> R2a active at cutoff</span></div>
    </section>
    <section class="kpis">
      <article class="kpi"><strong>1.12</strong><span>avg active concurrency · was 1.52</span></article>
      <article class="kpi"><strong>−42%</strong><span>logged tokens / adjusted hour</span></article>
      <article class="kpi"><strong>5</strong><span>CI runs · was 14</span></article>
      <article class="kpi"><strong>106</strong><span>ledger patches · was 375</span></article>
      <article class="kpi"><strong>27</strong><span>task threads · was 78</span></article>
      <article class="kpi"><strong>10h 24m</strong><span>wall after longest pause</span></article>
    </section>

    <section class="section" id="parallelism">
      <div class="section-head"><h2>Parallelism declined—substantially.</h2><p>The successor occasionally reached three task agents, but most useful work ran as one dependency-bound lane. Maximum burst capacity improved; sustained occupancy did not.</p></div>
      <div class="grid2">
        <article class="card"><span class="signal mixed">Measured tradeoff</span><h3>Concurrency comparison</h3><div class="legend"><span><i class="old"></i>first P1 sample</span><span><i class="new"></i>successor</span></div><div class="compare" id="parallel-bars"></div></article>
        <article class="card"><span class="signal good">Machine discipline</span><h3>Heavy work stopped competing</h3><p>Resource checks gated every worktree/heavy transition. Directly classified dotnet calls never overlapped, and the latest low-memory sample denied another heavy family.</p><div class="facts"><div class="fact"><strong>38</strong><span>resource samples</span></div><div class="fact"><strong>1</strong><span>max dotnet overlap</span></div><div class="fact"><strong>19.1%</strong><span>worker time in tools · was 42.8%</span></div></div><div class="callout">The heavy lease explains some lost concurrency, but not most of it: recovery ordering, R0→R1→R2 dependencies, one reusable writer worktree, and reserved role slots serialized non-heavy work too.</div></article>
      </div>
      <div class="grid3" style="margin-top:14px">
        <article class="card"><span class="signal warn">Capacity seam</span><h3>“Idle and recallable” still used a slot</h3><p>Architecture, specification review, and the writer occupied all three task-thread slots. A Luna monitor could not start, so root polled CI itself.</p></article>
        <article class="card"><span class="signal mixed">Critical path</span><h3>Safe, but inherently serial</h3><p>Recovery, exact review, CI, R0 specification, R1 contract code, and three remediation cycles formed one gated chain. There was little frozen independent work to overlap.</p></article>
        <article class="card"><span class="signal good">No collapse</span><h3>Burst concurrency reached three</h3><p>The runtime and policy can still overlap read/review work. The problem is lane availability and slot reservation, not a universal one-agent cap.</p></article>
      </div>
    </section>

    <section class="section" id="efficiency">
      <div class="section-head"><h2>The efficiency signal survives a transcript check.</h2><p>Weekly quota telemetry is not in the logs, so this uses pause-adjusted transcript intensity. It corroborates the owner's rough quota observation without pretending quota units equal tokens.</p></div>
      <div class="grid2">
        <article class="card"><span class="signal good">Independent evidence</span><div class="big-stat down">−42.2%</div><h3>logged tokens per adjusted hour</h3><div class="compare" id="efficiency-bars" style="margin-top:18px"></div><div class="callout">At exactly 27% for eight hours, a linear quota extrapolation is about 29.6 hours; “about 30–32h” is fair given both inputs were approximate. The report does not infer an exact conversion.</div></article>
        <article class="card"><span class="signal mixed">Next bottleneck</span><h3>The savings came from workers, not root</h3><p>Worker tokens per worker-hour fell 35.4%. Root tokens per adjusted hour fell only 9.2%, while root share rose from 37.6% to 59.1%.</p><div class="facts"><div class="fact"><strong>59.1%</strong><span>new root token share</span></div><div class="fact"><strong>46.7/h</strong><span>root waits · unchanged</span></div><div class="fact"><strong>6</strong><span>root compactions</span></div></div><div class="callout">Thread reuse and smaller worker fan-out worked. Root polling, exact-target checks, role routing, and repeated re-freezes now define the control-plane floor.</div></article>
      </div>
      <div class="grid2" style="margin-top:14px">
        <article class="card"><h3>Coordination consolidated</h3><div class="facts"><div class="fact"><strong>27</strong><span>threads · down from 78</span></div><div class="fact"><strong>2.59</strong><span>turns/thread · up from 1.90</span></div><div class="fact"><strong>10.2/h</strong><span>ledger patches · down from 28.4</span></div></div></article>
        <article class="card"><h3>Release ceremony consolidated</h3><div class="facts"><div class="fact"><strong>2 + 3</strong><span>main + draft-PR CI runs</span></div><div class="fact"><strong>18m 41s</strong><span>CI wall · down from 56m 15s</span></div><div class="fact"><strong>2</strong><span>remote run branches · down from 14</span></div></div></article>
      </div>
    </section>

    <section class="section" id="roles">
      <div class="section-head"><h2>The new roles caught real problems.</h2><p>They also concentrated cost and scarce agent capacity. The role design is useful; its lifecycle and post-freeze boundaries need tuning.</p></div>
      <div class="role-grid">
        <article class="card role-card"><span class="signal good">Architecture</span><h3>Lead and re-freeze agents</h3><strong>8 turns · 90m</strong><p>Mapped the phase, corrected recovery topology, and replaced a flawed compatibility firewall with global typing. Later work used a second architecture thread rather than one literal phase-long owner.</p></article>
        <article class="card role-card"><span class="signal mixed">Specification</span><h3>Independent xhigh gate</h3><strong>12 turns · 112m</strong><p>Six pre-implementation design turns all found concrete fixes. The same xhigh lane then performed R0/R1 exact-artifact review, preserving context but crossing into correctness-review work.</p></article>
        <article class="card role-card"><span class="signal warn">High-risk writer</span><h3>One reused Sol/high lane</h3><strong>12 turns · 152m</strong><p>A failure-analysis thread became the R0/R1 writer. Reuse saved spawn/context churn, but R1 still needed three rejected commits before the fourth exact tip passed.</p></article>
        <article class="card role-card"><span class="signal good">Lightweight gates</span><h3>Terra/Luna stayed bounded</h3><strong>28 turns · 100m</strong><p>Deterministic fixes, validation, CI, and evidence work remained on lighter models. They were not the dominant allowance surface and produced no observed quality escape.</p></article>
      </div>
      <div class="grid2" style="margin-top:14px">
        <article class="card"><h3>Model / effort portfolio</h3><div class="bar-list" id="model-bars"></div></article>
        <article class="card"><h3>Role assessment</h3><p><strong>Early verdict:</strong> keep the architecture/specification separation, but stop treating a persistent xhigh specification thread as the default reviewer for every post-freeze artifact. Reclassify explicitly and use Sol/high correctness review unless a new cross-cutting seam reopens architecture.</p><div class="callout">Sol high + xhigh represented 80.1% of subagent tokens, versus 51.6% Sol/high in the baseline. Total intensity still fell because the workflow created far fewer threads and redundant gates.</div></article>
      </div>
      <div class="filters" style="margin-top:16px"><input id="agent-search" type="search" placeholder="Filter agent path"><select id="agent-role"><option value="all">All roles</option></select></div>
      <div class="table-wrap"><table><thead><tr><th>Agent</th><th>Role</th><th>Model</th><th class="num">Turns</th><th class="num">Worker time</th><th class="num">Tokens</th></tr></thead><tbody id="agent-body"></tbody></table></div>
    </section>

    <section class="section" id="improvements">
      <div class="section-head"><h2>Improvement-by-improvement audit</h2><p>This includes changes beyond the user's initial prompts: preview/grilling, continuity, Git authority, recovery state, role lifecycle, and compaction behavior.</p></div>
      <div class="scorecard" id="improvement-list"></div>
    </section>

    <section class="section" id="recommendations">
      <div class="section-head"><h2>What to tune before the final review</h2><p>Keep the safety gains. The immediate opportunity is to recover useful non-heavy overlap and remove work from root.</p></div>
      <div class="card recommendations">
        <article class="rec"><div><h3>Keep the one-heavy-operation lease</h3><p>It is doing what it was meant to do: no competing dotnet families, lower tool-time share, and correct fail-closed behavior under memory pressure.</p></div></article>
        <article class="rec"><div><h3>Reserve one ephemeral task-agent slot</h3><p>Make the runtime meaning of “recallable” explicit. Avoid retaining architecture plus specification plus writer when that blocks Luna monitoring or a safe independent audit.</p></div></article>
        <article class="rec"><div><h3>Separate spec review from correctness review after freeze</h3><p>Use the xhigh spec role to settle cross-cutting seams. Once frozen, use an independently allocated Sol/high reviewer unless new architecture genuinely appears.</p></div></article>
        <article class="rec"><div><h3>Exploit edit/research overlap around the heavy lease</h3><p>Pre-admit the next independent read/edit lane while one task validates locally or waits for remote CI. The current run rarely exercised this intended form of parallelism.</p></div></article>
        <article class="rec"><div><h3>Reduce root polling</h3><p>Ledger coalescing worked, but waits per adjusted hour did not move. Longer event waits and a guaranteed lightweight reconciliation slot should lower the root floor.</p></div></article>
        <article class="rec"><div><h3>Finish the longitudinal comparison</h3><p>At completion, add R2–R5 throughput, final PR/merge/cleanup, escaped defects, actual quota outcome, and whether the production-continuity path remained stable.</p></div></article>
      </div>
    </section>

    <section class="section" id="method">
      <div class="section-head"><h2>Method and limitations</h2><p>This cut is deliberately comparable to the previous report, but it is not a controlled benchmark.</p></div>
      <div class="card method"><p>The successor family is defined by recursive <span class="mono">thread_spawn.parent_thread_id</span> ancestry to <span class="mono">01a054ee…</span>. The report uses native Codex transcript events, exact Git history, run-scoped ledgers, local/remote refs, draft PR #97, and authenticated read-only GitHub Actions metadata. The successor pause adjustment removes the contiguous 10h30m interval between the last preview-agent completion and the owner's resume message; the baseline keeps its published 5h42m54s authorization pause.</p><p style="margin-top:12px">Concurrency uses completed or aborted task-turn intervals; the incomplete R2a turn is excluded. Worker/tool times overlap and are not timesheets. Logged tokens do not map to subscription quota. Git lines and commits describe scope, not value. The two sessions performed different work, and the successor spent meaningful time repairing production state inherited from the first run.</p><div class="source-links"><a href="https://github.com/ehonda/KicktippAi/tree/main/docs/codex/p1-orchestration-follow-up-investigation">Extractor and normalized data</a><a href="https://github.com/ehonda/KicktippAi/tree/main/session-analysis/p1-orchestration-follow-up">Published HTML source</a><a href="../p1-orchestration-interim/index.html">Previous interactive report</a></div></div>
    </section>
  </main>
  <footer><div class="wrap">P1 orchestration follow-up · interim native-transcript comparison</div></footer>
  <script id="report-data" type="application/json">__REPORT_DATA__</script>
  <script>
  (()=>{"use strict";const d=JSON.parse(document.getElementById("report-data").textContent);const $=s=>document.querySelector(s);const num=new Intl.NumberFormat("en-US");const compact=new Intl.NumberFormat("en-US",{notation:"compact",maximumFractionDigits:1});const esc=v=>String(v??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));const dur=s=>{const m=Math.round(Number(s)/60),h=Math.floor(m/60),r=m%60;return h?`${h}h ${r}m`:`${m}m`};const date=v=>new Intl.DateTimeFormat("en-GB",{day:"2-digit",month:"short",hour:"2-digit",minute:"2-digit",hour12:false,timeZone:"Europe/Berlin"}).format(new Date(v));
  $("#theme").onclick=()=>{document.documentElement.dataset.theme=document.documentElement.dataset.theme==="dark"?"light":"dark"};$("#snapshot").innerHTML=`<strong>Snapshot:</strong> ${esc(date(d.meta.end))} CEST`;$("#run").textContent=d.meta.newRun.slice(0,8)+"…";$("#boundary").textContent=`${d.meta.base.slice(0,7)}..${d.meta.final.slice(0,7)}`;
  const p=d.comparison.parallelism;const parallel=[{label:"Average concurrency",note:"while any task agent active",old:p.old_average_while_active,new:p.new_average_while_active,scale:2,fmt:v=>v.toFixed(2),delta:"−26%"},{label:"Two-plus occupancy",note:"share of task-agent-active wall",old:100*p.old_two_plus_share,new:100*p.new_two_plus_share,scale:60,fmt:v=>v.toFixed(1)+"%",delta:"−40.3pp"},{label:"Worker / adjusted wall",note:"aggregate worker seconds per second",old:p.old_worker_seconds_per_effective_wall_second,new:p.new_worker_seconds_per_effective_wall_second,scale:1.5,fmt:v=>v.toFixed(2),delta:"−41%"}];$("#parallel-bars").innerHTML=parallel.map(x=>`<div class="metric"><div class="metric-label"><strong>${esc(x.label)}</strong><small>${esc(x.note)}</small></div><div class="duo"><div class="track old"><i style="width:${100*x.old/x.scale}%"></i></div><div class="track new"><i style="width:${100*x.new/x.scale}%"></i></div><small>${esc(x.fmt(x.old))} → ${esc(x.fmt(x.new))}</small></div><div class="delta">${esc(x.delta)}</div></div>`).join("");
  const e=d.comparison.efficiency;const efficiency=[{label:"All logged tokens / h",old:e.old_logged_tokens_per_effective_hour,new:e.new_logged_tokens_per_effective_hour,delta:"−42.2%"},{label:"Uncached + output / h",old:e.old_non_cached_plus_output_per_effective_hour,new:e.new_non_cached_plus_output_per_effective_hour,delta:"−41.6%"},{label:"Worker tokens / worker-h",old:e.old_worker_tokens_per_worker_hour,new:e.new_worker_tokens_per_worker_hour,delta:"−35.4%"}];const maxEff=Math.max(...efficiency.map(x=>x.old));$("#efficiency-bars").innerHTML=efficiency.map(x=>`<div class="metric"><div class="metric-label"><strong>${esc(x.label)}</strong></div><div class="duo"><div class="track old"><i style="width:${100*x.old/maxEff}%"></i></div><div class="track new"><i style="width:${100*x.new/maxEff}%"></i></div><small>${compact.format(x.old)} → ${compact.format(x.new)}</small></div><div class="delta">${esc(x.delta)}</div></div>`).join("");
  const maxModel=Math.max(...d.models.map(x=>x.tokens));const colors={"gpt-5.6-sol":"var(--violet)","gpt-5.6-terra":"var(--cyan)","gpt-5.6-luna":"var(--mint)"};$("#model-bars").innerHTML=d.models.map(x=>`<div class="bar-row"><small>${esc(x.model.replace("gpt-5.6-","")+"/"+x.effort)}</small><div class="track"><i style="width:${100*x.tokens/maxModel}%;background:${colors[x.model]||"var(--amber)"}"></i></div><strong>${compact.format(x.tokens)}</strong></div>`).join("");
  const roles=[...new Set(d.agents.map(x=>x.role))].sort();$("#agent-role").insertAdjacentHTML("beforeend",roles.map(x=>`<option>${esc(x)}</option>`).join(""));const renderAgents=()=>{const q=$("#agent-search").value.toLowerCase(),r=$("#agent-role").value;$("#agent-body").innerHTML=d.agents.filter(x=>(!q||x.path.toLowerCase().includes(q))&&(r==="all"||x.role===r)).map(x=>`<tr><td class="mono">${esc(x.path)}</td><td>${esc(x.role)}</td><td class="mono">${esc(x.model)}</td><td class="num">${num.format(x.turns)}</td><td class="num">${dur(x.seconds)}</td><td class="num">${compact.format(x.tokens)}</td></tr>`).join("")};$("#agent-search").oninput=renderAgents;$("#agent-role").onchange=renderAgents;renderAgents();
  $("#improvement-list").innerHTML=d.improvements.map(x=>`<article class="improvement"><h3>${esc(x.name)}</h3><span class="badge ${esc(x.tone)}">${esc(x.verdict)}</span><div><p>${esc(x.evidence)}</p><p><strong>Caveat:</strong> ${esc(x.caveat)}</p></div></article>`).join("");
  })();
  </script>
</body></html>'''


def main() -> None:
    source_dir = pathlib.Path(__file__).parent / "data"
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=pathlib.Path, default=source_dir)
    parser.add_argument("--old-data-dir", type=pathlib.Path, default=pathlib.Path(__file__).parents[1] / "p1-orchestration-interim-investigation" / "data")
    parser.add_argument("--output", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3] / "session-analysis" / "p1-orchestration-follow-up" / "index.html")
    args = parser.parse_args()
    payload = build_payload(args.data_dir, args.old_data_dir)
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    output = HTML.replace("__REPORT_DATA__", encoded)
    validate(output, payload)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8", newline="\n")
    print(f"Wrote {args.output} ({args.output.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
