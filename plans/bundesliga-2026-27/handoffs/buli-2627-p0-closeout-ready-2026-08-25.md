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
or production-activation gates. Resume from [P0-23](../tasks/p0-23-gpt-5-6-production-candidate-evidence.md),
then finish [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md), the
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

1. The owner supplies the detailed P0-23 candidate surface and separately
   authorizes the cost and quality phases with separate maximum budgets.
2. After the applicable authorization, collect the requested cutoff-safe cost
   and quality evidence with immutable provenance, or record the owner's
   explicit evidence waiver, rationale, and accepted risk. Keep cost and quality
   evidence separate. Reuse the completed Luna row without another Luna model
   run.
3. P0-06 pauses for the owner to select the exact final production model,
   reasoning effort, output cap, numbered prompt versions, service-tier/fallback
   policy, cost ceiling, and challenger matrix. Record the selection, estimator
   evidence, and comparative evidence or waiver in the model ledger and a **new
   Accepted ADR**; do not edit an existing Accepted ADR to make the selection.
4. Build and review the model-bound manual matchday and bonus callers for
   `pes-squad` and `schadensfresse` using the exact selected identity. Their
   model-independent context callers are already present.
5. Build the arena production-copy callers only after the owner also supplies
   the arena participant, local profile, and exact credential names and the
   matching `pes-squad` callers are reviewed and green. Preserve P0-24 bonus
   compatibility and independent target-context fallback plus fail-closed match
   copy behavior.
6. P0-21 owns the remaining administrator and live gates: external
   `schadensfresse` setup, names-only repository secret presence,
   authentication/current-season readiness, POST permission, exact match and
   bonus deadlines, the Club Elo operating decision, named
   operator/monitor/rollback ownership, manual context and prediction evidence,
   the new activation ADR, deliberate schedules, and first scheduled
   observation.

## Copyable owner input for P0-23

Complete this block before any new experiment mutation or spend:

```text
Candidate surface
- Included candidate rows (repeat for each row):
  - Exact model ID:
  - Exact reasoning effort:
  - Exact maximum output-token cap:
- Configurations intentionally excluded:
- Exact requested cost topology:
- Exact requested quality topology, including fixture count, repetitions,
  paired/common-manifest rule, metrics, aggregation, and failure handling:

Cost phase
- Authorized to begin (yes/no):
- Maximum cost-phase spend:

Quality phase
- Authorized to begin (yes/no):
- Maximum quality-phase spend:

Selection evidence
- Comparative quality evidence required before selection (yes/no):
- If waived, rationale and accepted risk for the final production-selection ADR:
```

For each authorized candidate, the agent must still verify the current official
knowledge cutoff and pricing and derive its own exact cutoff-safe eligibility
window before preparing evidence. An owner surface does not waive those
fail-closed checks.

## Hard boundaries

- Do not infer or revive Terra, Sol, a reasoning default, an output cap, a
  topology, or an exclusion from the superseded provisional surface.
- Do not prepare/sync a dataset, mutate Langfuse or a hosted prompt, call a
  model, or incur spend without authorization for that exact phase.
- Do not rerun the completed Luna cost row; it is reusable evidence and is not
  production selection.
- Do not make a production POST, dispatch a production workflow, or add/enable
  a production schedule before the applicable P0-21 gates pass.
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

Report the exact main/origin SHA and CI state, clean worktree inventory, P0-23
owner-input status, candidate/spend authorization state, and the bounded lane
assignment. If the owner block above is incomplete, stop before dataset,
Langfuse, prompt, or model mutation and request only the missing inputs.
