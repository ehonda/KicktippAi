using System.Globalization;
using System.Reflection;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaHistoryPlayedDateMapEntry(
    string DocumentName,
    int RowOrdinal,
    string HistoryCompetition,
    string HomeTeam,
    string AwayTeam,
    string Score,
    string Annotation,
    string PlayedAt,
    string SourceClass,
    string SourceName,
    string SourceUrl,
    string SourceRevision,
    string SourceMatchId,
    string VerifiedAt);

public sealed class BundesligaHistoryPlayedDateMap
{
    public const string RelativePath = "data/bundesliga-2026-27/history/history-played-dates.csv";
    public const string TransfermarktDatasetRevision = "154367dfa6d6eb0b86332e332f9df0a080c7ddce";
    public const string TransfermarktDatasetSourceClass = "revision-pinned-dataset";
    public const string TransfermarktDatasetSourceName = "transfermarkt-datasets";
    public const string OpenLigaDbSourceClass = "captured-odbl-response";
    public const string OpenLigaDbSourceName = "OpenLigaDB";
    public const string OpenLigaDbLeagueUrl = "https://api.openligadb.de/getmatchdata/bl2/2025";
    public const string OpenLigaDbLeagueRevision = "83dbea21fe56c30ed2393dd888efede627cbdf7b26c5694f14753cf792af6a84";
    public const string OpenLigaDbRelegationUrl = "https://api.openligadb.de/getmatchdata/rel/2025";
    public const string OpenLigaDbRelegationRevision = "0cbe277ed6539364eb4f9f2122e4af33e2e10a5797e159382b27908a74e08d8e";
    public const string OpenLigaDbDfbPokalUrl = "https://api.openligadb.de/getmatchdata/dfb/2025";
    public const string OpenLigaDbDfbPokalRevision = "9d16d5d30e5882c592ec4d8b39b592ea0f102c2e2695da98897f76a87b6ec2a3";
    public const string OpenLigaDbDfbPokalFinalMatchId = "81581";
    public const string OpenLigaDbDfbPokal2026Url = "https://api.openligadb.de/getmatchdata/dfb/2026";
    public const string OpenLigaDbDfbPokal2026Revision = "b60d4c1ef214ffa2680efb27cace33cc7b47bf9700b4f57e7043736919a8eeab";
    public const string OpenLigaDbDfbPokal2026MatchId = "81832";
    public const string UefaSourceClass = "official-match-record";
    public const string UefaSourceName = "UEFA";
    public const string UefaFinalUrl = "https://www.uefa.com/uefaeuropaleague/match/2047743/";
    public const string UefaFinalRevision = "UEFA-match-2047743";
    public const string UefaFinalMatchId = "2047743";

    private const string ResourceName = "EHonda.KicktippAi.Core.Data.Bundesliga2026_27HistoryPlayedDates.csv";
    private static readonly string[] ExpectedHeaders =
    [
        "Document_Name", "Row_Ordinal", "History_Competition", "Home_Team", "Away_Team", "Score", "Annotation",
        "Played_At", "Source_Class", "Source_Name", "Source_Url", "Source_Revision", "Source_Match_Id", "Verified_At"
    ];

    private static readonly Lazy<BundesligaHistoryPlayedDateMap> DefaultMap = new(LoadEmbedded);
    private static readonly DateTimeOffset OpenLigaDbDfbPokal2026EvidenceAvailableAt =
        new(2026, 8, 21, 19, 57, 23, TimeSpan.FromHours(2));
    private static readonly string[] SelectedDocumentPrefixes = ["away-history-", "home-history-", "recent-history-"];
    private static readonly HashSet<string> TransfermarktCompetitions =
        new(["1.BL", "DFB", "CL", "EL", "ConfL"], StringComparer.Ordinal);

    private BundesligaHistoryPlayedDateMap(IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> entries) => Entries = entries;

    public static BundesligaHistoryPlayedDateMap Default => DefaultMap.Value;

    public IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> Entries { get; }

    public static IReadOnlyList<string> ExpectedDocumentNames { get; } = BundesligaTeamManifest.Default.Entries
        .SelectMany(team => SelectedDocumentPrefixes.Select(prefix => $"{prefix}{team.TeamSlug}.csv"))
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static BundesligaHistoryPlayedDateMap Parse(TextReader reader, string sourceName = RelativePath) =>
        ParseCore(reader, sourceName, requireCompleteDocumentSet: true);

    /// <summary>
    /// Parses a deliberately partial map for focused source-contract validation. Production callers must use
    /// <see cref="Parse(TextReader, string)"/>, which requires the complete preseason document set.
    /// </summary>
    public static BundesligaHistoryPlayedDateMap ParseFragment(TextReader reader, string sourceName) =>
        ParseCore(reader, sourceName, requireCompleteDocumentSet: false);

    private static BundesligaHistoryPlayedDateMap ParseCore(
        TextReader reader,
        string sourceName,
        bool requireCompleteDocumentSet)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        try
        {
            using var csv = new CsvReader(reader, configuration);
            if (!csv.Read() || !csv.ReadHeader())
            {
                throw Invalid(sourceName, "a header row is required");
            }

            if (!(csv.HeaderRecord ?? []).SequenceEqual(ExpectedHeaders, StringComparer.Ordinal))
            {
                throw Invalid(sourceName, $"headers must be exactly: {string.Join(',', ExpectedHeaders)}");
            }

            var entries = new List<BundesligaHistoryPlayedDateMapEntry>();
            while (csv.Read())
            {
                if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) == true)
                {
                    continue;
                }

                entries.Add(ParseEntry(csv, sourceName));
            }

            Validate(entries, sourceName, requireCompleteDocumentSet);
            return new BundesligaHistoryPlayedDateMap(entries.AsReadOnly());
        }
        catch (CsvHelperException exception)
        {
            throw Invalid(sourceName, $"CSV parsing failed at row {exception.Context?.Parser?.Row ?? -1}: {exception.Message}", exception);
        }
    }

    public static string Write(IEnumerable<BundesligaHistoryPlayedDateMapEntry> entries)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var header in ExpectedHeaders)
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        foreach (var entry in entries.OrderBy(entry => entry.DocumentName, StringComparer.Ordinal).ThenBy(entry => entry.RowOrdinal))
        {
            csv.WriteField(entry.DocumentName);
            csv.WriteField(entry.RowOrdinal);
            csv.WriteField(entry.HistoryCompetition);
            csv.WriteField(entry.HomeTeam);
            csv.WriteField(entry.AwayTeam);
            csv.WriteField(entry.Score);
            csv.WriteField(entry.Annotation);
            csv.WriteField(entry.PlayedAt);
            csv.WriteField(entry.SourceClass);
            csv.WriteField(entry.SourceName);
            csv.WriteField(entry.SourceUrl);
            csv.WriteField(entry.SourceRevision);
            csv.WriteField(entry.SourceMatchId);
            csv.WriteField(entry.VerifiedAt);
            csv.NextRecord();
        }

        return writer.ToString();
    }

    private static BundesligaHistoryPlayedDateMapEntry ParseEntry(CsvReader csv, string sourceName)
    {
        var row = csv.Parser.Row;
        var ordinalText = Required(csv, "Row_Ordinal", sourceName, row);
        if (!int.TryParse(ordinalText, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) || ordinal <= 0)
        {
            throw Invalid(sourceName, $"row {row} has invalid Row_Ordinal '{ordinalText}'");
        }

        var entry = new BundesligaHistoryPlayedDateMapEntry(
            Required(csv, "Document_Name", sourceName, row),
            ordinal,
            Required(csv, "History_Competition", sourceName, row),
            Required(csv, "Home_Team", sourceName, row),
            Required(csv, "Away_Team", sourceName, row),
            Required(csv, "Score", sourceName, row),
            csv.GetField("Annotation")?.Trim() ?? string.Empty,
            Required(csv, "Played_At", sourceName, row),
            Required(csv, "Source_Class", sourceName, row),
            Required(csv, "Source_Name", sourceName, row),
            Required(csv, "Source_Url", sourceName, row),
            Required(csv, "Source_Revision", sourceName, row),
            Required(csv, "Source_Match_Id", sourceName, row),
            Required(csv, "Verified_At", sourceName, row));

        ValidateEntry(entry, sourceName, row);
        return entry;
    }

    private static void ValidateEntry(BundesligaHistoryPlayedDateMapEntry entry, string sourceName, int row)
    {
        BundesligaHistoryPlayedDateCollector.ValidateSelectedDocumentName(entry.DocumentName);
        if (!DateOnly.TryParseExact(entry.PlayedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw Invalid(sourceName, $"row {row} has invalid Played_At '{entry.PlayedAt}'");
        }
        if (!DateTimeOffset.TryParse(entry.VerifiedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var verifiedAt))
        {
            throw Invalid(sourceName, $"row {row} has invalid Verified_At '{entry.VerifiedAt}'");
        }
        if (verifiedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw Invalid(sourceName, $"row {row} has unreasonably future Verified_At '{entry.VerifiedAt}'");
        }
        if (!Uri.TryCreate(entry.SourceUrl, UriKind.Absolute, out var sourceUrl) || sourceUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw Invalid(sourceName, $"row {row} requires an absolute HTTPS Source_Url");
        }
        if (!IsNormalizedScore(entry.Score))
        {
            throw Invalid(sourceName, $"row {row} has invalid normalized Score '{entry.Score}'");
        }
        var isSecondBundesliga = string.Equals(entry.HistoryCompetition, "2.BL", StringComparison.Ordinal);
        var isRelegation = string.Equals(entry.HistoryCompetition, "Releg", StringComparison.Ordinal);
        var isDfbPokalCapture = string.Equals(entry.HistoryCompetition, "DFB", StringComparison.Ordinal)
                                && string.Equals(entry.SourceName, OpenLigaDbSourceName, StringComparison.Ordinal);
        var isUefaFinal = string.Equals(entry.HistoryCompetition, "EL", StringComparison.Ordinal)
                          && string.Equals(entry.SourceName, UefaSourceName, StringComparison.Ordinal);
        var isAcceptedDfbPokalCapture = isDfbPokalCapture
            && ((string.Equals(entry.SourceRevision, OpenLigaDbDfbPokalRevision, StringComparison.Ordinal)
                && string.Equals(entry.SourceUrl, OpenLigaDbDfbPokalUrl, StringComparison.Ordinal)
                && string.Equals(entry.SourceMatchId, OpenLigaDbDfbPokalFinalMatchId, StringComparison.Ordinal)
                && string.Equals(entry.HomeTeam, "FC Bayern München", StringComparison.Ordinal)
                && string.Equals(entry.AwayTeam, "VfB Stuttgart", StringComparison.Ordinal)
                && string.Equals(entry.Score, "3:0", StringComparison.Ordinal)
                && string.Equals(entry.PlayedAt, "2026-05-23", StringComparison.Ordinal))
                || (string.Equals(entry.SourceRevision, OpenLigaDbDfbPokal2026Revision, StringComparison.Ordinal)
                && string.Equals(entry.SourceUrl, OpenLigaDbDfbPokal2026Url, StringComparison.Ordinal)
                && string.Equals(entry.SourceMatchId, OpenLigaDbDfbPokal2026MatchId, StringComparison.Ordinal)
                && string.Equals(entry.HomeTeam, "SC St. Tönis", StringComparison.Ordinal)
                && string.Equals(entry.AwayTeam, "Eintracht Frankfurt", StringComparison.Ordinal)
                && string.Equals(entry.Score, "0:11", StringComparison.Ordinal)
                && string.Equals(entry.PlayedAt, "2026-08-21", StringComparison.Ordinal)
                && verifiedAt >= OpenLigaDbDfbPokal2026EvidenceAvailableAt));
        var validSource = isSecondBundesliga
            ? string.Equals(entry.SourceClass, OpenLigaDbSourceClass, StringComparison.Ordinal)
              && string.Equals(entry.SourceName, OpenLigaDbSourceName, StringComparison.Ordinal)
              && string.Equals(entry.SourceRevision, OpenLigaDbLeagueRevision, StringComparison.Ordinal)
              && string.Equals(entry.SourceUrl, OpenLigaDbLeagueUrl, StringComparison.Ordinal)
            : isRelegation
              ? string.Equals(entry.SourceClass, OpenLigaDbSourceClass, StringComparison.Ordinal)
                && string.Equals(entry.SourceName, OpenLigaDbSourceName, StringComparison.Ordinal)
                && string.Equals(entry.SourceRevision, OpenLigaDbRelegationRevision, StringComparison.Ordinal)
                && string.Equals(entry.SourceUrl, OpenLigaDbRelegationUrl, StringComparison.Ordinal)
            : isDfbPokalCapture
              ? string.Equals(entry.SourceClass, OpenLigaDbSourceClass, StringComparison.Ordinal)
                && isAcceptedDfbPokalCapture
            : isUefaFinal
              ? string.Equals(entry.SourceClass, UefaSourceClass, StringComparison.Ordinal)
                && string.Equals(entry.SourceRevision, UefaFinalRevision, StringComparison.Ordinal)
                && string.Equals(entry.SourceUrl, UefaFinalUrl, StringComparison.Ordinal)
                && string.Equals(entry.SourceMatchId, UefaFinalMatchId, StringComparison.Ordinal)
                && string.Equals(entry.HomeTeam, "SC Freiburg", StringComparison.Ordinal)
                && string.Equals(entry.AwayTeam, "Aston Villa", StringComparison.Ordinal)
                && string.Equals(entry.Score, "0:3", StringComparison.Ordinal)
                && string.Equals(entry.PlayedAt, "2026-05-20", StringComparison.Ordinal)
            : TransfermarktCompetitions.Contains(entry.HistoryCompetition)
              && string.Equals(entry.SourceName, TransfermarktDatasetSourceName, StringComparison.Ordinal)
              && string.Equals(entry.SourceClass, TransfermarktDatasetSourceClass, StringComparison.Ordinal)
              && string.Equals(entry.SourceRevision, TransfermarktDatasetRevision, StringComparison.Ordinal)
              && entry.SourceMatchId.Length > 0
              && entry.SourceMatchId.All(char.IsAsciiDigit)
              && string.Equals(sourceUrl.Host, "www.transfermarkt.co.uk", StringComparison.Ordinal)
              && string.Equals(sourceUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
                  {
                      [.., "spielbericht", var sourceMatchId] => sourceMatchId,
                      _ => null
                  }, entry.SourceMatchId, StringComparison.Ordinal)
              && string.IsNullOrEmpty(sourceUrl.Query)
              && string.IsNullOrEmpty(sourceUrl.Fragment);
        if (!validSource)
        {
            throw Invalid(sourceName, $"row {row} does not identify the accepted fixed source for '{entry.HistoryCompetition}'");
        }
    }

    private static void Validate(
        IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> entries,
        string sourceName,
        bool requireCompleteDocumentSet)
    {
        var duplicateOrdinal = entries.GroupBy(entry => (entry.DocumentName, entry.RowOrdinal)).FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrdinal is not null)
        {
            throw Invalid(sourceName, $"duplicate document ordinal {duplicateOrdinal.Key}");
        }

        var duplicateSource = entries.GroupBy(entry => (entry.DocumentName, entry.SourceMatchId)).FirstOrDefault(group => group.Count() > 1);
        if (duplicateSource is not null)
        {
            throw Invalid(sourceName, $"source match '{duplicateSource.Key.SourceMatchId}' occurs more than once in {duplicateSource.Key.DocumentName}");
        }

        var duplicateIdentity = entries.GroupBy(entry => (
                entry.DocumentName,
                entry.HistoryCompetition,
                entry.HomeTeam,
                entry.AwayTeam,
                entry.Score,
                entry.Annotation))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateIdentity is not null)
        {
            throw Invalid(sourceName, $"exact fixed-map identity occurs more than once in {duplicateIdentity.Key.DocumentName}");
        }

        var nonContiguous = entries
            .GroupBy(entry => entry.DocumentName, StringComparer.Ordinal)
            .FirstOrDefault(group => !group
                .OrderBy(entry => entry.RowOrdinal)
                .Select(entry => entry.RowOrdinal)
                .SequenceEqual(Enumerable.Range(1, group.Count())));
        if (nonContiguous is not null)
        {
            throw Invalid(sourceName, $"completed row ordinals in {nonContiguous.Key} must be contiguous from 1");
        }

        if (requireCompleteDocumentSet)
        {
            var actualDocumentNames = entries.Select(entry => entry.DocumentName).ToHashSet(StringComparer.Ordinal);
            var expectedDocumentNames = ExpectedDocumentNames.ToHashSet(StringComparer.Ordinal);
            if (!actualDocumentNames.SetEquals(expectedDocumentNames))
            {
                var missing = expectedDocumentNames.Except(actualDocumentNames, StringComparer.Ordinal).Order(StringComparer.Ordinal);
                var unexpected = actualDocumentNames.Except(expectedDocumentNames, StringComparer.Ordinal).Order(StringComparer.Ordinal);
                throw Invalid(sourceName,
                    $"document set must exactly match all {ExpectedDocumentNames.Count} preseason selected-history documents; " +
                    $"missing=[{string.Join(',', missing)}], unexpected=[{string.Join(',', unexpected)}]");
            }
        }

        var uefaRows = entries.Where(entry => string.Equals(entry.SourceName, UefaSourceName, StringComparison.Ordinal)).ToArray();
        if (uefaRows.Length is not 0 and not 2
            || uefaRows.Length == 2 && !uefaRows.Select(entry => entry.DocumentName).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["home-history-scf.csv", "recent-history-scf.csv"]))
        {
            throw Invalid(sourceName, "UEFA match 2047743 must be used exactly once in each of home-history-scf.csv and recent-history-scf.csv");
        }

        var openLigaDfbFinalRows = entries.Where(entry =>
            string.Equals(entry.HistoryCompetition, "DFB", StringComparison.Ordinal)
            && string.Equals(entry.SourceName, OpenLigaDbSourceName, StringComparison.Ordinal)
            && string.Equals(entry.SourceMatchId, OpenLigaDbDfbPokalFinalMatchId, StringComparison.Ordinal)).ToArray();
        if (openLigaDfbFinalRows.Length is not 0 and not 4
            || openLigaDfbFinalRows.Length == 4 && !openLigaDfbFinalRows.Select(entry => entry.DocumentName).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["away-history-vfb.csv", "home-history-fcb.csv", "recent-history-fcb.csv", "recent-history-vfb.csv"]))
        {
            throw Invalid(sourceName, "OpenLigaDB DFB-Pokal final 81581 must be used exactly once in each of the four accepted inventory documents");
        }
        var openLigaDfb2026Rows = entries.Where(entry =>
            string.Equals(entry.HistoryCompetition, "DFB", StringComparison.Ordinal)
            && string.Equals(entry.SourceName, OpenLigaDbSourceName, StringComparison.Ordinal)
            && string.Equals(entry.SourceMatchId, OpenLigaDbDfbPokal2026MatchId, StringComparison.Ordinal)).ToArray();
        if (openLigaDfb2026Rows.Length is not 0 and not 2
            || openLigaDfb2026Rows.Length == 2 && !openLigaDfb2026Rows.Select(entry => entry.DocumentName).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["away-history-sge.csv", "recent-history-sge.csv"]))
        {
            throw Invalid(sourceName, "OpenLigaDB DFB-Pokal match 81832 must be used exactly once in each of away-history-sge.csv and recent-history-sge.csv");
        }

        var ordered = entries.OrderBy(entry => entry.DocumentName, StringComparer.Ordinal).ThenBy(entry => entry.RowOrdinal).ToArray();
        if (!entries.SequenceEqual(ordered))
        {
            throw Invalid(sourceName, "rows must be ordered by Document_Name and Row_Ordinal using ordinal comparison");
        }
    }

    private static string Required(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = csv.GetField(fieldName)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? throw Invalid(sourceName, $"row {row} requires {fieldName}") : value;
    }

    private static bool IsNormalizedScore(string score)
    {
        var parts = score.Split(':', StringSplitOptions.None);
        return parts.Length == 2
               && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var home)
               && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var away)
               && home >= 0
               && away >= 0
               && string.Equals(score, $"{home}:{away}", StringComparison.Ordinal);
    }

    private static BundesligaHistoryPlayedDateMap LoadEmbedded()
    {
        using var stream = typeof(BundesligaHistoryPlayedDateMap).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Bundesliga history date map '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    private static InvalidDataException Invalid(string sourceName, string message, Exception? inner = null) =>
        new($"Invalid Bundesliga history played-date map '{sourceName}': {message}.", inner);
}
