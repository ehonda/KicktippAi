using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Verify;

public class VerifySettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to verify predictions for")]
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

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--agent")]
    [Description("Agent mode - hide prediction details from output (for automated environments)")]
    [DefaultValue(false)]
    public bool Agent { get; set; }

    [CommandOption("--init-matchday")]
    [Description("Init matchday mode - return error when no predictions exist to trigger initial prediction workflow")]
    [DefaultValue(false)]
    public bool InitMatchday { get; set; }

    [CommandOption("--check-outdated")]
    [Description("Check if predictions are outdated based on context document changes")]
    [DefaultValue(false)]
    public bool CheckOutdated { get; set; }

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
