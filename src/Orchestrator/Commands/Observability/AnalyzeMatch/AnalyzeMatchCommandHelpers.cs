using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using FirebaseAdapter;
using KicktippIntegration;

namespace Orchestrator.Commands.Observability.AnalyzeMatch;

internal sealed record AnalyzeMatchContextDocumentInfo(DocumentContext Document, int Version);

internal static class AnalyzeMatchCommandHelpers
{
    public static ILoggerFactory CreateLoggerFactory(bool debug)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            });
            builder.SetMinimumLevel(debug ? LogLevel.Information : LogLevel.Error);
        });
    }

    public static async Task<Match?> ResolveMatchAsync(
        AnalyzeMatchBaseSettings settings,
        IKicktippClient? kicktippClient,
        ILogger logger,
        string communityContext,
        IAnsiConsole console)
    {

        if (kicktippClient != null)
        {
            try
            {
                var matches = await kicktippClient.GetMatchesWithHistoryAsync(communityContext);
                var found = matches.FirstOrDefault(m =>
                    m.Match.Matchday == settings.Matchday &&
                    string.Equals(m.Match.HomeTeam, settings.HomeTeam, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Match.AwayTeam, settings.AwayTeam, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    console.MarkupLine("[dim]Using match metadata from Kicktipp schedule[/]");
                    return found.Match;
                }

                logger.LogWarning(
                    "Match not found via Kicktipp lookup for community {CommunityContext}, matchday {Matchday}, teams {HomeTeam} vs {AwayTeam}. Continuing with provided details.",
                    communityContext,
                    settings.Matchday,
                    settings.HomeTeam,
                    settings.AwayTeam);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch match metadata from Kicktipp; continuing with provided details");
            }
        }
        else
        {
            logger.LogWarning("Kicktipp client not configured; continuing with provided match details");
        }

        return new Match(
            settings.HomeTeam,
            settings.AwayTeam,
            default,
            settings.Matchday!.Value);
    }

    public static async Task<List<AnalyzeMatchContextDocumentInfo>> GetMatchContextDocumentsAsync(
        IContextRepository contextRepository,
        IDocumentPublicationRepository publicationRepository,
        string homeTeam,
        string awayTeam,
        string communityContext,
        bool verbose,
        IAnsiConsole console)
    {
        var match = new Match(homeTeam, awayTeam, default, 0);
        var resolved = await new BundesligaMatchContextResolver(contextRepository, publicationRepository)
            .ResolveLiveAsync(match, communityContext);
        if (verbose)
        {
            console.MarkupLine("[dim]Resolved 7 generic versioned and 4 publication-snapshot-backed Bundesliga context documents[/]");
            console.MarkupLine($"[dim]Roster snapshot: {resolved.Manifest.RosterPublicationSnapshotId}; Club Elo snapshot: {resolved.Manifest.ClubEloPublicationSnapshotId}[/]");
        }

        return resolved.ResolvedDocuments
            .Select(document => new AnalyzeMatchContextDocumentInfo(
                new DocumentContext(document.DocumentName, document.Content),
                document.Version))
            .ToList();
    }
}
