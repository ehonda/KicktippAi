# Bundesliga 2026/27 execution strategy

- Status: Accepted current P1 strategy
- Last updated: 2026-09-07
- Historical P0 execution and launch evidence: [P0 archive](archive/p0/README.md)

This document defines current dependencies, milestones, production continuity,
and phase-specific execution policy. Repository-wide orchestration mechanics
remain in the root `AGENTS.md` and `$orchestrate`; task and design files remain
the implementation contracts named by a frozen active-contract packet.

## Current objective and graph

P1 improves live context freshness, experiment readiness, cost calibration,
and typed Schadensfresse routing without regressing the established production
lane.

1. Reconcile the retained P1-04/P1-05 common-runtime work and independently
   accept its current contracts.
2. Release P1-05 after the common seam. Keep P1-04 separately gated on an
   accepted Club Elo source/date/provenance contract.
3. Run other semantically independent ready P1 work only after its task is
   frozen and its owner/authority gates are explicit.
4. Keep P1-10 last. Reconcile and complete its P1-13/R4a predecessor before the
   atomic target-primary replacement.

The [current plan index](README.md) owns task status and routing. The
[P1 status snapshot](p1-status-snapshot.md) is dated handoff evidence and must
be reconciled with live Git, PR, worktree, and agent state before use.

## Milestones and release seams

| Milestone | Acceptance boundary | Downstream release |
|---|---|---|
| P1-04/P1-05 common context-refresh seam | Exact common tip independently reviewed against ADR-0074 and the frozen execution packet | P1-05 may proceed independently; P1-04 remains source-contract gated |
| P1-05 roster refresh | Candidate/rejection/LKG/provenance behavior and affected tests accepted without activating a new source prematurely | P1-07 may consume live roster-refresh evidence after activation evidence exists |
| P1-04 Club Elo refresh | Source/date semantics, freshness, LKG, provenance, and rollback independently accepted | P1-07 may consume live Club Elo refresh evidence after activation evidence exists |
| Independent maintenance lanes | Each bounded task meets its own contract and does not reopen a shared seam | Integrate independently when production-safe |
| P1-13/R4a predecessor | Preserved implementation and review state reconciled; exact successor boundary frozen | Releases final P1-10 implementation |
| P1-10 target-primary replacement | One atomic PR covers typed identity, target-owned routes, persistence/provenance, validation, and reviewed transition | Replaces or terminates temporary copy only under ADR-0068 |

A necessary cross-cutting architecture lead derives the detailed milestone and
downstream-release matrix from the current graph. Do not hard-code an old run's
implementation sequence into a new preview.

## Production continuity

- ADR-0062's current recovery topology remains eight strict context→match
  pairs/16 jobs. Schadensfresse target context and `pes-squad`-source copy match
  run after `pes-squad`; relaxdays follows Schadensfresse.
- ADR-0068 replaces only ADR-0062's calendar sunset. The recovery remains until
  a reviewed successor names the replacement topology, rollback and recovery
  owner, exact integrated revision, and required green validation.
- Preserve cron `7 2,9 * * *`, non-cancelling concurrency, serial/default-
  success ordering, manual-only leaf callers, no scheduled bonus, and
  ADR-0053's monitoring and whole-cron rollback contract.
- The recovery grants no manual dispatch, prediction replacement/delete, force,
  model call/change, prompt promotion, POST, Firestore/Langfuse mutation,
  credential change, or target-primary activation.
- Existing dated Club Elo seed and roster last-known-good consumers remain the
  fallback until P1-04/P1-05 replacements are separately reviewed and activated.
- A milestone that temporarily disables or regresses live behavior remains on
  an integration branch/draft PR until the production-safe release unit is
  ready. Any separate quarantine requires explicit owner approval of impact,
  fallback, rollback, recovery owner, and restoration deadline.

## Orchestration and implementation policy

- Invoke `$orchestrate` explicitly for a phase execution run. Before writers,
  audit the whole supplied objective and freeze only interview-complete tasks or
  cohesive milestones; leave the rest `needs-interview` without guessing.
- Use `gpt-6-astra/high` only for a genuinely necessary phase-wide or cross-
  cutting architecture lead. A different `gpt-5.6-sol/xhigh` specification
  reviewer remains mandatory.
- Give each writer one frozen milestone, disjoint owned paths, one admitted
  worktree, focused tests, and a defined review/integration route. Keep the
  primary checkout for serialized integration while writers are active.
- Do not impose a universal writer or worktree count. Select a wave-local
  throttle from useful ready work, path ownership, disk reservations, external
  leases, review capacity, and integration routes. Keep the sole-heavy-family
  lease independent.
- Before worktree creation/reactivation or heavy validation, use the checked-in
  reservation-based resource helper. Preserve intentionally retained worktrees;
  missing ownership is never cleanup permission.
- Use the compact preview and scoped recovery manifests from ADR-0075. Archive
  material is opt-in and must not enter startup/hot recovery merely because it
  shares this plan tree.

## Review, integration, and CI

1. A writer self-reviews and runs the focused gate for its exact milestone.
2. The first independent implementation reviewer may follow up only on its own
   findings within ADR-0075's correction limits.
3. Final milestone acceptance uses a fresh reviewer with the exact tip, full
   diff, frozen contract, operative decisions, and closed-findings checklist.
4. Publish only a cohesive reviewed milestone or recovery-critical long lane.
   Before every push, verify branch, remotes, status, exact tip, scoped payload,
   and the allowlisted explicit refspec.
5. Reconcile required CI against the exact pushed SHA. One failed-workflow rerun
   is allowed only after diagnosis; repeated failure requires correction.
6. Merge a clean green PR only when its frozen publication route permits it,
   then synchronize local `main` before beginning a dependent PR.

## Owner and authority gates

The owner retains decisions that change:

- Club Elo unattended-network activation or source semantics;
- production model, reasoning, output cap, service/fallback, or hosted prompt;
- Schadensfresse replacement rows, cost/force/reprediction/cutoff bounds, or
  target-primary activation;
- production schedules, topology, credentials, or external/live side effects;
  and
- any production-safety quarantine outside an already accepted rollback contract.

Fact-finding and implementation agents may establish evidence and recommend,
but they do not silently resolve these gates.

## Validation policy

- Review observable behavior against the active task and accepted ADRs.
- Concentrate independent review on storage/typed identity, source provenance,
  context selection, persistence/freshness, workflow ordering, prompt
  promotion, and production-continuity boundaries.
- Use `dotnet run --project tests/<Project>` for TUnit projects. Treat full
  solution/test or multi-job families as the sole heavy-operation family.
- Treat CI as confirmation, not the first correctness check. Record exact run
  and job evidence in the active task or purpose-specific evidence artifact,
  never in the recovery capsule or a generic orchestration journal.
