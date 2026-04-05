using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ReconstructPrompt;

public class ReconstructPromptSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The model used for the stored prediction")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("--community-context")]
    [Description("Community context used for the stored prediction")]
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

    [CommandOption("--with-justification")]
    [Description("Reconstruct the justification prompt variant")]
    [DefaultValue(false)]
    public bool WithJustification { get; set; }

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

        return ValidationResult.Success();
    }
}
