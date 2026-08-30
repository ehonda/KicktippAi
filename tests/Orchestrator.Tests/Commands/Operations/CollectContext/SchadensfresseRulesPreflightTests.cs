using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class SchadensfresseRulesPreflightTests
{
    [Test]
    public async Task Read_only_preflight_accepts_only_current_seed_pinned_immutable_rules_document()
    {
        var seed = BundesligaSeasonRoutingSeed.Default;
        var bytes = File.ReadAllBytes(Path.Combine(SolutionPathUtility.FindSolutionRoot(), "community-rules", "schadensfresse.md"));
        var now = DateTimeOffset.UtcNow;
        var observation = new SchadensfresseLiveRulesObservation(
            SchadensfresseRulesCanonicalJson.Expected,
            now,
            SchadensfresseRulesCanonicalJson.ScoringTableSha256,
            SchadensfresseRulesCanonicalJson.LegacyNormalizedSha256);
        var readback = new SchadensfresseRulesPublicationReadback(
            SchadensfresseRulesPublicationGate.DocumentName,
            1,
            seed.CommunityRulesContentSha256);

        SchadensfresseRulesPreflight.ValidateObservation(observation, seed, bytes, SchadensfresseRulesPublicationGate.DocumentName, 1, readback, now);
        await Assert.That(() => SchadensfresseRulesPreflight.ValidateObservation(observation, seed, bytes, "latest", 1, readback, now)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseRulesPreflight.ValidateObservation(observation, seed, bytes, SchadensfresseRulesPublicationGate.DocumentName, 2, readback, now)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseRulesPreflight.ValidateObservation(observation, seed, bytes, SchadensfresseRulesPublicationGate.DocumentName, 1, readback with { Version = -1 }, now)).Throws<InvalidDataException>();
    }
}
