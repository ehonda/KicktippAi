using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed class CollectContextProfileSettings : CollectContextSettings
{
    [CommandOption("--community-context <COMMUNITY_CONTEXT>")]
    [Description("The explicit community context to collect and publish the profile for")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--kicktipp-credential-profile <PROFILE>")]
    [Description("Optional participant profile suffix for .env.<community-context>.<profile> credential selection")]
    public string? KicktippCredentialProfile { get; set; }

    [CommandOption("--competition <COMPETITION>")]
    [Description("The explicit competition profile identifier")]
    public string Competition { get; set; } = string.Empty;

    [CommandOption("--matchdays")]
    [Description("Comma-separated Kicktipp matchday indexes to collect instead of only the current matchday")]
    public string? Matchdays { get; set; }

    [CommandOption("--full-season")]
    [Description("Collect the complete profile-owned Bundesliga season fixture context atomically")]
    public bool FullSeason { get; set; }

    [CommandOption("--dry-run")]
    [Description("Validate every selected collector without publishing")]
    public bool DryRun { get; set; }

    [CommandOption("--enable-club-elo-source")]
    [Description("Enable the dormant Club Elo cycle source for this invocation")]
    public bool EnableClubEloSource { get; set; }

    [CommandOption("--enable-roster-source")]
    [Description("Enable the dormant roster cycle source for this invocation")]
    public bool EnableRosterSource { get; set; }

    [CommandOption("--context-source-scope <SCOPE>")]
    [Description("Context-source cycle scope: development (default) or production-live")]
    public string? ContextSourceScope { get; set; }

    [CommandOption("--context-source-cycle-id <CYCLE_ID>")]
    [Description("Exact shared production cycle ID gha:<github.repository_id>:<github.run_id>")]
    public string? ContextSourceCycleId { get; set; }

    [CommandOption("--context-source-current-lane <LANE>")]
    [Description("Current production context lane consuming the shared source cycle")]
    public string? ContextSourceCurrentLane { get; set; }

    [CommandOption("--context-source-producer-lane <LANE>")]
    [Description("Fixed production lane that creates and uploads the shared source cycle")]
    public string? ContextSourceProducerLane { get; set; }

    [CommandOption("--context-source-consumers <LANES>")]
    [Description("Comma-separated production context lanes in exact execution order")]
    public string? ContextSourceConsumers { get; set; }

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
