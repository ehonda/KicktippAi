# P0-24 — Prove bonus copy-post question and option compatibility

- Status: Not started
- Priority: P0
- Depends on: [P0-16](p0-16-question-aware-bonus-context.md), [P0-17](p0-17-community-scope.md), and [P0-18](p0-18-base-workflow-support.md)
- Gates: production copies of [P0-19](p0-19-community-workflow-triad.md) and [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0037](../decisions/0037-record-immutable-bonus-context-manifests.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md)

## Outcome

The `arena-production-copy` invocation reuses a stored `pes-squad` bonus prediction only when the source and target expose the exact same normalized question, selection constraint, and complete normalized option set. Compatibility performs no model call. A missing or incompatible source candidate, legacy/malformed provenance, question, `MaxSelections`, or option-set incompatibility instead produces exactly one independent target prediction in the same invocation. Only an invalid target definition/selection or an immutable-context safety violation fails closed.

## Work items

- [ ] Record the durable normalization and compatibility contract in a new ADR before implementation. It must compare normalized question identity, `MaxSelections`, and the complete normalized option set, not only question text, form-field ID, selected options, or option count.
- [ ] Define a deterministic canonical option-set representation that is independent of community-local option IDs and presentation order, rejects ambiguous duplicate normalized options, and detects every missing, extra, or changed option.
- [ ] Persist the canonical question/complete-option-set identity and content hash with newly generated bonus prediction provenance so it can be exact-read with the approved model configuration and source community context.
- [ ] Treat legacy or partial prediction metadata without the complete compatibility provenance as incompatible. Do not reconstruct or infer historical provenance from the target's current Kicktipp page.
- [ ] Before posting, exact-read the `pes-squad` source prediction and provenance, compare them with the current `ehonda-ai-arena` question, and map every selected canonical source option to exactly one target option ID.
- [ ] On an exact compatibility match, copy-post the stored selection without constructing or calling a prediction service. Record payload-safe telemetry for copy source, source prediction identity, compatibility hash, and the no-model-call outcome.
- [ ] On a missing or incompatible source candidate, legacy/partial/malformed provenance, question, `MaxSelections`, or complete-option-set mismatch, stop the copy branch before any Kicktipp write and generate exactly one independent target prediction in the same invocation using the approved production configuration.
- [ ] Resolve immutable bonus context and persist the independently generated prediction under target community context `ehonda-ai-arena`; never resolve or save the mismatch fallback under requested copy-source context `pes-squad`. Only the compatible copy branch may read and reuse the `pes-squad` source prediction. Record the branch explicitly; do not present the model call as a successful copy.
- [ ] Reserve fail-closed behavior for an invalid or ambiguous target question/selection, failure to map the independently generated selection to the target, or an immutable target-context/provenance safety violation. Ordinary copy incompatibility must not terminate without the one independent target prediction.
- [ ] Preserve match copy-post fixture compatibility and ordinary self-contained bonus generation behavior; this task must not relax their existing provenance checks.
- [ ] Update production workflow/task documentation so the copy and independent-generation paths, credentials, telemetry, and failure behavior are explicit before P0-21.

## Validation

- Test exact normalized compatibility when raw question/option formatting or community-local IDs/order differ but canonical identities are equal.
- Test changed question text or `MaxSelections`, missing/extra/changed/duplicate source options, missing source candidate, and legacy/partial/malformed source provenance; every case must refuse copy and make exactly one independent target prediction in the same invocation.
- Prove the compatible path performs exactly one copy-post and zero model-service constructions/calls.
- Prove every ordinary incompatible path constructs/calls the approved target prediction service exactly once, uses `ehonda-ai-arena` context, and posts exactly the independently generated target selection.
- Prove mismatch/no-candidate context resolution, prediction persistence, outdated checks, and telemetry all bind `communityContext=ehonda-ai-arena` and never write a new prediction under `pes-squad`.
- Prove invalid/ambiguous target definitions or selections and immutable target-context safety violations fail closed without a post; these are the only incompatibility-related no-post outcomes.
- Add repository round-trip, command, telemetry, workflow-contract, and production-topology coverage for the immutable compatibility fields and copy source.
- In P0-21, inspect one compatible production copy end to end and retain any real mismatch as evidence of independent generation or fail-closed behavior without recording question/option payloads.

## Complete when

- A reviewer can reproduce the compatibility decision from immutable source provenance and the target's complete normalized question/option identity.
- Compatible arena bonus posting reuses the `pes-squad` prediction without an extra model call.
- Every ordinary mismatch or missing-provenance case produces exactly one declared independent target prediction, while target-definition/selection and immutable-context safety violations fail closed.
