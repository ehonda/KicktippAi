#!/usr/bin/env python3
"""Build the self-contained P1 context-refresh orchestration report."""

from __future__ import annotations

import argparse
import csv
from html.parser import HTMLParser
import json
import pathlib
from typing import Any

from verify_snapshot import verify_data_dir


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
    with path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def typed_rows(rows: list[dict[str, str]], integer_fields: set[str], float_fields: set[str]) -> list[dict[str, Any]]:
    converted: list[dict[str, Any]] = []
    for row in rows:
        item: dict[str, Any] = dict(row)
        for field in integer_fields:
            if field in item and item[field] != "":
                item[field] = int(item[field])
        for field in float_fields:
            if field in item and item[field] != "":
                item[field] = float(item[field])
        converted.append(item)
    return converted


def build_payload(data_dir: pathlib.Path) -> dict[str, Any]:
    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    derived = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    curated = json.loads((data_dir / "curated-findings.json").read_text(encoding="utf-8"))
    compactions = typed_rows(
        read_csv(data_dir / "compactions.csv"),
        {"replacement_items", "replacement_json_chars", "hook_context_chars", "custom_tool_calls", "tool_output_json_chars", "distinct_context_paths", "resume_input_tokens", "model_context_window"},
        {"recovery_seconds", "resume_context_percent"},
    )
    roles = typed_rows(
        read_csv(data_dir / "role-usage.csv"),
        {"threads", "turns", "input_tokens", "cached_input_tokens", "cache_write_input_tokens", "output_tokens", "total_tokens"},
        {"active_seconds", "api_cost_equivalent_usd"},
    )
    resources = typed_rows(
        read_csv(data_dir / "resource-samples.csv"),
        set(),
        {"available_memory_gib"},
    )
    return {
        "summary": analysis["summary"],
        "metrics": derived,
        "curated": curated,
        "compactions": compactions,
        "roles": roles,
        "resources": resources,
    }


def validate(output: str, payload: dict[str, Any], data_dir: pathlib.Path) -> None:
    verify_data_dir(data_dir)
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
    if len(payload["compactions"]) != payload["metrics"]["recovery"]["compactions"]:
        raise ValueError("Compaction row count mismatch")


HTML = r'''<!doctype html>
<html lang="en" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="kicktippai-report-title" content="P1 context-refresh orchestration">
  <title>P1 context-refresh orchestration</title>
  <style>
    :root{--bg:#08101a;--panel:#101c29;--panel2:#162536;--text:#eff6f4;--muted:#9badab;--line:#294052;--mint:#6fe0bb;--cyan:#61c9e8;--amber:#ffc369;--red:#ff7f87;--violet:#b4a0ff;--shadow:0 24px 74px rgba(0,0,0,.3)}
    :root[data-theme="light"]{--bg:#f2eee6;--panel:#fffdf9;--panel2:#f1ebe1;--text:#14212a;--muted:#657477;--line:#d8d0c4;--shadow:0 20px 55px rgba(57,46,33,.12)}
    *{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;color:var(--text);font-family:Inter,"Segoe UI",sans-serif;line-height:1.58;background:radial-gradient(circle at 10% 0,rgba(97,201,232,.13),transparent 30%),radial-gradient(circle at 92% 8%,rgba(180,160,255,.12),transparent 28%),var(--bg)}a{color:var(--cyan)}button{font:inherit}.wrap{width:min(1220px,calc(100% - 34px));margin:auto}.top{position:sticky;top:0;z-index:8;border-bottom:1px solid var(--line);background:color-mix(in srgb,var(--bg) 88%,transparent);backdrop-filter:blur(15px)}.top .wrap{display:flex;align-items:center;gap:18px;min-height:60px}.brand{font-weight:900;margin-right:auto}.nav{display:flex;gap:15px}.nav a{color:var(--muted);font-size:.82rem;text-decoration:none}.theme{border:1px solid var(--line);background:var(--panel);color:var(--text);border-radius:999px;padding:7px 12px;cursor:pointer}.hero{padding:70px 0 28px}.eyebrow{color:var(--cyan);font-size:.75rem;font-weight:900;letter-spacing:.16em;text-transform:uppercase}.hero h1{font-size:clamp(3rem,7vw,6.2rem);line-height:.92;letter-spacing:-.065em;max-width:1060px;margin:15px 0 24px}.hero .lead{font-size:clamp(1.08rem,2vw,1.32rem);color:var(--muted);max-width:980px}.verdict{display:grid;grid-template-columns:150px 1fr;gap:20px;margin-top:30px;padding:23px;border:1px solid color-mix(in srgb,var(--amber) 45%,var(--line));border-radius:21px;background:linear-gradient(115deg,rgba(255,195,105,.12),rgba(97,201,232,.07))}.verdict b{color:var(--amber);font-size:1.35rem}.chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:18px}.chip{padding:7px 10px;border:1px solid var(--line);border-radius:999px;background:var(--panel);color:var(--muted);font-size:.79rem}.mono{font-family:"Cascadia Code",Consolas,monospace}.kpis{display:grid;grid-template-columns:repeat(6,1fr);gap:11px;margin:26px 0 64px}.kpi{padding:17px;border:1px solid var(--line);border-radius:17px;background:var(--panel);box-shadow:var(--shadow)}.kpi strong{display:block;font-size:clamp(1.35rem,3vw,2.1rem);line-height:1;margin-bottom:8px}.kpi span{color:var(--muted);font-size:.79rem}.section{padding:35px 0 50px;scroll-margin-top:72px}.section-head{display:grid;grid-template-columns:minmax(0,1fr) minmax(280px,.6fr);gap:28px;align-items:end;margin-bottom:23px}.section h2{font-size:clamp(2rem,4vw,3.25rem);line-height:1;letter-spacing:-.045em;margin:0}.section-head p{margin:0;color:var(--muted)}.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.card{padding:22px;border:1px solid var(--line);border-radius:20px;background:var(--panel);box-shadow:var(--shadow)}.card h3{margin:0 0 8px}.card p{margin:0;color:var(--muted)}.card p+p{margin-top:10px}.signal{display:inline-flex;padding:4px 9px;border-radius:999px;font-size:.69rem;font-weight:900;letter-spacing:.09em;text-transform:uppercase;margin-bottom:12px}.good{color:var(--mint);background:rgba(111,224,187,.12)}.mixed,.pilot{color:var(--amber);background:rgba(255,195,105,.12)}.poor{color:var(--red);background:rgba(255,127,135,.12)}.big{font-size:clamp(2.1rem,5vw,4rem);font-weight:900;line-height:1;letter-spacing:-.05em}.facts{display:grid;grid-template-columns:repeat(3,1fr);gap:9px;margin-top:17px}.fact{padding:12px;border-radius:13px;background:var(--panel2)}.fact strong{display:block;font-size:1.18rem}.fact span{font-size:.73rem;color:var(--muted)}.callout{margin-top:16px;padding:13px 0 13px 16px;border-left:4px solid var(--amber);color:var(--muted)}.timeline{display:grid;gap:0}.event{display:grid;grid-template-columns:95px 20px 1fr;gap:13px;min-height:84px}.event time{font-weight:900;color:var(--cyan);font-variant-numeric:tabular-nums}.rail{position:relative}.rail:before{content:"";position:absolute;left:9px;top:0;bottom:0;width:2px;background:var(--line)}.rail:after{content:"";position:absolute;left:4px;top:7px;width:12px;height:12px;border-radius:50%;background:var(--cyan);box-shadow:0 0 0 4px var(--panel)}.event.stop .rail:after,.event.loop .rail:after{background:var(--red)}.event.gate .rail:after,.event.owner .rail:after{background:var(--amber)}.event.recovery .rail:after{background:var(--violet)}.event h3{font-size:1rem;margin:0}.event p{margin:3px 0 0;color:var(--muted);font-size:.84rem}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:18px;background:var(--panel)}table{border-collapse:collapse;width:100%;min-width:740px}th,td{padding:12px 14px;border-bottom:1px solid var(--line);text-align:left;font-size:.81rem}th{background:var(--panel2);color:var(--muted);font-size:.68rem;text-transform:uppercase;letter-spacing:.08em}td.num{text-align:right;font-variant-numeric:tabular-nums}.bar-list{display:grid;gap:12px}.bar-row{display:grid;grid-template-columns:180px 1fr 92px;gap:12px;align-items:center}.bar-row small{color:var(--muted)}.track{height:13px;border-radius:999px;background:var(--panel2);overflow:hidden}.track i{display:block;height:100%;border-radius:inherit;background:var(--cyan)}.scorecard{display:grid;gap:11px}.finding{display:grid;grid-template-columns:260px 105px 1fr;gap:18px;padding:18px;border:1px solid var(--line);border-radius:17px;background:var(--panel)}.finding h3{font-size:1rem;margin:0}.finding ul{margin:0;padding-left:18px;color:var(--muted);font-size:.84rem}.finding li+li{margin-top:5px}.badge{width:max-content;padding:4px 8px;border-radius:999px;font-size:.68rem;font-weight:900;text-transform:uppercase}.recommendations{counter-reset:rec}.rec{display:grid;grid-template-columns:45px 1fr;gap:13px;padding:18px 0;border-bottom:1px solid var(--line)}.rec:last-child{border-bottom:0}.rec:before{counter-increment:rec;content:counter(rec);display:grid;place-items:center;width:36px;height:36px;border-radius:50%;background:var(--panel2);color:var(--cyan);font-weight:900}.rec h3{margin:0 0 5px}.rec p{margin:0;color:var(--muted)}details{margin-top:14px;border:1px solid var(--line);border-radius:15px;background:var(--panel2)}summary{cursor:pointer;padding:12px 14px;font-weight:800}details>div{padding:0 14px 14px;color:var(--muted);font-size:.86rem}.method{color:var(--muted);font-size:.87rem}.links{display:flex;flex-wrap:wrap;gap:13px;margin-top:14px}footer{padding:34px 0 56px;border-top:1px solid var(--line);color:var(--muted);font-size:.8rem}
    @media(max-width:1000px){.kpis{grid-template-columns:repeat(3,1fr)}.grid3{grid-template-columns:1fr}.section-head{grid-template-columns:1fr}.finding{grid-template-columns:200px 100px 1fr}.nav{display:none}}
    @media(max-width:700px){.wrap{width:min(100% - 20px,1220px)}.hero{padding-top:48px}.kpis{grid-template-columns:repeat(2,1fr)}.grid2{grid-template-columns:1fr}.verdict{grid-template-columns:1fr}.facts{grid-template-columns:1fr}.finding{grid-template-columns:1fr}.bar-row{grid-template-columns:125px 1fr 72px}.event{grid-template-columns:72px 18px 1fr}}
  </style>
</head>
<body>
  <header class="top"><div class="wrap"><div class="brand">KicktippAi · Codex</div><nav class="nav"><a href="#startup">Startup</a><a href="#progress">Progress</a><a href="#models">Roles</a><a href="#recovery">Recovery</a><a href="#memory">Memory</a><a href="#changes">Next</a><a href="#method">Method</a></nav><button class="theme" id="theme" type="button">Theme</button></div></header>
  <main class="wrap">
    <section class="hero">
      <div class="eyebrow">P1-04 / P1-05 · in-flight analysis · 6 Sep 2026</div>
      <h1>Recovery worked.<br>The run still serialized itself.</h1>
      <p class="lead">A frozen analysis of the first 19h04m of the current orchestration session. The capsule survived every compaction, but full rereads consumed most of the recovered context. After a preventable 5h30m startup stop, two nominally independent tasks converged on one oversized common-runtime gate.</p>
      <div class="verdict"><b>Verdict</b><div><strong>Safety mechanisms held; execution shape did not.</strong> At the cutoff, three planning commits and eighteen local common-runtime commits existed, but neither task-specific implementation lane had started and neither task was complete. The best next change is not more agents: it is a smaller frozen graph, hot-path recovery, milestone-sized common work, and a clean second worktree.</div></div>
      <div class="chips"><span class="chip"><strong>Snapshot:</strong> 22:21:56 CEST</span><span class="chip"><strong>Run:</strong> <span class="mono">01a07449…</span></span><span class="chip"><strong>Status:</strong> active at cutoff</span><span class="chip"><strong>Boundary:</strong> <span class="mono">a1e333f..c99e163</span></span></div>
    </section>
    <section class="kpis">
      <article class="kpi"><strong>5h 30m</strong><span>preventable owner stall</span></article>
      <article class="kpi"><strong>0 / 2</strong><span>P1 tasks complete</span></article>
      <article class="kpi"><strong>17 / 17</strong><span>common reviews blocked</span></article>
      <article class="kpi"><strong>64.6%</strong><span>context used after 22:00 recovery</span></article>
      <article class="kpi"><strong>67 KiB</strong><span>frozen preview.md</span></article>
      <article class="kpi"><strong>13 / 45</strong><span>heavy samples denied</span></article>
    </section>

    <section class="section" id="startup">
      <div class="section-head"><h2>The startup failure was a graph error</h2><p>The root did run the initial audits in parallel. It then converted two resolvable questions into a terminal owner gate.</p></div>
      <div class="grid3">
        <article class="card"><span class="signal mixed">Legitimate friction</span><div class="big">3m 30s</div><h3>Hook-trust confirmation</h3><p>Project hooks require the user to review and trust the exact definition. Asking was defensible, but the kickoff had no positive trust marker, so every fresh run must rediscover it.</p></article>
        <article class="card"><span class="signal poor">Preventable</span><div class="big">5h 30m</div><h3>Owner stall</h3><p>The root stopped at 03:47 after declaring P1-04 evidence unavailable. The owner correctly replied that the evidence could be acquired by the agents.</p></article>
        <article class="card"><span class="signal poor">Preventable</span><div class="big">P1-05</div><h3>False dependency</h3><p>P1-05 was initially sequenced behind P1-04 even though its evidence lane was independent. Once corrected, the two evidence agents overlapped immediately.</p></article>
      </div>
      <div class="callout">Improve startup evidence, not trust policy: a trusted <span class="mono">UserPromptSubmit</span> hook can inject the current hook-definition hash when <span class="mono">$orchestrate</span> starts. If trust is absent or the definition changed, the marker will be absent and the root can still fail closed. See the <a href="https://learn.chatgpt.com/docs/hooks">official Codex hooks documentation</a>.</div>
    </section>

    <section class="section" id="progress">
      <div class="section-head"><h2>Fast evidence, slow acceptance</h2><p>The session moved through discovery and plan integration in roughly two hours after resume, then spent nearly eleven hours on a single common-runtime correction loop.</p></div>
      <div class="grid2">
        <article class="card"><div class="timeline" id="timeline"></div></article>
        <div>
          <article class="card"><span class="signal poor">Serial megagate</span><div class="big">10h 54m</div><h3>Common runtime loop at cutoff</h3><p>Eighteen commits changed 20 files by +6,121/-5 lines. Seventeen independent reviews blocked. The review tags repeatedly touched receipts/provenance, freshness/health, persistence/schema, replay/recovery, and bundle integrity.</p><div class="facts"><div class="fact"><strong>230.1M</strong><span>writer + review tokens</span></div><div class="fact"><strong>77.4%</strong><span>of subagent tokens</span></div><div class="fact"><strong>0</strong><span>thread-limit errors</span></div></div></article>
          <article class="card" style="margin-top:14px"><h3>Why independent lanes still blocked one another</h3><p>The corrected graph allowed P1-04 and P1-05 evidence to overlap, but implementation remained behind acceptance of the whole common surface. Separately, the common worktree plus a dirty retained P1-10 worktree occupied the 2/2 writer budget. Agent capacity was ample: concurrency peaked at four, while two-plus occupancy was only 5.23%.</p><details><summary>Recurring review areas</summary><div><div class="bar-list" id="review-bars"></div><p style="margin-top:12px">These are overlapping keyword tags, not independent defect counts. A review can—and usually did—appear in several categories.</p></div></details></article>
        </div>
      </div>
    </section>

    <section class="section" id="models">
      <div class="section-head"><h2>Use Astra as an architecture experiment</h2><p>Design work was meaningful but not the dominant cost. Implementation correction churn was. A narrow model pilot gives a useful signal without weakening independent review.</p></div>
      <div class="grid2">
        <article class="card"><span class="signal pilot">Recommended pilot</span><h3>Astra/high architecture lead</h3><p>Swap Astra/high into one phase-wide architecture-lead role. Keep a different Sol/xhigh agent as specification reviewer. Measure first-pass spec acceptance, downstream review rounds, wall time, tokens, and list-price equivalent.</p><div class="facts"><div class="fact"><strong>2 / 3</strong><span>architecture threads / turns</span></div><div class="fact"><strong>9.80M</strong><span>architecture tokens</span></div><div class="fact"><strong>+$10.35</strong><span>same-token Astra premium</span></div></div><div class="callout">Do not switch the root or final reviewer yet. Official guidance describes Astra as the most capable model for complex work, but also notes that it may ask more clarifying questions and is especially sensitive to instructions and skills. See the <a href="https://developers.openai.com/api/docs/guides/latest-model">model selection guide</a>.</div></article>
        <article class="card"><h3>Where agent usage actually went</h3><div class="bar-list" id="role-bars"></div><details><summary>Role table and equivalent cost</summary><div class="table-wrap"><table><thead><tr><th>Role</th><th class="num">Threads</th><th class="num">Turns</th><th class="num">Worker time</th><th class="num">Tokens</th><th class="num">API eq.</th></tr></thead><tbody id="role-body"></tbody></table></div></details></article>
      </div>
      <div class="callout">The 12 design/planning threads used 43.76M tokens and $29.20 equivalent. Pricing the same token mix entirely as Astra would be $76.52; that is not a forecast because model choice can change token use. Rates: <a href="https://developers.openai.com/api/docs/models/gpt-6-astra">Astra</a> and <a href="https://developers.openai.com/api/docs/models/gpt-5.6-sol">Sol</a>.</div>
    </section>

    <section class="section" id="recovery">
      <div class="section-head"><h2>The hook succeeded; recovery over-read</h2><p>All five compactions injected the validated capsule. The expensive behavior happened afterward, under the full recovery preflight.</p></div>
      <div class="grid3">
        <article class="card"><span class="signal good">Reliable</span><div class="big">5 / 5</div><h3>Validated injections</h3><p>No compaction lost run identity or failed capsule validation.</p></article>
        <article class="card"><span class="signal mixed">Expensive</span><div class="big">33m 41s</div><h3>Total recovery</h3><p>Median recovery took 7m16s and the five recoveries consumed 4.13% of pause-adjusted wall time.</p></article>
        <article class="card"><span class="signal poor">Context-heavy</span><div class="big">58.0%</div><h3>Median resume context</h3><p>The first post-recovery coordination action occurred with more than half the context already occupied.</p></article>
      </div>
      <div class="table-wrap" style="margin-top:14px"><table><thead><tr><th>Compacted (Berlin)</th><th class="num">Recovery</th><th class="num">Tool calls</th><th class="num">Paths</th><th class="num">Tool output</th><th class="num">Capsule injection</th><th class="num">Context at resume</th></tr></thead><tbody id="compaction-body"></tbody></table></div>
      <div class="grid2" style="margin-top:14px">
        <article class="card"><h3>What happened at 22:00 Berlin</h3><p>The root compacted at 22:00:49 and did not resume coordination until 22:09:05. It made 40 custom tool calls, read 25 normalized paths, emitted 559,510 JSON characters of tool output, and reached 166,933 / 258,400 input tokens—64.6%.</p><p>The validated hook injection was only 6,043 characters. Reads included the root and nested instructions, orchestration and onboarding skills, preview and capsule, three ADRs, design and execution packets, resource scripts, and unrelated P0/P1 task records. Several large files were then read again in chunks.</p></article>
        <article class="card"><h3>Hot recovery should be boring</h3><p>When capsule checksum, session identity, instruction-packet hash, and active-contract SHA all match, one deterministic helper should return live agents, Git/worktrees, resource admission, and the heavy lease in compact JSON. The root should read only the active lane contract if needed and then resume scheduling.</p><p>Use the current full reread only as cold recovery for corrupt/missing state, changed instructions, material re-freeze, or uncertain ownership. A <span class="mono">PostCompact</span> hook would not fix this; <span class="mono">SessionStart:compact</span> already injected the correct state. The redundant work followed the injection.</p></article>
      </div>
    </section>

    <section class="section" id="memory">
      <div class="section-head"><h2>1.10 GiB is a policy line, not a measured optimum</h2><p>The gate is deterministic and conservative. This snapshot does not show that it materially limited throughput, nor that a lower floor is safe.</p></div>
      <div class="grid3">
        <article class="card"><div class="big">28.9%</div><h3>Samples denied</h3><p>13 of 45 heavy-admission snapshots were below 1.10 GiB.</p></article>
        <article class="card"><div class="big">55s</div><h3>Closed causal delay</h3><p>Only one completed denial can be tied directly to ready heavy work. Most denial intervals contain unrelated activity.</p></article>
        <article class="card"><div class="big">1.11</div><h3>Minimum admitted GiB</h3><p>The maximum denied value was 1.09 GiB. This proves exact threshold enforcement, not threshold quality.</p></article>
      </div>
      <article class="card" style="margin-top:14px"><h3>Calibrate from operation outcomes</h3><p>Keep 1.10 GiB provisionally. For every heavy lease, record operation family, start/min/post available memory, duration, exit/outcome, paging or OOM symptoms, and actual queue delay. After a meaningful sample, estimate failure and slowdown risk by starting-memory band. Lower by 0.10 GiB only when the next band has clean outcomes; raise the floor if paging, instability, or severe slowdown appears.</p><div class="callout">Do not sum gaps between denied and later admitted samples. In this run those gaps include the five-and-a-half-hour owner absence, ordinary reviews, and post-job snapshots.</div></article>
    </section>

    <section class="section" id="assessment">
      <div class="section-head"><h2>Evidence-backed assessment</h2><p>Transcript facts and analytical judgments remain separate in the checked-in data.</p></div>
      <div class="scorecard" id="findings"></div>
    </section>

    <section class="section" id="changes">
      <div class="section-head"><h2>Changes for the next orchestration run</h2><p>Shorten recovery and unblock accepted work earlier; avoid weakening gates merely to increase concurrency.</p></div>
      <article class="card recommendations" id="recommendations"></article>
    </section>

    <section class="section" id="method">
      <div class="section-head"><h2>Method and limits</h2><p>This is a frozen, in-flight investigation—not a completion report or controlled model benchmark.</p></div>
      <article class="card method"><p>The task family is defined by recursive <span class="mono">thread_spawn.parent_thread_id</span> ancestry to run <span class="mono">01a07449-de77-7ae0-ac4a-8f5330c43121</span>. The cutoff is 6 September 2026 at 22:21:56.196 Europe/Berlin. Exact Git boundaries are <span class="mono">a1e333f..c99e163</span>; the frozen common-runtime tip is <span class="mono">6cce46a</span>. The active session continued after this boundary, so later fixes and outcomes are intentionally excluded.</p><p>Recovery is measured from each root <span class="mono">compacted</span> event to the first subsequent coordination call. Resume context uses the last token-count event before that call. Path counts are normalized references found in recovery tool inputs; output size is serialized JSON character count, not bytes read from disk. Preview churn uses transcript file-change events through the cutoff; the first file creation has no full unified diff, so cumulative additions are conservative.</p><p>Agent durations overlap. Keyword review tags overlap and do not count independent defects. Memory samples are observations, not continuous telemetry; only causally matched ready-work intervals are described as delay. API list-price equivalents are reproducible comparisons, not Codex subscription charges. Astra same-token figures are counterfactual pricing, not performance forecasts.</p><div class="links"><a href="https://github.com/ehonda/KicktippAi/tree/main/docs/codex/p1-context-refresh-orchestration-investigation">Extractor and normalized data</a><a href="https://github.com/ehonda/KicktippAi/tree/main/session-analysis/p1-context-refresh">Published HTML source</a><a href="../p1-orchestration-follow-up/index.html">Previous orchestration analysis</a><a href="https://developers.openai.com/api/docs/guides/compaction">Official compaction guide</a></div></article>
    </section>
  </main>
  <footer><div class="wrap">P1 context-refresh orchestration · frozen in-flight native-transcript analysis</div></footer>
  <script id="report-data" type="application/json">__REPORT_DATA__</script>
  <script>
  (()=>{"use strict";const d=JSON.parse(document.getElementById("report-data").textContent);const $=s=>document.querySelector(s);const esc=v=>String(v??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));const compact=new Intl.NumberFormat("en-US",{notation:"compact",maximumFractionDigits:2});const money=v=>new Intl.NumberFormat("en-US",{style:"currency",currency:"USD",maximumFractionDigits:2}).format(v);const dur=s=>{let m=Math.round(Number(s)/60),h=Math.floor(m/60);return h?`${h}h ${m%60}m`:`${m}m`};const time=v=>new Intl.DateTimeFormat("en-GB",{hour:"2-digit",minute:"2-digit",second:"2-digit",hour12:false,timeZone:"Europe/Berlin"}).format(new Date(v));
  $("#theme").onclick=()=>{document.documentElement.dataset.theme=document.documentElement.dataset.theme==="dark"?"light":"dark"};
  $("#timeline").innerHTML=d.curated.timeline.map(x=>`<article class="event ${esc(x.kind)}"><time>${esc(x.time)}</time><span class="rail"></span><div><h3>${esc(x.title)}</h3><p>${esc(x.detail)}</p></div></article>`).join("");
  const tags=d.metrics.delivery.review_tag_counts,maxTag=Math.max(...Object.values(tags));$("#review-bars").innerHTML=Object.entries(tags).sort((a,b)=>b[1]-a[1]).map(([k,v])=>`<div class="bar-row"><small>${esc(k.replaceAll("-"," / "))}</small><div class="track"><i style="width:${100*v/maxTag}%;background:var(--red)"></i></div><strong>${v} / 17</strong></div>`).join("");
  const roles=[...d.roles].sort((a,b)=>b.total_tokens-a.total_tokens),maxRole=roles[0].total_tokens;$("#role-bars").innerHTML=roles.map(x=>`<div class="bar-row"><small>${esc(x.role)}</small><div class="track"><i style="width:${100*x.total_tokens/maxRole}%"></i></div><strong>${compact.format(x.total_tokens)}</strong></div>`).join("");$("#role-body").innerHTML=roles.map(x=>`<tr><td>${esc(x.role)}</td><td class="num">${x.threads}</td><td class="num">${x.turns}</td><td class="num">${dur(x.active_seconds)}</td><td class="num">${compact.format(x.total_tokens)}</td><td class="num">${money(x.api_cost_equivalent_usd)}</td></tr>`).join("");
  $("#compaction-body").innerHTML=d.compactions.map(x=>`<tr><td>${time(x.compacted_at)}</td><td class="num">${dur(x.recovery_seconds)}</td><td class="num">${x.custom_tool_calls}</td><td class="num">${x.distinct_context_paths}</td><td class="num">${compact.format(x.tool_output_json_chars)} chars</td><td class="num">${compact.format(x.hook_context_chars)} chars</td><td class="num"><strong>${x.resume_context_percent.toFixed(2)}%</strong></td></tr>`).join("");
  $("#findings").innerHTML=d.curated.findings.map(x=>`<article class="finding"><h3>${esc(x.id.replaceAll("-"," "))}</h3><span class="badge ${esc(x.assessment)}">${esc(x.assessment)}</span><ul>${x.evidence.map(y=>`<li>${esc(y)}</li>`).join("")}</ul></article>`).join("");
  $("#recommendations").innerHTML=d.curated.recommendations.map(x=>`<article class="rec"><div><h3>${esc(x.title)}</h3><p>${esc(x.detail)}</p></div></article>`).join("");
  })();
  </script>
</body></html>'''


def main() -> None:
    source_dir = pathlib.Path(__file__).parent / "data"
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=pathlib.Path, default=source_dir)
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[3] / "session-analysis" / "p1-context-refresh" / "index.html",
    )
    args = parser.parse_args()
    payload = build_payload(args.data_dir)
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    output = HTML.replace("__REPORT_DATA__", encoded)
    validate(output, payload, args.data_dir)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8", newline="\n")
    print(f"Wrote {args.output} ({args.output.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
