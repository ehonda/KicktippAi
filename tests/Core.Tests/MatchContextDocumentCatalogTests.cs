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
    }

    [Test]
    public async Task Current_bundesliga_competition_uses_manifest_document_slugs()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "FC Bayern München",
            "Borussia Dortmund",
            "pes-squad",
            CompetitionIds.Bundesliga2026_27);

        await Assert.That(selection.RequiredDocumentNames).Contains("bundesliga-standings.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("recent-history-fcb.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("head-to-head-fcb-vs-bvb.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("roster-fcb");
        await Assert.That(selection.RequiredDocumentNames).Contains("roster-bvb");
        await Assert.That(selection.RequiredDocumentNames).Contains("club-elo-fcb.csv");
        await Assert.That(selection.RequiredDocumentNames).Contains("club-elo-bvb.csv");
        await Assert.That(selection.RequiredDocumentNames).Count().IsEqualTo(11);
        await Assert.That(selection.RequiredDocumentNames.SequenceEqual(
            [
                "bundesliga-standings.csv",
                "community-rules-pes-squad.md",
                "recent-history-fcb.csv",
                "recent-history-bvb.csv",
                "home-history-fcb.csv",
                "away-history-bvb.csv",
                "head-to-head-fcb-vs-bvb.csv",
                "roster-fcb",
                "roster-bvb",
                "club-elo-fcb.csv",
                "club-elo-bvb.csv"
            ])).IsTrue();
    }

    [Test]
    public async Task Explicit_bundesliga_competition_takes_precedence_over_an_unmapped_community_name()
    {
        var selection = MatchContextDocumentCatalog.ForMatch(
            "FC Bayern München",
            "Borussia Dortmund",
            "community-name-containing-wm-but-not-mapped",
            CompetitionIds.Bundesliga2026_27);

        await Assert.That(selection.RequiredDocumentNames.SequenceEqual(
        [
            "bundesliga-standings.csv",
            "community-rules-community-name-containing-wm-but-not-mapped.md",
            "recent-history-fcb.csv",
            "recent-history-bvb.csv",
            "home-history-fcb.csv",
            "away-history-bvb.csv",
            "head-to-head-fcb-vs-bvb.csv",
            "roster-fcb",
            "roster-bvb",
            "club-elo-fcb.csv",
            "club-elo-bvb.csv"
        ])).IsTrue();
    }

    [Arguments(CompetitionIds.Bundesliga2026_27, "ehonda-dev-wm26")]
    [Arguments(CompetitionIds.FifaWorldCup2026, "ehonda-dev-buli-2627")]
    [Test]
    public async Task Explicit_competition_and_known_community_conflicts_fail_closed(
        string competition,
        string communityContext)
    {
        await Assert.That(() => MatchContextDocumentCatalog.ForCommunity(communityContext, competition))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Match_catalog_rejects_known_conflict_before_interpreting_team_names()
    {
        await Assert.That(() => MatchContextDocumentCatalog.ForMatch(
                "FC Bayern München",
                "Borussia Dortmund",
                "ehonda-dev-wm26",
                CompetitionIds.Bundesliga2026_27))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Current_bundesliga_competition_rejects_unknown_team_instead_of_slugging_it()
    {
        await Assert.That(() => MatchContextDocumentCatalog.ForMatch(
                "Unknown Team FC",
                "Borussia Dortmund",
                "ehonda-dev-buli-2627",
                CompetitionIds.Bundesliga2026_27))
            .Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task World_cup_competition_still_slugs_team_names_without_the_bundesliga_manifest()
    {
        var slug = MatchContextDocumentCatalog.GetTeamAbbreviation(
            "Côte d'Ivoire",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(slug).IsEqualTo("cote-d-ivoire");
    }
}
