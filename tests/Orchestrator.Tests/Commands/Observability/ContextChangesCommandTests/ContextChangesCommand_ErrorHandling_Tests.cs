using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Observability.ContextChanges;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ContextChangesCommandTests;

/// <summary>
/// Tests for error handling and edge cases in ContextChangesCommand.
/// </summary>
public class ContextChangesCommand_ErrorHandling_Tests : ContextChangesCommandTests_Base
{
    [Test]
    public async Task Repository_exception_returns_exit_code_one()
    {
        // Arrange — GetContextDocumentNamesAsync throws
        var mockRepo = new Mock<IContextRepository>();
        mockRepo.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Firestore connection failed"));
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Firestore connection failed");
    }

    [Test]
    public async Task Repository_exception_logs_error()
    {
        // Arrange
        var mockRepo = new Mock<IContextRepository>();
        mockRepo.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        var logger = new FakeLogger<ContextChangesCommand>();
        var context = CreateContextChangesCommandApp(mockRepo, logger);

        // Act
        await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        var errorLogs = logger.Collector.GetSnapshot()
            .Where(log => log.Level == Microsoft.Extensions.Logging.LogLevel.Error)
            .ToList();
        await Assert.That(errorLogs.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Per_document_exception_shows_error_but_continues_processing()
    {
        // Arrange — first document throws, second document is normal
        var normalDoc = CreateContextDocument(
            documentName: "normal.csv",
            content: "same",
            version: 0);

        var mockRepo = new Mock<IContextRepository>();
        mockRepo.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "failing-doc.csv", "normal.csv" });

        mockRepo.Setup(r => r.GetLatestContextDocumentAsync(
                "failing-doc.csv",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Doc retrieval failed"));

        mockRepo.Setup(r => r.GetLatestContextDocumentAsync(
                "normal.csv",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalDoc);

        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert — command succeeds overall (exit code 0), shows error for failing doc
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing document 'failing-doc.csv'");
        await Assert.That(output).Contains("Doc retrieval failed");
    }

    [Test]
    public async Task Per_document_exception_does_not_count_as_change()
    {
        // Arrange — one document throws, one has no changes
        var latestDoc = CreateContextDocument(
            documentName: "unchanged.csv",
            content: "same",
            version: 0);

        var mockRepo = new Mock<IContextRepository>();
        mockRepo.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "error-doc.csv", "unchanged.csv" });

        mockRepo.Setup(r => r.GetLatestContextDocumentAsync(
                "error-doc.csv",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));

        mockRepo.Setup(r => r.GetLatestContextDocumentAsync(
                "unchanged.csv",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestDoc);

        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }

    [Test]
    public async Task Latest_document_returns_null_does_not_count_as_change()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: ["missing.csv"],
            latestDocuments: new() { ["missing.csv"] = null });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }

    [Test]
    public async Task Previous_version_returns_null_does_not_count_as_change()
    {
        // Arrange — latest is version 5, but previous version (4) not found
        var latestDoc = CreateContextDocument(
            documentName: "orphaned.csv",
            content: "data",
            version: 5);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["orphaned.csv"],
            latestDocuments: new() { ["orphaned.csv"] = latestDoc },
            documentsByVersion: new());
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }

    [Test]
    public async Task Multiple_documents_all_with_errors_shows_no_changes()
    {
        // Arrange
        var mockRepo = new Mock<IContextRepository>();
        mockRepo.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "err-1.csv", "err-2.csv" });

        mockRepo.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("all fail"));

        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }
}
