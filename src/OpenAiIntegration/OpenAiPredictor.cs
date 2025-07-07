using System.ClientModel;
using Core;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiIntegration;

public class OpenAiPredictor : IPredictor<PredictorContext>
{
    private readonly ChatClient _client;
    private readonly ILogger<OpenAiPredictor> _logger;

    public OpenAiPredictor(ChatClient client, ILogger<OpenAiPredictor> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Prediction> PredictAsync(Match match, PredictorContext context)
    {
        _logger.LogInformation("Generating prediction for match: {HomeTeam} vs {AwayTeam} at {StartTime}", 
            match.HomeTeam, match.AwayTeam, match.StartsAt);

        try
        {
            var prompt = GeneratePrompt(match, context);
            _logger.LogDebug("Generated prompt: {Prompt}", prompt);

            var response = await _client.CompleteChatAsync(prompt);
            _logger.LogDebug("Received response from OpenAI");

            var prediction = ParsePrediction(response);
            _logger.LogInformation("Prediction generated: {HomeGoals}-{AwayGoals} for {HomeTeam} vs {AwayTeam}", 
                prediction.HomeGoals, prediction.AwayGoals, match.HomeTeam, match.AwayTeam);

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prediction for match: {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            
            // Return a fallback prediction in case of error
            _logger.LogWarning("Returning fallback prediction (1-1) due to error");
            return new Prediction(1, 1);
        }
    }

    private string GeneratePrompt(Match match, PredictorContext context)
    {
        var prompt = $@"You are a football prediction expert. Predict the final score for this match:

Match: {match.HomeTeam} vs {match.AwayTeam}
Kick-off: {match.StartsAt:yyyy-MM-dd HH:mm}

Please provide your prediction in the following format only:
HOME_GOALS-AWAY_GOALS

For example: 2-1

Consider:
- Home advantage (home teams typically score slightly more)
- Recent form and performance
- Common football scores (0-0, 1-0, 1-1, 2-0, 2-1, etc.)

Your prediction:";

        return prompt;
    }

    private Prediction ParsePrediction(ClientResult<ChatCompletion>? response)
    {
        try
        {
            if (response?.Value?.Content == null || !response.Value.Content.Any())
            {
                _logger.LogWarning("No content in OpenAI response, using fallback prediction");
                return new Prediction(1, 1);
            }

            var content = response.Value.Content[0].Text?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Empty content in OpenAI response, using fallback prediction");
                return new Prediction(1, 1);
            }

            _logger.LogDebug("Parsing response content: {Content}", content);

            // Look for pattern like "2-1" in the response
            var scorePattern = System.Text.RegularExpressions.Regex.Match(content, @"(\d+)-(\d+)");
            
            if (scorePattern.Success)
            {
                var homeGoals = int.Parse(scorePattern.Groups[1].Value);
                var awayGoals = int.Parse(scorePattern.Groups[2].Value);
                
                // Validate reasonable score range (0-10 goals per team)
                if (homeGoals >= 0 && homeGoals <= 10 && awayGoals >= 0 && awayGoals <= 10)
                {
                    return new Prediction(homeGoals, awayGoals);
                }
                else
                {
                    _logger.LogWarning("Parsed scores out of reasonable range: {HomeGoals}-{AwayGoals}, using fallback", 
                        homeGoals, awayGoals);
                    return new Prediction(1, 1);
                }
            }
            else
            {
                _logger.LogWarning("Could not parse score from response: {Content}, using fallback prediction", content);
                return new Prediction(1, 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing prediction response, using fallback prediction");
            return new Prediction(1, 1);
        }
    }
}
