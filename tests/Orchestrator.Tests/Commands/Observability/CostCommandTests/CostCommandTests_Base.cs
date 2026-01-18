using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Observability.Cost;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Tests.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Factory methods for creating CostCommand test infrastructure.
/// Separated from temp directory concerns so tests that don't need file system can use this directly.
/// </summary>
public static class CostCommandTestFactories
{
    /// <summary>
    /// Creates a configured command app for testing CostCommand.
    /// </summary>
    /// <param name="predictionRepository">Mock prediction repository. Defaults to a new mock configured for costs.</param>
    /// <param name="logger">Logger instance. Defaults to a FakeLogger.</param>
    /// <returns>A tuple containing the CommandApp, TestConsole, mock prediction repository, and logger.</returns>
    public static CostCommandTestContext CreateCostCommandApp(
        Option<Mock<IPredictionRepository>> predictionRepository = default,
        Option<FakeLogger<CostCommand>> logger = default)
    {
        var testConsole = new TestConsole();
        var mockPredictionRepo = predictionRepository.Or(() => CreateMockPredictionRepositoryForCosts());
        var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
        mockFirebaseFactory.Setup(f => f.CreatePredictionRepository()).Returns(mockPredictionRepo.Object);
        var fakeLogger = logger.Or(() => new FakeLogger<CostCommand>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton<ILogger<CostCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<CostCommand>("cost");
        });

        return new CostCommandTestContext(app, testConsole, mockFirebaseFactory, mockPredictionRepo, fakeLogger);
    }

    /// <summary>
    /// Context record containing all test infrastructure for CostCommand tests.
    /// </summary>
    public record CostCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IPredictionRepository> PredictionRepository,
        FakeLogger<CostCommand> Logger);
}

/// <summary>
/// Base class for CostCommand tests that need temporary file system access.
/// Provides factory methods plus temp directory handling.
/// </summary>
public abstract class CostCommandTests_Base : TempDirectoryTestBase
{
    protected override string TestDirectoryName => "CostCommandTests";

    /// <summary>
    /// Creates a configured command app for testing CostCommand.
    /// </summary>
    protected static CostCommandTestFactories.CostCommandTestContext CreateCostCommandApp(
        Option<Mock<IPredictionRepository>> predictionRepository = default,
        Option<FakeLogger<CostCommand>> logger = default)
        => CostCommandTestFactories.CreateCostCommandApp(predictionRepository, logger);

    /// <summary>
    /// Creates a JSON config file in the test directory.
    /// </summary>
    /// <param name="config">The configuration to serialize.</param>
    /// <returns>The path to the created config file.</returns>
    protected string CreateConfigFile(CostConfiguration config)
    {
        // Ensure directory exists (defensive - should be created by [Before(Test)] hook)
        Directory.CreateDirectory(TestDirectory);
        var path = Path.Combine(TestDirectory, "config.json");
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>
    /// Creates a raw JSON config file in the test directory.
    /// </summary>
    /// <param name="jsonContent">The raw JSON content.</param>
    /// <returns>The path to the created config file.</returns>
    protected string CreateRawConfigFile(string jsonContent)
    {
        // Ensure directory exists (defensive - should be created by [Before(Test)] hook)
        Directory.CreateDirectory(TestDirectory);
        var path = Path.Combine(TestDirectory, "config.json");
        File.WriteAllText(path, jsonContent);
        return path;
    }
}
