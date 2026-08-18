using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>Publishes the atomic seed-backed Bundesliga Club Elo prompt snapshot.</summary>
public sealed class CollectContextClubEloCommand : AsyncCommand<CollectContextClubEloSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IBundesligaClubEloSource _seedSource;
    private readonly ILogger<CollectContextClubEloCommand> _logger;

    public CollectContextClubEloCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IBundesligaClubEloSource seedSource,
        ILogger<CollectContextClubEloCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _seedSource = seedSource;
        _logger = logger;
    }

    protected override Task<int> ExecuteAsync(CommandContext context, CollectContextClubEloSettings settings, CancellationToken cancellationToken) =>
        ExecuteWithSettingsAsync(settings, cancellationToken);

    internal async Task<int> ExecuteWithSettingsAsync(CollectContextClubEloSettings settings, CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Source.StartActivity("collect-context-club-elo");
        activity?.SetTag("club_elo.dry_run", settings.DryRun);
        try
        {
            if (string.IsNullOrWhiteSpace(settings.CommunityContext) || string.IsNullOrWhiteSpace(settings.Competition))
            {
                _console.MarkupLine("[red]Error: Explicit --community-context and --competition are required[/]");
                return 1;
            }

            var communityContext = settings.CommunityContext.Trim();
            var competition = CompetitionResolver.ResolveCompetition(settings.Competition, communityContext, communityContext);
            if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                _console.MarkupLine("[red]Error: collect-context club-elo only supports bundesliga-2026-27[/]");
                return 1;
            }

            _console.MarkupLine("[green]Collect-context Club Elo command initialized[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }

            var seedResult = await LoadSeedAsync(settings.Seed, cancellationToken);
            if (!seedResult.IsComplete || seedResult.Snapshot is null)
            {
                throw new InvalidDataException($"Club Elo launch seed was rejected: {string.Join(", ", seedResult.Diagnostics)}.");
            }

            var publicationRepository = _firebaseServiceFactory.CreateDocumentPublicationRepository(competition);
            var loaded = await publicationRepository.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.ClubElo, communityContext, cancellationToken);
            BundesligaClubEloSnapshot? lastKnownGood = null;
            if (loaded is not null)
            {
                // A corrupt headed set is never treated as absence: publishing over it would hide a
                // damaged LKG instead of preserving it for investigation.
                lastKnownGood = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded);
            }

            var selection = BundesligaClubEloPolicy.Select(
                seedResult.Snapshot,
                lastKnownGood,
                networkCandidate: null,
                unattendedNetworkUseAllowed: false);
            var publication = BundesligaClubEloPublication.Build(selection);
            var request = BundesligaClubEloPublication.CreateRequest(
                communityContext,
                loaded?.Snapshot.SnapshotId,
                publication);
            // Dry-run deliberately executes the same Core request validation and content hashing
            // as a real publication, while stopping before the repository write boundary.
            DocumentPublicationContract.ValidateRequest(competition, BundesligaDocumentPublication.ClubElo, request);
            var targetSnapshotId = DocumentPublicationContract.ComputeSnapshotId(request.Documents);
            var ageDays = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - selection.Selected.RatedAt.DayNumber;
            activity?.SetTag("club_elo.origin", selection.Selected.Origin.ToString());
            activity?.SetTag("club_elo.selection_disposition", selection.Disposition.ToString());
            activity?.SetTag("club_elo.rated_at", selection.Selected.RatedAt.ToString("yyyy-MM-dd"));
            activity?.SetTag("club_elo.collected_at", selection.Selected.CollectedAt.ToString("O"));
            activity?.SetTag("club_elo.source_url", selection.Selected.SourceUrl.AbsoluteUri);
            activity?.SetTag("club_elo.age_days", ageDays);
            activity?.SetTag("club_elo.mapping_coverage", selection.Selected.Entries.Count);
            activity?.SetTag("club_elo.previous_snapshot_id", loaded?.Snapshot.SnapshotId ?? "");
            activity?.SetTag("club_elo.target_snapshot_id", targetSnapshotId);
            activity?.SetTag("club_elo.diagnostics", string.Join(",", selection.Diagnostics));

            _console.MarkupLine($"[blue]Selected origin:[/] [yellow]{selection.Selected.Origin}[/]");
            _console.MarkupLine($"[blue]Selection disposition:[/] [yellow]{selection.Disposition}[/]");
            _console.MarkupLine($"[blue]Rated at:[/] [yellow]{selection.Selected.RatedAt:yyyy-MM-dd}[/] ([yellow]{ageDays}[/] days old)");
            _console.MarkupLine($"[blue]Collected at:[/] [yellow]{selection.Selected.CollectedAt:O}[/]");
            _console.MarkupLine($"[blue]Source URL:[/] [yellow]{Markup.Escape(selection.Selected.SourceUrl.AbsoluteUri)}[/]");
            _console.MarkupLine($"[blue]Mapped manifest teams:[/] [yellow]{selection.Selected.Entries.Count}/{BundesligaTeamManifest.ExpectedTeamCount}[/]");
            _console.MarkupLine($"[blue]Last-known-good snapshot:[/] [yellow]{loaded?.Snapshot.SnapshotId ?? "<none>"}[/]");
            _console.MarkupLine($"[blue]Rendered target snapshot:[/] [yellow]{targetSnapshotId}[/]");
            foreach (var diagnostic in selection.Diagnostics)
            {
                _console.MarkupLine($"[dim]Diagnostic: {Markup.Escape(diagnostic)}[/]");
            }

            if (settings.DryRun)
            {
                foreach (var document in publication.Documents)
                {
                    _console.MarkupLine($"[magenta]Dry run - would publish {document.Kind} document:[/] {Markup.Escape(document.Name)}");
                }

                _console.MarkupLine("[magenta]✓ Dry run completed - no documents were written[/]");
                activity?.SetTag("club_elo.publication_disposition", "DryRun");
                return 0;
            }

            var result = await publicationRepository.PublishAsync(
                BundesligaDocumentPublication.ClubElo,
                request,
                cancellationToken);
            _console.MarkupLine($"[green]✓ Club Elo publication {result.Disposition}[/]");
            _console.MarkupLine($"[green]  Snapshot: {result.Snapshot.SnapshotId}[/]");
            activity?.SetTag("club_elo.publication_disposition", result.Disposition.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            activity?.SetTag("club_elo.publication_disposition", "Failed");
            _logger.LogError(exception, "Error executing collect-context club-elo command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private async Task<BundesligaClubEloSourceResult> LoadSeedAsync(string seedPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seedPath) || string.Equals(seedPath, BundesligaClubEloSeed.RelativePath, StringComparison.Ordinal))
        {
            return await _seedSource.GetLatestAsync(cancellationToken);
        }

        var bytes = await File.ReadAllBytesAsync(seedPath, cancellationToken);
        return BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Parse(bytes));
    }
}
