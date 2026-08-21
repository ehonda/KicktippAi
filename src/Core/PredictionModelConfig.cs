namespace EHonda.KicktippAi.Core;

public sealed record PredictionModelConfig
{
    private static readonly HashSet<string> AllowedReasoningEfforts = new(StringComparer.Ordinal)
    {
        "none",
        "minimal",
        "low",
        "medium",
        "high",
        "xhigh",
        "max"
    };

    private PredictionModelConfig(
        string model,
        string? reasoningEffort,
        int? maxOutputTokenCount,
        string? promptName,
        int? promptVersion)
    {
        Model = model;
        ReasoningEffort = reasoningEffort;
        MaxOutputTokenCount = maxOutputTokenCount;
        PromptName = promptName;
        PromptVersion = promptVersion;
    }

    public string Model { get; }

    public string? ReasoningEffort { get; }

    public int? MaxOutputTokenCount { get; }

    public string? PromptName { get; }

    public int? PromptVersion { get; }

    public string IdentityKey
    {
        get
        {
            var parts = new List<string> { Model };
            if (ReasoningEffort is not null)
            {
                parts.Add($"reasoning-effort:{ReasoningEffort}");
            }

            if (MaxOutputTokenCount is not null)
            {
                parts.Add($"max-output-tokens:{MaxOutputTokenCount.Value}");
            }

            if (PromptName is not null)
            {
                parts.Add($"prompt-name:{Uri.EscapeDataString(PromptName)}");
            }

            if (PromptVersion is not null)
            {
                parts.Add($"prompt-version:{PromptVersion.Value}");
            }

            return string.Join(':', parts);
        }
    }

    public string DisplayName
    {
        get
        {
            var displayName = ReasoningEffort is null ? Model : $"{Model} ({ReasoningEffort})";
            if (MaxOutputTokenCount is not null)
            {
                displayName += $", max output {MaxOutputTokenCount.Value}";
            }

            if (PromptName is not null)
            {
                displayName += PromptVersion is null
                    ? $", prompt {PromptName}"
                    : $", prompt {PromptName} v{PromptVersion.Value}";
            }

            return displayName;
        }
    }

    public bool AllowsLegacyModelOnlyLookup =>
        ReasoningEffort is null && !HasPinnedRuntimeIdentity;

    public bool AllowsReasoningEffortOnlyLookup => !HasPinnedRuntimeIdentity;

    public bool HasPinnedRuntimeIdentity =>
        MaxOutputTokenCount is not null || PromptName is not null || PromptVersion is not null;

    public static PredictionModelConfig Create(
        string model,
        string? reasoningEffort = null,
        int? maxOutputTokenCount = null,
        string? promptName = null,
        int? promptVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (maxOutputTokenCount is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputTokenCount),
                maxOutputTokenCount,
                "Maximum output tokens must be at least 1 when provided.");
        }

        if (promptVersion is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(promptVersion),
                promptVersion,
                "Prompt version must be at least 1 when provided.");
        }

        var normalizedPromptName = string.IsNullOrWhiteSpace(promptName) ? null : promptName.Trim();
        if (promptVersion is not null && normalizedPromptName is null)
        {
            throw new ArgumentException("Prompt name is required when a prompt version is provided.", nameof(promptName));
        }

        if (normalizedPromptName is not null && promptVersion is null)
        {
            throw new ArgumentException("Prompt version is required when a prompt name is provided.", nameof(promptVersion));
        }

        return new PredictionModelConfig(
            model.Trim(),
            NormalizeReasoningEffort(reasoningEffort),
            maxOutputTokenCount,
            normalizedPromptName,
            promptVersion);
    }

    public static string? NormalizeReasoningEffort(string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return null;
        }

        var normalized = reasoningEffort.Trim().ToLowerInvariant();
        if (!AllowedReasoningEfforts.Contains(normalized))
        {
            throw new ArgumentException(
                "--reasoning-effort must be one of: none, minimal, low, medium, high, xhigh, max",
                nameof(reasoningEffort));
        }

        return normalized;
    }

    public static bool IsValidReasoningEffort(string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return true;
        }

        return AllowedReasoningEfforts.Contains(reasoningEffort.Trim().ToLowerInvariant());
    }
}
