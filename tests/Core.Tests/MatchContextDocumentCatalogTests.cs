using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class MatchContextDocumentCatalogTests
{
    [Test]
    public async Task World_cup_competition_uses_world_cup_match_context_documents()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "Germany",
            "Cote d'Ivoire",
            "ehonda-dev-wm26",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(selection.RequiredDocumentNames).IsEquivalentTo(
            [
                "fifa-world-cup-2026-standings.csv",
                "community-rules-ehonda-dev-wm26.md",
                "recent-history-germany.csv",
                "recent-history-cote-d-ivoire.csv",
                "fifa-ranking-germany.csv",
                "fifa-ranking-cote-d-ivoire.csv",
                "lineup-germany.csv",
                "lineup-cote-d-ivoire.csv"
            ]);
        await Assert.That(selection.OptionalDocumentNames).IsEmpty();
    }

    [Test]
    public async Task World_cup_knockout_match_replaces_normal_rules_with_knockout_rules()
    {
        var match = new Match("Germany", "Brazil", default, 37)
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };

        var selection = MatchContextDocumentCatalog.ForMatch(
            match,
            "ehonda-dev-wm26",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(selection.RequiredDocumentNames)
            .Contains("community-rules-ehonda-dev-wm26-knockout.md");
        await Assert.That(selection.RequiredDocumentNames)
            .DoesNotContain("community-rules-ehonda-dev-wm26.md");
    }

    [Test]
    public async Task World_cup_community_context_uses_standings_and_community_rules()
    {
        var selection = MatchContextDocumentCatalog.ForCommunity(
            "ehonda-dev-wm26",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(selection.RequiredDocumentNames).IsEquivalentTo(
            [
                "fifa-world-cup-2026-standings.csv",
                "community-rules-ehonda-dev-wm26.md"
            ]);
        await Assert.That(selection.OptionalDocumentNames).IsEmpty();
    }

    [Test]
    public async Task World_cup_standings_document_uses_world_cup_file_name()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "Germany",
            "Cote d'Ivoire",
            "other-wm-community",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(selection.RequiredDocumentNames).Contains("fifa-world-cup-2026-standings.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-germany.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-cote-d-ivoire.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("fifa-ranking-germany.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("fifa-ranking-cote-d-ivoire.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("lineup-germany.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("lineup-cote-d-ivoire.csv");
        await Assert.That(selection.RequiredDocumentNames).DoesNotContain("head-to-head-germany-vs-cote-d-ivoire.csv");
        await Assert.That(selection.OptionalDocumentNames).IsEmpty();
    }

    [Test]
    public async Task Bundesliga_competition_keeps_legacy_standings_document_and_abbreviations()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "FC Bayern München",
            "Borussia Dortmund",
            "pes-squad",
            CompetitionIds.Bundesliga2025_26);

        await Assert.That(selection.RequiredDocumentNames).Contains("bundesliga-standings.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-fcb.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("head-to-head-fcb-vs-bvb.csv");
    }
}
