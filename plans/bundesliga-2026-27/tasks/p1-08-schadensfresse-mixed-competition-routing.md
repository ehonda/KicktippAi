# P1-08 — Route schadensfresse mixed-competition predictions

- Status: Superseded by [P1-10](p1-10-schadensfresse-primary-community.md)
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md), [ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md)

## Supersession

[ADR-0058](../decisions/0058-make-schadensfresse-a-competition-typed-primary.md)
establishes that `schadensfresse` is a target-owned primary for every match and
bonus question. [P1-10](p1-10-schadensfresse-primary-community.md) therefore
fully absorbs this task's DFB-Pokal and Champions-League typing, prompt,
context, result-basis, validation, and activation requirements. Do not build
the former Bundesliga-copy-plus-exceptions design.

The outcome and checklist below are retained only as historical planning
context. Their September 9 deadline, four-point bonus score, ordinary
Bundesliga copy, and temporary exception-routing premises are stale and
superseded. P1-10 records the corrected live evidence and implementation-ready
contract.

## Historical outcome — superseded

The former plan kept ordinary Bundesliga predictions copied from `pes-squad`
while switching only DFB-Pokal and Champions-League work to target-owned
generation. The live scoring-rule change invalidated that split topology.

## Historical work items — superseded

- Inventory mixed-competition fixture/question identities and result bases.
- Add DFB-Pokal and Champions-League exceptions to the Bundesliga copy path.
- Route three CL bonus questions before the formerly reported September 9
  deadline.
- Validate the exception paths separately from ordinary copy operation.

## Complete when

No separate completion applies. This task is superseded; P1-10 and ADR-0058
own all acceptance criteria.
