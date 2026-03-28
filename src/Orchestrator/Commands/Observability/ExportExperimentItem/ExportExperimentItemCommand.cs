using System.Globalization;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Observability;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentItem;

public sealed class ExportExperimentItemCommand : AsyncCommand<ExportExperimentItemSettings>
{
    private static readonly DateTimeZone BundesligaTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<ExportExperimentItemCommand> _logger;

    public ExportExperimentItemCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<ExportExperimentItemCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ExportExperimentItemSettings settings)
    {
        try
        {
            _console.MarkupLine($"[green]Exporting experiment item for:[/] [yellow]{Markup.Escape(settings.HomeTeam)}[/] vs [yellow]{Markup.Escape(settings.AwayTeam)}[/]");

            var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
            var contextRepository = _firebaseServiceFactory.CreateContextRepository();
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();

            var reconstructionService = new MatchPromptReconstructionService(
                predictionRepository,
                contextRepository,
                new InstructionsTemplateProvider(PromptsFileProvider.Create()));

            var evaluationTime = EvaluationTimeParser.ParseOrNull(settings.EvaluationTime);
            var evaluationPolicy = EvaluationTimestampPolicyParser.ParseOrNull(
                settings.EvaluationPolicyKind,
                settings.EvaluationPolicyOffset);
            var reconstructAtTimestamp = evaluationTime is not null || evaluationPolicy is not null;

            var storedMatch = await predictionRepository.GetStoredMatchAsync(
                settings.HomeTeam,
                settings.AwayTeam,
                settings.Matchday!.Value,
                reconstructAtTimestamp ? null : settings.Model,
                reconstructAtTimestamp ? null : settings.CommunityContext);

            if (storedMatch is null)
            {
                _console.MarkupLine($"[red]Stored match not found on matchday {settings.Matchday}:[/] {Markup.Escape(settings.HomeTeam)} vs {Markup.Escape(settings.AwayTeam)}");
                return 1;
            }

            var promptMatch = RehydrateForPromptOutput(storedMatch);
            var resolvedEvaluationTime = evaluationTime
                ?? (evaluationPolicy is null ? null : EvaluationTimestampResolver.Resolve(promptMatch, evaluationPolicy));

            ReconstructedMatchPredictionPrompt? reconstructedPrompt;
            if (resolvedEvaluationTime is null)
            {
                reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAsync(
                    promptMatch,
                    settings.Model,
                    settings.CommunityContext,
                    settings.WithJustification);
            }
            else
            {
                var selection = MatchContextDocumentCatalog.ForMatch(
                    promptMatch.HomeTeam,
                    promptMatch.AwayTeam,
                    settings.CommunityContext);

                reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAtTimestampAsync(
                    promptMatch,
                    settings.Model,
                    settings.CommunityContext,
                    resolvedEvaluationTime.Value,
                    selection.RequiredDocumentNames,
                    selection.OptionalDocumentNames,
                    settings.WithJustification);
            }

            if (reconstructedPrompt is null)
            {
                _console.MarkupLine("[red]No stored prediction metadata found for that match/model/community combination.[/]");
                return 1;
            }

            var outcomes = await matchOutcomeRepository.GetMatchdayOutcomesAsync(
                settings.Matchday.Value,
                settings.CommunityContext);

            var outcome = outcomes.FirstOrDefault(candidate =>
                string.Equals(candidate.HomeTeam, settings.HomeTeam, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.AwayTeam, settings.AwayTeam, StringComparison.OrdinalIgnoreCase));

            if (outcome is null)
            {
                _console.MarkupLine("[red]No persisted match outcome was found for the selected match.[/]");
                return 1;
            }

            if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
            {
                _console.MarkupLine("[red]The selected match does not have a completed persisted outcome yet.[/]");
                return 1;
            }

            var export = BuildExport(reconstructedPrompt, outcome);
            var outputPath = ResolveOutputPath(settings, export.DatasetItem.Metadata);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(export, OutputJsonOptions));

            _console.MarkupLine($"[green]Wrote experiment item:[/] [yellow]{Markup.Escape(outputPath)}[/]");
            _console.MarkupLine($"[blue]Dataset item id:[/] {Markup.Escape(export.DatasetItem.Id)}");
            _console.MarkupLine($"[blue]{(resolvedEvaluationTime is null ? "Prediction timestamp" : "Reconstruction timestamp")}:[/] {export.DatasetItem.Metadata.PredictionCreatedAt:O}");
            _console.MarkupLine($"[blue]Outcome:[/] {outcome.HomeGoals}:{outcome.AwayGoals} ({outcome.Availability})");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting experiment item");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static ExportedExperimentItem BuildExport(
        ReconstructedMatchPredictionPrompt reconstructedPrompt,
        PersistedMatchOutcome outcome)
    {
        using var matchJsonDocument = JsonDocument.Parse(reconstructedPrompt.MatchJson);
        var tippSpielId = outcome.TippSpielId ?? throw new InvalidOperationException(
            $"Persisted outcome for {outcome.HomeTeam} vs {outcome.AwayTeam} is missing tippspielId.");

        var metadata = new MatchExperimentMetadata(
            reconstructedPrompt.CommunityContext,
            outcome.Competition,
            reconstructedPrompt.Match.Matchday,
            reconstructedPrompt.Match.HomeTeam,
            reconstructedPrompt.Match.AwayTeam,
            tippSpielId,
            reconstructedPrompt.Model,
            reconstructedPrompt.IncludeJustification,
            reconstructedPrompt.PromptTimestamp,
            reconstructedPrompt.PromptTemplatePath,
            reconstructedPrompt.ContextDocumentNames,
            reconstructedPrompt.ResolvedContextDocuments
                .Select(document => new MatchExperimentResolvedContextDocument(
                    document.DocumentName,
                    document.Version,
                    document.CreatedAt))
                .ToList()
                .AsReadOnly(),
            new MatchExperimentOutcome(
                outcome.HomeGoals!.Value,
                outcome.AwayGoals!.Value));

        return new ExportedExperimentItem(
            new MatchExperimentDatasetItem(
                BuildHostedDatasetItemId(outcome.Competition, outcome.CommunityContext, tippSpielId),
                matchJsonDocument.RootElement.Clone(),
                new MatchExperimentExpectedOutput(
                    outcome.HomeGoals!.Value,
                    outcome.AwayGoals!.Value,
                    outcome.Availability.ToString()),
                metadata),
            new MatchExperimentRunnerPayload(
                reconstructedPrompt.SystemPrompt,
                reconstructedPrompt.MatchJson));
    }

    private static Match RehydrateForPromptOutput(Match match)
    {
        var instant = match.StartsAt.ToInstant();
        var offset = BundesligaTimeZone.GetUtcOffset(instant);
        var localizedStartsAt = instant.InZone(DateTimeZone.ForOffset(offset));
        return new Match(match.HomeTeam, match.AwayTeam, localizedStartsAt, match.Matchday, match.IsCancelled);
    }

    private static string ResolveOutputPath(ExportExperimentItemSettings settings, MatchExperimentMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            return Path.GetFullPath(settings.OutputPath);
        }

        var fileName = $"{metadata.Matchday:00}-{Slugify(metadata.HomeTeam)}-vs-{Slugify(metadata.AwayTeam)}-{Slugify(metadata.Model)}.json";
        return Path.GetFullPath(Path.Combine("artifacts", "langfuse-runner-spike", fileName));
    }

    private static string BuildHostedDatasetItemId(string competition, string communityContext, string tippSpielId)
    {
        return string.Join(
            "__",
            Slugify(competition),
            Slugify(communityContext),
            $"ts{Slugify(tippSpielId)}");
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '-')
            {
                continue;
            }

            builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }
}
