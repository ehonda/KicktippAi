# P1-12 — Exempt standings-only changes from repredictions

- Status: In progress — implementation and automated validation complete; representative dev persistence/trace evidence awaits safe confirmation of the base credential identity
- Priority: P1 — High
- Depends on: [P0-12](p0-12-match-context-and-transfer-retirement.md), [P0-21](p0-21-production-activation.md)
- Decisions: [ADR-0057](../decisions/0057-exempt-standings-from-reprediction-staleness.md)

## Incident

The first production-live Bundesliga 2026/27 cycle after Bayern München's
Friday 5:1 result over VfB Stuttgart classified every prediction for the eight
still-open matchday-1 fixtures as outdated. All eight production lanes reported
`hasRepredictions=true` and `repredictionIndices=|1|` on 2026-08-29. The five
generating configurations emitted 40 `predict-match` observations with
`repredictionIndex=1`; the three copy-posting lanes propagated the same index.

Comparison with the original index-0 prompts found exactly one changed context
document in every generating configuration: `bundesliga-standings.csv`. The
roster and Club Elo snapshots and the other ordinary match-context documents
were unchanged. This creates the predictable waste pattern where a Friday
result repredicts all Saturday and Sunday fixtures, then Saturday results can
repredict the remaining Sunday fixtures even though a small early-season table
update is not independently material prediction information.

## Outcome

A new version or content hash of `bundesliga-standings.csv` alone does not make
an existing Bundesliga 2026/27 match prediction outdated. Standings remain in
the resolved context manifest and in prompts; when another eligible context
change causes a prediction, that prediction uses the current standings.

Every other existing provenance and staleness rule remains fail-closed. Changes
to match histories, community rules, roster publications, or Club Elo
publications still trigger reprediction, and missing, malformed, or mismatched
manifest data remains invalid rather than becoming reusable through the
exemption.

## Work items

- [x] Exclude the exact canonical `bundesliga-standings.csv` entry from the
      ordinary-document version/content comparison in the Bundesliga match
      prediction outdated checker.
- [x] Keep standings in manifest construction, validation, prompt
      reconstruction, experiment export, and newly generated prompts.
- [x] Preserve exact version and content checks for every other ordinary
      document and preserve roster/Club Elo publication-head checks.
- [x] Add focused regressions proving that a standings-only update keeps an
      existing prediction current while history, rules, roster, and Club Elo
      changes still classify it as outdated.
- [x] Add command-level coverage for the Friday-to-weekend pattern: after one
      fixture completes and standings refresh, the remaining open fixtures are
      reused without model calls or new reprediction indices.
- [x] Cover the analogous Saturday-to-Sunday transition and copy-posting lanes
      so reuse does not allocate or propagate an unnecessary new index.
- [x] Verify trace metadata for a representative no-op cycle reports
      `hasRepredictions=false` and retains the existing index.
- [ ] Verify the exact Git target, commit the scoped changes intentionally, and
      push the explicit remote and branch.

## Validation

- Run the focused outdated-checker and Matchday command tests with the
  repository-prescribed TUnit `dotnet run` commands.
- Run the complete Orchestrator test project.
- In `ehonda-dev-buli-2627`, create an index-0 Luna/none prediction with a
  pinned output cap, publish only a newer standings document, and verify a
  repredict-mode Matchday run reuses the prediction without an OpenAI call or
  index allocation.
- Inspect the resulting Langfuse root observation and Firestore prediction set
  without promoting the validation configuration to production.

## Validation evidence

- 2026-08-30 — focused `BundesligaPredictionOutdatedCheckerTests` passed:
  16/16. It proves that the recorded standings row is exact-read and validates
  missing, hash-tampered, version-tampered, malformed, scope-corrupt, missing
  latest, and latest-version rollback states fail closed before a valid newer
  current standings version is exempted; retained history, rules, roster, and
  Club Elo staleness checks also pass.
- 2026-08-30 — focused affected `MatchdayCommand_AdditionalCoverage_Tests`
  passed: 33/33. An exact-read standings `InvalidDataException` is classified
  unsafe before a model call, resolved-context reprediction save, or Kicktipp
  submission when the cached prediction is at the configured index limit.
- 2026-08-30 — focused affected `MatchdayCommand_*` classes passed: 163/163,
  including the
  Friday-to-weekend and Saturday-to-Sunday no-op cases, three `pes-squad`
  context copy-posting targets, no model calls/no reprediction save, and root
  trace metadata `repredictionIndices=|0|` / `hasRepredictions=false`.
- 2026-08-30 — focused `VerifyMatchdayCommand_Outdated_Tests` passed: 21/21.
  The matcher verifies the exact canonical standings entry can advance in both
  version and content while current verification succeeds.
- 2026-08-30 — complete affected TUnit project passed:
  `dotnet run --project tests/Orchestrator.Tests`; 1190/1190 passed in
  2m16.660s. Pre-existing compile warnings were emitted but no test failed.
- 2026-08-30 — live development validation was not run. The sibling secrets
  base `.env` and `firebase.json` are present with relevant nonblank
  assignments, while `.env.ehonda-dev-buli-2627` is absent. The base Kicktipp
  identity was not safely established, so do not substitute credentials or
  create an unbounded fixture. Remaining evidence is one isolated
  `ehonda-dev-buli-2627` index-0 `gpt-5.6-luna` / `none` prediction with a
  pinned output cap, publication of only a newer standings document, a
  repredict-mode no-op with no OpenAI call/index allocation, followed by
  Firestore prediction-set and Langfuse root-observation inspection.

## Complete when

- A standings-only refresh cannot create a match reprediction.
- Every non-exempt context/provenance change retains its current staleness
  behavior.
- Friday and Saturday result-driven standings refreshes reuse predictions for
  the remaining weekend fixtures unless another eligible context input changed.
- Focused and complete affected tests pass, and representative development
  trace and persistence evidence confirm no new index or model call.
