namespace EHonda.KicktippAi.Core;

public enum BundesligaContextCsvValidationState
{
    NotApplicable,
    NotChecked,
    Missing,
    Valid,
    Invalid
}

public sealed record BundesligaContextCsvDocumentContract(
    DocumentPublicationKey Key,
    string Header);

public sealed record BundesligaContextCsvValidation(
    BundesligaContextCsvValidationState State,
    string? Diagnostic);

/// <summary>
/// Defines and validates the exact byte-oriented CSV envelope for every CSV document in the
/// Bundesliga 2026/27 context-hygiene allowlist. The validator deliberately reports only fixed
/// diagnostic codes; payload content never belongs in an inventory result or diagnostic.
/// </summary>
public static class BundesligaContextCsvFormatContract
{
    public const string CsvEmpty = "CSV_EMPTY";
    public const string CsvBomNotAllowed = "CSV_BOM_NOT_ALLOWED";
    public const string CsvHeaderNotFirstOrExact = "CSV_HEADER_NOT_FIRST_OR_EXACT";
    public const string CsvLineEndingNotCrLf = "CSV_LINE_ENDING_NOT_CRLF";
    public const string CsvFinalCrLfRequired = "CSV_FINAL_CRLF_REQUIRED";

    public const string StandingsHeader =
        "Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses,Group";

    public const string HistoryHeader =
        "Competition,Played_At,Home_Team,Away_Team,Score,Annotation";

    public const string HeadToHeadHeader =
        "Competition,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation";

    public static IReadOnlyList<BundesligaContextCsvDocumentContract> GetExpectedCsvDocuments(
        string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        return BundesligaContextHygienePolicy.GetExpectedDocuments(communityContext)
            .Select(entry => new
            {
                entry.Key,
                Header = GetExpectedHeader(entry.Key, communityContext)
            })
            .Where(entry => entry.Header is not null)
            .Select(entry => new BundesligaContextCsvDocumentContract(entry.Key, entry.Header!))
            .ToArray();
    }

    public static string? GetExpectedHeader(
        DocumentPublicationKey key,
        string communityContext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var assessment = BundesligaContextHygienePolicy.Assess(key.Kind, key.Name, communityContext);
        if (assessment.Classification != BundesligaContextHygieneClassification.Expected)
        {
            return null;
        }

        if (key.Kind == DocumentPublicationKind.Context)
        {
            if (string.Equals(key.Name, "bundesliga-standings.csv", StringComparison.Ordinal))
            {
                return StandingsHeader;
            }

            if (key.Name.StartsWith("recent-history-", StringComparison.Ordinal)
                || key.Name.StartsWith("home-history-", StringComparison.Ordinal)
                || key.Name.StartsWith("away-history-", StringComparison.Ordinal))
            {
                return HistoryHeader;
            }

            if (key.Name.StartsWith("head-to-head-", StringComparison.Ordinal))
            {
                return HeadToHeadHeader;
            }

            if (key.Name.StartsWith("roster-", StringComparison.Ordinal)
                || string.Equals(
                    key.Name,
                    BundesligaRosterPublicationContract.AggregateRosterDocumentName,
                    StringComparison.Ordinal))
            {
                return string.Join(',', BundesligaRosterCsv.RosterHeaders);
            }

            if (key.Name.StartsWith("club-elo-", StringComparison.Ordinal))
            {
                return BundesligaClubEloPublication.CsvHeader;
            }
        }

        if (key.Kind == DocumentPublicationKind.Kpi)
        {
            if (string.Equals(
                    key.Name,
                    BundesligaRosterPublicationContract.SquadSummaryDocumentName,
                    StringComparison.Ordinal))
            {
                return string.Join(',', BundesligaRosterCsv.SummaryHeaders);
            }

            if (string.Equals(
                    key.Name,
                    BundesligaDocumentPublication.ClubEloRankingsDocumentName,
                    StringComparison.Ordinal))
            {
                return BundesligaClubEloPublication.CsvHeader;
            }
        }

        return null;
    }

    public static BundesligaContextCsvValidation Validate(
        DocumentPublicationKey key,
        string communityContext,
        string? effectiveContent,
        bool validateBytes)
    {
        var expectedHeader = GetExpectedHeader(key, communityContext);
        if (expectedHeader is null)
        {
            return new(BundesligaContextCsvValidationState.NotApplicable, null);
        }

        if (effectiveContent is null)
        {
            return new(BundesligaContextCsvValidationState.Missing, null);
        }

        if (!validateBytes)
        {
            return new(BundesligaContextCsvValidationState.NotChecked, null);
        }

        var diagnostic = ValidateContent(effectiveContent, expectedHeader);
        return diagnostic is null
            ? new(BundesligaContextCsvValidationState.Valid, null)
            : new(BundesligaContextCsvValidationState.Invalid, diagnostic);
    }

    private static string? ValidateContent(string content, string expectedHeader)
    {
        if (content.Length == 0)
        {
            return CsvEmpty;
        }

        if (content[0] == '\uFEFF')
        {
            return CsvBomNotAllowed;
        }

        var firstTerminatorIndex = content.IndexOfAny(['\r', '\n']);
        var firstLine = firstTerminatorIndex < 0
            ? content
            : content[..firstTerminatorIndex];
        if (!string.Equals(firstLine, expectedHeader, StringComparison.Ordinal))
        {
            return CsvHeaderNotFirstOrExact;
        }

        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\r')
            {
                if (index + 1 >= content.Length || content[index + 1] != '\n')
                {
                    return CsvLineEndingNotCrLf;
                }

                index++;
            }
            else if (content[index] == '\n')
            {
                return CsvLineEndingNotCrLf;
            }
        }

        return content.EndsWith("\r\n", StringComparison.Ordinal)
            ? null
            : CsvFinalCrLfRequired;
    }
}
