using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

/// <summary>
/// Settings for the collect-context command.
/// </summary>
public class CollectContextSettings : CommandSettings
{
    [CommandArgument(0, "<SUBCOMMAND>")]
    [Description("The context source to collect from (kicktipp)")]
    public string Subcommand { get; set; } = string.Empty;

    [CommandOption("--community")]
    [Description("The Kicktipp community to collect context from")]
    public string Community { get; set; } = string.Empty;

    [CommandOption("--community-context")]
    [Description("The community context (rules/scoring) to use - defaults to community name")]
    public string? CommunityContext { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    public bool DryRun { get; set; }
}
