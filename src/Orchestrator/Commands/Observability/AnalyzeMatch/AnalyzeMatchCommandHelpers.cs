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
        string homeTeam,
        string awayTeam,
        string communityContext,
        bool verbose,
        IAnsiConsole console)
    {
        var contextDocuments = new List<AnalyzeMatchContextDocumentInfo>();
        var homeAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(
            homeTeam,
            CompetitionIds.Bundesliga2026_27);
        var awayAbbreviation = MatchContextDocumentCatalog.GetTeamAbbreviation(
            awayTeam,
            CompetitionIds.Bundesliga2026_27);

        var requiredDocuments = new[]
        {
            "bundesliga-standings.csv",
            $"community-rules-{communityContext}.md",
            $"recent-history-{homeAbbreviation}.csv",
            $"recent-history-{awayAbbreviation}.csv",
            $"home-history-{homeAbbreviation}.csv",
            $"away-history-{awayAbbreviation}.csv",
            $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv"
        };

        var optionalDocuments = new[]
        {
            $"{homeAbbreviation}-transfers.csv",
            $"{awayAbbreviation}-transfers.csv"
        };

        if (verbose)
        {
            console.MarkupLine($"[dim]Looking for {requiredDocuments.Length} required context documents in database[/]");
        }

        foreach (var documentName in requiredDocuments)
        {
            var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
            if (contextDoc != null)
            {
                contextDocuments.Add(new AnalyzeMatchContextDocumentInfo(new DocumentContext(contextDoc.DocumentName, contextDoc.Content), contextDoc.Version));

                if (verbose)
                {
                    console.MarkupLine($"[dim]  ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                }
            }
            else if (verbose)
            {
                console.MarkupLine($"[dim]  ✗ Missing {documentName}[/]");
            }
        }

        foreach (var documentName in optionalDocuments)
        {
            try
            {
                var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                if (contextDoc != null)
                {
                    contextDocuments.Add(new AnalyzeMatchContextDocumentInfo(new DocumentContext(contextDoc.DocumentName, contextDoc.Content), contextDoc.Version));

                    if (verbose)
                    {
                        console.MarkupLine($"[dim]  ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                    }
                }
                else if (verbose)
                {
                    console.MarkupLine($"[dim]  · Missing optional {documentName}[/]");
                }
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    console.MarkupLine($"[dim]  · Failed optional {documentName}: {Markup.Escape(ex.Message)}[/]");
                }
            }
        }

        return contextDocuments;
    }
}
