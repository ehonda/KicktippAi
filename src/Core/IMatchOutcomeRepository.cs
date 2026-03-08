namespace EHonda.KicktippAi.Core;

public interface IMatchOutcomeRepository
{
    Task<MatchOutcomeUpsertResult> UpsertMatchOutcomeAsync(
        CollectedMatchOutcome outcome,
        string communityContext,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetIncompleteMatchdaysAsync(
        string communityContext,
        int currentMatchday,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedMatchOutcome>> GetMatchdayOutcomesAsync(
        int matchday,
        string communityContext,
        CancellationToken cancellationToken = default);
}
