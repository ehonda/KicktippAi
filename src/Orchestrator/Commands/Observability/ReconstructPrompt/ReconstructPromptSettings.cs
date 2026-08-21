using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ReconstructPrompt;

public class ReconstructPromptSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The model used for the stored prediction")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("--community-context")]
    [Description("Community context used for the stored prediction")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--home")]
    [Description("Home team name")]
    public string HomeTeam { get; set; } = string.Empty;

    [CommandOption("--away")]
    [Description("Away team name")]
    public string AwayTeam { get; set; } = string.Empty;

    [CommandOption("--matchday")]
    [Description("Matchday number for the selected match")]
    public int? Matchday { get; set; }

    [CommandOption("--with-justification")]
    [Description("Reconstruct the justification prompt variant")]
    [DefaultValue(false)]
    public bool WithJustification { get; set; }

    [CommandOption("--reasoning-effort")]
    [Description("Optional OpenAI reasoning effort used for the stored prediction (none, minimal, low, medium, high, xhigh, max)")]
    public string? ReasoningEffort { get; set; }

    [CommandOption("--competition")]
    [Description("Competition identifier for repository and stored identity selection (defaults to bundesliga-2026-27)")]
    public string? Competition { get; set; }

    [CommandOption("--max-output-tokens")]
    [Description("Maximum output token cap used by the stored prediction identity")]
    public int? MaxOutputTokenCount { get; set; }

    [CommandOption("--prompt-source")]
    [Description("Prompt source used by the stored prediction identity: local or langfuse")]
    public string? PromptSource { get; set; }

    [CommandOption("--langfuse-prompt-name")]
    [Description("Langfuse hosted prompt name used by the stored prediction identity")]
    public string? LangfusePromptName { get; set; }

    [CommandOption("--langfuse-prompt-label")]
    [Description("Langfuse hosted prompt label used by the stored prediction identity")]
    public string? LangfusePromptLabel { get; set; }

    [CommandOption("--langfuse-prompt-version")]
    [Description("Exact Langfuse hosted prompt version used by the stored prediction identity")]
    public int? LangfusePromptVersion { get; set; }

    [CommandOption("--evaluation-time")]
    [Description("Optional explicit evaluation time in NodaTime invariant ZonedDateTime 'G' format, for example '2026-03-15T12:00:00 Europe/Berlin (+01)'")]
    public string? EvaluationTime { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ValidationResult.Error("Model is required");
        }

        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(HomeTeam))
        {
            return ValidationResult.Error("--home must be provided");
        }

        if (string.IsNullOrWhiteSpace(AwayTeam))
        {
            return ValidationResult.Error("--away must be provided");
        }

        if (!Matchday.HasValue)
        {
            return ValidationResult.Error("--matchday must be provided");
        }

        if (!string.IsNullOrWhiteSpace(EvaluationTime))
        {
            try
            {
                _ = Commands.Observability.EvaluationTimeParser.Parse(EvaluationTime);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.Error(ex.Message);
            }
        }

        if (!PredictionModelConfig.IsValidReasoningEffort(ReasoningEffort))
        {
            return ValidationResult.Error("--reasoning-effort must be one of: none, minimal, low, medium, high, xhigh, max");
        }

        if (MaxOutputTokenCount is < 1)
        {
            return ValidationResult.Error("--max-output-tokens must be at least 1 when provided");
        }

        if (LangfusePromptVersion is < 1)
        {
            return ValidationResult.Error("--langfuse-prompt-version must be at least 1 when provided");
        }

        return ValidationResult.Success();
    }
}
