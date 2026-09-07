# Bundesliga 2026/27 planning instructions

These instructions apply to every file and implementation task under this directory.

## Read order

1. Read this file.
2. Read the current-status and dependency sections of [README.md](README.md).
3. Read the active task or handoff named by that index and only the designs and
   operative ADRs needed for the assigned lane. Historical task/evidence and
   archive directories are opt-in, never unconditional context.
4. Consult readiness research only for background; accepted ADRs supersede
   research proposals. Reconcile an in-flight handoff with current Git and
   worktree state before treating either as current truth.

## Source-of-truth precedence

- Runtime and repository state decide what exists now; an accepted operative
  ADR decides durable intent; the current plan index decides routing; the
  active task/design/handoff decides lane execution. Reconcile contradictions
  explicitly rather than loading more history by default.
- Keep `README.md` current-only: program state, active/deferred tasks,
  dependencies, owner gates, and links. Keep `execution-strategy.md` focused on
  current phase policy, milestones, authority, and continuity rather than
  completed chronology.
- A phase orchestration preview names its exact active-contract packet.
  Material outside that packet is read only when the current contract points
  to it or a cold reconstruction requires it.

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
- Production bonus callers remain manual-only. Match predictions run only
  through [ADR-0053](decisions/0053-schedule-the-production-live-matchday-lane.md)'s
  strict outer schedule; all leaf callers remain manual-only. The currently
  accepted recovery is [ADR-0062](decisions/0062-temporarily-restore-schadensfresse-copy.md)'s
  eight-pair topology: Schadensfresse uses target-owned context followed by a
  source-compatible `pes-squad` match copy. Under
  [ADR-0068](decisions/0068-replace-copy-sunset-with-reviewed-replacement-condition.md),
  that pair remains until a reviewed successor replaces or terminates it.
  P1-10's typed target-primary route is still the separately reviewed atomic
  successor, not current runtime. See
  [ADR-0006](decisions/0006-stage-validation-with-a-cheap-test-model.md).

## Task records

- Keep each task's status, checklist, validation evidence, and ADR links current while implementing it.
- Do not mark a task complete until every completion criterion and listed automated check passes, or a linked ADR explicitly changes the criterion.
- Add newly discovered work as a small task with dependencies instead of expanding an existing task without bound.
- Keep task records as compact execution contracts: status, outcome, dependencies, owner gates, milestone checklist, completion criteria, evidence links, and ADR/design links. Put cross-cutting invariants and seam maps in `designs/`, phase order/resources/authority in the frozen phase execution packet, and high-volume run evidence in dedicated evidence artifacts or CI links.
- Under [ADR-0075](decisions/0075-refine-orchestration-recovery-and-resource-admission.md), as refined by [ADR-0076](decisions/0076-pause-orchestration-hook-readiness-gate.md), a phase-scale `$orchestrate` run must finish whole-phase intake before writers start. Genuinely phase-wide or cross-cutting architecture uses `gpt-6-astra` / `high` only as architecture lead, followed by a different `gpt-5.6-sol` / `xhigh` specification reviewer. Bounded implementation uses the capability tier appropriate to the frozen task; read-only status/CI evidence stays lightweight.
- Each checkout/worktree has one writer. There is no universal writer or linked-worktree count; the root admits path-disjoint ready work through the checked-in reservation-based disk policy, sole-heavy lease, and a run-local wave throttle. A new cross-cutting invariant, missing ADR, dependency seam, invalidated architecture, or material expansion pauses the affected lane for redesign and re-freeze instead of growing the task silently.
- A milestone that disables or regresses active production behavior must stay on an integration branch or draft PR until the safe release unit is ready. A separate safety quarantine needs explicit owner approval, documented impact/fallback/rollback/recovery owner, and a restoration deadline.
- Every implementation task ends with a scoped local commit. Push cohesive reviewed milestones and recovery-critical long lanes—not every local lane—after verifying the exact Git target according to the repository-level authorization contract.

## Archival lifecycle

- When a task or handoff becomes `Complete` or `Superseded`, reconcile its
  commit/worktree state and confirm that no live recovery, rollback, or current
  contract still depends on it. Then move completed execution history and
  evidence into the indexed archive in the same focused change; do not wait
  for a broad cleanup.
- Keep accepted ADRs in `decisions/` while they remain operative, regardless of
  task age or phase number. When a mixed artifact contains both a live contract
  and completed evidence, split those concerns before archiving the evidence.
- Update every repository link atomically with a move and validate Markdown
  links. Do not leave redirect stubs solely to preserve discovery clutter.
- Archive paths are opt-in historical reference material. Never add an archive
  directory, completed-phase index, or historical execution strategy to a
  general startup, recovery, root, or task-agent read order.
