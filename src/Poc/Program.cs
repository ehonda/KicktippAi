using DotNetEnv;
using KicktippAi.Poc.Models;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace KicktippAi.Poc;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Kicktipp.de Automation POC");
        Console.WriteLine("==========================");
        
        try
        {
            // Load environment variables from .env file
            LoadEnvironmentVariables();
            
            // Get credentials from environment
            var credentials = LoadCredentials();
            if (!credentials.IsValid)
            {
                Console.WriteLine("Error: Please set KICKTIPP_USERNAME and KICKTIPP_PASSWORD in your .env file");
                Console.WriteLine("You can use .env.example as a template.");
                return;
            }

            // Setup dependency injection
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, credentials);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            
            // Get the client from DI - authentication is handled automatically
            var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
            
            Console.WriteLine($"Attempting to login with username: {credentials.Username}");
            
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
                
                // Generate random bets for these matches
                Console.WriteLine();
                Console.WriteLine("Generating random bets for open predictions...");
                
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
                Console.WriteLine();
                Console.WriteLine("=== DRY RUN ===");
                foreach (var bet in bets)
                {
                    Console.WriteLine($"  Would bet {bet.Value} for {bet.Key}");
                }
                
                Console.WriteLine();
                Console.Write("Do you want to place these bets for real? (y/N): ");
                var userInput = Console.ReadLine()?.Trim().ToLowerInvariant();
                
                if (userInput == "y" || userInput == "yes")
                {
                    Console.WriteLine();
                    Console.WriteLine("=== PLACING REAL BETS ===");
                    var realBetSuccess = await kicktippClient.PlaceBetsAsync("ehonda-test", bets, overrideBets: true);
                    
                    if (realBetSuccess)
                    {
                        Console.WriteLine("✓ Bets placed successfully!");
                    }
                    else
                    {
                        Console.WriteLine("✗ Failed to place bets");
                    }
                }
                else
                {
                    Console.WriteLine("Bet placement cancelled by user");
                }
            }
            else
            {
                Console.WriteLine("No open predictions found for ehonda-test community");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
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
    
    private static void LoadEnvironmentVariables()
    {
        try
        {
            // Try to load .env file from current directory
            var currentDir = Directory.GetCurrentDirectory();
            var envPath = Path.Combine(currentDir, ".env");
            
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                Console.WriteLine($"Loaded .env file from: {envPath}");
            }
            else
            {
                // Try from project directory
                var projectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (projectDir != null)
                {
                    envPath = Path.Combine(projectDir, ".env");
                    if (File.Exists(envPath))
                    {
                        Env.Load(envPath);
                        Console.WriteLine($"Loaded .env file from: {envPath}");
                    }
                    else
                    {
                        Console.WriteLine("Warning: No .env file found. Please create one based on .env.example");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
        }
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
