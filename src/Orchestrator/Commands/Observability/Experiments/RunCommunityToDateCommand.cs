using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunCommunityToDateCommand : AsyncCommand<RunCommunityToDateSettings>
{
    private readonly IAnsiConsole _console;
    private readonly PreparedExperimentRunExecutor _executor;
    private readonly ILogger<RunCommunityToDateCommand> _logger;

    public RunCommunityToDateCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient,
        ILogger<RunCommunityToDateCommand> logger)
    {
        _console = console;
        _executor = new PreparedExperimentRunExecutor(firebaseServiceFactory, openAiServiceFactory, langfuseClient);
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunCommunityToDateSettings settings)
    {
        try
        {
            var summary = await _executor.ExecuteCommunityToDateAsync(
                new PreparedExperimentCommunityRunRequest(
                    settings.ManifestPath,
                    settings.RunFamilyName,
                    settings.RunDescription,
                    settings.DatasetName,
                    settings.ReplaceRuns,
                    settings.BatchSize,
                    settings.ParticipantLimit,
                    settings.GetParticipantIdFilter()),
                CancellationToken.None);

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing run-community-to-date command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
