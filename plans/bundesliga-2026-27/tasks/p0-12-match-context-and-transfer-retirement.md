# P0-12 — Replace transfer context in the match contract

- Status: In progress
- Priority: P0
- Depends on: [P0-09](p0-09-roster-collector.md), [P0-11](p0-11-club-elo-collector.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0015](../decisions/0015-club-elo-prompt-publication-contract.md), [ADR-0016](../decisions/0016-validate-club-elo-publication-metadata.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md), [ADR-0020](../decisions/0020-record-immutable-match-context-manifests.md)

## Outcome

Every 2026/27 match requires the two roster and two Club Elo documents and no live path selects or uploads transfer documents.

## Work items

- [x] Extend `MatchContextDocumentCatalog` with manifest-backed canonical `roster-{slug}` and `club-elo-{slug}.csv` names for both teams.
- [x] Keep standings, community rules, recent history, home/away history, and head-to-head history required for Bundesliga.
- [x] Remove `IncludeTransfers`, transfer optional-document selection, and transfer-specific retrieval branches from matchday, random-match, prompt reconstruction, experiment export/execution, and match-analysis helpers.
- [x] Remove the `upload-transfers` command registration, implementation, tests, and current documentation because it no longer serves a live context contract.
- [x] Simplify `MatchContextDocumentSelection` if optional documents no longer have another supported use.
- [x] Replace transfer-focused tests with assertions that both Elo and roster documents are mandatory and missing required context fails clearly.
- [ ] Complete independent review before changing task status.
- [ ] Leave historical Firestore documents untouched; do not add a deletion migration.

## Validation

- Run `MatchContextDocumentCatalogTests`, matchday/random-match context retrieval tests, prompt reconstruction tests, and affected experiment/analyze-match tests.
- Search live `src`, `tests`, `.github`, and non-archive docs for transfer-document names and account for every remaining result.

## Implementation evidence

- 2026-08-18: Added ADR-0020 and the shared snapshot-backed resolver. `dotnet build src/Orchestrator/Orchestrator.csproj --no-restore` passed (two existing nullable/obsolete warnings). Full focused and emulator validation remains required before review.
- 2026-08-18: Removed the live upload command, DI registration, prompt file, command tests, and required-only catalog migration. Remaining transfer mentions are being reduced to historical research/ADR wording and rewritten non-live test fixtures; no Firestore deletion is performed.
- 2026-08-18: Validated the immutable resolver and Firestore round trip, including coherent headed snapshots, no generic reserved reads, on-demand ordinary-document materialization with exact reread, and stored-version reconstruction after head advance. Full serial suites passed: Core 120, FirebaseAdapter 241 (emulator), OpenAiIntegration 212, ContextProviders.Kicktipp 46, and Orchestrator 861. `git diff --check` passed. The task remains In progress pending independent review.
- 2026-08-19: Independent-review remediation is in progress: semantic publication reconstruction is mandatory on every resolver boundary; manifests and Firestore reads validate immutable canonical scope/order/identity; provenance persistence blocks Bundesliga submissions when it fails; and Matchday/Verify use exact ordinary versions plus publication heads for outdated checks. Focused resolver tests (6) and Firestore manifest round trip (1) pass. Broader command and prepared-experiment coverage remains pending re-review.
- 2026-08-19: Prepared 2026/27 experiment items support `resolvedContextManifest` plus `predictionCreatedAt`; validation rejects either omission and execution reconstructs only from the recorded snapshots. Legacy 2025/26 prepared artifacts retain timestamp reconstruction. The current outcomes-only `prepare-*` commands intentionally cannot produce 2026/27 artifacts until [P1-06](p1-06-observability-datasets.md) supplies explicit prediction provenance; they fail closed rather than emit unsafe timestamp-only artifacts. Matchday semantic fixtures now assert provenance saves and fail-closed persistence; independent-review/full-suite evidence remains pending.
- 2026-08-19: Review remediation keeps the legacy timestamp-based Verify fixtures explicitly on `bundesliga-2025-26`; 2026/27 Verify now proves that a missing immutable manifest is outdated and that a complete matching manifest/head is current without generic reserved-document reads. The shared checker also rejects reserved-document version advances even when an identical-content snapshot ID is reused. Focused Verify passed 17/17; Core 122/122, FirebaseAdapter 241/241, OpenAiIntegration 212/212, ContextProviders.Kicktipp 46/46, and a prior full Orchestrator run passed 863/863. Final re-review remains required, so the task stays In progress.

## Complete when

- A match trace has standings, rules, histories, two Elo rows, and two roster documents.
- No live command asks for `*-transfers.csv` and no upload-transfers command is exposed.
- Future historical experiments remain possible only by explicitly assembling their own context.
