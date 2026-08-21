using System.Globalization;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.ContextHygiene;

public sealed record ContextHygieneInventoryRow(
    string StorageKind,
    string Name,
    string Classification,
    string IntendedUse,
    string State,
    int? Version,
    string? ContentSha256,
    string? CreatedAt,
    string? PublicationSet,
    string? PublicationSnapshotId,
    string? SourceAsOf,
    string Freshness);

public sealed record ContextHygieneInventoryReport(
    string Competition,
    string CommunityContext,
    string EvaluationDate,
    int ExpectedCount,
    int PresentCount,
    int MissingCount,
    int UnexpectedCount,
    IReadOnlyList<ContextHygieneInventoryRow> Documents);

public sealed class ContextHygieneInventoryCommand : AsyncCommand<ContextHygieneInventorySettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<ContextHygieneInventoryCommand> _logger;

    public ContextHygieneInventoryCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<ContextHygieneInventoryCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        ContextHygieneInventorySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var competition = CompetitionIds.Canonicalize(settings.Competition);
            if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Context hygiene inventory supports only '{CompetitionIds.Bundesliga2026_27}'.");
            }

            var evaluationDate = string.IsNullOrWhiteSpace(settings.EvaluationDate)
                ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                    DateTimeOffset.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime)
                : DateOnly.ParseExact(
                    settings.EvaluationDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            var report = await BuildReportAsync(
                competition,
                settings.CommunityContext,
                evaluationDate,
                cancellationToken);
            if (settings.Json)
            {
                _console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            }
            else
            {
                WriteTable(report);
            }

            return 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error in context-hygiene inventory command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private async Task<ContextHygieneInventoryReport> BuildReportAsync(
        string competition,
        string communityContext,
        DateOnly evaluationDate,
        CancellationToken cancellationToken)
    {
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(competition);
        var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(competition);
        var publicationRepository = _firebaseServiceFactory.CreateDocumentPublicationRepository(competition);

        var contextNames = await contextRepository.GetContextDocumentNamesAsync(communityContext, cancellationToken);
        var contexts = new Dictionary<DocumentPublicationKey, ContextDocument>();
        foreach (var name in contextNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var document = await contextRepository.GetLatestContextDocumentAsync(name, communityContext, cancellationToken);
            if (document is not null)
            {
                contexts.Add(new DocumentPublicationKey(DocumentPublicationKind.Context, name), document);
            }
        }

        var kpis = (await kpiRepository.GetAllKpiDocumentsAsync(communityContext, cancellationToken))
            .ToDictionary(
                document => new DocumentPublicationKey(DocumentPublicationKind.Kpi, document.DocumentName),
                document => document);

        var rosterPublication = await publicationRepository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.Rosters,
            communityContext,
            cancellationToken);
        var eloPublication = await publicationRepository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.ClubElo,
            communityContext,
            cancellationToken);

        var headed = new Dictionary<DocumentPublicationKey, HeadedIdentity>();
        var sourceDates = new Dictionary<DocumentPublicationKey, SourceDateIdentity>();
        AddRosterPublication(rosterPublication, headed, sourceDates);
        AddClubEloPublication(eloPublication, headed, sourceDates);

        var expected = BundesligaContextHygienePolicy.GetExpectedDocuments(communityContext)
            .ToDictionary(entry => entry.Key, entry => entry.Use);
        var allKeys = expected.Keys
            .Concat(contexts.Keys)
            .Concat(kpis.Keys)
            .Concat(headed.Keys)
            .Distinct()
            .OrderBy(key => key.Kind)
            .ThenBy(key => key.Name, StringComparer.Ordinal)
            .ToArray();

        var rows = allKeys.Select(key => BuildRow(
                key,
                communityContext,
                expected.ContainsKey(key),
                contexts.GetValueOrDefault(key),
                kpis.GetValueOrDefault(key),
                headed.GetValueOrDefault(key),
                sourceDates.GetValueOrDefault(key),
                evaluationDate))
            .ToArray();

        return new ContextHygieneInventoryReport(
            competition,
            communityContext,
            evaluationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            expected.Count,
            rows.Count(row => row.State is "Present" or "Headed" or "UnheadedReserved"),
            rows.Count(row => row.State == "Missing"),
            rows.Count(row => row.Classification != nameof(BundesligaContextHygieneClassification.Expected)),
            rows);
    }

    private static ContextHygieneInventoryRow BuildRow(
        DocumentPublicationKey key,
        string communityContext,
        bool isExpected,
        ContextDocument? context,
        KpiDocument? kpi,
        HeadedIdentity? headed,
        SourceDateIdentity? sourceDate,
        DateOnly evaluationDate)
    {
        var assessment = BundesligaContextHygienePolicy.Assess(key.Kind, key.Name, communityContext);
        var genericVersion = context?.Version ?? kpi?.Version;
        var genericContent = context?.Content ?? kpi?.Content;
        var genericCreatedAt = context?.CreatedAt ?? kpi?.CreatedAt;
        var isReserved = BundesligaDocumentPublication.IsReserved(
            CompetitionIds.Bundesliga2026_27,
            key.Kind,
            key.Name);
        var state = headed is not null
            ? "Headed"
            : isReserved && genericVersion is not null
                ? "UnheadedReserved"
                : genericVersion is not null
                    ? "Present"
                    : isExpected
                        ? "Missing"
                        : "Absent";
        var version = headed?.Version ?? genericVersion;
        var contentHash = headed?.ContentSha256
                          ?? (genericContent is null ? null : DocumentPublicationContract.ComputeContentSha256(genericContent));
        var createdAt = headed?.CreatedAt ?? genericCreatedAt;

        return new ContextHygieneInventoryRow(
            key.Kind.ToString(),
            key.Name,
            assessment.Classification.ToString(),
            assessment.Use.ToString(),
            state,
            version,
            contentHash,
            createdAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            headed?.PublicationSet,
            headed?.SnapshotId,
            sourceDate?.Display,
            GetFreshness(key, sourceDate, evaluationDate));
    }

    private static void AddRosterPublication(
        LoadedDocumentPublication? loaded,
        IDictionary<DocumentPublicationKey, HeadedIdentity> headed,
        IDictionary<DocumentPublicationKey, SourceDateIdentity> sourceDates)
    {
        if (loaded is null)
        {
            return;
        }

        var reconstructed = BundesligaRosterPublication.ReconstructLastKnownGood(loaded);
        var memberships = reconstructed.Snapshots.ToDictionary(snapshot => snapshot.Team.TeamSlug, snapshot => snapshot.MembershipAsOf);
        var aggregateMinimumDate = reconstructed.Snapshots.Min(snapshot => snapshot.MembershipAsOf);
        var aggregateMaximumDate = reconstructed.Snapshots.Max(snapshot => snapshot.MembershipAsOf);
        var aggregateDate = new SourceDateIdentity(
            aggregateMinimumDate == aggregateMaximumDate
                ? aggregateMinimumDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : $"{aggregateMinimumDate:yyyy-MM-dd}..{aggregateMaximumDate:yyyy-MM-dd}",
            aggregateMinimumDate);
        foreach (var document in loaded.Documents)
        {
            AddHeaded(document, loaded.Snapshot, headed);
            var key = document.Key;
            if (document.Kind == DocumentPublicationKind.Context
                && document.Name.StartsWith("roster-", StringComparison.Ordinal))
            {
                var membershipDate = memberships[document.Name["roster-".Length..]];
                sourceDates[key] = new SourceDateIdentity(
                    membershipDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    membershipDate);
            }
            else
            {
                sourceDates[key] = aggregateDate;
            }
        }
    }

    private static void AddClubEloPublication(
        LoadedDocumentPublication? loaded,
        IDictionary<DocumentPublicationKey, HeadedIdentity> headed,
        IDictionary<DocumentPublicationKey, SourceDateIdentity> sourceDates)
    {
        if (loaded is null)
        {
            return;
        }

        var reconstructed = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded);
        foreach (var document in loaded.Documents)
        {
            AddHeaded(document, loaded.Snapshot, headed);
            sourceDates[document.Key] = new SourceDateIdentity(
                reconstructed.RatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reconstructed.RatedAt);
        }
    }

    private static void AddHeaded(
        PublishedDocument document,
        DocumentPublicationSnapshot snapshot,
        IDictionary<DocumentPublicationKey, HeadedIdentity> headed)
    {
        headed.Add(document.Key, new HeadedIdentity(
            document.Version,
            DocumentPublicationContract.ComputeContentSha256(document.Content),
            document.CreatedAt,
            snapshot.PublicationSet,
            snapshot.SnapshotId));
    }

    private static string GetFreshness(
        DocumentPublicationKey key,
        SourceDateIdentity? sourceDate,
        DateOnly evaluationDate)
    {
        if (sourceDate is null)
        {
            return "Unknown";
        }

        if (key.Name.StartsWith("roster-", StringComparison.Ordinal)
            || string.Equals(key.Name, BundesligaRosterPublicationContract.AggregateRosterDocumentName, StringComparison.Ordinal)
            || string.Equals(key.Name, BundesligaRosterPublicationContract.SquadSummaryDocumentName, StringComparison.Ordinal))
        {
            return BundesligaRosterPolicy.IsFreshForProductionActivation(
                sourceDate.Oldest,
                evaluationDate)
                ? "CurrentForProductionActivation"
                : "StaleForProductionActivation";
        }

        return "SourceDateReported";
    }

    private void WriteTable(ContextHygieneInventoryReport report)
    {
        _console.MarkupLine(
            $"[blue]Context hygiene inventory:[/] [yellow]{Markup.Escape(report.Competition)}/{Markup.Escape(report.CommunityContext)}[/]");
        _console.MarkupLine($"[blue]Freshness evaluation date (Europe/Berlin):[/] {report.EvaluationDate}");
        var table = new Table()
            .AddColumn("Kind")
            .AddColumn("Name")
            .AddColumn("Classification")
            .AddColumn("Use")
            .AddColumn("State")
            .AddColumn("Version")
            .AddColumn("SHA-256")
            .AddColumn("Created at")
            .AddColumn("Source as of")
            .AddColumn("Freshness")
            .AddColumn("Publication snapshot");
        foreach (var row in report.Documents)
        {
            table.AddRow(
                row.StorageKind,
                Markup.Escape(row.Name),
                row.Classification,
                row.IntendedUse,
                row.State,
                row.Version?.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.ContentSha256 ?? "-",
                row.CreatedAt ?? "-",
                row.SourceAsOf ?? "-",
                row.Freshness,
                row.PublicationSnapshotId ?? "-");
        }

        _console.Write(table);
        _console.MarkupLine(
            $"[blue]Expected:[/] {report.ExpectedCount}; [green]present:[/] {report.PresentCount}; " +
            $"[red]missing:[/] {report.MissingCount}; [yellow]unexpected:[/] {report.UnexpectedCount}");
    }

    private sealed record HeadedIdentity(
        int Version,
        string ContentSha256,
        DateTimeOffset CreatedAt,
        string PublicationSet,
        string SnapshotId);

    private sealed record SourceDateIdentity(string Display, DateOnly Oldest);
}
