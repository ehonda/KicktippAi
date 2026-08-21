using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.BundesligaHistory;

public class BundesligaHistorySettings : CommandSettings
{
}

public abstract class BundesligaHistoryCommunitySettings : BundesligaHistorySettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    public string Competition { get; set; } = CompetitionIds.Bundesliga2026_27;

    [CommandOption("--verbose")]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext)) return ValidationResult.Error("--community-context is required");
        return string.Equals(Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
            ? ValidationResult.Success()
            : ValidationResult.Error($"--competition must be {CompetitionIds.Bundesliga2026_27}");
    }
}

public sealed class BundesligaHistoryExportInventorySettings : BundesligaHistoryCommunitySettings
{
    [CommandOption("-o|--output <OUTPUT>")]
    public string Output { get; set; } = "data/bundesliga-2026-27/history/history-played-dates-inventory.csv";

    [CommandOption("--from-kicktipp")]
    [Description("Collect raw history from Kicktipp instead of reading latest Firestore documents; never writes either service")]
    public bool FromKicktipp { get; set; }

    [CommandOption("--matchdays <MATCHDAYS>")]
    [Description("Comma-separated Kicktipp matchdays used with --from-kicktipp; defaults to 1")]
    public string Matchdays { get; set; } = "1";
}

public class BundesligaHistoryAuditSettings : BundesligaHistoryCommunitySettings
{
    [CommandOption("-i|--input <INPUT>")]
    public string Input { get; set; } = BundesligaHistoryPlayedDateMap.RelativePath;
}

public sealed class BundesligaHistoryApplySettings : BundesligaHistoryAuditSettings
{
    [CommandOption("--dry-run")]
    [Description("Audit and report updates without writing Firestore")]
    public bool DryRun { get; set; }
}
