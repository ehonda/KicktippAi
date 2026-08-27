# Bundesliga 2026/27 planning instructions

These instructions apply to every file and implementation task under this directory.

## Read order

1. Read this file.
2. Read [README.md](README.md) for task order and dependencies.
3. Read the task being implemented and every linked prerequisite ADR.
4. Consult the readiness research only for background; accepted ADRs in this directory supersede research proposals.

## Architecture decision records

- Record every durable planning or implementation decision as an ADR under `decisions/`.
- Create or update the ADR in the same change that makes the decision concrete. Do not leave a decision only in chat, a pull request, a task checkbox, or code comments.
- Use [0000-template.md](decisions/0000-template.md). Number ADRs sequentially and use a short kebab-case title.
- ADR status must be `Proposed`, `Accepted`, or `Superseded`. An accepted ADR is immutable apart from spelling or link corrections; replace it with a new ADR and mark the old one superseded when the decision changes.
- Record context, the decision, considered alternatives, consequences, and affected tasks. Link the ADR from each affected task and from the decision index in [README.md](README.md).
- Decisions that always require an ADR include competition/default behavior, storage identity, provider/source and reuse terms, prompt route, model configuration, context document contracts, community scope, refresh cadence, schedule activation, and launch-gate changes.
- If implementation exposes an unrecorded choice, pause that part of the task, add an ADR, and then continue. Do not silently inherit a Bundesliga 2025/26 or WM26 choice.

## Fixed scope

- `bundesliga-2026-27` is the only live Bundesliga season supported by this plan.
- Do not add compatibility work for Bundesliga 2025/26 workflows, prompts, defaults, or implicit document IDs. Existing historical data and experiment artifacts need not be migrated or deleted.
- Transfer documents are not part of the Bundesliga 2026/27 match or bonus context contract. Club Elo rankings, current rosters, and squad summaries supersede them.
- DuckDB is the primary roster-membership source per club only when it explicitly represents 2026/27 and passes the gates in [ADR-0003](decisions/0003-duckdb-primary-rosters-with-fallback.md). Otherwise use the complete source-dated fallback or last-known-good membership. DuckDB also provides safe enrichment; this does not justify creating transfer documents.
- A future historical experiment must provide its competition, prompt, and context contract explicitly; it is outside this plan.

## Validation and activation safety

- Agents may autonomously write to `ehonda-dev-buli-2627` using only `gpt-5.6-luna` with `none` reasoning and a pinned output cap. Treat this as plumbing validation, never as prediction-quality evidence.
- After its configured participant and credentials are available, the same Luna/none path may be validated in `ehonda-ai-arena` through local CLI, `workflow_dispatch`, and an arena-only schedule. Inspect Kicktipp writes, Firestore state, Langfuse traces, and workflow ordering at each stage.
- Never promote the validation model to production. The project owner controls the final model/prompt/cost decision, Club Elo unattended-network decision, and final schedule activation.
- Load community-specific sibling `.env.<community>` credentials for local writes without printing values or replacing the base development `.env`.
- Production bonus callers remain manual-only. Match predictions for the ready
  `pes-squad`, `schadensfresse`, `relaxdays-tippt`, and `ehonda-ai-arena` rows
  run only through [ADR-0053](decisions/0053-schedule-the-production-live-matchday-lane.md)
  as extended by
  [ADR-0055](decisions/0055-add-schadensfresse-to-production-live-lane.md)'s
  strict outer schedule; their leaf callers remain manual-only. P1-08's later
  mixed-competition `schadensfresse` work remains outside that lane. See
  [ADR-0006](decisions/0006-stage-validation-with-a-cheap-test-model.md).

## Task records

- Keep each task's status, checklist, validation evidence, and ADR links current while implementing it.
- Do not mark a task complete until every completion criterion and listed automated check passes, or a linked ADR explicitly changes the criterion.
- Add newly discovered work as a small task with dependencies instead of expanding an existing task without bound.
- Treat the strongest, highest-reasoning agent as the orchestration control plane for this plan, not as the routine implementation or review default. Assign bounded implementation to a task agent whose capability tier matches the task, use lightweight agents for read-only research/status/CI evidence, and use a separate dedicated reviewer only when the task's risk justifies it. Each checkout/worktree has one writer; under ADR-0009 the orchestrator may run up to two isolated writers with non-overlapping ownership in separate worktrees.
- The final execution step for every implementation task is to verify the exact Git target, commit the scoped changes intentionally, and push the explicit remote/branch according to the repository-level instructions.
