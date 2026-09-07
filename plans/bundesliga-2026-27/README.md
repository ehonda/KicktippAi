# Bundesliga 2026/27 implementation plan

- Program state: P0 complete; P1 active
- Current execution policy: [execution strategy](execution-strategy.md)
- Local instructions and archival lifecycle: [AGENTS.md](AGENTS.md)
- Last reconciled: 2026-09-07

This file is the current program-state and navigation index. Completed
chronology, task evidence, and superseded execution records live in the
[opt-in archive](archive/README.md) and are not startup or recovery context.

## Current program state

- The live Bundesliga season/storage partition is `bundesliga-2026-27`.
- P0 is closed. Its task ledger, handoffs, execution waves, and first natural
  production-run evidence are indexed in the [P0 archive](archive/p0/README.md).
- The production outer matchday workflow currently retains ADR-0062's reviewed
  eight-pair source-copy topology. Under
  [ADR-0068](decisions/0068-replace-copy-sunset-with-reviewed-replacement-condition.md),
  it remains until a reviewed successor explicitly replaces or terminates it;
  calendar time alone does not change runtime.
- P1-04 and P1-05 share the accepted context-refresh seam in
  [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md).
  Their retained common branch is not merged or active runtime.
- P1-10 remains the final atomic target-primary replacement lane. Its temporary
  recovery route and future implementation are governed by the
  [P1 recovery packet](p1-execution-packet.md) and
  [production-recovery design](designs/p1-10-production-recovery-and-atomic-delivery.md).

## Active and deferred graph

| Lane | Current state | Dependencies and next gate |
|---|---|---|
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Source selected; specification not yet accepted | Reconcile the [in-flight handoff](handoffs/p1-04-club-elo-html-in-flight-2026-09-06.md), retained common seam, and remaining source-contract blockers before implementation |
| [P1-05](tasks/p1-05-roster-refresh.md) | Common foundation implemented locally; source implementation not started | Fresh cumulative common review, then proceed independently under ADR-0074 using the [in-flight handoff](handoffs/p1-05-roster-refresh-in-flight-2026-09-06.md) |
| [P1-06](tasks/p1-06-observability-datasets.md) | Not started | P0 context and production prerequisites are complete; freeze the exact experiment-data scope before work |
| [P1-07](tasks/p1-07-cost-calibration.md) | Not started | Waits for live evidence from P1-04 and P1-05 |
| P1-13 / R4a predecessor | Preserved and intentionally deferred | Reconcile the exact branch/PR state recorded in the [P1 status snapshot](p1-status-snapshot.md) before resuming; it gates final P1-10 work |
| [P1-10](tasks/p1-10-schadensfresse-primary-community.md) | In progress; atomic future PR | Resume after its predecessor lane; preserve recovery runtime until the independently reviewed replacement passes ADR-0068 |
| [P1-11](tasks/p1-11-langfuse-v4-migration.md) | Not started | Freeze the migration and compatibility boundary before implementation |
| [P1-16](tasks/p1-16-automatic-history-date-updates.md) | Deferred / low urgency | Needs interview; current manual maintenance remains accepted under ADR-0072 |

Completed and superseded P1 task records are indexed in the
[P1 archive](archive/p1/README.md). They are evidence, not active contracts.

## Production continuity and owner gates

- Required production communities remain `pes-squad`, `schadensfresse`,
  `relaxdays-tippt`, and `ehonda-ai-arena`; `ehonda-dev-buli-2627` remains the
  safe plumbing target.
- Every leaf caller remains manual-only. The sole recurring outer matchday lane
  keeps cron `7 2,9 * * *`, non-cancelling concurrency, strict serial/default-
  success ordering, and no scheduled bonus.
- The current Schadensfresse recovery pair uses target context and
  `pes-squad` source-compatible match copy. It grants no manual copy authority,
  bonus scheduling, target-primary activation, prompt/model change, or force.
- Existing Club Elo seed and roster last-known-good paths remain active until
  P1-04/P1-05 successors are separately accepted and activated.
- The owner retains final authority for source activation, production model or
  prompt changes, prediction replacement/cost/cutoff bounds, and schedule or
  topology changes.

## Current execution artifacts

- [P1 status snapshot](p1-status-snapshot.md) — dated reconciliation aid; verify
  it against live Git, worktree, agent, and PR state before relying on it.
- [P1-04/P1-05 execution packet](p1-04-05-execution-packet.md) — frozen common
  context-refresh graph and handoff boundary.
- [P1 recovery execution packet](p1-execution-packet.md) — P1-10 recovery and
  atomic delivery boundary.
- [Context-refresh design](designs/p1-04-05-context-refresh.md).
- [P1-10 production-recovery design](designs/p1-10-production-recovery-and-atomic-delivery.md).
- [Decision index](decisions/README.md) — specialist lookup for accepted and
  superseded ADRs; ADR age or phase number is not an archival criterion.
- [Historical orchestration investigations](archive/orchestration/README.md) —
  opt-in evidence only.
