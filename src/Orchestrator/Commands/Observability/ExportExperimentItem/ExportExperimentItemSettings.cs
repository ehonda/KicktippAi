using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentItem;

public sealed class ExportExperimentItemSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The model used for the stored prediction that anchors prompt reconstruction")]
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

    [CommandOption("--evaluation-time")]
    [Description("Optional explicit evaluation time in NodaTime invariant ZonedDateTime 'G' format, for example '2026-03-15T12:00:00 Europe/Berlin (+01)'")]
    public string? EvaluationTime { get; set; }

    [CommandOption("--evaluation-policy-kind")]
    [Description("Optional evaluation policy kind. Currently only 'relative' is supported.")]
    public string? EvaluationPolicyKind { get; set; }

    [CommandOption("--evaluation-policy-offset")]
    [Description("Optional NodaTime Duration offset for the evaluation policy, for example '-12:00:00'.")]
    public string? EvaluationPolicyOffset { get; set; }

    [CommandOption("--output")]
    [Description("Path to the JSON file to write. Defaults to artifacts/langfuse-experiments/items/<match>.json")]
    public string? OutputPath { get; set; }

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

        if (!string.IsNullOrWhiteSpace(EvaluationTime))
        {
            try
            {
                _ = Commands.Observability.EvaluationTimeParser.Parse(EvaluationTime);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.Error(ex.Message);
            }
        }

        var hasEvaluationPolicyKind = !string.IsNullOrWhiteSpace(EvaluationPolicyKind);
        var hasEvaluationPolicyOffset = !string.IsNullOrWhiteSpace(EvaluationPolicyOffset);

        if (hasEvaluationPolicyKind != hasEvaluationPolicyOffset)
        {
            return ValidationResult.Error("--evaluation-policy-kind and --evaluation-policy-offset must be provided together");
        }

        if (!string.IsNullOrWhiteSpace(EvaluationTime) && hasEvaluationPolicyKind)
        {
            return ValidationResult.Error("--evaluation-time cannot be combined with --evaluation-policy-kind/--evaluation-policy-offset");
        }

        if (hasEvaluationPolicyKind)
        {
            try
            {
                _ = Commands.Observability.EvaluationTimestampPolicyParser.Parse(EvaluationPolicyKind, EvaluationPolicyOffset);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.Error(ex.Message);
            }
        }

        return ValidationResult.Success();
    }
}
