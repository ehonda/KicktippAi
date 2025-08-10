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
    string CommunityContext);
