using System.Text.Json;
using System.Text.Json.Serialization;
using Core;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for predicting match outcomes using OpenAI models
/// </summary>
public class PredictionService : IPredictionService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<PredictionService> _logger;
    private readonly ICostCalculationService _costCalculationService;
    private readonly string _model;
    private readonly string _instructionsTemplate;

    public PredictionService(
        ChatClient chatClient, 
        ILogger<PredictionService> logger,
        ICostCalculationService costCalculationService,
        string model)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _costCalculationService = costCalculationService ?? throw new ArgumentNullException(nameof(costCalculationService));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _instructionsTemplate = LoadInstructionsTemplate();
    }

    public async Task<Prediction?> PredictMatchAsync(
        Match match, 
        IEnumerable<DocumentContext> contextDocuments, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating prediction for match: {HomeTeam} vs {AwayTeam} at {StartTime}", 
            match.HomeTeam, match.AwayTeam, match.StartsAt);

        try
        {
            // Build the instructions by combining template with context
            var instructions = BuildInstructions(contextDocuments);
            
            // Create match JSON
            var matchJson = CreateMatchJson(match);
            
            _logger.LogDebug("Instructions length: {InstructionsLength} characters", instructions.Length);
            _logger.LogDebug("Context documents: {ContextCount}", contextDocuments.Count());
            _logger.LogDebug("Match JSON: {MatchJson}", matchJson);

            // Create messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(instructions),
                new UserChatMessage(matchJson)
            };

            _logger.LogDebug("Calling OpenAI API for prediction");

            // Call OpenAI with structured output format
            var response = await _chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 10_000, // Safeguard against high costs
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "match_prediction",
                        jsonSchema: BinaryData.FromBytes("""
                            {
                                "type": "object",
                                "properties": {
                                    "home": {
                                        "type": "integer",
                                        "description": "Predicted goals for the home team"
                                    },
                                    "away": {
                                        "type": "integer", 
                                        "description": "Predicted goals for the away team"
                                    }
                                },
                                "required": ["home", "away"],
                                "additionalProperties": false
                            }
                            """u8.ToArray()),
                        jsonSchemaIsStrict: true)
                },
                cancellationToken);

            // Parse the structured response
            var predictionJson = response.Value.Content[0].Text;
            _logger.LogDebug("Received prediction JSON: {PredictionJson}", predictionJson);

            var prediction = ParsePrediction(predictionJson);
            
            _logger.LogInformation("Prediction generated: {HomeGoals}-{AwayGoals} for {HomeTeam} vs {AwayTeam}", 
                prediction.HomeGoals, prediction.AwayGoals, match.HomeTeam, match.AwayTeam);

            // Log token usage and cost breakdown
            var usage = response.Value.Usage;
            _logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

            // Calculate and log costs
            _costCalculationService.LogCostBreakdown(_model, usage);

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prediction for match: {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            
            return null;
        }
    }

    private string BuildInstructions(IEnumerable<DocumentContext> contextDocuments)
    {
        var instructions = _instructionsTemplate;
        
        var contextList = contextDocuments.ToList();
        if (contextList.Any())
        {
            var contextSection = "\n";
            foreach (var doc in contextList)
            {
                contextSection += "---\n\n";
                contextSection += $"{doc.Name}\n\n";
                contextSection += $"{doc.Content}\n\n";
            }
            contextSection += "---";
            
            instructions += contextSection;
            
            _logger.LogDebug("Added {ContextCount} context documents to instructions", contextList.Count);
        }
        else
        {
            _logger.LogDebug("No context documents provided");
        }

        return instructions;
    }

    private static string CreateMatchJson(Match match)
    {
        return JsonSerializer.Serialize(new
        {
            homeTeam = match.HomeTeam,
            awayTeam = match.AwayTeam,
            startsAt = match.StartsAt.ToString()
        }, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private Prediction ParsePrediction(string predictionJson)
    {
        try
        {
            _logger.LogDebug("Parsing prediction JSON: {PredictionJson}", predictionJson);
            
            var predictionResponse = JsonSerializer.Deserialize<PredictionResponse>(predictionJson);
            if (predictionResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize prediction response");
            }

            _logger.LogDebug("Parsed prediction response - Home: {Home}, Away: {Away}", predictionResponse.Home, predictionResponse.Away);

            return new Prediction(predictionResponse.Home, predictionResponse.Away);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse prediction JSON: {PredictionJson}", predictionJson);
            throw new InvalidOperationException($"Failed to parse prediction response: {ex.Message}", ex);
        }
    }

    private static string LoadInstructionsTemplate()
    {
        // Try to find the instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var instructionsPath = Path.Combine(directory.FullName, "prompts", "reasoning-models", 
                    "predict-one-match", "v0-handcrafted", "instructions_template.md");
                
                if (File.Exists(instructionsPath))
                {
                    return File.ReadAllText(instructionsPath);
                }
                
                throw new FileNotFoundException($"Instructions template not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate instructions template");
    }

    /// <summary>
    /// Internal class for deserializing the structured prediction response
    /// </summary>
    private class PredictionResponse
    {
        [JsonPropertyName("home")]
        public int Home { get; set; }
        
        [JsonPropertyName("away")]
        public int Away { get; set; }
    }
}
