using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class CollectContextDevSettings : DevParticipationSettings
{
    [CommandOption("--matchdays")]
    [Description("Comma-separated Kicktipp matchday indexes to collect instead of only the current matchday")]
    public string? Matchdays { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }
}
