using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Verify;

public class VerifySettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to verify predictions for (e.g., gpt-4o-2024-08-06, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to use (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("--community-context")]
    [Description("The community context for filtering predictions (defaults to community name if not specified)")]
    public string? CommunityContext { get; set; }

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
}
