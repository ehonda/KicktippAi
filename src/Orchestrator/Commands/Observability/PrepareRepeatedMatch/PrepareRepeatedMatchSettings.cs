using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareRepeatedMatch;

public sealed class PrepareRepeatedMatchSettings : CommandSettings
{
    [CommandOption("--community-context")]
    [Description("Community context used to load persisted historical outcomes")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--home")]
    [Description("Home team name")]
    public string HomeTeam { get; set; } = string.Empty;

    [CommandOption("--away")]
    [Description("Away team name")]
    public string AwayTeam { get; set; } = string.Empty;

    [CommandOption("--matchday")]
    [Description("Historical matchday number")]
    public int? Matchday { get; set; }

    [CommandOption("--sample-size")]
    [Description("Number of repeated executions to materialize in the dedicated hosted dataset")]
    [DefaultValue(5)]
    public int SampleSize { get; set; } = 5;

    [CommandOption("--slice-key")]
    [Description("Optional repetition slice key override. Defaults to repeat-<sample-size>")]
    public string? SliceKey { get; set; }

    [CommandOption("--source-pool-key")]
    [Description("Optional source pool identifier used in dataset names and output paths. Defaults to md<matchday>-<home>-vs-<away>")]
    public string? SourcePoolKey { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override for the repeated-match dataset")]
    public string? DatasetName { get; set; }

    [CommandOption("--output-directory")]
    [Description("Optional output directory override. Defaults to artifacts/langfuse-experiments/repeated-match/<community>/<source-pool-key>/<slice-key>")]
    public string? OutputDirectory { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(HomeTeam))
        {
            return ValidationResult.Error("--home is required");
        }

        if (string.IsNullOrWhiteSpace(AwayTeam))
        {
            return ValidationResult.Error("--away is required");
        }

        if (!Matchday.HasValue)
        {
            return ValidationResult.Error("--matchday is required");
        }

        if (SampleSize < 1)
        {
            return ValidationResult.Error("--sample-size must be at least 1");
        }

        return ValidationResult.Success();
    }
}
