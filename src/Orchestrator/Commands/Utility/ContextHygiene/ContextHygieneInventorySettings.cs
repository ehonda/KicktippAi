using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.ContextHygiene;

public sealed class ContextHygieneInventorySettings : CommandSettings
{
    [CommandOption("-c|--community-context <COMMUNITY_CONTEXT>")]
    [Description("Community context whose Bundesliga 2026/27 document partition will be inventoried")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("Competition identifier; this inventory supports only bundesliga-2026-27")]
    [DefaultValue("bundesliga-2026-27")]
    public string Competition { get; set; } = "bundesliga-2026-27";

    [CommandOption("--json")]
    [Description("Write deterministic machine-readable JSON instead of a table")]
    [DefaultValue(false)]
    public bool Json { get; set; }

    [CommandOption("--evaluation-date <YYYY-MM-DD>")]
    [Description("Optional explicit Europe/Berlin date used for reproducible freshness classification")]
    public string? EvaluationDate { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (!string.IsNullOrWhiteSpace(EvaluationDate)
            && !DateOnly.TryParseExact(
                EvaluationDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return ValidationResult.Error("--evaluation-date must use yyyy-MM-dd");
        }

        return ValidationResult.Success();
    }
}
