# P1-08 — Route schadensfresse mixed-competition predictions

- Status: Not started
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0054](../decisions/0054-copy-schadensfresse-bundesliga-from-pes-squad.md)

## Outcome

`schadensfresse` continues to copy ordinary Bundesliga predictions from
`pes-squad`, but switches explicitly to target-owned primary generation for its
DFB-Pokal and Champions-League finals. Its three open Champions-League bonus
questions are routed deliberately before their `2026-09-09T10:00:00Z`
deadline rather than being assumed to match the Bundesliga reference bonus set.

## Work items

- [ ] Inventory the exact Kicktipp match and bonus identities, deadlines, and
      result-boundary representation for the included DFB-Pokal and Champions-
      League finals without recording private prediction payloads.
- [ ] Add a competition-aware routing contract that cannot copy a DFB-Pokal or
      Champions-League fixture from the Bundesliga-only `pes-squad` source.
- [ ] Introduce question/fixture-specific competition typing plus prompt,
      context, and result-basis routing. Do not inject DFB/CL clauses into the
      canonical Bundesliga rules document or use an untyped target fallback as
      mixed-competition support.
- [ ] Generate those final predictions independently with target-owned
      competition-correct context, the Owner-selected production model
      identity, and an after-penalties result basis.
- [ ] Decide and implement the collection/storage identity for the two added
      competitions without weakening `bundesliga-2026-27`'s explicit storage
      boundary or exact-nine matchday contract.
- [ ] Route the three Champions-League bonus questions to a CL-specific prompt
      and context before `2026-09-09T10:00:00Z`. Reuse a reference only if an
      accepted CL source exists and ADR-0048 exact compatibility passes;
      otherwise generate through an explicit CL primary path.
- [ ] Add ordered manual validation, payload-safe Firestore/Langfuse/Kicktipp
      inspection, and a separate reviewed recurring or deadline-driven
      orchestration change before the first affected cutoff.
- [ ] Add focused command, persistence, workflow-contract, and schedule tests
      covering the ordinary-Bundesliga copy path and both mixed-competition
      primary exceptions.

## Complete when

- No DFB-Pokal or Champions-League match can silently reuse a Bundesliga source
  prediction.
- The two finals use the after-penalties result basis and pass live validation
  before activation.
- The three Champions-League bonus questions have an explicit, tested
  competition/prompt/context route before `2026-09-09T10:00:00Z`.
- Ordinary Bundesliga match and compatible bonus copies remain zero-model-call
  paths sourced from `pes-squad`.
