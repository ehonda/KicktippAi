# P1-04 / P1-05 execution packet

- Status: Frozen C3/E1 successor contract and P1-05 dormant closeout — 2026-09-09; C3 correction-limited at local unintegrated `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, E1 blocked, P1-05 deferred
- Authority: ADR-0074 as refined by [ADR-0077](decisions/0077-refresh-club-elo-from-official-html.md), [ADR-0078](decisions/0078-refine-context-source-pre-artifact-and-publication-fence.md), [ADR-0079](decisions/0079-pin-roster-refresh-endpoints-and-close-c2-validation.md), [ADR-0080](decisions/0080-bound-transitional-context-publication-recovery.md), [ADR-0081](decisions/0081-close-club-elo-html-publication-and-selection-seams.md), and [ADR-0082](decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md)
- Scope: dormant P1-04/P1-05 only; no source is enabled

## Frozen order

1. C1 is satisfied/integrated on current `main` at exact
   `f21f89d8f5d3b36c73d1dd0aa96dc1bddb8b1a07`. Its historical cumulative review
   of `c99e163..852d179` and 20-path integration are not future work.
2. S1 acceptance closes the metadata-authority contract.
3. S3 accepts ADR-0080's narrow replacement for transitional C1 inference.
   It releases no source and authorizes no implementation by itself.
4. C2 is reusable common evidence only.
5. ADR-0081 is published and exact-head green at
   `f37c9e549e952004c5443f25aab363fda6e2811c`, workflow `34319614680`, all 12
   jobs. C3 is correction-limited at local unintegrated
   `d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
   diagnosis/reslicing and subsequent correction/review. E1's 17 exact paths
   remain blocked until corrected C3 is accepted, published, and exact-head
   green. R1 remains deferred under ADR-0082 pending the exact ADR-0079
   dcaribou sidecar and separate owner gates. Only accepted E1 may release W1.
   W1/A1 are outside this objective and not released; A1 later owns E1
   attribution only, while roster attribution waits for a future accepted R1.
   Exact-head CI closes future implementation milestones.

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
`A1-source-attribution` is outside this objective and is not released. When
later released, it alone owns repository-root `README.md` for E1 attribution
only after accepted E1; roster attribution waits for a future accepted R1.
R1/E1 do not concurrently own repository-root `README.md`. S0 does not edit it.

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
health/fence surfaces. C3 is correction-limited at local unintegrated
`d9a328e551c0473b3eeb6e704b70cdfa47a8d5d8`, under fresh bounded
diagnosis/reslicing and subsequent correction/review after published/green
ADR-0081; E1 remains blocked until corrected C3 is accepted, published, and
exact-head green. C3 serially reuses C2 paths.

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
R1's deferred future hostile/null/replay/supersession checklist includes
ADR-0079's strict sidecar contract. This closeout makes no sidecar/artifact
probe and no provider, observation, receipt, artifact, health, publication, or
API action. Dcaribou remains the sole metadata authority; seed/LKG retains
truthful original dates/provenance and automatic freshness remains unavailable.
Existing v1/v2 remain unchanged; no v3 relabel, migration, or backfill occurs.
C3/E1 cover ADR-0081's HTML fixture/reconstruction/integer, receipt-completion
and family-specific numeric contracts; R1 covers rejection and synthetic
takeover; disabled sources prove zero resolution/writes/API calls.
All flags remain false. Separate owner gates cover live acquisition,
development persistence, unattended HTML reuse, GitHub mutations, production
writes/activation, rollback, restoration and completion evidence.
The current eight-pair topology, schedule, serial/default-success and
non-cancelling behavior, manual-only leaves, no bonus, models, prompts,
credentials, posting, and copy remain unchanged. The rejected R1
`b4c9041b323cd55534194fa894b2f3975ac6526a` and rejected closeout `80b7c6c`
remain unintegrated evidence only and confer no implementation credit.

## Closeout literal scope

```text
.agents/skills/bundesliga-2026-27-onboarding/references/competition-profile.md
plans/bundesliga-2026-27/README.md
plans/bundesliga-2026-27/decisions/0082-close-dormant-roster-refresh-scope-with-r1-deferred.md
plans/bundesliga-2026-27/decisions/README.md
plans/bundesliga-2026-27/designs/p1-04-05-context-refresh.md
plans/bundesliga-2026-27/execution-strategy.md
plans/bundesliga-2026-27/handoffs/p1-04-club-elo-html-in-flight-2026-09-06.md
plans/bundesliga-2026-27/handoffs/p1-05-roster-refresh-in-flight-2026-09-06.md
plans/bundesliga-2026-27/p1-04-05-execution-packet.md
plans/bundesliga-2026-27/p1-status-snapshot.md
plans/bundesliga-2026-27/tasks/p1-04-club-elo-refresh.md
plans/bundesliga-2026-27/tasks/p1-05-roster-refresh.md
```
