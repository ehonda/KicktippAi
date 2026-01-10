using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.ListKpi;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Orchestrator.Tests.Infrastructure;

/// <summary>
/// Factory methods for creating test infrastructure for Orchestrator command tests.
/// Use these methods to create test instances with sensible defaults,
/// allowing tests to override only the dependencies relevant to their scenario.
/// </summary>
public static class OrchestratorTestFactories
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing commands.
    /// </summary>
    /// <typeparam name="TCommand">The command type to configure.</typeparam>
    /// <param name="commandName">The command name to register (e.g., "list-kpi").</param>
    /// <param name="console">Optional TestConsole instance. Defaults to a new TestConsole.</param>
    /// <param name="firebaseServiceFactory">Optional mock for IFirebaseServiceFactory. Defaults to a new mock.</param>
    /// <param name="configureServices">Optional action to configure additional services.</param>
    /// <returns>A tuple containing the CommandApp and the TestConsole for assertions.</returns>
    public static (CommandApp App, TestConsole Console) CreateCommandApp<TCommand>(
        string commandName,
        Option<TestConsole> console = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Action<IServiceCollection>> configureServices = default)
        where TCommand : class, ICommand
    {
        var testConsole = console.Or(() => new TestConsole());
        var mockFactory = firebaseServiceFactory.Or(() => new Mock<IFirebaseServiceFactory>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFactory.Object);
        services.AddSingleton<ILogger<TCommand>>(new FakeLogger<TCommand>());

        var configureAction = configureServices.Or(() => _ => { });
        configureAction(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<TCommand>(commandName);
        });

        return (app, testConsole);
    }

    /// <summary>
    /// Runs a command and returns the result including exit code and console output.
    /// </summary>
    /// <param name="app">The CommandApp to run.</param>
    /// <param name="console">The TestConsole to capture output from.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A tuple containing the exit code and the console output.</returns>
    public static async Task<(int ExitCode, string Output)> RunCommandAsync(
        CommandApp app,
        TestConsole console,
        params string[] args)
    {
        var exitCode = await app.RunAsync(args);
        var output = console.Output;
        return (exitCode, output);
    }

    /// <summary>
    /// Creates a mock <see cref="IKpiRepository"/> configured to return the specified documents.
    /// </summary>
    /// <param name="documents">The documents to return from GetAllKpiDocumentsAsync. Defaults to empty list.</param>
    /// <param name="communityContext">The community context to match. If not provided, matches any.</param>
    public static Mock<IKpiRepository> CreateMockKpiRepository(
        Option<IReadOnlyList<KpiDocument>> documents = default,
        Option<string> communityContext = default)
    {
        var mock = new Mock<IKpiRepository>();
        var docs = documents.Or(() => new List<KpiDocument>());

        // Use deconstruction to check if communityContext was explicitly provided
        var (hasContext, contextValue) = communityContext;

        if (hasContext)
        {
            mock.Setup(r => r.GetAllKpiDocumentsAsync(contextValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(docs);
        }
        else
        {
            mock.Setup(r => r.GetAllKpiDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(docs);
        }

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IFirebaseServiceFactory"/> that returns the specified KPI repository.
    /// </summary>
    /// <param name="kpiRepository">The KPI repository to return. Defaults to a new mock.</param>
    public static Mock<IFirebaseServiceFactory> CreateMockFirebaseServiceFactory(
        Option<Mock<IKpiRepository>> kpiRepository = default)
    {
        var mockFactory = new Mock<IFirebaseServiceFactory>();
        var mockRepo = kpiRepository.Or(() => new Mock<IKpiRepository>());

        mockFactory.Setup(f => f.CreateKpiRepository()).Returns(mockRepo.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a test <see cref="KpiDocument"/> with default values.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-document".</param>
    /// <param name="content">Document content. Defaults to "test content".</param>
    /// <param name="description">Document description. Defaults to "test description".</param>
    /// <param name="version">Document version. Defaults to 1.</param>
    /// <param name="createdAt">Creation timestamp. Defaults to 2025-01-10 12:00 UTC.</param>
    public static KpiDocument CreateKpiDocument(
        Option<string> documentName = default,
        Option<string> content = default,
        Option<string> description = default,
        Option<int> version = default,
        Option<DateTimeOffset> createdAt = default)
    {
        return new KpiDocument(
            documentName.Or("test-document"),
            content.Or("test content"),
            description.Or("test description"),
            version.Or(1),
            createdAt.Or(() => new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero)));
    }
}
