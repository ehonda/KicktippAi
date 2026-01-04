using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ContextChanges;

/// <summary>
/// Settings for the context-changes command.
/// </summary>
public class ContextChangesSettings : CommandSettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("The community context to filter by (required)")]
    public required string CommunityContext { get; set; } = string.Empty;

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; } = false;

    [CommandOption("-s|--seed <SEED>")]
    [Description("Random seed for document selection (optional)")]
    public int? Seed { get; set; }

    [CommandOption("-n|--count <COUNT>")]
    [Description("Number of documents to show (default: 10)")]
    [DefaultValue(10)]
    public int Count { get; set; } = 10;
}
