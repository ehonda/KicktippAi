using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>Publishes the atomic Bundesliga Club Elo snapshot, optionally consuming a prepared source cycle.</summary>
public sealed class CollectContextClubEloCommand : AsyncCommand<CollectContextClubEloSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IBundesligaClubEloSource _seedSource;
    private readonly ILogger<CollectContextClubEloCommand> _logger;
    private readonly IServiceProvider? _services;

    public CollectContextClubEloCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IBundesligaClubEloSource seedSource,
        ILogger<CollectContextClubEloCommand> logger,
        IServiceProvider? services = null)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _seedSource = seedSource;
        _logger = logger;
        _services = services;
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

            var preparation = ContextSourceCyclePreparation.Current;
            var observation = preparation?.Files.Bundle.Observations.SingleOrDefault(value => value.Source == BundesligaContextSource.ClubElo);
            ContextSourceCycleCoordinator? coordinator = null;
            if (observation is not null)
            {
                preparation!.Files.Validate();
                if (preparation.Files.Bundle.Cycle.Competition != competition)
                    throw new InvalidDataException("Prepared Club Elo competition contradicts the command.");
                BundesligaContextSourceContract.ValidateConsumerAuthority(preparation.Files.Bundle.Cycle.Scope, preparation.CurrentLaneId, communityContext);
                if (!settings.DryRun)
                {
                    if (preparation.PersistedCycle is null)
                        throw new InvalidDataException("A Club Elo publication requires a persisted preparation.");
                    coordinator = _services?.GetService(typeof(ContextSourceCycleCoordinator)) as ContextSourceCycleCoordinator
                        ?? throw new InvalidOperationException("Prepared Club Elo receipt completion requires its coordinator.");
                    if (preparation.TryGetPersistedReceipt(BundesligaContextSource.ClubElo, out var receipt))
                    {
                        await coordinator.CompletePreparedReceiptAsync(preparation, receipt!.Request, cancellationToken);
                        _console.MarkupLine($"[green]✓ Club Elo receipt replay {receipt.Request.PublicationDisposition}[/]");
                        return 0;
                    }
                }
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
                DocumentPublicationContract.ValidateLoaded(competition, communityContext, BundesligaDocumentPublication.ClubElo, loaded.Snapshot, loaded.Documents);
                lastKnownGood = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded);
            }

            var selection = observation is null ? BundesligaClubEloPolicy.Select(
                seedResult.Snapshot,
                lastKnownGood,
                networkCandidate: null,
                unattendedNetworkUseAllowed: false)
                : BundesligaClubEloRefresh.Select(observation, lastKnownGood ?? seedResult.Snapshot);
            var publication = observation is not null && selection.Disposition == BundesligaClubEloSelectionDisposition.NetworkAccepted
                ? BundesligaClubEloPublication.BuildSourceBacked(selection, preparation!.Files.Bundle.Cycle, observation,
                    preparation.Files.Payloads[observation.Payload!.Path])
                : BundesligaClubEloPublication.Build(selection);
            var commit = observation is null ? null : CreateCommit(preparation!, observation, communityContext, selection);
            var request = new DocumentPublicationRequest(communityContext, loaded?.Snapshot.SnapshotId,
                publication.Documents, publication.MetadataJson, commit);
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

            if (observation is not null && selection.Selected.Origin == BundesligaClubEloSnapshotOrigin.LastKnownGood)
            {
                if (targetSnapshotId != loaded!.Snapshot.SnapshotId)
                    throw new InvalidDataException("Retained Club Elo selection contradicts the verified head.");
                var receipt = CreateReceipt(commit!, loaded.Snapshot.SnapshotId, BundesligaContextSourcePublicationDisposition.NotAttempted);
                await coordinator!.CompletePreparedReceiptAsync(preparation!, receipt, cancellationToken);
                _console.MarkupLine("[green]✓ Club Elo publication NotAttempted (retained verified head)[/]");
                activity?.SetTag("club_elo.publication_disposition", "NotAttempted");
                return 0;
            }

            var result = await publicationRepository.PublishAsync(
                BundesligaDocumentPublication.ClubElo,
                request,
                cancellationToken);
            if (observation is not null)
            {
                var disposition = result.Disposition switch
                {
                    DocumentPublicationDisposition.Published => BundesligaContextSourcePublicationDisposition.Published,
                    DocumentPublicationDisposition.Unchanged => BundesligaContextSourcePublicationDisposition.Unchanged,
                    DocumentPublicationDisposition.Reactivated => BundesligaContextSourcePublicationDisposition.Reactivated,
                    _ => throw new InvalidDataException("Unknown Club Elo publication outcome.")
                };
                await coordinator!.CompletePreparedReceiptAsync(preparation!, CreateReceipt(commit!, result.Snapshot.SnapshotId, disposition), cancellationToken);
            }
            _console.MarkupLine($"[green]✓ Club Elo publication {result.Disposition}[/]");
            _console.MarkupLine($"[green]  Snapshot: {result.Snapshot.SnapshotId}[/]");
            activity?.SetTag("club_elo.publication_disposition", result.Disposition.ToString());
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            activity?.SetTag("club_elo.publication_disposition", "Failed");
            _logger.LogError(exception, "Error executing collect-context club-elo command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private static ContextSourcePublicationCommitRequest CreateCommit(ContextSourceCyclePreparation preparation,
        BundesligaContextSourceObservation observation, string community, BundesligaClubEloSelection selection)
    {
        var cycle = preparation.Files.Bundle.Cycle;
        var conditions = new List<BundesligaContextSourceHealthCondition>();
        if (observation.Disposition == BundesligaContextSourceDisposition.Rejected)
        {
            conditions.Add(BundesligaContextSourceHealthCondition.AcquisitionFailed);
            conditions.Add(BundesligaContextSourceHealthCondition.ClubEloSourceRejected);
        }
        if (DateOnly.FromDateTime(preparation.Files.Bundle.StalenessReferenceAtUtc.UtcDateTime).DayNumber - selection.Selected.RatedAt.DayNumber > 7)
            conditions.Add(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        var guard = new ContextSourcePublicationGuard(cycle.Competition, cycle.Scope, cycle.CycleId,
            BundesligaContextSource.ClubElo, preparation.CurrentLaneId, community, BundesligaDocumentPublication.ClubEloPublicationSet,
            preparation.Files.Digest, observation.ObservationDigest, cycle.Sequence, cycle.CycleId);
        var template = new ContextSourcePublicationReceiptTemplate(selection.Disposition switch
        {
            BundesligaClubEloSelectionDisposition.NetworkAccepted => BundesligaContextSourceSelectionDisposition.NetworkAccepted,
            BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer => BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer,
            BundesligaClubEloSelectionDisposition.NetworkCandidateStale => BundesligaContextSourceSelectionDisposition.NetworkCandidateStale,
            BundesligaClubEloSelectionDisposition.NetworkCandidateRejected => BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected,
            _ => throw new InvalidDataException("Prepared source cannot have a disabled selection.")
        }, selection.Selected.Origin switch
        {
            BundesligaClubEloSnapshotOrigin.NetworkCandidate => BundesligaContextSourceSelectedOrigin.NetworkCandidate,
            BundesligaClubEloSnapshotOrigin.LaunchSeed => BundesligaContextSourceSelectedOrigin.LaunchSeed,
            BundesligaClubEloSnapshotOrigin.LastKnownGood => BundesligaContextSourceSelectedOrigin.LastKnownGood,
            _ => throw new InvalidDataException("Unknown Club Elo origin.")
        }, new BundesligaContextSourceDates(selection.Selected.RatedAt, null, null, null), null,
            new BundesligaContextSourceCarriedFields(0, 0, 0, null), BundesligaContextSourceHealth.OrderConditions(conditions));
        return new(guard, template);
    }

    private static BundesligaContextSourceReceiptRequest CreateReceipt(ContextSourcePublicationCommitRequest commit,
        string snapshotId, BundesligaContextSourcePublicationDisposition disposition)
    {
        var guard = commit.Guard;
        var template = commit.ReceiptTemplate;
        return new(guard.Identity, guard.Source, guard.ConsumerLaneId, guard.CommunityContext, guard.ObservationDigest,
            guard.BundleDigest, template.SelectionDisposition, snapshotId, template.SelectedOrigin, disposition,
            template.SourceDates, template.RosterRevision, template.CarriedFields, template.ActiveConditions);
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
