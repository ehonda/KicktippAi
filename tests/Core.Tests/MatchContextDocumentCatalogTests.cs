using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class MatchContextDocumentCatalogTests
{
    [Test]
    public async Task World_cup_competition_uses_world_cup_standings_document()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "Germany",
            "Cote d'Ivoire",
            "ehonda-dev-wm26",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(selection.RequiredDocumentNames).Contains("fifa-world-cup-2026-standings.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-germany.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-cote-d-ivoire.csv");
        await Assert.That(selection.OptionalDocumentNames).Contains("germany-transfers.csv");
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
