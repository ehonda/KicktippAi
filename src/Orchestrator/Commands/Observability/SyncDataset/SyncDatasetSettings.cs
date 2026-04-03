using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.SyncDataset;

public sealed class SyncDatasetSettings : CommandSettings
{
    [CommandOption("--input")]
    [Description("Path to the exported dataset artifact JSON file")]
    public string InputPath { get; set; } = string.Empty;

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override")]
    public string? DatasetName { get; set; }

    [CommandOption("--dry-run")]
    [Description("Validate the artifact and print a summary without calling Langfuse")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            return ValidationResult.Error("--input is required");
        }

        return ValidationResult.Success();
    }
}