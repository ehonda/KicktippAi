#!/usr/bin/env python3
"""Build the self-contained P1 orchestration interim investigation report."""

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
    if re.search(r"(?:^|/)ci_|_ci$", path):
        return "CI / reconciliation"
    if "review" in path:
        return "Independent review"
    if any(word in path for word in ("audit", "research", "inventory", "monitor", "readiness", "diagnosis", "evidence")):
        return "Research / audit"
    if any(word in path for word in ("writer", "foundation", "persistence", "validator", "removal", "join", "bridge", "resolver", "quarantine")):
        return "Implementation"
    return "Other"


def build_payload(analysis: dict[str, Any], derived: dict[str, Any], data_dir: pathlib.Path) -> dict[str, Any]:
    subagents = [thread for thread in analysis["threads"] if thread["kind"] == "subagent"]
    model_buckets: dict[tuple[str, str], dict[str, Any]] = defaultdict(
        lambda: {"threads": 0, "turns": 0, "seconds": 0.0, "tokens": 0, "input": 0, "cached": 0, "output": 0}
    )
    for thread in subagents:
        for usage in thread["model_effort_usage"]:
            key = (usage["model"], usage["reasoning_effort"])
            bucket = model_buckets[key]
            bucket["threads"] += 1
            bucket["turns"] += thread["turn_count"]
            bucket["seconds"] += thread["active_seconds"]
            bucket["tokens"] += usage["usage"]["total_tokens"]
            bucket["input"] += usage["usage"]["input_tokens"]
            bucket["cached"] += usage["usage"]["cached_input_tokens"]
            bucket["output"] += usage["usage"]["output_tokens"]
    models = [
        {"model": model, "effort": effort, **values}
        for (model, effort), values in model_buckets.items()
    ]
    models.sort(key=lambda row: row["tokens"], reverse=True)

    task_buckets: dict[str, dict[str, Any]] = defaultdict(
        lambda: {"threads": 0, "turns": 0, "seconds": 0.0, "tokens": 0}
    )
    role_buckets: dict[str, dict[str, Any]] = defaultdict(
        lambda: {"threads": 0, "turns": 0, "seconds": 0.0, "tokens": 0}
    )
    agent_rows = []
    for thread in subagents:
        task = task_buckets[thread["task_group"]]
        task["threads"] += 1
        task["turns"] += thread["turn_count"]
        task["seconds"] += thread["active_seconds"]
        task["tokens"] += thread["usage"]["total_tokens"]
        role_name = role_for(thread["agent_path"])
        role = role_buckets[role_name]
        role["threads"] += 1
        role["turns"] += thread["turn_count"]
        role["seconds"] += thread["active_seconds"]
        role["tokens"] += thread["usage"]["total_tokens"]
        agent_rows.append({
            "path": thread["agent_path"],
            "task": thread["task_group"],
            "role": role_name,
            "turns": thread["turn_count"],
            "seconds": thread["active_seconds"],
            "tokens": thread["usage"]["total_tokens"],
            "model": "; ".join(
                f"{item['model'].replace('gpt-5.6-', '')}/{item['reasoning_effort']}"
                for item in thread["model_effort_usage"]
            ),
        })
    tasks = [{"task": name, **values} for name, values in task_buckets.items()]
    tasks.sort(key=lambda row: row["tokens"], reverse=True)
    roles = [{"role": name, **values} for name, values in role_buckets.items()]
    roles.sort(key=lambda row: row["tokens"], reverse=True)
    agent_rows.sort(key=lambda row: row["tokens"], reverse=True)

    commits = read_csv(data_dir / "commit-stats.csv")
    ci_runs = read_csv(data_dir / "ci-runs.csv")
    review_turns = read_csv(data_dir / "review-turns.csv")
    branches = read_csv(data_dir / "branches.csv")
    remote_p1_10 = [row for row in branches if row["ref"].startswith("origin/codex/p1-10-")]
    local_p1_10 = [row for row in branches if row["ref"].startswith("codex/p1-10-")]
    p1_10_threads = [thread for thread in subagents if thread["task_group"] == "P1-10"]
    p1_10_review_threads = [thread for thread in p1_10_threads if "review" in thread["agent_path"]]

    summary = analysis["summary"]
    ci_seconds = sum(int(row["duration_seconds"]) for row in ci_runs)
    p1_10_seconds = sum(thread["active_seconds"] for thread in p1_10_threads)
    p1_10_tokens = sum(thread["usage"]["total_tokens"] for thread in p1_10_threads)
    repeated_review_lanes = sum(thread["completed_turns"] > 1 for thread in p1_10_review_threads)
    completed_review_lanes = sum(thread["completed_turns"] > 0 for thread in p1_10_review_threads)
    root_ledger_updates = derived["root_patch_target_counts"].get("orchestration-ledger", 0)

    return {
        "meta": {
            "thread": summary["root_thread_id"],
            "start": summary["session_started_at"],
            "end": summary["session_last_event_at"],
            "generated": analysis["generated_at"],
            "base": derived["session_boundary"]["base_commit"],
            "final": derived["session_boundary"]["final_commit"],
            "status": "interim snapshot; the parallel session was still active",
        },
        "summary": {
            "wall": summary["session_wall_span_seconds"],
            "authorizationPause": 20_574,
            "threads": summary["subagent_threads"],
            "turns": summary["subagent_turns"],
            "tokens": summary["usage"]["total_tokens"],
            "rootTokens": summary["root_usage"]["total_tokens"],
            "guardians": summary["guardian_threads"],
            "guardianTokens": summary["guardian_usage"]["total_tokens"],
            "commits": summary["session_commits"],
            "concurrency": summary["concurrency"],
            "spawnSelection": summary["subagent_spawn_selection"],
            "rootCalls": summary["root_function_calls"],
            "rootNested": summary["root_nested_tool_calls"],
            "rootCompactions": summary["root_compactions"],
            "p1_10Seconds": p1_10_seconds,
            "p1_10Tokens": p1_10_tokens,
            "p1_10Threads": len(p1_10_threads),
            "toolShare": derived["subagent_tool_share_of_active_time"],
            "toolSeconds": derived["subagent_tool_elapsed_seconds"],
            "dotnetCalls": derived["tool_time_by_class"]["dotnet"]["calls"],
            "dotnetSeconds": derived["tool_time_by_class"]["dotnet"]["elapsed_seconds"],
            "ciRuns": len(ci_runs),
            "ciSeconds": ci_seconds,
            "ciFailures": sum(row["conclusion"] != "success" for row in ci_runs),
            "remoteP110Branches": len(remote_p1_10),
            "localP110Branches": len(local_p1_10),
            "ledgerUpdates": root_ledger_updates,
            "ledgerSeconds": derived["root_ledger_elapsed_seconds"],
            "reviewApprovals": derived["review_verdict_counts"].get("approved", 0),
            "reviewFindings": derived["review_verdict_counts"].get("rejected-or-findings", 0),
            "repeatedReviewLanes": repeated_review_lanes,
            "completedReviewLanes": completed_review_lanes,
            "netFiles": 100,
            "netInsertions": 9578,
            "netDeletions": 922,
            "specBaseLines": 65,
            "specFinalLines": 326,
        },
        "models": models,
        "tasks": tasks,
        "roles": roles,
        "agents": agent_rows,
        "commits": commits,
        "ci": ci_runs,
        "reviews": review_turns,
        "toolClasses": derived["tool_time_by_class"],
        "recommendations": [
            {"priority": 1, "title": "Add a spec-first architecture gate", "impact": "Highest", "text": "Before the first writer, assign one Sol/high lead to produce the end-to-end seam map, invariants, dependency DAG, slice boundaries, and explicit non-goals; have a second Sol/high reviewer approve that brief. Keep the lead available when discoveries change the design."},
            {"priority": 2, "title": "Integrate in cohesive layers", "impact": "High", "text": "Keep focused local tests and exact-SHA review, but group P1-10 into roughly three or four integration milestones: contract/quarantine; typed identity and persistence; command/workflow activation. Run full main CI once per milestone instead of once per small commit."},
            {"priority": 3, "title": "Separate isolation from publication", "impact": "High", "text": "Worktrees and local branches are useful. Remote-pushing every lane is not required for parallelism. Push only recovery-critical long lanes and milestone integration branches; preserve commits locally between reviews."},
            {"priority": 4, "title": "Reclassify emergent architecture immediately", "impact": "Medium", "text": "Luna/low mechanical agents performed the intended narrow work. When a Terra writer encounters a new contract or cross-cutting invariant, pause implementation and route the design decision to Sol/high rather than discovering architecture through repeated code review."},
            {"priority": 5, "title": "Coalesce ledger updates", "impact": "Medium", "text": "Update the ledger on phase transitions, ownership changes, blockers, frozen commits, and release gates. Avoid checkpoint-level edits for every message, poll, or test result. Preserve durability with fewer, denser entries."},
            {"priority": 6, "title": "Use PRs as milestone containers", "impact": "Situational", "text": "A PR per micro-branch would add overhead. One draft PR per top-level task, or one per cohesive milestone, can provide human visibility, a stable diff anchor, and branch-protection checks while replacing bespoke branch reconciliation."},
        ],
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
    if payload["summary"]["threads"] != len(payload["agents"]):
        raise ValueError("Agent row count mismatch")


HTML = r'''<!doctype html>
<html lang="en" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="kicktippai-report-title" content="P1 orchestration interim investigation">
  <title>P1 orchestration interim investigation</title>
  <style>
    :root{--bg:#0d1117;--panel:#151b23;--panel2:#1b2330;--text:#edf2f7;--muted:#9ca9b8;--line:#2b3645;--orange:#ff9b50;--blue:#63b3ed;--mint:#63d6b1;--red:#ff7a85;--violet:#bb9af7;--shadow:0 22px 60px rgba(0,0,0,.3)}
    :root[data-theme="light"]{--bg:#f5f1e9;--panel:#fffdf8;--panel2:#f6efe3;--text:#18202a;--muted:#637080;--line:#ddd3c3;--shadow:0 18px 48px rgba(80,55,30,.12)}
    *{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:radial-gradient(circle at 12% 0,rgba(255,155,80,.13),transparent 31%),radial-gradient(circle at 90% 10%,rgba(99,179,237,.12),transparent 28%),var(--bg);color:var(--text);font-family:Inter,"Segoe UI",sans-serif;line-height:1.55}a{color:var(--blue)}button,select,input{font:inherit}.wrap{width:min(1200px,calc(100% - 32px));margin:auto}.top{position:sticky;top:0;z-index:8;border-bottom:1px solid var(--line);background:color-mix(in srgb,var(--bg) 86%,transparent);backdrop-filter:blur(15px)}.top .wrap{display:flex;gap:18px;align-items:center;min-height:60px}.brand{font-weight:800;margin-right:auto}.nav{display:flex;gap:16px}.nav a{text-decoration:none;color:var(--muted);font-size:.88rem}.theme{border:1px solid var(--line);background:var(--panel);color:var(--text);border-radius:999px;padding:7px 11px;cursor:pointer}.hero{padding:76px 0 32px}.eyebrow{text-transform:uppercase;letter-spacing:.16em;color:var(--orange);font-size:.78rem;font-weight:800}.hero h1{font-size:clamp(2.6rem,7vw,5.8rem);line-height:.95;letter-spacing:-.055em;margin:15px 0 24px;max-width:1050px}.hero .lead{font-size:clamp(1.08rem,2vw,1.32rem);color:var(--muted);max-width:920px}.verdict{margin-top:30px;padding:22px 24px;border:1px solid color-mix(in srgb,var(--orange) 45%,var(--line));border-radius:20px;background:linear-gradient(120deg,rgba(255,155,80,.13),rgba(99,179,237,.07));font-size:1.06rem}.chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:20px}.chip{border:1px solid var(--line);background:var(--panel);border-radius:999px;padding:7px 10px;color:var(--muted);font-size:.82rem}.kpis{display:grid;grid-template-columns:repeat(6,1fr);gap:12px;margin:28px 0 72px}.kpi{background:var(--panel);border:1px solid var(--line);border-radius:17px;padding:18px;box-shadow:var(--shadow)}.kpi strong{display:block;font-size:clamp(1.5rem,3vw,2.25rem);line-height:1;margin-bottom:8px}.kpi span{color:var(--muted);font-size:.82rem}.section{padding:34px 0 52px;scroll-margin-top:70px}.section-head{display:grid;grid-template-columns:minmax(0,1fr) minmax(250px,.55fr);gap:25px;align-items:end;margin-bottom:24px}.section h2{font-size:clamp(2rem,4vw,3.3rem);letter-spacing:-.04em;line-height:1;margin:0}.section-head p{color:var(--muted);margin:0}.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.card{background:var(--panel);border:1px solid var(--line);border-radius:20px;padding:22px;box-shadow:var(--shadow)}.card h3{margin:0 0 8px;font-size:1.12rem}.card p{color:var(--muted);margin:0}.signal{display:inline-block;border-radius:999px;padding:4px 8px;font-size:.72rem;text-transform:uppercase;letter-spacing:.09em;font-weight:800;margin-bottom:14px}.dominant{color:var(--orange);background:rgba(255,155,80,.13)}.material{color:var(--blue);background:rgba(99,179,237,.13)}.avoidable{color:var(--red);background:rgba(255,122,133,.13)}.minor{color:var(--mint);background:rgba(99,214,177,.13)}.meter{height:11px;border-radius:999px;background:var(--panel2);overflow:hidden;margin:18px 0 8px}.meter i{display:block;height:100%;border-radius:inherit;background:linear-gradient(90deg,var(--blue),var(--mint))}.facts{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-top:16px}.fact{padding:12px;border-radius:14px;background:var(--panel2)}.fact strong{display:block;font-size:1.25rem}.fact span{font-size:.75rem;color:var(--muted)}.flow{display:grid;grid-template-columns:repeat(5,1fr);gap:9px;align-items:stretch}.step{position:relative;padding:16px;border:1px solid var(--line);border-radius:16px;background:var(--panel)}.step:not(:last-child):after{content:"→";position:absolute;right:-12px;top:42%;z-index:2;color:var(--orange);font-weight:900}.step strong{display:block;font-size:1.5rem}.step span{color:var(--muted);font-size:.8rem}.note{margin-top:14px;color:var(--muted);font-size:.86rem}.bar-list{display:grid;gap:13px}.bar-row{display:grid;grid-template-columns:170px 1fr 90px;gap:12px;align-items:center}.bar-row .track{height:13px;border-radius:999px;background:var(--panel2);overflow:hidden}.bar-row i{display:block;height:100%;border-radius:inherit}.bar-row small{color:var(--muted)}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:18px;background:var(--panel)}table{border-collapse:collapse;width:100%;min-width:760px}th,td{padding:12px 14px;border-bottom:1px solid var(--line);text-align:left;font-size:.84rem}th{position:sticky;top:0;background:var(--panel2);color:var(--muted);font-size:.72rem;text-transform:uppercase;letter-spacing:.08em}td.num{text-align:right;font-variant-numeric:tabular-nums}.mono{font-family:"Cascadia Code",Consolas,monospace}.filters{display:flex;gap:9px;margin-bottom:12px}.filters input,.filters select{border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:9px 11px}.filters input{flex:1}.review-score{display:grid;grid-template-columns:1fr 1fr;gap:10px}.score{padding:18px;border-radius:17px;background:var(--panel2)}.score strong{display:block;font-size:2.2rem}.score.bad strong{color:var(--red)}.score.good strong{color:var(--mint)}.spec-growth{display:flex;align-items:end;gap:14px;height:210px;padding:18px 0}.spec-column{flex:1;text-align:center}.spec-column i{display:block;width:72%;max-width:110px;margin:auto;border-radius:12px 12px 3px 3px;background:linear-gradient(var(--violet),var(--blue))}.spec-column strong{display:block;margin-top:8px;font-size:1.35rem}.rec{display:grid;grid-template-columns:46px 1fr 110px;gap:16px;padding:19px 0;border-bottom:1px solid var(--line)}.rec:last-child{border-bottom:0}.rec b{display:grid;place-items:center;width:40px;height:40px;border-radius:50%;background:var(--panel2);color:var(--orange)}.rec h3{margin:0 0 5px}.rec p{margin:0;color:var(--muted)}.impact{align-self:start;text-align:center;border-radius:999px;padding:5px 8px;background:rgba(99,214,177,.12);color:var(--mint);font-size:.72rem;font-weight:800;text-transform:uppercase}.method{font-size:.88rem;color:var(--muted)}footer{padding:34px 0 60px;border-top:1px solid var(--line);color:var(--muted);font-size:.82rem}.source-links{display:flex;flex-wrap:wrap;gap:12px;margin-top:15px}
    @media(max-width:950px){.kpis{grid-template-columns:repeat(3,1fr)}.grid3{grid-template-columns:1fr}.flow{grid-template-columns:1fr}.step:not(:last-child):after{content:"↓";right:auto;left:50%;top:auto;bottom:-18px}.section-head{grid-template-columns:1fr}.bar-row{grid-template-columns:130px 1fr 75px}.nav{display:none}}
    @media(max-width:640px){.wrap{width:min(100% - 20px,1200px)}.hero{padding-top:52px}.kpis{grid-template-columns:repeat(2,1fr)}.grid2{grid-template-columns:1fr}.facts{grid-template-columns:1fr}.rec{grid-template-columns:42px 1fr}.impact{grid-column:2;text-align:left;width:max-content}.filters{flex-direction:column}}
  </style>
</head>
<body>
  <header class="top"><div class="wrap"><div class="brand">KicktippAi · Codex</div><nav class="nav"><a href="#diagnosis">Diagnosis</a><a href="#cycles">Cycles</a><a href="#models">Models</a><a href="#changes">Changes</a><a href="#recommendations">Recommendations</a><a href="#method">Method</a></nav><button class="theme" id="theme" type="button">Theme</button></div></header>
  <main class="wrap">
    <section class="hero">
      <div class="eyebrow">Interim orchestration investigation · 30 Aug 2026</div>
      <h1>Real complexity,<br>amplified by micro-gates.</h1>
      <p class="lead">This snapshot examines the active <span class="mono">$orchestrate P1</span> run from its exact transcript family, Git history, worktree branches, review outcomes, tool timing, and GitHub Actions runs.</p>
      <div class="verdict"><strong>Verdict:</strong> the machine is a material contributor and P1-10 is genuinely large, but the avoidable slowdown is the combination of just-in-time specification, one branch/review/push/CI cycle per narrow slice, and a ledger updated at checkpoint granularity. Lower-tier allocation is mostly working as intended; the missing piece is a persistent high-tier architecture/specification lane.</div>
      <div class="chips"><span class="chip" id="snapshot"></span><span class="chip"><strong>Interim:</strong> parallel session still active</span><span class="chip" id="pause-chip"></span><span class="chip"><strong>Boundary:</strong> <span class="mono" id="boundary"></span></span></div>
    </section>
    <section class="kpis">
      <article class="kpi"><strong id="k-wall"></strong><span>observed wall span</span></article>
      <article class="kpi"><strong id="k-scope"></strong><span>net files changed</span></article>
      <article class="kpi"><strong id="k-workers"></strong><span>task-agent threads</span></article>
      <article class="kpi"><strong id="k-p110"></strong><span>P1-10 worker time</span></article>
      <article class="kpi"><strong id="k-reject"></strong><span>review turns with findings</span></article>
      <article class="kpi"><strong id="k-ci"></strong><span>main CI runs</span></article>
    </section>

    <section class="section" id="diagnosis">
      <div class="section-head"><h2>What is actually slow?</h2><p>The components overlap, so these are causal indicators rather than an additive wall-clock decomposition.</p></div>
      <div class="grid2">
        <article class="card"><span class="signal dominant">Dominant · genuine scope</span><h3>P1-10 became a cross-cutting platform change</h3><p id="complexity-text"></p><div class="facts"><div class="fact"><strong>3</strong><span>new ADRs</span></div><div class="fact"><strong id="net-lines"></strong><span>net changed lines</span></div><div class="fact"><strong id="spec-multiple"></strong><span>task-spec growth</span></div></div></article>
        <article class="card"><span class="signal material">Material · machine/tools</span><h3>Build and tool time is significant, not decisive</h3><p id="machine-text"></p><div class="meter"><i id="tool-meter"></i></div><div class="note">Tool share is an upper bound for “machine”: it also includes network, Git, file reads, and CI polling.</div></article>
        <article class="card"><span class="signal avoidable">Avoidable · workflow shape</span><h3>Isolation was converted into release ceremony</h3><p id="fragment-text"></p><div class="facts"><div class="fact"><strong id="branch-count"></strong><span>P1-10 local branches</span></div><div class="fact"><strong id="ci-minutes"></strong><span>serial CI compute</span></div><div class="fact"><strong id="ci-agents"></strong><span>CI-only agents</span></div></div></article>
        <article class="card"><span class="signal minor">Small wall cost · noisy control plane</span><h3>The ledger is cheap in seconds, expensive in attention</h3><p id="ledger-text"></p><div class="facts"><div class="fact"><strong id="ledger-count"></strong><span>completed ledger patches</span></div><div class="fact"><strong id="ledger-time"></strong><span>direct patch time</span></div><div class="fact"><strong id="compactions"></strong><span>root compactions</span></div></div></article>
      </div>
    </section>

    <section class="section" id="cycles">
      <div class="section-head"><h2>The micro-gate funnel</h2><p>Exact-SHA review caught real defects. The inefficiency is that the full release gate repeats after nearly every slice.</p></div>
      <div class="flow" id="funnel"></div>
      <div class="grid2" style="margin-top:16px">
        <article class="card"><h3>Review churn</h3><div class="review-score"><div class="score bad"><strong id="review-bad"></strong><span>completed review turns found issues or rejected</span></div><div class="score good"><strong id="review-good"></strong><span>completed approval turns</span></div></div><p class="note" id="review-note"></p></article>
        <article class="card"><h3>Specification moved under implementation</h3><div class="spec-growth"><div class="spec-column"><i id="spec-before"></i><strong id="spec-before-label"></strong><span>lines at kickoff</span></div><div class="spec-column"><i id="spec-after"></i><strong id="spec-after-label"></strong><span>lines at cutoff</span></div></div><p class="note">Five-fold growth is evidence of legitimate discovery, but also that later writers were implementing against a moving contract.</p></article>
      </div>
    </section>

    <section class="section" id="models">
      <div class="section-head"><h2>Model allocation: fixed, with one missing role</h2><p>All spawn calls explicitly selected model and effort with no-history forks—the P0 inheritance failure did not recur.</p></div>
      <div class="grid2">
        <article class="card"><h3>Observed subagent portfolio</h3><div class="bar-list" id="model-bars"></div></article>
        <article class="card"><h3>Assessment</h3><p><strong>Luna/low is not the bottleneck.</strong> Its work is mechanical CI/status reconciliation and it consumed a small fraction of worker tokens. Terra/high writers did need substantial review correction, but Sol/high contract and ADR writers also failed first review. The common factor is emergent cross-cutting design, not simply model tier.</p><p style="margin-top:12px"><strong>The missing role is a persistent lead specifier.</strong> The run had research and several ADR/audit agents, but design was produced just in time between implementation slices. A Sol/high architecture lane should own and refresh the end-to-end contract before and during the writer wave.</p><div class="source-links"><a href="https://learn.chatgpt.com/docs/agent-configuration/subagents">Official OpenAI subagent guidance</a></div></article>
      </div>
      <div class="table-wrap" style="margin-top:16px"><table><thead><tr><th>Model / effort</th><th class="num">Threads</th><th class="num">Turns</th><th class="num">Worker time</th><th class="num">Tokens</th><th>Observed role</th></tr></thead><tbody id="model-body"></tbody></table></div>
    </section>

    <section class="section" id="changes">
      <div class="section-head"><h2>What the time bought</h2><p>The output is not consistent with a small task stalled on a slow PC: it is a large reviewed implementation with high cumulative churn.</p></div>
      <div class="grid3" id="role-cards"></div>
      <div class="filters"><input id="agent-search" type="search" placeholder="Filter agent path"><select id="agent-role"><option value="all">All roles</option></select></div>
      <div class="table-wrap"><table><thead><tr><th>Agent</th><th>Role</th><th>Model</th><th class="num">Turns</th><th class="num">Worker time</th><th class="num">Tokens</th></tr></thead><tbody id="agent-body"></tbody></table></div>
      <div class="table-wrap" style="margin-top:16px"><table><thead><tr><th>Commit</th><th>Subject</th><th>Committed</th><th class="num">Files</th><th class="num">+</th><th class="num">−</th></tr></thead><tbody id="commit-body"></tbody></table></div>
    </section>

    <section class="section" id="recommendations">
      <div class="section-head"><h2>Recommended protocol changes</h2><p>Keep the safety properties—bounded ownership, independent review, exact targets—while reducing the frequency of release and state ceremonies.</p></div>
      <div class="card" id="recommendation-list"></div>
      <div class="card" style="margin-top:16px"><h3>Proposed P1 task shape</h3><div class="flow" style="margin-top:16px"><div class="step"><strong>1</strong><span>Sol/high end-to-end architecture brief</span></div><div class="step"><strong>2</strong><span>Independent spec review and frozen slice DAG</span></div><div class="step"><strong>3</strong><span>Up to two isolated writers with focused local gates</span></div><div class="step"><strong>4</strong><span>One layer-level exact-SHA review</span></div><div class="step"><strong>5</strong><span>One milestone push / PR / full CI</span></div></div><p class="note">PRs help when they replace bespoke reconciliation and provide a durable human review surface. They are not a speed improvement if created for every micro-branch.</p></div>
    </section>

    <section class="section" id="method">
      <div class="section-head"><h2>Method and raw evidence</h2><p>This is an interim snapshot, not a final accounting of the still-running P1 objective.</p></div>
      <div class="card method"><p>The root transcript family is defined recursively by <span class="mono">thread_spawn.parent_thread_id</span>. Complete prompts, reasoning, private payloads, and secrets are not copied. Normalized JSON/CSV records retain timestamps, hashes, bounded excerpts, model/effort, turns, tool timing, Git stats, branch refs, review verdicts, and CI run metadata. Worker durations overlap and are not timesheets. Tool elapsed time includes local execution, file/Git operations, network, and polling. The CI table was captured read-only with authenticated GitHub CLI.</p><div class="source-links"><a href="https://github.com/ehonda/KicktippAi/tree/main/docs/codex/p1-orchestration-interim-investigation">Extractor and raw data</a><a href="https://github.com/ehonda/KicktippAi/tree/main/session-analysis/p1-orchestration-interim">Published HTML source</a><a href="https://learn.chatgpt.com/docs/agent-configuration/subagents">Official OpenAI subagent guidance</a></div></div>
    </section>
  </main>
  <footer><div class="wrap"><span>P1 orchestration interim investigation · generated from normalized schema evidence</span></div></footer>
  <script id="report-data" type="application/json">__REPORT_DATA__</script>
  <script>
  (()=>{"use strict";const d=JSON.parse(document.getElementById("report-data").textContent);const $=s=>document.querySelector(s);const num=new Intl.NumberFormat("en-US");const compact=new Intl.NumberFormat("en-US",{notation:"compact",maximumFractionDigits:1});const esc=v=>String(v??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));const dur=s=>{const m=Math.round(Number(s)/60),h=Math.floor(m/60),r=m%60;return h?`${h}h ${r}m`:`${m}m`};const date=v=>new Intl.DateTimeFormat("en-GB",{day:"2-digit",month:"short",hour:"2-digit",minute:"2-digit",hour12:false,timeZone:"Europe/Berlin"}).format(new Date(v));
  $("#theme").onclick=()=>{document.documentElement.dataset.theme=document.documentElement.dataset.theme==="dark"?"light":"dark"};
  $("#snapshot").innerHTML=`<strong>Snapshot:</strong> ${esc(date(d.meta.end))}`;$("#pause-chip").innerHTML=`<strong>Authorization pause:</strong> ${dur(d.summary.authorizationPause)}`;$("#boundary").textContent=`${d.meta.base.slice(0,7)}..${d.meta.final.slice(0,7)}`;
  $("#k-wall").textContent=dur(d.summary.wall);$("#k-scope").textContent=num.format(d.summary.netFiles);$("#k-workers").textContent=num.format(d.summary.threads);$("#k-p110").textContent=dur(d.summary.p1_10Seconds);$("#k-reject").textContent=num.format(d.summary.reviewFindings);$("#k-ci").textContent=num.format(d.summary.ciRuns);
  $("#complexity-text").textContent=`The session changed ${num.format(d.summary.netFiles)} files (+${num.format(d.summary.netInsertions)} / −${num.format(d.summary.netDeletions)} net), introduced typed routing/storage/rules/manifest contracts, and still had a resolver lane in flight at cutoff.`;$("#net-lines").textContent=compact.format(d.summary.netInsertions+d.summary.netDeletions);$("#spec-multiple").textContent=`${(d.summary.specFinalLines/d.summary.specBaseLines).toFixed(1)}×`;
  $("#machine-text").textContent=`Measured subagent tool calls occupied ${dur(d.summary.toolSeconds)} in aggregate, ${Math.round(100*d.summary.toolShare)}% of worker-active time. ${num.format(d.summary.dotnetCalls)} dotnet cells alone accounted for ${dur(d.summary.dotnetSeconds)} before continuation polling. The wall span also contains a ${dur(d.summary.authorizationPause)} overnight no-agent interval ending with explicit push authorization; excluding it leaves about ${dur(d.summary.wall-d.summary.authorizationPause)}.`;$("#tool-meter").style.width=`${100*d.summary.toolShare}%`;
  $("#fragment-text").textContent=`P1-10 used ${num.format(d.summary.localP110Branches)} local task branches and ${num.format(d.summary.remoteP110Branches)} remote recovery refs. Main received ${num.format(d.summary.ciRuns)} CI-triggering pushes; ${num.format(d.summary.ciFailures)} failed and required a follow-up.`;$("#branch-count").textContent=num.format(d.summary.localP110Branches);$("#ci-minutes").textContent=dur(d.summary.ciSeconds);const ciRole=d.roles.find(x=>x.role==="CI / reconciliation");$("#ci-agents").textContent=num.format(ciRole?.threads||0);
  $("#ledger-text").textContent=`The durable state mechanism worked and made recovery auditable. But ${num.format(d.summary.ledgerUpdates)} completed ledger patches—about one every ${(d.summary.wall/d.summary.ledgerUpdates/60).toFixed(1)} minutes—show checkpoint-level interpretation of “material handoff.” Direct patch execution was only ${dur(d.summary.ledgerSeconds)}; the larger cost is root context and compaction pressure.`;$("#ledger-count").textContent=num.format(d.summary.ledgerUpdates);$("#ledger-time").textContent=dur(d.summary.ledgerSeconds);$("#compactions").textContent=num.format(d.summary.rootCompactions);
  const funnel=[[d.summary.localP110Branches,"local P1-10 branches"],[d.summary.reviewFindings+d.summary.reviewApprovals,"completed review turns"],[d.summary.reviewFindings,"turns with findings"],[d.summary.ciRuns,"main CI runs"],[d.commits.filter(c=>String(c.subject).toLowerCase().includes("schadensfresse")||new Date(c.committed_at)>=new Date("2026-08-30T07:56:00Z")).length,"P1-10-era commits"]];$("#funnel").innerHTML=funnel.map(([v,l])=>`<div class="step"><strong>${num.format(v)}</strong><span>${esc(l)}</span></div>`).join("");
  $("#review-bad").textContent=num.format(d.summary.reviewFindings);$("#review-good").textContent=num.format(d.summary.reviewApprovals);$("#review-note").textContent=`${d.summary.repeatedReviewLanes} of ${d.summary.completedReviewLanes} completed P1-10 review lanes required more than one review turn. The findings were substantive—compile failures, missing fail-closed paths, concurrency semantics, and contract contradictions—not style churn.`;$("#spec-before").style.height="40px";$("#spec-after").style.height="200px";$("#spec-before-label").textContent=num.format(d.summary.specBaseLines);$("#spec-after-label").textContent=num.format(d.summary.specFinalLines);
  const maxModel=Math.max(...d.models.map(x=>x.tokens));const colors={"gpt-5.6-sol":"var(--violet)","gpt-5.6-terra":"var(--blue)","gpt-5.6-luna":"var(--mint)"};$("#model-bars").innerHTML=d.models.map(x=>`<div class="bar-row"><small>${esc(x.model.replace("gpt-5.6-","") + "/" + x.effort)}</small><div class="track"><i style="width:${100*x.tokens/maxModel}%;background:${colors[x.model]||"var(--orange)"}"></i></div><strong>${compact.format(x.tokens)}</strong></div>`).join("");const observed=x=>x.model.includes("luna")?(x.effort==="low"?"Mechanical CI / exact-SHA":"Bounded lookup"):x.model.includes("terra")?(x.effort==="medium"?"Narrow deterministic fix":"Implementation writer"):"Review, architecture, difficult audit";$("#model-body").innerHTML=d.models.map(x=>`<tr><td class="mono">${esc(x.model)}/${esc(x.effort)}</td><td class="num">${num.format(x.threads)}</td><td class="num">${num.format(x.turns)}</td><td class="num">${dur(x.seconds)}</td><td class="num">${compact.format(x.tokens)}</td><td>${esc(observed(x))}</td></tr>`).join("");
  $("#role-cards").innerHTML=d.roles.slice(0,3).map(x=>`<article class="card"><span class="signal ${x.role.includes("Review")?"avoidable":x.role.includes("Implementation")?"material":"minor"}">${esc(x.role)}</span><h3>${num.format(x.threads)} threads · ${dur(x.seconds)}</h3><p>${compact.format(x.tokens)} tokens across ${num.format(x.turns)} turns.</p></article>`).join("");const roles=[...new Set(d.agents.map(x=>x.role))].sort();$("#agent-role").insertAdjacentHTML("beforeend",roles.map(x=>`<option>${esc(x)}</option>`).join(""));const renderAgents=()=>{const q=$("#agent-search").value.toLowerCase(),r=$("#agent-role").value;$("#agent-body").innerHTML=d.agents.filter(x=>(!q||x.path.toLowerCase().includes(q))&&(r==="all"||x.role===r)).slice(0,40).map(x=>`<tr><td class="mono">${esc(x.path)}</td><td>${esc(x.role)}</td><td class="mono">${esc(x.model)}</td><td class="num">${num.format(x.turns)}</td><td class="num">${dur(x.seconds)}</td><td class="num">${compact.format(x.tokens)}</td></tr>`).join("")};$("#agent-search").oninput=renderAgents;$("#agent-role").onchange=renderAgents;renderAgents();$("#commit-body").innerHTML=d.commits.slice().reverse().map(x=>`<tr><td class="mono"><a href="https://github.com/ehonda/KicktippAi/commit/${esc(x.sha)}">${esc(x.short_sha)}</a></td><td>${esc(x.subject)}</td><td>${esc(date(x.committed_at))}</td><td class="num">${num.format(x.files_changed)}</td><td class="num">${num.format(x.insertions)}</td><td class="num">${num.format(x.deletions)}</td></tr>`).join("");
  $("#recommendation-list").innerHTML=d.recommendations.map(x=>`<article class="rec"><b>${x.priority}</b><div><h3>${esc(x.title)}</h3><p>${esc(x.text)}</p></div><span class="impact">${esc(x.impact)}</span></article>`).join("");
  })();
  </script>
</body></html>'''


def main() -> None:
    parser = argparse.ArgumentParser()
    source_dir = pathlib.Path(__file__).parent / "data"
    parser.add_argument("--analysis", type=pathlib.Path, default=source_dir / "analysis.json")
    parser.add_argument("--derived", type=pathlib.Path, default=source_dir / "derived-metrics.json")
    parser.add_argument("--output", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[3] / "session-analysis" / "p1-orchestration-interim" / "index.html")
    args = parser.parse_args()
    analysis = json.loads(args.analysis.read_text(encoding="utf-8"))
    derived = json.loads(args.derived.read_text(encoding="utf-8"))
    payload = build_payload(analysis, derived, args.analysis.parent)
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    output = HTML.replace("__REPORT_DATA__", encoded)
    validate(output, payload)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8", newline="\n")
    print(f"Wrote {args.output} ({args.output.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
