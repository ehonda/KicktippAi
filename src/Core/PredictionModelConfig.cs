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
        "xhigh"
    };

    private PredictionModelConfig(string model, string? reasoningEffort)
    {
        Model = model;
        ReasoningEffort = reasoningEffort;
    }

    public string Model { get; }

    public string? ReasoningEffort { get; }

    public string IdentityKey => ReasoningEffort is null
        ? Model
        : $"{Model}:reasoning-effort:{ReasoningEffort}";

    public string DisplayName => ReasoningEffort is null
        ? Model
        : $"{Model} ({ReasoningEffort})";

    public bool AllowsLegacyModelOnlyLookup => ReasoningEffort is null;

    public static PredictionModelConfig Create(string model, string? reasoningEffort = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new PredictionModelConfig(
            model.Trim(),
            NormalizeReasoningEffort(reasoningEffort));
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
                "--reasoning-effort must be one of: none, minimal, low, medium, high, xhigh",
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
