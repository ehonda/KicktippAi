using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class PredictionCopyCompatibilityV2Tests
{
    private static PredictionCopyCompatibilityEvidenceV2 Evidence(
        bool rules = true, bool prompt = true, bool options = true) =>
        new("synthetic-compatibility-v2", rules, prompt, options);

    [Test]
    public async Task Exact_match_and_bonus_bindings_succeed_without_degraded_fallback()
    {
        var postingSeed = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var sourceSeed = BundesligaPredictionContractTestData.Seed("pes-squad");
        var binding = BundesligaPredictionContractTestData.Binding(postingSeed, sourceSeed);
        var match = PredictionCopyCompatibilityV2.EvaluateMatch(
            binding.RequirePostingItem(BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt")),
            postingSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt")).MatchSnapshot!,
            sourceSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("pes-squad")).MatchSnapshot!,
            BundesligaPredictionContractTestData.MatchRoute, Evidence());
        var bonus = PredictionCopyCompatibilityV2.EvaluateBonus(
            binding.RequirePostingItem(BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt")),
            postingSeed.RequireEntry(BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt")).BonusSnapshot!,
            sourceSeed.RequireEntry(BundesligaPredictionContractTestData.BonusKey("pes-squad")).BonusSnapshot!,
            BundesligaPredictionContractTestData.BonusRoute, Evidence());

        await Assert.That(match.Succeeded).IsTrue();
        await Assert.That(match.Failure).IsEqualTo(PredictionCopyCompatibilityV2Failure.None);
        await Assert.That(bonus.Succeeded).IsTrue();
        await Assert.That(bonus.OptionProjection.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(false, true, true, PredictionCopyCompatibilityV2Failure.RulesOrScoringMismatch)]
    [Arguments(true, false, true, PredictionCopyCompatibilityV2Failure.PromptModelMismatch)]
    [Arguments(true, true, false, PredictionCopyCompatibilityV2Failure.OptionMeaningMismatch)]
    public async Task Any_missing_bonus_compatibility_evidence_rejects_all_copy_output(
        bool rules, bool prompt, bool options, PredictionCopyCompatibilityV2Failure failure)
    {
        var postingSeed = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var sourceSeed = BundesligaPredictionContractTestData.Seed("pes-squad");
        var binding = BundesligaPredictionContractTestData.Binding(postingSeed, sourceSeed);
        var result = PredictionCopyCompatibilityV2.EvaluateBonus(
            binding.RequirePostingItem(BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt")),
            postingSeed.RequireEntry(BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt")).BonusSnapshot!,
            sourceSeed.RequireEntry(BundesligaPredictionContractTestData.BonusKey("pes-squad")).BonusSnapshot!,
            BundesligaPredictionContractTestData.BonusRoute, Evidence(rules, prompt, options));

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Failure).IsEqualTo(failure);
        await Assert.That(result.OptionProjection).IsEmpty();
    }

    [Test]
    public async Task Snapshot_drift_and_route_drift_fail_closed()
    {
        var postingSeed = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var sourceSeed = BundesligaPredictionContractTestData.Seed("pes-squad");
        var entry = BundesligaPredictionContractTestData.Binding(postingSeed, sourceSeed)
            .RequirePostingItem(BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt"));
        var moved = BundesligaPredictionContractTestData.Match("relaxdays-tippt", scheduledInstant: "2026-09-01T19:00:00Z");
        var source = sourceSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("pes-squad")).MatchSnapshot!;

        var hashFailure = PredictionCopyCompatibilityV2.EvaluateMatch(entry, moved, source, BundesligaPredictionContractTestData.MatchRoute, Evidence());
        var routeFailure = PredictionCopyCompatibilityV2.EvaluateMatch(
            entry,
            postingSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt")).MatchSnapshot!,
            source, "different-route", Evidence());
        await Assert.That(hashFailure.Failure).IsEqualTo(PredictionCopyCompatibilityV2Failure.SnapshotHashMismatch);
        await Assert.That(routeFailure.Failure).IsEqualTo(PredictionCopyCompatibilityV2Failure.RouteMismatch);
    }
}
