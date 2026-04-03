using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Operations.RandomMatch;
using Orchestrator.Commands.Operations.Bonus;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Verify;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using Orchestrator.Commands.Observability.ContextChanges;
using Orchestrator.Commands.Observability.Cost;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Commands.Observability.ExportExperimentItem;
using Orchestrator.Commands.Observability.PrepareTask5SingleMatch;
using Orchestrator.Commands.Observability.PrepareTask5Slice;
using Orchestrator.Commands.Observability.ReconstructPrompt;
using Orchestrator.Commands.Observability.RunTask5Slice;
using Orchestrator.Commands.Observability.SyncDataset;
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
        // Load environment variables once at startup
        var startupLogger = LoggingConfiguration.CreateLogger<Program>();
        EnvironmentHelper.LoadEnvironmentVariables(startupLogger);
        
        // Dependency Injection setup follows Spectre.Console.Cli patterns:
        // - Tutorial: https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/tutorials/dependency-injection-in-cli-apps.md
        // - Testing: https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/how-to/testing-command-line-applications.md
        var services = new ServiceCollection();
        
        // Register IAnsiConsole for dependency injection into commands
        services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        
        // Register all command services (factories and shared infrastructure)
        services.AddAllCommandServices();
        
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        
        app.Configure(config =>
        {
            config.SetApplicationName("Orchestrator");
            config.SetApplicationVersion("1.0.0");
            
            config.AddCommand<MatchdayCommand>("matchday")
                .WithDescription("Generate predictions for the current matchday")
                .WithExample("matchday", "gpt-4o-2024-08-06", "--community", "ehonda-test-buli");

            config.AddCommand<RandomMatchCommand>("random-match")
                .WithDescription("Generate a prediction for a random match from the current matchday (useful for testing)")
                .WithExample("random-match", "gpt-5-nano", "--community", "ehonda-test-buli");

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

            config.AddCommand<ReconstructPromptCommand>("reconstruct-prompt")
                .WithDescription("Reconstruct the historical prompt inputs for a stored match prediction")
                .WithExample("reconstruct-prompt", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26")
                .WithExample("reconstruct-prompt", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--with-justification")
                .WithExample("reconstruct-prompt", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--evaluation-time", "\"2026-03-15T12:00:00 Europe/Berlin (+01)\"");

            config.AddCommand<ExportExperimentItemCommand>("export-experiment-item")
                .WithDescription("Export a single historical match experiment item for runner testing")
                .WithExample("export-experiment-item", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26")
                .WithExample("export-experiment-item", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--output", "artifacts/langfuse-experiments/items/vfb-stuttgart-vs-rb-leipzig.json")
                .WithExample("export-experiment-item", "o4-mini", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--evaluation-time", "\"2026-03-15T12:00:00 Europe/Berlin (+01)\"");

            config.AddCommand<ExportExperimentDatasetCommand>("export-experiment-dataset")
                .WithDescription("Export the canonical hosted Langfuse dataset artifact for completed historical matches")
                .WithExample("export-experiment-dataset", "--community-context", "pes-squad")
                .WithExample("export-experiment-dataset", "--community-context", "pes-squad", "--matchdays", "1,2,3", "--output", "artifacts/langfuse-dataset/pes-squad-sample.json");

            config.AddCommand<PrepareTask5SliceCommand>("prepare-task5-slice")
                .WithDescription("Create a reusable Task 5 sampled slice artifact and manifest from a canonical dataset export")
                .WithExample("prepare-task5-slice", "--input", "artifacts/langfuse-dataset/pes-squad.json", "--sample-size", "16", "--sample-seed", "20260403")
                .WithExample("prepare-task5-slice", "--input", "artifacts/langfuse-dataset/pes-squad.json", "--sample-size", "10", "--source-pool-key", "matchdays-26", "--slice-key", "random-10-seed-20251011");

            config.AddCommand<PrepareTask5SingleMatchCommand>("prepare-task5-single-match")
                .WithDescription("Create a repeated single-match dataset and manifest that can be executed via run-task5-slice")
                .WithExample("prepare-task5-single-match", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--sample-size", "16")
                .WithExample("prepare-task5-single-match", "--community-context", "pes-squad", "--home", "VfB Stuttgart", "--away", "RB Leipzig", "--matchday", "26", "--sample-size", "8", "--slice-key", "repeat-8");

            config.AddCommand<SyncDatasetCommand>("sync-dataset")
                .WithDescription("Sync an exported hosted experiment dataset artifact to Langfuse via the public API")
                .WithExample("sync-dataset", "--input", "artifacts/langfuse-dataset/pes-squad.json")
                .WithExample("sync-dataset", "--input", "artifacts/langfuse-experiments/slices/pes-squad/all-matchdays/random-16-seed-20260403/slice-dataset.json", "--dataset-name", "match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260403");

            config.AddCommand<RunTask5SliceCommand>("run-task5-slice")
                .WithDescription("Run a Task 5 prepared dataset directly via IPredictionService and the Langfuse public API")
                .WithExample("run-task5-slice", "gpt-5-nano", "--manifest", "artifacts/langfuse-experiments/slices/pes-squad/all-matchdays/random-16-seed-20260403/slice-manifest.json", "--run-name", "task-5__pes-squad__gpt-5-nano__prompt-v1__random-16-seed-20260403__startsat-12h__2026-04-03t12-00-00z", "--prompt-key", "prompt-v1", "--evaluation-policy-kind", "relative", "--evaluation-policy-offset", "-12:00:00", "--batch-size", "8")
                .WithExample("run-task5-slice", "o3", "--manifest", "artifacts/langfuse-experiments/single-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-16/slice-manifest.json", "--run-name", "task-5__pes-squad__o3__prompt-v1__repeat-16__exact-time__2026-03-15t12-00-00z", "--prompt-key", "prompt-v1", "--evaluation-time", "\"2026-03-15T12:00:00 Europe/Berlin (+01)\"", "--batch-size", "8", "--replace-run");
                
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
