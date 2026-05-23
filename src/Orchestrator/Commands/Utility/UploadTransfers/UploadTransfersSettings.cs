using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.UploadTransfers;

public class UploadTransfersSettings : CommandSettings
{
    [CommandArgument(0, "<TEAM_ABBREVIATION>")]
    [Description("Three-letter team abbreviation (e.g., fcb, bvb, b04)")]
    public string TeamAbbreviation { get; set; } = string.Empty;

    [CommandOption("-c|--community-context")]
    [Description("The community context to use (e.g., ehonda-test-buli)")]
    public required string CommunityContext { get; set; }

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community context, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}
