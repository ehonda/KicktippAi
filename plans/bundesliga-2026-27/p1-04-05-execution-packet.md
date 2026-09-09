# P1-04 / P1-05 execution packet

- Status: Frozen C3/E1 successor contract — 2026-09-09; implementation pending
- Authority: ADR-0074 as refined by [ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md), and [ADR-0081](decisions/0081-close-club-elo-html-publication-and-selection-seams.md)
- Scope: dormant P1-04/P1-05 only; no source is enabled

## Frozen order

1. C1 is satisfied/integrated on current `main` at exact
   `f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`. Its historical cumulative review
   of `c99e163..852d179` and 20-path integration are not future work.
2. S1 acceptance closes the metadata-authority contract.
3. S3 accepts ADR-0080's narrow replacement for transitional C1 inference.
   It releases no source and authorizes no implementation by itself.
4. C2 is accepted at `c7cc0712b16834d4013948949f1502514ae46770`.
5. The tracked ADR-0081 specification receives fresh acceptance/publication;
   a fresh C3 writer then applies its 13 exact paths, followed by E1's 17 exact
   paths. R1 follows C2 independently; W1 follows C2 and one accepted source
   but cannot partially wire Club Elo. Exact-head CI closes each milestone.

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
the matrix/fence and its round-trip/null/legacy/crash-replay tests. Sequentially
after C1 integration, C2 additionally reuses the thirteenth path,
`tests/Orchestrator.Tests/Commands/Operations/Dev/CompetitionCollectionProfileTests.cs`,
for exactly two mechanical fixture substitutions to
`BundesligaContextSourceDescriptorContract.RosterMetadataUrl` and
`BundesligaContextSourceDescriptorContract.RosterArtifactUrl`; C1's historical
ownership of that path remains satisfied, and this test-fixture-only reuse
authorizes no other change in the file or any additional path. R1 owns only
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

C3 owns exactly these 13 paths:
`src/Core/BundesligaContextSourceBundle.cs`,
`src/Core/BundesligaContextSourceHealth.cs`,
`src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceCycleCoordinator.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceBundleHandoff.cs`,
`tests/Core.Tests/BundesligaContextSourceBundleContractTests.cs`,
`tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`,
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`,
`src/Core/BundesligaClubEloPublication.cs`,
`tests/Core.Tests/BundesligaClubEloPublicationTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceBundleHandoffTests.cs`, and
`tests/FirebaseAdapter.Tests/FirebaseDocumentPublicationRepositoryTests.cs`.
The last is tests only; no Firebase publication-repository implementation is
authorized. E1 owns exactly these 17 paths:
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
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/hostile-fragment.html`,
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextClubEloCommand.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextClubEloCommandTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextClubEloCommandFirestoreTests.cs`, and
`tests/Orchestrator.Tests/Orchestrator.Tests.csproj`.
The test-project edit makes only the six named fixtures deterministically
available. E1 adds a direct AngleSharp reference to `Orchestrator.csproj`
using the existing central 1.7.2 pin; no central-package edit or inferred
dependency/fixture is authorized. R1 cannot edit shared descriptor/receipt/
health/fence surfaces. C3 starts only after accepted ADR-0081 specification
publication and serially reuses C2 paths.

## Verification and authority

C2 covers receipt-first round-trip/null/fence/supersession/crash-retry/legacy compatibility,
state-aware reason precedence, impossible-reason exclusion for unavailable
identity, raw SHA/length drift evidence, canonical lane IDs, prior
receipt/health validation before metadata-unchanged writes, and ADR-0080's
direct guarded Firebase matrix: exact replay, fatal expected/current mismatch
including current-equals-target, mutation-free provable `Unchanged`, ordinary
atomic `Published`/`Reactivated`, and distinct prior/current staleness. The
five local corrections are exact receipt replay, guard-before-head ordering,
mismatch fatality, bounded recovery, and authoritative prior-cycle selection.
Before retriable Firebase work, C2 defensively snapshots/freezes caller-supplied
document collection/ordered entries, guard evidence, receipt template, and
conditions; every retry uses that frozen canonical input. Invalid supplied order
fails and C2 never silently sorts it. One shared retained-diagnostic
evaluation-precedence validator covers observation diagnostics, retained
descriptors, and `MetadataUnchanged.retainedDiagnostics`; no per-call ad hoc or
lexical order is permitted. Direct Firebase proof includes valid non-lexical ADR
order plus reversed, duplicate, invalid-primary, and unknown-code hostiles.
R1's frozen hostile/null/replay/supersession
checklist includes ADR-0079's strict sidecar contract.
C3/E1 cover ADR-0081's HTML fixture/reconstruction/integer, receipt-completion
and family-specific numeric contracts; R1 covers rejection and synthetic
takeover; disabled sources prove zero resolution/writes/API calls.
All flags remain false. Separate owner gates cover live acquisition,
development persistence, unattended HTML reuse, GitHub mutations, production
writes/activation, rollback, restoration and completion evidence.
