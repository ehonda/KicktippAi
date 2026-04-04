using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunRepeatedMatchCommand : AsyncCommand<RunRepeatedMatchSettings>
{
    private readonly IAnsiConsole _console;
    private readonly PreparedExperimentRunExecutor _executor;
    private readonly ILogger<RunRepeatedMatchCommand> _logger;

    public RunRepeatedMatchCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient,
        ILogger<RunRepeatedMatchCommand> logger)
    {
        _console = console;
        _executor = new PreparedExperimentRunExecutor(firebaseServiceFactory, openAiServiceFactory, langfuseClient);
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunRepeatedMatchSettings settings)
    {
        try
        {
            var summary = await _executor.ExecuteAsync(
                "repeated-match",
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
            _logger.LogError(ex, "Error executing run-repeated-match command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
