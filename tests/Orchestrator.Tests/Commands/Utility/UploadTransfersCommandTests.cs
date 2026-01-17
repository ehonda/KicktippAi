using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.UploadTransfers;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

/// <summary>
/// Tests for the <see cref="UploadTransfersCommand"/>.
/// </summary>
public class UploadTransfersCommandTests
{
    /// <summary>
    /// Creates a configured command app for testing UploadTransfersCommand.
    /// </summary>
    /// <param name="contextRepository">Mock context repository. Defaults to a new mock configured for upload.</param>
    /// <param name="fileProvider">Mock file provider. Defaults to an empty mock.</param>
    /// <param name="logger">Logger instance. Defaults to a FakeLogger.</param>
    /// <returns>A tuple containing the CommandApp, TestConsole, and mock context repository for verification.</returns>
    private static (CommandApp App, TestConsole Console, Mock<IContextRepository> ContextRepo) CreateUploadTransfersCommandApp(
        Option<Mock<IContextRepository>> contextRepository = default,
        Option<Mock<IFileProvider>> fileProvider = default,
        Option<FakeLogger<UploadTransfersCommand>> logger = default)
    {
        var testConsole = new TestConsole();
        var mockContextRepo = contextRepository.Or(() => CreateMockContextRepositoryForUpload());
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var mockFileProvider = fileProvider.Or(() => new Mock<IFileProvider>());
        var fakeLogger = logger.Or(() => new FakeLogger<UploadTransfersCommand>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddKeyedSingleton<IFileProvider>(ServiceRegistrationExtensions.TransfersDocumentsFileProviderKey, mockFileProvider.Object);
        services.AddSingleton<ILogger<UploadTransfersCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<UploadTransfersCommand>("upload-transfers");
        });

        return (app, testConsole, mockContextRepo);
    }

    /// <summary>
    /// Creates a mock file provider with a single transfers document JSON file.
    /// </summary>
    private static Mock<IFileProvider> CreateFileProviderWithTransfersDocument(
        string communityContext,
        string teamAbbreviation,
        string jsonContent)
    {
        var docName = $"{teamAbbreviation.ToLowerInvariant()}-transfers.csv";
        var filePath = $"output/{communityContext}/{docName}.json";
        var files = new Dictionary<string, string>
        {
            [filePath] = jsonContent
        };
        return CreateMockTransfersFileProvider(files);
    }

    [Test]
    public async Task Uploading_new_document_creates_version_zero()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 0);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "fcb-transfers.csv",
            content: "Player transfers data",
            description: "FCB transfers",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-community", "fcb", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "fcb", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Upload Transfers command initialized for document:");
        await Assert.That(output).Contains("fcb-transfers.csv");
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("No existing transfers document found");
        await Assert.That(output).Contains("will create version 0");
        await Assert.That(output).Contains("Created transfers document version 0");
    }

    [Test]
    public async Task Uploading_existing_document_with_changes_creates_new_version()
    {
        // Arrange
        var existingDoc = CreateContextDocument(
            documentName: "fcb-transfers.csv",
            content: "Old content",
            version: 1);
        var mockContextRepo = CreateMockContextRepositoryForUpload(existingDoc, savedVersion: 2);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "fcb-transfers.csv",
            content: "New content",
            description: "Updated transfers",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-community", "fcb", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "fcb", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing transfers document 'fcb-transfers.csv' (version 1)");
        await Assert.That(output).Contains("Content changed - created new version 2");
    }

    [Test]
    public async Task Uploading_existing_document_without_changes_keeps_same_version()
    {
        // Arrange
        var existingDoc = CreateContextDocument(
            documentName: "fcb-transfers.csv",
            content: "Same content",
            version: 3);
        var mockContextRepo = CreateMockContextRepositoryForUpload(existingDoc, savedVersion: null);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "fcb-transfers.csv",
            content: "Same content",
            description: "Same data",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-community", "fcb", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "fcb", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing transfers document 'fcb-transfers.csv' (version 3)");
        await Assert.That(output).Contains("Content unchanged");
        await Assert.That(output).Contains("remains at version 3");
    }

    [Test]
    public async Task File_not_found_returns_error_exit_code()
    {
        // Arrange - file provider returns non-existent file
        var mockFileProvider = CreateMockTransfersFileProvider(new Dictionary<string, string>());
        var (app, console, _) = CreateUploadTransfersCommandApp(fileProvider: mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "missing", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Transfers document JSON not found");
        await Assert.That(output).Contains("output/my-community/missing-transfers.csv.json");
        await Assert.That(output).Contains("Run Create-TransfersDocument.ps1");
    }

    [Test]
    public async Task Invalid_json_returns_error_exit_code()
    {
        // Arrange - file contains invalid JSON that deserializes to null
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-community", "bad", "null");
        var (app, console, _) = CreateUploadTransfersCommandApp(fileProvider: mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "bad", "-c", "my-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to parse transfers document JSON");
    }

    [Test]
    public async Task Repository_exception_returns_error_exit_code_and_logs_error()
    {
        // Arrange
        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Firebase connection failed"));
        var transfersJson = CreateTransfersDocumentJson();
        var mockFileProvider = CreateFileProviderWithTransfersDocument("test-community", "test", transfersJson);
        var fakeLogger = new FakeLogger<UploadTransfersCommand>();
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider, fakeLogger);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "test", "-c", "test-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Firebase connection failed");
        await Assert.That(fakeLogger.LatestRecord.Level).IsEqualTo(LogLevel.Error);
        await Assert.That(fakeLogger.LatestRecord.Message).Contains("Error in upload-transfers command");
    }

    [Test]
    public async Task Verbose_mode_shows_additional_details_for_new_document()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 0);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "verbose-transfers.csv",
            content: "Verbose content here",
            description: "Verbose description",
            communityContext: "verbose-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("verbose-community", "verbose", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "verbose", "-c", "verbose-community", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Document Name: verbose-transfers.csv");
        await Assert.That(output).Contains("Community Context: verbose-community");
        await Assert.That(output).Contains("Content length:");
        await Assert.That(output).Contains("characters");
    }

    [Test]
    public async Task Verbose_mode_shows_checking_for_changes_for_existing_document()
    {
        // Arrange
        var existingDoc = CreateContextDocument(
            documentName: "existing-transfers.csv",
            content: "Short",
            version: 1);
        var mockContextRepo = CreateMockContextRepositoryForUpload(existingDoc, savedVersion: 2);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "existing-transfers.csv",
            content: "Much longer content than before",
            description: "Updated",
            communityContext: "my-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-community", "existing", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "existing", "-c", "my-community", "-v"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Checking for changes");
    }

    [Test]
    public async Task Non_verbose_mode_hides_details()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 0);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "simple-transfers.csv",
            content: "Simple content",
            communityContext: "simple-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("simple-community", "simple", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "simple", "-c", "simple-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
        await Assert.That(output).DoesNotContain("Document Name:");
        await Assert.That(output).DoesNotContain("Content length:");
    }

    [Test]
    public async Task Repository_is_called_with_correct_parameters()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 5);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "my-transfers.csv",
            content: "My content",
            description: "My description",
            communityContext: "my-ctx");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("my-ctx", "my", transfersJson);
        var (app, console, contextRepo) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        await app.RunAsync(["upload-transfers", "my", "-c", "my-ctx"]);

        // Assert - verify GetLatestContextDocumentAsync was called with correct parameters
        contextRepo.Verify(r => r.GetLatestContextDocumentAsync(
            "my-transfers.csv",
            "my-ctx",
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - verify SaveContextDocumentAsync was called with correct parameters
        contextRepo.Verify(r => r.SaveContextDocumentAsync(
            "my-transfers.csv",
            "My content",
            "my-ctx",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Reading_transfers_document_shows_file_path()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 0);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "test-transfers.csv",
            communityContext: "path-test");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("path-test", "test", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "test", "-c", "path-test"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reading transfers document from:");
        await Assert.That(output).Contains("output/path-test/test-transfers.csv.json");
    }

    [Test]
    public async Task Team_abbreviation_is_lowercased_in_document_name()
    {
        // Arrange
        var mockContextRepo = CreateMockContextRepositoryForUpload(savedVersion: 0);
        var transfersJson = CreateTransfersDocumentJson(
            documentName: "fcb-transfers.csv",
            content: "FCB transfers data",
            communityContext: "test-community");
        var mockFileProvider = CreateFileProviderWithTransfersDocument("test-community", "FCB", transfersJson);
        var (app, console, _) = CreateUploadTransfersCommandApp(mockContextRepo, mockFileProvider);

        // Act
        var exitCode = await app.RunAsync(["upload-transfers", "FCB", "-c", "test-community"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("fcb-transfers.csv");
        await Assert.That(output).DoesNotContain("FCB-transfers.csv");
    }
}
