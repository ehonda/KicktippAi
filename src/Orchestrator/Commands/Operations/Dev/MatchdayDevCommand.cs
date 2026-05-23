using ContextProviders.Kicktipp;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class MatchdayDevCommand : AsyncCommand<MatchdayDevSettings>
{
    private readonly IAnsiConsole _console;
    private readonly MatchdayCommand _matchdayCommand;

    public MatchdayDevCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ILogger<MatchdayCommand> logger,
        ILangfusePublicApiClient? langfuseClient = null)
    {
        _console = console;
        _matchdayCommand = new MatchdayCommand(
            console,
            firebaseServiceFactory,
            kicktippClientFactory,
            openAiServiceFactory,
            contextProviderFactory,
            logger,
            langfuseClient);
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        MatchdayDevSettings settings,
        CancellationToken cancellationToken)
    {
        if (!DevParticipationCommandSupport.TryCreateBaseSettings(
                settings,
                _console,
                "matchday-dev",
                settings.ShowContextDocuments,
                out var baseSettings))
        {
            return 1;
        }

        return await _matchdayCommand.ExecuteWithSettingsAsync(baseSettings, cancellationToken);
    }
}
