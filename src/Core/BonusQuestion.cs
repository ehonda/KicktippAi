using NodaTime;

namespace Core;

/// <summary>
/// Represents a bonus question that can be answered by selecting from available options.
/// </summary>
public record BonusQuestion(
    string Id,
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
    string QuestionId,
    List<string> SelectedOptionIds
);

/// <summary>
/// Extended bonus prediction result that includes metadata about how the prediction was generated.
/// </summary>
public record BonusPredictionResult(
    BonusPrediction BonusPrediction,
    string Model,
    string TokenUsage,
    double Cost,
    string CommunityContext);
