using System.Diagnostics;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Commands.Operations.RandomMatch;
using Orchestrator.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.RandomMatch;

/// <summary>
/// Tests for <see cref="RandomMatchCommand"/> telemetry
/// (activity naming and Langfuse environment tagging).
/// </summary>
public class RandomMatchCommand_Telemetry_Tests
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the random-match command
    /// with minimal mock wiring sufficient for telemetry tests.
    /// </summary>
    private static (CommandApp App, TestConsole Console) CreateRandomMatchCommandApp()
    {
        var testConsole = new TestConsole();

        var matchesWithHistory = new List<MatchWithHistory> { CreateMatchWithHistory() };
        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: matchesWithHistory);
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var contextDocuments = CreateMatchContextDocuments();
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocuments);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: mockContextRepository);

        var mockPredictionService = CreateMockPredictionService(
            predictMatchResult: CreatePrediction());
        var mockTokenUsageTracker = CreateMockTokenUsageTracker();
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(
            predictionService: mockPredictionService,
            tokenUsageTracker: mockTokenUsageTracker);

        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockOpenAiFactory.Object);
        services.AddSingleton(mockContextProviderFactory.Object);
        services.AddSingleton<ILogger<RandomMatchCommand>>(new FakeLogger<RandomMatchCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<RandomMatchCommand>("random-match");
        });

        return (app, testConsole);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Root_activity_is_named_random_match()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var (app, console) = CreateRandomMatchCommandApp();

        await RunCommandAsync(app, console, "random-match", "gpt-4o", "-c", "test-community");

        var rootActivity = capturedActivities.FirstOrDefault(a => a.Parent == null);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.OperationName).IsEqualTo("random-match");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Environment_is_always_development_regardless_of_community()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var (app, console) = CreateRandomMatchCommandApp();

        // Use a production community name — RandomMatch should still be "development"
        await RunCommandAsync(app, console, "random-match", "gpt-4o", "-c", "pes-squad");

        var rootActivity = capturedActivities.FirstOrDefault(a => a.Parent == null);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }
}
