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
                .WithExample("matchday", "gpt-4o-2024-08-06", "--community", "ehonda-test-buli");
                
            config.AddCommand<BonusCommand>("bonus")
                .WithDescription("Generate bonus predictions")
                .WithExample("bonus", "gpt-4o-2024-08-06", "--community", "ehonda-test-buli");
                
            config.AddCommand<CollectContextCommand>("collect-context")
                .WithDescription("Collect context documents and store them in database")
                .WithExample("collect-context", "kicktipp", "--community", "ehonda-test-buli");
                
            config.AddCommand<VerifyMatchdayCommand>("verify")
                .WithDescription("Verify that database predictions have been successfully posted to Kicktipp")
                .WithExample("verify", "--community", "ehonda-test-buli");
                
            config.AddCommand<VerifyBonusCommand>("verify-bonus")
                .WithDescription("Verify that database bonus predictions are valid and complete")
                .WithExample("verify-bonus", "--community", "ehonda-test-buli");
                
            config.AddCommand<UploadKpiCommand>("upload-kpi")
                .WithDescription("Upload a KPI context document to Firebase")
                .WithExample("upload-kpi", "team-data", "--community", "ehonda-test-buli");
                
            config.AddCommand<ListKpiCommand>("list-kpi")
                .WithDescription("List KPI context documents from Firebase")
                .WithExample("list-kpi", "--community", "ehonda-test-buli");
                
            config.AddCommand<CostCommand>("cost")
                .WithDescription("Calculate aggregate costs for predictions")
                .WithExample("cost", "--all")
                .WithExample("cost", "--matchdays", "1,2,3")
                .WithExample("cost", "--models", "gpt-4o,o1-mini", "--bonus");
        });

        return await app.RunAsync(args);
    }
}
