using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public abstract class RunExperimentSettingsBase : CommandSettings
{
    private static readonly HashSet<string> AllowedReasoningEfforts = new(StringComparer.Ordinal)
    {
        "none",
        "minimal",
        "low",
        "medium",
        "high",
        "xhigh"
    };

    [CommandArgument(0, "<MODEL>")]
    [Description("The model to execute for the experiment run")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("--manifest")]
    [Description("Path to the prepared experiment manifest JSON file")]
    public string ManifestPath { get; set; } = string.Empty;

    [CommandOption("--run-name")]
    [Description("Langfuse dataset run name")]
    public string RunName { get; set; } = string.Empty;

    [CommandOption("--run-description")]
    [Description("Optional Langfuse dataset run description")]
    public string? RunDescription { get; set; }

    [CommandOption("--run-metadata-file")]
    [Description("Optional path to an experiment run metadata JSON file. When omitted, metadata is built from the manifest and command flags")]
    public string? RunMetadataFile { get; set; }

    [CommandOption("--prompt-key")]
    [Description("Prompt variant identifier used in run metadata and trace tags")]
    [DefaultValue("prompt-v1")]
    public string PromptKey { get; set; } = "prompt-v1";

    [CommandOption("--prompt-source")]
    [Description("Prompt source for experiment predictions: local or langfuse")]
    [DefaultValue("local")]
    public string PromptSource { get; set; } = "local";

    [CommandOption("--langfuse-prompt-name")]
    [Description("Langfuse hosted prompt name when --prompt-source langfuse is used")]
    public string? LangfusePromptName { get; set; }

    [CommandOption("--langfuse-prompt-label")]
    [Description("Langfuse hosted prompt label when --prompt-source langfuse is used")]
    [DefaultValue("production")]
    public string? LangfusePromptLabel { get; set; } = "production";

    [CommandOption("--langfuse-prompt-version")]
    [Description("Optional Langfuse hosted prompt version when --prompt-source langfuse is used")]
    public int? LangfusePromptVersion { get; set; }

    [CommandOption("--reasoning-effort")]
    [Description("Optional OpenAI reasoning effort for experiment predictions: none, minimal, low, medium, high, or xhigh")]
    public string? ReasoningEffort { get; set; }

    [CommandOption("--include-justification")]
    [Description("Use the justification prompt variant when reconstructing historical prompts")]
    [DefaultValue(false)]
    public bool IncludeJustification { get; set; }

    [CommandOption("--evaluation-time")]
    [Description("Optional exact evaluation time in NodaTime invariant ZonedDateTime 'G' format, for example '2026-03-15T12:00:00 Europe/Berlin (+01)'")]
    public string? EvaluationTime { get; set; }

    [CommandOption("--evaluation-policy-kind")]
    [Description("Optional evaluation policy kind. Defaults to 'relative' when no run metadata file or exact evaluation time is provided")]
    public string? EvaluationPolicyKind { get; set; }

    [CommandOption("--evaluation-policy-offset")]
    [Description("Optional evaluation policy offset. Defaults to '-12:00:00' when no run metadata file or exact evaluation time is provided")]
    public string? EvaluationPolicyOffset { get; set; }

    [CommandOption("--dataset-name")]
    [Description("Optional hosted dataset name override")]
    public string? DatasetName { get; set; }

    [CommandOption("--replace-run")]
    [Description("Delete an existing dataset run with the same name before starting")]
    [DefaultValue(false)]
    public bool ReplaceRun { get; set; }

    protected ValidationResult ValidateCommon()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ValidationResult.Error("Model is required");
        }

        if (string.IsNullOrWhiteSpace(ManifestPath))
        {
            return ValidationResult.Error("--manifest is required");
        }

        if (string.IsNullOrWhiteSpace(RunName))
        {
            return ValidationResult.Error("--run-name is required");
        }

        if (string.IsNullOrWhiteSpace(PromptKey))
        {
            return ValidationResult.Error("--prompt-key must be a non-empty string");
        }

        if (!string.IsNullOrWhiteSpace(ReasoningEffort))
        {
            var normalizedReasoningEffort = ReasoningEffort.Trim().ToLowerInvariant();
            if (!AllowedReasoningEfforts.Contains(normalizedReasoningEffort))
            {
                return ValidationResult.Error("--reasoning-effort must be one of: none, minimal, low, medium, high, xhigh");
            }

            ReasoningEffort = normalizedReasoningEffort;
        }

        var normalizedPromptSource = PromptSource.Trim().ToLowerInvariant();
        if (normalizedPromptSource is not ("local" or "langfuse"))
        {
            return ValidationResult.Error("--prompt-source must be either 'local' or 'langfuse'");
        }

        if (normalizedPromptSource == "langfuse")
        {
            if (IncludeJustification)
            {
                return ValidationResult.Error("--prompt-source langfuse does not support --include-justification in this POC");
            }

            if (string.IsNullOrWhiteSpace(LangfusePromptName))
            {
                return ValidationResult.Error("--langfuse-prompt-name is required when --prompt-source langfuse is used");
            }

            if (LangfusePromptVersion is < 1)
            {
                return ValidationResult.Error("--langfuse-prompt-version must be at least 1 when provided");
            }
        }
        else if (!string.IsNullOrWhiteSpace(LangfusePromptName) || LangfusePromptVersion is not null)
        {
            return ValidationResult.Error("Langfuse prompt options require --prompt-source langfuse");
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

    private protected PreparedExperimentRunOptions CreateRunOptions(
        string batchStrategy,
        int? batchSize = null,
        int? batchCount = null)
    {
        var normalizedPromptSource = PromptSource.Trim().ToLowerInvariant();
        var langfusePromptName = normalizedPromptSource == "langfuse" ? LangfusePromptName : null;
        var langfusePromptLabel = normalizedPromptSource == "langfuse" ? LangfusePromptLabel : null;
        var langfusePromptVersion = normalizedPromptSource == "langfuse" ? LangfusePromptVersion : null;

        return new PreparedExperimentRunOptions(
            Model,
            PromptKey,
            IncludeJustification,
            EvaluationTime,
            EvaluationPolicyKind,
            EvaluationPolicyOffset,
            DatasetName,
            normalizedPromptSource,
            langfusePromptName,
            langfusePromptLabel,
            langfusePromptVersion,
            batchStrategy,
            batchSize,
            batchCount,
            ReasoningEffort);
    }
}

public sealed class RunSliceSettings : RunExperimentSettingsBase
{
    [CommandOption("--batch-size")]
    [Description("Optional batch size override")]
    public int? BatchSize { get; set; }

    public override ValidationResult Validate()
    {
        var commonValidation = ValidateCommon();
        if (!commonValidation.Successful)
        {
            return commonValidation;
        }

        if (BatchSize is < 1)
        {
            return ValidationResult.Error("--batch-size must be at least 1 when provided");
        }

        return ValidationResult.Success();
    }

    internal PreparedExperimentRunOptions ToRunOptions()
    {
        return CreateRunOptions("simple-batched", BatchSize);
    }
}
