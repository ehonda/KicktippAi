using OpenAiIntegration;

namespace Orchestrator.Infrastructure.Langfuse;

internal enum LangfusePromptKind
{
    Match,
    Bonus
}

internal sealed class LangfuseTextPromptTemplateProvider : IInstructionsTemplateProvider, IPromptTemplateTelemetryMetadataProvider
{
    private readonly ILangfusePublicApiClient _client;
    private readonly string _promptName;
    private readonly string? _label;
    private readonly int? _version;
    private readonly LangfusePrompt? _preloadedPrompt;
    private readonly LangfusePromptKind _promptKind;
    private readonly IInstructionsTemplateProvider? _fallbackTemplateProvider;
    private readonly string? _fallbackModel;
    private readonly Action<string>? _fallbackWarning;
    private readonly Lazy<ResolvedPrompt> _prompt;

    public LangfuseTextPromptTemplateProvider(
        ILangfusePublicApiClient client,
        string promptName,
        string? label,
        int? version,
        LangfusePrompt? preloadedPrompt = null,
        LangfusePromptKind promptKind = LangfusePromptKind.Match,
        IInstructionsTemplateProvider? fallbackTemplateProvider = null,
        string? fallbackModel = null,
        Action<string>? fallbackWarning = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _promptName = string.IsNullOrWhiteSpace(promptName)
            ? throw new ArgumentException("Langfuse prompt name must be provided.", nameof(promptName))
            : promptName.Trim();
        _label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        _version = version;
        _preloadedPrompt = preloadedPrompt;
        _promptKind = promptKind;
        _fallbackTemplateProvider = fallbackTemplateProvider;
        _fallbackModel = string.IsNullOrWhiteSpace(fallbackModel) ? null : fallbackModel.Trim();
        _fallbackWarning = fallbackWarning;
        _prompt = new Lazy<ResolvedPrompt>(LoadPrompt);
    }

    public LangfusePrompt? Prompt => _prompt.Value.Prompt;

    public PromptTemplateTelemetryMetadata? GetPromptTemplateTelemetryMetadata()
    {
        return _prompt.IsValueCreated ? _prompt.Value.TelemetryMetadata : null;
    }

    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        if (_promptKind != LangfusePromptKind.Match)
        {
            throw new NotSupportedException("This Langfuse prompt provider is configured for bonus prompts.");
        }

        if (includeJustification)
        {
            throw new NotSupportedException(
                "The Langfuse prompt source only supports WM 2026 match prompts without justification in this version.");
        }

        var prompt = _prompt.Value;
        return (prompt.Template, prompt.Path);
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        if (_promptKind != LangfusePromptKind.Bonus)
        {
            throw new NotSupportedException("This Langfuse prompt provider is configured for match prompts.");
        }

        var prompt = _prompt.Value;
        return (prompt.Template, prompt.Path);
    }

    private ResolvedPrompt LoadPrompt()
    {
        try
        {
            var prompt = _preloadedPrompt
                         ?? _client.GetPromptAsync(_promptName, _label, _version)
                             .GetAwaiter()
                             .GetResult();

            if (prompt is not null)
            {
                var path = BuildPromptPath(prompt);
                return new ResolvedPrompt(
                    prompt.GetTextPrompt(),
                    path,
                    prompt,
                    new PromptTemplateTelemetryMetadata(prompt.Name, prompt.Version, IsFallback: false, path));
            }

            return LoadFallbackPrompt($"Langfuse prompt '{_promptName}' was not found.");
        }
        catch (Exception ex) when (_fallbackTemplateProvider is not null)
        {
            return LoadFallbackPrompt($"Failed to fetch Langfuse prompt '{_promptName}': {ex.Message}");
        }
    }

    private string BuildPromptPath(LangfusePrompt prompt)
    {
        var labelSuffix = string.IsNullOrWhiteSpace(_label) ? string.Empty : $"?label={Uri.EscapeDataString(_label)}";
        return $"langfuse://prompts/{Uri.EscapeDataString(prompt.Name)}/versions/{prompt.Version}{labelSuffix}";
    }

    private ResolvedPrompt LoadFallbackPrompt(string reason)
    {
        if (_fallbackTemplateProvider is null || string.IsNullOrWhiteSpace(_fallbackModel))
        {
            throw new FileNotFoundException(
                $"{reason} No local fallback prompt was configured for '{_promptName}'.");
        }

        var fallback = _promptKind == LangfusePromptKind.Match
            ? _fallbackTemplateProvider.LoadMatchTemplate(_fallbackModel, includeJustification: false)
            : _fallbackTemplateProvider.LoadBonusTemplate(_fallbackModel);

        _fallbackWarning?.Invoke($"{reason} Using local fallback prompt '{fallback.path}'.");
        return new ResolvedPrompt(
            fallback.template,
            fallback.path,
            Prompt: null,
            new PromptTemplateTelemetryMetadata(_promptName, null, IsFallback: true, fallback.path));
    }

    private sealed record ResolvedPrompt(
        string Template,
        string Path,
        LangfusePrompt? Prompt,
        PromptTemplateTelemetryMetadata TelemetryMetadata);
}
