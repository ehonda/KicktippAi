namespace OpenAiIntegration;

public sealed record PredictionServiceOptions(
    bool DisableFlexProcessing = false,
    LangfusePromptTraceMetadata? LangfusePromptTraceMetadata = null,
    string? ReasoningEffort = null,
    int MaxOutputTokenCount = 10_000)
{
    public static PredictionServiceOptions Default { get; } = new();

    public static PredictionServiceOptions StandardProcessing { get; } = new(DisableFlexProcessing: true);

    public static PredictionServiceOptions FlexProcessingWithStandardFallback { get; } = Default;

    public static bool RequiresStandardProcessing(string model) =>
        string.Equals(model, "gpt-6.2-sol", StringComparison.OrdinalIgnoreCase);
}

public sealed record LangfusePromptTraceMetadata(string Name, int Version, bool IsFallback = false);
