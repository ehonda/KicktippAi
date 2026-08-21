using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.UploadContext;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

public class UploadContextCommandTests
{
    private const string FictionalLineupContent =
        "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\n" +
        "Exampleland,2026-05-25,Player,Alex Example,24,Forward,1000000\n" +
        "Exampleland,2026-05-25,Coach,Casey Sample,51,Coach,";

    private static (CommandApp App, TestConsole Console, Mock<IContextRepository> ContextRepository) CreateUploadContextCommandApp(
        Mock<IContextRepository>? contextRepository = null)
    {
        var testConsole = new TestConsole();
        var mockContextRepository = contextRepository ?? CreateMockContextRepositoryForUpload(savedVersion: 0);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton<IFirebaseServiceFactory>(mockFirebaseFactory.Object);
        services.AddSingleton<ILogger<UploadContextCommand>>(new FakeLogger<UploadContextCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<UploadContextCommand>("upload-context");
        });

        return (app, testConsole, mockContextRepository);
    }

    [Test]
    public async Task Uploading_context_json_saves_context_document()
    {
        var inputPath = WriteContextJson(
            "lineup-exampleland.csv",
            FictionalLineupContent,
            "test-community");
        var (app, console, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input",
            inputPath,
            "--competition",
            CompetitionIds.FifaWorldCup2026
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Upload context command initialized");
        await Assert.That(console.Output).Contains("lineup-exampleland.csv");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            "lineup-exampleland.csv",
            FictionalLineupContent,
            "test-community",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Dry_run_validates_input_without_writing_context_document()
    {
        var inputPath = WriteContextJson(
            "lineup-exampleland.csv",
            FictionalLineupContent,
            "test-community");
        var (app, console, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input",
            inputPath,
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--dry-run"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Dry run mode enabled");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Missing_input_file_returns_error_without_writing_context_document()
    {
        var (app, console, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input",
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"),
            "--competition",
            CompetitionIds.FifaWorldCup2026
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Context document JSON not found");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Arguments("team-data")]
    [Arguments("recent-history-fcb.csv")]
    [Arguments("lineup-germany.csv")]
    [Arguments("team-squad-summary")]
    [Arguments("club-elo-rankings")]
    [Test]
    public async Task Live_bundesliga_profile_owned_or_retired_names_fail_before_writes(string documentName)
    {
        var inputPath = WriteContextJson(documentName, "stale content", "ehonda-dev-buli-2627");
        var (app, console, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input",
            inputPath,
            "--competition",
            CompetitionIds.Bundesliga2026_27,
            "--dry-run"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Generic mutation");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Historical_bundesliga_partition_preserves_generic_uploads()
    {
        var inputPath = WriteContextJson("team-data", "historical content", "historical-community");
        var (app, _, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input",
            inputPath,
            "--competition",
            CompetitionIds.Bundesliga2025_26
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            "team-data",
            "historical content",
            "historical-community",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Known_target_community_conflict_fails_before_repository_access_even_in_dry_run()
    {
        var inputPath = WriteContextJson("operator-notes.md", "content", "ehonda-dev-wm26");
        var (app, console, contextRepository) = CreateUploadContextCommandApp();

        var exitCode = await app.RunAsync([
            "upload-context",
            "--input", inputPath,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--dry-run"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("belongs to");
        contextRepository.Verify(repository => repository.GetLatestContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string WriteContextJson(string documentName, string content, string communityContext)
    {
        var path = Path.Combine(Path.GetTempPath(), $"upload-context-{Guid.NewGuid():N}.json");
        var json = JsonSerializer.Serialize(new
        {
            documentName,
            content,
            description = "Fictional lineup context",
            communityContext
        });
        File.WriteAllText(path, json);
        return path;
    }
}
