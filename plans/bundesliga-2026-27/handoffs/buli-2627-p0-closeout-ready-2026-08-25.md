# Bundesliga 2026/27 P0 closeout-ready handoff

- Handoff ID: `buli-2627-p0-closeout-ready-2026-08-25`
- Created: 2026-08-25
- Status: **Active P0 closeout handoff; P0 is not complete**
- Repository: `ehonda/KicktippAi`
- Branch/remote baseline: `main` / `origin/main`
- Exact clean baseline: `78ee2c0aa1b4e1b0093b7ef442936cf042ad2681`
- Exact green Actions run: [32898097769](https://github.com/ehonda/KicktippAi/actions/runs/32898097769), all 12 jobs successful

## Resume objective

Close P0 without crossing the remaining owner, spend, community-administrator,
or production-activation gates. P0-25 is complete. Resume from
[P0-23](../tasks/p0-23-gpt-5-6-production-candidate-evidence.md), then finish
[P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md), the
model-bound production [P0-19](../tasks/p0-19-community-workflow-triad.md)
callers, and finally [P0-21](../tasks/p0-21-production-activation.md).

## Exact durable state

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
  are green. The first live dataset sync remains blocked pending explicit
  authorization for cutoff-safe historical/context dataset egress. No HTTP or
  payload egress, model call, or P0-23 spend occurred; its observed cumulative
  ledger remains exactly USD 0.
- `pes-squad-context-collection.yml` and
  `schadensfresse-context-collection.yml` are integrated manual-only
  `workflow_dispatch` context callers. They have no inputs, `workflow_call`, or
  schedule and have never been dispatched.
- The four superseded `pes-squad` / `schadensfresse` Bundesliga 2025/26 match
  and bonus callers remain inert `workflow_call`-only paths with
  `retired_configuration: true`; the reusable prediction workflows reject them
  before checkout or prediction work.
- Matchday and bonus telemetry tests now explicitly prove that
  `schadensfresse` is classified as a Langfuse `production` environment, while
  retaining deterministic activity correlation. This is code evidence, not a
  live trace or posting claim.

## Remaining sequence

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
   program, but the first live dataset sync remains blocked on the separate
   explicit historical/context dataset-egress authorization.
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
   roster publication through ADR-0051's paired explicit overlay mode to each
   production community before its first prediction,
   external
   `schadensfresse` setup, names-only repository secret presence,
   authentication/current-season readiness, POST permission, exact match and
   bonus deadlines, the Club Elo operating decision, named
   operator/monitor/rollback ownership, manual context and prediction evidence,
   the new activation ADR, deliberate schedules, and first scheduled
   observation.

## Resolved P0-23 owner input

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

- Do not replace ADR-0049's exact matrix, derive candidate caps outside its
  preflight process, or extend its one preliminary quality block after results
  without returning to the owner.
- Do not prepare/sync a dataset, mutate Langfuse or a hosted prompt, call a
  model, or incur spend before the corrected checkpoint is independently
  accepted and the machine-readable Decimal cumulative gate is integrated and
  invoked successfully.
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
ADR-0049 checkpoint-review state, Decimal budget-gate integration/command state,
the completed P0-25 evidence state, and bounded lane assignment. Do not assign a
new P0-25 live lane. Stop before dataset, Langfuse, prompt, or model mutation
while the applicable prerequisite remains open.
