using OpenAiIntegration;

namespace Orchestrator.Infrastructure;

internal sealed class LocalPromptTemplateProvider : IInstructionsTemplateProvider, IPromptTemplateTelemetryMetadataProvider
{
    private readonly IInstructionsTemplateProvider _inner;
    private readonly string _promptModel;
    private PromptTemplateTelemetryMetadata? _lastTelemetryMetadata;

    public LocalPromptTemplateProvider(IInstructionsTemplateProvider inner, string promptModel)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _promptModel = string.IsNullOrWhiteSpace(promptModel)
            ? throw new ArgumentException("Local prompt model must be provided.", nameof(promptModel))
            : promptModel.Trim();
    }

    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        var prompt = _inner.LoadMatchTemplate(_promptModel, includeJustification);
        Record(prompt);
        return prompt;
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        var prompt = _inner.LoadBonusTemplate(_promptModel);
        Record(prompt);
        return prompt;
    }

    public PromptTemplateTelemetryMetadata? GetPromptTemplateTelemetryMetadata() =>
        Volatile.Read(ref _lastTelemetryMetadata);

    private void Record((string template, string path) prompt)
    {
        Volatile.Write(ref _lastTelemetryMetadata, new PromptTemplateTelemetryMetadata(
            RequestedSource: CompetitionResolver.LocalPromptSource,
            ActualSource: CompetitionResolver.LocalPromptSource,
            LangfusePromptName: null,
            LangfusePromptLabel: null,
            LangfusePromptVersion: null,
            IsFallback: false,
            PromptPath: prompt.path,
            ContentSha256: PromptTemplateContentHash.ComputeSha256(prompt.template)));
    }
}
