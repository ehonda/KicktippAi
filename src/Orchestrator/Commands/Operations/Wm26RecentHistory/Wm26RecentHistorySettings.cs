using System.Globalization;
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

    [CommandOption("--apply-known-only")]
    [Description("Apply exact dates for known map rows while preserving unmapped rows")]
    public bool ApplyKnownOnly { get; set; }

    [CommandOption("--preserve-collected-on-or-after <DATE>")]
    [Description("When applying known rows only, preserve rows whose Played_At or legacy Data_Collected_At is on or after this YYYY-MM-DD date")]
    public string? PreserveCollectedOnOrAfter { get; set; }

    [CommandOption("--verbose")]
    [Description("Enable verbose output")]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (!string.IsNullOrWhiteSpace(PreserveCollectedOnOrAfter))
        {
            if (!ApplyKnownOnly)
            {
                return ValidationResult.Error("--preserve-collected-on-or-after requires --apply-known-only");
            }

            if (!DateOnly.TryParseExact(
                    PreserveCollectedOnOrAfter.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return ValidationResult.Error("--preserve-collected-on-or-after must use YYYY-MM-DD");
            }
        }

        return ValidationResult.Success();
    }
}
