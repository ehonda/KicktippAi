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
    private readonly ITokenUsageTracker _tokenUsageTracker;
    private readonly string _model;
    private readonly string _instructionsTemplate;

    public PredictionService(
        ChatClient chatClient, 
        ILogger<PredictionService> logger,
        ICostCalculationService costCalculationService,
        ITokenUsageTracker tokenUsageTracker,
        string model)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _costCalculationService = costCalculationService ?? throw new ArgumentNullException(nameof(costCalculationService));
        _tokenUsageTracker = tokenUsageTracker ?? throw new ArgumentNullException(nameof(tokenUsageTracker));
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

            // Add usage to tracker
            _tokenUsageTracker.AddUsage(_model, usage);

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

    public async Task<BonusPrediction?> PredictBonusQuestionAsync(
        BonusQuestion bonusQuestion,
        IEnumerable<DocumentContext> contextDocuments,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating prediction for bonus question: {QuestionText}", bonusQuestion.Text);

        try
        {
            // Build the instructions by combining template with context
            var instructions = BuildBonusInstructions(contextDocuments);
            
            // Create bonus question JSON
            var questionJson = CreateBonusQuestionJson(bonusQuestion);
            
            _logger.LogDebug("Instructions length: {InstructionsLength} characters", instructions.Length);
            _logger.LogDebug("Context documents: {ContextCount}", contextDocuments.Count());
            _logger.LogDebug("Question JSON: {QuestionJson}", questionJson);

            // Create messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(instructions),
                new UserChatMessage(questionJson)
            };

            _logger.LogDebug("Calling OpenAI API for bonus prediction");

            // Create JSON schema based on the question
            var jsonSchema = CreateSingleBonusPredictionJsonSchema(bonusQuestion);

            // Call OpenAI with structured output format
            var response = await _chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 10_000, // Standard limit for single question
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "bonus_prediction",
                        jsonSchema: BinaryData.FromBytes(jsonSchema),
                        jsonSchemaIsStrict: true)
                },
                cancellationToken);

            // Parse the structured response
            var predictionJson = response.Value.Content[0].Text;
            _logger.LogDebug("Received bonus prediction JSON: {PredictionJson}", predictionJson);

            var prediction = ParseSingleBonusPrediction(predictionJson, bonusQuestion);
            
            if (prediction != null)
            {
                _logger.LogInformation("Generated prediction for bonus question: {SelectedOptions}", 
                    string.Join(", ", prediction.SelectedOptionIds));
            }

            // Log token usage and cost breakdown
            var usage = response.Value.Usage;
            _logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

            // Add usage to tracker
            _tokenUsageTracker.AddUsage(_model, usage);

            // Calculate and log costs
            _costCalculationService.LogCostBreakdown(_model, usage);

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating bonus prediction for question: {QuestionText}", bonusQuestion.Text);
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

    private string BuildBonusInstructions(IEnumerable<DocumentContext> contextDocuments)
    {
        // Load the bonus instructions template
        var bonusInstructionsTemplate = LoadBonusInstructionsTemplate();
        
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
            
            bonusInstructionsTemplate += contextSection;
            
            _logger.LogDebug("Added {ContextCount} context documents to bonus instructions", contextList.Count);
        }
        else
        {
            _logger.LogDebug("No context documents provided for bonus predictions");
        }

        return bonusInstructionsTemplate;
    }

    private static string CreateBonusQuestionJson(BonusQuestion question)
    {
        var questionData = new
        {
            text = question.Text,
            options = question.Options.Select(o => new { id = o.Id, text = o.Text }).ToArray(),
            maxSelections = question.MaxSelections
        };

        return JsonSerializer.Serialize(questionData, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static byte[] CreateSingleBonusPredictionJsonSchema(BonusQuestion question)
    {
        // For multi-selection questions, require exactly MaxSelections answers
        // For single-selection questions, require exactly 1 answer
        var requiredSelections = question.MaxSelections;
        
        var schema = new
        {
            type = "object",
            properties = new
            {
                selectedOptionIds = new
                {
                    type = "array",
                    items = new { type = "string", @enum = question.Options.Select(o => o.Id).ToArray() },
                    minItems = requiredSelections,
                    maxItems = requiredSelections
                }
            },
            required = new[] { "selectedOptionIds" },
            additionalProperties = false
        };

        return JsonSerializer.SerializeToUtf8Bytes(schema);
    }

    private BonusPrediction? ParseSingleBonusPrediction(string predictionJson, BonusQuestion question)
    {
        try
        {
            _logger.LogDebug("Parsing single bonus prediction JSON: {PredictionJson}", predictionJson);
            
            var response = JsonSerializer.Deserialize<SingleBonusPredictionResponse>(predictionJson);
            if (response?.SelectedOptionIds?.Any() != true)
            {
                throw new InvalidOperationException("Failed to deserialize bonus prediction response or no options selected");
            }

            // Validate that all selected options exist for this question
            var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
            var invalidOptions = response.SelectedOptionIds.Where(id => !validOptionIds.Contains(id)).ToArray();
            
            if (invalidOptions.Any())
            {
                _logger.LogWarning("Invalid option IDs for question '{QuestionText}': {InvalidOptions}", 
                    question.Text, string.Join(", ", invalidOptions));
                return null;
            }

            // Validate no duplicate selections
            var duplicateOptions = response.SelectedOptionIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
                
            if (duplicateOptions.Any())
            {
                _logger.LogWarning("Duplicate option IDs for question '{QuestionText}': {DuplicateOptions}", 
                    question.Text, string.Join(", ", duplicateOptions));
                return null;
            }

            // Validate selection count - must match exactly MaxSelections for full predictions
            if (response.SelectedOptionIds.Length != question.MaxSelections)
            {
                _logger.LogWarning("Invalid selection count for question '{QuestionText}': expected exactly {MaxSelections}, got {ActualCount}", 
                    question.Text, question.MaxSelections, response.SelectedOptionIds.Length);
                return null;
            }

            var prediction = new BonusPrediction(response.SelectedOptionIds.ToList());
            
            _logger.LogDebug("Parsed prediction: {SelectedOptions}", 
                string.Join(", ", response.SelectedOptionIds));

            return prediction;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse bonus prediction JSON: {PredictionJson}", predictionJson);
            return null;
        }
    }

    private static string LoadBonusInstructionsTemplate()
    {
        // Try to find the bonus instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var instructionsPath = Path.Combine(directory.FullName, "prompts", "reasoning-models", 
                    "predict-bonus-questions", "v0-handcrafted", "instructions_template.md");
                
                if (File.Exists(instructionsPath))
                {
                    return File.ReadAllText(instructionsPath);
                }
                
                throw new FileNotFoundException($"Bonus instructions template not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate bonus instructions template");
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

    /// <summary>
    /// Internal class for deserializing the bonus predictions response
    /// </summary>
    private class BonusPredictionsResponse
    {
        [JsonPropertyName("predictions")]
        public BonusPredictionEntry[]? Predictions { get; set; }
    }

    /// <summary>
    /// Internal class for deserializing individual bonus prediction entries
    /// </summary>
    private class BonusPredictionEntry
    {
        [JsonPropertyName("questionId")]
        public string QuestionId { get; set; } = string.Empty;
        
        [JsonPropertyName("selectedOptionIds")]
        public string[] SelectedOptionIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Internal class for deserializing single bonus prediction response
    /// </summary>
    private class SingleBonusPredictionResponse
    {
        [JsonPropertyName("selectedOptionIds")]
        public string[] SelectedOptionIds { get; set; } = Array.Empty<string>();
    }
}
