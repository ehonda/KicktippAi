using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Operations.Bonus;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Verify;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using Orchestrator.Commands.Observability.ContextChanges;
using Orchestrator.Commands.Observability.Cost;
using Orchestrator.Commands.Utility.UploadKpi;
using Orchestrator.Commands.Utility.UploadTransfers;
using Orchestrator.Commands.Utility.ListKpi;
using Orchestrator.Commands.Utility.Snapshots;
using Orchestrator.Infrastructure;

namespace Orchestrator;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Dependency Injection setup follows Spectre.Console.Cli patterns:
        // - Tutorial: https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/tutorials/dependency-injection-in-cli-apps.md
        // - Testing: https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/how-to/testing-command-line-applications.md
        var services = new ServiceCollection();
        
        // Register IAnsiConsole for dependency injection into commands
        services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        
        app.Configure(config =>
        {
            config.SetApplicationName("Orchestrator");
            config.SetApplicationVersion("1.0.0");
            
            config.AddCommand<MatchdayCommand>("matchday")
                .WithDescription("Generate predictions for the current matchday")
                .WithExample("matchday", "gpt-4o-2024-08-06", "--community", "ehonda-test-buli");

            config.AddBranch("analyze-match", analyzeMatch =>
            {
                analyzeMatch.SetDescription("Analyze prediction distributions for a single match without persisting results");

                analyzeMatch.AddCommand<AnalyzeMatchDetailedCommand>("detailed")
                    .WithDescription("Detailed analysis with justification output and live estimates")
                    .WithExample(
                        "analyze-match",
                        "detailed",
                        "gpt-5-nano",
                        "--community-context",
                        "ehonda-test-buli",
                        "--home",
                        "FC Bayern München",
                        "--away",
                        "RB Leipzig",
                        "--matchday",
                        "1",
                        "--runs",
                        "5");

                analyzeMatch.AddCommand<AnalyzeMatchComparisonCommand>("comparison")
                    .WithDescription("Compare predictions generated with and without justification")
                    .WithExample(
                        "analyze-match",
                        "comparison",
                        "gpt-5-nano",
                        "--community-context",
                        "ehonda-test-buli",
                        "--home",
                        "FC Bayern München",
                        "--away",
                        "RB Leipzig",
                        "--matchday",
                        "1",
                        "--runs",
                        "5");
            });
                
            config.AddCommand<BonusCommand>("bonus")
                .WithDescription("Generate bonus predictions")
                .WithExample("bonus", "gpt-4o-2024-08-06", "--community", "ehonda-test-buli");
                
            config.AddBranch<CollectContextSettings>("collect-context", collectContext =>
            {
                collectContext.SetDescription("Collect context documents and store them in database");
                collectContext.AddCommand<CollectContextKicktippCommand>("kicktipp")
                    .WithDescription("Collect context from Kicktipp")
                    .WithExample("collect-context", "kicktipp", "--community", "ehonda-test-buli", "--community-context", "ehonda-test-buli", "--dry-run");
            });
                
            config.AddCommand<VerifyMatchdayCommand>("verify")
                .WithDescription("Verify that database predictions have been successfully posted to Kicktipp")
                .WithExample("verify", "--community", "ehonda-test-buli");
                
            config.AddCommand<VerifyBonusCommand>("verify-bonus")
                .WithDescription("Verify that database bonus predictions are valid and complete")
                .WithExample("verify-bonus", "--community", "ehonda-test-buli");
                
            config.AddCommand<UploadKpiCommand>("upload-kpi")
                .WithDescription("Upload a KPI context document to Firebase")
                .WithExample("upload-kpi", "team-data", "--community", "ehonda-test-buli");
            
            config.AddCommand<UploadTransfersCommand>("upload-transfers")
                .WithDescription("Upload a transfers context document to Firebase (team transfers CSV)")
                .WithExample("upload-transfers", "fcb", "--community-context", "ehonda-test-buli");
                
            config.AddCommand<ListKpiCommand>("list-kpi")
                .WithDescription("List KPI context documents from Firebase")
                .WithExample("list-kpi", "--community", "ehonda-test-buli");
                
            config.AddCommand<ContextChangesCommand>("context-changes")
                .WithDescription("Show changes between latest and previous versions of context documents")
                .WithExample("context-changes", "--community-context", "ehonda-test-buli", "--count", "5")
                .WithExample("context-changes", "--community-context", "ehonda-test-buli", "--seed", "42");
                
            config.AddCommand<CostCommand>("cost")
                .WithDescription("Calculate aggregate costs for predictions")
                .WithExample("cost", "--all")
                .WithExample("cost", "--matchdays", "1,2,3")
                .WithExample("cost", "--models", "gpt-4o,o1-mini", "--bonus");
                
            config.AddBranch("snapshots", snapshots =>
            {
                snapshots.SetDescription("Generate and encrypt HTML snapshots from Kicktipp for test fixtures");
                
                snapshots.AddCommand<SnapshotsFetchCommand>("fetch")
                    .WithDescription("Fetch HTML snapshots from Kicktipp")
                    .WithExample("snapshots", "fetch", "--community", "ehonda-test-buli")
                    .WithExample("snapshots", "fetch", "--community", "ehonda-test-buli", "--output", "my-snapshots");
                    
                snapshots.AddCommand<SnapshotsEncryptCommand>("encrypt")
                    .WithDescription("Encrypt HTML snapshots for safe committing")
                    .WithExample("snapshots", "encrypt")
                    .WithExample("snapshots", "encrypt", "--input", "my-snapshots", "--output", "tests/KicktippIntegration.Tests/Fixtures/Html");
                    
                snapshots.AddCommand<SnapshotsAllCommand>("all")
                    .WithDescription("Fetch and encrypt snapshots in one step")
                    .WithExample("snapshots", "all", "--community", "ehonda-test-buli")
                    .WithExample("snapshots", "all", "--community", "ehonda-test-buli", "--keep-originals");
            });
        });

        return await app.RunAsync(args);
    }
}
