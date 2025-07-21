using System.Text.Json.Serialization;

namespace PromptSampleTests.Models;

/// <summary>
/// Simple and concise match prediction response format for structured outputs
/// </summary>
public class MatchPrediction
{
    /// <summary>
    /// Predicted goals for the home team
    /// </summary>
    [JsonPropertyName("home")]
    public int Home { get; set; }

    /// <summary>
    /// Predicted goals for the away team  
    /// </summary>
    [JsonPropertyName("away")]
    public int Away { get; set; }
}
