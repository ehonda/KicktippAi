using Spectre.Console.Cli;
using System.ComponentModel;

namespace Orchestrator.Commands;

/// <summary>
/// Settings for the reconstruct-data-collected-at command.
/// </summary>
public class ReconstructDataCollectedAtSettings : CommandSettings
{
    [CommandOption("--community-context")]
    [Description("The community context for filtering (e.g., ehonda-test-buli)")]
    public required string CommunityContext { get; set; }

    [CommandOption("--dry-run")]
    [Description("Preview changes without modifying the database")]
    public bool DryRun { get; set; } = false;

    [CommandOption("-v|--verbose")]
    [Description("Show verbose output")]
    public bool Verbose { get; set; } = false;
}
