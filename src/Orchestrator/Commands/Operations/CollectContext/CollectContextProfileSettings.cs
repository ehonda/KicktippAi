using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed class CollectContextProfileSettings : CollectContextSettings
{
    [CommandOption("--community-context <COMMUNITY_CONTEXT>")]
    [Description("The explicit community context to collect and publish the profile for")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("The explicit competition profile identifier")]
    public string Competition { get; set; } = string.Empty;

    [CommandOption("--matchdays")]
    [Description("Comma-separated Kicktipp matchday indexes to collect instead of only the current matchday")]
    public string? Matchdays { get; set; }

    [CommandOption("--dry-run")]
    [Description("Validate every selected collector without publishing")]
    public bool DryRun { get; set; }

    [CommandOption("--recent-history-date-map <INPUT>")]
    [Description("Canonical WM26 recent-history played-date map CSV path used only by the WM26 profile")]
    public string RecentHistoryDateMap { get; set; } = "data/wm26/recent-history/recent-history-match-dates.csv";

    [CommandOption("--markdown-summary-output <OUTPUT>")]
    [Description("Optional Markdown file to append the resolved profile and actual collector dispositions to")]
    public string? MarkdownSummaryOutput { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose diagnostics")]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(Competition))
        {
            return ValidationResult.Error("--competition is required");
        }

        return ValidationResult.Success();
    }
}
