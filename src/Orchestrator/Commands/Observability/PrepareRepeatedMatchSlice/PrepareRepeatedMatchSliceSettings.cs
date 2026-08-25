using System.ComponentModel;
using System.Globalization;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Observability.Experiments;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareRepeatedMatchSlice;

public sealed class PrepareRepeatedMatchSliceSettings : CommandSettings
{
    [CommandOption("--competition")]
    [Description("Canonical competition scope. Defaults to bundesliga-2026-27; historical 2025/26 requires the explicit compatibility mode.")]
    [DefaultValue(CompetitionIds.Bundesliga2026_27)]
    public string Competition { get; set; } = CompetitionIds.Bundesliga2026_27;

    [CommandOption("--historical-context-compatibility")]
    [Description("Explicit read-only historical context compatibility mode. Bundesliga 2025/26 supports bundesliga-2025-26-legacy-id-hash-v1.")]
    public string? HistoricalContextCompatibility { get; set; }

    [CommandOption("--official-knowledge-cutoff")]
    [Description("Official model knowledge cutoff date (yyyy-MM-dd), required for historical compatibility preparation.")]
    public string? OfficialKnowledgeCutoff { get; set; }

    [CommandOption("--community-context")]
    [Description("Community context used to scope persisted historical match outcomes")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--matchdays")]
    [Description("Optional comma-separated list of matchdays to sample from. Defaults to all Bundesliga matchdays.")]
    public string? Matchdays { get; set; }

    [CommandOption("--match-count")]
    [Description("Number of distinct fixtures to sample")]
    [DefaultValue(5)]
    public int MatchCount { get; set; } = 5;

    [CommandOption("--repetitions")]
    [Description("Number of repeated predictions to materialize per selected fixture")]
    [DefaultValue(4)]
    public int Repetitions { get; set; } = 4;

    [CommandOption("--sample-seed")]
    [Description("Optional deterministic seed for random fixture selection. Defaults to the current UTC date in yyyyMMdd format")]
    public int? SampleSeed { get; set; }

    [CommandOption("--starts-after")]
    [Description("Optional match start cutoff in NodaTime invariant ZonedDateTime 'G' format. Only matches strictly after this timestamp are eligible.")]
    public string? StartsAfter { get; set; }

    [CommandOption("--slice-key")]
    [Description("Optional slice key override. Defaults to random-<match-count>x<repetitions>-seed-<sample-seed>")]
    public string? SliceKey { get; set; }

    [CommandOption("--source-pool-key")]
    [Description("Optional source pool identifier used in dataset names and output paths. Defaults to all-matchdays")]
    public string? SourcePoolKey { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override for the prepared repeated-match slice")]
    public string? DatasetName { get; set; }

    [CommandOption("--dataset-description")]
    [Description("Optional short note describing this repeated-match slice dataset")]
    public string? DatasetDescription { get; set; }

    [CommandOption("--output-directory")]
    [Description("Optional output directory override. Defaults to artifacts/langfuse-experiments/repeated-match-slices/<community>/<source-pool-key>/<slice-key>")]
    public string? OutputDirectory { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        string competition;
        try
        {
            competition = CompetitionIds.Canonicalize(Competition);
        }
        catch (ArgumentException ex)
        {
            return ValidationResult.Error(ex.Message);
        }

        if (string.Equals(competition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal))
        {
            if (!string.Equals(
                    HistoricalContextCompatibility,
                    ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1,
                    StringComparison.Ordinal))
            {
                return ValidationResult.Error(
                    $"Bundesliga 2025/26 preparation requires --historical-context-compatibility {ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1}");
            }

            if (string.IsNullOrWhiteSpace(StartsAfter))
            {
                return ValidationResult.Error("Bundesliga 2025/26 historical compatibility preparation requires --starts-after");
            }

            if (!DateOnly.TryParseExact(
                    OfficialKnowledgeCutoff,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var officialKnowledgeCutoff))
            {
                return ValidationResult.Error(
                    "Bundesliga 2025/26 historical compatibility preparation requires --official-knowledge-cutoff in yyyy-MM-dd format");
            }

            var requiredSamplingCutoff = PreparedExperimentCommandSupport.BuildRequiredHistoricalSamplingCutoff(
                officialKnowledgeCutoff);
            if (!string.Equals(
                    EvaluationTimeParser.NormalizeOrNull(StartsAfter),
                    requiredSamplingCutoff,
                    StringComparison.Ordinal))
            {
                return ValidationResult.Error(
                    $"Bundesliga 2025/26 historical compatibility preparation requires --starts-after exactly '{requiredSamplingCutoff}' (Europe/Berlin local midnight two days after the official cutoff)");
            }
        }
        else if (!string.IsNullOrWhiteSpace(HistoricalContextCompatibility)
                 || !string.IsNullOrWhiteSpace(OfficialKnowledgeCutoff))
        {
            return ValidationResult.Error(
                "Historical compatibility options are only valid with --competition bundesliga-2025-26");
        }

        if (MatchCount < 1)
        {
            return ValidationResult.Error("--match-count must be at least 1");
        }

        if (Repetitions < 1)
        {
            return ValidationResult.Error("--repetitions must be at least 1");
        }

        if (!string.IsNullOrWhiteSpace(StartsAfter))
        {
            try
            {
                _ = EvaluationTimeParser.Parse(StartsAfter);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.Error(ex.Message);
            }
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
