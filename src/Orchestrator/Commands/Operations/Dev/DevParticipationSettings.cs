using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public class DevParticipationSettings : CommandSettings
{
    [CommandOption("-c|--community")]
    [Description("The development Kicktipp community to use (e.g., ehonda-dev-wm26)")]
    public required string Community { get; set; }

    [CommandOption("--community-context")]
    [Description("The community context for filtering predictions (defaults to community name if not specified)")]
    public string? CommunityContext { get; set; }

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

}
