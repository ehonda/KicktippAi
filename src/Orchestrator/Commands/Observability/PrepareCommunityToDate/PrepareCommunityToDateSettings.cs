using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareCommunityToDate;

public sealed class PrepareCommunityToDateSettings : CommandSettings
{
    [CommandOption("--community-context")]
    [Description("Community context used to fetch tippuebersicht snapshots and build the prepared dataset")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--cutoff-matchday")]
    [Description("Optional cutoff matchday. Defaults to the currently displayed tippuebersicht matchday.")]
    public int? CutoffMatchday { get; set; }

    [CommandOption("--source-pool-key")]
    [Description("Optional source pool identifier used in dataset names and output paths. Defaults to through-md<cutoff>.")]
    public string? SourcePoolKey { get; set; }

    [CommandOption("--slice-key")]
    [Description("Optional slice key override. Defaults to community-to-date-md<cutoff>.")]
    public string? SliceKey { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override for the prepared community-to-date dataset")]
    public string? DatasetName { get; set; }

    [CommandOption("--output-directory")]
    [Description("Optional output directory override. Defaults to artifacts/langfuse-experiments/community-to-date/<community>/<source-pool-key>/<slice-key>")]
    public string? OutputDirectory { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (CutoffMatchday is < 1 or > 35)
        {
            return ValidationResult.Error("--cutoff-matchday must be between 1 and 35 when provided");
        }

        return ValidationResult.Success();
    }
}
