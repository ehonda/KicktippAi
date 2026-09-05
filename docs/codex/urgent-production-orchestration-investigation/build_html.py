#!/usr/bin/env python3
"""Build the self-contained urgent-production orchestration report."""

from __future__ import annotations

import argparse
import csv
from html import escape
from html.parser import HTMLParser
import json
import pathlib

from verify_snapshot import verify_data_dir


class Inspector(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.duplicates: set[str] = set()
        self.fragments: set[str] = set()
        self.external_assets: list[str] = []

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
            self.external_assets.append(values["src"] or "")
        if tag == "link" and "stylesheet" in (values.get("rel") or "").split():
            self.external_assets.append(href or "")


def read_csv(path: pathlib.Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def money(value: float) -> str:
    return f"${value:,.2f}"


def hours(seconds: float) -> str:
    total_minutes = round(seconds / 60)
    return f"{total_minutes // 60}h {total_minutes % 60:02d}m"


def scorecard_html(rows: list[dict[str, str]]) -> str:
    labels = {"pass": "Pass", "mixed": "Mixed", "regression": "Regression", "qualified pass": "Qualified pass"}
    return "\n".join(
        f"""
        <article class="score-card">
          <div><span class="signal {escape(row['verdict'].replace(' ', '-'))}">{labels[row['verdict']]}</span><h3>{escape(row['change'])}</h3></div>
          <p>{escape(row['evidence'])}</p>
          <p class="interpretation">{escape(row['interpretation'])}</p>
        </article>"""
        for row in rows
    )


def timeline_html(rows: list[dict[str, str]]) -> str:
    return "\n".join(
        f"""
        <li><time>{escape(row['time'])}</time><div><strong>{escape(row['title'])}</strong><p>{escape(row['detail'])}</p></div></li>"""
        for row in rows
    )


def model_rows_html(rows: list[dict[str, str]]) -> str:
    priced = [row for row in rows if row["kind"] != "guardian" and row["api_cost_equivalent_usd"]]
    maximum = max(float(row["api_cost_equivalent_usd"]) for row in priced)
    return "\n".join(
        f"""
        <tr>
          <td><strong>{escape(row['agent_path'])}</strong><small>{escape(row['model'])} · {escape(row['reasoning_effort'])}</small></td>
          <td class="num">{int(row['responses']):,}</td>
          <td class="num">{int(row['total_tokens']) / 1_000_000:.1f}M</td>
          <td><div class="mini-bar"><i style="width:{float(row['api_cost_equivalent_usd']) / maximum * 100:.2f}%"></i></div><span class="cost-label">{money(float(row['api_cost_equivalent_usd']))}</span></td>
        </tr>"""
        for row in priced
    )


def ci_rows_html(rows: list[dict[str, str]]) -> str:
    return "\n".join(
        f"""
        <tr><td><a href="{escape(row['url'])}">{escape(row['head_sha'][:7])}</a></td><td>{escape(row['title'])}</td><td><span class="ci {escape(row['conclusion'])}">{escape(row['conclusion'])}</span></td></tr>"""
        for row in rows
    )


def build(data_dir: pathlib.Path, output: pathlib.Path) -> None:
    verify_data_dir(data_dir)
    analysis = json.loads((data_dir / "analysis.json").read_text(encoding="utf-8"))
    metrics = json.loads((data_dir / "derived-metrics.json").read_text(encoding="utf-8"))
    facts = json.loads((data_dir / "session-facts.json").read_text(encoding="utf-8"))
    models = read_csv(data_dir / "model-usage.csv")
    ci_runs = read_csv(data_dir / "ci-runs.csv")

    cost = metrics["cost"]
    workflow = metrics["workflow"]
    delivery = metrics["delivery"]
    outcome = facts["production_outcome"]
    root_usage = analysis["threads"][0]["usage"]
    uncached = root_usage["input_tokens"] - root_usage["cached_input_tokens"] - root_usage["cache_write_input_tokens"]
    astra_parts = {
        "uncached": uncached * 10 / 1_000_000,
        "cached": root_usage["cached_input_tokens"] / 1_000_000,
        "output": root_usage["output_tokens"] * 50 / 1_000_000,
    }
    task_agent_cost = cost["priced_agent_portfolio_usd"] - cost["root_astra_medium_usd"]
    previous_cost_change = (cost["actual_session_root_cost_ratio"] - 1) * 100
    concurrency_change = (workflow["average_concurrency_while_active"] / workflow["previous_average_concurrency_while_active"] - 1) * 100
    two_plus_change = (workflow["two_plus_share"] / workflow["previous_two_plus_share"] - 1) * 100
    wait_change = (workflow["waits_per_effective_hour"] / workflow["previous_waits_per_effective_hour"] - 1) * 100
    ledger_change = (workflow["root_ledger_patches_per_effective_hour"] / workflow["previous_ledger_patches_per_effective_hour"] - 1) * 100

    cost_data = json.dumps(
        [
            {"label": "Observed Astra / medium", "value": cost["root_astra_medium_usd"], "note": "this session"},
            {"label": "Same tokens at Sol rates", "value": cost["same_usage_sol_xhigh_usd"], "note": "price-only counterfactual"},
            {"label": "Prior Sol / xhigh root", "value": cost["previous_sol_xhigh_root_usd"], "note": "different session"},
        ],
        separators=(",", ":"),
    )
    source_url = "https://github.com/ehonda/KicktippAi/tree/538c30c53870faa608cf0d6e6a9dbf20f8d833d3"
    previous_report = "../p1-orchestration-follow-up/"
    pricing_url = "https://developers.openai.com/api/docs/pricing"

    html = f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="kicktippai-report-title" content="Urgent production orchestration">
  <title>Urgent production orchestration · KicktippAi</title>
  <style>
    :root {{ --ink:#172026; --muted:#5f6d73; --paper:#f6f1e8; --card:#fffdf8; --line:#d9d1c4; --blue:#176b87; --blue2:#7cc0cb; --green:#29755d; --amber:#a66b17; --red:#a4473d; --shadow:0 20px 50px rgba(35,41,43,.10); }}
    * {{ box-sizing:border-box; min-width:0; }} html {{ scroll-behavior:smooth; overflow-x:hidden; }}
    body {{ margin:0; color:var(--ink); background:radial-gradient(circle at 10% 0,rgba(54,154,174,.16),transparent 32%),linear-gradient(180deg,#fbf8f1,var(--paper)); font-family:"Segoe UI",system-ui,sans-serif; }}
    a {{ color:var(--blue); }} main {{ max-width:1180px; margin:auto; padding:26px 22px 70px; }}
    .hero {{ padding:44px; background:linear-gradient(140deg,#13252c 0%,#1e5361 68%,#397d88 100%); color:#f9f7ef; border-radius:30px; box-shadow:var(--shadow); overflow:hidden; position:relative; }}
    .hero:after {{ content:""; position:absolute; width:370px; height:370px; border:70px solid rgba(255,255,255,.055); border-radius:50%; right:-135px; top:-190px; }}
    .eyebrow {{ display:block; text-transform:uppercase; letter-spacing:.16em; font-size:.75rem; font-weight:800; color:#a8d9df; margin-bottom:12px; overflow-wrap:anywhere; }}
    h1 {{ font-size:clamp(2.5rem,6vw,5.4rem); letter-spacing:-.055em; line-height:.94; max-width:900px; margin:0 0 20px; }}
    .hero .lead {{ font-size:clamp(1.05rem,2vw,1.32rem); line-height:1.55; max-width:850px; color:#dcebed; overflow-wrap:anywhere; }}
    .verdict {{ margin-top:28px; display:flex; gap:14px; align-items:flex-start; max-width:900px; }} .verdict b {{ color:#fff; }}
    .verdict i {{ flex:0 0 10px; height:74px; border-radius:10px; background:#f6bd60; }}
    .chips {{ display:flex; flex-wrap:wrap; gap:9px; margin-top:26px; }} .chip {{ background:rgba(255,255,255,.1); border:1px solid rgba(255,255,255,.16); border-radius:999px; padding:7px 11px; font-size:.78rem; overflow-wrap:anywhere; max-width:100%; }}
    nav {{ display:flex; flex-wrap:wrap; gap:8px; padding:18px 4px 8px; position:sticky; top:0; z-index:5; backdrop-filter:blur(12px); }} nav a {{ text-decoration:none; background:rgba(255,253,248,.88); border:1px solid var(--line); border-radius:999px; padding:8px 12px; font-size:.82rem; color:var(--ink); }}
    .kpis {{ display:grid; grid-template-columns:repeat(4,1fr); gap:13px; margin:18px 0; }} .kpi {{ background:var(--card); border:1px solid var(--line); border-radius:18px; padding:20px; box-shadow:0 8px 24px rgba(35,41,43,.05); }} .kpi strong {{ display:block; font-size:2rem; letter-spacing:-.04em; }} .kpi span {{ color:var(--muted); font-size:.86rem; line-height:1.35; }}
    section.block {{ background:rgba(255,253,248,.82); border:1px solid var(--line); border-radius:24px; padding:30px; margin-top:18px; }} .section-head {{ display:flex; justify-content:space-between; gap:24px; align-items:end; margin-bottom:22px; }} .section-head h2 {{ font-size:clamp(1.8rem,3.7vw,3rem); letter-spacing:-.04em; margin:0; }} .section-head p {{ max-width:540px; color:var(--muted); line-height:1.55; margin:0; }}
    .grid2 {{ display:grid; grid-template-columns:1fr 1fr; gap:15px; }} .grid3 {{ display:grid; grid-template-columns:repeat(3,1fr); gap:15px; }}
    .card {{ background:var(--card); border:1px solid var(--line); border-radius:18px; padding:20px; }} .card h3 {{ margin:8px 0 10px; font-size:1.2rem; }} .card p {{ color:var(--muted); line-height:1.58; }} .big {{ font-size:2.8rem; letter-spacing:-.05em; font-weight:800; display:block; }}
    .outcome {{ border-top:4px solid var(--green); }} .caution {{ border-top:4px solid var(--amber); }}
    .score-grid {{ display:grid; grid-template-columns:1fr 1fr; gap:13px; }} .score-card {{ background:var(--card); border:1px solid var(--line); border-radius:18px; padding:20px; }} .score-card h3 {{ margin:10px 0; }} .score-card p {{ line-height:1.55; color:var(--muted); }} .score-card .interpretation {{ color:var(--ink); border-top:1px solid var(--line); padding-top:12px; }}
    .signal {{ display:inline-block; border-radius:999px; padding:5px 9px; text-transform:uppercase; letter-spacing:.08em; font-size:.67rem; font-weight:850; }} .signal.pass {{ background:#dbeee6; color:var(--green); }} .signal.qualified-pass,.signal.mixed {{ background:#f5e8c9; color:#80530f; }} .signal.regression {{ background:#f1dad6; color:var(--red); }}
    .bars {{ display:grid; gap:15px; }} .bar-head {{ display:flex; justify-content:space-between; gap:10px; align-items:baseline; margin-bottom:6px; }} .bar-head small {{ color:var(--muted); }} .track {{ height:18px; background:#e6e0d6; border-radius:999px; overflow:hidden; }} .track i {{ height:100%; display:block; border-radius:inherit; background:linear-gradient(90deg,var(--blue),var(--blue2)); transition:width .7s ease; }}
    .formula {{ display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin-top:15px; }} .formula div {{ background:#eef5f5; border-radius:12px; padding:12px; }} .formula b,.formula span {{ display:block; }} .formula span {{ color:var(--muted); font-size:.76rem; margin-top:4px; }}
    .recommendation {{ background:#13252c; color:#fff; border-radius:18px; padding:22px; margin-top:15px; }} .recommendation p {{ color:#d7e5e7; line-height:1.6; }}
    .stat-row {{ display:grid; grid-template-columns:repeat(4,1fr); gap:10px; margin:14px 0; }} .stat-row div {{ background:#f1eee7; padding:13px; border-radius:12px; }} .stat-row b,.stat-row small {{ display:block; }} .stat-row small {{ color:var(--muted); margin-top:3px; }}
    table {{ width:100%; border-collapse:collapse; font-size:.88rem; }} th {{ text-align:left; color:var(--muted); font-size:.7rem; text-transform:uppercase; letter-spacing:.08em; }} td,th {{ padding:11px 9px; border-bottom:1px solid var(--line); vertical-align:middle; }} td small {{ display:block; color:var(--muted); margin-top:3px; }} td.num {{ text-align:right; font-variant-numeric:tabular-nums; }} .mini-bar {{ display:inline-block; vertical-align:middle; width:120px; height:8px; background:#e6e0d6; border-radius:99px; overflow:hidden; margin-right:8px; }} .mini-bar i {{ display:block; height:100%; background:var(--blue); }} .cost-label {{ white-space:nowrap; }}
    .ci {{ font-size:.72rem; font-weight:800; text-transform:uppercase; }} .ci.success {{ color:var(--green); }} .ci.failure {{ color:var(--red); }}
    .timeline {{ list-style:none; padding:0; margin:0; }} .timeline li {{ display:grid; grid-template-columns:150px 18px 1fr; gap:12px; position:relative; padding:0 0 22px; }} .timeline li:before {{ content:""; grid-column:2; width:10px; height:10px; background:var(--blue); border:4px solid #d8edf0; border-radius:50%; z-index:2; }} .timeline li:after {{ content:""; position:absolute; left:167px; top:15px; bottom:-1px; width:2px; background:#c7d9db; }} .timeline li:last-child:after {{ display:none; }} .timeline time {{ text-align:right; color:var(--muted); font-size:.78rem; padding-top:2px; }} .timeline strong {{ display:block; }} .timeline p {{ margin:5px 0 0; color:var(--muted); line-height:1.5; }}
    .delta {{ font-weight:800; }} .good-text {{ color:var(--green); }} .warn-text {{ color:var(--red); }}
    ul.clean {{ padding-left:20px; }} ul.clean li {{ margin:9px 0; line-height:1.55; color:var(--muted); }}
    footer {{ color:var(--muted); font-size:.8rem; line-height:1.6; padding:28px 6px 0; }}
    @media(max-width:850px) {{ .kpis,.grid2,.grid3,.score-grid {{ grid-template-columns:1fr 1fr; }} .section-head {{ display:block; }} .section-head p {{ margin-top:8px; }} .stat-row {{ grid-template-columns:1fr 1fr; }} }}
    @media(max-width:600px) {{ main {{ padding:12px 10px 38px; overflow:hidden; }} .hero,section.block {{ padding:21px; border-radius:20px; }} .hero h1 {{ font-size:2.25rem; }} .hero .lead {{ font-size:1rem; }} .kpis,.grid2,.grid3,.score-grid,.formula {{ grid-template-columns:1fr; }} nav {{ position:static; }} .timeline li {{ grid-template-columns:1fr 18px; }} .timeline time {{ grid-column:1; text-align:left; }} .timeline li:before {{ grid-column:2; grid-row:1; }} .timeline li div {{ grid-column:1/-1; }} .timeline li:after {{ display:none; }} table {{ min-width:690px; }} .table-wrap {{ overflow:auto; max-width:100%; }} }}
  </style>
</head>
<body>
<main>
  <header class="hero">
    <span class="eyebrow">Codex orchestration investigation · 5 September 2026</span>
    <h1>Safer and more resilient. Still expensive.</h1>
    <p class="lead">The PR #98 workflow survived an urgent production repair, live HTML drift, a Linux-only CI defect, repeated exact reviews, and a planning handoff. The strongest improvements were resource admission and agent-slot recovery. Astra/medium did not reduce root token volume enough to offset its 2.5× price.</p>
    <div class="verdict"><i></i><p><b>Verdict:</b> keep the new capacity, memory, heavy-lease, and bounded-publication rules. Tighten ledger coalescing and end specialist reuse when the role changes materially. This run does not establish enough incremental orchestration value to make Astra/medium the cost default.</p></div>
    <div class="chips"><span class="chip">Run {metrics['session_boundary']['run_id']}</span><span class="chip">Frozen at {metrics['session_boundary']['final_commit'][:7]}</span><span class="chip">{hours(metrics['session_boundary']['wall_seconds'])} wall · {hours(metrics['session_boundary']['effective_seconds'])} effective</span><span class="chip">Root: gpt-6-astra / medium</span></div>
  </header>

  <nav><a href="#outcome">Outcome</a><a href="#workflow">PR #98 scorecard</a><a href="#cost">Astra cost</a><a href="#agents">Agent portfolio</a><a href="#timeline">Timeline</a><a href="#limits">Limits</a></nav>

  <div class="kpis">
    <article class="kpi"><strong>2 × 16/16</strong><span>natural production workflow cycles succeeded</span></article>
    <article class="kpi"><strong>3 / 3</strong><span>CL answers stored and verified on Kicktipp</span></article>
    <article class="kpi"><strong>11</strong><span>task-agent threads · zero slot-limit errors</span></article>
    <article class="kpi"><strong>{money(cost['root_astra_medium_usd'])}</strong><span>Astra root API list-price equivalent</span></article>
  </div>

  <section class="block" id="outcome">
    <div class="section-head"><h2>The session restored production.</h2><p>The delivery result matters when judging control-plane overhead: this was a long, adversarial incident response rather than a routine implementation sample.</p></div>
    <div class="grid3">
      <article class="card outcome"><span class="big">32 / 32</span><h3>Natural jobs passed</h3><p>Two independently observed scheduled runs completed every job after the history repair. The second cycle proved continuity after later CL changes.</p></article>
      <article class="card outcome"><span class="big">1 / 4 / 1</span><h3>CL selections verified</h3><p>One successful POST applied all three generated answers. A read-only recovery pass confirmed Firestore lineage and the exact Kicktipp selections.</p></article>
      <article class="card outcome"><span class="big">18</span><h3>Scoped commits</h3><p>{delivery['net_files']} net files changed, with {delivery['net_insertions']:,} insertions and {delivery['net_deletions']:,} deletions. The final source-session CI passed all 12 jobs.</p></article>
    </div>
    <div class="grid2" style="margin-top:15px">
      <article class="card caution"><h3>Fail-closed behavior worked</h3><p>The first paid attempt stopped before model, Firestore, or Kicktipp mutation when the live form action changed. Later, an uncertain redirect response was reconciled through reads before any retry.</p></article>
      <article class="card caution"><h3>Live evidence still outran the specification</h3><p>Three release corrections were needed for a changed endpoint, library-specific base resolution, and Linux URI semantics. Strong review found many defects, but test fixtures did not capture all production and cross-platform behavior before the first dispatch.</p></article>
    </div>
  </section>

  <section class="block" id="workflow">
    <div class="section-head"><h2>PR #98 mostly worked.</h2><p>Four changes passed directly, two helped with qualifications, one exposed over-reuse, and the transition-only ledger rule regressed.</p></div>
    <div class="stat-row">
      <div><b>+{concurrency_change:.1f}%</b><small>average active concurrency</small></div>
      <div><b>+{two_plus_change:.0f}%</b><small>share of active time at 2+ agents</small></div>
      <div><b>{workflow['admitted_heavy_samples_below_warning_floor']}</b><small>heavy admissions below old floor</small></div>
      <div><b>{workflow['denied_resource_samples']}</b><small>resource samples denied by current gates</small></div>
    </div>
    <div class="score-grid">{scorecard_html(facts['workflow_scorecard'])}</div>
    <div class="recommendation"><h3>Next workflow edit</h3><p>Keep the PR #98 changes. Add a release trigger when a retained agent changes role or crosses a recorded context-cost threshold; the root should assign a fresh bounded thread for the next role. Make no-change resource samples ledger-silent, and clarify that another <code>EXECUTION START</code> marker is permitted only after a material re-freeze. Record standing production authority explicitly so a safe pre-mutation failure does not recreate an owner gate.</p></div>
  </section>

  <section class="block" id="cost">
    <div class="section-head"><h2>Astra cost 2.5× for the same tokens.</h2><p>The cleanest comparison applies OpenAI's current short-context rates to the observed root token mix. All responses stayed below the long-context pricing threshold.</p></div>
    <div class="grid2">
      <article class="card">
        <div id="cost-bars" class="bars"></div>
        <div class="formula">
          <div><b>{money(astra_parts['cached'])}</b><span>186.69M cached input × $1/M</span></div>
          <div><b>{money(astra_parts['uncached'])}</b><span>1.75M uncached input × $10/M</span></div>
          <div><b>{money(astra_parts['output'])}</b><span>0.30M output × $50/M</span></div>
        </div>
      </article>
      <article class="card">
        <span class="signal regression">Cost premium</span><span class="big">+{money(cost['astra_increment_usd'])}</span>
        <h3>for the observed root token mix</h3>
        <p>Astra and Sol prices differ by exactly 2.5× in every applicable token class. Rates per million input / cached input / output tokens are $10 / $1 / $50 for Astra and $4 / $0.40 / $20 for Sol. The 99.07% cache hit rate helped both models, but cached input still dominated the root bill.</p>
        <div class="stat-row">
          <div><b>+{cost['root_token_change'] * 100:.2f}%</b><small>tokens vs prior Sol root</small></div>
          <div><b>+{previous_cost_change:.1f}%</b><small>cost vs prior Sol root</small></div>
          <div><b>{money(cost['current_root_cost_per_effective_hour'])}</b><small>Astra / effective hour</small></div>
          <div><b>{money(cost['previous_sol_cost_per_effective_hour'])}</b><small>prior Sol / effective hour</small></div>
        </div>
      </article>
    </div>
    <div class="grid2" style="margin-top:15px">
      <article class="card"><h3>Whole priced portfolio</h3><span class="big">{money(cost['priced_agent_portfolio_usd'])}</span><p>Root plus priced task agents. The task agents account for {money(task_agent_cost)}. Repricing only the root to Sol lowers the portfolio to {money(cost['priced_portfolio_if_root_sol_usd'])}; Astra's premium is {cost['astra_increment_share_of_priced_portfolio'] * 100:.1f}% of the observed portfolio.</p></article>
      <article class="card"><h3>Decision</h3><p>The session achieved a difficult recovery, but it is not a matched quality experiment. Root tokens rose 6.24% from the prior Sol/xhigh session, so no token-efficiency signal offsets the list-price premium. Keep Sol/xhigh as the cost baseline and reserve Astra/medium for incident response or decisions where a matched evaluation shows enough quality or latency gain to justify roughly {money(cost['astra_increment_usd'])} at this run size.</p><p><a href="{pricing_url}">Official OpenAI pricing used in this report</a></p></article>
    </div>
  </section>

  <section class="block" id="agents">
    <div class="section-head"><h2>Reuse cut thread churn and grew contexts.</h2><p>The session used fewer logical threads than the predecessor but many more turns per thread. The completion specialist became the clearest concentration risk.</p></div>
    <div class="stat-row">
      <div><b>{workflow['subagent_threads']}</b><small>threads · predecessor 27</small></div>
      <div><b>{workflow['subagent_turns']}</b><small>turns · predecessor 70</small></div>
      <div><b>{workflow['turns_per_thread']:.2f}</b><small>turns per thread</small></div>
      <div><b>{workflow['root_token_share'] * 100:.1f}%</b><small>root share of priced tokens</small></div>
    </div>
    <div class="table-wrap"><table><thead><tr><th>Agent</th><th class="num">Responses</th><th class="num">Tokens</th><th>API equivalent</th></tr></thead><tbody>{model_rows_html(models)}</tbody></table></div>
    <div class="grid2" style="margin-top:15px">
      <article class="card"><h3>Coordination got quieter</h3><p>The root issued {workflow['wait_calls']} event waits, {workflow['waits_per_effective_hour']:.2f} per effective hour, down {abs(wait_change):.1f}% from the predecessor. Eleven spawns plus 97 follow-ups show the V2 lifecycle was actively reused.</p></article>
      <article class="card"><h3>The ledger got louder</h3><p>{workflow['root_ledger_patches']} state patches produced {workflow['root_ledger_patches_per_effective_hour']:.2f} writes per effective hour, up {ledger_change:.1f}%. The count is too high for a compact recovery ledger even after allowing for live re-freezes.</p></article>
    </div>
  </section>

  <section class="block" id="timeline">
    <div class="section-head"><h2>Sixteen and a half hours, with 5h 23m paused.</h2><p>The pause-adjusted 11h 11m denominator removes two production-approval waits and the owner's deliberate break before planning resumed.</p></div>
    <ol class="timeline">{timeline_html(facts['timeline'])}</ol>
  </section>

  <section class="block" id="ci">
    <div class="section-head"><h2>Nine source-session CI runs.</h2><p>Eight passed. The lone Linux failure at c1c44f was diagnosed and corrected at 497e1a5 before the successful production run.</p></div>
    <div class="table-wrap"><table><thead><tr><th>SHA</th><th>Title</th><th>Result</th></tr></thead><tbody>{ci_rows_html(ci_runs)}</tbody></table></div>
  </section>

  <section class="block" id="limits">
    <div class="section-head"><h2>Read the comparison within its limits.</h2><p>This report freezes the root transcript at its terminal response and the repository at its final source-session commit.</p></div>
    <ul class="clean">{''.join(f'<li>{escape(item)}</li>' for item in facts['limitations'])}</ul>
    <div class="grid3" style="margin-top:18px">
      <article class="card"><h3>Frozen source</h3><p><a href="{source_url}">Repository at {metrics['session_boundary']['final_commit'][:7]}</a><br>Cutoff {escape(metrics['session_boundary']['event_cutoff_at'])}</p></article>
      <article class="card"><h3>Comparison</h3><p><a href="{previous_report}">Previous Sol/xhigh orchestration report</a><br>Different task graph; used only as an observed baseline.</p></article>
      <article class="card"><h3>Data</h3><p>Normalized JSON and CSV evidence lives beside the report source under <code>docs/codex/urgent-production-orchestration-investigation/data</code>.</p></article>
    </div>
  </section>

  <footer>Generated from frozen local Codex transcripts, exact Git history, authenticated GitHub Actions metadata, and OpenAI's pricing page. API list-price equivalents are analytical comparisons; they are not a reconstruction of subscription billing.</footer>
</main>
<script>
  const costData={cost_data}; const maxCost=Math.max(...costData.map(d=>d.value));
  document.querySelector('#cost-bars').innerHTML=costData.map(d=>`<div><div class="bar-head"><span>${{d.label}} <small>· ${{d.note}}</small></span><strong>${{d.value.toLocaleString('en-US',{{style:'currency',currency:'USD'}})}}</strong></div><div class="track"><i style="width:${{(d.value/maxCost*100).toFixed(2)}}%"></i></div></div>`).join('');
</script>
</body>
</html>
"""

    inspector = Inspector()
    inspector.feed(html)
    missing_fragments = inspector.fragments - inspector.ids
    if inspector.duplicates:
        raise ValueError(f"duplicate HTML IDs: {sorted(inspector.duplicates)}")
    if missing_fragments:
        raise ValueError(f"missing local fragments: {sorted(missing_fragments)}")
    if inspector.external_assets:
        raise ValueError(f"report must be self-contained: {inspector.external_assets}")
    if html.count("<section") != html.count("</section>"):
        raise ValueError("unbalanced section tags")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(html, encoding="utf-8", newline="\n")
    print(f"Wrote {output}")


def main() -> None:
    here = pathlib.Path(__file__).parent
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", type=pathlib.Path, default=here / "data")
    parser.add_argument("--output", type=pathlib.Path, default=here.parents[2] / "session-analysis" / "urgent-production-orchestration" / "index.html")
    args = parser.parse_args()
    build(args.data_dir, args.output)


if __name__ == "__main__":
    main()
