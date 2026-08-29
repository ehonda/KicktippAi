# P1-10 — Convert schadensfresse to a primary community

- Status: Not started
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md), [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md), [ADR-0055](../decisions/0055-add-schadensfresse-to-production-live-lane.md)

## Trigger

The `schadensfresse` community rules changed after its production-copy path was
accepted. The current rules award 2/3/5 points for a winning tendency, goal
difference, and exact result, and 3/–/5 points for a draw. `schadensfresse`
therefore no longer has the same ordinary Bundesliga scoring contract as
`pes-squad`, so it must not remain a secondary/copy community.

The other currently visible boundaries remain explicit: Bundesliga results are
evaluated after 90 minutes, while DFB-Pokal and Champions-League results are
evaluated after a penalty shootout. Tips close with zero minutes of lead time.

## Outcome

`schadensfresse` is an independent primary community for match and bonus
predictions. It generates from its own rules and target-owned context using the
Owner-selected production model configuration rather than copying predictions
from `pes-squad`. The conversion also absorbs P1-08's competition-correct
primary routing requirements for DFB-Pokal and Champions League.

## Work items

- [ ] Re-read the live Kicktipp rules and inventory the exact current match,
      bonus, deadline, scoring, and result-boundary contract without recording
      private prediction payloads.
- [ ] Add and accept a new ADR that makes `schadensfresse` a primary production
      row and narrowly supersedes ADR-0054's copy decision plus ADR-0055's
      scheduled match-copy topology.
- [ ] Update the target-owned rules document to the verified 2/3/5 win and
      3/–/5 draw scoring, 90-minute Bundesliga boundary, and after-penalties
      DFB-Pokal/Champions-League boundary.
- [ ] Change the schadensfresse match and bonus entrypoints from `pes-squad`
      reference copying to target-owned generation with
      `community_context: schadensfresse` and the production model, reasoning,
      prompt, output-cap, and service-tier identity selected by ADR-0052 or its
      accepted successor.
- [ ] Remove or fail closed on schadensfresse-to-pes-squad copy aliases and
      provenance paths so no ordinary Bundesliga, cup, Champions-League, or
      bonus prediction can silently fall back to the old secondary topology.
- [ ] Preserve competition-aware prompt, context, storage, and result-basis
      routing for Bundesliga, DFB-Pokal, and Champions League; do not flatten
      their different result boundaries into one untyped rules path.
- [ ] Update the production-live lane to run the schadensfresse primary path
      after target-owned context collection while retaining the accepted
      cadence, concurrency, failure, monitoring, and rollback contracts unless
      the new ADR explicitly changes one.
- [ ] Plan existing copied-prediction replacement explicitly. Do not force a
      reprediction or consume a production model call until the Owner approves
      the exact live transition and reprediction budget.
- [ ] Add focused command, persistence, provenance, workflow-contract, and
      schedule tests proving target-owned primary generation and rejecting the
      former copy configuration.
- [ ] Run ordered manual validation and payload-safe Kicktipp, Firestore, and
      Langfuse inspection before activating the revised recurring path.
- [ ] Verify the exact Git target, commit the scoped changes intentionally, and
      push the explicit remote and branch.

## Complete when

- Every schadensfresse match and bonus prediction uses its own verified rules,
  context, storage identity, and production-model generation path.
- No schadensfresse prediction can copy from or inherit immutable prediction
  provenance from `pes-squad`.
- Bundesliga uses the verified 90-minute and 2/3/5 scoring contract, while
  DFB-Pokal and Champions League use their verified after-penalties routes.
- The revised manual ladder and first reviewed recurring execution pass with
  target-owned Firestore and Langfuse provenance and the expected Kicktipp
  results.
