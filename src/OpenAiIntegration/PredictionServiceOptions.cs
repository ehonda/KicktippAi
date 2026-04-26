namespace OpenAiIntegration;

public sealed record PredictionServiceOptions(
    bool UseFlexProcessingWithStandardFallback = false,
    LangfusePromptTraceMetadata? LangfusePromptTraceMetadata = null)
{
    public static PredictionServiceOptions Default { get; } = new();

    public static PredictionServiceOptions FlexProcessingWithStandardFallback { get; } = new(true);
}

public sealed record LangfusePromptTraceMetadata(string Name, int Version);
