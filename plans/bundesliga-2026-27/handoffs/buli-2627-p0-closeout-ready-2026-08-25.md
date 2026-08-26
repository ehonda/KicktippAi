# Bundesliga 2026/27 P0 closeout-ready handoff

- Handoff ID: `buli-2627-p0-closeout-ready-2026-08-25`
- Created: 2026-08-25
- Last advanced: 2026-08-27
- Status: **Active P0 closeout handoff; P0-06 and schedule-free P0-19 are complete; P0-21 remains**
- Repository: `ehonda/KicktippAi`
- Historical branch/remote baseline at creation: `main` / `origin/main`
- Historical exact clean baseline at creation: `78ee2c0aa1b4e1b0093b7ef442936cf042ad2681`
- Historical exact green Actions run at creation: [32898097769](https://github.com/ehonda/KicktippAi/actions/runs/32898097769), all 12 jobs successful

## Resume objective

Close P0 through [P0-21](../tasks/p0-21-production-activation.md) without
crossing its remaining community-administrator, runtime-write, or schedule
activation gates. P0-06 and every ADR-0052 P0-19 repository row are complete.

## Owner-selection and workflow closeout addendum — 2026-08-27

- [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)
  selects production `gpt-5.6-sol` / `xhigh` / cap `10000`, Flex-first /
  Standard-fallback, and the non-enforced USD 35 planning orientation.
- `pes-squad` and `schadensfresse` are independent primaries;
  `relaxdays-tippt` and the arena Sol/`xhigh` participant copy the exact
  `pes-squad` production identity. Self-contained arena challengers are
  Sol/`high`, Luna/`medium`, Terra/`xhigh`, and Luna/`none`, all cap `10000`.
- Match prompt v3 is hosted and checked in at normalized SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`.
  `production`, `staging`, and automatic `latest` each resolve version 3;
  bonus remains version 1. Historical P0-23 runs remain immutable on v2.
- All primary/copy/challenger workflow triads are prepared as independent
  `workflow_dispatch` entrypoints with no schedules. No workflow was dispatched
  and no prediction was posted during preparation.
- The reusable context workflow now has a false-by-default pinned launch-roster
  input. `pes-squad`, `relaxdays-tippt`, and the pending `schadensfresse`
  caller opt in: their context job downloads the exact audited public artifact,
  runs the SHA/revision/date-gated paired P0-25 overlay before ordinary profile
  collection, and stops on any failure. Arena callers omit it and preserve the
  already verified shared enriched head
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
- The Owner confirmed every exact canonical Kicktipp Actions username/password
  pair in the community ledger provisioned on 2026-08-27. This is not API
  enumeration, authentication, current-season readiness, or POST evidence.
- Resume only with P0-21: remediate `schadensfresse` externally, validate every
  participant/community manually, dispatch and inspect the prepared launch-
  overlay/profile context path where applicable, record
  deadlines and permissions, perform opening writes, then return to the final
  Owner schedule gate and observe the first deliberately enabled sequence.
- After this repository change is independently reviewed, integrated, pushed,
  and green, the Owner authorizes manual context-then-prediction dispatch and
  initial writes for `pes-squad`, `relaxdays-tippt`, and every selected arena
  participant. Run primaries before dependent secondaries and stop on failure.
  Keep `schadensfresse` unrun/manual-only. Successful evidence authorizes a
  later root-owned schedule ADR/workflow lane for ready rows; this lane remains
  schedule-free.

This addendum supersedes later stale statements in this dated handoff that call
the Owner selection, production callers, challenger rows, prompt v3, or secret
provisioning unresolved.

### Schedule-free repository validation

- Release solution build completed with zero errors; existing dependency,
  nullability, and obsolete-API warnings remain unchanged.
- Full `OpenAiIntegration.Tests` passed `233/233` after the v3 mirror/hash and
  unresolved-placeholder assertions were added.
- Focused context-workflow contracts passed `12/12`, and the final full
  `Orchestrator.Tests` suite passed `1136/1136` after the launch-overlay path.
- The deterministic workflow contract passed with `2` prediction bases, `14`
  callable WM26 callers, `12` explicitly retired Bundesliga callers, and `16`
  current Bundesliga callers. Docker actionlint passed all `23` changed/new
  workflow files.
- The two checked-in match mirrors are byte-identical and reproduce normalized
  v3 SHA-256
  `7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`.
  Changed/new Markdown relative links, added-content secret patterns, and
  `git diff --check` passed.
- No workflow dispatch, model call, Kicktipp post, prediction write, schedule,
  bonus-prompt mutation, or roster publication was performed by this lane.
  Independent exact-SHA review and integration remain the orchestrator's next
  repository gates.

## P0-23 completion addendum — 2026-08-26

- [P0-23](../tasks/p0-23-gpt-5-6-production-candidate-evidence.md) is complete.
  Its [quality results](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-quality-results.md)
  and [cost evidence](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-cost-results.md)
  are the current decision inputs for P0-06. No production model or arena
  participant was selected by the experiment lane.
- Eight originally planned configurations completed. Luna/max did not: its p5
  and p3 attempts ended in transient capacity failures, and the Owner explicitly
  stopped the planned p1 retry. It has no quality score, rank, confidence
  interval, or imputed quality result.
- After all eight accepted original scores were visible, the Owner added
  Sol/xhigh. Its cost row and quality run completed, but this was a post-hoc,
  data-dependent addition. Every nine-run-family inference that includes it is
  exploratory rather than preregistered confirmatory evidence.
- Final experiment accounting is USD `4.708337270000` observed plus USD
  `0.099600000000` reserved, USD `4.807937270000` all-in, and USD
  `25.192062730000` remaining under the cumulative USD 30 ceiling.
- This addendum supersedes the stale execution-state and resume instructions in
  the dated historical sections below. The original ADR/preregistration and its
  audit trail remain point-in-time records; do not rewrite them as though the
  Owner stop or Sol/xhigh addition had been planned originally. Do not perform
  another P0-23 dataset sync, model call, or experiment mutation without a new
  Owner-authorized task and fresh budget gate.

## Historical durable state at handoff creation

The dated state in this section is preserved for audit history. Where it says
P0-23 was pending or blocked, the completion addendum above is authoritative.

- `main` and `origin/main` were clean and equal at the exact baseline above.
  The exact-head Actions run was terminal green with all 12 jobs successful.
- Baseline cleanup had removed every temporary worktree. This handoff is being
  authored in one new helper-created worktree; remove it after integration and
  re-confirm that no temporary worktrees remain.
- Implementation through P0-20 and P0-24 is complete and integrated. P0-23 is
  the active closeout evidence gate; the final P0-06 selection and all P0-21
  production evidence remain open.
- P0-25 is a completed launch-data remediation under
  [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md)
  and its launch-boundary correction
  [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md).
  It adds v2 roster documents with one derived known-value subtotal per team,
  retains strict historical v1 reconstruction, and gates explicit launch
  publication on the audited artifact SHA and 464/464/450 coverage floors. Its
  explicit republish from exact-green main
  `f1cfddeb6e2f7ba376856c0843a196af104b9a5c` passed 18-team/18-derived-row and
  464/464/450 final reconstruction with unchanged headed snapshot
  `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`.
  Exactly one authorized Luna/none index-0 replacement round completed in
  [run 32917812259](https://github.com/ehonda/KicktippAi/actions/runs/32917812259)
  and passed pre/post identity, inventory, roster, and payload-safe trace checks.
  P0-25 records the full evidence; its arena authorization is consumed.
- [ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
  supersedes this handoff's provisional P0-23 owner-input template. The exact
  nine-row GPT-5.6 matrix, one cumulative USD 30 ceiling, evidence-derived cap
  mechanics, adaptive topology, and preliminary-return gate are now fixed. The
  no-spend checkpoint is independently approved, and the machine-readable
  Decimal cumulative-budget gate is integrated at exact main commit
  `0b86b11564b9cc7500b7bfaf94301e4e83263f73`; its 24 focused tests and
  exact-commit [Build and Test run 32910669112](https://github.com/ehonda/KicktippAi/actions/runs/32910669112)
  are green. The exact `1 × 1` and `5 × 4` artifacts are prepared locally and
  reproduce the frozen pool, selection, and manifest identities. Their first
  Langfuse sync remains blocked pending explicit authorization to upload the
  public cutoff-safe historical match dataset records described below. No HTTP
  or payload egress, model call, or P0-23 spend occurred; its observed
  cumulative ledger remains exactly USD 0.
- The `1 × 1` raw dataset/manifest hashes are
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717` /
  `b396ffd599c8c79569db656d66e68ebe9169caf9a7e274d1aa0e7a0c8f8017c1`;
  its canonical historical-artifact hash is
  `a03c31c174e0e0be1723b5214453a3992c2b5d023d125eb75fa658a7503c2946`.
  The `5 × 4` raw dataset/manifest hashes are
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18` /
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`;
  its canonical historical-artifact hash is
  `22dfcab23f063e2fbb7a7fa96df4f2fb5dca384bb1329adc0c33157f5419a105`.
  The exact eligible pool is `109` fixtures/hash
  `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`;
  selected-set hashes are
  `4a293d4bac8f6406cb88770332a5b85f9084f01d2f2e0227f7d52d63e93c4e16`
  and
  `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`.
- The exact pending upload contains only public historical match records:
  fixture/team names, kickoff, competition/season/community slug, matchday and
  label, Kicktipp match ID, fixture/repetition indices, and completed score.
  `slice-dataset.json` contains no historical context bodies, references, or
  hashes, prompt text, prediction output, credentials, or secrets. The local
  manifest alone retains seven context-document reference/version/timestamp/
  content-hash tuples and is not the dataset sync payload.
- `pes-squad-context-collection.yml` and
  `schadensfresse-context-collection.yml` are integrated manual-only
  `workflow_dispatch` context callers. They have no dispatch inputs,
  `workflow_call`, or schedule and had never been dispatched at handoff
  creation. ADR-0052 later added the reusable caller's exact internal launch-
  overlay opt-in.
- The four superseded `pes-squad` / `schadensfresse` Bundesliga 2025/26 match
  and bonus callers remain inert `workflow_call`-only paths with
  `retired_configuration: true`; the reusable prediction workflows reject them
  before checkout or prediction work.
- Matchday and bonus telemetry tests now explicitly prove that
  `schadensfresse` is classified as a Langfuse `production` environment, while
  retaining deterministic activity correlation. This is code evidence, not a
  live trace or posting claim.

## Historical remaining sequence from 2026-08-25 (superseded)

This sequence records the original handoff state. Resume from the current
objective and completion addendum above, not from its pending P0-23 steps.

1. **Complete:** P0-25 was independently reviewed, integrated, and green before
   the paired explicit overlay republish. The unchanged enriched snapshot passed
   the final 18/18/464/464/450 gate; the preflight inventory proved exactly nine
   Luna/none/cap-10000 records at index 0 and none at index 1+; and exactly one
   forced replacement workflow passed final verification. Exact trace
   `3c2814f7b2b6200f3cf4e4bab94d772e` had one root plus nine ordered Flex
   generations, snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`,
   no fallback/errors, and payload-safe usage/cost evidence. No prompt or
   prediction payload was retained. This is arena plumbing validation only.
2. **Complete:** The corrected no-spend checkpoint was independently approved,
   and the machine-readable Decimal cumulative-budget gate plus its exact
   aggregate command were integrated and validated at the exact green commit
   and Actions run recorded above. ADR-0049 authorizes its exact evidence
   program, but the first live dataset sync remains blocked on separate explicit
   authorization to upload the public cutoff-safe historical match records.
3. Collect the ADR-0049 cutoff-safe cost rows and adaptive quality evidence with
   immutable provenance. Keep cost and quality evidence separate, reuse the
   completed Luna row without another Luna model run, and return to the owner
   after the one preliminary quality-first block before any additional block.
4. P0-06 pauses for the owner to select the exact final production model,
   reasoning effort, output cap, numbered prompt versions, service-tier/fallback
   policy, cost ceiling, and challenger matrix. Record the selection, estimator
   evidence, and comparative evidence or waiver in the model ledger and a **new
   Accepted ADR**; do not edit an existing Accepted ADR to make the selection.
5. Build and review the model-bound manual matchday and bonus callers for
   `pes-squad` and `schadensfresse` using the exact selected identity. Their
   model-independent context callers are already present.
6. Build the arena production-copy callers only after the owner also supplies
   the arena participant, local profile, and exact credential names and the
   matching `pes-squad` callers are reviewed and green. Preserve P0-24 bonus
   compatibility and independent target-context fallback plus fail-closed match
   copy behavior.
7. P0-21 owns the remaining administrator and live gates: pinned enriched v2
   roster publication through the prepared ADR-0051 paired-overlay context
   path for each non-arena production community before its first prediction,
   preservation inspection of the exact already enriched arena head,
   external
   `schadensfresse` setup, names-only repository secret presence,
   authentication/current-season readiness, POST permission, exact match and
   bonus deadlines, the Club Elo operating decision, named
   operator/monitor/rollback ownership, manual context and prediction evidence,
   the new activation ADR, deliberate schedules, and first scheduled
   observation.

## Historical preregistered P0-23 owner input

[ADR-0049](../decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
is the authoritative P0-23 experiment contract. It records:

- Sol `high` / `medium` / `none`;
- Terra `xhigh` / `medium` / `none`;
- Luna `max` / `medium` / `none`;
- one cumulative USD 30 ceiling for new P0-23 attempts;
- exact preflight-derived candidate caps;
- a 20-paired-repetition full-matrix target and one quality-first preliminary
  fallback block, with explicitly weaker 15/10-repetition exploratory fallbacks
  only after the Decimal gate proves stronger options unaffordable; and
- a mandatory return to the owner after that preliminary report.

The owner still reserves final production and arena selection. Each candidate's
current official cutoff and price, the hosted prompt binding, and the exact
historical pool/manifest provenance remain execution-date fail-closed gates.

## Hard boundaries

- Preserve ADR-0049 and the preregistration as the frozen historical design. Do
  not describe the Owner-stopped Luna/max p1 attempt or post-hoc Sol/xhigh
  addition as preregistered. Any inference across all nine completed run
  families is exploratory and data-dependent.
- P0-23 is complete. Do not sync or rerun its dataset, mutate its Langfuse
  experiment state, call another model, or incur more experiment spend under
  the consumed authorization. A new experiment requires a new Owner-authorized
  task and fresh cumulative gate.
- Do not rerun the completed Luna cost row; it is reusable evidence and is not
  production selection.
- Do not make a production POST, dispatch a production workflow, or add/enable
  a production schedule before the applicable P0-21 gates pass.
- P0-25's authorization for exactly one arena-only Luna/none replacement round
  is consumed. Do not repeat that publish/override ladder or infer authority for
  a production-community prediction, bonus round, schedule, or P0-23 quality
  claim.
- The `schadensfresse` setup request remains external and pending. Agents do not
  administer that community or treat authentication as current-season
  readiness or POST permission.
- Do not invent a production selection, participant, local profile, credential
  name or value, challenger, topology, budget, permission, deadline, cadence,
  rollback rule, or schedule.

## Resume protocol

1. Read [`../AGENTS.md`](../AGENTS.md), [`../README.md`](../README.md), and
   [`../execution-strategy.md`](../execution-strategy.md), then the active task
   and every linked Accepted ADR.
2. Keep the primary checkout integration-only. Create bounded writer lanes with
   `New-AgentWorktree.ps1`; never use raw `git worktree add`. Verify the
   helper-created `.codex-local/original-repository-path` locator before work.
3. Give every lane one writer and disjoint path ownership. Use at most the
   execution strategy's bounded two-writer limit and serialize Git integration,
   pushes, and live external work.
4. Freeze a scoped commit and obtain an independent review of its exact SHA.
   Integrate accepted commits sequentially against the current main head.
5. Before each push, record the exact branch, remote, status, and commit; push
   the explicit remote/branch. Reconcile every required Actions run and job to
   the exact pushed SHA before advancing.
6. After integration, remove the helper-created worktree, prune stale metadata,
   and verify that no temporary worktrees remain.

## First resume checkpoint

Report the exact main/origin SHA and CI state, clean worktree inventory,
P0-23 publication/integration state, the pending P0-06 Owner-selection state,
the completed P0-25 evidence state, and bounded lane assignment. Do not assign
a new P0-23 or P0-25 live lane, and do not perform another P0-23 external or
model action while resuming the P0-06 decision gate.
