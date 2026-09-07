# P1-04 / P1-05 execution packet

- Status: Frozen S0 successor contract — 2026-09-07
- Authority: ADR-0074 as refined by [ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md) and [ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md)
- Scope: dormant P1-04/P1-05 only; no source is enabled

## Frozen order

1. Fresh cumulative C1 review of exact `c99e163..852d179` for ADR-0074
   correctness and compatibility with deferred ADR-0077/0078; it is not
   implementation compliance with either successor. Tests alone are not
   acceptance.
2. Content-integrate the accepted exact 20-path bytes onto current `main`.
3. C2 implements ADR-0078's roster matrix and optional source publication
   fence, then receives incremental and fresh final review.
4. C3 applies the nine literal shared HTML amendments; E1 implements official
   HTML only after C3. R1 follows C2 independently of HTML. W1 follows C2 and
   at least one accepted source. Exact-head CI closes each cohesive milestone.

This preserves ADR-0074's seven-milestone upper bound; no two/three-writer or
old-run push assumption applies. One heavy-operation family is serialized.

## Literal C1/C2/R1/W1 ownership

C1's reviewed range is exactly these 20 paths (the former common list excluding
`tests/Orchestrator.Tests/Commands/Operations/Dev/CollectContextDevCommandTests.cs`,
which is not authorized unless a later frozen packet explicitly adds it):

`src/Core/BundesligaContextSourceCycle.cs`;
`src/Core/BundesligaContextSourceBundle.cs`;
`src/Core/BundesligaContextSourceHealth.cs`;
`src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`;
`src/FirebaseAdapter/Models/ContextSourceCycleFirestoreModels.cs`;
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceCycleCoordinator.cs`;
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceBundleHandoff.cs`;
`tests/Core.Tests/BundesligaContextSourceCycleContractTests.cs`;
`tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`;
`tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`;
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`;
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`;
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceBundleHandoffTests.cs`;
`src/Orchestrator/Commands/Operations/Dev/CompetitionCollectionProfile.cs`;
`src/Orchestrator/Commands/Operations/Dev/CompetitionProfileCollectorExecutor.cs`;
`src/Orchestrator/Commands/Operations/Dev/CompetitionProfileCollectionRunner.cs`;
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileSettings.cs`;
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileCommand.cs`;
`tests/Orchestrator.Tests/Commands/Operations/Dev/CompetitionCollectionProfileTests.cs`; and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextProfileCommandTests.cs`.

C2 owns only `src/Core/DocumentPublication.cs`,
`src/FirebaseAdapter/FirebaseDocumentPublicationRepository.cs`,
`src/Core/BundesligaContextSourceBundle.cs`, `src/Core/BundesligaContextSourceHealth.cs`,
`src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`,
`tests/Core.Tests/DocumentPublicationContractTests.cs`,
`tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`,
`tests/FirebaseAdapter.Tests/FirebaseDocumentPublicationRepositoryTests.cs`,
`tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`, and
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceBundleHandoffTests.cs` for
the matrix/fence and its round-trip/null/legacy/crash-replay tests. R1 owns only
`src/Core/BundesligaRosterRefresh.cs`, `src/Core/BundesligaRosterModels.cs`,
`src/Core/BundesligaRosterPolicy.cs`, `src/Core/BundesligaRosterPublication.cs`,
`src/Core/BundesligaRosterPublicationContract.cs`, `src/Core/BundesligaRosterCsv.cs`,
`src/Core/BundesligaRosterSeed.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaRosterArtifactAcquirer.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaRosterSource.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextRostersCommand.cs`,
`data/bundesliga-2026-27/rosters/roster-refresh-policy-v1.json`,
`docs/sources/bundesliga-2026-27-rosters.md`,
`tests/Core.Tests/BundesligaRosterRefreshTests.cs`,
`tests/Core.Tests/BundesligaRosterPolicyTests.cs`,
`tests/Core.Tests/BundesligaRosterPublicationTests.cs`,
`tests/Core.Tests/BundesligaRosterPublicationContractTests.cs`,
`tests/Core.Tests/BundesligaRosterCsvTests.cs`, `tests/Core.Tests/BundesligaRosterSeedTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterArtifactAcquirerTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterSourceTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterDuckDbFixture.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextRostersCommandTests.cs`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextRostersCommandFirestoreTests.cs`.
`A1-source-attribution` alone owns repository-root `README.md` after both R1
and E1 acceptance and before closeout; R1/E1 do not concurrently own it. S0
does not edit it.

W1 owns only `src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceArtifactStore.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/GitHubContextSourceIssueProjector.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceArtifactStoreTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/GitHubContextSourceIssueProjectorTests.cs`,
`src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs`,
`src/FirebaseAdapter/ServiceCollectionExtensions.cs`, `.github/workflows/base-context-collection.yml`,
`.github/workflows/buli2627-production-live-matchday.yml`,
`.github/scripts/Test-PredictionWorkflowContracts.ps1`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextCollectionWorkflowContractTests.cs`.

## Literal C3/E1 ownership

C3 owns the exact nine paths listed in ADR-0077. E1 owns only
`src/Core/BundesligaClubEloRefresh.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaClubEloRefreshSource.cs`,
`src/Orchestrator/Orchestrator.csproj`, `data/bundesliga-2026-27/club-elo-name-map.csv`,
`docs/sources/bundesliga-2026-27-club-elo.md`,
`tests/Core.Tests/BundesligaClubEloRefreshTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaClubEloRefreshSourceTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/eligible.html`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/unknown-source-date.html`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/partial.html`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/hostile-dom.html`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/hostile-lexer.html`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/hostile-fragment.html`.
No inferred dependency/fixture is authorized. R1 cannot edit shared
descriptor/receipt/health/fence surfaces.
C3 starts only after C2 acceptance and serially reuses its shared paths.

## Verification and authority

C2 covers round-trip/null/fence/supersession/crash-retry/legacy compatibility;
C3/E1 cover HTML fixture/reconstruction/integer contracts; R1 covers rejection
and synthetic takeover; disabled sources prove zero resolution/writes/API calls.
All flags remain false. Separate owner gates cover live acquisition,
development persistence, unattended HTML reuse, GitHub mutations, production
writes/activation, rollback, restoration and completion evidence.
