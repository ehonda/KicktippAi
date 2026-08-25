using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

public static class BundesligaRosterCsv
{
    public const string MissingValue = "N/A";
    public const string TeamAccumulatedRole = "Team Accumulated";

    public static readonly IReadOnlyList<string> RosterHeaders =
    [
        "Team",
        "Data_Collected_At",
        "Role",
        "Name",
        "Age",
        "Position",
        "Market_Value_EUR"
    ];

    public static readonly IReadOnlyList<string> SummaryHeaders =
    [
        "Team_Slug",
        "Team",
        "Data_Collected_At",
        "Membership_Source",
        "Coach",
        "Squad_Size",
        "Known_Age_Count",
        "Average_Age",
        "Valued_Player_Count",
        "Total_Market_Value_EUR",
        "Median_Market_Value_EUR"
    ];

    public static readonly IReadOnlyList<string> QualityReportHeaders =
    [
        "Team_Slug",
        "Team",
        "Selected_Source",
        "Membership_As_Of",
        "Source_References",
        "Source_Revision",
        "Last_Known_Good_Snapshot_Id",
        "DuckDB_Snapshot_As_Of",
        "Player_Count",
        "Coach_Count",
        "Stable_Player_Id_Count",
        "Known_Age_Count",
        "Known_Position_Count",
        "Valued_Player_Count",
        "DuckDB_Gate_Result",
        "Selection_Reason",
        "Diagnostics"
    ];

    private static readonly Regex MachineCodePattern = new(
        "^[A-Z0-9_]+(?::[^;\\r\\n]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string RenderTeamRoster(BundesligaRosterClubSnapshot snapshot)
        => RenderTeamRoster(snapshot, includeTeamAccumulated: true);

    internal static string RenderLegacyTeamRoster(BundesligaRosterClubSnapshot snapshot)
        => RenderTeamRoster(snapshot, includeTeamAccumulated: false);

    private static string RenderTeamRoster(BundesligaRosterClubSnapshot snapshot, bool includeTeamAccumulated)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);
        return WriteCsv(RosterHeaders, csv => WriteRosterRows(csv, [snapshot], includeTeamAccumulated));
    }

    public static string RenderAggregate(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null)
        => RenderAggregate(snapshots, expectedTeams, includeTeamAccumulated: true);

    internal static string RenderLegacyAggregate(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null)
        => RenderAggregate(snapshots, expectedTeams, includeTeamAccumulated: false);

    private static string RenderAggregate(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams,
        bool includeTeamAccumulated)
    {
        var ordered = ValidateAndOrderSnapshots(snapshots, expectedTeams);
        return WriteCsv(RosterHeaders, csv => WriteRosterRows(csv, ordered, includeTeamAccumulated));
    }

    public static string RenderSummary(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null)
    {
        var ordered = ValidateAndOrderSnapshots(snapshots, expectedTeams);
        return WriteCsv(SummaryHeaders, csv =>
        {
            foreach (var snapshot in ordered)
            {
                var coach = snapshot.Members.Single(member => member.Role == BundesligaRosterRole.Coach);
                var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
                var ages = players.Where(player => player.Age is not null).Select(player => player.Age!.Value).ToArray();
                var values = KnownMarketValues(players);

                csv.WriteField(snapshot.Team.TeamSlug);
                csv.WriteField(snapshot.Team.KicktippName);
                csv.WriteField(FormatDate(snapshot.MembershipAsOf));
                csv.WriteField(FormatSource(snapshot.MembershipSource));
                csv.WriteField(BundesligaRosterSeed.NormalizeName(coach.Name));
                csv.WriteField(players.Length);
                csv.WriteField(ages.Length);
                csv.WriteField(ages.Length == 0
                    ? MissingValue
                    : ages.Average().ToString("0.0", CultureInfo.InvariantCulture));
                csv.WriteField(values.Length);
                csv.WriteField(values.Length == 0 ? MissingValue : FormatMoney(values.Sum()));
                csv.WriteField(values.Length == 0 ? MissingValue : FormatMoney(Median(values)));
                csv.NextRecord();
            }
        });
    }

    public static string RenderQualityReport(
        IReadOnlyList<BundesligaRosterQualityReportRow> rows,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        expectedTeams ??= BundesligaTeamManifest.Default.Entries;
        var ordered = rows.OrderBy(row => row.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        var expectedBySlug = expectedTeams.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
        if (!ordered.Select(row => row.Team.TeamSlug).SequenceEqual(expectedBySlug.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Bundesliga roster quality report must contain exactly the expected Team_Slug values.");
        }

        if (ordered.Any(row => !Equals(row.Team, expectedBySlug[row.Team.TeamSlug])))
        {
            throw new InvalidDataException("Bundesliga roster quality report team identity does not match the manifest.");
        }

        return WriteCsv(QualityReportHeaders, csv =>
        {
            foreach (var row in ordered)
            {
                ValidateQualityReportRow(row);
                csv.WriteField(row.Team.TeamSlug);
                csv.WriteField(row.Team.KicktippName);
                csv.WriteField(FormatSource(row.SelectedSource));
                csv.WriteField(FormatDate(row.MembershipAsOf));
                csv.WriteField(string.Join(
                    " | ",
                    row.SourceReferences
                        .Select(reference => reference.AbsoluteUri)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)));
                csv.WriteField(ValueOrMissing(row.SourceRevision));
                csv.WriteField(ValueOrMissing(row.LastKnownGoodSnapshotId));
                csv.WriteField(row.DuckDbSnapshotAsOf is null ? MissingValue : FormatDate(row.DuckDbSnapshotAsOf.Value));
                csv.WriteField(row.PlayerCount);
                csv.WriteField(row.CoachCount);
                csv.WriteField(row.StablePlayerIdCount);
                csv.WriteField(row.KnownAgeCount);
                csv.WriteField(row.KnownPositionCount);
                csv.WriteField(row.ValuedPlayerCount);
                csv.WriteField(FormatGateResult(row.DuckDbGateResult));
                csv.WriteField(row.SelectionReason);
                csv.WriteField(row.Diagnostics.Count == 0
                    ? "NONE"
                    : string.Join(';', row.Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
                csv.NextRecord();
            }
        });
    }

    private static void WriteRosterRows(
        CsvWriter csv,
        IEnumerable<BundesligaRosterClubSnapshot> snapshots,
        bool includeTeamAccumulated)
    {
        foreach (var snapshot in snapshots)
        {
            foreach (var member in OrderMembers(snapshot.Members))
            {
                csv.WriteField(snapshot.Team.KicktippName);
                csv.WriteField(FormatDate(snapshot.MembershipAsOf));
                csv.WriteField(member.Role.ToString());
                csv.WriteField(BundesligaRosterSeed.NormalizeName(member.Name));
                csv.WriteField(member.Role == BundesligaRosterRole.Coach || member.Age is null
                    ? MissingValue
                    : member.Age.Value.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(member.Role == BundesligaRosterRole.Coach
                    ? "Coach"
                    : member.Position?.ToString() ?? MissingValue);
                csv.WriteField(member.Role == BundesligaRosterRole.Coach || member.MarketValueEur is null
                    ? MissingValue
                    : FormatMoney(member.MarketValueEur.Value));
                csv.NextRecord();
            }

            if (includeTeamAccumulated)
            {
                var total = KnownMarketValueTotal(snapshot.Members);
                csv.WriteField(snapshot.Team.KicktippName);
                csv.WriteField(FormatDate(snapshot.MembershipAsOf));
                csv.WriteField(TeamAccumulatedRole);
                csv.WriteField(MissingValue);
                csv.WriteField(MissingValue);
                csv.WriteField(MissingValue);
                csv.WriteField(total is null ? MissingValue : FormatMoney(total.Value));
                csv.NextRecord();
            }
        }
    }

    internal static long? KnownMarketValueTotal(IEnumerable<BundesligaRosterMember> members)
    {
        var values = KnownMarketValues(members);
        return values.Length == 0 ? null : values.Sum();
    }

    private static long[] KnownMarketValues(IEnumerable<BundesligaRosterMember> members) => members
        .Where(member => member.Role == BundesligaRosterRole.Player && member.MarketValueEur is not null)
        .Select(member => member.MarketValueEur!.Value)
        .Order()
        .ToArray();

    private static IReadOnlyList<BundesligaRosterClubSnapshot> ValidateAndOrderSnapshots(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        expectedTeams ??= BundesligaTeamManifest.Default.Entries;
        foreach (var snapshot in snapshots)
        {
            ValidateSnapshot(snapshot);
        }

        var ordered = snapshots.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        var expectedBySlug = expectedTeams.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
        if (!ordered.Select(snapshot => snapshot.Team.TeamSlug).SequenceEqual(expectedBySlug.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Bundesliga roster snapshots must contain exactly the expected Team_Slug values.");
        }

        if (ordered.Any(snapshot => !Equals(snapshot.Team, expectedBySlug[snapshot.Team.TeamSlug])))
        {
            throw new InvalidDataException("Bundesliga roster snapshot team identity does not match the manifest.");
        }

        var duplicatePlayerId = ordered
            .SelectMany(snapshot => snapshot.Members)
            .Where(member => member.Role == BundesligaRosterRole.Player && member.TransfermarktPlayerId is not null)
            .GroupBy(member => member.TransfermarktPlayerId!.Value)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePlayerId is not null)
        {
            throw new InvalidDataException(
                $"Transfermarkt player ID {duplicatePlayerId.Key} occurs in more than one Bundesliga roster snapshot.");
        }

        return ordered;
    }

    private static void ValidateSnapshot(BundesligaRosterClubSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Team);
        ArgumentNullException.ThrowIfNull(snapshot.Members);
        var coaches = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Coach).ToArray();
        var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
        if (coaches.Length != 1)
        {
            throw new InvalidDataException(
                $"Roster '{snapshot.Team.TeamSlug}' requires exactly one coach but found {coaches.Length}.");
        }

        if (players.Length is < BundesligaRosterPolicy.MinimumPlayerCount or > BundesligaRosterPolicy.MaximumPlayerCount)
        {
            throw new InvalidDataException(
                $"Roster '{snapshot.Team.TeamSlug}' requires {BundesligaRosterPolicy.MinimumPlayerCount}-{BundesligaRosterPolicy.MaximumPlayerCount} players but found {players.Length}.");
        }

        foreach (var member in snapshot.Members)
        {
            _ = BundesligaRosterSeed.NormalizeName(member.Name);
            if (member.TransfermarktPlayerId is <= 0)
            {
                throw new InvalidDataException("Transfermarkt player IDs must be positive when present.");
            }

            if (member.Role == BundesligaRosterRole.Coach
                && (member.TransfermarktPlayerId is not null
                    || member.Age is not null
                    || member.Position is not null
                    || member.MarketValueEur is not null))
            {
                throw new InvalidDataException("Coach supplemental fields and player ID must be absent.");
            }

            if (member.Role == BundesligaRosterRole.Player && member.Age is <= 0)
            {
                throw new InvalidDataException("Player age must be positive when present; use null for N/A.");
            }

            if (member.MarketValueEur is <= 0)
            {
                throw new InvalidDataException("Market value must be positive when present; use null for N/A.");
            }
        }

        var duplicateName = snapshot.Members
            .GroupBy(member => BundesligaRosterSeed.NormalizeName(member.Name), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new InvalidDataException(
                $"Roster '{snapshot.Team.TeamSlug}' contains duplicate member name '{duplicateName.Key}'.");
        }

        var duplicateId = players
            .Where(player => player.TransfermarktPlayerId is not null)
            .GroupBy(player => player.TransfermarktPlayerId!.Value)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidDataException(
                $"Roster '{snapshot.Team.TeamSlug}' contains duplicate Transfermarkt player ID {duplicateId.Key}.");
        }
    }

    private static void ValidateQualityReportRow(BundesligaRosterQualityReportRow row)
    {
        ArgumentNullException.ThrowIfNull(row.Team);
        ArgumentNullException.ThrowIfNull(row.SourceReferences);
        ArgumentNullException.ThrowIfNull(row.Diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.SelectionReason);
        if (!MachineCodePattern.IsMatch(row.SelectionReason)
            || row.Diagnostics.Any(diagnostic => !MachineCodePattern.IsMatch(diagnostic)))
        {
            throw new InvalidDataException("Quality-report reasons and diagnostics must be stable machine codes.");
        }

        if (row.SourceReferences.Count == 0
            || row.SourceReferences.Any(reference => !string.Equals(reference.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Quality-report source references must contain at least one HTTPS URI.");
        }

        if (row.PlayerCount is < BundesligaRosterPolicy.MinimumPlayerCount or > BundesligaRosterPolicy.MaximumPlayerCount
            || row.CoachCount != 1)
        {
            throw new InvalidDataException("Quality-report roster counts are outside the contract.");
        }

        if (row.SelectedSource == BundesligaRosterMembershipSource.DuckDb
            && (string.IsNullOrWhiteSpace(row.SourceRevision)
                || row.DuckDbSnapshotAsOf is null
                || row.DuckDbGateResult != BundesligaRosterDuckDbGateResult.Pass))
        {
            throw new InvalidDataException(
                "A selected DuckDB quality-report row requires revision, snapshot date, and PASS gate result.");
        }

        if (row.SelectedSource == BundesligaRosterMembershipSource.LastKnownGood
            && string.IsNullOrWhiteSpace(row.LastKnownGoodSnapshotId))
        {
            throw new InvalidDataException("A last-known-good quality-report row requires its snapshot ID.");
        }

        foreach (var count in new[]
                 {
                     row.StablePlayerIdCount,
                     row.KnownAgeCount,
                     row.KnownPositionCount,
                     row.ValuedPlayerCount
                 })
        {
            if (count < 0 || count > row.PlayerCount)
            {
                throw new InvalidDataException("Quality-report coverage counts must be between zero and Player_Count.");
            }
        }
    }

    private static IReadOnlyList<BundesligaRosterMember> OrderMembers(IEnumerable<BundesligaRosterMember> members)
    {
        return members
            .OrderBy(member => member.Role == BundesligaRosterRole.Coach ? 0 : 1)
            .ThenBy(member => BundesligaRosterSeed.NormalizeName(member.Name), StringComparer.Ordinal)
            .ThenBy(member => member.TransfermarktPlayerId ?? 0)
            .ToArray();
    }

    private static string WriteCsv(IReadOnlyList<string> headers, Action<CsvWriter> writeRows)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\r\n"
        };
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var csv = new CsvWriter(writer, configuration);
        foreach (var header in headers)
        {
            csv.WriteField(header);
        }

        csv.NextRecord();
        writeRows(csv);
        csv.Flush();
        return writer.ToString();
    }

    private static long Median(IReadOnlyList<long> sortedValues)
    {
        var middle = sortedValues.Count / 2;
        if (sortedValues.Count % 2 != 0)
        {
            return sortedValues[middle];
        }

        return checked((long)decimal.Round(
            ((decimal)sortedValues[middle - 1] + sortedValues[middle]) / 2,
            0,
            MidpointRounding.AwayFromZero));
    }

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatMoney(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("de-DE"));

    private static string ValueOrMissing(string? value) => string.IsNullOrWhiteSpace(value) ? MissingValue : value;

    private static string FormatSource(BundesligaRosterMembershipSource source) => source switch
    {
        BundesligaRosterMembershipSource.DuckDb => "DuckDB",
        BundesligaRosterMembershipSource.FallbackSeed => "FallbackSeed",
        BundesligaRosterMembershipSource.LastKnownGood => "LastKnownGood",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };

    private static string FormatGateResult(BundesligaRosterDuckDbGateResult result) => result switch
    {
        BundesligaRosterDuckDbGateResult.Pass => "PASS",
        BundesligaRosterDuckDbGateResult.Rejected => "REJECTED",
        BundesligaRosterDuckDbGateResult.NotAvailable => "NOT_AVAILABLE",
        BundesligaRosterDuckDbGateResult.NotEvaluated => "NOT_EVALUATED",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
    };
}
