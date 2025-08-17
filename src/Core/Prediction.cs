namespace Core;

public record Prediction(
    int HomeGoals,
    int AwayGoals);

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
    List<string> ContextDocumentNames);
