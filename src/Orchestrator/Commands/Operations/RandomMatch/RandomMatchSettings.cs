using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Spectre.Console;
using Spectre.Console.Cli;

#pragma warning disable CA1822 // these properties follow the Spectre.Console.Cli CommandSettings pattern

namespace Orchestrator.Commands.Operations.RandomMatch;

public class RandomMatchSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction")]
    public string? Model { get; set; }

    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to use (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("--community-context")]
    [Description("The community context for filtering predictions (defaults to community name if not specified)")]
    public string? CommunityContext { get; set; }

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("--reasoning-effort")]
    [Description("Optional OpenAI reasoning effort (none, minimal, low, medium, high, xhigh)")]
    public string? ReasoningEffort { get; set; }

    [CommandOption("--prompt-source")]
    [Description("Prompt source to use: local or langfuse")]
    public string? PromptSource { get; set; }

    [CommandOption("--langfuse-prompt-name")]
    [Description("Langfuse hosted prompt name when --prompt-source langfuse is used")]
    public string? LangfusePromptName { get; set; }

    [CommandOption("--langfuse-prompt-label")]
    [Description("Langfuse hosted prompt label when --prompt-source langfuse is used")]
    public string? LangfusePromptLabel { get; set; }

    [CommandOption("--langfuse-prompt-version")]
    [Description("Optional Langfuse hosted prompt version when --prompt-source langfuse is used")]
    public int? LangfusePromptVersion { get; set; }

    [CommandOption("--with-justification")]
    [Description("Include model justification text alongside predictions")]
    [DefaultValue(false)]
    public bool WithJustification { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ValidationResult.Error("MODEL is required");
        }

        if (!PredictionModelConfig.IsValidReasoningEffort(ReasoningEffort))
        {
            return ValidationResult.Error("--reasoning-effort must be one of: none, minimal, low, medium, high, xhigh");
        }

        return ValidationResult.Success();
    }
}
