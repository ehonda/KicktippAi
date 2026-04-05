using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentAnalysis;

public sealed class ExportExperimentAnalysisSettings : CommandSettings
{
    [CommandOption("--dataset-name")]
    [Description("Hosted Langfuse dataset name that contains the compared runs")]
    public string DatasetName { get; set; } = string.Empty;

    [CommandOption("--run-names")]
    [Description("Comma-separated list of Langfuse dataset run names to export as one comparable analysis bundle")]
    public string RunNames { get; set; } = string.Empty;

    [CommandOption("--output")]
    [Description("Optional output path for the normalized analysis bundle JSON file")]
    public string? OutputPath { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(DatasetName))
        {
            return ValidationResult.Error("--dataset-name is required");
        }

        var runNames = GetParsedRunNames();
        if (runNames.Count < 2)
        {
            return ValidationResult.Error("--run-names must contain at least two unique run names");
        }

        return ValidationResult.Success();
    }

    internal IReadOnlyList<string> GetParsedRunNames()
    {
        return RunNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(runName => !string.IsNullOrWhiteSpace(runName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}