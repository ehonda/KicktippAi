using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

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
    [Description("Aggregate costs over all available matchdays, models, and community contexts")]
    [DefaultValue(false)]
    public bool All { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed cost breakdown")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}
