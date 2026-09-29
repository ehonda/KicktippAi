using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class CollectContextDevSettings : DevParticipationSettings
{
    [CommandOption("--context-source-only")]
    [Description("Prepare and collect only the enabled Club Elo source")]
    public bool ContextSourceOnly { get; set; }

    [CommandOption("--enable-club-elo-source")]
    public bool EnableClubEloSource { get; set; }

    [CommandOption("--enable-roster-source")]
    public bool EnableRosterSource { get; set; }
    [CommandOption("--matchdays")]
    [Description("Comma-separated Kicktipp matchday indexes to collect instead of only the current matchday")]
    public string? Matchdays { get; set; }

    [CommandOption("--full-season")]
    [Description("Collect the complete profile-owned Bundesliga season fixture context atomically")]
    public bool FullSeason { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without actually saving to database")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("--recent-history-date-map <INPUT>")]
    [Description("Canonical WM26 recent-history played-date map CSV path")]
    public string RecentHistoryDateMap { get; set; } = "data/wm26/recent-history/recent-history-match-dates.csv";
}
