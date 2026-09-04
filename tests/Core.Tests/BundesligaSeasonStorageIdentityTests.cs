using EHonda.KicktippAi.Core;
using NodaTime;

namespace Core.Tests;

public sealed class BundesligaSeasonStorageIdentityTests
{
    [Test]
    [Arguments(BundesligaSeasonSubcompetition.Bundesliga, ResultBasis.RegularTime90Minutes)]
    [Arguments(BundesligaSeasonSubcompetition.DfbPokal, ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)]
    [Arguments(BundesligaSeasonSubcompetition.ChampionsLeague, ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)]
    public async Task Current_Bundesliga_match_identity_accepts_each_exact_subcompetition(
        BundesligaSeasonSubcompetition subcompetition,
        ResultBasis resultBasis)
    {
        var match = Match(subcompetition, resultBasis);

        BundesligaSeasonStorageIdentity.ValidateMatch(CompetitionIds.Bundesliga2026_27, match);

        await Assert.That(match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue())
            .IsNotEmpty();
    }

    [Test]
    public async Task Missing_unknown_conflicting_and_cross_partition_identity_fails_closed()
    {
        var valid = Match(BundesligaSeasonSubcompetition.Bundesliga, ResultBasis.RegularTime90Minutes);

        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateMatch(
                CompetitionIds.Bundesliga2026_27, valid with { KicktippFixtureId = null }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateMatch(
                CompetitionIds.Bundesliga2026_27, valid with { ResultBasis = (ResultBasis)99 }))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateMatch(
                CompetitionIds.Bundesliga2026_27,
                valid with { ResultBasis = ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateMatch(
                CompetitionIds.FifaWorldCup2026, valid))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Bonus_identity_requires_a_stable_question_id_and_never_requires_Bundesliga_fields_for_WM26()
    {
        var deadline = Instant.FromUtc(2026, 9, 8, 16, 45).InZone(DateTimeZone.Utc);
        var typed = new BonusQuestion("Question", deadline, [new BonusQuestionOption("1", "One")], 1)
        {
            KicktippQuestionId = "question-1",
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };

        BundesligaSeasonStorageIdentity.ValidateBonusQuestion(CompetitionIds.Bundesliga2026_27, typed);
        BundesligaSeasonStorageIdentity.ValidateBonusQuestion(
            CompetitionIds.FifaWorldCup2026,
            typed with { BundesligaSeasonSubcompetition = null, KicktippQuestionId = null });

        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateBonusQuestion(
                CompetitionIds.Bundesliga2026_27, typed with { KicktippQuestionId = null }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => BundesligaSeasonStorageIdentity.ValidateBonusQuestion(
                CompetitionIds.FifaWorldCup2026, typed))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Bonus_storage_identity_binds_every_immutable_question_field_in_order()
    {
        var baseline = new BonusQuestion("Exact text", Instant.FromUtc(2026, 9, 8, 16, 45).InZone(DateTimeZone.Utc),
            [new("a", "Alpha"), new("b", "Beta")], 1)
        {
            KicktippQuestionId = "question-1",
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga
        };
        var hash = BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(baseline);
        var drifts = new[]
        {
            baseline with { Text = "Changed text" },
            baseline with { Deadline = baseline.Deadline.PlusHours(1) },
            baseline with { MaxSelections = 2 },
            baseline with { Options = [new("x", "Alpha"), new("b", "Beta")] },
            baseline with { Options = [new("b", "Beta"), new("a", "Alpha")] },
        };

        await Assert.That(drifts.Select(BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256))
            .DoesNotContain(hash);
    }

    private static Match Match(BundesligaSeasonSubcompetition subcompetition, ResultBasis resultBasis) =>
        new("Home", "Away", Instant.FromUtc(2026, 9, 1, 18, 0).InZone(DateTimeZone.Utc), 1)
        {
            KicktippFixtureId = "fixture-1",
            KicktippRoundName = "Exact round",
            BundesligaSeasonSubcompetition = subcompetition,
            ResultBasis = resultBasis
        };
}
