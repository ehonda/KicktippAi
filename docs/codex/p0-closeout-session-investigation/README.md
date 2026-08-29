# P0 closeout Codex session investigation

**Investigated session:** `01a02485-f0b3-7241-a6c7-c6f58fe44509`

**Local interval:** 2026-08-21 15:34 CEST to 2026-08-28 09:02 CEST

**Repository interval:** `6d0fca3..2c824c8`

**Analysis generated:** 2026-08-29

**Interactive visualization:** [open the self-contained Pages report](../../../session-analysis/p0-closeout/index.html)

## Executive conclusion

The session achieved an unusually large, deadline-critical outcome: it moved 22 concrete P0 task files to `Complete`, left all 32 instantiated P0 tasks complete, integrated 124 first-parent commits, activated the deliberately gated production schedule, and audited the first natural 16-job run to green. It did so without silently promoting the plumbing model or bypassing owner-controlled production and experiment gates.

The orchestration was effective but expensive. The root plus its real task agents consumed 2.975 billion logged tokens, 97.7% of input tokens were cached, and the public API list-price equivalent is **$1,582.11**. This is not an actual Codex subscription charge. Every real task thread used `gpt-5.6-sol` at `xhigh`; the accepted model-tiering guidance was not applied. The root alone represented 977.2 million tokens and $467.98 of the equivalent cost.

Delegation improved materially over the preceding orchestration baseline. Useful subagent work overlapped for 24h52m, compared with 7 minutes in the earlier session, and average concurrency while any subagent was active rose from 1.02 to 1.51. The session reached the observed ceiling of three simultaneous subagents. The tradeoff was a very noisy control plane: 102 realized descendant threads, 3,527 root `wait_agent` calls, 830 messages to running agents, 330 follow-up assignments, 235 agent-list polls, 2,784 root execution-tool calls, and 32 root compactions.

The user sent 45 genuine messages including the kickoff, hence **44 interventions after work began**, clustered into 25 interaction bursts. The interventions were not mostly implementation instructions: 24 supplied authorization, external state, budget, or stop/go decisions; 16 corrected or clarified scope; and four asked about status or process. Several corrections materially changed the outcome, especially the worktree-locator fix, removal of WM26 assumptions, restoration of task-agent ownership for P0-06, the candidate-model surface, and the final schedule topology.

## Headline evidence

| Dimension | Result |
|---|---:|
| Session wall span | 6d 17h 28m |
| Sum of 39 root-turn durations | 64h 19m |
| Real descendant task-agent threads | 102 |
| Task-agent turns | 414: 402 completed, 12 aborted, 0 left incomplete |
| Aggregate task-agent worker time | 86h 45m |
| Maximum simultaneous subagents | 3 |
| Time with at least two subagents active | 24h 52m / 43.3% of subagent-active wall time |
| Time with three subagents active | 4h 24m / 7.7% of subagent-active wall time |
| Average concurrency while subagents were active | 1.51 |
| User messages | 45 including kickoff; 44 later interventions; 25 bursts |
| Root assistant-message records | 1,600 |
| Root compactions | 32 |
| Task-family logged tokens | 2,975,403,460 |
| Task-family API list-price equivalent | $1,582.11 |
| Auto-review guardian overhead | 117 internal threads, 3,176 turns, 368,677,675 unpriced tokens |
| P0 task records completed in this session | 22 |
| First-parent commits | 124 |
| Repository diff over the session | 372 files; +52,110 / -2,859 lines |

The six-day wall span includes the weekend, usage resets, owner gates, hosted-service work, GitHub Actions queues, and the wait for a natural schedule occurrence. Root and worker “active time” is the sum of transcript turn durations, not keyboard time; it includes tool execution and waits inside open turns.

## Scope and method

The source of truth is the root JSONL transcript under the local Codex session store. A thread belongs to the task family only when its `session_meta` records a recursive `thread_spawn.parent_thread_id` relationship to the root. This avoids counting thread IDs merely mentioned in messages. Internal approval-review sessions are identified separately as guardians and are never described as user-spawned task agents.

The extractor also reads Git history between the exact starting and ending commits. It records the last transition of each task document from a non-complete state to `Complete`, the first-parent commit history, model contexts, task-turn timings, token-counter segments, collaboration calls, and user-message metadata. Full prompts, reasoning, tool output, and complete user messages are not copied into the repository; excerpts are length-limited and hashed.

The normalized source is [`data/analysis.json`](data/analysis.json). The accompanying CSVs are flattened views for a future static or interactive HTML report. [`data/README.md`](data/README.md) documents the schemas and reproduction command.

Important limitations:

- Task prompts in collaboration calls are encrypted in the transcript. Task attribution therefore combines agent paths, final result summaries, and repository history.
- A reused agent can cross task boundaries. The task timing table is an evidence envelope, not timesheet accounting.
- Agent worker durations overlap and include waits. They are not additive to root duration or wall duration.
- Git completion time is exact for the task ledger, but later review or repair can legitimately follow it.
- The public API pricing calculation is an equivalence estimate, not an invoice or statement about how a Codex plan is billed.

## User intervention analysis

The root transcript contains 45 genuine user messages. Excluding the initial request leaves 44 interventions. A 10-minute gap groups these into 25 bursts, which better represents how often the user returned to the session than counting rapid follow-up messages separately.

| Hand-annotated purpose | Messages | Interpretation |
|---|---:|---|
| Authorization or external control/unblock | 24 | Credentials were available, budgets changed, hosted writes or runs were authorized, a slow run was stopped, or owner gates were decided. |
| Scope correction or clarification | 16 | The user corrected an assumption, supplied missing product intent, or changed the requested experiment/schedule surface. |
| Status or process question | 4 | The user asked about blockers, delegation, status, or ETA. |
| Initial kickoff | 1 | Close P0 using the recently adopted orchestration improvements. |

| Local date | Genuine user messages |
|---|---:|
| 2026-08-21 | 9 |
| 2026-08-25 | 10 |
| 2026-08-26 | 13 |
| 2026-08-27 | 8 |
| 2026-08-28 | 5 |

The transcript also contains 28 automatic goal-continuation messages and six injected repository/environment contexts. Those are excluded from the 45-message count.

The highest-impact interventions were:

1. **Worktree execution context:** the user recognized that the original-checkout locator was missing and suggested that the worktree helper should install it automatically. The agents converted this into durable automation and rollback tests.
2. **Competition scope:** the user rejected a WM26 validation path in a Bundesliga-only community and supplied the correct development-credential interpretation.
3. **Delegation discipline:** the user noticed the root doing P0-06 work that belonged to its task agent and explicitly required a protocol recheck.
4. **Experiment definition and budget:** the user supplied the actual candidate matrix, added budget, stopped an excessively slow Luna/`max` attempt, and added Sol/`xhigh` and then Sol/`max` evidence under explicit gates.
5. **Production topology:** the user questioned why `pes-squad` lacked a schedule, asked whether secondaries ran after the primary, and required context collection to precede matchday execution. That led to the centralized, dependency-ordered schedule rather than duplicate leaf schedules.

The detailed, time-stamped record is in [`data/user-messages.csv`](data/user-messages.csv). Its categories are analytical annotations; the timestamps, hashes, and message counts are transcript facts.

## Agents, models, tokens, and cost

The root made 105 `spawn_agent` calls and 102 descendant task-agent logs materialized. The transcript metadata does not establish why the other three calls did not produce descendants, so this report distinguishes spawn attempts from realized threads. Of the realized agents, 101 were direct children and one was a depth-two child.

Every root and task-agent model context was `gpt-5.6-sol` with `xhigh` reasoning. References to Luna, Terra, Sol/`high`, and Sol/`max` elsewhere in the session describe models exercised by the KicktippAi application experiments, not Codex subagent models.

| Cohort | Input tokens | Cached input | Cache share | Output tokens | Total tokens | API equivalent |
|---|---:|---:|---:|---:|---:|---:|
| Root orchestrator | 975,947,047 | 961,524,864 | 98.52% | 1,283,836 | 977,230,883 | $467.98 |
| 102 real task agents | 1,991,948,736 | 1,938,370,688 | 97.31% | 6,223,841 | 1,998,172,577 | $1,114.14 |
| **Task family** | **2,967,895,783** | **2,899,895,552** | **97.71%** | **7,507,677** | **2,975,403,460** | **$1,582.11** |
| Internal auto-review guardians | 368,413,756 | 300,495,104 | 81.56% | 263,919 | 368,677,675 | Not publicly priced |

Reasoning-output tokens are a subset of output tokens and are not added to the total or priced twice. The task family recorded 2,456,070 reasoning-output tokens; guardians recorded 81,374.

The estimate applies the official `gpt-5.6-sol` rates current on 2026-08-29: $4.00 per million uncached input tokens, $0.40 per million cached input tokens, and $20.00 per million output tokens. No task-family response exceeded the 272,000-input-token long-context threshold; the maximum was 237,299, so the normal rates apply. See the [official GPT-5.6 Sol model page](https://developers.openai.com/api/docs/models/gpt-5.6-sol).

The 117 `codex-auto-review`/`low` guardian threads are an additional 368.7 million tokens. That internal model has no public API list price, so assigning it a dollar amount would be invented. Including it only as usage volume brings the observed total to 3,344,081,135 tokens.

The largest task-agent consumers illustrate both the workload concentration and role drift:

| Agent path | Turns | Total tokens | API equivalent |
|---|---:|---:|---:|
| `/root/p0_06_luna_cost` | 38 | 183,588,789 | $104.43 |
| `/root/p0_code_seams` | 15 | 181,481,360 | $98.16 |
| `/root/p0_20_arena_validation` | 30 | 165,051,251 | $85.21 |
| `/root/p0_23_quality_live` | 4 | 125,804,021 | $63.99 |
| `/root/p0_05_prompt_route` | 11 | 103,658,890 | $56.55 |
| `/root/p0_status_audit` | 37 | 96,908,439 | $54.52 |

Several of these paths handled follow-up work outside their original task label. That reuse saved spawn setup but made ownership and later attribution harder.

## How the orchestrator performed

| Dimension | Evidence | Assessment |
|---|---|---|
| Outcome | All concrete P0 records complete, exact-head CI green, first natural schedule run audited, no helper worktrees left | Excellent delivery under a real deadline |
| Safety and governance | Production writes, schedules, hosted prompt changes, experiment spend, and model selection remained behind explicit owner gates | Strong |
| Defect discovery | Independent reviews repeatedly found correctness, provenance, Unicode, workflow, and live-validation defects before closeout | Strong |
| Parallelism | Average active concurrency 1.51; two-plus agents active for 43.3%; maximum three | Major improvement over the prior baseline |
| Delegation discipline | 102 task threads did most worker time, but the root still absorbed substantial P0-06 implementation/analysis until corrected | Mixed |
| Model allocation | Root and every child used the strongest `Sol/xhigh` configuration | Poor cost-to-task matching |
| Coordination efficiency | 3,527 waits, 830 messages, 330 follow-ups, 235 polls, 12 interrupts, and 32 compactions | Too chatty and state-heavy |
| Auditability | Exact session family, commits, task transitions, run evidence, and costs are recoverable | Strong after post-hoc extraction; weak during the run |

The root performed 39 turns totaling 64h19m and produced 1,600 assistant-message records. Its collaboration calls were:

| Call | Count |
|---|---:|
| `wait_agent` | 3,527 |
| `send_message` | 830 |
| `followup_task` | 330 |
| `list_agents` | 235 |
| `spawn_agent` | 105 |
| `interrupt_agent` | 12 |

It also issued 2,784 execution-tool calls. Parsed inner calls include 2,587 shell executions, 306 terminal-session writes, 83 plan updates, 33 patches, and 30 web operations. These counts explain why the root consumed nearly one billion tokens despite delegating much of the implementation.

### Comparison with the pre-closeout baseline

The accepted [subagent orchestration investigation](../../../plans/bundesliga-2026-27/subagent-orchestration-investigation.md) measured the preceding ongoing session at 18 real threads and 45 assigned turns. The tasks and duration differ, so this is a behavior comparison, not a productivity benchmark.

| Metric | Earlier baseline | P0 closeout |
|---|---:|---:|
| Real subagent threads | 18 | 102 |
| Assigned turns | 45 | 414 |
| Aggregate worker time | 4h57m | 86h45m |
| Wall time with any subagent | 4h50m | 57h29m |
| Wall time with at least two | 7m / 2.4% | 24h52m / 43.3% |
| Average active concurrency | 1.02 | 1.51 |
| Maximum concurrency | 2 | 3 |
| Root waits per assigned turn | about 6.0 | 8.52 |
| Root + subagent logged tokens | 165.4M | 2,975.4M |

The intended parallelism improvement clearly worked. Two isolated or read-only lanes overlapped for meaningful periods, and three-agent occupancy was exercised for more than four hours. The remaining optimization target is no longer “use concurrency at all”; it is to reduce coordination overhead and use cheaper models for bounded status, research, and routine review work.

## Task completion and timing

At the base commit, ten concrete P0 task records were already complete. During this session, 22 more concrete records transitioned to `Complete`: P0-05, P0-06, P0-13 through P0-18, eight instantiated P0-19 records, and P0-20 through P0-25. The generic P0-19 file remains explicitly a `Template` and is not an unchecked task.

All timestamps below are CEST. “Ledger complete” is the latest `Complete` transition among files in the group. “Last evidence” is the last task-attributed child turn or that transition, whichever is later. Wall windows include blockers and idle time. Worker time is the sum of attributed child-turn durations, can overlap, and excludes unallocated root time.

| Task | Files | First child work | Ledger complete | Last evidence | Wall window | Child worker time | Turns / threads |
|---|---:|---:|---:|---:|---:|---:|---:|
| P0-05 | 1 | Aug 21 16:21 | Aug 21 17:16 | Aug 21 23:24 | 7h03m | 1h29m | 7 / 2 |
| P0-06 | 1 | Aug 21 17:20 | Aug 27 08:33 | Aug 27 09:11 | 5d15h50m | 12h59m | 47 / 11 |
| P0-13 | 1 | Aug 21 15:41 | Aug 21 16:17 | Aug 21 16:17 | 36m | 36m | 1 / 1 |
| P0-14 | 1 | Aug 21 18:50 | Aug 21 20:44 | Aug 21 20:44 | 1h54m | 1h09m | 2 / 1 |
| P0-15 | 1 | Aug 21 19:14 | Aug 21 22:00 | Aug 21 22:00 | 2h45m | 2h14m | 9 / 3 |
| P0-16 | 1 | Aug 21 22:01 | Aug 21 23:05 | Aug 21 23:05 | 1h03m | 54m | 3 / 1 |
| P0-17 | 1 | Aug 21 22:02 | Aug 21 23:52 | Aug 21 23:58 | 1h56m | 52m | 5 / 3 |
| P0-18 | 1 | Aug 21 21:50 | Aug 22 00:38 | Aug 22 01:04 | 3h13m | 1h43m | 10 / 5 |
| P0-19 family | 8 | Aug 21 23:59 | Aug 27 23:58 | Aug 28 08:49 | 6d08h50m | 1h47m | 18 / 11 |
| P0-20 | 1 | Aug 22 00:43 | Aug 25 11:49 | Aug 25 23:11 | 3d22h27m | 13h21m | 79 / 13 |
| P0-21 | 1 | Aug 25 15:07 | Aug 28 08:50 | Aug 28 08:50 | 2d17h42m | 9h40m | 32 / 22 |
| P0-22 | 1 | Aug 21 15:41 | Aug 21 18:34 | Aug 25 02:19 | 3d10h37m | 3h50m | 7 / 3 |
| P0-23 | 1 | Aug 25 12:30 | Aug 26 18:57 | Aug 27 09:21 | 1d20h50m | 20h23m | 82 / 33 |
| P0-24 | 1 | Aug 25 12:05 | Aug 25 13:49 | Aug 26 19:34 | 1d07h28m | 2h38m | 13 / 4 |
| P0-25 | 1 | Aug 26 00:30 | Aug 26 03:33 | Aug 26 03:39 | 3h08m | 2h23m | 16 / 2 |
| Schadensfresse P0-19/21 subset | 1 | Aug 27 11:34 | — | Aug 28 08:28 | 20h53m | 4h26m | 22 / 8 |

The Schadensfresse row is an overlapping workstream inside P0-19/P0-21, not an additional completed task. Its early start is readiness analysis while administrator setup was still pending; live onboarding began only after the owner supplied the rules and external setup late on August 27.

### Calendar timeline

| Phase | What happened |
|---|---|
| Aug 21 afternoon | Audited the remaining P0 graph; closed bonus-context baseline P0-13; migrated P0-05 to hosted prompts; reconstructed the played-date history in P0-22. |
| Aug 21 evening to Aug 22 01:05 | Ran the first parallel implementation wave: profile-driven collection, context hygiene and provenance, question-aware bonus bounds, community topology, reusable workflows, and the first P0-19 arena workflow. |
| Aug 22–24 | Weekend/usage boundary. No genuine user messages; durable commits and continuation state allowed resumption without restarting. |
| Aug 25 | Resumed P0-20 live validation; repaired DFB/history/prompt/input issues; validated and removed the temporary Luna schedule; added P0-24; established cutoff-safe historical cost evidence; received the full model-experiment specification. |
| Aug 26 | Built and ran the bounded P0-23 candidate-evidence program; stopped the slow Luna/`max` branch; added Sol evidence; derived and published enriched roster context in P0-25. |
| Aug 27 | Owner selected Sol/`xhigh`; agents closed the model/workflow matrix, ran the ordered arena and production validation ladders, implemented the centralized production schedule, and onboarded Schadensfresse after external readiness. |
| Aug 28 | Fixed the smart-default matchday verifier, completed Schadensfresse scheduling, observed the first natural production run, audited all 16 jobs and eight participant paths, updated final evidence, and closed P0 at `2c824c8`. |

The first natural run was [GitHub Actions run 33143114280](https://github.com/ehonda/KicktippAi/actions/runs/33143114280). GitHub delivered it 2h46m22s after the nominal occurrence, but execution completed in 38m46s before the next daily occurrence. All participants retained 9/9 Kicktipp and Firestore predictions, and the run caused no generation, prediction write, reprediction, usage, or cost. The durable closeout is in [P0-21](../../../plans/bundesliga-2026-27/tasks/p0-21-production-activation.md).

## Autonomous problem solving

The strongest autonomy appeared inside a bounded writer-review-repair loop. The root did not know these concrete failures when work began; agents discovered them from code, tests, traces, live workflows, or independent review and then produced durable fixes.

| Episode | What was unclear or failed | Agent-discovered resolution | Evidence |
|---|---|---|---|
| Hosted prompt migration | Historical integration fixtures began selecting Langfuse implicitly and failed without a client. | A review agent isolated the selection regression; the repair made historical fixtures explicitly local without weakening the new hosted production route. | `d37cdf6`; [P0-05](../../../plans/bundesliga-2026-27/tasks/p0-05-prompt-route.md) |
| Played-date history | A nominally complete collector could mix cup rows, ignore the requested matchday, or publish partial/empty sets. | Agents enforced league-only evidence, the requested matchday, a canonical full schedule, and atomic publication; later review rejected empty completed histories. | `6541e1f` through `f9cf608`, then `78512dc`; [ADR-0042](../../../plans/bundesliga-2026-27/decisions/0042-publish-complete-preseason-context-atomically.md) |
| Context provenance | Shadowing and cached-content races could pair a prediction with incoherent or mutable provenance. | Independent review led to immutable publication identity and fail-closed equality/integrity checks. | `8a440da`, `3464efc`, `0002028`, `ecb70e8`; [P0-15](../../../plans/bundesliga-2026-27/tasks/p0-15-context-document-hygiene.md) |
| Bonus classifier | Ordinary string boundaries mishandled supplementary Unicode, while Champions League suppression was too global. | Agents implemented Unicode-scalar phrase boundaries and span-local exclusion so unrelated “champion” text does not suppress Bundesliga intent. | `fe48c82`, `cfe5a52`; [ADR-0038](../../../plans/bundesliga-2026-27/decisions/0038-bound-bonus-context-by-question-policy.md) |
| Development ladder | Non-dry validation exposed completed DFB history, empty-CSV/head safety, versioned-prompt validation, and reprediction-input failures. | Agents diagnosed each live failure, fixed the reusable path, and reran the ladder instead of documenting exceptions. | `0434f31`, `5921266`, `a25c0ca`; [P0-20](../../../plans/bundesliga-2026-27/tasks/p0-20-seed-and-development-validation.md) |
| Experiment accounting | Langfuse session-level usage could be contaminated, and missing first-row estimates made the budget gate circular. | Agents bound usage to exact dataset runs, bootstrapped conservative missing rows, used decimal cumulative observed-plus-reserved gates, and kept immutable result provenance. | `106adb8`, `2bef485`; [ADR-0046](../../../plans/bundesliga-2026-27/decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md) |
| Roster quality | Luna traces showed launch rosters were structurally valid but contextually thin. | From that trace evidence, agents designed a versioned enrichment overlay, derived team subtotals, validated atomic publication, and confirmed the new context in rerun traces. | `281b181`, `f1cfdde`, `22e8693`; [ADR-0050](../../../plans/bundesliga-2026-27/decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md) |
| Bonus reference copies | Live validation showed the verifier assumed source option IDs and did not safely resolve target context. | Agents made verification copy-aware and fail-closed before any model call. | `e095276`; [P0-24](../../../plans/bundesliga-2026-27/tasks/p0-24-bonus-copy-post-compatibility.md) |
| Schadensfresse verifier | The strict command assumed matchday 2 although matchday 1 was still unplayed. | Agents implemented and hardened a smart default that derives the currently verifiable matchday. | `d5644bd`, `919e916` |
| Natural schedule evidence | The first cron occurrence arrived almost three hours late, so “no run yet” could have been mistaken for broken activation. | Agents continued observation, separated delivery latency from execution correctness, audited all 16 jobs, and closed the task only after exact-head evidence. | `dd4d563`, `740d707`, `2c824c8`; [closeout handoff](../../../plans/bundesliga-2026-27/handoffs/buli-2627-p0-closeout-ready-2026-08-25.md) |

Two other important solutions were collaborative rather than independently discovered:

- The user diagnosed the missing original-repository locator. Agents turned it into [`New-AgentWorktree.ps1`](../../../New-AgentWorktree.ps1), rollback hardening, and exact-start verification in commits `268b376`, `13ba418`, and `3f5f886`.
- The user detected that leaf production workflows did not establish the desired scheduling semantics. Agents compared alternatives and implemented the centralized outer lane with context-first dependencies and primary-before-secondary ordering in [ADR-0053](../../../plans/bundesliga-2026-27/decisions/0053-schedule-the-production-live-matchday-lane.md).

## Separate application-experiment spend

The P0-23 Langfuse experiment program is distinct from the Codex task-agent cost above. Its closeout evidence recorded $4.70833727 observed plus $0.0996 reserved at initial completion. The later Sol/`max` addendum brought cumulative observed experiment spend to $8.52748527 and the bounded total to $8.64311647, below the $30 owner ceiling. Those figures measure model calls made by KicktippAi experiments and must not be added as though they were already part of the Codex transcript-token estimate.

## Recommendations

1. **Keep the parallel shape, change the model allocation.** Two isolated writers plus an independent reviewer/status lane worked. Use `Sol/xhigh` for architecture, high-risk integration, and final synthesis; assign bounded status, CI inspection, routine research, and simple reviews to cheaper capable tiers.
2. **Make task ownership stable.** Prefer a small number of task-specific agents with explicit checkpoints over paths that drift across P0 codes. Rotate only when context quality degrades or ownership changes materially.
3. **Replace polling with event-oriented waits and backoff.** The concurrency gain is real, but 3,527 waits for 414 turns is excessive. Longer waits, fewer `list_agents` calls, and consolidated progress messages should reduce root tokens without reducing autonomy.
4. **Collect owner gates in batches.** A preregistered decision sheet for hosted writes, budget, candidate matrix, production topology, and schedules could preserve safety while reducing the 24 authorization/external-control messages.
5. **Generate this telemetry during long goals.** Persist a lightweight task/agent/event ledger at checkpoints so completion reports do not require post-hoc reconstruction from roughly a gigabyte of forked logs.
6. **Keep Markdown as the canonical narrative for now.** A later HTML page can read `analysis.json` plus the curated findings and render filters, expandable agent rows, cost charts, and a task timeline without changing the evidence model.

The session was a successful emergency closeout and a convincing proof that the improved worktree/concurrency approach works. Its next evolution should target efficiency: fewer control-plane loops, clearer ownership, and intentional model tiering—not more parallel agents for their own sake.
