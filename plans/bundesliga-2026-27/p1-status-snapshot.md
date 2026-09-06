# Bundesliga 2026/27 P1 status snapshot

- Snapshot date: 2026-09-06
- Verified branch: `main`
- Verified local and remote head: `33ff358b604748c67d9f87cc98fb8a01fa336d37`
- Planning baseline: `538c30c53870faa608cf0d6e6a9dbf20f8d833d3`
- Purpose: dated readiness handoff for the next explicit orchestration session

This is a snapshot, not a new execution decision or production-activation
authority. The task records, accepted ADRs, designs, and execution strategy
remain authoritative. A later orchestration session must re-verify repository,
task, dependency, source-evidence, pull-request, and owner-gate state, then
update this file and its snapshot date before relying on it.

The planning baseline is an ancestor of the verified head, and its exact
[Build and Test run](https://github.com/ehonda/KicktippAi/actions/runs/33972378750)
completed successfully. Changes after that baseline do not modify the P1
planning files summarized here.

## Status

| Item | Snapshot state | Implementation readiness |
|---|---|---|
| P1-01 / P1-02 | Promoted into completed P0-15 / P0-16 work | Complete; no P1 implementation remains |
| [P1-03](tasks/p1-03-generic-onboarding-skill.md) | Generic competition onboarding tooling | Complete |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Owner interview and planning accepted; runtime untouched | **Ready to enter implementation first** |
| [P1-05](tasks/p1-05-roster-refresh.md) | Owner interview and planning accepted; runtime untouched | **Ready to enter implementation second, after P1-04** |
| [P1-06](tasks/p1-06-observability-datasets.md) | Not started; owner frontier and exact stored-prediction evidence remain | Not ready |
| [P1-07](tasks/p1-07-cost-calibration.md) | Not started; depends on P1-04 and P1-05 and remains ungrilled | Not ready |
| [P1-08](tasks/p1-08-schadensfresse-mixed-competition-routing.md) | Fully absorbed by P1-10 | Superseded; do not implement |
| [P1-09](tasks/p1-09-current-open-matchday-context.md) | Current open-matchday reconciliation delivered | Complete |
| [P1-10](tasks/p1-10-schadensfresse-primary-community.md) | Target-primary conversion remains isolated on an atomic draft-PR route | In progress but deliberately last; blocked on P1-13 completion |
| [P1-11](tasks/p1-11-langfuse-v4-migration.md) | Not started; fresh migration inventory and owner interview remain | Not ready |
| [P1-12](tasks/p1-12-standings-reprediction-exemption.md) | Standings-only exemption delivered and validated | Complete |
| P1-13 / R4a | Global typed authority is preserved in [draft PR #97](https://github.com/ehonda/KicktippAi/pull/97); R3 is accepted and R4a is partially implemented locally | Technically resumable at R4a, but intentionally deferred with P1-10 until last |
| [P1-14](tasks/p1-14-history-source-continuity.md) | Source repair, bounded proxy continuity, and maintenance reporting delivered | Complete |
| [P1-15](tasks/p1-15-schadensfresse-champions-league-bonus.md) | All three frozen answers verified in Firestore and Kicktipp | Complete |
| [P1-16](tasks/p1-16-automatic-history-date-updates.md) | Deferred, low urgency, and explicitly needs interview | Not ready |

## Ready implementation queue

1. P1-04 is ready to begin without further owner grilling.
2. P1-05 is also fully decided, with accepted sequencing after P1-04.
3. P1-13's R4a slice is technically resumable, but the accepted priority keeps
   P1-13 and P1-10 last rather than making R4a the next lane.

For P1-04 and P1-05, readiness means implementation may start; it does not
authorize immediate workflow edits or production activation. The implementation
must first resolve the remaining technical/source gates in
[ADR-0073](decisions/0073-refresh-strength-and-rosters-during-context-collection.md)
and the accepted
[context-refresh design](designs/p1-04-05-context-refresh.md):

- prove Club Elo CSV date and name semantics;
- establish a trustworthy DuckDB source-date recipe or reject the candidate as
  `UNKNOWN_SOURCE_DATE`;
- independently review the minimal cycle-health and artifact-handoff seam; and
- validate development-first, with first production enablement separately
  reviewed.

These are implementation gates, not unresolved owner-policy decisions.

## Preserved P1-10 / P1-13 state

At this snapshot, PR #97 is open and draft with remote head
`fffa486a005c3f3e4723168c5f2339406879488f`; its exact-head checks are green.
The linked local worktree is at accepted documentation tip
`634b65316422b545ecd1956996a74525fafdd80d` and intentionally contains the
incomplete, uncommitted R4a runtime and test migration. Preserve that worktree;
do not clean, merge, activate, or treat the partial R4a seam as completed
evidence.
