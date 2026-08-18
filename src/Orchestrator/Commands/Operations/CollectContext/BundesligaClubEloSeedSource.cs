using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Deliberately local source used until the owner accepts unattended Club Elo network reuse.
/// A later HTTP provider can implement the Core source boundary without changing the command.
/// </summary>
internal sealed class BundesligaClubEloSeedSource : IBundesligaClubEloSource
{
    public Task<BundesligaClubEloSourceResult> GetLatestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Default));
}
