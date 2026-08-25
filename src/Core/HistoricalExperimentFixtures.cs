namespace EHonda.KicktippAi.Core;

/// <summary>
/// Read-only access to completed fixtures used by historical experiments.
/// This boundary intentionally exposes no collection or mutation operations.
/// </summary>
public interface IHistoricalExperimentFixtureReader
{
    Task<IReadOnlyList<PersistedMatchOutcome>> GetCompletedMatchdayFixturesAsync(
        int matchday,
        string communityContext,
        CancellationToken cancellationToken = default);
}
