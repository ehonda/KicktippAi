using Spectre.Console.Cli;
using System.ComponentModel;

namespace Orchestrator.Commands.Utility.ListKpi;

public class ListKpiSettings : CommandSettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("The community context to filter by (required)")]
    public required string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community context, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; } = false;
}
