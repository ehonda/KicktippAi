using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.BundesligaHistory;

public sealed class BundesligaHistoryExportInventoryCommand : AsyncCommand<BundesligaHistoryExportInventorySettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseFactory;
    private readonly IKicktippClientFactory _kicktippFactory;
    private readonly IContextProviderFactory _providerFactory;
    private readonly ILogger<BundesligaHistoryExportInventoryCommand> _logger;

    public BundesligaHistoryExportInventoryCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseFactory,
        IKicktippClientFactory kicktippFactory, IContextProviderFactory providerFactory,
        ILogger<BundesligaHistoryExportInventoryCommand> logger)
    {
        _console = console; _firebaseFactory = firebaseFactory; _kicktippFactory = kicktippFactory;
        _providerFactory = providerFactory; _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BundesligaHistoryExportInventorySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var documents = settings.FromKicktipp
                ? await CollectFromKicktippAsync(settings, cancellationToken)
                : await BundesligaHistoryCommandSupport.LoadStoredDocumentsAsync(
                    _firebaseFactory.CreateContextRepository(settings.Competition), settings.CommunityContext, cancellationToken);
            if (documents.Count == 0)
            {
                _console.MarkupLine("[red]No selected Bundesliga history documents found[/]");
                return 1;
            }

            var inventory = BundesligaHistoryPlayedDateCollector.ExportInventory(documents);
            var directory = Path.GetDirectoryName(settings.Output);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(settings.Output, BundesligaHistoryPlayedDateMap.Write(inventory.Entries), cancellationToken);
            _console.MarkupLine($"[green]Exported {inventory.Entries.Count} exact completed history row identities from {documents.Count} documents to {settings.Output}[/]");
            _console.MarkupLine($"[dim]Excluded {inventory.ExcludedIncompleteRowCount} incomplete row(s) because a completed score is required for selected history.[/]");
            _console.MarkupLine("[dim]The export is an inventory, not accepted date evidence; populate and strictly audit every provenance field before replacing the canonical map.[/]");
            return 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to export Bundesliga history inventory");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private async Task<IReadOnlyList<BundesligaHistoryDocument>> CollectFromKicktippAsync(
        BundesligaHistoryExportInventorySettings settings, CancellationToken cancellationToken)
    {
        _console.MarkupLine("[blue]Read-only Kicktipp inventory mode; no Firestore or Kicktipp writes will be made[/]");
        var client = _kicktippFactory.CreateClient();
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var matchday in BundesligaHistoryCommandSupport.ParseMatchdays(settings.Matchdays))
        {
            var provider = _providerFactory.CreateKicktippContextProvider(client, settings.CommunityContext,
                settings.Competition, settings.CommunityContext, matchday);
            var matches = await client.GetMatchesWithHistoryAsync(settings.CommunityContext, matchday, settings.Competition);
            foreach (var matchWithHistory in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var match = matchWithHistory.Match;
                var historyDocuments = new[]
                {
                    await provider.RecentHistory(match.HomeTeam),
                    await provider.RecentHistory(match.AwayTeam),
                    await provider.HomeHistory(match.HomeTeam, match.AwayTeam),
                    await provider.AwayHistory(match.HomeTeam, match.AwayTeam)
                };
                foreach (var document in historyDocuments)
                {
                    documents.TryAdd(document.Name, document.Content);
                }
            }
        }
        return documents.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new BundesligaHistoryDocument(pair.Key, pair.Value)).ToArray();
    }
}
