using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunSliceCommand : AsyncCommand<RunSliceSettings>
{
    private readonly IAnsiConsole _console;
    private readonly PreparedExperimentRunExecutor _executor;
    private readonly ILogger<RunSliceCommand> _logger;

    public RunSliceCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient,
        ILogger<RunSliceCommand> logger)
    {
        _console = console;
        _executor = new PreparedExperimentRunExecutor(firebaseServiceFactory, openAiServiceFactory, langfuseClient);
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunSliceSettings settings)
    {
        try
        {
            var summary = await _executor.ExecuteAsync(
                "slice",
                new PreparedExperimentRunRequest(
                    settings.ManifestPath,
                    settings.RunName,
                    settings.RunDescription,
                    settings.RunMetadataFile,
                    settings.ReplaceRun,
                    settings.ToRunOptions()),
                CancellationToken.None);

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing run-slice command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
