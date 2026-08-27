# P0-24 — Prove bonus copy-post question and option compatibility

- Status: Complete
- Priority: P0
- Depends on: [P0-16](p0-16-question-aware-bonus-context.md), [P0-17](p0-17-community-scope.md), and [P0-18](p0-18-base-workflow-support.md)
- Gates: production copies of [P0-19](p0-19-community-workflow-triad.md) and [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0037](../decisions/0037-record-immutable-bonus-context-manifests.md), [ADR-0038](../decisions/0038-bound-bonus-context-by-question-policy.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0048](../decisions/0048-verify-bonus-compatibility-before-reference-copy.md)

## Outcome

The `arena-production-copy` invocation reuses a stored `pes-squad` bonus prediction only when the source and target expose the exact same normalized question, selection constraint, and complete normalized option set. Compatibility performs no model call. A missing or incompatible source candidate, legacy/malformed provenance, question, `MaxSelections`, or option-set incompatibility instead produces exactly one independent target prediction in the same invocation. Only an invalid target definition/selection or an immutable-context safety violation fails closed.

## Work items

- [x] Record the durable normalization and compatibility contract in a new ADR before implementation. It must compare normalized question identity, `MaxSelections`, and the complete normalized option set, not only question text, form-field ID, selected options, or option count.
- [x] Define a deterministic canonical option-set representation that is independent of community-local option IDs and presentation order, rejects ambiguous duplicate normalized options, and detects every missing, extra, or changed option.
- [x] Persist the canonical question/complete-option-set identity and content hash with newly generated bonus prediction provenance so it can be exact-read with the approved model configuration and source community context.
- [x] Treat legacy or partial prediction metadata without the complete compatibility provenance as incompatible. Do not reconstruct or infer historical provenance from the target's current Kicktipp page.
- [x] Before posting, exact-read the `pes-squad` source prediction and provenance, compare them with the current `ehonda-ai-arena` question, and map every selected canonical source option to exactly one target option ID.
- [x] On an exact compatibility match, copy-post the stored selection without constructing or calling a prediction service. Record payload-safe telemetry for copy source, source prediction identity, compatibility hash, and the no-model-call outcome.
- [x] On a missing or incompatible source candidate, legacy/partial/malformed provenance, question, `MaxSelections`, or complete-option-set mismatch, stop the copy branch before any Kicktipp write and generate exactly one independent target prediction in the same invocation using the approved production configuration.
- [x] Resolve immutable bonus context and persist the independently generated prediction under target community context `ehonda-ai-arena`; never resolve or save the mismatch fallback under requested copy-source context `pes-squad`. Only the compatible copy branch may read and reuse the `pes-squad` source prediction. Record the branch explicitly; do not present the model call as a successful copy.
- [x] Reserve fail-closed behavior for an invalid or ambiguous target question/selection, failure to map the independently generated selection to the target, or an immutable target-context/provenance safety violation. Ordinary copy incompatibility must not terminate without the one independent target prediction.
- [x] Preserve match copy-post fixture compatibility and ordinary self-contained bonus generation behavior; this task must not relax their existing provenance checks.
- [x] Update production workflow/task documentation so the copy and independent-generation paths, credentials, telemetry, and failure behavior are explicit before P0-21.

## Completion evidence — 2026-08-25

- [ADR-0048](../decisions/0048-verify-bonus-compatibility-before-reference-copy.md) records the accepted Form-KC/whitespace normalization, ordinal case- and accent-sensitive comparison, canonical complete-option-set hash, exact-read, mapping, independent-generation, fail-closed, lazy-service, and payload-safe compatibility-metadata contracts.
- Cumulative task branch SHA `774b9a6e11c05809a5105063df1df80aebb0a857` was integrated on `main` as `5fae2b1` (manifest, persistence, command, ADR, and primary tests), `0de314d` (generated-output fail-closed validation and expanded matrix), and `e927564` (final literal compatibility/topology coverage). The cumulative result received independent approval after the review-driven follow-ups.
- Core compatibility coverage passed its focused fixture 6/6 and full suite 283/283. It proves normalization-equivalent formatting, ID/order independence, missing/extra/changed/duplicate detection, and explicit case/accent sensitivity.
- Orchestrator copy coverage passed 22/22 and the full Orchestrator suite passed 1105/1105. It proves zero prediction-service construction/calls for compatible copy; exactly one target-context call for each ordinary incompatibility; target selection posting; null/unknown/duplicate/wrong-count generated-output rejection; immutable source/target context failure; exact source prediction identity and payload-safe compatibility telemetry; and unchanged ordinary generation behavior.
- Firebase focused round-trip coverage passed 11/11 and the full Firebase suite passed 292/292. The Release solution build completed with zero errors. Static workflow contracts passed for 2 reusable bases, 14 WM26 callers, 12 retired Bundesliga callers, and 2 current Bundesliga callers.
- The current executable arena workflow remains the self-contained `ehonda-ai-arena` to `ehonda-ai-arena` Luna validation path. The supported production-copy topology (`community=ehonda-ai-arena`, `community_context=pes-squad`) is covered at the command contract boundary, including target credentials and target posting with a source-context exact read. Creating and exercising its model-bound production workflow remains a P0-19/P0-21 activation responsibility.
- This evidence is automated contract and persistence proof. It does not claim a production community prediction, model call, copy-post, or live P0-21 validation.

### Final-verifier remediation — 2026-08-27

- Activation review found that generation and posting already followed ADR-0048,
  but the final `verify-bonus` step still read only the requested source
  `community_context`. That source-only read could reject a valid copy whose
  community-local option IDs differ, and it could not verify the target-context
  fallback produced for an ordinary incompatibility.
- Bundesliga reference-copy verification is now copy-aware. A compatible exact
  source candidate is freshness-checked and mapped to the current target option
  IDs before comparison with Kicktipp. An ordinary incompatible or missing
  source instead exact-reads, compatibility-maps, and freshness-checks the
  independently persisted target-context fallback. Missing, incoherent, stale,
  or ambiguous target state and immutable source/target provenance failures
  remain discrepancies.
- The verifier does not create a prediction service or make a model call.
  Self-contained Bundesliga, credential-profile, and legacy competition paths
  retain their existing behavior. Focused verifier coverage passed 67/67,
  including six new compatible-copy, target-fallback, and fail-closed cases;
  the cumulative full Orchestrator suite passed 1142/1142.

## Validation

- [x] Test exact normalized compatibility when raw question/option formatting or community-local IDs/order differ but canonical identities are equal.
- [x] Test changed question text or `MaxSelections`, missing/extra/changed/duplicate source options, missing source candidate, and legacy/partial/malformed source provenance; every case must refuse copy and make exactly one independent target prediction in the same invocation.
- [x] Prove the compatible path performs exactly one copy-post and zero model-service constructions/calls.
- [x] Prove every ordinary incompatible path constructs/calls the approved target prediction service exactly once, uses `ehonda-ai-arena` context, and posts exactly the independently generated target selection.
- [x] Prove mismatch/no-candidate context resolution, prediction persistence, outdated checks, and telemetry all bind `communityContext=ehonda-ai-arena` and never write a new prediction under `pes-squad`.
- [x] Prove invalid/ambiguous target definitions or selections and immutable target-context safety violations fail closed without a post; these are the only incompatibility-related no-post outcomes.
- [x] Add repository round-trip, command, telemetry, workflow-contract, and production-topology coverage for the immutable compatibility fields and copy source.
- [x] Retain the P0-21 handoff to inspect one compatible production copy end to end and retain any real mismatch as evidence of independent generation or fail-closed behavior without recording question/option payloads. This task records the required activation check, not live-production evidence.
- [x] Make final verification follow the same compatible-copy versus exact target-fallback branch as generation, without constructing or calling a prediction service.

## Complete when

- [x] A reviewer can reproduce the compatibility decision from immutable source provenance and the target's complete normalized question/option identity.
- [x] Compatible arena bonus posting reuses the `pes-squad` prediction without an extra model call at the command contract boundary; P0-21 owns the first production execution.
- [x] Every ordinary mismatch or missing-provenance case produces exactly one declared independent target prediction, while target-definition/selection and immutable-context safety violations fail closed.
