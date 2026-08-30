# ADR-0061: Preview and milestone orchestration

- Status: Accepted
- Date: 2026-08-31

## Context

The first `$orchestrate P1` run used explicit model allocation and bounded
worktrees successfully, but it accepted a phase label without a whole-phase
readiness audit. P1-10 then discovered cross-cutting architecture between
narrow implementation slices. Per-lane remote publication, repeated main CI,
late review, checkpoint-level ledger edits, and missing Git authorization made
the safe workflow unnecessarily serial. One CI-green sequence also quarantined
working Schadensfresse production paths on `main` before their replacement was
ready.

The same run proved that fixed concurrency is not sufficient resource control:
this host has four logical processors, about eight GiB RAM, limited free disk,
and worktrees with substantial duplicated build output.

## Decision

Bundesliga phase orchestration begins with a read-only whole-phase preview. It
expands current tasks and dependencies, completes a phase-wide foundation,
classifies architecture and production-continuity risk, records owner/external
gates and deadlines, selects milestones and publication topology, verifies Git
targets, and admits resources before writers start.

Readiness defects automatically use the installed `$grill-me` workflow. The
phase foundation is completed first; individual tasks or cohesive milestones
are then grilled in full. The owner may stop between those units and execute
the independently frozen subgraph while remaining nodes stay
`needs-interview`.

Cross-cutting or high-risk architecture always uses a `gpt-5.6-sol` / `xhigh`
lead plus a different `gpt-5.6-sol` / `xhigh` specification reviewer during
this pilot. The lead remains recallable. A semantic scope-growth trigger
pauses the affected lane for redesign, independent review, and re-freeze.

The orchestrator derives cohesive milestones from the frozen graph. Ordinary
lane commits and branches stay local; milestone commits and
recovery-critical long lanes are published. Writers run focused checks; exact
milestone SHAs and exceptional high-risk lanes receive independent review;
published milestones receive full CI.

Direct `main` integration remains allowed for independently production-safe
milestones. Any intermediate change that disables or regresses active
production behavior uses an integration branch or draft PR until the safe
replacement is ready. A standalone safety quarantine requires explicit owner
approval plus exact impact, fallback status, rollback, recovery owner, and a
time-bounded restoration gate.

Explicit `$orchestrate` invocation authorizes scoped commits and non-force
pushes to the verified canonical repository and allowlisted refs. It also
authorizes bounded draft-PR operations, exact pre-authorized merges, one failed
milestone CI rerun, and cancellation of superseded CI. The frozen packet and
repository auto-review policy define the exact limits.

Machine admission uses separate task-agent, writable-worktree, and heavy-job
budgets. At most two linked task worktrees are admitted, and the checked-in
resource profile/helper may reduce concurrency. Missing disk or memory evidence
fails closed. Ledger updates occur only at recovery-relevant state transitions.

The restarted P1 run will create the substantive P1 execution packet and any
P1-10 architecture brief from the then-current repository state. This ADR does
not claim that the current P1 task file is already reviewed or frozen.

## Alternatives considered

- **Keep per-task just-in-time audits:** rejected because P1-10 proved that a
  local task can hide phase-wide seams and production-continuity consequences.
- **Require one task per orchestration run:** rejected because the owner needs
  unattended phase-scale progress and selected a strong root model for that
  judgment.
- **Hard-code three or four milestones:** rejected because one interim run is
  insufficient evidence for a universal number.
- **Push and run full CI for every lane:** rejected because isolation does not
  require remote publication and the ceremony dominated narrow slices.
- **Require PRs for every change or protect `main` now:** rejected because safe
  isolated changes should retain a direct-main path during the pilot.
- **Build an orchestration telemetry service:** rejected because native Codex,
  Git, PR, and CI evidence is sufficient for the next owner-triggered analysis.

## Consequences

- Preview may pause for owner grilling before writers start, but can release a
  fully interviewed independent subgraph when owner time is limited.
- Architecture and specification review spend more high-tier allowance up
  front to reduce expensive downstream drift.
- Worktrees remain isolated while remote branches, main pushes, CI runs, and
  ledger edits should be materially fewer.
- Production-degrading migrations consume a branch/PR until their safe release
  unit is ready; emergency quarantine remains possible but visible and timed.
- Resource pressure queues heavy work instead of overloading the laptop.
- The next P1 analysis may relax Sol/xhigh or tune resource and milestone
  defaults when evidence supports it.

## Affected tasks

- All Bundesliga P1 tasks, beginning with a restarted preview of
  [P1-10](../tasks/p1-10-schadensfresse-primary-community.md).
- [Bundesliga execution strategy](../execution-strategy.md).
- Repository `$orchestrate` and auto-review policy.

## Supersedes

- [ADR-0009](0009-bounded-orchestration-and-hybrid-git.md).
- [ADR-0023](0023-use-orchestrator-created-cli-worktrees.md).

ADR-0061 retains their two-worktree ceiling, locator-based secret resolution,
single-writer worktree ownership, hybrid direct-main/PR option, serialized Git
integration, and cleanup requirements. It replaces their per-lane publication,
concurrent-full-gate default, just-in-time design, and review/CI cadence.
