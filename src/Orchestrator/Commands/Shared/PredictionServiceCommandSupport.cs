using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;

namespace Orchestrator.Commands.Shared;

internal static class PredictionServiceCommandSupport
{
    public const string WorldCupDefaultModel = "gpt-5-nano";
    public const string WorldCupDefaultReasoningEffort = "minimal";

    public static IPredictionService CreatePredictionService(
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient? langfuseClient,
        IAnsiConsole console,
        string model,
        string competition,
        string community,
        string communityContext,
        string? promptSource,
        string? langfusePromptName,
        string? langfusePromptLabel,
        int? langfusePromptVersion,
        string? reasoningEffort,
        bool bonusPrompt)
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition,
            community,
            communityContext,
            promptSource,
            langfusePromptName,
            langfusePromptLabel,
            bonusPrompt);

        var options = PredictionServiceOptions.FlexProcessingWithStandardFallback with
        {
            ReasoningEffort = NormalizeReasoningEffort(reasoningEffort)
                              ?? (CompetitionResolver.IsWorldCupCompetition(metadata.Competition)
                                  ? WorldCupDefaultReasoningEffort
                                  : null)
        };

        if (!string.Equals(metadata.PromptSource, CompetitionResolver.LangfusePromptSource, StringComparison.OrdinalIgnoreCase))
        {
            return openAiServiceFactory.CreatePredictionService(model, options);
        }

        if (langfuseClient is null)
        {
            throw new InvalidOperationException("Langfuse prompt source requires a Langfuse public API client.");
        }

        var promptName = metadata.PromptName;
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new InvalidOperationException("--langfuse-prompt-name is required when --prompt-source langfuse is used.");
        }

        var fallbackModel = string.IsNullOrWhiteSpace(metadata.FallbackPromptModel)
            ? model
            : metadata.FallbackPromptModel;

        var templateProvider = new LangfuseTextPromptTemplateProvider(
            langfuseClient,
            promptName,
            string.IsNullOrWhiteSpace(metadata.PromptLabel) ? null : metadata.PromptLabel,
            langfusePromptVersion,
            promptKind: bonusPrompt ? LangfusePromptKind.Bonus : LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: fallbackModel,
            fallbackWarning: message => console.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}"));

        return openAiServiceFactory.CreatePredictionService(model, options, templateProvider);
    }

    public static bool UsesLangfusePromptSource(
        string competition,
        string community,
        string communityContext,
        string? promptSource,
        bool bonusPrompt)
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition,
            community,
            communityContext,
            promptSource,
            langfusePromptName: null,
            langfusePromptLabel: null,
            bonusPrompt);

        return string.Equals(metadata.PromptSource, CompetitionResolver.LangfusePromptSource, StringComparison.OrdinalIgnoreCase);
    }

    public static string? NormalizeReasoningEffort(string? reasoningEffort)
    {
        return string.IsNullOrWhiteSpace(reasoningEffort)
            ? null
            : reasoningEffort.Trim().ToLowerInvariant();
    }

    public static string ResolveModel(string? model, string competition)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            return model.Trim();
        }

        if (CompetitionResolver.IsWorldCupCompetition(competition))
        {
            return WorldCupDefaultModel;
        }

        throw new InvalidOperationException("MODEL is required unless the competition has a configured default model.");
    }
}
