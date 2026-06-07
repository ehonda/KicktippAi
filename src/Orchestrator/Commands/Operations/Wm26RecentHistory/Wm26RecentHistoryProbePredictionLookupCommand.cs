using System.Globalization;
using Microsoft.Extensions.Logging;
using NodaTime;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Wm26RecentHistory;

public sealed class Wm26RecentHistoryProbePredictionLookupCommand
    : AsyncCommand<Wm26RecentHistoryProbePredictionLookupSettings>
{
    private static readonly DateTimeZone BerlinTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<Wm26RecentHistoryProbePredictionLookupCommand> _logger;

    public Wm26RecentHistoryProbePredictionLookupCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<Wm26RecentHistoryProbePredictionLookupCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Wm26RecentHistoryProbePredictionLookupSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var competition = CompetitionResolver.ResolveCompetition(
                settings.Competition,
                communityContext: settings.CommunityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);
            var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository(repositoryCompetition);

            _console.MarkupLine("[green]Probing latest predicted match lookup[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(settings.CommunityContext)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
            _console.MarkupLine(
                $"[blue]Query:[/] [yellow]{Markup.Escape(settings.HomeTeam.Trim())}[/] vs [yellow]{Markup.Escape(settings.AwayTeam.Trim())}[/]");

            if (settings.Verbose)
            {
                _console.MarkupLine(
                    "[dim]Firestore query filters match-predictions by competition, communityContext, homeTeam, awayTeam; orders by startsAt descending; limits to 1.[/]");
            }

            var match = await predictionRepository.GetLatestPredictedMatchByTeamsAsync(
                settings.HomeTeam,
                settings.AwayTeam,
                settings.CommunityContext,
                cancellationToken);

            if (match is null)
            {
                _console.MarkupLine("[yellow]No matching prediction found[/]");
                return 1;
            }

            _console.MarkupLine(
                "[green]Found latest predicted match:[/] " +
                $"{Markup.Escape(match.HomeTeam)} vs {Markup.Escape(match.AwayTeam)}, " +
                $"matchday {match.Matchday}, starts at {FormatPlayedAt(match.StartsAt)}");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to probe WM26 prediction lookup");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static string FormatPlayedAt(ZonedDateTime startsAt)
    {
        var local = startsAt.WithZone(BerlinTimeZone);
        var dateTimeOffset = new DateTimeOffset(
            local.LocalDateTime.ToDateTimeUnspecified(),
            local.Offset.ToTimeSpan());

        return dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }
}
