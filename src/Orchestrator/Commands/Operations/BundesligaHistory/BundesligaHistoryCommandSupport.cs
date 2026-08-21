using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.BundesligaHistory;

internal static class BundesligaHistoryCommandSupport
{
    public static async Task<IReadOnlyList<BundesligaHistoryDocument>> LoadStoredDocumentsAsync(
        IContextRepository repository,
        string communityContext,
        CancellationToken cancellationToken)
    {
        var names = await repository.GetContextDocumentNamesAsync(communityContext, cancellationToken);
        var documents = new List<BundesligaHistoryDocument>();
        foreach (var name in names.Where(BundesligaHistoryPlayedDateCollector.IsSelectedDocumentName).Order(StringComparer.Ordinal))
        {
            var document = await repository.GetLatestContextDocumentAsync(name, communityContext, cancellationToken);
            if (document is not null) documents.Add(new(name, document.Content));
        }
        return documents.AsReadOnly();
    }

    public static async Task<IReadOnlyList<PersistedMatchOutcome>> LoadOutcomesAsync(
        IMatchOutcomeRepository repository,
        string communityContext,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<PersistedMatchOutcome>();
        for (var matchday = 1; matchday <= 34; matchday++)
        {
            outcomes.AddRange(await repository.GetMatchdayOutcomesAsync(matchday, communityContext, cancellationToken));
        }
        return outcomes.AsReadOnly();
    }

    public static BundesligaHistoryPlayedDateMap ReadMap(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Bundesliga history played-date map was not found", path);
        using var reader = File.OpenText(path);
        return BundesligaHistoryPlayedDateMap.Parse(reader, path);
    }

    public static IReadOnlyList<int> ParseMatchdays(string value)
    {
        var result = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var matchday) && matchday is >= 1 and <= 34
                ? matchday : throw new ArgumentException($"Invalid matchday '{token}'; expected 1-34"))
            .Distinct().ToArray();
        return result.Length > 0 ? result : throw new ArgumentException("At least one matchday is required");
    }

    public static string FormatFixedSourceCounts(BundesligaHistoryPlayedDateCollectionResult result) =>
        string.Join(", ", result.Resolutions
            .Where(resolution => resolution.SourceClass == BundesligaHistoryPlayedDateSourceClass.FixedExternalMap)
            .Select(resolution => resolution.SourceIdentity.Split('@', 2)[0])
            .GroupBy(source => source, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}"));
}
