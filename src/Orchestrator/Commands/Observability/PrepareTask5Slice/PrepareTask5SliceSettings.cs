using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareTask5Slice;

public sealed class PrepareTask5SliceSettings : CommandSettings
{
    [CommandOption("--input")]
    [Description("Path to the canonical exported experiment dataset JSON file")]
    public string InputPath { get; set; } = string.Empty;

    [CommandOption("--sample-size")]
    [Description("Number of dataset items to sample")]
    [DefaultValue(10)]
    public int SampleSize { get; set; } = 10;

    [CommandOption("--sample-seed")]
    [Description("Optional deterministic seed for random slice selection. Defaults to the current UTC date in yyyyMMdd format")]
    public int? SampleSeed { get; set; }

    [CommandOption("--slice-key")]
    [Description("Optional slice key override. Defaults to random-<sample-size>-seed-<sample-seed>")]
    public string? SliceKey { get; set; }

    [CommandOption("--slice-kind")]
    [Description("Logical slice kind metadata tag")]
    [DefaultValue("random-sample")]
    public string SliceKind { get; set; } = "random-sample";

    [CommandOption("--sample-method")]
    [Description("Logical sample selection method metadata tag")]
    [DefaultValue("random-sample")]
    public string SampleMethod { get; set; } = "random-sample";

    [CommandOption("--source-pool-key")]
    [Description("Optional source pool identifier used in dataset names and output paths. Defaults to all-matchdays")]
    public string? SourcePoolKey { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override for the prepared slice")]
    public string? DatasetName { get; set; }

    [CommandOption("--output-directory")]
    [Description("Optional output directory override. Defaults to artifacts/langfuse-experiments/slices/<community>/<source-pool-key>/<slice-key>")]
    public string? OutputDirectory { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            return ValidationResult.Error("--input is required");
        }

        if (SampleSize < 1)
        {
            return ValidationResult.Error("--sample-size must be at least 1");
        }

        if (string.IsNullOrWhiteSpace(SliceKind))
        {
            return ValidationResult.Error("--slice-kind must be a non-empty string");
        }

        if (string.IsNullOrWhiteSpace(SampleMethod))
        {
            return ValidationResult.Error("--sample-method must be a non-empty string");
        }

        return ValidationResult.Success();
    }
}
