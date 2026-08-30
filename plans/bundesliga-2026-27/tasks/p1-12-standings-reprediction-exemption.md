# P1-12 — Exempt standings-only changes from repredictions

- Status: Complete — automated and isolated `ehonda-dev-buli-2627` evidence confirms the standings-only exemption and fail-closed non-exempt behavior; production configuration was not changed
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
- [x] The implementation lane committed `6e1be19a40a7fc2130b81472d107de747a9f0c98`,
      integrated as `4a5709e173a165613025887dcf75ba9d6f5d149b` on the combined
      exact head `050b946055a8fb690af94b15344adfe4dd4c950a`. This task-record
      closeout is separately committed and handed to the root for independent
      review and explicit push.

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

- 2026-08-30 — implementation commit
  `6e1be19a40a7fc2130b81472d107de747a9f0c98` integrated as
  `4a5709e173a165613025887dcf75ba9d6f5d149b`; the combined exact validation
  head was `050b946055a8fb690af94b15344adfe4dd4c950a`.
- 2026-08-30 — `dotnet run --project tests/Orchestrator.Tests -- --treenode-filter "/*/*/BundesligaPredictionOutdatedCheckerTests/*"` passed
  16/16. The canonical standings entry is exact-read; missing, hash- or
  version-tampered, malformed, scope-corrupt, missing-latest, and rollback
  states fail closed. Retained history, rules, roster, and Club Elo staleness
  checks passed.
- 2026-08-30 — `dotnet run --project tests/Orchestrator.Tests -- --treenode-filter "/*/*/MatchdayCommand_AdditionalCoverage_Tests/*"` passed
  33/33. An invalid exact-read standings document stops before model call,
  resolved-context reprediction save, or Kicktipp submission at the configured
  index limit. Affected `MatchdayCommand_*` coverage passed 163/163, including
  Friday-to-weekend, Saturday-to-Sunday, and three `pes-squad` copy-posting
  no-op cases; `VerifyMatchdayCommand_Outdated_Tests` passed 21/21.
- 2026-08-30 — exact combined-head suite: `dotnet run --project
  tests/Orchestrator.Tests` passed 1190/1190, exit 0 (pre-existing compile
  warnings only).
- 2026-08-30 — authorized isolated development validation used the exact
  guarded configuration: `gpt-5.6-luna`, reasoning `none`, output cap `10000`,
  and hosted `kicktippai/bundesliga-2026-27/predict-one-match` immutable version
  `3` with required `production` membership. The index-0 baseline root trace
  `5f27a9a514e3c7ea5c197aa9161fd0be` (root observation
  `48d6a2c2bdfbc88f`) recorded two generation observations
  `62ae99a243f6db40` and `80f2b6f2928f36e4`, with total cost `$0.0008115`.
  The exact two v3 Firestore rows were both index 0.
- 2026-08-30 — only the canonical standings document was advanced from v0
  SHA `acf247c7fc37eda0fa9bbf616337c886340a3ce3b88a71df448212d93ab2d9f8` to
  temporary valid v1 SHA
  `2b9dfe03c67ead948f2655178fd72978031c151f781222e3b4d75f966a17fce5`, then
  restored as v2 with the original v0 SHA. The repredict-mode no-op root trace
  `0db33a6114bf53aa99025abc25be0088` (root observation
  `1b09c22f366c42a8`) reports `repredictMode=true`,
  `hasRepredictions=false`, and `repredictionIndices=|0|`; it has zero
  generation observations, usage, and cost. No OpenAI call, reprediction save,
  index allocation, or Kicktipp submission occurred. No prediction values are
  recorded here. It ran from `2026-08-30T07:16:41.2727721Z` to
  `2026-08-30T07:16:49.820557Z`, exit `0`, with:

  ```text
  dotnet run --no-build --project src/Orchestrator -- matchday gpt-5.6-luna --community ehonda-dev-buli-2627 --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --reasoning-effort none --max-output-tokens 10000 --prompt-source langfuse --langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match --langfuse-prompt-label production --langfuse-prompt-version 3 --max-repredictions 0 --agent
  ```
- 2026-08-30 — the same Firestore rows
  `809ccd1d-a762-4082-9ff6-573dd04ca0ad` and
  `d2ae61b4-9e6f-4f5f-84d3-2d3134fd3fda` remained index 0. The non-standings
  fingerprint, roster publication, and Club Elo publication were unchanged;
  final verification was 2/2. Temporary helper/raw artifacts were removed.
  Residual authorized dev state is exactly the two retained index-0 v3 rows and
  the restored canonical standings v2 document containing the original v0
  content hash; no production configuration or data was changed.

## Complete when

- [x] A standings-only refresh cannot create a match reprediction.
- [x] Every non-exempt context/provenance change retains its current staleness
      behavior.
- [x] Friday and Saturday result-driven standings refreshes reuse predictions
      for the remaining weekend fixtures unless another eligible context input
      changed.
- [x] Focused and complete affected tests pass, and representative development
      trace and persistence evidence confirm no new index or model call.
