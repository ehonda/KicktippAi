# P1 context-refresh orchestration investigation

**Investigated session:** `01a07449-de77-7ae0-ac4a-8f5330c43121`

**Frozen interval:** 2026-09-06 03:17:19 to 22:21:56 Europe/Berlin

**Repository boundary:** `a1e333f..c99e163`; common-runtime tip `6cce46a`

**Status at cutoff:** active

**Interactive visualization:** [open the self-contained Pages report](../../../session-analysis/p1-context-refresh/index.html)

## Executive conclusion

The recovery capsule and hooks did their core job: all five root compactions
received a validated capsule injection, and the run did not lose its identity or
frozen Git state. The orchestration around them was inefficient. The full recovery
preflight repeatedly re-read instructions, plans, ADRs, skills, scripts, and some
unrelated task history. Recovery consumed 33m41s in total. At the 22:00 Berlin
compaction, the root made 40 custom tool calls and resumed orchestration 8m15s later
with 64.6% of its context already occupied. The hook injection was only 6,043
characters; the post-hook reads produced 559,510 serialized JSON characters.

Startup had a separate graph failure. The root correctly waited for trust in the
exact hook definition, costing 3m29.7s. It then stopped at 03:47 after treating
P1-04 evidence as owner-only and P1-05 as dependent on P1-04. Both assumptions
were corrected by the owner after returning at 09:17. The resulting 5h29m51s wait
was preventable.

After resume, evidence and planning moved quickly. Independent P1-04 and P1-05
evidence lanes overlapped, and three plan commits landed between 10:38 and 11:16.
Execution then collapsed into a single common-runtime gate. Between 11:27 and the
cutoff, that lane accumulated 18 local commits over 20 files and +6,121/-5 lines.
Seventeen fresh reviews all blocked. Neither task-specific writer started and
neither P1 task reached `Complete`.

The bottleneck was not the eight-thread limit. The run peaked at four concurrent
subagents, logged no thread-limit failures, and spent only 5.23% of subagent-active
wall time with two or more agents active. The common acceptance dependency and a
2/2 writable-worktree reservation—one slot held by a dirty predecessor P1-10
worktree—were the effective constraints.

## Headline evidence

| Dimension | Frozen result |
|---|---:|
| Wall span | 19h04m37s |
| Preventable owner stall | 5h29m51s |
| Pause-adjusted span | 13h34m45s |
| Integrated planning commits | 3 |
| Common-runtime commits | 18 |
| Common-runtime review cycles | 17 blocked / 17 |
| P1-04 / P1-05 task files completed | 0 / 2 |
| Real subagent threads / turns | 52 / 62 |
| Maximum active subagents | 4 |
| Two-plus share of subagent-active time | 5.23% |
| Root compactions / validated injections | 5 / 5 |
| Total recovery time | 33m41s |
| Median context occupied at resume | 57.97% |
| Maximum context occupied at resume | 64.60% |
| Frozen `preview.md` | 67,073 bytes / 663 lines |
| Heavy admission samples denied | 13 / 45 |
| Logged tokens | 468.1M |
| API list-price equivalent | $264.46 |

## Design and plan agents

Architecture, specification-review, and planning-writer roles used 12 threads, 16
turns, 2h17m of overlapping worker time, 43.76M tokens, and $29.20 in public API
list-price equivalent. The much larger cost surface was the common implementation
loop: writer and reviewer roles used 230.1M tokens, 77.4% of subagent tokens.

An Astra pilot is still worthwhile, but it should be narrow. Use
`gpt-6-astra/high` for one phase-wide architecture lead and retain a different
`gpt-5.6-sol/xhigh` specification reviewer. Architecture alone used two threads,
three turns, 9.80M tokens, and $6.90 equivalent. At identical token counts Astra
would cost $17.25, a $10.35 premium; actual token behavior is unknown. The pilot
should measure first-pass specification acceptance, downstream review rounds, wall
time, tokens, and equivalent price.

This boundary fits current official guidance: Astra is the most capable option for
complex work, but may ask more clarifying questions and is especially sensitive to
instructions and skills. That makes it a plausible architecture synthesizer but a
riskier immediate replacement for the root control plane or independent final gate.
See the [official model-selection guide](https://developers.openai.com/api/docs/guides/latest-model),
[Astra pricing](https://developers.openai.com/api/docs/models/gpt-6-astra), and
[Sol pricing](https://developers.openai.com/api/docs/models/gpt-5.6-sol).

## Recovery diagnosis

The 22:00 Berlin recovery read 25 normalized paths. They included the root and
nested agent instructions, orchestration and onboarding skills, capsule and
preview, three ADRs, the active design and execution packet, resource/capsule
scripts, and unrelated P0/P1 task records. Several large documents were then read
again in chunks. The mandatory full reread in `AGENTS.md` therefore defeated the
capsule's intended minimality.

Use two recovery modes:

1. **Hot recovery:** when session identity, capsule checksum,
   `instruction_packet_sha256`, and active-contract SHA match, run one deterministic
   helper returning compact live agent, Git/worktree, resource, CI, and lease state.
   Read only the active lane contract if required, then resume scheduling.
2. **Cold recovery:** perform the current full reconstruction only for a missing or
   corrupt capsule, changed instructions, material re-freeze, or uncertain
   ownership.

Adding `PostCompact` would not address this evidence. The existing
`SessionStart:compact` hook already injected the right state. The excess work
followed the injection. The [official hooks documentation](https://learn.chatgpt.com/docs/hooks)
also establishes that project hooks require trust in the exact definition, so the
startup optimization should produce positive trust evidence rather than remove the
gate.

## Preview and capsule churn

At the cutoff, `preview.md` was 67,073 bytes, 663 lines, and 17 level-two sections.
It received 22 patches with 667 observed added lines and only 21 deleted lines. The
capsule received 79 patches and 77 observed seal invocations. These are signs that
the artifacts were functioning as a shadow transcript rather than a compact frozen
graph and durability snapshot.

Make `preview.md` a replace-in-place current-graph index capped around 8–12 KiB.
Link exact tracked contracts and move review/event history to a separate evidence
stream. Capsule writes should remain strictly durability-barrier based.

## Memory-floor calibration

The 1.10 GiB floor denied 13 of 45 heavy-admission samples. The minimum admitted
value was 1.11 GiB and the maximum denied value was 1.09 GiB, proving exact policy
enforcement but not that the threshold is optimal. Thirteen samples were admitted
below the 1.50 GiB warning line.

Only one closed denial can be causally tied to ready heavy work: it cleared after
55 seconds. A second denial had remained open for at least 183 seconds at the
cutoff. Other denial-to-admission gaps include owner waits, reviews, and post-job
measurements and are not valid throughput-loss estimates.

Keep 1.10 GiB provisionally. For each heavy lease record operation family,
start/min/post available memory, duration, exit/outcome, paging or OOM symptoms,
and actual queue delay. After a meaningful sample, estimate failure and slowdown
risk by starting-memory band and move the floor in 0.10 GiB steps. Do not tune from
sparse snapshots alone.

## Recommended workflow changes

1. Add hot/cold recovery and an instruction-packet hash.
2. Replace `preview.md` in place and cap the current graph around 8–12 KiB.
3. Split the common runtime into separately acceptable milestones: bundle/canonical
   selection, coordinator state, persistence/replay, and health/receipts.
4. Recover, publish, or explicitly reserve dirty predecessor worktrees during
   intake before freezing writer capacity.
5. Retain one writer for a bounded correction loop and optionally one incremental
   reviewer; use a fresh independent reviewer for milestone acceptance.
6. Pilot Astra/high only for the architecture lead, with outcome metrics.
7. Calibrate the memory floor from heavy-operation outcomes and causal queue delay.
8. Let a trusted kickoff hook inject positive evidence of the exact hook hash.

## Reproduction

```powershell
uv --cache-dir .uv-cache run --no-project --with tzdata python docs/codex/p1-context-refresh-orchestration-investigation/analyze.py --output-dir docs/codex/p1-context-refresh-orchestration-investigation/data --repo . --quiet
uv --cache-dir .uv-cache run --no-project python docs/codex/p1-context-refresh-orchestration-investigation/enrich.py --analysis docs/codex/p1-context-refresh-orchestration-investigation/data/analysis.json --output-dir docs/codex/p1-context-refresh-orchestration-investigation/data --repo .
uv --cache-dir .uv-cache run --no-project python docs/codex/p1-context-refresh-orchestration-investigation/verify_snapshot.py
uv --cache-dir .uv-cache run --no-project python docs/codex/p1-context-refresh-orchestration-investigation/build_html.py
```

The normalized evidence is in [`data/`](data/). Full prompts, reasoning, secrets,
and complete message bodies are not checked in. Agent durations overlap, review
keyword tags overlap, and API pricing is an equivalence estimate rather than a
Codex subscription charge.
