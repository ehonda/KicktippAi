namespace OpenAiIntegration;

public sealed record PromptTemplateTelemetryMetadata(
    string? LangfusePromptName,
    int? LangfusePromptVersion,
    bool IsFallback,
    string PromptPath);

public interface IPromptTemplateTelemetryMetadataProvider
{
    PromptTemplateTelemetryMetadata? GetPromptTemplateTelemetryMetadata();
}
