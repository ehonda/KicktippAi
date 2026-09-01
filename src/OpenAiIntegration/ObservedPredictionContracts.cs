using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace OpenAiIntegration;

public sealed class PredictionPromptExecutionRequirement
{
    private PredictionPromptExecutionRequirement(
        PredictionModelConfig modelConfig,
        string hostedNormalizedReadbackSha256,
        string requiredLabel,
        string? fallbackFile,
        string? fallbackSha256)
    {
        ModelConfig = modelConfig;
        HostedNormalizedReadbackSha256 = hostedNormalizedReadbackSha256;
        RequiredLabel = requiredLabel;
        FallbackFile = fallbackFile;
        FallbackSha256 = fallbackSha256;
    }

    public PredictionModelConfig ModelConfig { get; }
    public string HostedName => ModelConfig.PromptName!;
    public int HostedVersion => ModelConfig.PromptVersion!.Value;
    public string HostedNormalizedReadbackSha256 { get; }
    public string RequiredLabel { get; }
    public string? FallbackFile { get; }
    public string? FallbackSha256 { get; }

    public static PredictionPromptExecutionRequirement Create(
        PredictionModelConfig modelConfig,
        string hostedNormalizedReadbackSha256,
        string requiredLabel,
        string? fallbackFile = null,
        string? fallbackSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(modelConfig);
        if (!modelConfig.HasPinnedRuntimeIdentity || modelConfig.ReasoningEffort is null
            || modelConfig.MaxOutputTokenCount is null || modelConfig.PromptName is null
            || modelConfig.PromptVersion is null)
        {
            throw new InvalidDataException("Observed prompt execution requires a fully pinned model and prompt.");
        }

        RequireSha256(hostedNormalizedReadbackSha256, nameof(hostedNormalizedReadbackSha256));
        RequireExact(requiredLabel, nameof(requiredLabel));
        if ((fallbackFile is null) != (fallbackSha256 is null))
        {
            throw new InvalidDataException("Fallback file and hash must either both be present or both be absent.");
        }
        if (fallbackFile is not null)
        {
            RequireExact(fallbackFile, nameof(fallbackFile));
            RequireSha256(fallbackSha256!, nameof(fallbackSha256));
        }

        return new(modelConfig, hostedNormalizedReadbackSha256, requiredLabel, fallbackFile, fallbackSha256);
    }

    internal static void RequireExact(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl))
            throw new InvalidDataException($"{name} must be exact canonical text.");
    }

    public void RequireResolved(ResolvedPredictionPromptTemplate resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        RequireProvenance(resolved.Provenance);
    }

    public void RequireExactPolicy(PredictionPromptExecutionRequirement actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (ModelConfig != actual.ModelConfig
            || HostedNormalizedReadbackSha256 != actual.HostedNormalizedReadbackSha256
            || RequiredLabel != actual.RequiredLabel
            || FallbackFile != actual.FallbackFile
            || FallbackSha256 != actual.FallbackSha256)
            throw new InvalidDataException("Prompt execution requirement does not match the exact registered policy.");
    }

    public void RequireProvenance(PredictionPromptProvenanceV2 provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.HostedName != HostedName
            || provenance.HostedVersion != HostedVersion
            || provenance.HostedNormalizedReadbackSha256 != HostedNormalizedReadbackSha256
            || provenance.RequiredLabel != RequiredLabel
            || !provenance.RequiredLabelMembership)
            throw new InvalidDataException("Prompt provenance does not match the exact execution requirement.");
        if (provenance.ActualSource == PredictionPromptSourceV2.CheckedInFallback
            && (provenance.ActualFallbackFile != FallbackFile
                || provenance.ActualFallbackSha256 != FallbackSha256))
            throw new InvalidDataException("Prompt provenance does not match the exact pinned fallback.");
    }

    internal static void RequireSha256(string value, string name)
    {
        if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)) || value != value.ToLowerInvariant())
            throw new InvalidDataException($"{name} must be a lowercase SHA-256 value.");
    }
}

public sealed class ResolvedPredictionPromptTemplate
{
    private ResolvedPredictionPromptTemplate(
        string template, string path, PredictionPromptProvenanceV2 provenance) =>
        (Template, Path, Provenance) = (template, path, provenance);

    public string Template { get; }
    public string Path { get; }
    public PredictionPromptProvenanceV2 Provenance { get; }

    public static ResolvedPredictionPromptTemplate CreateHosted(
        PredictionPromptExecutionRequirement requirement,
        string template,
        string path,
        string actualName,
        int actualVersion,
        IEnumerable<string> actualLabels)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(actualLabels);
        var labels = actualLabels.ToImmutableArray();
        if (actualName != requirement.HostedName || actualVersion != requirement.HostedVersion
            || !labels.Contains(requirement.RequiredLabel, StringComparer.Ordinal))
            throw new InvalidDataException("Hosted prompt identity or required label drifted.");
        var actualHash = ValidateTemplateAndPath(template, path);
        if (actualHash != requirement.HostedNormalizedReadbackSha256)
            throw new InvalidDataException("Hosted prompt normalized readback hash drifted.");
        return new(template, path, PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.Hosted, actualName, actualVersion, actualHash,
            requirement.RequiredLabel, requiredLabelMembership: true));
    }

    public static ResolvedPredictionPromptTemplate CreateFallback(
        PredictionPromptExecutionRequirement requirement, string template, string path)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirement.FallbackFile is null || requirement.FallbackSha256 is null
            || path != requirement.FallbackFile)
            throw new InvalidDataException("Fallback prompt path is not the exact pinned file.");
        var actualHash = ValidateTemplateAndPath(template, path);
        if (actualHash != requirement.FallbackSha256)
            throw new InvalidDataException("Fallback prompt hash drifted.");
        return new(template, path, PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.CheckedInFallback, requirement.HostedName,
            requirement.HostedVersion, requirement.HostedNormalizedReadbackSha256,
            requirement.RequiredLabel, requiredLabelMembership: true, path, actualHash));
    }

    private static string ValidateTemplateAndPath(string template, string path)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new InvalidDataException("Resolved prompt template is empty.");
        PredictionPromptExecutionRequirement.RequireExact(path, nameof(path));
        return PromptTemplateContentHash.ComputeSha256(template);
    }
}

public interface IObservedInstructionsTemplateProvider
{
    ValueTask<ResolvedPredictionPromptTemplate> LoadObservedMatchTemplateAsync(
        PredictionPromptExecutionRequirement requirement,
        bool includeJustification,
        CancellationToken cancellationToken = default);

    ValueTask<ResolvedPredictionPromptTemplate> LoadObservedBonusTemplateAsync(
        PredictionPromptExecutionRequirement requirement,
        CancellationToken cancellationToken = default);
}

public sealed class ObservedPredictionCallEvidence
{
    private ObservedPredictionCallEvidence(
        PredictionPromptExecutionRequirement promptRequirement,
        ResolvedPredictionPromptTemplate prompt,
        PredictionServiceTierProvenanceV2 serviceTier,
        PredictionGenerationUsageV2 usage) =>
        (PromptRequirement, Prompt, ServiceTier, Usage) =
        (promptRequirement, prompt, serviceTier, usage);

    public PredictionPromptExecutionRequirement PromptRequirement { get; }
    public PredictionModelConfig ModelConfig => PromptRequirement.ModelConfig;
    public ResolvedPredictionPromptTemplate Prompt { get; }
    public PredictionServiceTierProvenanceV2 ServiceTier { get; }
    public PredictionGenerationUsageV2 Usage { get; }

    public static ObservedPredictionCallEvidence Create(
        PredictionPromptExecutionRequirement promptRequirement,
        ResolvedPredictionPromptTemplate prompt,
        PredictionServiceTierProvenanceV2 serviceTier,
        PredictionGenerationUsageV2 usage)
    {
        ArgumentNullException.ThrowIfNull(promptRequirement);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(serviceTier);
        ArgumentNullException.ThrowIfNull(usage);
        promptRequirement.RequireResolved(prompt);
        return new(promptRequirement, prompt, serviceTier, usage);
    }
}

public sealed class ObservedMatchPredictionResult
{
    private ObservedMatchPredictionResult(Prediction prediction, ObservedPredictionCallEvidence evidence) =>
        (Prediction, Evidence) = (Copy(prediction), evidence);
    public Prediction Prediction { get; }
    public ObservedPredictionCallEvidence Evidence { get; }
    public static ObservedMatchPredictionResult Create(Prediction prediction, ObservedPredictionCallEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(prediction); ArgumentNullException.ThrowIfNull(evidence);
        if (prediction.HomeGoals < 0 || prediction.AwayGoals < 0) throw new InvalidDataException("Goals cannot be negative.");
        return new(prediction, evidence);
    }
    private static Prediction Copy(Prediction value) => value.Justification is null
        ? new(value.HomeGoals, value.AwayGoals)
        : new(value.HomeGoals, value.AwayGoals, new PredictionJustification(
            value.Justification.KeyReasoning,
            new PredictionJustificationContextSources(
                value.Justification.ContextSources.MostValuable.ToImmutableArray(),
                value.Justification.ContextSources.LeastValuable.ToImmutableArray()),
            value.Justification.Uncertainties.ToImmutableArray()));
}

public sealed class ObservedBonusPredictionResult
{
    private readonly ImmutableArray<string> _selected;
    private ObservedBonusPredictionResult(IEnumerable<string> selected, ObservedPredictionCallEvidence evidence)
        { _selected = selected.ToImmutableArray(); Evidence = evidence; }
    public IReadOnlyList<string> SelectedOptionIds => _selected;
    public ObservedPredictionCallEvidence Evidence { get; }
    public BonusPrediction ToBonusPrediction() => new(_selected.ToList());
    public static ObservedBonusPredictionResult Create(BonusPrediction prediction, ObservedPredictionCallEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(prediction); ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(prediction.SelectedOptionIds);
        var selected = prediction.SelectedOptionIds.ToArray();
        if (selected.Length == 0 || selected.Any(string.IsNullOrWhiteSpace)
            || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new InvalidDataException("Observed bonus selections must be exact and unique.");
        return new(selected, evidence);
    }
}

public sealed class ObservedPredictionException : Exception
{
    public ObservedPredictionException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public interface IObservedPredictionService
{
    Task<ObservedMatchPredictionResult> PredictObservedMatchAsync(
        Match match, IEnumerable<DocumentContext> contextDocuments,
        PredictionPromptExecutionRequirement requirement, bool includeJustification = false,
        PredictionTelemetryMetadata? telemetryMetadata = null, CancellationToken cancellationToken = default);
    Task<ObservedBonusPredictionResult> PredictObservedBonusQuestionAsync(
        BonusQuestion question, IEnumerable<DocumentContext> contextDocuments,
        PredictionPromptExecutionRequirement requirement,
        PredictionTelemetryMetadata? telemetryMetadata = null, CancellationToken cancellationToken = default);
}
