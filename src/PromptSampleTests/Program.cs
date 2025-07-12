using System.Text.Json;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Core;

namespace PromptSampleTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Setup logging with minimal console output for diagnostics only
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => 
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.TimestampFormat = null;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            })
            .SetMinimumLevel(LogLevel.Information)); // Show info level for .env loading feedback
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: PromptSampleTests <model> <prompt-sample-directory>");
            Console.WriteLine("Example: PromptSampleTests gpt-4o-2024-08-06 \"c:\\path\\to\\2425_md34_rbl_vfb\"");
            return 1;
        }

        var model = args[0];
        var promptSampleDirectory = args[1];

        try
        {
            // Load environment variables from .env file
            LoadEnvironmentVariables(logger);

            var runner = new PromptTestRunner(logger);
            await runner.RunAsync(model, promptSampleDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while running prompt test: {Message}", ex.Message);
            return 1;
        }
    }

    private static void LoadEnvironmentVariables(ILogger logger)
    {
        try
        {
            // Try to find .env file in secrets directory
            var envPath = FindEnvFile();
            
            if (envPath != null && File.Exists(envPath))
            {
                Env.Load(envPath);
                logger.LogInformation("Loaded .env file from: {EnvPath}", envPath);
            }
            else
            {
                logger.LogWarning("No .env file found. Please create one in the secrets directory based on .env.example");
                logger.LogInformation("Expected location: KicktippAi.Secrets/src/PromptSampleTests/.env (sibling to solution directory)");
                logger.LogInformation("Alternatively, set OPENAI_API_KEY environment variable directly");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load .env file");
        }
    }
    
    private static string? FindEnvFile()
    {
        // Start from the current assembly location and work our way up to find the solution directory
        var currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        
        while (currentDir != null)
        {
            // Look for solution file indicators (like .slnx or .sln files)
            var solutionFiles = Directory.GetFiles(currentDir, "*.sln*", SearchOption.TopDirectoryOnly);
            
            if (solutionFiles.Length > 0)
            {
                // Found solution directory, now look for sibling secrets directory
                var parentDir = Path.GetDirectoryName(currentDir);
                if (parentDir != null)
                {
                    var secretsDir = Path.Combine(parentDir, "KicktippAi.Secrets");
                    var envPath = Path.Combine(secretsDir, "src", "PromptSampleTests", ".env");
                    
                    if (File.Exists(envPath))
                    {
                        return envPath;
                    }
                }
                break; // Don't continue searching beyond the solution directory
            }
            
            currentDir = Path.GetDirectoryName(currentDir);
        }
        
        // Fallback: try relative to current working directory
        var currentWorkingDir = Directory.GetCurrentDirectory();
        var fallbackPath = Path.Combine(currentWorkingDir, "..", "..", "..", "..", "KicktippAi.Secrets", "src", "PromptSampleTests", ".env");
        
        if (File.Exists(fallbackPath))
        {
            return Path.GetFullPath(fallbackPath);
        }
        
        return null;
    }
}

public class PromptTestRunner
{
    private readonly ILogger _logger;

    public PromptTestRunner(ILogger logger)
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
        var response = await chatClient.CompleteChatAsync(messages);

        // Output results with clean formatting
        Console.WriteLine();
        Console.WriteLine("=== PREDICTION ===");
        Console.WriteLine(response.Value.Content[0].Text);
        Console.WriteLine();

        Console.WriteLine("=== TOKEN USAGE ===");
        var usage = response.Value.Usage;
        var usageJson = JsonSerializer.Serialize(new
        {
            input_tokens = usage.InputTokenCount,
            output_tokens = usage.OutputTokenCount,
            total_tokens = usage.TotalTokenCount,
            input_tokens_details = new
            {
                cached_tokens = 0 // OpenAI .NET client doesn't expose this currently
            },
            output_tokens_details = new
            {
                reasoning_tokens = 0 // OpenAI .NET client doesn't expose this currently
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        
        Console.WriteLine(usageJson);
    }
}
