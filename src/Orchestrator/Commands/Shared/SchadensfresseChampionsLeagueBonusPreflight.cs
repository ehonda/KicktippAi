using EHonda.KicktippAi.Core;
namespace Orchestrator.Commands.Shared;

/// <summary>
/// Exact, prompt-independent identity gate for the only currently seeded schadensfresse
/// bonus route. It deliberately accepts neither a text-derived route nor a partial set.
/// </summary>
internal static class SchadensfresseChampionsLeagueBonusPreflight
{
    internal const string ProfileId = "schadensfresse-champions-league-bonus-rules-only-v1";
    internal const string MissingPromptProvenanceReason =
        "schadensfresse Champions-League bonus is closed because accepted immutable CL prompt provenance is not configured.";

    internal static bool IsTargetCommunity(string? community) =>
        string.Equals(community, SchadensfressePrimaryRouteGate.Community, StringComparison.OrdinalIgnoreCase);

    internal static void EnsureCanonicalInvocation(
        string? community,
        string? communityContext,
        string? competition)
    {
        if (!string.Equals(community, SchadensfressePrimaryRouteGate.Community, StringComparison.Ordinal)
            || !string.Equals(communityContext, SchadensfressePrimaryRouteGate.Community, StringComparison.Ordinal)
            || !string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "schadensfresse Champions-League bonus requires exact bundesliga-2026-27, schadensfresse target/community context, and no reference-copy alias.");
        }
    }

    internal static IReadOnlyList<BonusQuestion> EnrichAndClassifyCompleteOpenSet(
        IReadOnlyCollection<BonusQuestion> openQuestions,
        BundesligaSeasonRoutingSeed seed)
    {
        ArgumentNullException.ThrowIfNull(openQuestions);
        ArgumentNullException.ThrowIfNull(seed);

        var expected = seed.Questions;
        if (expected.Count != 3
            || expected.Any(question => question.BundesligaSeasonSubcompetition != BundesligaSeasonSubcompetition.ChampionsLeague)
            || openQuestions.Count != expected.Count)
        {
            throw new InvalidDataException("schadensfresse Champions-League bonus questions are incomplete or ambiguous.");
        }

        var byId = new Dictionary<string, BonusQuestion>(StringComparer.Ordinal);
        foreach (var question in openQuestions)
        {
            if (question is null || string.IsNullOrWhiteSpace(question.KicktippQuestionId)
                || !byId.TryAdd(question.KicktippQuestionId, question))
            {
                throw new InvalidDataException("schadensfresse Champions-League bonus questions have missing or duplicate stable IDs.");
            }
        }

        var classifier = new BundesligaSeasonRoutingClassifier(seed);
        var classified = new List<BonusQuestion>(expected.Count);
        foreach (var expectedQuestion in expected)
        {
            if (!byId.TryGetValue(expectedQuestion.KicktippQuestionId, out var source))
            {
                throw new InvalidDataException("schadensfresse Champions-League bonus questions are missing a seeded ID.");
            }

            if (source.BundesligaSeasonSubcompetition is { } suppliedSubcompetition
                && suppliedSubcompetition != BundesligaSeasonSubcompetition.ChampionsLeague)
            {
                throw new InvalidDataException("schadensfresse Champions-League bonus question has a conflicting subcompetition.");
            }

            var enriched = source.BundesligaSeasonSubcompetition is null
                ? source with { BundesligaSeasonSubcompetition = expectedQuestion.BundesligaSeasonSubcompetition }
                : source;
            if (!classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, enriched, out var identity)
                || identity.BundesligaSeasonSubcompetition != BundesligaSeasonSubcompetition.ChampionsLeague)
            {
                throw new InvalidDataException("schadensfresse Champions-League bonus question identity drifted from the routing seed.");
            }

            classified.Add(enriched);
        }

        return classified;
    }
}
