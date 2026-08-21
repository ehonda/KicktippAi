using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using NodaTime;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaHistoryDocument(string Name, string Content);

public enum BundesligaHistoryPlayedDateSourceClass
{
    ExistingPlayedAt,
    KicktippOutcome,
    FixedExternalMap
}

public sealed record BundesligaHistoryPlayedDateResolution(
    string DocumentName,
    int RowOrdinal,
    string PlayedAt,
    BundesligaHistoryPlayedDateSourceClass SourceClass,
    string SourceIdentity);

public sealed record BundesligaHistoryPlayedDateDiagnostic(string DocumentName, int? RowOrdinal, string Message);

public sealed record BundesligaHistoryPlayedDateCollectionResult(
    bool Succeeded,
    IReadOnlyList<BundesligaHistoryDocument> Documents,
    IReadOnlyList<BundesligaHistoryPlayedDateResolution> Resolutions,
    IReadOnlyList<BundesligaHistoryPlayedDateDiagnostic> Diagnostics,
    int ExcludedIncompleteRowCount = 0)
{
    public int PreservedCount => Resolutions.Count(value => value.SourceClass == BundesligaHistoryPlayedDateSourceClass.ExistingPlayedAt);
    public int KicktippCount => Resolutions.Count(value => value.SourceClass == BundesligaHistoryPlayedDateSourceClass.KicktippOutcome);
    public int FixedMapCount => Resolutions.Count(value => value.SourceClass == BundesligaHistoryPlayedDateSourceClass.FixedExternalMap);
}

public interface IBundesligaHistoryPlayedDateCollector
{
    BundesligaHistoryPlayedDateCollectionResult Collect(
        string competition,
        IReadOnlyList<BundesligaHistoryDocument> documents,
        IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> dateMap,
        IReadOnlyList<PersistedMatchOutcome> matchOutcomes);
}

public sealed class BundesligaHistoryPlayedDateCollector : IBundesligaHistoryPlayedDateCollector
{
    private const string Competition = CompetitionIds.Bundesliga2026_27;
    private const string KicktippHistoryCompetition = "1.BL";
    private static readonly DateTimeZone Berlin = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
    private static readonly string[] UndatedHeaders = ["Competition", "Home_Team", "Away_Team", "Score", "Annotation"];
    private static readonly string[] LegacyHeaders = ["Competition", "Data_Collected_At", "Home_Team", "Away_Team", "Score", "Annotation"];
    private static readonly string[] DatedHeaders = ["Competition", "Played_At", "Home_Team", "Away_Team", "Score", "Annotation"];
    private static readonly string[] TimestampFormats = ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    public BundesligaHistoryPlayedDateCollectionResult Collect(
        string competition,
        IReadOnlyList<BundesligaHistoryDocument> documents,
        IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> dateMap,
        IReadOnlyList<PersistedMatchOutcome> matchOutcomes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(dateMap);
        ArgumentNullException.ThrowIfNull(matchOutcomes);
        if (!string.Equals(competition, Competition, StringComparison.Ordinal))
        {
            return Failure(documents, string.Empty, null, $"Expected competition '{Competition}' but received '{competition}'");
        }
        if (matchOutcomes.Any(outcome => !string.Equals(outcome.Competition, Competition, StringComparison.Ordinal)))
        {
            return Failure(documents, string.Empty, null, "Match outcomes contain a different competition partition");
        }

        var selectedNames = documents.Where(document => IsSelectedDocumentName(document.Name)).Select(document => document.Name).ToArray();
        if (selectedNames.Distinct(StringComparer.Ordinal).Count() != selectedNames.Length)
        {
            return Failure(documents, string.Empty, null, "Selected document names must be unique");
        }

        var diagnostics = new List<BundesligaHistoryPlayedDateDiagnostic>();
        var resolutions = new List<BundesligaHistoryPlayedDateResolution>();
        var rendered = new List<BundesligaHistoryDocument>(documents.Count);
        var excludedIncompleteRowCount = 0;
        var mapByOrdinal = dateMap.GroupBy(entry => (entry.DocumentName, entry.RowOrdinal)).ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var document in documents)
        {
            if (!IsSelectedDocumentName(document.Name))
            {
                rendered.Add(document);
                continue;
            }

            try
            {
                ValidateSelectedDocumentName(document.Name);
                var parsedRows = ParseRows(document, allowMissingScore: true);
                var incompleteRows = parsedRows.Where(row => string.IsNullOrWhiteSpace(row.Score)).ToArray();
                var rows = parsedRows.Where(row => !string.IsNullOrWhiteSpace(row.Score))
                    .Select((row, index) => row with { Ordinal = index + 1 }).ToArray();
                excludedIncompleteRowCount += incompleteRows.Length;
                var duplicateRows = rows.GroupBy(RowIdentity, StringComparer.Ordinal).Where(group => group.Count() > 1).ToArray();
                foreach (var duplicate in duplicateRows)
                {
                    diagnostics.Add(new(document.Name, null, $"Ambiguous duplicate row identity '{duplicate.Key}'"));
                }

                var resolvedRows = new List<HistoryRow>(rows.Length);
                foreach (var row in rows)
                {
                    var resolution = Resolve(document.Name, row, mapByOrdinal, matchOutcomes, diagnostics);
                    if (resolution is null)
                    {
                        resolvedRows.Add(row);
                        continue;
                    }

                    resolutions.Add(resolution);
                    resolvedRows.Add(row with { PlayedAt = resolution.PlayedAt });
                }

                rendered.Add(new(document.Name, Render(resolvedRows)));
            }
            catch (Exception exception) when (exception is InvalidDataException or KeyNotFoundException)
            {
                diagnostics.Add(new(document.Name, null, exception.Message));
                rendered.Add(document);
            }
        }

        var unusedMapEntries = dateMap.Where(entry => selectedNames.Contains(entry.DocumentName, StringComparer.Ordinal)
            && !resolutions.Any(resolution => resolution.DocumentName == entry.DocumentName && resolution.RowOrdinal == entry.RowOrdinal)).ToArray();
        foreach (var entry in unusedMapEntries)
        {
            diagnostics.Add(new(entry.DocumentName, entry.RowOrdinal, "Map entry did not resolve the exact document row"));
        }

        if (diagnostics.Count > 0)
        {
            return new(false, documents.ToArray(), Array.Empty<BundesligaHistoryPlayedDateResolution>(), diagnostics.AsReadOnly(), excludedIncompleteRowCount);
        }

        return new(true, rendered.AsReadOnly(), resolutions.AsReadOnly(), Array.Empty<BundesligaHistoryPlayedDateDiagnostic>(), excludedIncompleteRowCount);
    }

    public static bool IsSelectedDocumentName(string documentName) =>
        (documentName.StartsWith("recent-history-", StringComparison.Ordinal)
         || documentName.StartsWith("home-history-", StringComparison.Ordinal)
         || documentName.StartsWith("away-history-", StringComparison.Ordinal))
        && documentName.EndsWith(".csv", StringComparison.Ordinal);

    public static void ValidateSelectedDocumentName(string documentName)
    {
        if (!IsSelectedDocumentName(documentName))
        {
            throw new InvalidDataException($"Unsupported Bundesliga history document '{documentName}'");
        }

        var prefix = documentName.StartsWith("recent-history-", StringComparison.Ordinal) ? "recent-history-"
            : documentName.StartsWith("home-history-", StringComparison.Ordinal) ? "home-history-" : "away-history-";
        var slug = documentName[prefix.Length..^4];
        _ = BundesligaTeamManifest.Default.GetByTeamSlug(slug);
    }

    public static BundesligaHistoryPlayedDateInventory ExportInventory(
        IReadOnlyList<BundesligaHistoryDocument> documents)
    {
        var entries = new List<BundesligaHistoryPlayedDateMapEntry>();
        var excludedIncompleteRowCount = 0;
        foreach (var document in documents.Where(document => IsSelectedDocumentName(document.Name)).OrderBy(document => document.Name, StringComparer.Ordinal))
        {
            ValidateSelectedDocumentName(document.Name);
            var completedOrdinal = 0;
            foreach (var row in ParseRows(document, allowMissingScore: true))
            {
                if (string.IsNullOrWhiteSpace(row.Score))
                {
                    excludedIncompleteRowCount++;
                    continue;
                }
                completedOrdinal++;
                entries.Add(new(document.Name, completedOrdinal, row.HistoryCompetition, row.HomeTeam, row.AwayTeam, row.Score,
                    row.Annotation, row.PlayedAt, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
            }
        }
        return new(entries.AsReadOnly(), excludedIncompleteRowCount);
    }

    private static BundesligaHistoryPlayedDateResolution? Resolve(
        string documentName,
        HistoryRow row,
        IReadOnlyDictionary<(string DocumentName, int RowOrdinal), BundesligaHistoryPlayedDateMapEntry[]> map,
        IReadOnlyList<PersistedMatchOutcome> outcomes,
        List<BundesligaHistoryPlayedDateDiagnostic> diagnostics)
    {
        var normalizedScore = NormalizeScore(row.Score);
        var outcomeMatches = string.Equals(row.HistoryCompetition, KicktippHistoryCompetition, StringComparison.Ordinal)
            ? outcomes.Where(outcome => outcome.HasOutcome
                && string.Equals(outcome.HomeTeam, row.HomeTeam, StringComparison.Ordinal)
                && string.Equals(outcome.AwayTeam, row.AwayTeam, StringComparison.Ordinal)
                && string.Equals($"{outcome.HomeGoals}:{outcome.AwayGoals}", normalizedScore, StringComparison.Ordinal)).ToArray()
            : [];
        if (outcomeMatches.Length > 1)
        {
            diagnostics.Add(new(documentName, row.Ordinal, "Multiple competition-scoped Kicktipp outcomes match this row"));
            return null;
        }

        map.TryGetValue((documentName, row.Ordinal), out var mapMatches);
        mapMatches ??= [];
        var exactMapMatches = mapMatches.Where(entry => MapIdentityMatches(entry, row)).ToArray();
        if (mapMatches.Length != exactMapMatches.Length || exactMapMatches.Length > 1)
        {
            diagnostics.Add(new(documentName, row.Ordinal, "Map ordinal is duplicate or does not match the exact row identity"));
            return null;
        }

        var outcomeDate = outcomeMatches.Length == 1
            ? outcomeMatches[0].StartsAt.WithZone(Berlin).Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
        var mapDate = exactMapMatches.SingleOrDefault()?.PlayedAt;
        if (IsExactPlayedAt(row.PlayedAt))
        {
            if (outcomeDate is not null && !SameCalendarDate(row.PlayedAt, outcomeDate)
                || mapDate is not null && !SameCalendarDate(row.PlayedAt, mapDate))
            {
                diagnostics.Add(new(documentName, row.Ordinal,
                    $"Existing Played_At '{row.PlayedAt}' conflicts with exact source evidence"));
                return null;
            }
            return new(documentName, row.Ordinal, row.PlayedAt,
                BundesligaHistoryPlayedDateSourceClass.ExistingPlayedAt, $"{documentName}#{row.Ordinal}");
        }
        if (outcomeDate is not null && mapDate is not null && !string.Equals(outcomeDate, mapDate, StringComparison.Ordinal))
        {
            diagnostics.Add(new(documentName, row.Ordinal, $"Conflicting Kicktipp '{outcomeDate}' and map '{mapDate}' evidence"));
            return null;
        }
        if (outcomeDate is not null)
        {
            var outcome = outcomeMatches[0];
            return new(documentName, row.Ordinal, outcomeDate, BundesligaHistoryPlayedDateSourceClass.KicktippOutcome,
                $"matchday={outcome.Matchday};tippSpielId={outcome.TippSpielId ?? "N/A"}");
        }
        if (mapDate is not null)
        {
            var entry = exactMapMatches[0];
            return new(documentName, row.Ordinal, mapDate, BundesligaHistoryPlayedDateSourceClass.FixedExternalMap,
                $"{entry.SourceName}@{entry.SourceRevision}:{entry.SourceMatchId}");
        }

        diagnostics.Add(new(documentName, row.Ordinal,
            $"No exact played-date source for {row.HistoryCompetition} | {row.HomeTeam} vs {row.AwayTeam} | {row.Score} | {row.Annotation}"));
        return null;
    }

    private static List<HistoryRow> ParseRows(BundesligaHistoryDocument document, bool allowMissingScore = false)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null, HeaderValidated = null, MissingFieldFound = null, TrimOptions = TrimOptions.None
        };
        try
        {
            using var reader = new StringReader(document.Content);
            using var csv = new CsvReader(reader, configuration);
            if (!csv.Read() || !csv.ReadHeader()) throw new InvalidDataException("Header row is required");
            var headers = csv.HeaderRecord ?? [];
            var hasPlayedAt = headers.SequenceEqual(DatedHeaders, StringComparer.Ordinal);
            if (!hasPlayedAt && !headers.SequenceEqual(UndatedHeaders, StringComparer.Ordinal)
                && !headers.SequenceEqual(LegacyHeaders, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Unexpected headers in {document.Name}: {string.Join(',', headers)}");
            }

            var rows = new List<HistoryRow>();
            while (csv.Read())
            {
                if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) == true) continue;
                var ordinal = rows.Count + 1;
                var score = csv.GetField("Score")?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(score) && !allowMissingScore)
                    throw new InvalidDataException($"{document.Name} row {ordinal} requires Score");
                rows.Add(new(ordinal, Required(csv, "Competition", document.Name, ordinal),
                    hasPlayedAt ? csv.GetField("Played_At")?.Trim() ?? string.Empty : string.Empty,
                    Required(csv, "Home_Team", document.Name, ordinal), Required(csv, "Away_Team", document.Name, ordinal),
                    string.IsNullOrWhiteSpace(score) ? string.Empty : NormalizeScore(score), csv.GetField("Annotation")?.Trim() ?? string.Empty));
            }
            return rows;
        }
        catch (CsvHelperException exception)
        {
            throw new InvalidDataException($"Invalid history CSV '{document.Name}': {exception.Message}", exception);
        }
    }

    private static string Render(IReadOnlyList<HistoryRow> rows)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var header in DatedHeaders) csv.WriteField(header);
        csv.NextRecord();
        foreach (var row in rows)
        {
            csv.WriteField(row.HistoryCompetition); csv.WriteField(row.PlayedAt); csv.WriteField(row.HomeTeam);
            csv.WriteField(row.AwayTeam); csv.WriteField(row.Score); csv.WriteField(row.Annotation); csv.NextRecord();
        }
        return writer.ToString();
    }

    private static bool MapIdentityMatches(BundesligaHistoryPlayedDateMapEntry entry, HistoryRow row) =>
        string.Equals(entry.HistoryCompetition, row.HistoryCompetition, StringComparison.Ordinal)
        && string.Equals(entry.HomeTeam, row.HomeTeam, StringComparison.Ordinal)
        && string.Equals(entry.AwayTeam, row.AwayTeam, StringComparison.Ordinal)
        && string.Equals(NormalizeScore(entry.Score), row.Score, StringComparison.Ordinal)
        && string.Equals(entry.Annotation, row.Annotation, StringComparison.Ordinal);

    private static string RowIdentity(HistoryRow row) => string.Join('|', row.HistoryCompetition, row.HomeTeam, row.AwayTeam, row.Score, row.Annotation);

    private static string NormalizeScore(string score)
    {
        var parts = score.Trim().Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var home)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var away) || home < 0 || away < 0)
        {
            throw new InvalidDataException($"Invalid history score '{score}'");
        }
        return $"{home}:{away}";
    }

    private static bool IsExactPlayedAt(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
        || DateTimeOffset.TryParseExact(value, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool SameCalendarDate(string playedAt, string expectedDate)
    {
        if (DateOnly.TryParseExact(playedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return string.Equals(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expectedDate, StringComparison.Ordinal);
        return DateTimeOffset.TryParseExact(playedAt, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
               && string.Equals(timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expectedDate, StringComparison.Ordinal);
    }

    private static string Required(CsvReader csv, string field, string documentName, int ordinal)
    {
        var value = csv.GetField(field)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{documentName} row {ordinal} requires {field}") : value;
    }

    private static BundesligaHistoryPlayedDateCollectionResult Failure(
        IReadOnlyList<BundesligaHistoryDocument> documents, string documentName, int? ordinal, string message) =>
        new(false, documents.ToArray(), Array.Empty<BundesligaHistoryPlayedDateResolution>(),
            new[] { new BundesligaHistoryPlayedDateDiagnostic(documentName, ordinal, message) });

    private sealed record HistoryRow(int Ordinal, string HistoryCompetition, string PlayedAt, string HomeTeam, string AwayTeam, string Score, string Annotation);
}

public sealed record BundesligaHistoryPlayedDateInventory(
    IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> Entries,
    int ExcludedIncompleteRowCount);
