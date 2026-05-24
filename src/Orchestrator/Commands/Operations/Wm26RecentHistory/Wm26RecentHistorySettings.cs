using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Wm26RecentHistory;

public class Wm26RecentHistorySettings : CommandSettings
{
}

public sealed class Wm26RecentHistoryExportDateMapSettings : Wm26RecentHistorySettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("The community context whose latest WM26 recent-history documents should be exported")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("Competition identifier")]
    public string Competition { get; set; } = CompetitionIds.FifaWorldCup2026;

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("Canonical date map CSV path")]
    public string Output { get; set; } = "data/wm26/recent-history/recent-history-match-dates.csv";

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        return string.IsNullOrWhiteSpace(CommunityContext)
            ? ValidationResult.Error("--community-context is required")
            : ValidationResult.Success();
    }
}

public sealed class Wm26RecentHistoryApplyDateMapSettings : Wm26RecentHistorySettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("The community context whose latest WM26 recent-history documents should be updated")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("Competition identifier")]
    public string Competition { get; set; } = CompetitionIds.FifaWorldCup2026;

    [CommandOption("-i|--input <INPUT>")]
    [Description("Canonical date map CSV path")]
    public string Input { get; set; } = "data/wm26/recent-history/recent-history-match-dates.csv";

    [CommandOption("--dry-run")]
    [Description("Show what would be saved without writing Firestore documents")]
    public bool DryRun { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        return string.IsNullOrWhiteSpace(CommunityContext)
            ? ValidationResult.Error("--community-context is required")
            : ValidationResult.Success();
    }
}
