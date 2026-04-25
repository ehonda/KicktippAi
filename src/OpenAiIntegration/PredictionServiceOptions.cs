namespace OpenAiIntegration;

public sealed record PredictionServiceOptions(bool UseFlexProcessingWithStandardFallback = false)
{
    public static PredictionServiceOptions Default { get; } = new();

    public static PredictionServiceOptions FlexProcessingWithStandardFallback { get; } = new(true);
}
