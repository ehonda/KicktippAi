using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentAnalysis;

public sealed class PublishExperimentAnalysisSettings : CommandSettings
{
    [CommandOption("--input")]
    [Description("Path to a normalized experiment analysis bundle JSON file")]
    public string InputPath { get; set; } = string.Empty;

    [CommandOption("--experiment-name")]
    [Description("Optional stable experiment name to attach to all published runs")]
    public string? ExperimentName { get; set; }

    [CommandOption("--run-name-suffix")]
    [Description("Suffix appended to source run names when creating beta UI aliases")]
    [DefaultValue("__experiments-beta")]
    public string RunNameSuffix { get; set; } = "__experiments-beta";

    [CommandOption("--description")]
    [Description("Optional description for the published Langfuse dataset runs")]
    public string? Description { get; set; }

    [CommandOption("--replace-runs")]
    [Description("Delete existing published alias runs before recreating them")]
    public bool ReplaceRuns { get; set; }

    [CommandOption("--dry-run")]
    [Description("Print the runs that would be published without writing to Langfuse")]
    public bool DryRun { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            return ValidationResult.Error("--input is required");
        }

        if (!File.Exists(InputPath))
        {
            return ValidationResult.Error($"Input file does not exist: {InputPath}");
        }

        if (string.IsNullOrWhiteSpace(RunNameSuffix))
        {
            return ValidationResult.Error("--run-name-suffix must be non-empty so existing dataset runs are not modified accidentally");
        }

        return ValidationResult.Success();
    }
}
