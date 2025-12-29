using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

/// <summary>
/// Settings for the generate-snapshots command.
/// </summary>
public class GenerateSnapshotsSettings : CommandSettings
{
    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to fetch snapshots from (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("-o|--output")]
    [Description("Output directory for snapshots (default: kicktipp-snapshots/)")]
    [DefaultValue("kicktipp-snapshots")]
    public string OutputDirectory { get; set; } = "kicktipp-snapshots";

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}
