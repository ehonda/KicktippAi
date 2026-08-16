# P0-02 — Require competition-scoped persistence

- Status: Complete
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decision: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md)

## Outcome

All 2026/27 context, KPI, match-outcome, and prediction operations carry the resolved competition and use competition-scoped document identities.

## Work items

- [x] Trace competition construction through `FirebaseServiceFactory`, Firebase repositories, and `KicktippContextProvider`.
- [x] Remove the live `null`/2025/26 implicit path, including the `ToRepositoryCompetitionArgument` behavior that converts the old season to an unscoped argument.
- [x] Make production composition pass `bundesliga-2026-27` explicitly into every repository/provider.
- [x] Ensure document ID builders always include the new competition and the required community identity.
- [x] Remove 2025/26 default field initializers from the live write path and treat missing competition identity as invalid for current operations; historical record compatibility is out of scope.
- [x] Add isolation tests proving 2026/27 reads cannot return unscoped or WM26 documents and that writes cannot collide across communities.
- [x] Update Firebase adapter documentation to describe current competition scoping.

## Validation

- Run the competition-isolation, context-repository, KPI-repository, prediction-repository, and match-outcome repository test trees in `tests/FirebaseAdapter.Tests`.
- Inspect generated IDs in tests for the literal `bundesliga-2026-27` prefix and community partition.

## Validation evidence

- 2026-08-16: `dotnet run --project tests/FirebaseAdapter.Tests` passed 216/216 tests, including constructor guards and context, KPI, match-outcome, and prediction competition-isolation coverage against unscoped and WM26 records.
- 2026-08-16: `dotnet run --project tests/ContextProviders.Kicktipp.Tests` passed 46/46 tests, including missing-competition constructor rejection.
- 2026-08-16: the targeted Orchestrator runs passed 53/53 `VerifyBonusCommand*` tests, 24/24 `ListKpiCommandTests` and `UploadKpiCommandTests`, and 19/19 `CompetitionResolverTests`, `FactoryTests`, and `MatchOutcomeCollectionServiceTests`. This covers the 64 P0-01 CI regressions plus the surrounding passing cases.
- 2026-08-16: `dotnet run --project tests/Orchestrator.Tests --no-build` passed the full 824/824 test suite after the targeted runs, and `dotnet build tests/Integration.Tests/Integration.Tests.csproj` completed with zero errors.
- 2026-08-16: source/API audit found no `ToRepositoryCompetitionArgument`, parameterless repository factory call, nullable/default competition in the persistence/provider composition, or `bundesliga-2025-26` live storage default. Context, KPI, and match-outcome test snapshots assert `bundesliga-2026-27` plus community-prefixed deterministic IDs. Prediction IDs intentionally remain GUIDs; their isolation is enforced and tested through required `competition` and `communityContext` fields and query filters.
- No historical Firestore data was migrated or deleted. The remaining old-season matchday-completion comparison is deliberately unchanged and belongs to P0-03; the historical experiment defaults and examples remain owned by P1-06 as classified in P0-01.

## Complete when

- No normal 2026/27 repository is constructed with an absent competition.
- A test fixture containing old unscoped data cannot satisfy a 2026/27 query.
- No historical data migration or deletion is required.
