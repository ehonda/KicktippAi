using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentDataset;

public sealed class ExportExperimentDatasetSettings : CommandSettings
{
    [CommandOption("--community-context")]
    [Description("Community context used to scope persisted match outcomes")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--matchdays")]
    [Description("Optional comma-separated list of matchdays to export. Defaults to all Bundesliga matchdays.")]
    public string? Matchdays { get; set; }

    [CommandOption("--output")]
    [Description("Path to the JSON file to write. Defaults to artifacts/langfuse-dataset/<community>.json")]
    public string? OutputPath { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(Matchdays))
        {
            return ValidationResult.Success();
        }

        var segments = Matchdays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return ValidationResult.Error("--matchdays must contain at least one matchday number when provided");
        }

        foreach (var segment in segments)
        {
            if (!int.TryParse(segment, out var matchday) || matchday is < 1 or > 34)
            {
                return ValidationResult.Error($"Invalid matchday '{segment}'. Expected an integer between 1 and 34.");
            }
        }

        return ValidationResult.Success();
    }
}
