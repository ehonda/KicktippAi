using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Observability;
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

            var evaluationTime = EvaluationTimeParser.ParseOrNull(settings.EvaluationTime);

            var match = await ResolveMatchAsync(predictionRepository, settings, evaluationTime is not null);
            if (match is null)
            {
                _console.MarkupLine($"[red]Match not found on matchday {settings.Matchday}:[/] {Markup.Escape(settings.HomeTeam)} vs {Markup.Escape(settings.AwayTeam)}");
                return 1;
            }

            match = RehydrateForPromptOutput(match);

            ReconstructedMatchPredictionPrompt? reconstructedPrompt;
            if (evaluationTime is null)
            {
                reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAsync(
                    match,
                    settings.Model,
                    settings.CommunityContext,
                    settings.WithJustification);
            }
            else
            {
                var selection = MatchContextDocumentCatalog.ForMatch(
                    match.HomeTeam,
                    match.AwayTeam,
                    settings.CommunityContext);

                reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAtTimestampAsync(
                    match,
                    settings.Model,
                    settings.CommunityContext,
                    evaluationTime.Value,
                    selection.RequiredDocumentNames,
                    selection.OptionalDocumentNames,
                    settings.WithJustification);
            }

            if (reconstructedPrompt is null)
            {
                _console.MarkupLine("[red]No stored prediction metadata found for that match/model/community combination.[/]");
                return 1;
            }

            var timestampLabel = evaluationTime is null ? "Prediction timestamp" : "Reconstruction timestamp";
            _console.MarkupLine($"[blue]{timestampLabel}:[/] {reconstructedPrompt.PromptTimestamp:O}");
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

    private static async Task<Match?> ResolveMatchAsync(
        IPredictionRepository predictionRepository,
        ReconstructPromptSettings settings,
        bool allowExactTimestampFallback)
    {
        return await predictionRepository.GetStoredMatchAsync(
            settings.HomeTeam,
            settings.AwayTeam,
            settings.Matchday!.Value,
            allowExactTimestampFallback ? null : settings.Model,
            allowExactTimestampFallback ? null : settings.CommunityContext);
    }

    private static Match RehydrateForPromptOutput(Match match)
    {
        var instant = match.StartsAt.ToInstant();
        var offset = BundesligaTimeZone.GetUtcOffset(instant);
        var localizedStartsAt = instant.InZone(DateTimeZone.ForOffset(offset));
        return new Match(match.HomeTeam, match.AwayTeam, localizedStartsAt, match.Matchday, match.IsCancelled);
    }
}
