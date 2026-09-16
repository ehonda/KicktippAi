#!/usr/bin/env python3
"""Build the self-contained P1-04 activation orchestration report."""

from __future__ import annotations

import html
import json
import pathlib
from datetime import datetime


HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
DATA = HERE / "data"
OUTPUT = REPO / "session-analysis" / "p1-04-activation-orchestration" / "index.html"
MANIFEST = REPO / "docs/codex/session-analysis/reports/p1-04-activation-orchestration.report.json"
COMMIT = "a846bcbd0c23f22cd646c48ccc113ca25642ef2e"
REPO_URL = "https://github.com/ehonda/KicktippAi"


def load(path: pathlib.Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def duration(seconds: float) -> str:
    seconds = int(round(seconds))
    hours, remainder = divmod(seconds, 3600)
    minutes, secs = divmod(remainder, 60)
    if hours:
        return f"{hours}h {minutes:02d}m"
    if minutes:
        return f"{minutes}m {secs:02d}s"
    return f"{secs}s"


def number(value: int | float, digits: int = 0) -> str:
    if digits:
        return f"{value:,.{digits}f}"
    return f"{value:,.0f}"


def esc(value: object) -> str:
    return html.escape(str(value))


def optional_gib(value: float | None) -> str:
    return "—" if value is None else f"{value:.3f}"


def link(path: str, label: str) -> str:
    return f'<a href="{REPO_URL}/blob/{COMMIT}/{path}">{html.escape(label)}</a>'


def main() -> None:
    manifest = load(MANIFEST)
    analysis = load(DATA / "analysis.json")
    derived = load(DATA / "derived-metrics.json")
    external = load(DATA / "external-evidence.json")
    summary = analysis["summary"]
    waits = derived["root"]["wait_agent"]
    usage = waits["usage_for_pure_wait_responses"]
    rates = manifest["analysis"]["pricing"]["models"]["gpt-5.6-sol"]
    uncached = usage["input_tokens"] - usage["cached_input_tokens"] - usage["cache_write_input_tokens"]
    wait_cost = (
        uncached * rates["input"] + usage["cached_input_tokens"] * rates["cached_input"] +
        usage["cache_write_input_tokens"] * rates["cache_write_input"] + usage["output_tokens"] * rates["output"]
    ) / 1_000_000
    long_waits = waits["by_requested_timeout_seconds"]
    long_calls = long_waits["300"]["calls"] + long_waits["600"]["calls"]
    long_early = long_waits["300"]["early_return_calls"] + long_waits["600"]["early_return_calls"]
    output_counts = derived["coordination_outcomes"]["counts"]
    bad_outcomes = output_counts["failed"] + output_counts["rejected"]
    good_outcomes = output_counts["accepted"] + output_counts["passed"]

    owner_messages = {row.get("intervention_kind"): row for row in analysis["user_messages"] if row.get("intervention_kind")}
    production_correction = datetime.fromisoformat(owner_messages["production-intent-correction"]["timestamp"].replace("Z", "+00:00"))
    routing_correction = datetime.fromisoformat(owner_messages["orchestration-routing-correction"]["timestamp"].replace("Z", "+00:00"))
    direct_root_seconds = (routing_correction - production_correction).total_seconds()

    compaction_rows = "".join(
        f"<tr><td>{index + 1}</td><td>{number(row['pre_compaction_input_tokens'])}</td>"
        f"<td>{number(row['post_compaction_context_tokens'])}</td>"
        f"<td>{esc(row['recovery_shape'])}</td><td>{row['seconds_to_first_tool_call']:.1f}s</td></tr>"
        for index, row in enumerate(derived["compactions"]["events"])
    )
    heavy_rows = "".join(
        f"<tr><td>{esc(row['label'])}</td><td>{esc(row['outcome'])}</td>"
        f"<td>{optional_gib(row['started_available_gib'])}</td>"
        f"<td>{row['minimum_available_gib']:.3f}</td>"
        f"<td>{'—' if row['queue_delay_seconds'] is None else esc(row['queue_delay_seconds'])}</td>"
        f"<td>{esc(row['memory_symptoms'])}</td></tr>"
        for row in derived["heavy_observations"]
    )
    phase_rows = "".join(
        f"<tr><td>{esc(name)}</td><td>{row['threads']}</td><td>{row['turns']}</td>"
        f"<td>{duration(row['active_seconds'])}</td><td>${row['api_cost_equivalent_usd']:.2f}</td></tr>"
        for name, row in derived["subagents"]["by_phase"].items()
    )
    model_rows = "".join(
        f"<tr><td>{esc(row['kind'])}</td><td><code>{esc(row['model'])}</code></td>"
        f"<td>{esc(row['reasoning_effort'])}</td><td>{number(row['responses'])}</td>"
        f"<td>{number(row['total_tokens'])}</td><td>${row['api_cost_equivalent_usd']:.2f}</td></tr>"
        for row in derived["model_usage"]
    )
    focus_descriptions = {
        "checkpoint-helper-powershell-compatibility": "Root cause reproduced; cross-version parser wrapper and dual-host test specified.",
        "compaction-recovery-and-context-occupancy": "Four compactions quantified; two compact recoveries, one task read, one avoidable broad rediscovery.",
        "disk-warning-band-policy": "Warning was advisory; 14 GiB was the gate. Replace noisy percentage warning with a 20 GiB advisory.",
        "docker-prerequisite-preflight": "No startup check exists; add advisory availability plus validation-time hard gate when required.",
        "heavy-operation-concurrency-policy": "Observed headroom did not justify two heavy profiles; measured queue delay was zero.",
        "memory-policy-restrictiveness": "1.101 GiB passed; 1.049 GiB hit sustained paging; keep the 1.0 GiB preferred floor.",
        "orchestration-delivery-efficiency": "Meaningful 6,028-line net delivery, but C3 churn and polling were expensive.",
        "owner-waiting-vs-agent-blocking": "The long gap followed completed closeout; the avoidable delay was 25m51s of direct root work before protocol rerouting.",
        "reconciliation-models-and-stability": "Sol/xhigh re-slice needed two reviews; its graph held, but two later seam defects escaped to Astra acceptance.",
        "review-correction-churn": "Eight rejected and two failed turns; C3 had three implementation corrections and two final-acceptance rejects.",
        "subagent-coordination-effectiveness": "Complete role graph and first-pass outcomes are reconstructed below.",
        "subagent-polling-overhead": "266 waits, 173 no-change timeouts, 35.17M tokens, and $15.79 equivalent.",
        "useful-parallelism-and-wave-throttle": "Peak was one; the accepted graph was mostly serial and no causal heavy queue delay was recorded.",
    }
    focus_rows = "".join(
        f"<tr><td><code>{esc(focus_id)}</code></td><td><span class='tag good'>addressed</span></td>"
        f"<td>{esc(focus_descriptions[focus_id])}</td></tr>"
        for focus_id in manifest["focus_ids"]
    )

    html_text = f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'; worker-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; font-src data:; media-src data:">
  <title>{esc(manifest['title'])}</title>
  <style>
    :root {{ color-scheme: dark; --bg:#0b1117; --panel:#111b25; --panel2:#162432; --ink:#edf4f8; --muted:#9fb1bf; --line:#294052; --teal:#4ee1c1; --amber:#ffca66; --red:#ff7d82; --blue:#79b8ff; }}
    * {{ box-sizing:border-box; }}
    html {{ scroll-behavior:smooth; }}
    body {{ margin:0; background:radial-gradient(circle at 12% -10%,#163144 0,transparent 35%),var(--bg); color:var(--ink); font-family:Inter,"Segoe UI",system-ui,sans-serif; line-height:1.55; }}
    main {{ width:min(1180px,calc(100% - 30px)); margin:28px auto 70px; }}
    header,.panel {{ border:1px solid var(--line); border-radius:22px; background:linear-gradient(145deg,rgba(22,36,50,.96),rgba(13,23,32,.97)); box-shadow:0 18px 50px rgba(0,0,0,.24); }}
    header {{ padding:clamp(24px,5vw,52px); }}
    .eyebrow {{ color:var(--teal); text-transform:uppercase; letter-spacing:.16em; font-size:.74rem; font-weight:800; }}
    h1 {{ margin:.35rem 0 .7rem; font-size:clamp(2.2rem,6vw,4.8rem); line-height:.98; max-width:900px; }}
    h2 {{ margin:0 0 14px; font-size:clamp(1.45rem,3vw,2.2rem); }}
    h3 {{ margin:0 0 8px; font-size:1.08rem; }}
    p {{ color:var(--muted); margin:.45rem 0; }}
    a {{ color:var(--blue); }}
    code {{ color:#c9f5eb; font-size:.92em; overflow-wrap:anywhere; }}
    .verdict {{ margin-top:24px; padding:20px 22px; border-left:4px solid var(--amber); background:rgba(255,202,102,.08); border-radius:0 14px 14px 0; font-size:1.07rem; }}
    .verdict strong {{ color:var(--ink); }}
    .grid {{ display:grid; gap:14px; }}
    .stats {{ grid-template-columns:repeat(4,minmax(0,1fr)); margin:18px 0; }}
    .stat {{ padding:18px; background:var(--panel2); border:1px solid var(--line); border-radius:16px; }}
    .stat b {{ display:block; color:var(--teal); font-size:clamp(1.5rem,3vw,2.35rem); line-height:1.05; }}
    .stat span {{ color:var(--muted); font-size:.86rem; }}
    .panel {{ padding:clamp(20px,3vw,32px); margin-top:18px; }}
    .questions {{ grid-template-columns:repeat(3,minmax(0,1fr)); }}
    .question {{ padding:20px; background:rgba(10,18,25,.7); border:1px solid var(--line); border-radius:16px; }}
    .question .answer {{ color:var(--ink); font-weight:750; }}
    .tag {{ display:inline-block; padding:3px 8px; border-radius:999px; font-size:.75rem; font-weight:800; }}
    .tag.good {{ color:#091713; background:var(--teal); }}
    .tag.warn {{ color:#211703; background:var(--amber); }}
    .tag.bad {{ color:#210609; background:var(--red); }}
    .flow {{ display:grid; grid-template-columns:repeat(7,minmax(130px,1fr)); gap:10px; overflow-x:auto; padding-bottom:6px; }}
    .flow div {{ min-height:120px; padding:15px; border:1px solid var(--line); border-radius:14px; background:var(--panel2); }}
    .flow b {{ display:block; color:var(--teal); margin-bottom:6px; }}
    .flow small {{ color:var(--muted); }}
    .table-wrap {{ overflow-x:auto; border:1px solid var(--line); border-radius:15px; }}
    table {{ width:100%; border-collapse:collapse; min-width:720px; }}
    th,td {{ padding:11px 13px; text-align:left; border-bottom:1px solid var(--line); vertical-align:top; }}
    th {{ color:var(--teal); font-size:.76rem; text-transform:uppercase; letter-spacing:.08em; background:#0f1a24; }}
    tr:last-child td {{ border-bottom:0; }}
    .callout {{ margin-top:14px; padding:15px 18px; border:1px solid var(--line); border-radius:14px; background:rgba(121,184,255,.07); }}
    .actions {{ display:grid; gap:10px; counter-reset:item; padding:0; }}
    .actions li {{ list-style:none; padding:14px 16px 14px 54px; position:relative; border:1px solid var(--line); border-radius:14px; color:var(--muted); background:rgba(10,18,25,.6); }}
    .actions li::before {{ counter-increment:item; content:counter(item); position:absolute; left:15px; top:13px; width:27px; height:27px; display:grid; place-items:center; border-radius:50%; background:var(--teal); color:#06120f; font-weight:900; }}
    .actions strong {{ color:var(--ink); }}
    .two {{ grid-template-columns:1fr 1fr; }}
    .method {{ font-size:.92rem; }}
    footer {{ padding:24px 4px; color:var(--muted); font-size:.86rem; }}
    @media (max-width:900px) {{ .stats,.questions,.two {{ grid-template-columns:1fr 1fr; }} }}
    @media (max-width:620px) {{ .stats,.questions,.two {{ grid-template-columns:1fr; }} main {{ width:min(100% - 18px,1180px); margin-top:9px; }} header,.panel {{ border-radius:16px; }} }}
  </style>
</head>
<body>
<main>
  <header>
    <div class="eyebrow">{esc(manifest['eyebrow'])} · frozen 16 September 2026</div>
    <h1>{esc(manifest['title'])}</h1>
    <p>{esc(manifest['summary'])}</p>
    <div class="verdict"><strong>Overall: successful delivery, but C3 paid heavily for late seam discovery.</strong> PR #111 merged a substantial dormant implementation and E1 finished cleanly after one resource retry. The main inefficiencies were C3's ten rejected/failed turns, strict one-agent sequencing, and 173 no-change polls. The three owner concerns are valid: fix the timestamp parser, replace the noisy disk percentage warning, and surface Docker readiness at startup.</div>
  </header>

  <section class="grid stats" aria-label="Key metrics">
    <div class="stat"><b>31 / 45</b><span>subagent threads / turns; peak concurrency 1</span></div>
    <div class="stat"><b>{bad_outcomes}</b><span>rejected or failed subagent turns</span></div>
    <div class="stat"><b>180.46M</b><span>tokens, 95.7% cached input</span></div>
    <div class="stat"><b>${summary['api_cost_equivalent_usd']:.2f}</b><span>API list-price equivalent, not subscription spend</span></div>
  </section>

  <section class="panel" id="owner-questions">
    <h2>The three questions that prompted this analysis</h2>
    <div class="grid questions">
      <article class="question">
        <span class="tag bad">Fix</span><h3>PowerShell timestamp parsing</h3>
        <p class="answer">This is a real cross-version/culture defect.</p>
        <p>On PowerShell {esc(derived['powershell_compatibility_probe']['ps_version'])} with <code>{esc(derived['powershell_compatibility_probe']['culture'])}</code>, plain <code>ConvertFrom-Json</code> converts the ISO value to <code>DateTime</code>, stringifies it as <code>{esc(derived['powershell_compatibility_probe']['default_string'])}</code>, and <code>DateTimeOffset.TryParse</code> returns false. <code>-DateKind String</code> preserves the ISO value and parses successfully.</p>
        <p><strong>Change:</strong> route every helper JSON parse through a capability-tested wrapper: use <code>-DateKind String</code> where supported, plain parsing on Windows PowerShell 5.1, and run the exact persisted-state transition under both hosts.</p>
      </article>
      <article class="question">
        <span class="tag good">No gate</span><h3>31.79 GiB disk warning</h3>
        <p class="answer">It had no admission consequence.</p>
        <p>The warning fired only because 31.79 GiB is 13.4% of a 236.68 GiB disk, below the fixed 15% advisory. Worktree admission used the separate 14 GiB absolute floor and passed. Heavy validation was serialized by the one-family contract, not disk.</p>
        <p><strong>Change:</strong> replace the percentage warning with an absolute <code>minimumEffectiveFreeGiBWarning: 20</code>. Keep the 14 GiB hard floor separate unless the owner explicitly chooses a 20 GiB hard gate.</p>
      </article>
      <article class="question">
        <span class="tag warn">Add preflight</span><h3>Docker readiness</h3>
        <p class="answer">Startup currently says nothing.</p>
        <p>The orchestration contract contains {derived['policy']['docker_mentions_in_orchestrate_contract']} Docker references, while Firebase integration suites use a Testcontainers-backed Firestore emulator. The owner had to announce that Docker became available after the run began.</p>
        <p><strong>Change:</strong> show <code>available / unavailable / unknown</code> during initialization. Keep it advisory until the accepted validation graph requires a Docker-backed gate; recheck and block only at that gate.</p>
      </article>
    </div>
  </section>

  <section class="panel" id="delivery">
    <h2>What the run delivered</h2>
    <div class="grid stats">
      <div class="stat"><b>{derived['git']['files']}</b><span>net changed files</span></div>
      <div class="stat"><b>+{number(derived['git']['insertions'])}</b><span>insertions; −{number(derived['git']['deletions'])} deletions</span></div>
      <div class="stat"><b>{derived['git']['committed_during_session_window']}</b><span>range commits made during the session window</span></div>
      <div class="stat"><b>merged</b><span>PR #111 at <code>{external['github_pull_request']['merge_sha'][:7]}</code></span></div>
    </div>
    <p>P1-04 moved from an incomplete corrected checkpoint to <em>complete—dormant implementation only</em>; P1-05 remained truthfully deferred. The report boundary also includes the review brief, PR merge, and the first {duration(direct_root_seconds)} of owner-requested operational activation analysis. It ends before that activation was delegated through the orchestration graph.</p>
    <div class="callout"><strong>Wall-time denominator:</strong> the 23h 42m session span includes roughly 14h 20m between the completed closeout and the owner's later review-brief request. Do not treat that interval as agent blocking or continuous delivery time.</div>
  </section>

  <section class="panel" id="coordination">
    <h2>Coordination and correction graph</h2>
    <div class="flow" role="img" aria-label="Serial orchestration flow">
      <div><b>Preview</b><small>1 audit<br>graph corrected</small></div>
      <div><b>C3 initial</b><small>3 implementation rejects<br>fresh review accepted</small></div>
      <div><b>Cumulative</b><small>18 failing tests<br>diagnosis</small></div>
      <div><b>Re-slice</b><small>spec rejected once<br>then accepted</small></div>
      <div><b>Three slices</b><small>Firebase + coordinator each rejected once<br>handoff passed</small></div>
      <div><b>Acceptance</b><small>pass → reject<br>pass → reject<br>pass → accept</small></div>
      <div><b>E1 + closeout</b><small>review accepted<br>resource retry<br>CI green</small></div>
    </div>
    <div class="table-wrap" style="margin-top:16px"><table>
      <thead><tr><th>Phase</th><th>Threads</th><th>Turns</th><th>Observed active time</th><th>API equivalent</th></tr></thead>
      <tbody>{phase_rows}</tbody>
    </table></div>
    <p style="margin-top:14px">Across all 45 subagent turns, the reviewed classes are: {output_counts['delivered']} delivered, {good_outcomes} accepted/passed, {bad_outcomes} rejected/failed, {output_counts['diagnosed']} diagnoses, and one preview audit. C3's first implementation-review loop alone used four writer turns and three rejects. Fresh implementation clearance still did not predict cumulative success: the next gate found eight Firebase and ten Orchestrator failures.</p>
    <p>The explicit reconciliation was the C3 re-slice: diagnosis used Sol/high; both specification reviews used Sol/xhigh. The first spec was rejected for incomplete failure coverage and an impossible intermediate gate; the second stabilized ownership into Firebase, coordinator, and handoff slices. That graph held, but Astra/xhigh final acceptance later found two additional production seams—receipt-first replay and legacy-head bootstrap—so the reconciliation was operationally useful without being sufficient.</p>
  </section>

  <section class="panel" id="resources">
    <h2>Resource policy: the conservative choices were justified</h2>
    <div class="table-wrap"><table>
      <thead><tr><th>Heavy operation</th><th>Outcome</th><th>Start GiB</th><th>Minimum GiB</th><th>Queue delay s</th><th>Observed symptoms</th></tr></thead>
      <tbody>{heavy_rows}</tbody>
    </table></div>
    <p style="margin-top:14px"><strong>Memory:</strong> the successful C3 floor reached 1.101 GiB, while E1 stopped at 1.049 GiB after severe sustained paging. That is evidence <em>for</em> retaining the 1.0 GiB preferred floor, not relaxing it. The 0.444 GiB writer breach produced no OOM but was discovered late; sampling/escalation needs tightening.</p>
    <p><strong>Concurrency:</strong> every explicit heavy observation with a queue measure reported zero causal queue delay. At the successful 2.448 GiB start of C3 validation 4, two 1.19 GiB reservations plus the 1.0 GiB preferred floor would require 3.38 GiB. This run therefore does not support two concurrent heavy profiles. Peak subagent concurrency was one, but the accepted C3 graph was mostly serial; no ready heavy work was shown waiting on the lease.</p>
  </section>

  <section class="panel" id="polling">
    <h2>Polling consumed a third of root cost</h2>
    <div class="grid stats">
      <div class="stat"><b>{waits['completed_calls']}</b><span>wait calls</span></div>
      <div class="stat"><b>{waits['timed_out_no_change_calls']}</b><span>no-change timeouts</span></div>
      <div class="stat"><b>{duration(waits['actual_wait_seconds'])}</b><span>elapsed inside wait calls</span></div>
      <div class="stat"><b>${wait_cost:.2f}</b><span>pure-wait response API equivalent</span></div>
    </div>
    <p>Pure wait responses carried {number(usage['total_tokens'])} tokens, 39.1% of root tokens. The 55-second cadence produced 144 no-change timeouts in 194 calls. Although {long_early} of {long_calls} waits requested at 300 or 600 seconds returned early on events, the active runtime instructed the root to avoid blocking waits longer than 60 seconds and user-silence intervals longer than 60 seconds. The repository's five-/ten-minute wait policy therefore conflicted with higher-priority runtime guidance; the observed 55-second default was compliance with that guidance, not failure to adopt the redesign.</p>
    <p><strong>Recommendation:</strong> keep root waits within the runtime's 60-second boundary unless that higher-priority contract changes. Reduce repeated root turns through a platform-supported persistent or recurring monitor that can wait without resampling the root, rather than by requesting longer <code>wait_agent</code> timeouts.</p>
  </section>

  <section class="panel" id="compaction">
    <h2>Compaction recovery was fast, but not always disciplined</h2>
    <div class="table-wrap"><table>
      <thead><tr><th>#</th><th>Input tokens before</th><th>Context tokens after</th><th>First recovery action</th><th>Seconds to action</th></tr></thead>
      <tbody>{compaction_rows}</tbody>
    </table></div>
    <p style="margin-top:14px">The four compactions reduced observed context from 196k–247k input tokens to 17k–19k context tokens. Median time to the first tool call was 15.4 seconds. The two compactions during the original closeout resumed through the compact recovery snapshot. The third followed a material owner scope correction with task-specific reads. The fourth, immediately after the owner asked to return to the orchestration protocol, broadly rediscovered orchestration files; that read pattern was avoidable because the run already had a sealed control capsule and known skill path.</p>
  </section>

  <section class="panel" id="owner-gates">
    <h2>Owner waiting versus agent-created blocking</h2>
    <div class="grid two">
      <div class="question"><h3>Not a blocker</h3><p>The 14h 20m gap followed a completed closeout and preceded a new owner request. It belongs outside active delivery efficiency. The early disk and Docker messages clarified policy/capacity but did not block a ready lane.</p></div>
      <div class="question"><h3>Avoidable process mismatch</h3><p>After the owner clarified that disabled sources did not meet the real objective, the root spent {duration(direct_root_seconds)} investigating activation directly before the owner explicitly rerouted it back through the orchestration protocol. The ongoing run should have treated that material scope expansion as a new graph/preview boundary immediately.</p></div>
    </div>
  </section>

  <section class="panel" id="actions">
    <h2>Recommended changes</h2>
    <ol class="actions">
      <li><strong>Checkpoint JSON compatibility:</strong> introduce one date-string-preserving JSON parser wrapper and replace all helper-local <code>ConvertFrom-Json</code> calls. Test update/revision transitions under PowerShell 7.x with a non-US culture and Windows PowerShell 5.1.</li>
      <li><strong>Disk semantics:</strong> add an absolute 20 GiB advisory threshold; keep the 14 GiB hard admission floor distinct. Make snapshot output label <em>warning</em> and <em>admission</em> separately so narrative cannot imply false causality.</li>
      <li><strong>Docker readiness:</strong> run a short read-only availability probe during initialization and record advisory status in the preview/capsule. Recheck as a hard prerequisite only before a Docker-dependent gate.</li>
      <li><strong>C3 acceptance order:</strong> require the failure-producing cumulative suites before granting fresh implementation clearance when a writer has changed shared fixtures or seam contracts. Keep final acceptance independent; its two late findings were valuable.</li>
      <li><strong>Wait-policy conflict:</strong> the repository specifies five-/ten-minute event waits, but the active runtime requires waits and user-silence intervals to remain within 60 seconds. The observed 55-second cadence followed the higher-priority constraint but caused costly repeated root turns. Resolve this through a persistent or recurring monitoring mechanism; do not treat longer <code>wait_agent</code> timeouts as an actionable repository-level fix.</li>
      <li><strong>Material goal changes:</strong> when an active orchestration goal changes from dormant closeout to live activation, immediately freeze a new architecture/preview boundary and delegate; do not spend a root-only exploration interval first.</li>
    </ol>
  </section>

  <section class="panel" id="models">
    <h2>Model and usage ledger</h2>
    <div class="table-wrap"><table>
      <thead><tr><th>Role</th><th>Model</th><th>Effort</th><th>Responses</th><th>Tokens</th><th>API equivalent</th></tr></thead>
      <tbody>{model_rows}</tbody>
    </table></div>
    <p style="margin-top:14px">Pricing uses official OpenAI list prices captured as of 2026-09-16. It is a comparative API equivalent, not a Codex subscription bill. The largest subagent cost was Astra/xhigh at $33.47; it supplied three independent C3 acceptance passes, two of which found material defects, plus E1 review/acceptance work.</p>
  </section>

  <section class="panel" id="coverage">
    <h2>Question coverage</h2>
    <div class="table-wrap"><table>
      <thead><tr><th>Focus</th><th>Status</th><th>Finding</th></tr></thead>
      <tbody>{focus_rows}</tbody>
    </table></div>
  </section>

  <section class="panel method" id="method">
    <h2>Boundary, privacy, and sources</h2>
    <p>Root thread <code>{manifest['analysis']['root_thread_id']}</code>; one exact root log; cutoff <code>{manifest['analysis']['event_cutoff_at']}</code>; Git range <code>{manifest['analysis']['repository']['base_commit'][:12]}…{manifest['analysis']['repository']['final_commit'][:12]}</code>; timezone Europe/Berlin. The root session continued after the cutoff. Complete prompts, reasoning, tool output, user-home paths, secrets, and prediction payloads are excluded. Bounded excerpts are disabled. Final-answer hashes and outcome classes retain review accountability without publishing message text.</p>
    <p>Agent intervals do not overlap in this run, but remain observations rather than timesheets. Resource readings are discrete. Polling token attribution uses cumulative usage deltas for responses containing only <code>wait_agent</code>. Git line counts describe scope, not value. Causal claims are limited to explicit queue-delay and gate evidence.</p>
    <p>Sources: <a href="{external['github_pull_request']['url']}">PR #111</a> · {link('.agents/skills/orchestrate/scripts/Set-OrchestrationCheckpoint.ps1','checkpoint helper')} · {link('.agents/skills/orchestrate/scripts/Get-OrchestrationResourceSnapshot.ps1','resource admission')} · {link('.agents/skills/orchestrate/resources/resource-policy.json','resource policy')} · {link('tests/FirebaseAdapter.Tests/README.md','Firestore emulator requirements')} · <a href="{REPO_URL}/tree/main/docs/codex/p1-04-activation-orchestration-session-analysis">normalized source</a> · <a href="../p1-04-05-orchestration/index.html">previous P1-04/P1-05 analysis</a>.</p>
    <p>Official OpenAI pricing evidence: <a href="https://developers.openai.com/api/docs/models/gpt-5.6-luna">Luna</a> · <a href="https://developers.openai.com/api/docs/models/gpt-5.6-terra">Terra</a> · <a href="https://developers.openai.com/api/docs/models/gpt-5.6-sol">Sol</a> · <a href="https://developers.openai.com/api/docs/models/gpt-6-astra">Astra</a>.</p>
  </section>
  <footer>Generated from a manifest-bound, cutoff-locked local transcript family. Published artifacts contain normalized operational facts only.</footer>
</main>
</body>
</html>
"""
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(html_text, encoding="utf-8", newline="\n")
    print(f"Wrote {OUTPUT.relative_to(REPO).as_posix()}")


if __name__ == "__main__":
    main()
