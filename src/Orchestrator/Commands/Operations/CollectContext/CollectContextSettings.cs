using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Base settings for the collect-context command.
/// </summary>
public class CollectContextSettings : CommandSettings
{
    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    public bool DryRun { get; set; }
}

/// <summary>
/// Settings for the collect-context kicktipp subcommand.
/// </summary>
public class CollectContextKicktippSettings : CollectContextSettings
{
    [CommandOption("--community-context")]
    [Description("The community context (rules/scoring) to use")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }
}
