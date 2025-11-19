using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using OpenAiIntegration;
using NodaTime;

namespace PromptSampleTests.Commands;

public class ServiceSettings : BaseSettings
{
    [CommandOption("--home")]
    [Description("Home team name (default: VfB Stuttgart)")]
    [DefaultValue("VfB Stuttgart")]
    public string HomeTeam { get; set; } = "VfB Stuttgart";

    [CommandOption("--away")]
    [Description("Away team name (default: RB Leipzig)")]
    [DefaultValue("RB Leipzig")]
    public string AwayTeam { get; set; } = "RB Leipzig";

    [CommandOption("--match")]
    [Description("Match number to select from available matches")]
    public int? MatchNumber { get; set; }
}

[Description("Test the PredictionService with sample data")]
public class ServiceCommand : AsyncCommand<ServiceSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ServiceSettings settings)
    {
        Console.WriteLine("=== PredictionService Test ===");
        Console.WriteLine();

        try
        {
            // Create a temporary logger for loading environment
            var tempLogger = LoggingConfiguration.CreateLogger<ServiceCommand>();
            
            // Load environment
            EnvironmentHelper.LoadEnvironmentVariables(tempLogger);

            // Get API key
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ OPENAI_API_KEY environment variable is not set");
                return 1;
            }

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder => 
            {
                builder.AddConsole();
                if (settings.Verbose)
                {
                    // In verbose mode, show HTTP requests to understand what's happening
                    builder.SetMinimumLevel(LogLevel.Information);
                }
                else
                {
                    // In normal mode, only show warnings and costs
                    builder.SetMinimumLevel(LogLevel.Information);
                    // Filter out HTTP noise except for our own services
                    builder.AddFilter("System.Net.Http", LogLevel.Warning);
                    builder.AddFilter("KicktippIntegration", LogLevel.Warning);
                }
            });

            // Add OpenAI services
            services.AddOpenAiPredictor(apiKey, settings.Model);

            // Add Kicktipp services if credentials are available
            var kicktippUsername = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME");
            var kicktippPassword = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD");
            var hasKicktippCredentials = !string.IsNullOrEmpty(kicktippUsername) && !string.IsNullOrEmpty(kicktippPassword);

            List<DocumentContext> contextDocuments = [];

            if (hasKicktippCredentials)
            {
                Console.WriteLine("✅ Kicktipp credentials found - will use live context");
                
                services.Configure<KicktippOptions>(options =>
                {
                    options.Username = kicktippUsername!;
                    options.Password = kicktippPassword!;
                });
                services.AddKicktippClient();
                services.AddSingleton(provider =>
                    new KicktippContextProvider(
                        provider.GetRequiredService<IKicktippClient>(),
                        "ehonda-test-buli"));
            }
            else
            {
                Console.WriteLine("⚠️  No Kicktipp credentials - using sample context");
            }

            var serviceProvider = services.BuildServiceProvider();
            var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
            var logger = serviceProvider.GetRequiredService<ILogger<ServiceCommand>>();

            // Create match
            var utcZone = DateTimeZone.Utc;
            var match = new Match(
                settings.HomeTeam,
                settings.AwayTeam,
                utcZone.AtLeniently(LocalDateTime.FromDateTime(DateTime.UtcNow.AddDays(1))),
                1
            );

            Console.WriteLine($"Match: {match.HomeTeam} vs {match.AwayTeam}");
            Console.WriteLine($"Model: {settings.Model}");
            Console.WriteLine();

            // Get context documents if Kicktipp is available
            if (hasKicktippCredentials)
            {
                var contextProvider = serviceProvider.GetRequiredService<KicktippContextProvider>();
                await foreach (var doc in contextProvider.GetMatchContextAsync(match.HomeTeam, match.AwayTeam))
                {
                    contextDocuments.Add(doc);
                }
                Console.WriteLine($"Context documents: {contextDocuments.Count}");
            }
            else
            {
                // Add some sample context for testing
                contextDocuments.Add(new DocumentContext(
                    "Sample Team Stats",
                    $"""
                    {settings.HomeTeam} recent form: WWLWD
                    {settings.AwayTeam} recent form: WLWWL
                    
                    Head-to-head last 5 matches:
                    - {settings.HomeTeam} 2-1 {settings.AwayTeam}
                    - {settings.AwayTeam} 0-3 {settings.HomeTeam}
                    - {settings.HomeTeam} 1-1 {settings.AwayTeam}
                    - {settings.AwayTeam} 2-0 {settings.HomeTeam}
                    - {settings.HomeTeam} 3-2 {settings.AwayTeam}
                    """
                ));
                Console.WriteLine($"Sample context documents: {contextDocuments.Count}");
            }

            Console.WriteLine();
            Console.WriteLine("Calling PredictionService...");
            
            // Show context documents in verbose mode
            if (settings.Verbose && contextDocuments.Any())
            {
                Console.WriteLine();
                Console.WriteLine("=== CONTEXT DOCUMENTS ===");
                foreach (var doc in contextDocuments)
                {
                    Console.WriteLine($"Document: {doc.Name}");
                    Console.WriteLine($"Content: {doc.Content}");
                    Console.WriteLine();
                }
            }
            
            Console.WriteLine();

            // Generate prediction
            var prediction = await predictionService.PredictMatchAsync(match, contextDocuments);

            Console.WriteLine("=== PREDICTION RESULT ===");
            if (prediction != null)
            {
                Console.WriteLine($"Final Score: {prediction.HomeGoals}:{prediction.AwayGoals}");
                Console.WriteLine($"Winner: {GetWinner(prediction, match)}");
            }
            else
            {
                Console.WriteLine("❌ Prediction failed - no result returned");
            }
            Console.WriteLine();

            serviceProvider.Dispose();
            
            if (prediction != null)
            {
                Console.WriteLine("✅ Test completed successfully");
                return 0;
            }
            else
            {
                Console.WriteLine("❌ Test failed - prediction was null");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (settings.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    private static string GetWinner(Prediction prediction, Match match)
    {
        if (prediction.HomeGoals > prediction.AwayGoals)
            return match.HomeTeam;
        if (prediction.AwayGoals > prediction.HomeGoals)
            return match.AwayTeam;
        return "Draw";
    }
}
