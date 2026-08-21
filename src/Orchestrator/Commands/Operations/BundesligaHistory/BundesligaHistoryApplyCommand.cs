using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.BundesligaHistory;

public sealed class BundesligaHistoryApplyCommand : AsyncCommand<BundesligaHistoryApplySettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseFactory;
    private readonly IBundesligaHistoryPlayedDateCollector _collector;
    private readonly ILogger<BundesligaHistoryApplyCommand> _logger;

    public BundesligaHistoryApplyCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseFactory,
        IBundesligaHistoryPlayedDateCollector collector, ILogger<BundesligaHistoryApplyCommand> logger)
    {
        _console = console; _firebaseFactory = firebaseFactory; _collector = collector; _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BundesligaHistoryApplySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = _firebaseFactory.CreateContextRepository(settings.Competition);
            var documents = await BundesligaHistoryCommandSupport.LoadStoredDocumentsAsync(repository, settings.CommunityContext, cancellationToken);
            if (documents.Count == 0) throw new InvalidOperationException("No selected Bundesliga history documents found");
            var outcomes = await BundesligaHistoryCommandSupport.LoadOutcomesAsync(
                _firebaseFactory.CreateMatchOutcomeRepository(settings.Competition), settings.CommunityContext, cancellationToken);
            var result = _collector.Collect(settings.Competition, documents,
                BundesligaHistoryCommandSupport.ReadMap(settings.Input).Entries, outcomes);
            if (!result.Succeeded)
            {
                _console.MarkupLine($"[red]Strict apply gate failed with {result.Diagnostics.Count} diagnostic(s); no documents were written[/]");
                foreach (var diagnostic in result.Diagnostics.Take(50))
                    _console.MarkupLine($"[red]  {Markup.Escape(diagnostic.DocumentName)}#{diagnostic.RowOrdinal?.ToString() ?? "-"}: {Markup.Escape(diagnostic.Message)}[/]");
                return 1;
            }

            var changed = result.Documents.Zip(documents).Where(pair => pair.First.Content != pair.Second.Content).ToArray();
            var sourceEvidence = $"Sources: existing={result.PreservedCount}, Kicktipp={result.KicktippCount}, fixed-map={result.FixedMapCount}; excluded-incomplete={result.ExcludedIncompleteRowCount}; fixed-map sources: {Markup.Escape(BundesligaHistoryCommandSupport.FormatFixedSourceCounts(result))}";
            if (settings.DryRun)
            {
                _console.MarkupLine($"[magenta]Strict dry-run passed; {changed.Length} document(s) would be saved and no writes were made[/]");
                _console.MarkupLine($"[dim]{sourceEvidence}[/]");
                return 0;
            }

            foreach (var pair in changed)
                await repository.SaveContextDocumentAsync(pair.First.Name, pair.First.Content, settings.CommunityContext, cancellationToken);
            _console.MarkupLine($"[green]Strict apply completed; saved {changed.Length} document(s)[/]");
            _console.MarkupLine($"[dim]{sourceEvidence}[/]");
            return 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to apply Bundesliga history dates");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }
}
