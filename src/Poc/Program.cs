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
