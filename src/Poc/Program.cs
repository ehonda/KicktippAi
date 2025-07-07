using DotNetEnv;
using KicktippAi.Poc.Models;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KicktippAi.Poc;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup dependency injection and logging first
        var serviceCollection = new ServiceCollection();
        ConfigureLogging(serviceCollection);
        
        // Build a temporary service provider to get logger for startup
        var tempServiceProvider = serviceCollection.BuildServiceProvider();
        var logger = tempServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Kicktipp.de Automation POC");
        logger.LogInformation("==========================");
        
        try
        {
            // Load environment variables from .env file
            LoadEnvironmentVariables(logger);
            
            // Get credentials from environment
            var credentials = LoadCredentials();
            if (!credentials.IsValid)
            {
                logger.LogError("Please set KICKTIPP_USERNAME and KICKTIPP_PASSWORD in your .env file");
                logger.LogInformation("You can use .env.example as a template.");
                return;
            }

            // Setup dependency injection with credentials
            ConfigureServices(serviceCollection, credentials);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            
            // Get the client from DI - authentication is handled automatically
            var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
            
            logger.LogInformation("Attempting to login with username: {Username}", credentials.Username);
            
            // Test fetching open predictions for ehonda-test community
            // Authentication happens automatically via the authentication handler
            logger.LogInformation("Fetching open predictions for ehonda-test community...");
            var openPredictions = await kicktippClient.GetOpenPredictionsAsync("ehonda-test");
                
            if (openPredictions.Any())
            {
                logger.LogInformation("✓ Found {MatchCount} open matches:", openPredictions.Count);
                logger.LogInformation("");
                foreach (var match in openPredictions)
                {
                    logger.LogInformation("  {Match}", match);
                }
                
                // Generate random bets for these matches
                logger.LogInformation("");
                logger.LogInformation("Generating random bets for open predictions...");
                
                var predictor = new SimplePredictor();
                var bets = new Dictionary<Core.Match, KicktippIntegration.BetPrediction>();
                
                foreach (var match in openPredictions)
                {
                    var pocPrediction = predictor.Predict(ConvertToPocMatch(match));
                    var integrationPrediction = new KicktippIntegration.BetPrediction(
                        pocPrediction.HomeGoals, 
                        pocPrediction.AwayGoals);
                    bets[match] = integrationPrediction;
                }
                
                // First do a dry run to see what would be bet
                logger.LogInformation("");
                logger.LogInformation("=== DRY RUN ===");
                foreach (var bet in bets)
                {
                    logger.LogInformation("  Would bet {Prediction} for {Match}", bet.Value, bet.Key);
                }
                
                logger.LogInformation("");
                Console.Write("Do you want to place these bets for real? (y/N): ");
                var userInput = Console.ReadLine()?.Trim().ToLowerInvariant();
                
                if (userInput == "y" || userInput == "yes")
                {
                    logger.LogInformation("");
                    logger.LogInformation("=== PLACING REAL BETS ===");
                    var realBetSuccess = await kicktippClient.PlaceBetsAsync("ehonda-test", bets, overrideBets: true);
                    
                    if (realBetSuccess)
                    {
                        logger.LogInformation("✓ Bets placed successfully!");
                    }
                    else
                    {
                        logger.LogError("✗ Failed to place bets");
                    }
                }
                else
                {
                    logger.LogInformation("Bet placement cancelled by user");
                }
            }
            else
            {
                logger.LogInformation("No open predictions found for ehonda-test community");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }
    
    private static void ConfigureServices(IServiceCollection services, KicktippIntegration.KicktippCredentials credentials)
    {
        // Configure the Kicktipp options
        services.Configure<KicktippIntegration.KicktippOptions>(options =>
        {
            options.Username = credentials.Username;
            options.Password = credentials.Password;
        });
        
        // Add the Kicktipp client with authentication
        services.AddKicktippClient();
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
                logger.LogInformation("Expected location: KicktippAi.Secrets/src/Poc/.env (sibling to solution directory)");
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
                    var envPath = Path.Combine(secretsDir, "src", "Poc", ".env");
                    
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
        var fallbackPath = Path.Combine(currentWorkingDir, "..", "..", "..", "..", "KicktippAi.Secrets", "src", "Poc", ".env");
        
        if (File.Exists(fallbackPath))
        {
            return Path.GetFullPath(fallbackPath);
        }
        
        return null;
    }
    
    private static KicktippIntegration.KicktippCredentials LoadCredentials()
    {
        return new KicktippIntegration.KicktippCredentials(
            Environment.GetEnvironmentVariable("KICKTIPP_USERNAME") ?? string.Empty,
            Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD") ?? string.Empty
        );
    }
    
    // Helper method to convert Core.Match to POC Match for the predictor
    private static KicktippAi.Poc.Models.Match ConvertToPocMatch(Core.Match coreMatch)
    {
        return new KicktippAi.Poc.Models.Match
        {
            HomeTeam = coreMatch.HomeTeam,
            RoadTeam = coreMatch.AwayTeam,
            MatchDate = coreMatch.StartsAt.ToDateTimeOffset()
        };
    }
}
