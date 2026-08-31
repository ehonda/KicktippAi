namespace EHonda.KicktippAi.Core;

public record Prediction(
    int HomeGoals,
    int AwayGoals,
    PredictionJustification? Justification = null);

public record PredictionJustification(
    string KeyReasoning,
    PredictionJustificationContextSources ContextSources,
    IReadOnlyList<string> Uncertainties);

public record PredictionJustificationContextSources(
    IReadOnlyList<PredictionJustificationContextSource> MostValuable,
    IReadOnlyList<PredictionJustificationContextSource> LeastValuable);

public record PredictionJustificationContextSource(
    string DocumentName,
    string Details);

/// <summary>
/// Compares persisted prediction values independently of collection implementation details.
/// Record-generated equality is intentionally not used because the justification contains
/// interface-typed lists, whose generated equality is reference based.
/// </summary>
public static class PredictionContentEquality
{
    public static bool Equals(Prediction? left, Prediction? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null
            || left.HomeGoals != right.HomeGoals
            || left.AwayGoals != right.AwayGoals)
        {
            return false;
        }

        return JustificationsEqual(left.Justification, right.Justification);
    }

    private static bool JustificationsEqual(PredictionJustification? left, PredictionJustification? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null
            || !string.Equals(left.KeyReasoning, right.KeyReasoning, StringComparison.Ordinal)
            || left.ContextSources is null || right.ContextSources is null)
        {
            return false;
        }

        return SourcesEqual(left.ContextSources.MostValuable, right.ContextSources.MostValuable)
            && SourcesEqual(left.ContextSources.LeastValuable, right.ContextSources.LeastValuable)
            && StringsEqual(left.Uncertainties, right.Uncertainties);
    }

    private static bool SourcesEqual(
        IReadOnlyList<PredictionJustificationContextSource>? left,
        IReadOnlyList<PredictionJustificationContextSource>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        return left.Zip(right).All(pair => pair.First is not null
            && pair.Second is not null
            && string.Equals(pair.First.DocumentName, pair.Second.DocumentName, StringComparison.Ordinal)
            && string.Equals(pair.First.Details, pair.Second.Details, StringComparison.Ordinal));
    }

    private static bool StringsEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null && right is not null && left.SequenceEqual(right, StringComparer.Ordinal);
    }
}

/// <summary>
/// Extended prediction result that includes metadata about how the prediction was generated.
/// </summary>
public record PredictionResult(
    Prediction Prediction,
    string Model,
    string TokenUsage,
    double Cost,
    string CommunityContext,
    List<string> ContextDocumentNames);

/// <summary>
/// Prediction metadata for outdated checks and verification.
/// Includes context document names and creation timestamp.
/// </summary>
public record PredictionMetadata(
    Prediction Prediction,
    DateTimeOffset CreatedAt,
    List<string> ContextDocumentNames,
    ResolvedMatchContextManifest? ResolvedContextManifest = null,
    ResolvedTypedContextManifest? ResolvedTypedContextManifest = null);

/// <summary>Optional capability for prediction stores that preserve immutable context provenance.</summary>
public interface IResolvedMatchContextPredictionRepository
{
    Task SavePredictionWithResolvedContextAsync(
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedMatchContextManifest resolvedContextManifest,
        bool overrideCreatedAt = false,
        CancellationToken cancellationToken = default);

    Task SaveRepredictionWithResolvedContextAsync(
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        int expectedCurrentRepredictionIndex,
        int maxRepredictions,
        ResolvedMatchContextManifest resolvedContextManifest,
        CancellationToken cancellationToken = default);

    Task<ResolvedMatchContextManifest?> GetResolvedMatchContextManifestAsync(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional capability for immutable ADR-0060 rules-only generation provenance.</summary>
public interface IResolvedTypedContextPredictionRepository
{
    Task SavePredictionWithResolvedTypedContextAsync(
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedTypedContextManifest resolvedTypedContextManifest,
        bool overrideCreatedAt = false,
        CancellationToken cancellationToken = default);

    Task SaveBonusPredictionWithResolvedTypedContextAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedTypedContextManifest resolvedTypedContextManifest,
        bool overrideCreatedAt = false,
        CancellationToken cancellationToken = default);

    Task<PredictionMetadata?> GetCurrentTypedPredictionMetadataAsync(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default,
        DateTimeOffset? evaluationInstant = null);

    Task<BonusPredictionMetadata?> GetCurrentTypedBonusPredictionMetadataAsync(
        BonusQuestion bonusQuestion,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default,
        DateTimeOffset? evaluationInstant = null);
}
