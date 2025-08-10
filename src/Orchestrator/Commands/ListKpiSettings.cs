using Spectre.Console.Cli;
using System.ComponentModel;

namespace Orchestrator.Commands;

public class ListKpiSettings : CommandSettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("The community context to filter by (required)")]
    public required string CommunityContext { get; set; } = string.Empty;

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; } = false;

    [CommandOption("-t|--tags <TAGS>")]
    [Description("Filter by comma-separated tags")]
    public string? Tags { get; set; }
}
