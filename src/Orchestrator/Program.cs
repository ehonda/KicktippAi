using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Orchestrator.Commands;

namespace Orchestrator;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        
        app.Configure(config =>
        {
            config.SetApplicationName("Orchestrator");
            config.SetApplicationVersion("1.0.0");
            
            config.AddCommand<MatchdayCommand>("matchday")
                .WithDescription("Generate predictions for the current matchday")
                .WithExample("matchday", "gpt-4o-2024-08-06");
                
            config.AddCommand<BonusCommand>("bonus")
                .WithDescription("Generate bonus predictions")
                .WithExample("bonus", "gpt-4o-2024-08-06");
        });

        return await app.RunAsync(args);
    }
}
