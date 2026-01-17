using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.UploadKpi;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

/// <summary>
/// Tests for the <see cref="UploadKpiCommand"/>.
/// </summary>
public class UploadKpiCommandTests
{
    /// <summary>
    /// Creates a configured command app for testing UploadKpiCommand.
    /// </summary>
    /// <param name="kpiRepository">Mock KPI repository. Defaults to a new mock configured for upload.</param>
    /// <param name="fileProvider">Mock file provider. Defaults to an empty mock.</param>
    /// <param name="logger">Logger instance. Defaults to a FakeLogger.</param>
    /// <returns>A tuple containing the CommandApp, TestConsole, and mock KPI repository for verification.</returns>
    private static (CommandApp App, TestConsole Console, Mock<IKpiRepository> KpiRepo) CreateUploadKpiCommandApp(
        Option<Mock<IKpiRepository>> kpiRepository = default,
        Option<Mock<IFileProvider>> fileProvider = default,
        Option<FakeLogger<UploadKpiCommand>> logger = default)
    {
        var testConsole = new TestConsole();
        var mockKpiRepo = kpiRepository.Or(() => CreateMockKpiRepositoryForUpload());
        var mockFirebaseFactory = CreateMockFirebaseServiceFactory(mockKpiRepo);
        var mockFileProvider = fileProvider.Or(() => new Mock<IFileProvider>());
        var fakeLogger = logger.Or(() => new FakeLogger<UploadKpiCommand>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddKeyedSingleton<IFileProvider>(ServiceRegistrationExtensions.KpiDocumentsFileProviderKey, mockFileProvider.Object);
        services.AddSingleton<ILogger<UploadKpiCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<UploadKpiCommand>("upload-kpi");
        });

        return (app, testConsole, mockKpiRepo);
    }

    /// <summary>
    /// Creates a mock file provider with a single KPI document JSON file.
    /// </summary>
    private static Mock<IFileProvider> CreateFileProviderWithKpiDocument(
        string communityContext,
        string documentName,
        string jsonContent)
    {
        var filePath = $"output/{communityContext}/{documentName}.json";
        var files = new Dictionary<string, string>
        {
            [filePath] = jsonContent
        };
        return CreateMockKpiFileProvider(files);
    }

    [Test]
    public async Task Uploading_new_document_creates_version_zero()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "team-data",
            content: "Team information",
            description: "Team KPI data",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-community", "team-data", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "team-data", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Upload KPI command initialized for document:");
        await Assert.That(output).Contains("team-data");
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("No existing KPI document found");
        await Assert.That(output).Contains("will create version 0");
        await Assert.That(output).Contains("Successfully created KPI document");
        await Assert.That(output).Contains("as version 0");
    }

    [Test]
    public async Task Uploading_existing_document_with_changes_creates_new_version()
    {
        // Arrange
        var existingDoc = CreateKpiDocument(
            documentName: "team-data",
            content: "Old content",
            version: 1);
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(existingDoc, savedVersion: 2);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "team-data",
            content: "New content",
            description: "Updated data",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-community", "team-data", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "team-data", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing KPI document 'team-data' (version 1)");
        await Assert.That(output).Contains("Checking for content changes");
        await Assert.That(output).Contains("Content changed - Created new version 2");
    }

    [Test]
    public async Task Uploading_existing_document_without_changes_keeps_same_version()
    {
        // Arrange
        var existingDoc = CreateKpiDocument(
            documentName: "team-data",
            content: "Same content",
            version: 3);
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(existingDoc, savedVersion: 3);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "team-data",
            content: "Same content",
            description: "Same data",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-community", "team-data", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "team-data", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing KPI document 'team-data' (version 3)");
        await Assert.That(output).Contains("Content unchanged");
        await Assert.That(output).Contains("remains at version 3");
    }

    [Test]
    public async Task File_not_found_returns_error_exit_code()
    {
        // Arrange - file provider returns non-existent file
        var mockFileProvider = CreateMockKpiFileProvider(new Dictionary<string, string>());
        var (app, console, _) = CreateUploadKpiCommandApp(fileProvider: mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "missing-doc", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("KPI document file not found");
        await Assert.That(output).Contains("output/my-community/missing-doc.json");
        await Assert.That(output).Contains("Run the PowerShell script with firebase mode");
    }

    [Test]
    public async Task Invalid_json_returns_error_exit_code()
    {
        // Arrange - file contains invalid JSON that deserializes to null
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-community", "bad-doc", "null");
        var (app, console, _) = CreateUploadKpiCommandApp(fileProvider: mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "bad-doc", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to parse KPI document JSON");
    }

    [Test]
    public async Task Repository_exception_returns_error_exit_code_and_logs_error()
    {
        // Arrange
        var mockKpiRepo = new Mock<IKpiRepository>();
        mockKpiRepo.Setup(r => r.GetKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Firebase connection failed"));
        var kpiJson = CreateKpiDocumentJson();
        var mockFileProvider = CreateFileProviderWithKpiDocument("test-community", "test-document", kpiJson);
        var fakeLogger = new FakeLogger<UploadKpiCommand>();
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider, fakeLogger);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "test-document", "-c", "test-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Firebase connection failed");
        await Assert.That(fakeLogger.LatestRecord.Level).IsEqualTo(LogLevel.Error);
        await Assert.That(fakeLogger.LatestRecord.Message).Contains("Error in upload-kpi command");
    }

    [Test]
    public async Task Verbose_mode_shows_additional_details_for_new_document()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "verbose-doc",
            content: "Verbose content here",
            description: "Verbose description",
            communityContext: "verbose-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("verbose-community", "verbose-doc", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "verbose-doc", "-c", "verbose-community", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Document Name: verbose-doc");
        await Assert.That(output).Contains("Community Context: verbose-community");
        await Assert.That(output).Contains("Content length:");
        await Assert.That(output).Contains("characters");
        await Assert.That(output).Contains("Document saved to unified kpi-documents collection");
        await Assert.That(output).Contains("Document version: 0");
    }

    [Test]
    public async Task Verbose_mode_shows_content_lengths_for_existing_document()
    {
        // Arrange
        var existingDoc = CreateKpiDocument(
            documentName: "existing-doc",
            content: "Short",
            version: 1);
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(existingDoc, savedVersion: 2);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "existing-doc",
            content: "Much longer content than before",
            description: "Updated",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-community", "existing-doc", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "existing-doc", "-c", "my-community", "-v"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Current content length:");
        await Assert.That(output).Contains("New content length:");
    }

    [Test]
    public async Task Non_verbose_mode_hides_details()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "simple-doc",
            content: "Simple content",
            communityContext: "simple-community");
        var mockFileProvider = CreateFileProviderWithKpiDocument("simple-community", "simple-doc", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "simple-doc", "-c", "simple-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
        await Assert.That(output).DoesNotContain("Document Name:");
        await Assert.That(output).DoesNotContain("Content length:");
        await Assert.That(output).DoesNotContain("Document saved to unified kpi-documents collection");
    }

    [Test]
    public async Task Repository_is_called_with_correct_parameters()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 5);
        var kpiJson = CreateKpiDocumentJson(
            documentName: "my-doc",
            content: "My content",
            description: "My description",
            communityContext: "my-ctx");
        var mockFileProvider = CreateFileProviderWithKpiDocument("my-ctx", "my-doc", kpiJson);
        var (app, console, kpiRepo) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        await app.RunAsync(["upload-kpi", "my-doc", "-c", "my-ctx"]);

        // Assert - verify GetKpiDocumentAsync was called with correct parameters
        kpiRepo.Verify(r => r.GetKpiDocumentAsync(
            "my-doc",
            "my-ctx",
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - verify SaveKpiDocumentAsync was called with correct parameters
        kpiRepo.Verify(r => r.SaveKpiDocumentAsync(
            "my-doc",
            "My content",
            "My description",
            "my-ctx",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Reading_kpi_document_shows_file_path()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var kpiJson = CreateKpiDocumentJson(communityContext: "path-test");
        var mockFileProvider = CreateFileProviderWithKpiDocument("path-test", "test-document", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "test-document", "-c", "path-test"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reading KPI document from:");
        await Assert.That(output).Contains("output/path-test/test-document.json");
    }

    [Test]
    public async Task Processing_message_is_shown_before_upload()
    {
        // Arrange
        var mockKpiRepo = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var kpiJson = CreateKpiDocumentJson();
        var mockFileProvider = CreateFileProviderWithKpiDocument("test-community", "test-document", kpiJson);
        var (app, console, _) = CreateUploadKpiCommandApp(mockKpiRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-kpi", "test-document", "-c", "test-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing KPI document");
    }
}
