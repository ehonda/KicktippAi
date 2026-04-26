using OpenAiIntegration;

namespace Orchestrator.Infrastructure.Langfuse;

internal sealed class LangfuseTextPromptTemplateProvider : IInstructionsTemplateProvider
{
    private readonly ILangfusePublicApiClient _client;
    private readonly string _promptName;
    private readonly string? _label;
    private readonly int? _version;
    private readonly LangfusePrompt? _preloadedPrompt;
    private readonly Lazy<LangfusePrompt> _prompt;

    public LangfuseTextPromptTemplateProvider(
        ILangfusePublicApiClient client,
        string promptName,
        string? label,
        int? version,
        LangfusePrompt? preloadedPrompt = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _promptName = string.IsNullOrWhiteSpace(promptName)
            ? throw new ArgumentException("Langfuse prompt name must be provided.", nameof(promptName))
            : promptName.Trim();
        _label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        _version = version;
        _preloadedPrompt = preloadedPrompt;
        _prompt = new Lazy<LangfusePrompt>(LoadPrompt);
    }

    public LangfusePrompt Prompt => _prompt.Value;

    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        if (includeJustification)
        {
            throw new NotSupportedException(
                "The Langfuse prompt source POC only supports match prompts without justification.");
        }

        var prompt = Prompt;
        return (prompt.GetTextPrompt(), BuildPromptPath(prompt));
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        throw new NotSupportedException("The Langfuse prompt source POC does not support bonus prompts.");
    }

    private LangfusePrompt LoadPrompt()
    {
        if (_preloadedPrompt is not null)
        {
            return _preloadedPrompt;
        }

        return _client.GetPromptAsync(_promptName, _label, _version)
                   .GetAwaiter()
                   .GetResult()
               ?? throw new FileNotFoundException(
                   $"Langfuse prompt '{_promptName}' was not found for label '{_label ?? "<none>"}' and version '{_version?.ToString() ?? "<none>"}'.");
    }

    private string BuildPromptPath(LangfusePrompt prompt)
    {
        var labelSuffix = string.IsNullOrWhiteSpace(_label) ? string.Empty : $"?label={Uri.EscapeDataString(_label)}";
        return $"langfuse://prompts/{Uri.EscapeDataString(prompt.Name)}/versions/{prompt.Version}{labelSuffix}";
    }
}
