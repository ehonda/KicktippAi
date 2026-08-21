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
            var dateMap = BundesligaHistoryCommandSupport.ReadMap(settings.Input).Entries;
            var expectedDocumentNames = dateMap.Select(entry => entry.DocumentName).ToHashSet(StringComparer.Ordinal);
            var result = _collector.Collect(settings.Competition, documents, dateMap, outcomes, expectedDocumentNames);
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

            var saveResults = await repository.SaveContextDocumentsAtomicallyAsync(
                result.Documents
                    .OrderBy(document => document.Name, StringComparer.Ordinal)
                    .Select(document => new ContextDocumentWrite(document.Name, document.Content))
                    .ToArray(),
                settings.CommunityContext,
                cancellationToken);
            var savedCount = saveResults.Count(saveResult => saveResult.Version.HasValue);
            _console.MarkupLine($"[green]Strict apply completed; atomically saved {savedCount} document(s)[/]");
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
