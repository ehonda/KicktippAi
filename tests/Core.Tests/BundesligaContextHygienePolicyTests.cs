using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaContextHygienePolicyTests
{
    [Test]
    public async Task Expected_documents_cover_match_bonus_and_publication_support_without_legacy_names()
    {
        var documents = BundesligaContextHygienePolicy.GetExpectedDocuments("ehonda-dev-buli-2627");

        await Assert.That(documents.Count).IsEqualTo(401);
        await Assert.That(documents.SequenceEqual(documents
            .OrderBy(entry => entry.Key.Kind)
            .ThenBy(entry => entry.Key.Name, StringComparer.Ordinal))).IsTrue();
        await Assert.That(documents).Contains(entry =>
            entry.Key == new DocumentPublicationKey(DocumentPublicationKind.Context, "roster-fcb")
            && entry.Use == (BundesligaContextDocumentUse.Match | BundesligaContextDocumentUse.Bonus));
        await Assert.That(documents).Contains(entry =>
            entry.Key == new DocumentPublicationKey(DocumentPublicationKind.Kpi, "team-squad-summary")
            && entry.Use == BundesligaContextDocumentUse.Bonus);
        await Assert.That(documents).Contains(entry =>
            entry.Key == new DocumentPublicationKey(DocumentPublicationKind.Kpi, "club-elo-rankings")
            && entry.Use == BundesligaContextDocumentUse.Bonus);
        await Assert.That(documents).Contains(entry =>
            entry.Key == new DocumentPublicationKey(DocumentPublicationKind.Context, "team-rosters")
            && entry.Use == BundesligaContextDocumentUse.PublicationSupport);
        await Assert.That(documents.Select(entry => entry.Key.Name)).DoesNotContain("team-data");
        await Assert.That(documents.Select(entry => entry.Key.Name)).DoesNotContain("manager-data");
    }

    [Arguments("team-data", BundesligaContextHygieneClassification.DeprecatedTeamOrManager)]
    [Arguments("manager-data", BundesligaContextHygieneClassification.DeprecatedTeamOrManager)]
    [Arguments("transfer-data", BundesligaContextHygieneClassification.Transfer)]
    [Arguments("lineup-germany.csv", BundesligaContextHygieneClassification.WorldCup)]
    [Arguments("club-summary-2025-26.csv", BundesligaContextHygieneClassification.HistoricalSeason)]
    [Test]
    public async Task Deprecated_and_cross_contract_names_are_classified_and_blocked(
        string documentName,
        BundesligaContextHygieneClassification expectedClassification)
    {
        var assessment = BundesligaContextHygienePolicy.Assess(
            DocumentPublicationKind.Kpi,
            documentName,
            "ehonda-dev-buli-2627");

        await Assert.That(assessment.Classification).IsEqualTo(expectedClassification);
        await Assert.That(assessment.BlocksGenericMutation).IsTrue();
    }

    [Test]
    public async Task Current_profile_names_are_blocked_from_generic_mutation()
    {
        await Assert.That(() => BundesligaContextHygienePolicy.ThrowIfBlockedGenericMutation(
                CompetitionIds.Bundesliga2026_27,
                DocumentPublicationKind.Context,
                "recent-history-fcb.csv",
                "ehonda-dev-buli-2627"))
            .Throws<InvalidOperationException>();
    }

    [Arguments(DocumentPublicationKind.Context, "team-squad-summary")]
    [Arguments(DocumentPublicationKind.Context, "club-elo-rankings")]
    [Arguments(DocumentPublicationKind.Kpi, "bundesliga-standings.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "community-rules-ehonda-dev-buli-2627.md")]
    [Arguments(DocumentPublicationKind.Kpi, "recent-history-fcb.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "home-history-fcb.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "away-history-fcb.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "head-to-head-fcb-vs-bvb.csv")]
    [Arguments(DocumentPublicationKind.Kpi, "roster-fcb")]
    [Arguments(DocumentPublicationKind.Kpi, "team-rosters")]
    [Arguments(DocumentPublicationKind.Kpi, "club-elo-fcb.csv")]
    [Test]
    public async Task Wrong_kind_shadows_of_every_profile_owned_name_family_fail_closed(
        DocumentPublicationKind wrongKind,
        string documentName)
    {
        var assessment = BundesligaContextHygienePolicy.Assess(
            wrongKind,
            documentName,
            "ehonda-dev-buli-2627");

        await Assert.That(assessment.Classification)
            .IsEqualTo(BundesligaContextHygieneClassification.InvalidProfileOwnedName);
        await Assert.That(assessment.BlocksGenericMutation).IsTrue();
        await Assert.That(() => BundesligaContextHygienePolicy.ThrowIfBlockedGenericMutation(
                CompetitionIds.Bundesliga2026_27,
                wrongKind,
                documentName,
                "ehonda-dev-buli-2627"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public Task Historical_partition_preserves_generic_mutation_behavior()
    {
        BundesligaContextHygienePolicy.ThrowIfBlockedGenericMutation(
            CompetitionIds.Bundesliga2025_26,
            DocumentPublicationKind.Kpi,
            "team-data",
            "historical-community");

        return Task.CompletedTask;
    }

    [Test]
    public async Task Unexpected_current_document_is_visible_but_not_selected_or_blocked()
    {
        var assessment = BundesligaContextHygienePolicy.Assess(
            DocumentPublicationKind.Context,
            "operator-notes.md",
            "ehonda-dev-buli-2627");

        await Assert.That(assessment.Classification).IsEqualTo(BundesligaContextHygieneClassification.Unexpected);
        await Assert.That(assessment.Use).IsEqualTo(BundesligaContextDocumentUse.None);
        await Assert.That(assessment.BlocksGenericMutation).IsFalse();

        BundesligaContextHygienePolicy.ThrowIfBlockedGenericMutation(
            CompetitionIds.Bundesliga2026_27,
            DocumentPublicationKind.Context,
            "operator-notes.md",
            "ehonda-dev-buli-2627");
    }
}
