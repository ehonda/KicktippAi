using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

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
                MaxOutputTokenCount = 10_000 // Safeguard against high costs
            });

        // Output results with clean formatting
        Console.WriteLine();
        Console.WriteLine("=== PREDICTION ===");
        Console.WriteLine(response.Value.Content[0].Text);
        Console.WriteLine();

        Console.WriteLine("=== TOKEN USAGE ===");
        var usage = response.Value.Usage;
        var usageJson = JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true });
        
        Console.WriteLine(usageJson);
    }
}
