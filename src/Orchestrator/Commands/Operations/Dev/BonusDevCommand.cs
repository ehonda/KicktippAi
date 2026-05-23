using ContextProviders.Kicktipp;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Bonus;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class BonusDevCommand : AsyncCommand<DevParticipationSettings>
{
    private readonly BonusCommand _bonusCommand;
    private readonly IAnsiConsole _console;

    public BonusDevCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ILogger<BonusCommand> logger,
        ILangfusePublicApiClient? langfuseClient = null)
    {
        _console = console;
        _bonusCommand = new BonusCommand(
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
        DevParticipationSettings settings,
        CancellationToken cancellationToken)
    {
        if (!DevParticipationCommandSupport.TryCreateBaseSettings(
                settings,
                _console,
                "bonus-dev",
                showContextDocuments: false,
                out var baseSettings))
        {
            return 1;
        }

        return await _bonusCommand.ExecuteWithSettingsAsync(baseSettings, cancellationToken);
    }
}
