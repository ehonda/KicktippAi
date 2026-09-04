using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Services;

public sealed class BundesligaPredictionRouteRegistry
{
    private readonly ImmutableDictionary<string, BundesligaPredictionRouteSelection> _selections;

    public BundesligaPredictionRouteRegistry(IEnumerable<BundesligaPredictionRouteSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var materialized = selections.ToArray();
        if (materialized.Length == 0 || materialized.Any(selection => selection is null))
        {
            throw new InvalidDataException("R3a registration requires at least one explicit route selection.");
        }

        var duplicate = materialized
            .GroupBy(selection => selection.SelectionId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate registered selection '{duplicate.Key}'.");
        }

        var contracts = materialized.GroupBy(selection => selection.Route.RouteId, StringComparer.Ordinal)
            .Select(group =>
            {
                var contract = group.First().Route;
                if (group.Any(selection => selection.Route != contract))
                {
                    throw new InvalidDataException(
                        $"Registered route '{group.Key}' has conflicting contracts.");
                }
                return contract;
            }).ToArray();

        Routes = new BundesligaPredictionRouteCatalog(contracts);
        _selections = materialized.ToImmutableDictionary(
            selection => selection.SelectionId,
            StringComparer.Ordinal);
    }

    public BundesligaPredictionRouteCatalog Routes { get; }

    public BundesligaPredictionRouteSelection GetRequiredSelection(
        string selectionId,
        BundesligaPredictionAuthority authority,
        BundesligaValidatedMatchItem item) =>
        GetRequiredSelection(
            selectionId,
            authority,
            item.Authority,
            item.Route,
            item.Snapshot.Key,
            item.Snapshot.Subcompetition);

    public BundesligaPredictionRouteSelection GetRequiredSelection(
        string selectionId,
        BundesligaPredictionAuthority authority,
        BundesligaValidatedBonusItem item) =>
        GetRequiredSelection(
            selectionId,
            authority,
            item.Authority,
            item.Route,
            item.Snapshot.Key,
            item.Snapshot.Subcompetition);

    private BundesligaPredictionRouteSelection GetRequiredSelection(
        string selectionId,
        BundesligaPredictionAuthority authority,
        BundesligaPredictionAuthority itemAuthority,
        BundesligaPredictionRouteContract itemRoute,
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionId);
        ArgumentNullException.ThrowIfNull(authority);
        if (!_selections.TryGetValue(selectionId, out var selection)
            || authority != itemAuthority
            || !string.Equals(authority.CommunityContext, selection.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(authority.PostingCommunity, key.PostingCommunity, StringComparison.Ordinal)
            || selection.Route != itemRoute
            || selection.Route.ItemKind != key.ItemKind
            || selection.Route.Subcompetition != subcompetition)
        {
            throw new InvalidDataException(
                $"Selection '{selectionId}' is not the exact registered policy for the validated item and authority.");
        }

        Routes.Require(selection.Route.RouteId, selection.Route.ItemKind, selection.Route.Subcompetition);
        return selection;
    }
}
