# ADR-0040: Use hash-bound 2025/26 context for preseason cost experiments

- Status: Accepted
- Date: 2026-08-25

## Context

P0-06 needs paid token-usage evidence before Bundesliga 2026/27 starts. No 2026/27 fixture has a completed score yet, so the prescribed five-fixture by four-repetition base sample must use completed Bundesliga 2025/26 fixtures strictly after the model cutoff margin. Those historical Firestore context rows use the legacy document ID `{documentName}_{communityContext}_{version}`, while the current ordinary repository correctly requires a competition-prefixed ID. Relaxing that live validator or rewriting historical rows would weaken current-season isolation and change stored evidence.

The historical match route contains seven ordered documents: standings, community rules, both teams' recent history, home/away history, and head-to-head. The live 2026/27 route adds two roster and two Club Elo documents, for eleven total. A historical row can measure the exact selected model and hosted match-prompt output path, but its smaller input is only a preseason cost proxy and may understate live input-token cost.

## Decision

Historical cost preparation is an explicit experiment-only mode for canonical competition `bundesliga-2025-26`. It uses compatibility marker `bundesliga-2025-26-legacy-id-hash-v1` and a separate historical manifest; it does not generalize `ResolvedMatchContextManifest`, modify the live 2026/27 context contract, or expose a Firestore write operation.

The read-only adapter accepts only the legacy ID formula and validates the stored competition, community, document name, version, empty publication set, and `createdAt` boundary. Preparation selects completed fixtures strictly after the supplied sampling cutoff, resolves the canonical seven documents at exactly `startsAt -12h`, and records each source document ID, version, creation timestamp, and content SHA-256.

The prepared manifest binds:

- the official model knowledge-cutoff date and exact sampling cutoff;
- hosted prompt `kicktippai/bundesliga-2026-27/predict-one-match`, label `production`, exact version `2`;
- relative evaluation policy `startsAt -12:00:00`;
- fixture, start time, TippSpiel ID, repetition identity, and completed score; and
- each context-manifest hash plus an aggregate length-prefixed SHA-256 over the full historical artifact contract.

The aggregate contract also binds the task/slice/dataset provenance, seed, declared dimensions, selected fixture identities, and selected-ID hash. Validation reconstructs the canonical source and repeated-match-slice IDs and requires an exact `matchCount × repetitions` matrix; missing TippSpiel, fixture, or repetition identities fail closed. The sampling cutoff is the exact Europe/Berlin local midnight two calendar days after the official cutoff, not merely any later instant.

Before a marked run can delete or create a Langfuse dataset run, fetch the hosted prompt, construct a model client, or call a model, the runner exact-reads and re-hashes every distinct fixture manifest. It then read-resolves the hosted prompt and verifies the returned name, numbered version, and `production` label before any run deletion or model construction. Missing or drifted context or prompt provenance fails closed. Repetitions reuse the validated fixture cache. Marked historical items never use the ordinary context repository, prediction repository, stored-match lookup, or live outcome repository; their real score is the hash-bound prepared value. Existing unmarked historical artifacts and the live 2026/27 route keep their prior behavior.

Dataset synchronization retains the existing public `input`, `expectedOutput`, and `metadata` shapes. Historical context provenance remains in the local run manifest and propagated run/trace metadata.

For the Luna P0-06 row, the official cutoff is `2026-02-16` and the sampling cutoff is exactly `2026-02-18T00:00:00 Europe/Berlin (+01)`; selected fixtures must start strictly later. The result must be labeled `Langfuse Bundesliga match v2; Bundesliga 2025/26 7-document legacy-id-hash-v1 context`, described as a preseason cost proxy that may understate the live eleven-document input, and must make no prediction-quality claim.

## Alternatives considered

- **Relax `FirebaseContextRepository` identity validation:** Rejected because it would weaken the live ordinary-document trust boundary globally.
- **Migrate or rewrite historical Firestore rows:** Rejected because P0-06 needs no storage mutation and historical evidence should retain its original identity.
- **Embed context bytes as base64 in synced datasets:** Rejected because exact-version reread plus content hashes supplies immutability without enlarging or changing the hosted dataset contract.
- **Use pending 2026/27 fixtures without scores:** Rejected because the owner selected completed 2025/26 outcomes as the preseason cost basis, and P0-06 is not a prediction-quality study.

## Consequences

- P0-06 can collect valid model usage from completed prior-season fixtures without reopening legacy runtime behavior.
- Historical rows remain rerunnable only while their exact Firestore versions remain available and unchanged; deletion or drift fails rather than silently changing input.
- The row measures exact Luna/none/match-v2 output behavior but a seven-document historical input. P1 live calibration remains necessary for the eleven-document production route.
- This ADR authorizes no production model selection, workflow activation, schedule change, quality conclusion, or live Firestore write.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P1-07](../tasks/p1-07-cost-calibration.md)

## Supersedes

None.
