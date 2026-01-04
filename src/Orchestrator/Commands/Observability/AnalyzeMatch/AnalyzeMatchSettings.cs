using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.AnalyzeMatch;

public class AnalyzeMatchBaseSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-mini, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("--community-context")]
    [Description("The Kicktipp community identifier used for both schedule lookups and context documents")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--home")]
    [Description("Home team name")]
    public string HomeTeam { get; set; } = string.Empty;

    [CommandOption("--away")]
    [Description("Away team name")]
    public string AwayTeam { get; set; } = string.Empty;

    [CommandOption("--matchday")]
    [Description("Matchday number for the selected match")]
    public int? Matchday { get; set; }

    [CommandOption("-n|--runs")]
    [Description("Number of runs to execute")]
    [DefaultValue(3)]
    public int Runs { get; set; } = 3;

    [CommandOption("--verbose")]
    [Description("Enable verbose output for context retrieval")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--show-context-documents")]
    [Description("Print the list of loaded context documents")]
    [DefaultValue(false)]
    public bool ShowContextDocuments { get; set; }

    [CommandOption("--debug")]
    [Description("Enable detailed logging output")]
    [DefaultValue(false)]
    public bool Debug { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ValidationResult.Error("Model is required");
        }

        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(HomeTeam))
        {
            return ValidationResult.Error("--home must be provided");
        }

        if (string.IsNullOrWhiteSpace(AwayTeam))
        {
            return ValidationResult.Error("--away must be provided");
        }

        if (!Matchday.HasValue)
        {
            return ValidationResult.Error("--matchday must be provided");
        }

        if (Runs <= 0)
        {
            return ValidationResult.Error("--runs must be greater than 0");
        }

        return ValidationResult.Success();
    }
}

public class AnalyzeMatchDetailedSettings : AnalyzeMatchBaseSettings
{
    [CommandOption("--no-live-estimates")]
    [Description("Disable live cost and time estimates during execution")]
    [DefaultValue(false)]
    public bool NoLiveEstimates { get; set; }
}

public class AnalyzeMatchComparisonSettings : AnalyzeMatchBaseSettings
{
}
