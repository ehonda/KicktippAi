namespace EHonda.KicktippAi.Core;

public sealed record MatchdayCompletionFixture(
    string? TippSpielId,
    MatchOutcomeAvailability Availability);

public sealed record CompetitionMatchdayCompletionPolicy(
    string Competition,
    int? ExpectedMatchesPerMatchday)
{
    public bool IsComplete(IEnumerable<MatchdayCompletionFixture> fixtures)
    {
        ArgumentNullException.ThrowIfNull(fixtures);
        var materialized = fixtures.ToArray();

        if (materialized.Length == 0)
        {
            return false;
        }

        if (ExpectedMatchesPerMatchday is int expectedMatches
            && materialized.Length != expectedMatches)
        {
            return false;
        }

        if (materialized.Any(fixture => string.IsNullOrWhiteSpace(fixture.TippSpielId)))
        {
            return false;
        }

        if (materialized
                .Select(fixture => fixture.TippSpielId!)
                .Distinct(StringComparer.Ordinal)
                .Count() != materialized.Length)
        {
            return false;
        }

        return materialized.All(fixture => fixture.Availability == MatchOutcomeAvailability.Completed);
    }
}

public static class CompetitionMatchdayCompletionPolicies
{
    private static readonly CompetitionMatchdayCompletionPolicy Bundesliga2026_27 = new(
        CompetitionIds.Bundesliga2026_27,
        ExpectedMatchesPerMatchday: 9);

    private static readonly CompetitionMatchdayCompletionPolicy FifaWorldCup2026 = new(
        CompetitionIds.FifaWorldCup2026,
        ExpectedMatchesPerMatchday: null);

    public static CompetitionMatchdayCompletionPolicy Get(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var normalizedCompetition = competition.Trim();

        return normalizedCompetition switch
        {
            CompetitionIds.Bundesliga2026_27 => Bundesliga2026_27,
            CompetitionIds.FifaWorldCup2026 => FifaWorldCup2026,
            _ => throw new NotSupportedException(
                $"Competition '{normalizedCompetition}' does not define a matchday completion policy.")
        };
    }
}
