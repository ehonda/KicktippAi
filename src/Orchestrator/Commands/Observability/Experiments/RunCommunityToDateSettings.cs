using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunCommunityToDateSettings : CommandSettings
{
    [CommandOption("--manifest")]
    [Description("Path to the prepared community-to-date manifest JSON file")]
    public string ManifestPath { get; set; } = string.Empty;

    [CommandOption("--run-family-name")]
    [Description("Optional base name used to derive one Langfuse dataset run name per participant")]
    public string? RunFamilyName { get; set; }

    [CommandOption("--run-description")]
    [Description("Optional Langfuse dataset run description applied to all participant runs")]
    public string? RunDescription { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override")]
    public string? DatasetName { get; set; }

    [CommandOption("--batch-size")]
    [Description("Optional batch size override used within each participant run")]
    [DefaultValue(25)]
    public int BatchSize { get; set; } = 25;

    [CommandOption("--participant-limit")]
    [Description("Optional limit for the number of participant runs to execute, ordered by display name")]
    public int? ParticipantLimit { get; set; }

    [CommandOption("--participant-ids")]
    [Description("Optional comma-separated participant ids to execute. When omitted, all manifest participants are used")]
    public string? ParticipantIds { get; set; }

    [CommandOption("--replace-runs")]
    [Description("Delete existing participant dataset runs with the same derived names before starting")]
    [DefaultValue(false)]
    public bool ReplaceRuns { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(ManifestPath))
        {
            return ValidationResult.Error("--manifest is required");
        }

        if (BatchSize < 1)
        {
            return ValidationResult.Error("--batch-size must be at least 1");
        }

        if (ParticipantLimit is < 1)
        {
            return ValidationResult.Error("--participant-limit must be at least 1 when provided");
        }

        if (ParticipantIds is not null && GetParticipantIdFilter().Count == 0)
        {
            return ValidationResult.Error("--participant-ids must contain at least one non-empty participant id when provided");
        }

        return ValidationResult.Success();
    }

    internal IReadOnlySet<string> GetParticipantIdFilter()
    {
        return string.IsNullOrWhiteSpace(ParticipantIds)
            ? new HashSet<string>(StringComparer.Ordinal)
            : ParticipantIds
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
    }
}
