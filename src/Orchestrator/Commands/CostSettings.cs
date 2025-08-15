using System.ComponentModel;
using Spectre.Console.Cli;
using System.Text.Json.Serialization;

namespace Orchestrator.Commands;

public class CostConfiguration
{
    [JsonPropertyName("matchdays")]
    public string? Matchdays { get; set; }

    [JsonPropertyName("bonus")]
    public bool? Bonus { get; set; }

    [JsonPropertyName("models")]
    public string? Models { get; set; }

    [JsonPropertyName("communityContexts")]
    public string? CommunityContexts { get; set; }

    [JsonPropertyName("all")]
    public bool? All { get; set; }

    [JsonPropertyName("verbose")]
    public bool? Verbose { get; set; }

    [JsonPropertyName("detailedBreakdown")]
    public bool? DetailedBreakdown { get; set; }
}

public class CostSettings : CommandSettings
{
    [CommandOption("--matchdays")]
    [Description("Comma-separated list of matchdays to include in cost calculation (e.g., '1,2,3' or 'all' for all matchdays)")]
    public string? Matchdays { get; set; }

    [CommandOption("--bonus")]
    [Description("Include bonus predictions in cost calculation")]
    [DefaultValue(false)]
    public bool Bonus { get; set; }

    [CommandOption("--models")]
    [Description("Comma-separated list of AI models to include in calculation (e.g., 'gpt-4o,o1-mini' or 'all' for all models)")]
    public string? Models { get; set; }

    [CommandOption("--community-contexts")]
    [Description("Comma-separated list of community contexts to include (e.g., 'ehonda-test-buli,ehonda-test-buli-2' or 'all' for all contexts)")]
    public string? CommunityContexts { get; set; }

    [CommandOption("--all")]
    [Description("Aggregate costs over all available matchdays, models, community contexts, and includes bonus predictions")]
    [DefaultValue(false)]
    public bool All { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed cost breakdown")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--detailed-breakdown")]
    [Description("Show detailed breakdown by community context and model in the results table")]
    [DefaultValue(false)]
    public bool DetailedBreakdown { get; set; }

    [CommandOption("--file")]
    [Description("Load configuration from a JSON file (absolute or relative path)")]
    public string? ConfigFile { get; set; }
}
