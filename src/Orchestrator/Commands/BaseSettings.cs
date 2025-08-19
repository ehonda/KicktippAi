using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

public class BaseSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-2024-08-06, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to use (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("--community-context")]
    [Description("The community context for filtering predictions (defaults to community name if not specified)")]
    public string? CommunityContext { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--override-kicktipp")]
    [Description("Override existing predictions on Kicktipp (default: false)")]
    [DefaultValue(false)]
    public bool OverrideKicktipp { get; set; }

    [CommandOption("--override-database")]
    [Description("Override existing predictions in the database (default: false)")]
    [DefaultValue(false)]
    public bool OverrideDatabase { get; set; }

    [CommandOption("--agent")]
    [Description("Agent mode - hide prediction details from output (for automated environments)")]
    [DefaultValue(false)]
    public bool Agent { get; set; }

    [CommandOption("--dry-run")]
    [Description("Dry run mode - no changes will be made to database or Kicktipp")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("--show-context-documents")]
    [Description("Show the content of context documents used for prediction")]
    [DefaultValue(false)]
    public bool ShowContextDocuments { get; set; }

    [CommandOption("--estimated-costs")]
    [Description("Model to estimate costs for (e.g., o1) - shows what costs would be if using that model with same token counts")]
    public string? EstimatedCostsModel { get; set; }

    [CommandOption("--repredict")]
    [Description("Enable reprediction mode - create new predictions with incremented reprediction index (cannot be used with --override-database)")]
    [DefaultValue(false)]
    public bool Repredict { get; set; }

    [CommandOption("--max-repredictions")]
    [Description("Maximum number of repredictions allowed (0-based index). Implies --repredict. Example: 2 allows reprediction indices 0, 1, 2")]
    public int? MaxRepredictions { get; set; }

    /// <summary>
    /// Gets whether reprediction mode is enabled (either explicitly via --repredict or implicitly via --max-repredictions).
    /// </summary>
    public bool IsRepredictMode => Repredict || MaxRepredictions.HasValue;
}
