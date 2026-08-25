using System.Globalization;
using System.Security.Cryptography;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>Publishes one complete quality-gated Bundesliga roster snapshot.</summary>
public sealed class CollectContextRostersCommand : AsyncCommand<CollectContextRostersSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IBundesligaRosterSource _source;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CollectContextRostersCommand> _logger;

    public CollectContextRostersCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseServiceFactory,
        IBundesligaRosterSource source, TimeProvider timeProvider, ILogger<CollectContextRostersCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _source = source;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override Task<int> ExecuteAsync(CommandContext context, CollectContextRostersSettings settings, CancellationToken cancellationToken) =>
        ExecuteWithSettingsAsync(settings, cancellationToken);

    internal async Task<int> ExecuteWithSettingsAsync(CollectContextRostersSettings settings, CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Source.StartActivity("collect-context-rosters");
        activity?.SetTag("rosters.dry_run", settings.DryRun);
        try
        {
            if (string.IsNullOrWhiteSpace(settings.CommunityContext) || string.IsNullOrWhiteSpace(settings.Competition))
            {
                _console.MarkupLine("[red]Error: Explicit --community-context and --competition are required[/]");
                return 1;
            }
            var community = settings.CommunityContext.Trim();
            var competition = CompetitionResolver.ResolveCompetition(settings.Competition, community, community);
            if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                _console.MarkupLine("[red]Error: collect-context rosters only supports bundesliga-2026-27[/]");
                return 1;
            }
            if (!TryGetSnapshotDate(settings, out var snapshotDate, out var inputError))
            {
                _console.MarkupLine($"[red]Error: {Markup.Escape(inputError)}[/]");
                return 1;
            }
            if (!TryValidateDuckDbFile(settings, out inputError))
            {
                _console.MarkupLine($"[red]Error: {Markup.Escape(inputError)}[/]");
                return 1;
            }

            _console.MarkupLine("[green]Collect-context Bundesliga roster command initialized[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(community)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
            _console.MarkupLine($"[blue]Using roster seed:[/] [yellow]{Markup.Escape(settings.Seed)}[/]");
            _console.MarkupLine($"[blue]Using team manifest:[/] [yellow]{Markup.Escape(settings.Manifest)}[/]");
            _console.MarkupLine($"[blue]DuckDB input:[/] [yellow]{Markup.Escape(settings.DuckDbPath ?? "<not supplied>")}[/]");
            if (settings.DryRun) _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");

            var repository = _firebaseServiceFactory.CreateDocumentPublicationRepository(competition);
            var loaded = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community, cancellationToken);
            BundesligaRosterLastKnownGood? lkg = loaded is null ? null : BundesligaRosterPublication.ReconstructLastKnownGood(loaded);
            var collection = await _source.CollectAsync(new BundesligaRosterSourceRequest(settings.Seed, settings.Manifest, settings.DuckDbPath,
                settings.DuckDbRevision, snapshotDate), lkg, DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
            BundesligaRosterCoverage? launchCoverage = null;
            if (settings.RequireLaunchCoverage)
            {
                launchCoverage = BundesligaRosterLaunchCoverage.Validate(collection.Snapshots);
                _console.MarkupLine(
                    $"[green]Launch roster coverage passed:[/] ages {launchCoverage.KnownAgeCount}/{BundesligaRosterLaunchCoverage.RequiredKnownAgeCount}, " +
                    $"positions {launchCoverage.KnownPositionCount}/{BundesligaRosterLaunchCoverage.RequiredKnownPositionCount}, " +
                    $"valued {launchCoverage.ValuedPlayerCount}/{BundesligaRosterLaunchCoverage.RequiredValuedPlayerCount}");
            }
            activity?.SetTag("rosters.launch_coverage_required", settings.RequireLaunchCoverage);
            activity?.SetTag("rosters.launch_known_age_count", launchCoverage?.KnownAgeCount ?? 0);
            activity?.SetTag("rosters.launch_known_position_count", launchCoverage?.KnownPositionCount ?? 0);
            activity?.SetTag("rosters.launch_valued_player_count", launchCoverage?.ValuedPlayerCount ?? 0);
            if (collection.RetainLastKnownGood)
            {
                _console.MarkupLine("[yellow]Retained the exact headed last-known-good roster snapshot; no publication was attempted[/]");
                foreach (var diagnostic in collection.Diagnostics) _console.MarkupLine($"[yellow]Diagnostic: {Markup.Escape(diagnostic)}[/]");
                activity?.SetTag("rosters.publication_disposition", "RetainedLastKnownGood");
                activity?.SetTag("rosters.diagnostics", string.Join(",", collection.Diagnostics));
                return 0;
            }
            var publication = BundesligaRosterPublication.Build(collection.Snapshots, collection.QualityRows);
            var request = BundesligaRosterPublication.CreateRequest(community, loaded?.Snapshot.SnapshotId, publication);
            DocumentPublicationContract.ValidateRequest(competition, BundesligaDocumentPublication.Rosters, request);
            var targetSnapshot = DocumentPublicationContract.ComputeSnapshotId(request.Documents);

            activity?.SetTag("rosters.previous_snapshot_id", loaded?.Snapshot.SnapshotId ?? "");
            activity?.SetTag("rosters.target_snapshot_id", targetSnapshot);
            activity?.SetTag("rosters.club_count", collection.Snapshots.Count);
            activity?.SetTag("rosters.duckdb_path_supplied", !string.IsNullOrWhiteSpace(settings.DuckDbPath));
            activity?.SetTag("rosters.duckdb_revision", settings.DuckDbRevision ?? "");
            activity?.SetTag("rosters.duckdb_snapshot_date", snapshotDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
            activity?.SetTag("rosters.diagnostics", string.Join(",", collection.Diagnostics));
            _console.MarkupLine($"[blue]Last-known-good snapshot:[/] [yellow]{loaded?.Snapshot.SnapshotId ?? "<none>"}[/]");
            _console.MarkupLine($"[blue]Rendered target snapshot:[/] [yellow]{targetSnapshot}[/]");
            foreach (var row in collection.QualityRows)
            {
                _console.MarkupLine($"[blue]{Markup.Escape(row.Team.TeamSlug)}[/]: [yellow]{row.SelectedSource}[/], {Markup.Escape(row.SelectionReason)}, diagnostics: {Markup.Escape(row.Diagnostics.Count == 0 ? "NONE" : string.Join(';', row.Diagnostics))}");
            }

            if (settings.DryRun)
            {
                foreach (var document in publication.Documents) _console.MarkupLine($"[magenta]Dry run - would publish {document.Kind} document:[/] {Markup.Escape(document.Name)}");
                _console.MarkupLine("[magenta]✓ Dry run completed - selection, validation, hashing, and diagnostics ran without writes[/]");
                activity?.SetTag("rosters.publication_disposition", "DryRun");
                return 0;
            }

            var result = await repository.PublishAsync(BundesligaDocumentPublication.Rosters, request, cancellationToken);
            _console.MarkupLine($"[green]✓ Bundesliga roster publication {result.Disposition}[/]");
            _console.MarkupLine($"[green]  Snapshot: {result.Snapshot.SnapshotId}[/]");
            activity?.SetTag("rosters.publication_disposition", result.Disposition.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            activity?.SetTag("rosters.publication_disposition", "Failed");
            _logger.LogError(exception, "Error executing collect-context rosters command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private static bool TryGetSnapshotDate(CollectContextRostersSettings settings, out DateOnly? result, out string error)
    {
        result = null;
        error = string.Empty;
        var hasPath = !string.IsNullOrWhiteSpace(settings.DuckDbPath);
        var hasRevision = !string.IsNullOrWhiteSpace(settings.DuckDbRevision);
        var hasDate = !string.IsNullOrWhiteSpace(settings.DuckDbSnapshotDate);
        var hasSha256 = !string.IsNullOrWhiteSpace(settings.DuckDbSha256);
        if (!hasPath && (hasRevision || hasDate || hasSha256)) { error = "--duckdb-revision, --duckdb-snapshot-date, and --duckdb-sha256 require --duckdb-path"; return false; }
        if (hasPath && (!hasRevision || !hasDate)) { error = "--duckdb-path requires --duckdb-revision and --duckdb-snapshot-date"; return false; }
        if (settings.RequireLaunchCoverage && (!hasPath || !hasSha256)) { error = "--require-launch-coverage requires --duckdb-path, provenance, and --duckdb-sha256"; return false; }
        if (hasSha256 && !IsLowerSha256(settings.DuckDbSha256!)) { error = "--duckdb-sha256 must be 64 lower-case hexadecimal characters"; return false; }
        var date = default(DateOnly);
        if (hasDate && !DateOnly.TryParseExact(settings.DuckDbSnapshotDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) { error = "--duckdb-snapshot-date must use yyyy-MM-dd"; return false; }
        if (hasDate) result = date;
        return true;
    }

    private static bool TryValidateDuckDbFile(CollectContextRostersSettings settings, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(settings.DuckDbSha256))
        {
            return true;
        }

        var path = Path.GetFullPath(settings.DuckDbPath!);
        if (!File.Exists(path))
        {
            error = $"DuckDB file not found for SHA-256 validation: {path}";
            return false;
        }

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(actual, settings.DuckDbSha256, StringComparison.Ordinal))
        {
            error = $"DuckDB SHA-256 mismatch: expected {settings.DuckDbSha256}, actual {actual}";
            return false;
        }

        return true;
    }

    private static bool IsLowerSha256(string value) => value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
