using System.Globalization;
using DuckDB.NET.Data;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

public interface IBundesligaRosterSource
{
    Task<BundesligaRosterCollection> CollectAsync(
        BundesligaRosterSourceRequest request,
        BundesligaRosterLastKnownGood? lastKnownGood,
        DateOnly evaluationDate,
        CancellationToken cancellationToken = default);
}

/// <summary>Local-only implementation of the ADR-0017 roster source contract.</summary>
internal sealed class BundesligaRosterSource : IBundesligaRosterSource
{
    public Task<BundesligaRosterCollection> CollectAsync(
        BundesligaRosterSourceRequest request,
        BundesligaRosterLastKnownGood? lastKnownGood,
        DateOnly evaluationDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.LaunchEnrichmentOverlay
            && (string.IsNullOrWhiteSpace(request.DuckDbPath)
                || string.IsNullOrWhiteSpace(request.DuckDbRevision)
                || request.DuckDbSnapshotAsOf is null))
        {
            throw new ArgumentException(
                "Launch enrichment overlay requires a DuckDB path, revision, and snapshot date.",
                nameof(request));
        }
        var manifest = ReadManifest(request.ManifestPath);
        var seed = BundesligaRosterSeed.Parse(File.ReadAllBytes(ResolveExisting(request.SeedPath, "Roster seed")), manifest.Entries, request.SeedPath);
        var fallback = BuildSeedSnapshots(seed, manifest.Entries);
        var lkgSnapshots = lastKnownGood?.Snapshots.ToDictionary(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal);
        IReadOnlyDictionary<string, DuckDbClubData>? duckDb = null;
        var duckDbAvailableForEnrichment = false;
        string? duckDbFailure = null;
        if (!string.IsNullOrWhiteSpace(request.DuckDbPath))
        {
            if (string.IsNullOrWhiteSpace(request.DuckDbRevision) || request.DuckDbSnapshotAsOf is null)
            {
                duckDbFailure = "DUCKDB_PROVENANCE_REQUIRED";
            }
            else if (!File.Exists(Path.GetFullPath(request.DuckDbPath)))
            {
                // A caller may deliberately omit an optional local artifact.  It is not a
                // malformed artifact and must remain distinguishable in the audit report.
                duckDbFailure = "DUCKDB_NOT_AVAILABLE";
            }
            else
            {
                try
                {
                    var resolvedDuckDbPath = ResolveExisting(request.DuckDbPath, "DuckDB");
                    if (!request.LaunchEnrichmentOverlay)
                    {
                        duckDb = ReadDuckDb(resolvedDuckDbPath, manifest.Entries);
                    }
                    duckDbAvailableForEnrichment = true;
                }
                catch (Exception exception) when (exception is DuckDBException or InvalidOperationException or FormatException or OverflowException or FileNotFoundException)
                {
                    duckDbFailure = "DUCKDB_SCHEMA_OR_QUERY_FAILED";
                }
            }
        }

        // ADR-0011: schema/query failure preserves a headed LKG rather than publishing a degraded replacement.
        if (duckDbFailure == "DUCKDB_SCHEMA_OR_QUERY_FAILED" && lastKnownGood is not null)
        {
            var retained = lastKnownGood.Snapshots.Select(snapshot => snapshot with { MembershipSource = BundesligaRosterMembershipSource.LastKnownGood }).ToArray();
            return Task.FromResult(new BundesligaRosterCollection(retained, lastKnownGood.QualityRows,
                [duckDbFailure], request.SeedPath, request.ManifestPath, request.DuckDbPath, RetainLastKnownGood: true));
        }

        var selected = new List<BundesligaRosterClubSnapshot>();
        var evaluations = new Dictionary<string, BundesligaRosterDuckDbEvaluation>(StringComparer.Ordinal);
        foreach (var team in manifest.Entries)
        {
            var seedSnapshot = fallback[team.TeamSlug];
            var lkg = lkgSnapshots?.GetValueOrDefault(team.TeamSlug);
            var reference = PickReference(seedSnapshot, lkg);
            if (request.LaunchEnrichmentOverlay)
            {
                var overlayEvaluation = new BundesligaRosterDuckDbEvaluation(
                    BundesligaRosterDuckDbGateResult.NotEvaluated,
                    ["LAUNCH_ENRICHMENT_OVERLAY"]);
                evaluations[team.TeamSlug] = overlayEvaluation;
                var overlaySelection = BundesligaRosterPolicy.SelectMembership(
                    MembershipCandidate(seedSnapshot, BundesligaRosterMembershipSource.FallbackSeed),
                    lkg is null ? null : MembershipCandidate(lkg, BundesligaRosterMembershipSource.LastKnownGood, lastKnownGood!.SnapshotId),
                    null,
                    overlayEvaluation);
                selected.Add(overlaySelection.Selected.Source == BundesligaRosterMembershipSource.LastKnownGood
                    ? lkg! with { MembershipSource = BundesligaRosterMembershipSource.LastKnownGood }
                    : seedSnapshot);
                continue;
            }

            DuckDbClubData? data = duckDb?.GetValueOrDefault(team.TeamSlug);
            var candidate = data is null || request.DuckDbSnapshotAsOf is null || string.IsNullOrWhiteSpace(request.DuckDbRevision)
                ? null
                : data.ToCandidate(team.TeamSlug, team.TransfermarktClubId, request.DuckDbSnapshotAsOf.Value, request.DuckDbRevision);
            var evaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(candidate, evaluationDate, reference.MembershipAsOf,
                reference.Members.Where(member => member.Role == BundesligaRosterRole.Player)
                    .Select(member => new BundesligaRosterIdentity(member.TransfermarktPlayerId, member.Name)).ToArray());
            evaluations[team.TeamSlug] = evaluation;
            var selection = BundesligaRosterPolicy.SelectMembership(
                MembershipCandidate(seedSnapshot, BundesligaRosterMembershipSource.FallbackSeed),
                lkg is null ? null : MembershipCandidate(lkg, BundesligaRosterMembershipSource.LastKnownGood, lastKnownGood!.SnapshotId),
                candidate is null ? null : new BundesligaRosterMembershipCandidate(team.TeamSlug, BundesligaRosterMembershipSource.DuckDb, request.DuckDbSnapshotAsOf!.Value, true),
                evaluation);
            var snapshot = selection.Selected.Source switch
            {
                BundesligaRosterMembershipSource.DuckDb => BuildDuckDbSnapshot(team, data!, request.DuckDbSnapshotAsOf!.Value),
                BundesligaRosterMembershipSource.LastKnownGood => lkg! with { MembershipSource = BundesligaRosterMembershipSource.LastKnownGood },
                _ => seedSnapshot
            };
            selected.Add(snapshot);
        }

        var duplicateSelectedPlayerId = selected
            .SelectMany(snapshot => snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player)
                .Select(member => (snapshot.Team.TeamSlug, member.TransfermarktPlayerId)))
            .Where(member => member.TransfermarktPlayerId is not null)
            .GroupBy(member => member.TransfermarktPlayerId!.Value)
            .FirstOrDefault(group => group.Select(member => member.TeamSlug).Distinct(StringComparer.Ordinal).Count() > 1);
        if (duplicateSelectedPlayerId is not null)
        {
            throw new InvalidOperationException($"DUPLICATE_SELECTED_PLAYER_ID:{duplicateSelectedPlayerId.Key}");
        }

        // Enrichment is independent of membership selection and only attaches exact stable IDs at
        // each selected membership date. A whole enrichment failure cannot replace a valid LKG.
        var unmatchedByTeam = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        if (duckDbAvailableForEnrichment)
        {
            try
            {
                var enrichments = ReadSelectedEnrichment(ResolveExisting(request.DuckDbPath!, "DuckDB"), selected);
                var enriched = selected.Select(snapshot => Enrich(snapshot, enrichments[snapshot.Team.TeamSlug])).ToArray();
                selected = enriched.Select(value => value.Snapshot).ToList();
                unmatchedByTeam = enriched.ToDictionary(value => value.Snapshot.Team.TeamSlug, value => value.UnmatchedStablePlayerIds, StringComparer.Ordinal);
            }
            catch (Exception exception) when (exception is DuckDBException or InvalidOperationException or FormatException or OverflowException or FileNotFoundException)
            {
                if (lastKnownGood is not null)
                {
                    var retained = lastKnownGood.Snapshots.Select(snapshot => snapshot with { MembershipSource = BundesligaRosterMembershipSource.LastKnownGood }).ToArray();
                    return Task.FromResult(new BundesligaRosterCollection(retained, lastKnownGood.QualityRows,
                        ["ENRICHMENT_UNAVAILABLE"], request.SeedPath, request.ManifestPath, request.DuckDbPath, RetainLastKnownGood: true));
                }

                selected = fallback.Values.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToList();
                duckDbFailure = "ENRICHMENT_UNAVAILABLE";
            }
        }
        var rows = BuildQualityRows(selected, manifest.Entries, lastKnownGood?.SnapshotId, request.DuckDbRevision,
            request.DuckDbSnapshotAsOf, evaluations, duckDbFailure, evaluationDate, unmatchedByTeam);
        return Task.FromResult(new BundesligaRosterCollection(selected, rows,
            duckDbFailure is null ? [] : [duckDbFailure], request.SeedPath, request.ManifestPath, request.DuckDbPath));
    }

    private static BundesligaRosterMembershipCandidate MembershipCandidate(BundesligaRosterClubSnapshot snapshot, BundesligaRosterMembershipSource source, string? id = null) =>
        new(snapshot.Team.TeamSlug, source, snapshot.MembershipAsOf, true, id);

    private static BundesligaRosterClubSnapshot PickReference(BundesligaRosterClubSnapshot seed, BundesligaRosterClubSnapshot? lkg) =>
        lkg is null || seed.MembershipAsOf > lkg.MembershipAsOf ? seed : lkg;

    private static Dictionary<string, BundesligaRosterClubSnapshot> BuildSeedSnapshots(BundesligaRosterSeed seed, IReadOnlyList<BundesligaTeamManifestEntry> teams) =>
        seed.Entries.GroupBy(entry => entry.TeamSlug, StringComparer.Ordinal).ToDictionary(group => group.Key, group =>
        {
            var team = teams.Single(entry => entry.TeamSlug == group.Key);
            return new BundesligaRosterClubSnapshot(team, group.First().MembershipAsOf, BundesligaRosterMembershipSource.FallbackSeed,
                group.Select(entry => new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId)).ToArray());
        }, StringComparer.Ordinal);

    private static IReadOnlyList<BundesligaRosterQualityReportRow> BuildQualityRows(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry> teams,
        string? lastKnownGoodSnapshotId,
        string? revision,
        DateOnly? duckDbSnapshotAsOf,
        IReadOnlyDictionary<string, BundesligaRosterDuckDbEvaluation> evaluations,
        string? sourceFailure,
        DateOnly evaluationDate,
        IReadOnlyDictionary<string, IReadOnlyList<int>> unmatchedByTeam)
    {
        return snapshots.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).Select(snapshot =>
        {
            var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
            var diagnostics = new List<string>();
            if (evaluations.TryGetValue(snapshot.Team.TeamSlug, out var evaluation)) diagnostics.AddRange(evaluation.Diagnostics);
            diagnostics.AddRange(BundesligaRosterPolicy.GetFreshnessDiagnostics(snapshot.MembershipAsOf, evaluationDate));
            diagnostics.AddRange(BundesligaRosterPolicy.GetEnrichmentCoverageDiagnostics(players.Length, players.Count(p => p.Age is not null), players.Count(p => p.Position is not null), players.Count(p => p.MarketValueEur is not null)));
            if (players.Any(player => player.Age is null)) diagnostics.Add($"MISSING_DOB_OR_AGE:{players.Count(player => player.Age is null)}");
            if (players.Any(player => player.Position is null)) diagnostics.Add($"MISSING_OR_INVALID_POSITION:{players.Count(player => player.Position is null)}");
            if (players.Any(player => player.MarketValueEur is null)) diagnostics.Add($"MISSING_VALUATION:{players.Count(player => player.MarketValueEur is null)}");
            if (players.Any(player => player.TransfermarktPlayerId is null)) diagnostics.Add($"MISSING_STABLE_PLAYER_IDS:{players.Count(player => player.TransfermarktPlayerId is null)}");
            if (unmatchedByTeam.TryGetValue(snapshot.Team.TeamSlug, out var unmatched) && unmatched.Count > 0)
            {
                diagnostics.Add($"UNMATCHED_STABLE_PLAYER_IDS:{string.Join(',', unmatched.Order())}");
            }
            if (sourceFailure is not null) diagnostics.Add(sourceFailure == "DUCKDB_SCHEMA_OR_QUERY_FAILED" ? "ENRICHMENT_UNAVAILABLE" : sourceFailure);
            var isDuck = snapshot.MembershipSource == BundesligaRosterMembershipSource.DuckDb;
            var teamEvaluation = evaluations.GetValueOrDefault(snapshot.Team.TeamSlug);
            var gateResult = teamEvaluation?.Result ?? BundesligaRosterDuckDbGateResult.NotAvailable;
            var membershipSuffix = snapshot.MembershipSource == BundesligaRosterMembershipSource.LastKnownGood
                ? "USE_LAST_KNOWN_GOOD"
                : "USE_FALLBACK_SEED";
            var selectionReason = isDuck
                ? "DUCKDB_GATES_PASSED"
                : teamEvaluation?.Diagnostics.Contains("LAUNCH_ENRICHMENT_OVERLAY", StringComparer.Ordinal) == true
                    ? $"LAUNCH_ENRICHMENT_OVERLAY_{membershipSuffix}"
                    : $"{(gateResult is BundesligaRosterDuckDbGateResult.NotAvailable or BundesligaRosterDuckDbGateResult.NotEvaluated ? "DUCKDB_NOT_AVAILABLE" : "DUCKDB_REJECTED")}_{membershipSuffix}";
            return new BundesligaRosterQualityReportRow(snapshot.Team, snapshot.MembershipSource, snapshot.MembershipAsOf,
                [snapshot.Team.OfficialRosterSourceUrl], revision,
                snapshot.MembershipSource == BundesligaRosterMembershipSource.LastKnownGood ? lastKnownGoodSnapshotId : null,
                duckDbSnapshotAsOf, players.Length, 1, players.Count(p => p.TransfermarktPlayerId is not null),
                players.Count(p => p.Age is not null), players.Count(p => p.Position is not null), players.Count(p => p.MarketValueEur is not null),
                gateResult, selectionReason,
                diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }).ToArray();
    }

    private static BundesligaRosterClubSnapshot BuildDuckDbSnapshot(BundesligaTeamManifestEntry team, DuckDbClubData data, DateOnly asOf) =>
        new(team, asOf, BundesligaRosterMembershipSource.DuckDb,
            new[] { new BundesligaRosterMember(BundesligaRosterRole.Coach, BundesligaRosterSeed.NormalizeName(data.HeadCoach!)) }
                .Concat(data.Players.Select(player => new BundesligaRosterMember(BundesligaRosterRole.Player, BundesligaRosterSeed.NormalizeName(player.Name), player.PlayerId))
                    .OrderBy(member => member.Name, StringComparer.Ordinal).ThenBy(member => member.TransfermarktPlayerId))
                .ToArray());

    private static EnrichedSnapshot Enrich(BundesligaRosterClubSnapshot snapshot, IReadOnlyDictionary<int, EnrichmentMatch> enrichment)
    {
        var unmatched = new List<int>();
        var members = snapshot.Members.Select(member =>
        {
            if (member.Role == BundesligaRosterRole.Coach || member.TransfermarktPlayerId is null) return member;
            var value = enrichment[member.TransfermarktPlayerId.Value];
            if (!value.Found) { unmatched.Add(member.TransfermarktPlayerId.Value); return member; }
            return member with { Age = value.Value!.Age, Position = value.Value.Position, MarketValueEur = value.Value.MarketValueEur };
        }).ToArray();
        return new EnrichedSnapshot(snapshot with { Members = members }, unmatched.Order().ToArray());
    }

    private static IReadOnlyDictionary<string, DuckDbClubData> ReadDuckDb(string path, IReadOnlyList<BundesligaTeamManifestEntry> teams)
    {
        using var connection = OpenReadOnly(path);
        EnsureSchema(connection);
        var result = new Dictionary<string, DuckDbClubData>(StringComparer.Ordinal);
        foreach (var team in teams)
        {
            result.Add(team.TeamSlug, ReadClub(connection, team));
        }
        return result;
    }

    private static void EnsureSchema(DuckDBConnection connection)
    {
        var required = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["clubs"] = ["club_id", "domestic_competition_id", "last_season", "squad_size", "coach_name"],
            ["players"] = ["player_id", "name", "current_club_id", "last_season", "date_of_birth", "position"],
            ["player_valuations"] = ["player_id", "date", "market_value_in_eur"]
        };
        foreach (var (table, columns) in required)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "select column_name from information_schema.columns where table_schema = 'main' and table_name = $table";
            command.Parameters.Add(new DuckDBParameter("table", table));
            using var reader = command.ExecuteReader();
            var actual = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read()) actual.Add(reader.GetString(0));
            if (!columns.All(actual.Contains)) throw new InvalidOperationException($"DuckDB table '{table}' lacks the ADR-0017 schema.");
        }
    }

    private static DuckDbClubData ReadClub(DuckDBConnection connection, BundesligaTeamManifestEntry team)
    {
        if (team.TransfermarktClubId is null) throw new InvalidOperationException($"Manifest club '{team.TeamSlug}' has no Transfermarkt ID.");
        var clubs = new List<(string Competition, int Season, int? SquadSize, string? Coach)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select domestic_competition_id, last_season, squad_size, coach_name from clubs where cast(club_id as varchar) = $club";
            command.Parameters.Add(new DuckDBParameter("club", team.TransfermarktClubId.Value.ToString(CultureInfo.InvariantCulture)));
            using var reader = command.ExecuteReader();
            while (reader.Read()) clubs.Add((Text(reader, 0), Int(reader, 1), NullableInt(reader, 2), NullableText(reader, 3)));
        }
        var players = new List<DuckDbPlayer>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select player_id, name, current_club_id, last_season, date_of_birth, position from players where cast(current_club_id as varchar) = $club";
            command.Parameters.Add(new DuckDBParameter("club", team.TransfermarktClubId.Value.ToString(CultureInfo.InvariantCulture)));
            using var reader = command.ExecuteReader();
            while (reader.Read()) players.Add(new DuckDbPlayer(Int(reader, 0), Text(reader, 1), Int(reader, 2), Int(reader, 3), NullableDate(reader, 4), NullableText(reader, 5)));
        }
        var club = clubs.FirstOrDefault(); // Count remains raw for the duplicate-club gate.
        return new DuckDbClubData(team.TransfermarktClubId, clubs.Count, club.Competition ?? string.Empty, club.Season,
            club.SquadSize, club.Coach, players);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, EnrichmentMatch>> ReadSelectedEnrichment(string path, IReadOnlyList<BundesligaRosterClubSnapshot> snapshots)
    {
        using var connection = OpenReadOnly(path);
        EnsureSchema(connection);
        var result = new Dictionary<string, IReadOnlyDictionary<int, EnrichmentMatch>>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            var enrichment = new Dictionary<int, EnrichmentMatch>();
            foreach (var player in snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player && member.TransfermarktPlayerId is not null))
            {
                var playerId = player.TransfermarktPlayerId
                    ?? throw new InvalidOperationException("Stable-ID enrichment selected a player without a stable ID.");
                enrichment[playerId] = ReadEnrichment(connection, playerId, snapshot.MembershipAsOf);
            }
            result.Add(snapshot.Team.TeamSlug, enrichment);
        }
        return result;
    }

    private static EnrichmentMatch ReadEnrichment(DuckDBConnection connection, int playerId, DateOnly asOf)
    {
        using var player = connection.CreateCommand();
        player.CommandText = "select date_of_birth, position from players where cast(player_id as varchar) = $player";
        player.Parameters.Add(new DuckDBParameter("player", playerId.ToString(CultureInfo.InvariantCulture)));
        using var playerReader = player.ExecuteReader();
        if (!playerReader.Read()) return new EnrichmentMatch(false, null);
        var born = NullableDate(playerReader, 0);
        // ADR-0018 permits only the exact canonical position vocabulary.  Do not
        // repair whitespace here: that would turn a provider-data defect into a
        // trusted prompt classification.
        var position = MapPosition(NullableRawText(playerReader, 1));
        if (playerReader.Read()) throw new InvalidOperationException("DUPLICATE_ENRICHMENT_PLAYER_ID");
        using var valuation = connection.CreateCommand();
        valuation.CommandText = "select date, market_value_in_eur from player_valuations where cast(player_id as varchar) = $player and date <= $date and market_value_in_eur > 0 order by date desc";
        valuation.Parameters.Add(new DuckDBParameter("player", playerId.ToString(CultureInfo.InvariantCulture)));
        valuation.Parameters.Add(new DuckDBParameter("date", asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        using var values = valuation.ExecuteReader();
        long? value = null;
        if (values.Read())
        {
            var latest = Date(values, 0);
            value = Long(values, 1);
            while (values.Read() && Date(values, 0) == latest)
            {
                if (Long(values, 1) != value.Value) throw new InvalidOperationException("CONFLICTING_LATEST_VALUATION");
            }
        }
        return new EnrichmentMatch(true, new Enrichment(CalculateAge(born, asOf), position, value));
    }

    private static int? CalculateAge(DateOnly? born, DateOnly asOf) => born is null || born > asOf
        ? null
        : asOf.Year - born.Value.Year - (asOf < born.Value.AddYears(asOf.Year - born.Value.Year) ? 1 : 0);
    private static BundesligaRosterPosition? MapPosition(string? value) => value switch { "Goalkeeper" => BundesligaRosterPosition.Goalkeeper, "Defender" => BundesligaRosterPosition.Defender, "Midfield" => BundesligaRosterPosition.Midfield, "Attack" => BundesligaRosterPosition.Attack, _ => null };
    private static string Text(System.Data.Common.DbDataReader reader, int i) => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    private static string? NullableText(System.Data.Common.DbDataReader reader, int i) => reader.IsDBNull(i) ? null : Text(reader, i);
    private static string? NullableRawText(System.Data.Common.DbDataReader reader, int i) => reader.IsDBNull(i)
        ? null
        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
    private static int Int(System.Data.Common.DbDataReader reader, int i)
    {
        var value = reader.GetValue(i);
        try
        {
            return value switch
            {
                byte number => number,
                short number => number,
                int number => number,
                long number => checked((int)number),
                decimal number when decimal.Truncate(number) == number => checked((int)number),
                double number when double.IsFinite(number) && Math.Truncate(number) == number => checked((int)number),
                float number when float.IsFinite(number) && MathF.Truncate(number) == number => checked((int)number),
                string text when int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) => number,
                _ => throw new FormatException("DuckDB integer is not a lossless Int32.")
            };
        }
        catch (OverflowException exception) { throw new FormatException("DuckDB integer is outside Int32 range.", exception); }
    }
    private static int? NullableInt(System.Data.Common.DbDataReader reader, int i) => reader.IsDBNull(i) ? null : Int(reader, i);
    private static long Long(System.Data.Common.DbDataReader reader, int i)
    {
        var value = reader.GetValue(i);
        try
        {
            return value switch
            {
                byte number => number,
                short number => number,
                int number => number,
                long number => number,
                decimal number when decimal.Truncate(number) == number => checked((long)number),
                double number when double.IsFinite(number) && Math.Truncate(number) == number => checked((long)number),
                float number when float.IsFinite(number) && MathF.Truncate(number) == number => checked((long)number),
                string text when long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) => number,
                _ => throw new FormatException("DuckDB integer is not a lossless Int64.")
            };
        }
        catch (OverflowException exception) { throw new FormatException("DuckDB integer is outside Int64 range.", exception); }
    }
    private static DateOnly Date(System.Data.Common.DbDataReader reader, int i) => NullableDate(reader, i) ?? throw new FormatException("Required DuckDB date was null.");
    private static DateOnly? NullableDate(System.Data.Common.DbDataReader reader, int i) => reader.IsDBNull(i) ? null : reader.GetValue(i) switch { DateOnly date => date, DateTime date => DateOnly.FromDateTime(date), _ when DateOnly.TryParse(Text(reader, i), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed, _ => throw new FormatException("DuckDB date is invalid.") };
    private static string ResolveExisting(string value, string label) { var path = Path.GetFullPath(value); return File.Exists(path) ? path : throw new FileNotFoundException($"{label} file not found: {path}", path); }
    private static DuckDBConnection OpenReadOnly(string path)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ["Data Source"] = path, ["ACCESS_MODE"] = "READ_ONLY" };
        var connection = new DuckDBConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }
    private static BundesligaTeamManifest ReadManifest(string path) { using var reader = new StreamReader(ResolveExisting(path, "Team manifest")); return BundesligaTeamManifest.Parse(reader, path); }

    private sealed record DuckDbPlayer(int PlayerId, string Name, int CurrentClubId, int LastSeason, DateOnly? DateOfBirth, string? Position);
    private sealed record Enrichment(int? Age, BundesligaRosterPosition? Position, long? MarketValueEur);
    private sealed record EnrichmentMatch(bool Found, Enrichment? Value);
    private sealed record EnrichedSnapshot(BundesligaRosterClubSnapshot Snapshot, IReadOnlyList<int> UnmatchedStablePlayerIds);
    private sealed record DuckDbClubData(int? ClubId, int ClubRows, string Competition, int Season, int? SquadSize, string? HeadCoach, IReadOnlyList<DuckDbPlayer> Players)
    {
        public static DuckDbClubData Invalid(int? clubId) => new(clubId, 0, string.Empty, 0, null, null, []);
        public BundesligaRosterDuckDbCandidate ToCandidate(string slug, int? manifestId, DateOnly asOf, string revision) => new(slug, ClubId, ClubRows, Competition, Season, asOf, revision, SquadSize, HeadCoach,
            Players.Select(player => new BundesligaRosterDuckDbPlayer(player.PlayerId, player.Name, player.CurrentClubId, player.LastSeason)).ToArray());
    }
}

public sealed record BundesligaRosterSourceRequest(
    string SeedPath,
    string ManifestPath,
    string? DuckDbPath,
    string? DuckDbRevision,
    DateOnly? DuckDbSnapshotAsOf,
    bool LaunchEnrichmentOverlay = false);
public sealed record BundesligaRosterCollection(IReadOnlyList<BundesligaRosterClubSnapshot> Snapshots, IReadOnlyList<BundesligaRosterQualityReportRow> QualityRows, IReadOnlyList<string> Diagnostics, string SeedPath, string ManifestPath, string? DuckDbPath, bool RetainLastKnownGood = false);
