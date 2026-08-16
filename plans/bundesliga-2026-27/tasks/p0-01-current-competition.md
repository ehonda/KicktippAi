# P0-01 — Make 2026/27 the current Bundesliga competition

- Status: Complete
- Priority: P0
- Depends on: None
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

Every normal non-WM26 runtime path resolves Bundesliga to `bundesliga-2026-27`, and current-season observability metadata agrees.

## Work items

- [x] Add `CompetitionIds.Bundesliga2026_27` and make it the live Bundesliga identifier.
- [x] Advance `CompetitionResolver`'s non-WM26 default and `KicktippSeasonMetadata.Current` to the new ID.
- [x] Update CLI option descriptions and user-facing output that describes the default competition.
- [x] Remove active fallback logic whose only purpose is to keep 2025/26 current; do not add a parallel legacy runtime route.
- [x] Classify remaining `bundesliga-2025-26` literals as historical fixtures/artifacts or stale live defaults. Move stale live defaults into the relevant follow-up task rather than mechanically rewriting historical evidence.
- [x] Update resolver and season-metadata tests for explicit WM26, explicit 2026/27, and omitted-competition behavior.

## Validation

- Run the `CompetitionResolverTests` tree in `tests/Orchestrator.Tests`.
- Search live source and workflow code for current-season references to `bundesliga-2025-26` and account for every remaining result.

## Validation evidence

- 2026-08-16: `dotnet run --project tests/Orchestrator.Tests -- --treenode-filter $filter` with `$filter = '/*/*/CompetitionResolverTests/*'` passed 12/12 tests. Restore reported the existing `SSH.NET` `NU1903` advisory warning.
- 2026-08-16: the same targeted runner with `$filter = '/*/*/(VerifyMatchdayCommand_Settings_Tests)|(CollectContextKicktippCommand_NormalMode_Tests)/*'` passed 20/20 directly affected command tests.
- 2026-08-16: `rg -n 'bundesliga-2025-26' src .github` and the equivalent `CompetitionIds.Bundesliga2025_26` symbol audit found no workflow hits. Remaining source references are classified as follows:
  - P0-02 owns the Firebase model/repository null defaults, `FirebaseServiceFactory` and `KicktippContextProvider` fallbacks, `CompetitionResolver.ToRepositoryCompetitionArgument`, and the Firebase adapter documentation.
  - P0-03 owns the two `FirebaseMatchOutcomeRepository` old-season comparisons that currently determine Bundesliga matchday completion.
  - P1-06 owns `PrepareCommunityToDateCommand`, `ExperimentArtifactSupport`, and `SyncDatasetCommand` defaults. Its inventory also owns deciding which current `Program.cs` experiment examples advance; the existing examples point at specific historical 2025/26 artifacts and are not runtime defaults.
  - `CompetitionIds.Bundesliga2025_26` remains as an explicit historical identifier for existing fixtures/artifacts and for the already-scoped P0-02/P0-03 seams; no resolver or current-season metadata default selects it.

## Complete when

- An omitted competition resolves to `bundesliga-2026-27` for Bundesliga communities.
- Explicit WM26 resolution is unchanged.
- No production default reports or selects 2025/26.
