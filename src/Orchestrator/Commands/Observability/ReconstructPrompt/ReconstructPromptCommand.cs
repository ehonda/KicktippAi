using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ReconstructPrompt;

/// <summary>
/// Reconstructs historical prompt inputs for a stored match prediction.
/// </summary>
public class ReconstructPromptCommand : AsyncCommand<ReconstructPromptSettings>
{
    private static readonly DateTimeZone BundesligaTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<ReconstructPromptCommand> _logger;

    public ReconstructPromptCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<ReconstructPromptCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ReconstructPromptSettings settings)
    {
        try
        {
            _console.MarkupLine($"[green]Reconstructing prompt for:[/] [yellow]{Markup.Escape(settings.HomeTeam)}[/] vs [yellow]{Markup.Escape(settings.AwayTeam)}[/]");

            var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
            var contextRepository = _firebaseServiceFactory.CreateContextRepository();
            var reconstructionService = new MatchPromptReconstructionService(
                predictionRepository,
                contextRepository,
                new InstructionsTemplateProvider(PromptsFileProvider.Create()));

            var match = await ResolveMatchAsync(predictionRepository, settings);
            if (match is null)
            {
                _console.MarkupLine($"[red]Match not found on matchday {settings.Matchday}:[/] {Markup.Escape(settings.HomeTeam)} vs {Markup.Escape(settings.AwayTeam)}");
                return 1;
            }

            match = RehydrateForPromptOutput(match);

            var reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAsync(
                match,
                settings.Model,
                settings.CommunityContext,
                settings.WithJustification);

            if (reconstructedPrompt is null)
            {
                _console.MarkupLine("[red]No stored prediction metadata found for that match/model/community combination.[/]");
                return 1;
            }

            _console.MarkupLine($"[blue]Prediction timestamp:[/] {reconstructedPrompt.PredictionCreatedAt:O}");
            _console.MarkupLine($"[blue]Prompt template:[/] {Markup.Escape(reconstructedPrompt.PromptTemplatePath)}");
            _console.MarkupLine($"[blue]Justification variant:[/] {reconstructedPrompt.IncludeJustification}");
            _console.WriteLine();
            _console.MarkupLine("[green]Resolved context versions:[/]");

            foreach (var document in reconstructedPrompt.ResolvedContextDocuments)
            {
                _console.MarkupLine($"[dim]- {Markup.Escape(document.DocumentName)} | v{document.Version} | {document.CreatedAt:O}[/]");
            }

            _console.WriteLine();
            _console.MarkupLine("[green]Match JSON:[/]");
            _console.WriteLine(reconstructedPrompt.MatchJson);
            _console.WriteLine();
            _console.MarkupLine("[green]System prompt:[/]");
            _console.WriteLine(reconstructedPrompt.SystemPrompt);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing reconstruct-prompt command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static async Task<Match?> ResolveMatchAsync(IPredictionRepository predictionRepository, ReconstructPromptSettings settings)
    {
        return await predictionRepository.GetStoredMatchAsync(
            settings.HomeTeam,
            settings.AwayTeam,
            settings.Matchday!.Value,
            settings.Model,
            settings.CommunityContext);
    }

    private static Match RehydrateForPromptOutput(Match match)
    {
        var instant = match.StartsAt.ToInstant();
        var offset = BundesligaTimeZone.GetUtcOffset(instant);
        var localizedStartsAt = instant.InZone(DateTimeZone.ForOffset(offset));
        return new Match(match.HomeTeam, match.AwayTeam, localizedStartsAt, match.Matchday, match.IsCancelled);
    }
}
