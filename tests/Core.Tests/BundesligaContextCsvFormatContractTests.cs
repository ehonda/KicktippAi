using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaContextCsvFormatContractTests
{
    private const string CommunityContext = "ehonda-dev-buli-2627";

    [Test]
    public async Task Expected_mapping_covers_exactly_four_hundred_csv_documents_and_one_markdown_document()
    {
        var expected = BundesligaContextHygienePolicy.GetExpectedDocuments(CommunityContext);
        var csvDocuments = BundesligaContextCsvFormatContract.GetExpectedCsvDocuments(CommunityContext);
        var markdown = expected.Single(entry => entry.Key.Name == $"community-rules-{CommunityContext}.md");

        await Assert.That(expected.Count).IsEqualTo(401);
        await Assert.That(csvDocuments.Count).IsEqualTo(400);
        await Assert.That(csvDocuments.Select(document => document.Key).Distinct().Count()).IsEqualTo(400);
        await Assert.That(csvDocuments.Select(document => document.Key)).DoesNotContain(markdown.Key);
        await Assert.That(csvDocuments.Count(document => document.Key.Name == "bundesliga-standings.csv")).IsEqualTo(1);
        await Assert.That(csvDocuments.Count(document =>
                document.Key.Name.StartsWith("recent-history-", StringComparison.Ordinal)
                || document.Key.Name.StartsWith("home-history-", StringComparison.Ordinal)
                || document.Key.Name.StartsWith("away-history-", StringComparison.Ordinal)))
            .IsEqualTo(54);
        await Assert.That(csvDocuments.Count(document =>
                document.Key.Name.StartsWith("head-to-head-", StringComparison.Ordinal)))
            .IsEqualTo(306);
        await Assert.That(csvDocuments.Count(document =>
                document.Key.Name.StartsWith("roster-", StringComparison.Ordinal)))
            .IsEqualTo(18);
        await Assert.That(csvDocuments.Count(document =>
                document.Key.Kind == DocumentPublicationKind.Context
                && document.Key.Name.StartsWith("club-elo-", StringComparison.Ordinal)))
            .IsEqualTo(18);

        await Assert.That(Header(DocumentPublicationKind.Context, "bundesliga-standings.csv"))
            .IsEqualTo(BundesligaContextCsvFormatContract.StandingsHeader);
        await Assert.That(Header(DocumentPublicationKind.Context, "recent-history-fcb.csv"))
            .IsEqualTo(BundesligaContextCsvFormatContract.HistoryHeader);
        await Assert.That(Header(DocumentPublicationKind.Context, "home-history-fcb.csv"))
            .IsEqualTo(BundesligaContextCsvFormatContract.HistoryHeader);
        await Assert.That(Header(DocumentPublicationKind.Context, "away-history-fcb.csv"))
            .IsEqualTo(BundesligaContextCsvFormatContract.HistoryHeader);
        await Assert.That(Header(DocumentPublicationKind.Context, "head-to-head-fcb-vs-bvb.csv"))
            .IsEqualTo(BundesligaContextCsvFormatContract.HeadToHeadHeader);
        await Assert.That(Header(DocumentPublicationKind.Context, "roster-fcb"))
            .IsEqualTo(string.Join(',', BundesligaRosterCsv.RosterHeaders));
        await Assert.That(Header(DocumentPublicationKind.Context, "team-rosters"))
            .IsEqualTo(string.Join(',', BundesligaRosterCsv.RosterHeaders));
        await Assert.That(Header(DocumentPublicationKind.Kpi, "team-squad-summary"))
            .IsEqualTo(string.Join(',', BundesligaRosterCsv.SummaryHeaders));
        await Assert.That(Header(DocumentPublicationKind.Context, "club-elo-fcb.csv"))
            .IsEqualTo(BundesligaClubEloPublication.CsvHeader);
        await Assert.That(Header(DocumentPublicationKind.Kpi, "club-elo-rankings"))
            .IsEqualTo(BundesligaClubEloPublication.CsvHeader);
        await Assert.That(BundesligaContextCsvFormatContract.GetExpectedHeader(markdown.Key, CommunityContext))
            .IsNull();
    }

    [Arguments(DocumentPublicationKind.Context, "bundesliga-standings.csv")]
    [Arguments(DocumentPublicationKind.Context, "recent-history-fcb.csv")]
    [Arguments(DocumentPublicationKind.Context, "head-to-head-fcb-vs-bvb.csv")]
    [Arguments(DocumentPublicationKind.Context, "roster-fcb")]
    [Arguments(DocumentPublicationKind.Context, "team-rosters")]
    [Arguments(DocumentPublicationKind.Kpi, "team-squad-summary")]
    [Arguments(DocumentPublicationKind.Context, "club-elo-fcb.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "club-elo-rankings")]
    [Test]
    public async Task Header_only_and_multirow_payloads_are_valid(
        DocumentPublicationKind kind,
        string name)
    {
        var key = new DocumentPublicationKey(kind, name);
        var header = Header(kind, name);

        var headerOnly = BundesligaContextCsvFormatContract.Validate(
            key,
            CommunityContext,
            header + "\r\n",
            validateBytes: true);
        var multirow = BundesligaContextCsvFormatContract.Validate(
            key,
            CommunityContext,
            header + "\r\nvalue\r\nsecond\r\n",
            validateBytes: true);

        await Assert.That(headerOnly.State).IsEqualTo(BundesligaContextCsvValidationState.Valid);
        await Assert.That(headerOnly.Diagnostic).IsNull();
        await Assert.That(multirow.State).IsEqualTo(BundesligaContextCsvValidationState.Valid);
        await Assert.That(multirow.Diagnostic).IsNull();
    }

    [Test]
    public async Task Invalid_shapes_return_only_fixed_codes_in_deterministic_priority_order()
    {
        var cases = new[]
        {
            Invalid(DocumentPublicationKind.Context, "bundesliga-standings.csv", string.Empty,
                BundesligaContextCsvFormatContract.CsvEmpty),
            Invalid(DocumentPublicationKind.Context, "roster-fcb", "\uFEFFSECRET_BOM",
                BundesligaContextCsvFormatContract.CsvBomNotAllowed),
            Invalid(DocumentPublicationKind.Context, "recent-history-fcb.csv", " " + BundesligaContextCsvFormatContract.HistoryHeader + "\r\nSECRET_LEADING",
                BundesligaContextCsvFormatContract.CsvHeaderNotFirstOrExact),
            Invalid(DocumentPublicationKind.Context, "club-elo-fcb.csv", "\r\n" + BundesligaClubEloPublication.CsvHeader + "\r\nSECRET_BLANK",
                BundesligaContextCsvFormatContract.CsvHeaderNotFirstOrExact),
            Invalid(DocumentPublicationKind.Context, "head-to-head-fcb-vs-bvb.csv", BundesligaContextCsvFormatContract.HeadToHeadHeader + ",Drift\r\nSECRET_DRIFT",
                BundesligaContextCsvFormatContract.CsvHeaderNotFirstOrExact),
            Invalid(DocumentPublicationKind.Context, "team-rosters", string.Join(',', BundesligaRosterCsv.RosterHeaders) + "\nSECRET_LF\n",
                BundesligaContextCsvFormatContract.CsvLineEndingNotCrLf),
            Invalid(DocumentPublicationKind.Kpi, "team-squad-summary", string.Join(',', BundesligaRosterCsv.SummaryHeaders),
                BundesligaContextCsvFormatContract.CsvFinalCrLfRequired),
            Invalid(DocumentPublicationKind.Kpi, "club-elo-rankings", BundesligaClubEloPublication.CsvHeader + "\rSECRET_CR",
                BundesligaContextCsvFormatContract.CsvLineEndingNotCrLf)
        };

        foreach (var item in cases)
        {
            var result = BundesligaContextCsvFormatContract.Validate(
                item.Key,
                CommunityContext,
                item.Content,
                validateBytes: true);

            await Assert.That(result.State).IsEqualTo(BundesligaContextCsvValidationState.Invalid);
            await Assert.That(result.Diagnostic).IsEqualTo(item.ExpectedDiagnostic);
            await Assert.That(new[]
            {
                BundesligaContextCsvFormatContract.CsvEmpty,
                BundesligaContextCsvFormatContract.CsvBomNotAllowed,
                BundesligaContextCsvFormatContract.CsvHeaderNotFirstOrExact,
                BundesligaContextCsvFormatContract.CsvLineEndingNotCrLf,
                BundesligaContextCsvFormatContract.CsvFinalCrLfRequired
            }).Contains(result.Diagnostic!);
        }
    }

    [Test]
    public async Task Validation_states_distinguish_non_csv_skipped_and_missing_documents()
    {
        var csvKey = new DocumentPublicationKey(DocumentPublicationKind.Context, "bundesliga-standings.csv");
        var markdownKey = new DocumentPublicationKey(
            DocumentPublicationKind.Context,
            $"community-rules-{CommunityContext}.md");
        var unexpectedKey = new DocumentPublicationKey(DocumentPublicationKind.Context, "operator-notes.csv");

        var notApplicable = BundesligaContextCsvFormatContract.Validate(
            markdownKey, CommunityContext, "SECRET_MARKDOWN", validateBytes: true);
        var unexpected = BundesligaContextCsvFormatContract.Validate(
            unexpectedKey, CommunityContext, "SECRET_UNEXPECTED", validateBytes: true);
        var notChecked = BundesligaContextCsvFormatContract.Validate(
            csvKey, CommunityContext, "SECRET_NOT_CHECKED", validateBytes: false);
        var missing = BundesligaContextCsvFormatContract.Validate(
            csvKey, CommunityContext, null, validateBytes: true);

        await Assert.That(notApplicable).IsEqualTo(
            new BundesligaContextCsvValidation(BundesligaContextCsvValidationState.NotApplicable, null));
        await Assert.That(unexpected).IsEqualTo(
            new BundesligaContextCsvValidation(BundesligaContextCsvValidationState.NotApplicable, null));
        await Assert.That(notChecked).IsEqualTo(
            new BundesligaContextCsvValidation(BundesligaContextCsvValidationState.NotChecked, null));
        await Assert.That(missing).IsEqualTo(
            new BundesligaContextCsvValidation(BundesligaContextCsvValidationState.Missing, null));
    }

    private static string Header(DocumentPublicationKind kind, string name) =>
        BundesligaContextCsvFormatContract.GetExpectedHeader(
            new DocumentPublicationKey(kind, name),
            CommunityContext)!;

    private static InvalidCase Invalid(
        DocumentPublicationKind kind,
        string name,
        string content,
        string expectedDiagnostic) =>
        new(new DocumentPublicationKey(kind, name), content, expectedDiagnostic);

    private sealed record InvalidCase(
        DocumentPublicationKey Key,
        string Content,
        string ExpectedDiagnostic);
}
