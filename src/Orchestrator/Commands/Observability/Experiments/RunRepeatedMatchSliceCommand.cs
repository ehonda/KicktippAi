using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunRepeatedMatchSliceCommand : AsyncCommand<RunRepeatedMatchSliceSettings>
{
    private readonly IAnsiConsole _console;
    private readonly PreparedExperimentRunExecutor _executor;
    private readonly ILogger<RunRepeatedMatchSliceCommand> _logger;

    public RunRepeatedMatchSliceCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient,
        ILogger<RunRepeatedMatchSliceCommand> logger)
    {
        _console = console;
        _executor = new PreparedExperimentRunExecutor(firebaseServiceFactory, openAiServiceFactory, langfuseClient);
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        RunRepeatedMatchSliceSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _executor.ExecuteAsync(
                "repeated-match-slice",
                new PreparedExperimentRunRequest(
                    settings.ManifestPath,
                    settings.RunName,
                    settings.RunDescription,
                    settings.RunMetadataFile,
                    settings.ReplaceRun,
                    settings.ToRunOptions()),
                cancellationToken);

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing run-repeated-match-slice command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
