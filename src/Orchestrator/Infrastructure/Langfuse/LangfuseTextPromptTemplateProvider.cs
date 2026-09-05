using OpenAiIntegration;
using System.Net;

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
    private readonly string? _expectedContentSha256;
    private readonly bool _availabilityOnlyFallback;
    private readonly string _fallbackSource;
    private readonly Lazy<HostedPromptResolution> _hostedPrompt;
    private PromptTemplateTelemetryMetadata? _lastTelemetryMetadata;

    public LangfuseTextPromptTemplateProvider(
        ILangfusePublicApiClient client,
        string promptName,
        string? label,
        int? version,
        LangfusePrompt? preloadedPrompt = null,
        LangfusePromptKind promptKind = LangfusePromptKind.Match,
        IInstructionsTemplateProvider? fallbackTemplateProvider = null,
        string? fallbackModel = null,
        Action<string>? fallbackWarning = null,
        string? expectedContentSha256 = null,
        bool availabilityOnlyFallback = false,
        string? fallbackSource = null)
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
        _expectedContentSha256 = string.IsNullOrWhiteSpace(expectedContentSha256) ? null : expectedContentSha256.Trim();
        _availabilityOnlyFallback = availabilityOnlyFallback;
        _fallbackSource = string.IsNullOrWhiteSpace(fallbackSource)
            ? CompetitionResolver.LocalPromptSource
            : fallbackSource.Trim();
        _hostedPrompt = new Lazy<HostedPromptResolution>(LoadHostedPrompt);
    }

    public LangfusePrompt? Prompt => _hostedPrompt.Value.Prompt;

    public void EnsureHostedPromptResolved()
    {
        var hosted = _hostedPrompt.Value;
        if (hosted.Prompt is null || hosted.Template is null)
        {
            throw new FileNotFoundException(
                $"{hosted.FailureReason} A hosted prompt is required for this operation; local fallback is not permitted.");
        }
    }

    public PromptTemplateTelemetryMetadata? GetPromptTemplateTelemetryMetadata()
    {
        return Volatile.Read(ref _lastTelemetryMetadata);
    }

    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        if (_promptKind != LangfusePromptKind.Match)
        {
            throw new NotSupportedException("This Langfuse prompt provider is configured for bonus prompts.");
        }

        if (includeJustification &&
            string.Equals(_promptName, CompetitionResolver.WorldCupMatchPromptName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                "The WM 2026 hosted match prompt does not support responses with justification.");
        }

        var prompt = ResolvePrompt(includeJustification);
        Volatile.Write(ref _lastTelemetryMetadata, prompt.TelemetryMetadata);
        return (prompt.Template, prompt.Path);
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        if (_promptKind != LangfusePromptKind.Bonus)
        {
            throw new NotSupportedException("This Langfuse prompt provider is configured for match prompts.");
        }

        var prompt = ResolvePrompt(includeJustification: false);
        Volatile.Write(ref _lastTelemetryMetadata, prompt.TelemetryMetadata);
        return (prompt.Template, prompt.Path);
    }

    private HostedPromptResolution LoadHostedPrompt()
    {
        LangfusePrompt? prompt;
        try
        {
            prompt = _preloadedPrompt
                     ?? _client.GetPromptAsync(_promptName, _label, _version)
                         .GetAwaiter()
                         .GetResult();
        }
        catch (Exception ex) when (_fallbackTemplateProvider is not null && CanFallbackFor(ex))
        {
            return new HostedPromptResolution(
                Prompt: null,
                Template: null,
                FailureReason: $"Failed to fetch Langfuse prompt '{_promptName}': {ex.Message}");
        }

        if (prompt is null)
        {
            if (_availabilityOnlyFallback)
            {
                throw new FileNotFoundException($"Langfuse prompt '{_promptName}' was not found.");
            }

            return new HostedPromptResolution(
                Prompt: null,
                Template: null,
                FailureReason: $"Langfuse prompt '{_promptName}' was not found.");
        }

        ValidatePromptBinding(prompt);
        return new HostedPromptResolution(prompt, prompt.GetTextPrompt(), FailureReason: null);
    }

    private void ValidatePromptBinding(LangfusePrompt prompt)
    {
        if (!string.Equals(prompt.Name, _promptName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Resolved Langfuse prompt name '{prompt.Name}' does not match required name '{_promptName}'.");
        }

        if (_version is { } requiredVersion && prompt.Version != requiredVersion)
        {
            throw new InvalidDataException(
                $"Resolved Langfuse prompt '{_promptName}' version {prompt.Version} does not match required version {requiredVersion}.");
        }

        if (_label is { } requiredLabel
            && (prompt.Labels is null
                || !prompt.Labels.Contains(requiredLabel, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"Resolved Langfuse prompt '{_promptName}' version {prompt.Version} does not have required label '{requiredLabel}'.");
        }

        if (_expectedContentSha256 is { } expectedHash
            && !string.Equals(PromptTemplateContentHash.ComputeSha256(prompt.GetTextPrompt()), expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Resolved Langfuse prompt '{_promptName}' does not match the frozen normalized content hash.");
        }
    }

    private ResolvedPrompt ResolvePrompt(bool includeJustification)
    {
        var hosted = _hostedPrompt.Value;
        if (hosted.Prompt is { } prompt && hosted.Template is { } template)
        {
            var path = BuildPromptPath(prompt);
            return new ResolvedPrompt(
                template,
                path,
                new PromptTemplateTelemetryMetadata(
                    RequestedSource: CompetitionResolver.LangfusePromptSource,
                    ActualSource: CompetitionResolver.LangfusePromptSource,
                    LangfusePromptName: prompt.Name,
                    LangfusePromptLabel: _label,
                    LangfusePromptVersion: prompt.Version,
                    IsFallback: false,
                    PromptPath: path,
                    ContentSha256: PromptTemplateContentHash.ComputeSha256(template)));
        }

        return LoadFallbackPrompt(hosted.FailureReason!, includeJustification);
    }

    private string BuildPromptPath(LangfusePrompt prompt)
    {
        var labelSuffix = string.IsNullOrWhiteSpace(_label) ? string.Empty : $"?label={Uri.EscapeDataString(_label)}";
        return $"langfuse://prompts/{Uri.EscapeDataString(prompt.Name)}/versions/{prompt.Version}{labelSuffix}";
    }

    private ResolvedPrompt LoadFallbackPrompt(string reason, bool includeJustification)
    {
        if (_fallbackTemplateProvider is null || string.IsNullOrWhiteSpace(_fallbackModel))
        {
            throw new FileNotFoundException(
                $"{reason} No local fallback prompt was configured for '{_promptName}'.");
        }

        var fallback = _promptKind == LangfusePromptKind.Match
            ? _fallbackTemplateProvider.LoadMatchTemplate(_fallbackModel, includeJustification)
            : _fallbackTemplateProvider.LoadBonusTemplate(_fallbackModel);

        if (_expectedContentSha256 is { } expectedHash
            && !string.Equals(PromptTemplateContentHash.ComputeSha256(fallback.template), expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Local fallback prompt '{fallback.path}' does not match the frozen normalized content hash.");
        }

        _fallbackWarning?.Invoke($"{reason} Using local fallback prompt '{fallback.path}'.");
        return new ResolvedPrompt(
            fallback.template,
            fallback.path,
            new PromptTemplateTelemetryMetadata(
                RequestedSource: CompetitionResolver.LangfusePromptSource,
                ActualSource: _fallbackSource,
                LangfusePromptName: _promptName,
                LangfusePromptLabel: _label,
                LangfusePromptVersion: _version,
                IsFallback: true,
                PromptPath: fallback.path,
                ContentSha256: PromptTemplateContentHash.ComputeSha256(fallback.template)));
    }

    private sealed record HostedPromptResolution(
        LangfusePrompt? Prompt,
        string? Template,
        string? FailureReason);

    private sealed record ResolvedPrompt(
        string Template,
        string Path,
        PromptTemplateTelemetryMetadata TelemetryMetadata);

    private bool CanFallbackFor(Exception exception)
    {
        if (!_availabilityOnlyFallback)
        {
            return true;
        }

        if (exception is LangfusePublicApiException publicApiException)
        {
            return (int)publicApiException.StatusCode is >= 500 and <= 599;
        }

        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode is null || (int)httpException.StatusCode.Value is >= 500 and <= 599;
        }

        if (exception is OperationCanceledException canceled)
        {
            // This provider never supplies a caller cancellation token to the
            // prompt lookup. Treat HttpClient's timeout shape (or the legacy
            // token-less shape) as availability failure, but never turn an
            // explicitly requested cancellation into a mirror fallback.
            return canceled.InnerException is TimeoutException
                || !canceled.CancellationToken.IsCancellationRequested;
        }

        return exception is TimeoutException
            or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException;
    }
}
