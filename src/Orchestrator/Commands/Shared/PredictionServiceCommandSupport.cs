using EHonda.KicktippAi.Core;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;

namespace Orchestrator.Commands.Shared;

internal static class PredictionServiceCommandSupport
{
    public const string WorldCupDevDefaultModel = "gpt-5-nano";
    public const string WorldCupDevDefaultReasoningEffort = "minimal";

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
        int? maxOutputTokenCount,
        bool bonusPrompt,
        bool requireHostedPrompt = false,
        string? bonusProfile = null,
        int? bonusContextDocumentBudget = null,
        int? bonusContextTokenBudget = null,
        string? bonusDeadlineAtOrBefore = null)
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
            ReasoningEffort = NormalizeReasoningEffort(reasoningEffort),
            MaxOutputTokenCount = maxOutputTokenCount ?? PredictionServiceOptions.FlexProcessingWithStandardFallback.MaxOutputTokenCount
        };
        var isPotentialChampionsLeagueBonus = bonusPrompt
            && (SchadensfresseChampionsLeagueBonusProfile.IsPotentialInvocation(
                    bonusProfile,
                    community,
                    communityContext,
                    langfusePromptName)
                || string.Equals(bonusDeadlineAtOrBefore, SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc, StringComparison.Ordinal));
        var isChampionsLeagueBonus = bonusPrompt && SchadensfresseChampionsLeagueBonusProfile.IsExactInvocation(
            bonusProfile,
            competition,
            community,
            communityContext,
            promptSource,
            langfusePromptName,
            langfusePromptLabel,
            langfusePromptVersion,
            model,
            reasoningEffort,
            maxOutputTokenCount,
            bonusContextDocumentBudget,
            bonusContextTokenBudget,
            bonusDeadlineAtOrBefore);
        if (isPotentialChampionsLeagueBonus && !isChampionsLeagueBonus)
        {
            throw new InvalidOperationException(
                "The Schadensfresse Champions-League bonus route requires the complete exact frozen invocation tuple.");
        }

        if (!string.Equals(metadata.PromptSource, CompetitionResolver.LangfusePromptSource, StringComparison.OrdinalIgnoreCase))
        {
            var localTemplateProvider = new LocalPromptTemplateProvider(
                new InstructionsTemplateProvider(PromptsFileProvider.Create()),
                metadata.FallbackPromptModel);
            return openAiServiceFactory.CreatePredictionService(model, options, localTemplateProvider);
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
            langfusePromptVersion ?? metadata.PromptVersion,
            promptKind: bonusPrompt ? LangfusePromptKind.Bonus : LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: isChampionsLeagueBonus ? "bundesliga-2026-27/champions-league" : fallbackModel,
            fallbackWarning: message => console.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}"),
            expectedContentSha256: isChampionsLeagueBonus ? SchadensfresseChampionsLeagueBonusProfile.PromptNormalizedSha256 : null,
            availabilityOnlyFallback: isChampionsLeagueBonus,
            fallbackSource: isChampionsLeagueBonus ? "dedicated-cl-mirror" : null);

        if (requireHostedPrompt)
        {
            // Resolve and validate the exact immutable version/promotion binding
            // before constructing the model-backed prediction service.
            templateProvider.EnsureHostedPromptResolved();
        }

        return openAiServiceFactory.CreatePredictionService(model, options, templateProvider);
    }

    public static bool UsesUnsupportedWorldCupHostedMatchPrompt(
        string competition,
        string community,
        string communityContext,
        string? promptSource,
        string? langfusePromptName)
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition,
            community,
            communityContext,
            promptSource,
            langfusePromptName,
            langfusePromptLabel: null,
            bonusPrompt: false);

        return CompetitionResolver.IsWorldCupCompetition(metadata.Competition)
               && string.Equals(metadata.PromptSource, CompetitionResolver.LangfusePromptSource, StringComparison.OrdinalIgnoreCase)
               && string.Equals(metadata.PromptName, CompetitionResolver.WorldCupMatchPromptName, StringComparison.Ordinal);
    }

    public static string? NormalizeReasoningEffort(string? reasoningEffort)
    {
        return PredictionModelConfig.NormalizeReasoningEffort(reasoningEffort);
    }

    public static PredictionModelConfig CreateModelConfig(string? model, string? reasoningEffort)
    {
        return PredictionModelConfig.Create(ResolveModel(model), reasoningEffort);
    }

    public static PredictionModelConfig CreateModelConfig(
        string? model,
        string? reasoningEffort,
        string competition,
        string community,
        string communityContext,
        string? promptSource,
        string? langfusePromptName,
        string? langfusePromptLabel,
        int? langfusePromptVersion,
        int? maxOutputTokenCount,
        bool bonusPrompt)
    {
        var resolvedModel = ResolveModel(model);
        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.OrdinalIgnoreCase))
        {
            return PredictionModelConfig.Create(resolvedModel, reasoningEffort);
        }

        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition,
            community,
            communityContext,
            promptSource,
            langfusePromptName,
            langfusePromptLabel,
            bonusPrompt);
        var effectiveMaxOutputTokenCount = maxOutputTokenCount
                                           ?? PredictionServiceOptions.FlexProcessingWithStandardFallback.MaxOutputTokenCount;
        var usesHostedPrompt = string.Equals(
            metadata.PromptSource,
            CompetitionResolver.LangfusePromptSource,
            StringComparison.OrdinalIgnoreCase);
        var exactPromptVersion = usesHostedPrompt
            ? langfusePromptVersion ?? metadata.PromptVersion
            : null;
        var exactPromptName = exactPromptVersion is null ? null : metadata.PromptName;

        return PredictionModelConfig.Create(
            resolvedModel,
            reasoningEffort,
            effectiveMaxOutputTokenCount,
            exactPromptName,
            exactPromptVersion);
    }

    public static string ResolveModel(string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            return model.Trim();
        }

        throw new ArgumentException("MODEL is required.", nameof(model));
    }
}
