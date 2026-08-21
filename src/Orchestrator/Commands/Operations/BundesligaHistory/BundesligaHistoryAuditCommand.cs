using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.BundesligaHistory;

public sealed class BundesligaHistoryAuditCommand : AsyncCommand<BundesligaHistoryAuditSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseFactory;
    private readonly IBundesligaHistoryPlayedDateCollector _collector;
    private readonly ILogger<BundesligaHistoryAuditCommand> _logger;

    public BundesligaHistoryAuditCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseFactory,
        IBundesligaHistoryPlayedDateCollector collector, ILogger<BundesligaHistoryAuditCommand> logger)
    {
        _console = console; _firebaseFactory = firebaseFactory; _collector = collector; _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BundesligaHistoryAuditSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await AuditAsync(settings, cancellationToken);
            Print(result, settings.Verbose);
            return result.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to audit Bundesliga history dates");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    internal async Task<BundesligaHistoryPlayedDateCollectionResult> AuditAsync(
        BundesligaHistoryAuditSettings settings, CancellationToken cancellationToken)
    {
        var context = _firebaseFactory.CreateContextRepository(settings.Competition);
        var documents = await BundesligaHistoryCommandSupport.LoadStoredDocumentsAsync(context, settings.CommunityContext, cancellationToken);
        if (documents.Count == 0) throw new InvalidOperationException("No selected Bundesliga history documents found");
        var outcomes = await BundesligaHistoryCommandSupport.LoadOutcomesAsync(
            _firebaseFactory.CreateMatchOutcomeRepository(settings.Competition), settings.CommunityContext, cancellationToken);
        return _collector.Collect(settings.Competition, documents,
            BundesligaHistoryCommandSupport.ReadMap(settings.Input).Entries, outcomes);
    }

    internal void Print(BundesligaHistoryPlayedDateCollectionResult result, bool verbose)
    {
        if (!result.Succeeded)
        {
            _console.MarkupLine($"[red]Strict audit failed with {result.Diagnostics.Count} diagnostic(s); stored documents were not changed[/]");
            foreach (var diagnostic in result.Diagnostics.Take(50))
                _console.MarkupLine($"[red]  {Markup.Escape(diagnostic.DocumentName)}#{diagnostic.RowOrdinal?.ToString() ?? "-"}: {Markup.Escape(diagnostic.Message)}[/]");
            return;
        }
        _console.MarkupLine($"[green]Strict audit passed for {result.Documents.Count} document(s) and {result.Resolutions.Count} row(s)[/]");
        _console.MarkupLine($"[dim]Sources: existing={result.PreservedCount}, Kicktipp={result.KicktippCount}, fixed-map={result.FixedMapCount}; excluded-incomplete={result.ExcludedIncompleteRowCount}; fixed-map sources: {Markup.Escape(BundesligaHistoryCommandSupport.FormatFixedSourceCounts(result))}[/]");
        if (verbose)
            foreach (var resolution in result.Resolutions)
                _console.MarkupLine($"[dim]  {Markup.Escape(resolution.DocumentName)}#{resolution.RowOrdinal}: {resolution.PlayedAt} via {resolution.SourceClass} ({Markup.Escape(resolution.SourceIdentity)})[/]");
    }
}
