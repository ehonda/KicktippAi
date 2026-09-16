# P1-04 — Activate official Club Elo context refresh

- Status: D0 durable issue-create fence documented; F2 and all runtime gates pending
- Last reconciled: 2026-09-16
- Depends on: [ADR-0083](../decisions/0083-activate-official-club-elo-context-refresh.md) and [ADR-0084](../decisions/0084-fence-context-source-issue-creation.md)

## Outcome

P1-04 remains incomplete until the existing activation evidence gates and the
new durable issue-create fence are accepted. D0 changes documentation only.
F2 later ensures that the exact canonical marker has one durable lineage across
bodies, watermarks, cycles, processes, and run_attempts: it arms before POST,
binds verified identity, and exposes uncertainty or legacy absence as Pending
manual recovery rather than creating again.

## Milestones

- [x] D0: accepted durable issue-create-fence documentation and ownership refreeze.
- [ ] E2: parser-v2/current source grammar, versioned descriptor evidence and fixtures.
- [ ] W2 transport: immutable GitHub artifact transport and Node bridge only.
- [ ] F2: durable issue fence, atomic genesis, and transferred issue projector/tests.
- [ ] V2: DI, source-only CLI and workflow validation path; normal flags remain false.
- [ ] Live validation: real source-only development then production evidence.
- [ ] A2: atomic all-eight normal source enable after evidence.
- [ ] Closeout: final review/CI, merge, Pages and post-merge evidence.

The release graph is D0 -> E2 and W2 transport -> F2 -> V2 -> original live
evidence -> A2 -> closeout. F2 requires the ADR-0084 real-transaction and
controllable-HTTP crash/replay/genesis/legacy/listing/CAS matrix, solution build,
full Core/FirebaseAdapter emulator/Orchestrator suites, deterministic Node/lock
validation, fresh review, and exact-head CI. No runtime/source/live effect
exists. P1-05/R1 remains ADR-0082 deferred.
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


## D0 durable issue-create fence dependency

ADR-0084 adds D0 documentation and F2 durable issue fencing before V2. The
fence is marker-wide and durable across changed body, watermark, cycle,
process, and run_attempt. F2 must arm CreateUncertain in a current-health CAS
before one POST, bind only verified identity, re-admit POST/PATCH against
current Pending health, and retain all uncertain or legacy absence cases for
owner-directed manual recovery. It must prove crash/replay/genesis/legacy/
listing/stale-CAS behavior under the ADR-0084 validation gate. No runtime or
live source effect exists yet; P1-05/R1 remains deferred.
