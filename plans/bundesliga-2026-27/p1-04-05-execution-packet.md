# P1-04 / P1-05 execution packet

- Status: Frozen writer-ready packet — 2026-09-06
- Authority: [ADR-0074](decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md)
- Scope: P1-04/P1-05 common seam and dormant implementation only; neither task is complete.

## Graph and milestones

```text
P0-21 complete -> common cycle/bundle/health/handoff seam
                 -> P1-05 roster implementation and synthetic takeover proof
                 -> P1-04 source-contract choice -> Club Elo implementation
```

P1-05 depends on P0-21 and the common seam, not P1-04 evidence or implementation.
The current real DuckDB candidate rejects safely; synthetic future 2026/27
fixtures prove takeover. P1-04 remains deferred until an accepted direct-source
date/name contract exists. Predicted milestones are common/ADR, dormant roster,
roster activation PR, roster closeout, Club Elo source/dormant work, Club Elo
activation PR, and final closeout; activation milestones use draft branches.

## Literal ownership

The packet writer uses `.tmp/worktrees/p1-04-05-common` on
`codex/01a07449-de77-7ae0-ac4a-8f5330c43121-common-foundation`. Common runtime
paths are `src/Core/BundesligaContextSourceCycle.cs`,
`src/Core/BundesligaContextSourceBundle.cs`,
`src/Core/BundesligaContextSourceHealth.cs`,
`src/FirebaseAdapter/FirebaseContextSourceCycleRepository.cs`,
`src/FirebaseAdapter/Models/ContextSourceCycleFirestoreModels.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceCycleCoordinator.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/ContextSourceBundleHandoff.cs`,
`src/Orchestrator/Commands/Operations/Dev/CompetitionCollectionProfile.cs`,
`CompetitionProfileCollectorExecutor.cs`, `CompetitionProfileCollectionRunner.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/CollectContextProfileSettings.cs`,
`CollectContextProfileCommand.cs`, and the exact common test paths
`tests/Core.Tests/BundesligaContextSourceCycleContractTests.cs`,
`BundesligaContextSourceBundleContractTests.cs`,
`BundesligaContextSourceHealthContractTests.cs`,
`tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextSourceCycleCoordinatorTests.cs`,
`ContextSourceBundleHandoffTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/Dev/CompetitionCollectionProfileTests.cs`,
`CollectContextDevCommandTests.cs`, and
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextProfileCommandTests.cs`.

P1-04 owns only `src/Core/BundesligaClubEloRefresh.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaClubEloRefreshSource.cs`,
`data/bundesliga-2026-27/club-elo-name-map.csv`,
`docs/sources/bundesliga-2026-27-club-elo.md`, its three named Club Elo
fixtures, and the literal existing `BundesligaClubElo*`, seed source/command,
and named Core/Orchestrator test paths frozen in ADR-0074. P1-05 owns only
`src/Core/BundesligaRosterRefresh.cs`,
`src/Orchestrator/Commands/Operations/CollectContext/BundesligaRosterArtifactAcquirer.cs`,
`data/bundesliga-2026-27/rosters/roster-refresh-policy-v1.json`,
`docs/sources/bundesliga-2026-27-rosters.md`, its named test/acquirer paths,
and the literal existing `BundesligaRoster*`, roster source/command/fixture
paths frozen in ADR-0074. `IBundesligaContextSourceObservationProvider` is the
only source extension boundary.

Serialized integration alone owns `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs`,
`src/FirebaseAdapter/ServiceCollectionExtensions.cs`,
`.github/workflows/base-context-collection.yml`,
`.github/workflows/buli2627-production-live-matchday.yml`,
`.github/scripts/Test-PredictionWorkflowContracts.ps1`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/ContextCollectionWorkflowContractTests.cs`,
`README.md`, and the listed plan/ADR/design/task/snapshot files. No category or
wildcard ownership grants additional paths.

For avoidance of doubt, source-writer existing paths are exactly:

```text
P1-04: src/Core/BundesligaClubElo.cs; src/Core/BundesligaClubEloSeed.cs;
src/Core/BundesligaClubEloPublication.cs;
src/Orchestrator/Commands/Operations/CollectContext/BundesligaClubEloSeedSource.cs;
src/Orchestrator/Commands/Operations/CollectContext/CollectContextClubEloCommand.cs;
tests/Core.Tests/BundesligaClubEloSeedTests.cs;
tests/Core.Tests/BundesligaClubEloPublicationTests.cs;
tests/Core.Tests/BundesligaClubEloPolicyTests.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextClubEloCommandTests.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextClubEloCommandFirestoreTests.cs.

P1-05: src/Core/BundesligaRosterModels.cs; src/Core/BundesligaRosterPolicy.cs;
src/Core/BundesligaRosterPublication.cs; src/Core/BundesligaRosterPublicationContract.cs;
src/Core/BundesligaRosterCsv.cs; src/Core/BundesligaRosterSeed.cs;
src/Orchestrator/Commands/Operations/CollectContext/BundesligaRosterSource.cs;
src/Orchestrator/Commands/Operations/CollectContext/CollectContextRostersCommand.cs;
tests/Core.Tests/BundesligaRosterPolicyTests.cs;
tests/Core.Tests/BundesligaRosterPublicationTests.cs;
tests/Core.Tests/BundesligaRosterPublicationContractTests.cs;
tests/Core.Tests/BundesligaRosterCsvTests.cs; tests/Core.Tests/BundesligaRosterSeedTests.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterSourceTests.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterDuckDbFixture.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextRostersCommandTests.cs;
tests/Orchestrator.Tests/Commands/Operations/CollectContext/CollectContextRostersCommandFirestoreTests.cs.
```

Their exact new test/fixture paths are respectively
`tests/Core.Tests/BundesligaClubEloRefreshTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaClubEloRefreshSourceTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/Fixtures/ClubElo/eligible.csv`,
`unknown-source-date.csv`, `partial.csv`; and
`tests/Core.Tests/BundesligaRosterRefreshTests.cs`,
`tests/Orchestrator.Tests/Commands/Operations/CollectContext/BundesligaRosterArtifactAcquirerTests.cs`.

## Resource, authority, and continuity gates

At most two writers and one heavy operation family are admitted. Focused TUnit
uses `dotnet run` under the sole heavy lease; workflow/actionlint and exact-head
CI follow integration. No source flag is enabled by this packet. Legacy mode is
a zero-interaction bypass. Existing heads and the 16-job serial/default-success,
non-cancelling, no-bonus topology remain intact.

P1-05 production enablement requires owner authority for acquisition,
production writes, issue projection, rollback owner, and restoration. P1-04
also requires accepted provider semantics and unattended network/reuse approval.
Rollback turns off the affected flag. Restoration needs green exact-head CI,
valid/no-change/rejection evidence, all eight receipts, independent heads,
issue reconciliation, and copy compatibility without model/post work.

## Validation, review, publication, and stop

Validate canonical schemas/hash/null matrices, hostile handoff/replay/CAS cases,
receipt-health idempotency, disabled bypass, roster current-artifact rejection
plus synthetic takeover/diff/carry/conflict, v1/v2/v3 reconstruction, and
unchanged workflow topology. Each milestone receives independent exact-tip
review before serialized integration; only the reviewed cohesive milestone may
be committed/published. Publication is non-force to the verified canonical
`origin` target and must re-check branch, remote, status, log, scope, and
fast-forward immediately before push.

Stop after the reviewed P1-04/P1-05 closeout is published and the status
snapshot has been updated only for tasks actually completed. Stop earlier for a
new owner gate, material seam change, failed safety gate, or publication refusal.
