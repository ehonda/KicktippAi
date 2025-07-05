using DotNetEnv;
using KicktippAi.Poc.Models;
using KicktippAi.Poc.Services;

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
            
            Console.WriteLine($"Attempting to login with username: {credentials.Username}");
            
            // Create service and attempt login
            using var kicktippService = new KicktippService();
            var loginSuccess = await kicktippService.LoginAsync(credentials);
            
            if (loginSuccess)
            {
                Console.WriteLine("✓ Login successful!");
                
                // Extract and display login token for future use
                var loginToken = await kicktippService.GetLoginTokenAsync();
                if (!string.IsNullOrEmpty(loginToken))
                {
                    Console.WriteLine("✓ Login token extracted and saved to .env file");
                }
                else
                {
                    Console.WriteLine("Warning: Could not extract login token");
                }
                
                // Test fetching open predictions for ehonda-test community
                Console.WriteLine();
                Console.WriteLine("Fetching open predictions for ehonda-test community...");
                var openPredictions = await kicktippService.GetOpenPredictionsAsync("ehonda-test");
                
                if (openPredictions.Any())
                {
                    Console.WriteLine($"✓ Found {openPredictions.Count} open matches:");
                    Console.WriteLine();
                    foreach (var match in openPredictions)
                    {
                        Console.WriteLine($"  {match}");
                    }
                    
                    // Place random bets for these matches
                    Console.WriteLine();
                    Console.WriteLine("Placing random bets for open predictions...");
                    
                    // First do a dry run to see what would be bet
                    Console.WriteLine();
                    Console.WriteLine("=== DRY RUN ===");
                    var dryRunSuccess = await kicktippService.PlaceRandomBetsAsync("ehonda-test", dryRun: true, overrideBets: false);
                    
                    if (dryRunSuccess)
                    {
                        Console.WriteLine();
                        Console.Write("Do you want to place these bets for real? (y/N): ");
                        var userInput = Console.ReadLine()?.Trim().ToLowerInvariant();
                        
                        if (userInput == "y" || userInput == "yes")
                        {
                            Console.WriteLine();
                            Console.WriteLine("=== PLACING REAL BETS ===");
                            var realBetSuccess = await kicktippService.PlaceRandomBetsAsync("ehonda-test", dryRun: false, overrideBets: false);
                            
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
                        Console.WriteLine("✗ Dry run failed, not proceeding with real bets");
                    }
                }
                else
                {
                    Console.WriteLine("No open predictions found for ehonda-test community");
                }
            }
            else
            {
                Console.WriteLine("✗ Login failed!");
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
    
    private static KicktippCredentials LoadCredentials()
    {
        return new KicktippCredentials
        {
            Username = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD") ?? string.Empty
        };
    }
}
