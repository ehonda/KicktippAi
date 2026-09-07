# Bundesliga 2026/27 P1 status snapshot

- Snapshot date: 2026-09-06
- Verified branch: `main`
- Verified local and remote head: `204cfd8db2c4163ca320a7c8a1829ebbc2ee1ed2`
- Reviewed P1-04/P1-05 planning baseline: `c99e1635428bcfea48271e4169767b38f014148c`
- Purpose: owner-directed stop checkpoint for the next explicit orchestration session

This is a snapshot, not a new execution decision or production-activation
authority. The task records, accepted ADRs, designs, and execution strategy
remain authoritative. A later orchestration session must re-verify repository,
task, dependency, source-evidence, pull-request, and owner-gate state, then
update this file and its snapshot date before relying on it.

The verified head's exact
[Build and Test run](https://github.com/ehonda/KicktippAi/actions/runs/34059343588)
completed successfully with all 12 check runs green. The reviewed P1-04/P1-05
planning baseline is an ancestor of that head. The later `204cfd8` commit is an
unrelated orchestration-analysis change that must be preserved.

Neither P1-04 nor P1-05 is complete. Their exact in-flight state and first
resume actions are recorded separately in the
[P1-04 official-HTML handoff](handoffs/p1-04-club-elo-html-in-flight-2026-09-06.md)
and [P1-05 roster-refresh handoff](handoffs/p1-05-roster-refresh-in-flight-2026-09-06.md).

## Status

| Item | Snapshot state | Implementation readiness |
|---|---|---|
| P1-01 / P1-02 | Promoted into completed P0-15 / P0-16 work | Complete; no P1 implementation remains |
| [P1-03](archive/p1/tasks/p1-03-generic-onboarding-skill.md) | Generic competition onboarding tooling | Complete |
| [P1-04](tasks/p1-04-club-elo-refresh.md) | Official HTML selected; exact evidence captured; successor specification review remains blocked on two bounded corrections; no ADR-0075 or runtime exists | In flight; correct and independently re-review the specification before a tracked re-freeze or writer |
| [P1-05](tasks/p1-05-roster-refresh.md) | Source-neutral common foundation is local-only at `852d179`; focused tests are green, but fresh cumulative review and integration are pending; roster source implementation has not started | In flight independently of P1-04; review and integrate the common seam first |
| [P1-06](tasks/p1-06-observability-datasets.md) | Not started; owner frontier and exact stored-prediction evidence remain | Not ready |
| [P1-07](tasks/p1-07-cost-calibration.md) | Not started; depends on P1-04 and P1-05 and remains ungrilled | Not ready |
| [P1-08](archive/p1/tasks/p1-08-schadensfresse-mixed-competition-routing.md) | Fully absorbed by P1-10 | Superseded; do not implement |
| [P1-09](archive/p1/tasks/p1-09-current-open-matchday-context.md) | Current open-matchday reconciliation delivered | Complete |
| [P1-10](tasks/p1-10-schadensfresse-primary-community.md) | Target-primary conversion remains isolated on an atomic draft-PR route | In progress but deliberately last; blocked on P1-13 completion |
| [P1-11](tasks/p1-11-langfuse-v4-migration.md) | Not started; fresh migration inventory and owner interview remain | Not ready |
| [P1-12](archive/p1/tasks/p1-12-standings-reprediction-exemption.md) | Standings-only exemption delivered and validated | Complete |
| P1-13 / R4a | Global typed authority is preserved in [draft PR #97](https://github.com/ehonda/KicktippAi/pull/97); R3 is accepted and R4a is partially implemented locally | Technically resumable at R4a, but intentionally deferred with P1-10 until last |
| [P1-14](archive/p1/tasks/p1-14-history-source-continuity.md) | Source repair, bounded proxy continuity, and maintenance reporting delivered | Complete |
| [P1-15](archive/p1/tasks/p1-15-schadensfresse-champions-league-bonus.md) | All three frozen answers verified in Firestore and Kicktipp | Complete |
| [P1-16](tasks/p1-16-automatic-history-date-updates.md) | Deferred, low urgency, and explicitly needs interview | Not ready |

## Resume order for P1-04 and P1-05

1. Commission a fresh cumulative independent review of the clean, local-only
   common range
   `c99e1635428bcfea48271e4169767b38f014148c..852d1798e77d78e4dee4350ddc1d59cba54f60a5`.
2. If approved, integrate that reviewed source-neutral content on top of the
   then-current `main`, preserving `204cfd8` and the separate dirty P1-10
   worktree. P1-05 may then start its frozen source implementation without
   waiting for P1-04.
3. Independently correct P1-04's official-HTML specification: expand the
   common amendment to include health and Firebase descriptor reconstruction
   plus their tests/coordinator fixture, and remove the diagnostic
   double-prefix ambiguity. A different reviewer must approve the result
   before ADR-0075 and a material P1-04 re-freeze.
4. Do not start another P1 item from this checkpoint. Reprioritize in the new
   session after the orchestration-protocol improvements are applied.

Current DuckDB evidence is a safe rejection, not a blocker to implementation:
the 210,776,064-byte artifact has SHA-256
`ba1eff7337b8ca78cb533df0b6eba0d6fc58218e0767460f6770e0b37f5a2113`,
embedded revision `e44f186d6f06dd8452aaf54c7921ba66c961f637`, zero eligible
L1/`last_season=2026` club or player rows for all 18 manifest IDs, and no
authoritative revision-bound source dates. It must retain fallback/LKG with
`NO_ELIGIBLE_2026_MEMBERSHIP` and `UNKNOWN_SOURCE_DATE`; a synthetic future
artifact proves takeover.

Current Club Elo evidence is the exact official `https://clubelo.com/GER` HTML
body dated `2026-09-04`, 562,238 bytes, SHA-256
`a342b6f83dadbb49599c0fe6364ea3288f0953381d5b35e923eb87a03296aa59`.
The Owner selected that source, but selection alone is not an accepted
successor contract, unattended-network authority, or activation evidence.

## Preserved P1-10 / P1-13 state

At this snapshot, PR #97 is open and draft with remote head
`fffa486a005c3f3e4723168c5f2339406879488f`; its exact-head checks are green.
The linked local worktree is at accepted documentation tip
`634b65316422b545ecd1956996a74525fafdd80d` and intentionally contains the
incomplete, uncommitted R4a runtime and test migration. Preserve that worktree;
do not clean, merge, activate, or treat the partial R4a seam as completed
evidence.
