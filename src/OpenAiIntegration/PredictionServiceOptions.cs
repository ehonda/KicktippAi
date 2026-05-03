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
}

public sealed record LangfusePromptTraceMetadata(string Name, int Version);
