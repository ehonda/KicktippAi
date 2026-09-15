# P1-04 — Activate official Club Elo context refresh

- Status: In progress — operational activation
- Last reconciled: 2026-09-16
- Depends on: accepted [ADR-0083](../decisions/0083-activate-official-club-elo-context-refresh.md)
- Decisions: [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md), [ADR-0077](../decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](../decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0080](../decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](../decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0083](../decisions/0083-activate-official-club-elo-context-refresh.md)

## Outcome

P1-04 completes only after a current official Club Elo observation is accepted,
handed off immutably and reaches all eight ordered receipts and four physical
community heads with truthful provenance, health and issue reconciliation.
Merged PR #111 is prior dormant C3/E1 implementation, not current completion.

## Milestones

- [x] S0: accepted durable activation specification and current-plan records.
- [ ] E2: parser-v2/current source grammar, versioned descriptor evidence and fixtures.
- [ ] W2: immutable GitHub artifact transport and post-commit issue projection.
- [ ] V2: DI, source-only CLI and workflow validation path; normal flags remain false.
- [ ] Live validation: real source-only development then production evidence.
- [ ] A2: atomic all-eight normal source enable after evidence.
- [ ] Closeout: final review/CI, merge, Pages and post-merge evidence.

E2 and W2 may proceed independently after S0. V2 requires their cumulative
acceptance; A2 requires V2 plus real operational evidence. Each milestone
requires a scoped local commit, appropriate review and cumulative validation.

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

1. Run offline/emulator suites including hostile parser, eight-lane, replay and
   late-cycle matrices.
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
7. Run a distinct later production cycle and same-run replay; no fabricated
   `Unchanged`, reacquisition, reupload, head or health change is acceptable.
8. Change all eight normal source flags atomically, then obtain fresh final
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
