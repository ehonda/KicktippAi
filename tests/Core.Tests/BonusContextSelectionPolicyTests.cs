using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BonusContextSelectionPolicyTests
{
    private const string TopScorerTeamQuestion = "Welche Mannschaft stellt den Spieler mit den meisten Toren?";

    [Test]
    public async Task Unknown_question_uses_only_the_ordered_aggregate_baseline()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Wer wird Deutscher Meister?", "FC Bayern München", "Borussia Dortmund"),
            Rosters());

        await Assert.That(selection.RequiredDocuments.Select(document => $"{document.Kind}:{document.Name}"))
            .IsEquivalentTo(
            [
                "Kpi:club-elo-rankings",
                "Kpi:team-squad-summary"
            ]);
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary"
        ])).IsTrue();
        await Assert.That(selection.TargetedTeamSlugs).IsEmpty();
    }

    [Test]
    public async Task Top_scorer_team_question_targets_only_exact_manifest_options_in_slug_order()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question(TopScorerTeamQuestion, "Borussia Dortmund", "FC Bayern München"),
            Rosters());

        await Assert.That(selection.TargetedTeamSlugs.SequenceEqual(["bvb", "fcb"])).IsTrue();
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary",
            "roster-bvb",
            "roster-fcb"
        ])).IsTrue();
    }

    [Test]
    public async Task Top_scorer_player_option_targets_the_players_current_roster()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Wer wird Torschützenkönig?", "Harry Kane"),
            Rosters());

        await Assert.That(selection.TargetedTeamSlugs).IsEquivalentTo(["fcb"]);
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name))
            .DoesNotContain("team-rosters");
    }

    [Test]
    public async Task Coach_option_targets_the_coachs_current_roster()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Welcher Trainer wird zuerst entlassen?", "Niko Kovač"),
            Rosters());

        await Assert.That(selection.TargetedTeamSlugs).IsEquivalentTo(["bvb"]);
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name))
            .DoesNotContain("manager-data");
    }

    [Test]
    public async Task Roster_relevant_question_without_an_exact_target_fails_instead_of_loading_every_roster()
    {
        await Assert.That(() => BonusContextSelectionPolicy.SelectBundesliga(
                Question("Welcher Trainer wird zuerst entlassen?", "Unbekannte Person"),
                Rosters()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("requires targeted roster context");
    }

    [Test]
    public async Task Baseline_question_does_not_add_rosters_merely_because_options_are_teams()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Wer wird Deutscher Meister?", "FC Bayern München", "Borussia Dortmund"),
            Rosters());

        await Assert.That(selection.RequiredDocuments.Select(document => document.Name))
            .DoesNotContain(name => name.StartsWith("roster-", StringComparison.Ordinal));
    }

    [Test]
    public async Task Relegation_question_uses_the_aggregate_baseline_without_legacy_manager_context()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Welche drei Mannschaften steigen ab?", "FC Bayern München", "Borussia Dortmund"),
            Rosters());

        await Assert.That(selection.RequiredDocuments.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary"
        ])).IsTrue();
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name))
            .DoesNotContain("manager-data");
    }

    private static BonusQuestion Question(string text, params string[] options) => new(
        text,
        default,
        options.Select((option, index) => new BonusQuestionOption(index.ToString(), option)).ToList(),
        1);

    private static BundesligaRosterLastKnownGood Rosters()
    {
        var bayern = BundesligaTeamManifest.Default.GetByTeamSlug("fcb");
        var dortmund = BundesligaTeamManifest.Default.GetByTeamSlug("bvb");
        return new BundesligaRosterLastKnownGood(
            new string('a', 64),
            [
                new BundesligaRosterClubSnapshot(
                    bayern,
                    new DateOnly(2026, 8, 16),
                    BundesligaRosterMembershipSource.FallbackSeed,
                    [
                        new BundesligaRosterMember(BundesligaRosterRole.Coach, "Vincent Kompany"),
                        new BundesligaRosterMember(BundesligaRosterRole.Player, "Harry Kane")
                    ]),
                new BundesligaRosterClubSnapshot(
                    dortmund,
                    new DateOnly(2026, 8, 16),
                    BundesligaRosterMembershipSource.FallbackSeed,
                    [
                        new BundesligaRosterMember(BundesligaRosterRole.Coach, "Niko Kovač"),
                        new BundesligaRosterMember(BundesligaRosterRole.Player, "Serhou Guirassy")
                    ])
            ],
            [],
            string.Empty);
    }
}
