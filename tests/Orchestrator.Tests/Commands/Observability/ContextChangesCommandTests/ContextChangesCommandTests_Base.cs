using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Observability.ContextChanges;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ContextChangesCommandTests;

/// <summary>
/// Context record containing all test infrastructure for ContextChangesCommand tests.
/// </summary>
public record ContextChangesCommandTestContext(
    CommandApp App,
    TestConsole Console,
    Mock<IContextRepository> ContextRepository,
    FakeLogger<ContextChangesCommand> Logger);

/// <summary>
/// Base class and factory methods for ContextChangesCommand tests.
/// </summary>
public abstract class ContextChangesCommandTests_Base
{
    /// <summary>
    /// Creates a configured command app for testing ContextChangesCommand.
    /// </summary>
    /// <param name="contextRepository">Mock context repository. Defaults to a new mock.</param>
    /// <param name="logger">Logger instance. Defaults to a FakeLogger.</param>
    protected static ContextChangesCommandTestContext CreateContextChangesCommandApp(
        Option<Mock<IContextRepository>> contextRepository = default,
        Option<FakeLogger<ContextChangesCommand>> logger = default)
    {
        var testConsole = new TestConsole();
        var mockContextRepo = contextRepository.Or(() => new Mock<IContextRepository>());
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var fakeLogger = logger.Or(() => new FakeLogger<ContextChangesCommand>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton<ILogger<ContextChangesCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<ContextChangesCommand>("context-changes");
        });

        return new ContextChangesCommandTestContext(app, testConsole, mockContextRepo, fakeLogger);
    }

    /// <summary>
    /// Runs the context-changes command with the given arguments and returns exit code and output.
    /// </summary>
    protected static async Task<(int ExitCode, string Output)> RunContextChangesAsync(
        ContextChangesCommandTestContext context,
        params string[] extraArgs)
    {
        var args = new List<string> { "context-changes" };
        args.AddRange(extraArgs);
        var exitCode = await context.App.RunAsync(args.ToArray());
        return (exitCode, context.Console.Output);
    }

    /// <summary>
    /// Sets up the mock context repository with the given document names and documents.
    /// Documents are keyed by (documentName, version) for per-version retrieval.
    /// </summary>
    protected static Mock<IContextRepository> CreateContextChangesRepository(
        List<string> documentNames,
        Dictionary<(string Name, int Version), ContextDocument>? documentsByVersion = null,
        Dictionary<string, ContextDocument?>? latestDocuments = null)
    {
        var mock = new Mock<IContextRepository>();

        mock.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentNames);

        if (latestDocuments != null)
        {
            mock.Setup(r => r.GetLatestContextDocumentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string docName, string _, CancellationToken _) =>
                    latestDocuments.TryGetValue(docName, out var doc) ? doc : null);
        }

        if (documentsByVersion != null)
        {
            mock.Setup(r => r.GetContextDocumentAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string docName, int version, string _, CancellationToken _) =>
                    documentsByVersion.TryGetValue((docName, version), out var doc) ? doc : null);
        }

        return mock;
    }
}
