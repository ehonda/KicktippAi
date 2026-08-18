# Codex subagent orchestration investigation

- Status: Investigation and recommendation; not an accepted policy change
- Last updated: 2026-08-18
- Related policy: [Bundesliga 2026/27 execution strategy](execution-strategy.md) and [ADR-0009](decisions/0009-bounded-orchestration-and-hybrid-git.md)
- Live-session snapshot: 2026-08-18 23:28 CEST

## Conclusion

Do not change the default from two task agents to four parallel subagents yet.

At the snapshot, the local Codex runtime exposed four concurrent agent slots including the root, so it could schedule at most three subagents at once, not four. More importantly, the live session used two concurrent subagents for only 7.0 of 289.8 subagent-active minutes and never used three. Its measured bottleneck is dependency sequencing and root-mediated coordination, not the two-agent policy ceiling.

The next improvement should be to keep two useful lanes occupied: one sole writer for the current task and one read-only agent preparing the next independent task or reconciling evidence. If that produces a measurable wall-time improvement without more rework, trial a third **read-only** subagent for a bounded wave. Do not add concurrent writers in the same checkout, and do not raise the accepted two-writer/worktree limit without a superseding ADR and isolated worktrees.

This agrees with the [official Codex subagent guidance](https://learn.chatgpt.com/docs/agent-configuration/subagents): parallel agents are a good starting point for independent, read-heavy exploration, tests, triage, and summarization, while parallel write-heavy work needs more care because it increases conflict and coordination overhead.

## Scope and method

The investigation compared two local Codex transcript families stored under `%USERPROFILE%\.codex\sessions`:

- completed session `01a009ad-301d-7de3-925d-b1e7b052e2d2`, run on 2026-08-16;
- ongoing session `01a015ac-9217-7f23-9156-6c1e30f1a853`, sampled through 2026-08-18 23:28 CEST.

Real subagent threads were identified from `session_meta.payload.source.subagent.thread_spawn.parent_thread_id`. Guardian threads and unrelated CLI sessions were excluded. Assignment counts come from root `spawn_agent` and `followup_task` calls. Turn completion, duration, and final outcome come from child `task_started` and `task_complete` events. Interrupted inherited root turns in the first full-history forks were excluded from worker-time calculations.

Prompt and message bodies in parent collaboration calls are encrypted in the transcript. The analysis can still recover the sender, target, timing, model, reasoning effort, tool action, final plaintext task summary, and Git/CI outcome. Task descriptions below are therefore reconstructed from those observable fields and repository history, not from decrypted prompt text.

Token figures are raw logged model tokens for the root and real subagents, including cached input. They are useful as a relative workload signal, not as a normalized quality or billing comparison. The sessions implemented different tasks, so their throughput is not directly comparable.

## Refreshed comparison

| Metric | Completed 2026-08-16 session | Ongoing 2026-08-18 session at cutoff |
|---|---:|---:|
| Real subagent threads | 2 | 18 |
| Assigned subagent turns | 28 | 45 |
| Completed turns | 22 | 43 |
| Interrupted or aborted turns | 6 | 1 |
| Active turns at cutoff | 0 | 1 |
| Aggregate subagent worker time | 276.8 min | 296.8 min |
| Wall time with at least one subagent active | 175.6 min | 289.8 min |
| Wall time with at least two subagents active | 101.2 min | 7.0 min |
| Average concurrency while any subagent was active | 1.58 | 1.02 |
| Maximum observed concurrent subagents | 2 | 2 |
| Root `wait_agent` calls | 171 | 270 |
| Root mid-turn `send_message` calls | 54 | 50 |
| Root `list_agents` calls | 21 | 15 |
| Root shell/tool execution calls | 237 | 64 |
| Inbound agent messages and final reports | 94 | 112 |
| Raw logged root plus subagent tokens | 212.2M | 165.4M |
| Cached share of input tokens | 97.6% | 97.5% |

The refreshed live snapshot adds seven agent threads, 17 completed turns, 136.3 worker-minutes, and two completed P0 collectors since the earlier sample. It also adds about 100 root waits. Despite that activity, concurrency of at least two increased by only about 2.6 minutes.

The completed session used two long-lived, strongest-tier generalists. They overlapped heavily, drifted between audit, implementation, review, Git, and CI work, and eventually exhausted the weekly allowance. It completed P0-01, P0-02, P0-03, P0-04, P0-07, P0-08, and P0-10 with green CI, but six assigned turns were interrupted and about 24 minutes placed two implementation-scoped turns in the same checkout at once.

The ongoing session uses fresh, role-oriented threads and leaves much more execution work with the subagents. Four scoped commits reached `origin/main` with exact-SHA green CI by the cutoff:

- `4284f49` — atomic Core document-publication contract;
- `fbbbd86` — atomic Firestore publication boundary;
- `343929f` — P0-11 Club Elo collector;
- `8e9c6c9` — P0-09 roster collector.

P0-11 and P0-09 are complete. P0-12 was in progress with its completion writer active at the cutoff.

## Agents and assignments in the ongoing session

The turn count excludes the inherited root turn visible in the first two full-history forks.

| Stage | Agent path and nickname | Model / effort by turn | Turns | Assignment and observed outcome |
|---|---|---|---:|---|
| Publication | `/root/publication_audit` — Copernicus | Sol / xhigh | 1 | Audited the proposed shared publication contract and identified ADR and mutability conflicts. |
| Publication | `/root/publication_contract_impl` — Banach | Sol / xhigh | 1 aborted | Initially assigned implementation at the orchestration tier; interrupted when the user corrected model allocation. |
| Publication | `/root/publication_contract_writer` — Confucius | Terra / high | 7 | Implemented Core and Firestore publication slices, remediated review findings, committed, and pushed `4284f49` and `fbbbd86`. |
| Policy | `/root/orchestration_policy_docs` — Franklin | GPT-5.4 / medium | 1 | Clarified the control-plane, capability-tier, and one-writer-per-worktree policy. |
| Publication | `/root/publication_contract_review` — Hooke | Sol / high | 5 | Repeated independent review of scope, immutability, CAS, reserved keys, snapshot identity, and compatibility. |
| CI | `/root/ci_reconcile_4284f49` — Volta | Luna / low ×2; Luna / xhigh ×1 | 3 | Read-only exact-SHA CI reconciliation for the publication and Club Elo commits. |
| Club Elo | `/root/club_elo_cli_audit` — Linnaeus | Luna / medium | 1 | Read-only P0-11 contract and implementation-seam audit. |
| Club Elo | `/root/club_elo_collector_writer` — Laplace | Terra / high | 4 | Implemented, remediated, validated, committed, and pushed P0-11 as `343929f`. |
| Club Elo | `/root/club_elo_review` — Socrates | Sol / high | 3 | Reviewed canonical CSV, metadata, build validation, and emulator coverage until findings closed. |
| Rosters | `/root/roster_reuse_audit` — Popper | Luna / medium | 1 | Audited reusable WM26/DuckDB roster seams without editing. |
| Rosters | `/root/roster_collector_writer` — Hypatia | Terra / high | 3 | Implemented the initial P0-09 collector and part of its review remediation. |
| Rosters | `/root/roster_review` — Galileo | Sol / high ×1; Sol / xhigh ×3 | 4 | Ran four read-only review passes across effective dates, enrichment, diagnostics, and integrated coverage. |
| Rosters | `/root/roster_matrix_writer` — Wegener | Terra / high ×2; Terra / xhigh ×2 | 4 | Finished the fixture matrix and remediation, then committed and pushed P0-09 as `8e9c6c9`. |
| Triage and CI | `/root/next_task_triage` — Peirce | Luna / low ×1; Luna / xhigh ×1 | 2 | Selected the next dependency-safe task, then was reused for exact-SHA P0-09 CI reconciliation. |
| P0-12 | `/root/p0_12_contract_audit` — Hubble | Terra / medium | 1 | Read-only audit of task/ADR acceptance requirements. |
| P0-12 | `/root/p0_12_code_audit` — Nash | Terra / medium | 1 | Read-only audit of the code and test seams. |
| P0-12 | `/root/p0_12_writer` — Zeno | Terra / high | 2 | Implemented most of P0-12, then returned the remaining gaps without committing. |
| P0-12 | `/root/p0_12_completion_writer` — Darwin | Terra / high | 1 active | Continued the remaining P0-12 implementation at the snapshot cutoff. |

## How the agents interact

The observed topology is entirely hub-and-spoke:

`root -> audit -> root -> writer <-> root <-> reviewer -> writer -> root -> commit/push -> CI reconciler -> root`

The root created 18 threads and reused them for 27 follow-up turns. It also sent 50 mid-turn steering messages. Subagents sent 69 explicit messages, all to `/root`; none targeted another subagent. Together with 43 automatic final reports, the root received 112 agent messages. The root therefore owns task decomposition, every writer/reviewer handoff, remediation routing, Git sequencing, CI routing, and the durable plan update.

That control plane works: reviewers found material defects and writers closed them before release. Examples include loaded-publication scope and integrity checks, mutable retained data, CAS ordering, reserved-key ownership, snapshot identity, canonical Club Elo rendering, build-time validation, roster effective dates, nondeterministic valuation ties, enrichment source coupling, and missing integrated fixtures. The exact-SHA CI checks were green for every pushed commit at the cutoff.

The cost is serial coordination. The root called `wait_agent` 270 times, about six waits per assigned turn. Only seven minutes contained two active subagents, primarily short overlaps between an audit and writer, between two P0-12 audits, or at a writer/reviewer boundary. No interval contained three active subagents. Most review work correctly waited for a stable writer checkpoint, but next-task preparation also usually waited instead of overlapping the current writer.

## What works well

- Role-specialized agents keep the root out of routine implementation. Root execution calls fell from 237 in the completed session to 64 in the larger ongoing run.
- Balanced writers plus strong risk-based reviewers produced substantive fixes rather than ceremonial review.
- Fresh threads bound context to a task or role. The ongoing run has more completed turns and agent threads but fewer raw logged tokens than the completed two-generalist run, although task differences prevent a normalized efficiency claim.
- Read-only audit before implementation worked well for Club Elo, rosters, and P0-12. It exposed contracts and reusable seams without adding checkout conflicts.
- Exact-SHA, read-only CI reconciliation prevented stale green runs from being mistaken for validation of the current commit.
- The single-writer primary checkout avoided the overlapping write risk seen in the completed session.

## What should improve

### Keep two lanes busy before raising the cap

The current effective concurrency is 1.02, and only 2.4% of active wall time has two subagents working. Raising the configured ceiling cannot help until the orchestrator schedules independent work earlier.

Use this default rolling topology:

1. **Current-task writer:** one Terra/high writer owns the stable task scope and checkout changes.
2. **Read-only look-ahead:** one Luna/medium or Terra/medium agent audits the next dependency-safe task, linked ADRs, code seams, and test commands while the writer works.
3. When the writer reaches a stable checkpoint, replace the look-ahead lane with a Sol/high reviewer only when the artifact risk justifies independent review.
4. After push, reuse the read-only lane for Luna/low exact-SHA CI reconciliation while the writer or look-ahead agent starts independent preparation.

Review of the same diff and CI of its pushed commit remain dependency-serial. The parallel opportunity is preparing the *next* task, not pretending those gates can run early.

### Reduce root message-bus work

- Give each assignment a bounded contract: owned paths, forbidden paths, required evidence, allowed writes, completion criteria, and whether commit/push is authorized.
- Require compact final reports with fixed fields: status, changed paths, validations, open findings, commit/SHA, and next dependency.
- Prefer completion/message-driven waits and batch status checks. Avoid repeated short waits and `list_agents` polling when no decision can be made from unchanged state.
- Let a reviewer report a numbered finding set once. Route one consolidated remediation turn to the same writer, then re-review only reopened findings.
- Keep orchestration decisions in task files and ADRs so a new thread does not need the root to replay historical context.

### Keep role and model allocation stable

The initial Banach writer was overpowered and aborted. Later reuse also drifted CI/triage and roster turns from low or high effort to xhigh. Use fresh fixed-role agents when the task class changes, and reuse a writer or reviewer only for concrete remediation within the same task checkpoint. Record the intended model and effort in the assignment and verify it from the thread before expensive work starts.

### Avoid writer handoffs inside one task

P0-09 moved from Hypatia to Wegener, and P0-12 moved from Zeno to Darwin. A handoff can be justified when a writer stalls or the remaining slice is independently bounded, but it adds context reconstruction and root coordination. Prefer keeping one task writer through implementation, review remediation, validation, and scoped commit. If a handoff is necessary, require the outgoing writer to leave a structured gap list and exact dirty-path ownership.

## Feasibility of more parallel subagents

### Four subagents

Four simultaneous subagents are not feasible in the runtime observed for this investigation because the root consumes one of four available concurrent slots. Even if a future runtime exposes root plus four children, four write-capable agents would conflict with ADR-0009, the shared-checkout safety rule, the slow-machine constraint, serialized heavy commands, and the official caution about parallel writes.

Four logical lanes can still exist as a queue—writer, next-task audit, reviewer, and CI reconciler—but reviewer and CI are gated by the writer and normally will not all execute simultaneously. Calling that a four-agent pipeline would overstate usable parallelism.

### Three subagents

A maximum of three simultaneous subagents is technically possible in the current slot budget, but it should be an experiment, not the new default. The safe shape is:

- one writer;
- one independent next-task audit;
- one second read-only activity, such as a separate contract/code audit or remote CI reconciliation;
- the root as control plane.

Do not run two local heavy test/build commands concurrently. Do not let the extra read-only agents edit planning or source files. A second writer is allowed only under the existing ADR's isolated-worktree, non-overlapping-ownership conditions and still counts against its two-task-agent limit. Adopting three task agents as a standing policy would require a superseding ADR.

## Recommended experiment and decision gate

Run the next dependency-safe wave with the existing two-task-agent limit but deliberately overlap the current writer with next-task read-only preparation. Record:

- task start, stable-review checkpoint, commit, push, and exact-SHA CI times;
- minutes with one, two, and three subagents active;
- root spawn, follow-up, steering, wait, and status-poll counts;
- writer handoffs and review/remediation cycles;
- raw/cached tokens by role;
- test conflicts, checkout conflicts, and CI result.

The current live-session baseline is 2.4% dual-subagent utilization, 1.02 average concurrency while active, six root waits per assignment, one aborted turn, and green exact-SHA CI for all four pushed commits.

Only if deliberate two-lane scheduling still leaves independent ready work queued should the project trial a third read-only subagent. Adopt that topology through a superseding ADR only if it reduces wave wall time without increasing checkout conflicts, heavy-command contention, open review findings at commit time, CI failures, or token use enough to threaten the weekly allowance. There is no evidence yet supporting four simultaneous subagents or four writers.
