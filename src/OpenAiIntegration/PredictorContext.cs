using Core;

namespace OpenAiIntegration;

/// <summary>
/// Context for making predictions with OpenAI.
/// For MVP, this is a simple container that can be extended with additional context later.
/// </summary>
public class PredictorContext
{
    /// <summary>
    /// Additional documents/context that can be used for prediction (optional for MVP)
    /// </summary>
    public IReadOnlyList<DocumentContext> Documents { get; set; } = new List<DocumentContext>();

    /// <summary>
    /// Additional instructions or context for the AI predictor
    /// </summary>
    public string? AdditionalInstructions { get; set; }

    /// <summary>
    /// Creates a basic context for MVP usage
    /// </summary>
    public static PredictorContext CreateBasic(string? additionalInstructions = null)
    {
        return new PredictorContext
        {
            AdditionalInstructions = additionalInstructions
        };
    }
}
