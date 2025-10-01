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
    private readonly string _bonusInstructionsTemplate;
    private readonly string _matchPromptPath;
    private readonly string _bonusPromptPath;

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
        
        var (matchTemplate, matchPath) = LoadInstructionsTemplate();
        var (bonusTemplate, bonusPath) = LoadBonusInstructionsTemplate();
        
        _instructionsTemplate = matchTemplate;
        _bonusInstructionsTemplate = bonusTemplate;
        _matchPromptPath = matchPath;
        _bonusPromptPath = bonusPath;
    }

    public async Task<Prediction?> PredictMatchAsync(
        Match match, 
        IEnumerable<DocumentContext> contextDocuments, 
        bool includeJustification = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating prediction for match: {HomeTeam} vs {AwayTeam} at {StartTime}", 
            match.HomeTeam, match.AwayTeam, match.StartsAt);

        try
        {
            // Build the instructions by combining template with context
            var instructions = BuildInstructions(contextDocuments, includeJustification);
            
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
                        jsonSchema: BinaryData.FromBytes(BuildPredictionJsonSchema(includeJustification)),
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
            Console.Error.WriteLine($"Prediction error for {match.HomeTeam} vs {match.AwayTeam}: {ex.Message}");
            
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

    private string BuildInstructions(IEnumerable<DocumentContext> contextDocuments, bool includeJustification)
    {
        var instructions = _instructionsTemplate;

        if (includeJustification)
        {
            instructions += "\n\nWhen producing the structured JSON response, also include a \"justification\" property containing a concise explanation (1-2 sentences) for the predicted scoreline. Reference key context insights, stay objective, and avoid repeating raw document text verbatim.";
        }
        
        var contextList = contextDocuments.ToList();
        if (contextList.Any())
        {
            var contextSection = "\n";
            foreach (var doc in contextList)
            {
                contextSection += "---\n";
                contextSection += $"{doc.Name}\n\n";
                contextSection += $"{doc.Content}\n";
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

    private static byte[] BuildPredictionJsonSchema(bool includeJustification)
    {
        var properties = new Dictionary<string, object?>
        {
            ["home"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "Predicted goals for the home team"
            },
            ["away"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "Predicted goals for the away team"
            }
        };

        var required = new List<string> { "home", "away" };

        if (includeJustification)
        {
            properties["justification"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Concise explanation for the predicted outcome"
            };
            required.Add("justification");
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return JsonSerializer.SerializeToUtf8Bytes(schema);
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

            var justification = string.IsNullOrWhiteSpace(predictionResponse.Justification)
                ? null
                : predictionResponse.Justification.Trim();

            if (!string.IsNullOrEmpty(justification))
            {
                _logger.LogDebug("Parsed justification: {Justification}", justification);
            }

            return new Prediction(predictionResponse.Home, predictionResponse.Away, justification);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse prediction JSON: {PredictionJson}", predictionJson);
            throw new InvalidOperationException($"Failed to parse prediction response: {ex.Message}", ex);
        }
    }

    private (string template, string path) LoadInstructionsTemplate()
    {
        var promptModel = GetPromptModelForModel(_model);
        
        // Try to find the model-specific instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var instructionsPath = Path.Combine(directory.FullName, "prompts", promptModel, "match.md");
                
                if (File.Exists(instructionsPath))
                {
                    return (File.ReadAllText(instructionsPath), instructionsPath);
                }
                
                throw new FileNotFoundException($"Match instructions not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate match instructions");
    }

    private string BuildBonusInstructions(IEnumerable<DocumentContext> contextDocuments)
    {
        // Use the pre-loaded bonus instructions template
        var bonusInstructionsTemplate = _bonusInstructionsTemplate;
        
        var contextList = contextDocuments.ToList();
        if (contextList.Any())
        {
            var contextSection = "\n";
            foreach (var doc in contextList)
            {
                contextSection += "---\n";
                contextSection += $"{doc.Name}\n\n";
                contextSection += $"{doc.Content}\n";
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

    private (string template, string path) LoadBonusInstructionsTemplate()
    {
        var promptModel = GetPromptModelForModel(_model);
        
        // Try to find the model-specific bonus instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var instructionsPath = Path.Combine(directory.FullName, "prompts", promptModel, "bonus.md");
                
                if (File.Exists(instructionsPath))
                {
                    return (File.ReadAllText(instructionsPath), instructionsPath);
                }
                
                throw new FileNotFoundException($"Bonus instructions not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate bonus instructions");
    }

    /// <summary>
    /// Maps a model name to the appropriate prompt directory, handling cross-model mappings
    /// </summary>
    /// <param name="model">The model name to map</param>
    /// <returns>The prompt directory name to use</returns>
    private static string GetPromptModelForModel(string model)
    {
        return model switch
        {
            // Direct mappings
            "o3" => "o3",
            "gpt-5" => "gpt-5",
            
            // Cross-model mappings
            "o4-mini" => "o3",
            "gpt-5-mini" => "gpt-5",
            "gpt-5-nano" => "gpt-5",
            
            // Default to the model name itself for any new models
            _ => model
        };
    }

    /// <summary>
    /// Gets the file path of the match prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the match prompt file</returns>
    public string GetMatchPromptPath() => _matchPromptPath;

    /// <summary>
    /// Gets the file path of the bonus question prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the bonus prompt file</returns>
    public string GetBonusPromptPath() => _bonusPromptPath;

    /// <summary>
    /// Internal class for deserializing the structured prediction response
    /// </summary>
    private class PredictionResponse
    {
        [JsonPropertyName("home")]
        public int Home { get; set; }
        
        [JsonPropertyName("away")]
        public int Away { get; set; }

        [JsonPropertyName("justification")]
        public string? Justification { get; set; }
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
