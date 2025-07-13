using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using PromptSampleTests.Models;

namespace PromptSampleTests;

public class PromptTestRunner
{
    private readonly ILogger<PromptTestRunner> _logger;

    public PromptTestRunner(ILogger<PromptTestRunner> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(string model, string promptSampleDirectory)
    {
        // Validate directory exists
        if (!Directory.Exists(promptSampleDirectory))
        {
            throw new DirectoryNotFoundException($"Prompt sample directory not found: {promptSampleDirectory}");
        }

        // Load API key from environment (now loaded from .env file)
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set. Please check your .env file or set the environment variable directly.");
        }

        // Load instructions and match data
        var instructionsPath = Path.Combine(promptSampleDirectory, "instructions.md");
        var matchPath = Path.Combine(promptSampleDirectory, "match.json");

        if (!File.Exists(instructionsPath))
        {
            throw new FileNotFoundException($"Instructions file not found: {instructionsPath}");
        }

        if (!File.Exists(matchPath))
        {
            throw new FileNotFoundException($"Match file not found: {matchPath}");
        }

        var instructions = await File.ReadAllTextAsync(instructionsPath);
        var matchJson = await File.ReadAllTextAsync(matchPath);

        // Use Console.WriteLine for clean main output
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Prompt Directory: {promptSampleDirectory}");
        Console.WriteLine($"Instructions loaded: {instructions.Length} characters");
        Console.WriteLine($"Match JSON: {matchJson.Trim()}");
        Console.WriteLine("Using structured output format");
        Console.WriteLine();

        // Create ChatClient and run prediction
        var chatClient = new ChatClient(model, apiKey);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(instructions),
            new UserChatMessage(matchJson)
        };

        Console.WriteLine("Calling OpenAI API...");
        var response = await chatClient.CompleteChatAsync(
            messages,
            new()
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
            });

        // Output results with clean formatting
        Console.WriteLine();
        Console.WriteLine("=== STRUCTURED PREDICTION ===");
        var predictionJson = response.Value.Content[0].Text;
        Console.WriteLine(predictionJson);
        
        // Parse and display the structured prediction
        try
        {
            var prediction = JsonSerializer.Deserialize<MatchPrediction>(predictionJson);
            Console.WriteLine();
            Console.WriteLine("=== PARSED PREDICTION ===");
            Console.WriteLine($"Home Goals: {prediction!.Home}");
            Console.WriteLine($"Away Goals: {prediction.Away}");
            Console.WriteLine($"Final Score: {prediction.Home}:{prediction.Away}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing structured output: {ex.Message}");
        }
        Console.WriteLine();

        Console.WriteLine("=== TOKEN USAGE ===");
        var usage = response.Value.Usage;
        var usageJson = JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true });
        
        Console.WriteLine(usageJson);

        // Calculate and display cost information
        Console.WriteLine();
        Console.WriteLine("=== COST ANALYSIS ===");
        
        if (ModelPricingData.Pricing.TryGetValue(model, out var pricing))
        {
            // Get exact token counts from usage details
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens;
            var outputTokens = usage.OutputTokenCount;
            
            // Calculate costs for each component
            var uncachedInputCost = (uncachedInputTokens / 1_000_000m) * pricing.InputPrice;
            var cachedInputCost = pricing.CachedInputPrice.HasValue 
                ? (cachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value 
                : 0m;
            var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPrice;
            var totalCost = uncachedInputCost + cachedInputCost + outputCost;
            
            // Output the 4 cost analysis lines using invariant culture
            Console.WriteLine($"Uncached Input Tokens: {uncachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} × ${pricing.InputPrice.ToString("F2", CultureInfo.InvariantCulture)}/1M = ${uncachedInputCost.ToString("F6", CultureInfo.InvariantCulture)}");
            if (pricing.CachedInputPrice.HasValue && cachedInputTokens > 0)
            {
                Console.WriteLine($"Cached Input Tokens: {cachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} × ${pricing.CachedInputPrice.Value.ToString("F3", CultureInfo.InvariantCulture)}/1M = ${cachedInputCost.ToString("F6", CultureInfo.InvariantCulture)}");
            }
            else if (pricing.CachedInputPrice.HasValue)
            {
                Console.WriteLine($"Cached Input Tokens: {cachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} × ${pricing.CachedInputPrice.Value.ToString("F3", CultureInfo.InvariantCulture)}/1M = ${cachedInputCost.ToString("F6", CultureInfo.InvariantCulture)}");
            }
            Console.WriteLine($"Output Tokens: {outputTokens.ToString("N0", CultureInfo.InvariantCulture)} × ${pricing.OutputPrice.ToString("F2", CultureInfo.InvariantCulture)}/1M = ${outputCost.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Total Cost: ${totalCost.ToString("F6", CultureInfo.InvariantCulture)}");
        }
        else
        {
            Console.WriteLine($"Cost calculation not available: Pricing information not found for model '{model}'");
        }
    }
}
