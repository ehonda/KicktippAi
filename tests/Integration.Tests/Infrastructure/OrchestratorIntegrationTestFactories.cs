using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Operations.Verify;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Integration.Tests.Infrastructure;

public static class OrchestratorIntegrationTestFactories
{
    public static OrchestratorIntegrationTestContext CreateCommandApp(
        IFirebaseServiceFactory firebaseServiceFactory,
        Mock<IKicktippClientFactory> kicktippClientFactory,
        Mock<IOpenAiServiceFactory> openAiServiceFactory,
        Mock<IContextProviderFactory> contextProviderFactory,
        TestConsole? console = null)
    {
        var testConsole = console ?? new TestConsole();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(firebaseServiceFactory);
        services.AddSingleton(kicktippClientFactory.Object);
        services.AddSingleton(openAiServiceFactory.Object);
        services.AddSingleton(contextProviderFactory.Object);
        services.AddSingleton<ILogger<VerifyMatchdayCommand>>(new FakeLogger<VerifyMatchdayCommand>());
        services.AddSingleton<ILogger<MatchdayCommand>>(new FakeLogger<MatchdayCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<VerifyMatchdayCommand>("verify-matchday");
            config.AddCommand<MatchdayCommand>("matchday");
        });

        return new OrchestratorIntegrationTestContext(
            app,
            testConsole,
            kicktippClientFactory,
            openAiServiceFactory,
            contextProviderFactory);
    }

    public static async Task<(int ExitCode, string Output)> RunCommandAsync(
        CommandApp app,
        TestConsole console,
        params string[] args)
    {
        var exitCode = await app.RunAsync(args);
        return (exitCode, console.Output);
    }

    public sealed record OrchestratorIntegrationTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IOpenAiServiceFactory> OpenAiServiceFactory,
        Mock<IContextProviderFactory> ContextProviderFactory);
}
