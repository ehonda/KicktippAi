# P1-04 — Activate official Club Elo context refresh

- Status: In progress; flags-off v2 runtime merged, production source-only acceptance/retention observed, controlled ordinary continuation and A2 pending
- Last reconciled: 2026-09-29
- Depends on: [ADR-0086](../decisions/0086-bound-live-club-elo-replay-gate.md), [ADR-0085](../decisions/0085-isolate-optional-club-elo-refresh.md), retained [ADR-0083](../decisions/0083-activate-official-club-elo-context-refresh.md), and [ADR-0084](../decisions/0084-fence-context-source-issue-creation.md)

## Outcome and milestones

Optional refresh failure must permit required ordinary production context collection with verified retained Elo. Existing ordinary failures still stop dependents. PR #116 merged the v2-compatible reader on main with all eight normal source flags false; exact main CI passed. Run `36478629294` accepted one official production observation and produced eight receipts/four heads, and run `36481153403` safely retained them after source rejection. Its attempt-2 replay failed when the original artifact became unavailable. ADR-0086 records that failed gate and removes live rerun success as an A2 prerequisite under the owner's bounded-uncertainty instruction. A controlled normal-job failure/ordinary-success proof is still required before activation.

- [x] D0: prior accepted durable-fence documentation.
- [x] Reviewed ADR-0085 successor-document milestone.
- [x] Concrete finite transport/security corrections and F2 durable fence with ADR-0084 real-transaction correctness matrix.
- [x] Source-only profile/development request and early authorization; optional workflow boundary and strict retained-head preservation.
- [x] Focused regressions, one coherent solution build, fresh acceptance and exact-head required CI for the flags-off runtime.
- [x] Branch Actions development/production accepting and later retention/rejection evidence; the failed replay is recorded under ADR-0086 without success credit.
- [ ] Controlled refresh failure with verified old Elo and required ordinary continuation; no prediction authority from this proof.
- [ ] A2: reviewed atomic all-eight source activation.
- [ ] Closeout/merge, Pages and next existing scheduled-run observation.

This ordered graph replaces the serial local timeout/confirmation program. Known product/security defects remain blockers. Historical 54 reds (P4/H12/Z31/Docker7) stay failed evidence; no xfail/flake waiver or pass is implied. CT correction 1/1 and diagnostic execution 1/1 remain spent. Runtime/concurrency audit is parked and historical timeout cause remains uncertain. P1-05/R1 stays deferred.

## Fixed receipt contract

```text
pes-squad-context
schadensfresse-context
relaxdays-tippt-context
arena-sol-xhigh-context
arena-sol-high-context
arena-luna-medium-context
arena-terra-xhigh-context
arena-luna-none-context
```

The five arena receipts share the `ehonda-ai-arena` context. The other three
heads are `pes-squad`, `schadensfresse`, and `relaxdays-tippt`; there are four
physical heads, never eight. Each lane selection and receipt is independent.

## Required evidence and release gates

1. Pass ADR-0085 focused seam/regression matrices, the F2 real-transaction
   correctness matrix, one coherent solution build and required exact-head CI.
2. Run a real official local development source-only dry run on the reviewed
   tip and retain UTC/URL/response/hash/parser/date/coverage evidence privately.
3. Persist one development source-only cycle and re-read its receipt, context,
   aggregate, original/source dates and descriptor/payload binding.
4. Run a distinct later development cycle with truthful retention/no-change or
   rejection evidence.
5. Dispatch branch Actions development source-only validation with no model
   jobs, credentials or GitHub artifact/issue effect.
6. Dispatch branch Actions production source-only validation: one immutable
   artifact, an eligible advance for at least one lagging target, eight ordered
   receipts, four heads, health and desired issue reconciliation.
7. Run a distinct later production cycle; no fabricated `Unchanged`,
   reacquisition, reupload, head or health change is acceptable. Record the
   failed cross-attempt replay and its absent artifact under ADR-0086; live
   rerun success is no longer an activation prerequisite.
8. Prove controlled refresh failure preserves verified old Elo provenance,
   payloads and receipts while permitting executable
   ordinary collection; then change all eight normal source flags atomically
   and obtain fresh final
   review, exact-head CI, merge and post-merge main/Pages/source-only/schedule
   observation.

An accepting source-only result does not certify a failing whole-profile
schedule. If history blocks later scheduled work, record the blocked
whole-schedule observation without calling P1-04 complete.

## Authority and recovery

The owner authorization is limited to Club Elo reads/context writes/GitHub
handoff and issue effects/existing-schedule activation after evidence/Pages and
ready-PR merge. It excludes model, prediction, posting, roster, new schedule,
credential-routing and unrelated P1 changes. The project owner is recovery
owner. Rollback is a reviewed all-eight normal-flag-off commit; it does not
delete source state, reset heads or change topology. Restoration needs a
corrected reviewed tip, accepted then later distinct retention evidence, all
receipts, correct heads/health/issues and a fresh activation gate.
