namespace OpenAiIntegration;

public sealed record PromptTemplateTelemetryMetadata(
    string RequestedSource,
    string ActualSource,
    string? LangfusePromptName,
    string? LangfusePromptLabel,
    int? LangfusePromptVersion,
    bool IsFallback,
    string PromptPath,
    string ContentSha256);

public interface IPromptTemplateTelemetryMetadataProvider
{
    PromptTemplateTelemetryMetadata? GetPromptTemplateTelemetryMetadata();
}
