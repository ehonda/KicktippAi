using DuckDB.NET.Data;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;
using TestUtilities;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class BundesligaRosterSourceTests
{
    [Test]
    [Arguments("duplicate-club")]
    [Arguments("duplicate-player-id")]
    [Arguments("duplicate-player-name")]
    [Arguments("missing-coach")]
    [Arguments("wrong-competition")]
    [Arguments("wrong-season")]
    [Arguments("count")]
    [Arguments("overlap")]
    [Arguments("coach-player-collision")]
    public async Task Rejected_membership_gates_are_isolated_per_club_while_another_club_can_pass(string scenario)
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        fixture.AddEligibleClub("bmg");
        var b04 = BundesligaTeamManifest.Default.GetByTeamSlug("b04");
        var first = fixture.FirstPlayerId("b04");
        switch (scenario)
        {
            case "duplicate-club": fixture.Execute($"insert into clubs values ({b04.TransfermarktClubId}, 'L1', 2026, 1, 'Duplicate')"); break;
            case "duplicate-player-id": fixture.Execute($"insert into players values ({first}, 'Another Player', {b04.TransfermarktClubId}, 2026, '2000-01-01', 'Midfield')"); break;
            case "duplicate-player-name": fixture.Execute($"insert into players values (7999999, '  {BundesligaRosterSeed.Default.Entries.First(e => e.TeamSlug == "b04" && e.Role == BundesligaRosterRole.Player).Name}  ', {b04.TransfermarktClubId}, 2026, '2000-01-01', 'Midfield')"); break;
            case "missing-coach": fixture.Execute($"update clubs set coach_name = null where club_id = {b04.TransfermarktClubId}"); break;
            case "wrong-competition": fixture.Execute($"update clubs set domestic_competition_id = 'L2' where club_id = {b04.TransfermarktClubId}"); break;
            case "wrong-season": fixture.Execute($"update clubs set last_season = 2025 where club_id = {b04.TransfermarktClubId}"); break;
            case "count": fixture.Execute($"delete from players where current_club_id = {b04.TransfermarktClubId} and player_id = {first}"); break;
            case "overlap": fixture.Execute($"update players set player_id = player_id + 1000000, name = name || ' New' where current_club_id = {b04.TransfermarktClubId}"); break;
            case "coach-player-collision": fixture.Execute($"update clubs set coach_name = (select name from players where player_id = {first}) where club_id = {b04.TransfermarktClubId}"); break;
        }

        var result = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));

        if (scenario == "duplicate-player-id")
        {
            // The raw duplicate is a per-club membership rejection, but the selected
            // fallback identity still has two enrichment rows, which is globally unsafe.
            await Assert.That(result.Diagnostics).Contains("ENRICHMENT_UNAVAILABLE");
            await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
            return;
        }

        await Assert.That(result.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04").MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
        await Assert.That(result.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "bmg").MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.DuckDb);
        await Assert.That(result.QualityRows.Single(row => row.Team.TeamSlug == "b04").DuckDbGateResult).IsEqualTo(BundesligaRosterDuckDbGateResult.Rejected);
    }

    [Test]
    [Arguments("future", "FUTURE_SNAPSHOT")]
    [Arguments("stale", "STALE_SNAPSHOT")]
    [Arguments("older", "SNAPSHOT_OLDER_THAN_REFERENCE")]
    public async Task Snapshot_freshness_gates_retain_the_fallback_and_expose_the_exact_reason(string scenario, string diagnostic)
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var date = scenario switch { "future" => new DateOnly(2026, 8, 19), "stale" => new DateOnly(2026, 8, 3), _ => new DateOnly(2026, 8, 15) };
        var result = await CollectAsync(fixture.Path, date);
        var report = result.QualityRows.Single(row => row.Team.TeamSlug == "b04");
        await Assert.That(report.SelectedSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
        await Assert.That(report.Diagnostics).Contains(diagnostic);
        await Assert.That(report.SourceRevision).IsEqualTo("fixture@1");
        await Assert.That(report.DuckDbSnapshotAsOf).IsEqualTo(date);
    }

    [Test]
    public async Task Missing_file_is_not_a_schema_failure_and_uses_normal_fallback_selection()
    {
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.duckdb");
        var result = await CollectAsync(missing, new DateOnly(2026, 8, 18));
        await Assert.That(result.Diagnostics).Contains("DUCKDB_NOT_AVAILABLE");
        await Assert.That(result.Diagnostics).DoesNotContain("DUCKDB_SCHEMA_OR_QUERY_FAILED");
    }

    [Test]
    public async Task Rejected_duckdb_membership_falls_back_and_is_enriched_at_the_fallback_membership_date()
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var b04 = BundesligaTeamManifest.Default.GetByTeamSlug("b04");
        var playerId = fixture.FirstPlayerId("b04");
        fixture.Execute($"update clubs set coach_name = null where club_id = {b04.TransfermarktClubId}");
        fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-16', 2000000)");

        var snapshot = (await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18))).Snapshots.Single(value => value.Team.TeamSlug == "b04");
        var player = snapshot.Members.Single(value => value.TransfermarktPlayerId == playerId);
        await Assert.That(snapshot.MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
        await Assert.That(snapshot.MembershipAsOf).IsEqualTo(new DateOnly(2026, 8, 16));
        await Assert.That(player.MarketValueEur).IsEqualTo(2_000_000);
    }

    [Test]
    public async Task Last_known_good_selection_is_enriched_at_its_own_membership_date()
    {
        var root = SolutionPathUtility.FindSolutionRoot();
        var baseline = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null), null, new DateOnly(2026, 8, 18));
        var lkg = new BundesligaRosterLastKnownGood(new string('a', 64),
            baseline.Snapshots.Select(snapshot => snapshot with { MembershipAsOf = snapshot.MembershipAsOf.AddDays(1) }).ToArray(),
            baseline.QualityRows, BundesligaRosterCsv.RenderQualityReport(baseline.QualityRows));
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var b04 = BundesligaTeamManifest.Default.GetByTeamSlug("b04");
        var playerId = fixture.FirstPlayerId("b04");
        fixture.Execute($"update clubs set coach_name = null where club_id = {b04.TransfermarktClubId}");
        fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-17', 3000000)");

        var snapshot = (await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18), lkg)).Snapshots.Single(value => value.Team.TeamSlug == "b04");
        await Assert.That(snapshot.MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.LastKnownGood);
        await Assert.That(snapshot.MembershipAsOf).IsEqualTo(new DateOnly(2026, 8, 17));
        await Assert.That(snapshot.Members.Single(value => value.TransfermarktPlayerId == playerId).MarketValueEur).IsEqualTo(3_000_000);
    }

    [Test]
    public async Task Zero_match_fallback_members_emit_a_deterministic_unmatched_id_diagnostic()
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var b04 = BundesligaTeamManifest.Default.GetByTeamSlug("b04");
        fixture.Execute($"delete from players where current_club_id = {b04.TransfermarktClubId}");
        var report = (await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18))).QualityRows.Single(value => value.Team.TeamSlug == "b04");

        await Assert.That(report.SelectedSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
        await Assert.That(report.Diagnostics.Any(value => value.StartsWith("UNMATCHED_STABLE_PLAYER_IDS:", StringComparison.Ordinal))).IsTrue();
        var root = SolutionPathUtility.FindSolutionRoot();
        var baseline = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null), null, new DateOnly(2026, 8, 18));
        var lkg = new BundesligaRosterLastKnownGood(new string('c', 64),
            baseline.Snapshots.Select(snapshot => snapshot with { MembershipAsOf = snapshot.MembershipAsOf.AddDays(1) }).ToArray(),
            baseline.QualityRows, BundesligaRosterCsv.RenderQualityReport(baseline.QualityRows));
        var lkgReport = (await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18), lkg)).QualityRows.Single(value => value.Team.TeamSlug == "b04");
        await Assert.That(lkgReport.SelectedSource).IsEqualTo(BundesligaRosterMembershipSource.LastKnownGood);
        await Assert.That(lkgReport.Diagnostics.Any(value => value.StartsWith("UNMATCHED_STABLE_PLAYER_IDS:", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Duckdb_path_with_connection_string_delimiters_is_treated_as_a_literal_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buli;roster={Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var fixture = new BundesligaRosterDuckDbFixture(Path.Combine(directory, "source;literal.duckdb"));
            fixture.AddEligibleClub("b04");
            var result = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));
            await Assert.That(result.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04").MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.DuckDb);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task Cross_club_duplicate_selected_player_id_blocks_the_complete_snapshot()
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        fixture.AddEligibleClub("bmg");
        var duplicate = fixture.FirstPlayerId("b04");
        var bmg = BundesligaTeamManifest.Default.GetByTeamSlug("bmg");
        fixture.Execute($"update players set player_id = {duplicate} where current_club_id = {bmg.TransfermarktClubId} and player_id = {fixture.FirstPlayerId("bmg")}");
        await Assert.That(() => CollectAsync(fixture.Path, new DateOnly(2026, 8, 18))).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("future-dob")]
    [Arguments("whitespace-position")]
    [Arguments("future-valuation")]
    [Arguments("equal-latest-valuation")]
    public async Task Selected_date_enrichment_uses_only_canonical_past_values(string scenario)
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var playerId = fixture.FirstPlayerId("b04");
        switch (scenario)
        {
            case "future-dob": fixture.Execute($"update players set date_of_birth = '2026-08-19' where player_id = {playerId}"); break;
            case "whitespace-position": fixture.Execute($"update players set position = ' Midfield ' where player_id = {playerId}"); break;
            case "future-valuation": fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-19', 999999999)"); break;
            case "equal-latest-valuation": fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-18', 1000000)"); break;
        }

        var snapshot = (await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18))).Snapshots.Single(value => value.Team.TeamSlug == "b04");
        var player = snapshot.Members.Single(value => value.TransfermarktPlayerId == playerId);
        if (scenario == "future-dob") await Assert.That(player.Age).IsNull();
        if (scenario == "whitespace-position") await Assert.That(player.Position).IsNull();
        await Assert.That(player.MarketValueEur).IsEqualTo(1_000_000);
    }

    [Test]
    public async Task Conflicting_equal_date_or_lossy_numeric_enrichment_retains_no_degraded_membership()
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var playerId = fixture.FirstPlayerId("b04");
        fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-18', 1000001)");
        var conflict = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));
        await Assert.That(conflict.Diagnostics).Contains("ENRICHMENT_UNAVAILABLE");
        await Assert.That(conflict.Snapshots.Single(value => value.Team.TeamSlug == "b04").MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
    }

    [Test]
    [Arguments("fractional", "1000000.5")]
    [Arguments("overflow", "170141183460469231731687303715884105727")]
    public async Task Fractional_or_overflow_valuation_is_never_rounded_into_enrichment(string scenario, string invalidValue)
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var playerId = fixture.FirstPlayerId("b04");
        fixture.Execute("drop table player_valuations");
        fixture.Execute($"create table player_valuations(player_id integer, date date, market_value_in_eur {(scenario == "fractional" ? "double" : "hugeint")})");
        fixture.Execute($"insert into player_valuations values ({playerId}, '2026-08-18', {invalidValue})");

        var result = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));

        await Assert.That(result.Diagnostics).Contains("ENRICHMENT_UNAVAILABLE");
        await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
    }

    [Test]
    public async Task Duplicate_selected_enrichment_rows_are_a_global_source_failure()
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        var playerId = fixture.FirstPlayerId("b04");
        fixture.Execute($"insert into players values ({playerId}, 'Duplicate Enrichment', 99999, 2026, '2000-01-01', 'Midfield')");
        var result = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));
        await Assert.That(result.Diagnostics).Contains("ENRICHMENT_UNAVAILABLE");
        await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
        var root = SolutionPathUtility.FindSolutionRoot();
        var baseline = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null), null, new DateOnly(2026, 8, 18));
        var retained = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18),
            new BundesligaRosterLastKnownGood(new string('b', 64), baseline.Snapshots, baseline.QualityRows, BundesligaRosterCsv.RenderQualityReport(baseline.QualityRows)));
        await Assert.That(retained.RetainLastKnownGood).IsTrue();
        await Assert.That(retained.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.LastKnownGood)).IsTrue();
    }

    [Test]
    [Arguments("fractional", "1.5")]
    [Arguments("overflow", "2147483648")]
    public async Task Lossy_membership_values_are_global_source_failures(string scenario, string invalidValue)
    {
        using var fixture = new BundesligaRosterDuckDbFixture();
        fixture.AddEligibleClub("b04");
        fixture.Execute("alter table players alter player_id type double");
        fixture.Execute($"update players set player_id = {invalidValue} where player_id = {fixture.FirstPlayerId("b04")}");
        var result = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18));
        await Assert.That(result.Diagnostics).Contains("DUCKDB_SCHEMA_OR_QUERY_FAILED");
        await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
        var root = SolutionPathUtility.FindSolutionRoot();
        var baseline = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null), null, new DateOnly(2026, 8, 18));
        var retained = await CollectAsync(fixture.Path, new DateOnly(2026, 8, 18),
            new BundesligaRosterLastKnownGood(new string('a', 64), baseline.Snapshots, baseline.QualityRows, baseline.QualityRows.Count == 0 ? string.Empty : BundesligaRosterCsv.RenderQualityReport(baseline.QualityRows)));
        await Assert.That(retained.RetainLastKnownGood).IsTrue();
        await Assert.That(retained.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.LastKnownGood)).IsTrue();
    }

    [Test]
    [Arguments(-1, BundesligaRosterMembershipSource.FallbackSeed)]
    [Arguments(0, BundesligaRosterMembershipSource.LastKnownGood)]
    [Arguments(1, BundesligaRosterMembershipSource.LastKnownGood)]
    public async Task Seed_and_last_known_good_reference_selection_uses_newest_date_with_an_lkg_tie_break(int lkgOffsetDays, BundesligaRosterMembershipSource expected)
    {
        var root = SolutionPathUtility.FindSolutionRoot();
        var baseline = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null),
            null, new DateOnly(2026, 8, 18));
        var lkgSnapshots = baseline.Snapshots.Select(snapshot => snapshot with { MembershipAsOf = snapshot.MembershipAsOf.AddDays(lkgOffsetDays) }).ToArray();
        var lkgRows = baseline.QualityRows.Select(row => row with { MembershipAsOf = row.MembershipAsOf.AddDays(lkgOffsetDays) }).ToArray();
        var built = BundesligaRosterPublication.Build(lkgSnapshots, lkgRows);
        var published = built.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var lkg = BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(
            new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
                DocumentPublicationContract.ComputeSnapshotId(built.Documents), null, DateTimeOffset.UtcNow, built.MetadataJson,
                published.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
                    DocumentPublicationContract.ComputeContentSha256(document.Content)))),
            published));

        var selected = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null),
            lkg, new DateOnly(2026, 8, 18));

        await Assert.That(selected.Snapshots.Select(snapshot => snapshot.MembershipSource).Distinct()).IsEquivalentTo([expected]);
        var actualB04 = selected.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04");
        var expectedB04 = expected == BundesligaRosterMembershipSource.LastKnownGood
            ? lkg.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04") with { MembershipSource = expected }
            : baseline.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04");
        await Assert.That(BundesligaRosterCsv.RenderTeamRoster(actualB04)).IsEqualTo(BundesligaRosterCsv.RenderTeamRoster(expectedB04));
    }

    [Test]
    public async Task Valid_local_duckdb_takes_over_only_the_club_that_passes_every_gate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buli-roster-{Guid.NewGuid():N}.duckdb");
        try
        {
            CreateDuckDb(path);
            var root = SolutionPathUtility.FindSolutionRoot();
            var source = new BundesligaRosterSource();
            var result = await source.CollectAsync(new BundesligaRosterSourceRequest(
                Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath),
                path, "fixture@1", new DateOnly(2026, 8, 18)), null, new DateOnly(2026, 8, 18));

            var b04 = result.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "b04");
            var bmg = result.Snapshots.Single(snapshot => snapshot.Team.TeamSlug == "bmg");
            var b04Report = result.QualityRows.Single(row => row.Team.TeamSlug == "b04");

            await Assert.That(b04.MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.DuckDb);
            await Assert.That(bmg.MembershipSource).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
            await Assert.That(b04.Members.Where(member => member.Role == BundesligaRosterRole.Player).All(member => member.Age is not null && member.Position is not null && member.MarketValueEur == 1_000_000)).IsTrue();
            await Assert.That(b04Report.DuckDbGateResult).IsEqualTo(BundesligaRosterDuckDbGateResult.Pass);
            await Assert.That(b04Report.SourceRevision).IsEqualTo("fixture@1");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Missing_required_schema_keeps_an_initial_complete_seed_and_exposes_a_diagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buli-roster-{Guid.NewGuid():N}.duckdb");
        try
        {
            using (var connection = new DuckDBConnection($"Data Source={path}")) { connection.Open(); }
            var root = SolutionPathUtility.FindSolutionRoot();
            var result = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
                Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath),
                path, "fixture@1", new DateOnly(2026, 8, 18)), null, new DateOnly(2026, 8, 18));

            await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
            await Assert.That(result.Diagnostics).Contains("DUCKDB_SCHEMA_OR_QUERY_FAILED");
            await Assert.That(result.QualityRows.SelectMany(row => row.Diagnostics)).Contains("ENRICHMENT_UNAVAILABLE");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Missing_local_duckdb_file_is_a_safe_initial_seed_fallback()
    {
        var root = SolutionPathUtility.FindSolutionRoot();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.duckdb");
        var result = await new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath),
            missing, "fixture@1", new DateOnly(2026, 8, 18)), null, new DateOnly(2026, 8, 18));

        await Assert.That(result.Snapshots.All(snapshot => snapshot.MembershipSource == BundesligaRosterMembershipSource.FallbackSeed)).IsTrue();
        await Assert.That(result.Diagnostics).Contains("DUCKDB_NOT_AVAILABLE");
    }

    private static void CreateDuckDb(string path)
    {
        var seed = BundesligaRosterSeed.Default.Entries.Where(entry => entry.TeamSlug == "b04" && entry.Role == BundesligaRosterRole.Player).ToArray();
        using var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        Execute(connection, "create table clubs(club_id integer, domestic_competition_id varchar, last_season integer, squad_size integer, coach_name varchar)");
        Execute(connection, "create table players(player_id integer, name varchar, current_club_id integer, last_season integer, date_of_birth date, position varchar)");
        Execute(connection, "create table player_valuations(player_id integer, date date, market_value_in_eur bigint)");
        Execute(connection, $"insert into clubs values (15, 'L1', 2026, {seed.Length}, 'Coach Alpha')");
        var id = 900_000;
        foreach (var player in seed)
        {
            var playerId = player.TransfermarktPlayerId ?? ++id;
            using var playerCommand = connection.CreateCommand();
            playerCommand.CommandText = "insert into players values ($id, $name, 15, 2026, '2000-01-01', 'Midfield')";
            playerCommand.Parameters.Add(new DuckDBParameter("id", playerId));
            playerCommand.Parameters.Add(new DuckDBParameter("name", player.Name));
            playerCommand.ExecuteNonQuery();
            Execute(connection, $"insert into player_valuations values ({playerId}, '2026-08-18', 1000000)");
        }
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Task<BundesligaRosterCollection> CollectAsync(string duckDbPath, DateOnly snapshotDate, BundesligaRosterLastKnownGood? lkg = null)
    {
        var root = SolutionPathUtility.FindSolutionRoot();
        return new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath),
            duckDbPath, "fixture@1", snapshotDate), lkg, new DateOnly(2026, 8, 18));
    }
}
