using NodaTime;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Represents a bonus question that can be answered by selecting from available options.
/// </summary>
public record BonusQuestion(
    string Text,
    ZonedDateTime Deadline,
    List<BonusQuestionOption> Options,
    int MaxSelections,
    string? FormFieldName = null
);

/// <summary>
/// Represents an option for a bonus question.
/// </summary>
public record BonusQuestionOption(
    string Id,
    string Text
);

/// <summary>
/// Represents a prediction for a bonus question.
/// </summary>
public record BonusPrediction(
    List<string> SelectedOptionIds
);

/// <summary>
/// Compares bonus-prediction payloads by their persisted, ordered selection content.
/// </summary>
public static class BonusPredictionContentEquality
{
    public static bool Equals(BonusPrediction? left, BonusPrediction? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null
            && right is not null
            && left.SelectedOptionIds.SequenceEqual(right.SelectedOptionIds, StringComparer.Ordinal);
    }
}

/// <summary>
/// Extended bonus prediction result that includes metadata about how the prediction was generated.
/// </summary>
public record BonusPredictionResult(
    BonusPrediction BonusPrediction,
    string Model,
    string TokenUsage,
    double Cost,
    string CommunityContext,
    List<string> ContextDocumentNames);

/// <summary>
/// Bonus prediction metadata for outdated checks and verification.
/// Includes context document names and creation timestamp.
/// </summary>
public record BonusPredictionMetadata(
    BonusPrediction BonusPrediction,
    DateTimeOffset CreatedAt,
    List<string> ContextDocumentNames,
    ResolvedBonusContextManifest? ResolvedContextManifest = null,
    BonusQuestionCompatibilityManifest? QuestionCompatibilityManifest = null,
    string? PredictionIdentity = null);
