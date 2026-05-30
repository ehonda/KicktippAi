using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Base settings for the collect-context command.
/// </summary>
public class CollectContextSettings : CommandSettings
{
}

/// <summary>
/// Settings for the collect-context kicktipp subcommand.
/// </summary>
public class CollectContextKicktippSettings : CollectContextSettings
{
    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    public bool DryRun { get; set; }

    [CommandOption("--match-outcomes-only")]
    [Description("Collect and persist match outcomes without updating other context documents")]
    public bool MatchOutcomesOnly { get; set; }

    [CommandOption("--community-context")]
    [Description("The community context (rules/scoring) to use")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community context, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("--matchdays")]
    [Description("Comma-separated Kicktipp matchday indexes to collect instead of only the current matchday")]
    public string? Matchdays { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }
}

/// <summary>
/// Settings for the collect-context fifa subcommand.
/// </summary>
public class CollectContextFifaSettings : CollectContextSettings
{
    [CommandOption("--community-context")]
    [Description("The community context to upload FIFA ranking context for")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community context, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    public bool DryRun { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }
}

/// <summary>
/// Settings for the collect-context lineups subcommand.
/// </summary>
public class CollectContextLineupsSettings : CollectContextSettings
{
    [CommandOption("--community-context")]
    [Description("The community context to upload WM26 lineup context for")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition")]
    [Description("Competition identifier (defaults from community context, e.g., fifa-world-cup-2026 for ehonda-dev-wm26)")]
    public string? Competition { get; set; }

    [CommandOption("--seed")]
    [Description("Lineup seed CSV path")]
    public string Seed { get; set; } = "data/wm26/lineups/lineups-seed.csv";

    [CommandOption("--teams")]
    [Description("WM26 team manifest CSV path")]
    public string Teams { get; set; } = "data/wm26/lineups/wm26-teams.csv";

    [CommandOption("--duckdb-path")]
    [Description("Existing local transfermarkt-datasets DuckDB path; if omitted, the command refreshes the ignored cache")]
    public string? DuckDbPath { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    public bool DryRun { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }
}
