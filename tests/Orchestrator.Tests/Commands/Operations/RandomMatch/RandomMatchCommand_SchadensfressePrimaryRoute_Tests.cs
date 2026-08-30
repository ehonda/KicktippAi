using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.RandomMatch;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.RandomMatch;

public class RandomMatchCommand_SchadensfressePrimaryRoute_Tests
{
    [Test]
    public async Task Schadensfresse_fails_closed_before_any_configuration_or_service_activity()
    {
        var console = new TestConsole();
        var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        var kicktippFactory = new Mock<IKicktippClientFactory>(MockBehavior.Strict);
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
        var contextProviderFactory = new Mock<IContextProviderFactory>(MockBehavior.Strict);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(firebaseFactory.Object);
        services.AddSingleton(kicktippFactory.Object);
        services.AddSingleton(openAiFactory.Object);
        services.AddSingleton(contextProviderFactory.Object);
        services.AddSingleton(Mock.Of<ILangfusePublicApiClient>());
        services.AddSingleton<ILogger<RandomMatchCommand>>(new FakeLogger<RandomMatchCommand>());

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config =>
        {
            config.Settings.Console = console;
            config.AddCommand<RandomMatchCommand>("random-match");
        });

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "random-match",
            "gpt-5.6-sol",
            "--community",
            "schadensfresse",
            "--community-context",
            "pes-squad",
            "--competition",
            "fifa-world-cup-2026",
            "--prompt-source",
            "langfuse");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("schadensfresse predictions are disabled until the typed target-owned");
        await Assert.That(output).Contains("primary command route is available.");
        await Assert.That(output).DoesNotContain("Random match command initialized");

        firebaseFactory.VerifyNoOtherCalls();
        kicktippFactory.VerifyNoOtherCalls();
        openAiFactory.VerifyNoOtherCalls();
        contextProviderFactory.VerifyNoOtherCalls();
    }
}
