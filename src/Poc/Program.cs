using DotNetEnv;
using KicktippAi.Poc.Models;
using KicktippIntegration;
using OpenAiIntegration;
using Core;
using ContextProviders.Kicktipp;
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
        
        Console.WriteLine("Kicktipp.de Automation POC");
        Console.WriteLine("==========================");
        
        try
        {
            // Load environment variables from .env file
            LoadEnvironmentVariables(logger);
            
            // Get credentials from environment
            var credentials = LoadCredentials();
            if (!credentials.IsValid)
            {
                Console.WriteLine("❌ Please set KICKTIPP_USERNAME and KICKTIPP_PASSWORD in your .env file");
                Console.WriteLine("You can use .env.example as a template.");
                return;
            }

            // Check for OpenAI API key
            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                Console.WriteLine("❌ Please set OPENAI_API_KEY in your .env file");
                return;
            }

            // Setup dependency injection with credentials
            ConfigureServices(serviceCollection, credentials, openAiApiKey);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            
            // Get the client from DI - authentication is handled automatically
            var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
            var openAiPredictor = serviceProvider.GetRequiredService<IPredictor<PredictorContext>>();
            var predictorContext = serviceProvider.GetRequiredService<PredictorContext>();
            
            Console.WriteLine($"Attempting to login with username: {credentials.Username}");
            
            // Test fetching standings for ehonda-test-buli community
            Console.WriteLine("Fetching standings for ehonda-test-buli community...");
            var standings = await kicktippClient.GetStandingsAsync("ehonda-test-buli");
            
            if (standings.Any())
            {
                Console.WriteLine($"✓ Found {standings.Count} team standings:");
                Console.WriteLine();
                Console.WriteLine("=== CURRENT STANDINGS ===");
                foreach (var standing in standings)
                {
                    Console.WriteLine($"  {standing.Position,2}. {standing.TeamName,-25} {standing.Points,3} pts | {standing.GoalsFormatted,6} | {standing.GoalDifference,+3} | {standing.Wins,2}W {standing.Draws,2}D {standing.Losses,2}L");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("No standings found for ehonda-test-buli community");
            }
            
            // Test fetching matches with history for ehonda-test-buli community
            Console.WriteLine("Fetching matches with detailed history for ehonda-test-buli community...");
            var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync("ehonda-test-buli");
            
            if (matchesWithHistory.Any())
            {
                Console.WriteLine($"✓ Found {matchesWithHistory.Count} matches with history:");
                Console.WriteLine();
                Console.WriteLine("=== MATCHES WITH RECENT HISTORY ===");
                
                foreach (var matchWithHistory in matchesWithHistory.Take(2)) // Show first 2 matches for brevity
                {
                    var match = matchWithHistory.Match;
                    Console.WriteLine($"🥅 {match.HomeTeam} vs {match.AwayTeam} - {match.StartsAt:dd.MM.yy HH:mm}");
                    Console.WriteLine();
                    
                    // Show home team recent results
                    Console.WriteLine($"  📊 {match.HomeTeam} - Last {matchWithHistory.HomeTeamHistory.Count} results:");
                    foreach (var result in matchWithHistory.HomeTeamHistory.Take(5))
                    {
                        var outcomeIcon = result.Outcome switch
                        {
                            MatchOutcome.Win => "✅",
                            MatchOutcome.Draw => "➰",
                            MatchOutcome.Loss => "❌",
                            _ => "⏳"
                        };
                        var scoreText = result.HomeGoals.HasValue && result.AwayGoals.HasValue 
                            ? $"{result.HomeGoals}:{result.AwayGoals}" 
                            : "-:-";
                        Console.WriteLine($"    {outcomeIcon} {result.Competition}: {result.HomeTeam} vs {result.AwayTeam} ({scoreText})");
                    }
                    Console.WriteLine();
                    
                    // Show away team recent results
                    Console.WriteLine($"  📊 {match.AwayTeam} - Last {matchWithHistory.AwayTeamHistory.Count} results:");
                    foreach (var result in matchWithHistory.AwayTeamHistory.Take(5))
                    {
                        var outcomeIcon = result.Outcome switch
                        {
                            MatchOutcome.Win => "✅",
                            MatchOutcome.Draw => "➰",
                            MatchOutcome.Loss => "❌",
                            _ => "⏳"
                        };
                        var scoreText = result.HomeGoals.HasValue && result.AwayGoals.HasValue 
                            ? $"{result.HomeGoals}:{result.AwayGoals}" 
                            : "-:-";
                        Console.WriteLine($"    {outcomeIcon} {result.Competition}: {result.HomeTeam} vs {result.AwayTeam} ({scoreText})");
                    }
                    Console.WriteLine();
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No matches with history found for ehonda-test-buli community");
            }
            
            // Test fetching open predictions for ehonda-test community
            // Authentication happens automatically via the authentication handler
            Console.WriteLine("Fetching open predictions for ehonda-test community...");
            var openPredictions = await kicktippClient.GetOpenPredictionsAsync("ehonda-test");
                
            if (openPredictions.Any())
            {
                Console.WriteLine($"✓ Found {openPredictions.Count} open matches:");
                Console.WriteLine();
                foreach (var match in openPredictions)
                {
                    Console.WriteLine($"  {match}");
                }
                
                // Generate AI-powered bets for these matches
                Console.WriteLine();
                Console.WriteLine("Generating AI-powered predictions for open matches...");
                
                var bets = new Dictionary<Core.Match, KicktippIntegration.BetPrediction>();
                
                foreach (var match in openPredictions)
                {
                    // Use OpenAI predictor directly with Core.Match
                    var aiPrediction = await openAiPredictor.PredictAsync(match, predictorContext, CancellationToken.None);
                    var integrationPrediction = new KicktippIntegration.BetPrediction(
                        aiPrediction.HomeGoals, 
                        aiPrediction.AwayGoals);
                    bets[match] = integrationPrediction;
                }
                  // Show AI predictions
                Console.WriteLine();
                Console.WriteLine("=== AI-POWERED PREDICTIONS ===");
                foreach (var bet in bets)
                {
                    Console.WriteLine($"  AI predicts {bet.Value} for {bet.Key}");
                }

                // Automatically place bets
                Console.WriteLine();
                Console.WriteLine("=== PLACING BETS ===");
                var realBetSuccess = await kicktippClient.PlaceBetsAsync("ehonda-test", bets, overrideBets: true);
                
                if (realBetSuccess)
                {
                    Console.WriteLine("✓ Bets placed successfully!");
                }
                else
                {
                    Console.WriteLine("❌ Failed to place bets");
                }
            }
            else
            {
                Console.WriteLine("No open predictions found for ehonda-test community");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred: {ex.Message}");
            logger.LogError(ex, "An error occurred during execution");
        }
    }
    
    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }
      private static void ConfigureServices(IServiceCollection services, KicktippIntegration.KicktippCredentials credentials, string openAiApiKey)
    {
        // Configure the Kicktipp options
        services.Configure<KicktippIntegration.KicktippOptions>(options =>
        {
            options.Username = credentials.Username;
            options.Password = credentials.Password;
        });

        // Add the Kicktipp client with authentication
        services.AddKicktippClient();

        // Add OpenAI predictor
        services.AddOpenAiPredictor(openAiApiKey);
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
