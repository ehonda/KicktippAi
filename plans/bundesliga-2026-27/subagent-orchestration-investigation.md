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

### Runtime capacity versus repository policy

Two different limits apply and must not be conflated:

- The observed session received a Codex runtime instruction allowing four active agents in total, including the root. That runtime could therefore execute at most three subagents simultaneously. This was enforced by the session environment, not selected by this repository.
- ADR-0009 deliberately allows at most two task agents, normally one writer and one read-heavy helper. This repository policy is stricter than the observed runtime capacity.

The four-total runtime budget is not a universal Codex maximum. Codex exposes `agents.max_concurrent_threads_per_session` for spawned threads, excluding the primary agent; neither the user nor repository Codex config set that option during this investigation, so Codex selected the session default. The official documentation shows higher configured examples. Any attempt to change the runtime capacity should be verified in a new session rather than inferred from the repository policy.

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

### Why the two-task-agent allowance became mostly one active task agent

The policy says **at most** two task agents; it does not require two writers. Its normal shape is one writer plus one read-heavy helper, and a second writer is allowed only in a separate worktree with non-overlapping ownership. The ongoing session had only the primary worktree and a large dirty P0-12 change, so starting another writer there would have violated the policy.

The task loop also has real serial gates: a reviewer needs a stable diff, remediation needs concrete findings, CI needs a pushed commit, and the next dependent task needs its prerequisites. The orchestrator concentrated on one task through audit, implementation, repeated review, commit, and CI instead of keeping an independent look-ahead lane continuously occupied.

There was nevertheless ready independent work. The absence of concurrency was not caused solely by the dependency graph:

| Ready opportunity at the live cutoff | State | Parallel value |
|---|---|---|
| P0-05 prompt route | Ready since P0-01 completed | Independent prompt/configuration lane; later unlocks the non-owner part of P0-06 |
| P0-22 history played dates | Ready since P0-02 and P0-04 completed | Independent history lane, initially read-heavy because a source/identity ADR must be accepted before implementation |
| P0-13 bonus-context baseline | Blocked only by active P0-12; P0-09 and P0-11 are complete | Becomes a context-lane candidate as soon as P0-12 lands |

The missed opportunity was operational isolation and scheduling. P0-05 or P0-22 preparation could have overlapped the collector and P0-12 writers. A second implementation writer needed a worktree that was never created.

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

### Allow narrow reviewer-to-writer communication

The available collaboration tools can address another named agent directly, but the observed subagents sent every explicit message to `/root`. Direct reviewer-to-writer communication can shorten clarification and remediation latency when both threads already exist. It does not make review independent of a stable writer checkpoint and therefore will not materially increase task parallelism by itself.

Use direct communication within these boundaries:

- A reviewer may send numbered, evidence-backed findings or answer a clarification directly to the assigned writer.
- The reviewer also sends the root a compact finding summary; the writer reports resolutions and validation to the root.
- Only the root starts a new remediation turn, changes scope or ownership, accepts a risk, authorizes Git integration, or decides that a finding is closed for the release gate.
- A reviewer must not recursively assign work or silently expand the writer's paths. If reviewer and writer disagree about the contract, they stop that point and escalate it to the root.

This keeps the root as decision maker while removing it as a verbatim relay. The feasibility experiment should include one controlled direct-message handoff and compare root steering/wait traffic with the baseline.

### Keep role and model allocation stable

The initial Banach writer was overpowered and aborted. Later reuse also drifted CI/triage and roster turns from low or high effort to xhigh. Use fresh fixed-role agents when the task class changes, and reuse a writer or reviewer only for concrete remediation within the same task checkpoint. Record the intended model and effort in the assignment and verify it from the thread before expensive work starts.

### Avoid writer handoffs inside one task

P0-09 moved from Hypatia to Wegener, and P0-12 moved from Zeno to Darwin. A handoff can be justified when a writer stalls or the remaining slice is independently bounded, but it adds context reconstruction and root coordination. Prefer keeping one task writer through implementation, review remediation, validation, and scoped commit. If a handoff is necessary, require the outgoing writer to leave a structured gap list and exact dirty-path ownership.

### Make worktree isolation compatible with repository secrets

Worktrees were not rejected. ADR-0009 explicitly authorizes up to two isolated writers in separate worktrees. The live session simply did not create one.

The repository does have a worktree-specific constraint that the earlier policy did not spell out. `PathUtility` resolves `.env`, community `.env.<community>`, and `firebase.json` through a `KicktippAi.Secrets` directory next to the solution root. A Codex-managed worktree under the Codex home directory changes that parent directory and therefore does not naturally resolve the existing secrets repository. The repo has no `.worktreeinclude`, and that feature copies ignored files from inside the source checkout; it does not solve this external sibling-repository layout.

A manually created worktree that is a direct sibling of `KicktippAi` and `KicktippAi.Secrets` preserves the existing path contract without copying secrets. For example, `.../ehonda/KicktippAi-p0-prompt` still resolves `../KicktippAi.Secrets`. The [official Codex worktree documentation](https://learn.chatgpt.com/docs/environments/git-worktrees) confirms that managed worktrees otherwise receive tracked files and only explicitly included ignored local files.

One additional feasibility gate remains: subagents in the observed orchestration runtime share the parent's current directory and workspace permissions. A child can be instructed to use another `workdir`, but the experiment must prove that it can edit a sibling worktree with normal patch tooling and appropriately scoped permissions. Do not work around a failed probe with broad unsandboxed shell writes, secret copies, or directory junctions.

Secrets are not required for ordinary code edits and unit tests. Live collection or write validation should remain serialized and may be run from the primary checkout after integration if the assigned worktree cannot safely load the sibling secrets.

## Feasibility of more parallel subagents

### Four subagents

Four simultaneous subagents are not feasible in the runtime observed for this investigation because the root consumes one of four available active slots. This is a session-environment limit, not the repository's two-task-agent policy and not a universal Codex maximum. Even if a future runtime exposes root plus four children, four write-capable agents would conflict with ADR-0009, the shared-checkout safety rule, the slow-machine constraint, serialized heavy commands, and the official caution about parallel writes.

Four logical lanes can still exist as a queue—writer, next-task audit, reviewer, and CI reconciler—but reviewer and CI are gated by the writer and normally will not all execute simultaneously. Calling that a four-agent pipeline would overstate usable parallelism.

### Three subagents

A maximum of three simultaneous subagents is technically possible in the current slot budget, but it should be an experiment, not the new default. The safe shape is:

- one writer;
- one independent next-task audit;
- one second read-only activity, such as a separate contract/code audit or remote CI reconciliation;
- the root as control plane.

Do not run two local heavy test/build commands concurrently. Do not let the extra read-only agents edit planning or source files. A second writer is allowed only under the existing ADR's isolated-worktree, non-overlapping-ownership conditions and still counts against its two-task-agent limit. Adopting three task agents as a standing policy would require a superseding ADR.

## Two-lane feasibility experiment

Do not run this experiment inside the current dirty, late-running P0-12 session. Start only after that session has committed and pushed its scoped work, exact-SHA CI is green, `main` is clean, and no agent still owns the primary checkout.

The experiment does not change ADR-0009; it tests the two-writer mode the ADR already permits.

### Phase 1: worktree and secrets smoke test

1. Record the clean baseline SHA and create one temporary manual worktree as a direct sibling of the primary repository, on its own experiment branch. Keep the primary checkout as the integration lane.
2. Confirm the worktree resolves the existing sibling `KicktippAi.Secrets/src/Orchestrator` paths for the base environment, each community environment, and Firebase credentials. Report only path existence; never print file contents or secret values.
3. Confirm a subagent assigned to the sibling worktree can create and remove an ignored scratch file with normal patch tooling, inspect Git state, and run a harmless read-only command without acquiring access to the primary lane's dirty files.
4. Run the focused `PathUtilityAndEnvironmentHelperTests` outside the sandbox. Do not run a live collector, prediction, or remote write.
5. If the agent cannot write the sibling worktree without broad permission escalation, or if the secrets path does not resolve, stop. Keep the existing one-writer topology and record whether the prerequisite is workspace-root support or an explicit secrets-root override.

### Phase 2: small real two-lane trial

Use two orthogonal, already-ready slices rather than inventing throwaway work:

| Lane | Initial bounded slice | Ownership guard |
|---|---|---|
| Prompt lane | P0-05 runtime/prompt-route audit and a local checked-in implementation slice | Own prompt files, prompt-provider/runtime-metadata code, focused tests, and P0-05 evidence; defer shared integration files and hosted promotion until the integration gate |
| History lane | P0-22 source/schema inventory and proposed source/identity ADR, followed by the smallest accepted parser/identity slice | Own the new ADR/evidence, history-specific code/tests, and P0-22 evidence; do not implement beyond the accepted source decision |

The root assigns exact paths after both lane agents report their anticipated file sets. Any overlapping file becomes root-owned integration work or forces one lane to wait. Neither lane pushes `main`, edits the other worktree, runs live external collection, or changes a late owner gate.

Run both agents concurrently for read, edit, and lightweight validation. Serialize `dotnet`, containers, live network collection, Git integration, and full test suites. Each writer commits only its branch. Reviewers inspect a frozen lane diff in that lane's worktree; one reviewer may send a controlled numbered finding directly to its writer under the communication rules above.

### Phase 3: integration and measurement

The root reviews and integrates the two branch commits sequentially into the primary checkout, preferring cherry-picks of cohesive commits. Then run affected tests, the broader gate, one explicit push, and exact-SHA CI reconciliation. Remove a worktree only after its commit is integrated and recoverable.

Record:

- worktree creation, task start, stable-review checkpoint, commit, integration, push, and exact-SHA CI times;
- minutes with zero, one, and two task agents active and the reason for every idle interval;
- root spawn, follow-up, steering, wait, status-poll, and message-relay counts;
- direct reviewer/writer messages and whether they avoided a root relay without hiding a decision;
- file ownership violations, merge conflicts, writer handoffs, and review/remediation cycles;
- targeted and full validation duration, including confirmation that heavy commands did not overlap;
- raw/cached tokens by role and model/effort drift;
- secret-path success without copied or printed secrets.

The current live-session baseline is 2.4% dual-subagent utilization, 1.02 average concurrency while active, six root waits per assignment, one aborted turn, and green exact-SHA CI for all four pushed commits.

### Pass, fail, and adoption rules

The feasibility experiment passes only when both lane agents can work in isolated checkouts with normal scoped permissions, secrets paths are safe, ownership remains disjoint, integration needs no material conflict resolution, serialized validation stays reliable, and both lane outputs satisfy review and exact-SHA CI.

Treat improved wall time as evidence only when concurrency is real rather than nominal: record at least one meaningful interval of simultaneous useful lane work and compare total wave time with the sequential estimate. Do not require a specific percentage from a single small sample.

If it passes, use two isolated lanes for dependency-safe P0 work under the existing ADR. A likely continuation is the prompt lane through the non-owner portion of P0-06 while the context lane completes P0-22 and then picks up newly unblocked P0-13. Rejoin before P0-14/P0-15 and serialize shared integration and validation.

If it fails because subagent workspace permissions cannot isolate sibling worktrees, retain one writer plus one read-heavy helper. A separate top-level Codex worktree chat is an alternative, but it is a different orchestration model because the root session cannot directly supervise that chat. If it fails only because of secrets lookup, consider a separately reviewed explicit secrets-root override before retrying; never copy secrets into tracked files.

Only after successful two-lane evidence should the project consider a third read-only subagent. Adopting three task agents as standing policy still requires a superseding ADR. There is no evidence supporting four simultaneous subagents or four writers.
