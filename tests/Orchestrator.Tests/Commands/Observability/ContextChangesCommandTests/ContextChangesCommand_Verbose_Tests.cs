using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ContextChangesCommandTests;

/// <summary>
/// Tests for verbose and non-verbose output in ContextChangesCommand.
/// </summary>
public class ContextChangesCommand_Verbose_Tests : ContextChangesCommandTests_Base
{
    [Test]
    public async Task Verbose_mode_shows_verbose_mode_enabled_message()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: []);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_verbose_mode_enabled_message()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: []);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
    }

    [Test]
    public async Task Verbose_mode_shows_document_count()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "doc-a.csv",
            content: "same",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-a.csv", "doc-b.csv", "doc-c.csv"],
            latestDocuments: new()
            {
                ["doc-a.csv"] = latestDoc,
                ["doc-b.csv"] = latestDoc,
                ["doc-c.csv"] = latestDoc
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 3 context document(s)");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_document_count()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "doc-a.csv",
            content: "same",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-a.csv"],
            latestDocuments: new()
            {
                ["doc-a.csv"] = latestDoc
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("context document(s)");
    }

    [Test]
    public async Task Verbose_mode_shows_checking_document_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "standings.csv",
            content: "data",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["standings.csv"],
            latestDocuments: new() { ["standings.csv"] = latestDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Checking document: standings.csv");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_checking_document_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "standings.csv",
            content: "data",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["standings.csv"],
            latestDocuments: new() { ["standings.csv"] = latestDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Checking document:");
    }

    [Test]
    public async Task Verbose_mode_shows_document_not_found_message()
    {
        // Arrange — latest returns null for a document
        var mockRepo = CreateContextChangesRepository(
            documentNames: ["missing-doc.csv"],
            latestDocuments: new() { ["missing-doc.csv"] = null });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Document 'missing-doc.csv' not found");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_document_not_found_message()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: ["missing-doc.csv"],
            latestDocuments: new() { ["missing-doc.csv"] = null });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("not found");
    }

    [Test]
    public async Task Verbose_mode_shows_only_one_version_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "single-version.md",
            content: "first",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["single-version.md"],
            latestDocuments: new() { ["single-version.md"] = latestDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("has only one version");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_only_one_version_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "single-version.md",
            content: "first",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["single-version.md"],
            latestDocuments: new() { ["single-version.md"] = latestDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("has only one version");
    }

    [Test]
    public async Task Verbose_mode_shows_previous_version_not_found_message()
    {
        // Arrange — latest is version 3, but version 2 is not found
        var latestDoc = CreateContextDocument(
            documentName: "orphaned.csv",
            content: "data",
            version: 3);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["orphaned.csv"],
            latestDocuments: new() { ["orphaned.csv"] = latestDoc },
            documentsByVersion: new());
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Previous version of 'orphaned.csv' not found");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_previous_version_not_found_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "orphaned.csv",
            content: "data",
            version: 3);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["orphaned.csv"],
            latestDocuments: new() { ["orphaned.csv"] = latestDoc },
            documentsByVersion: new());
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Previous version");
    }

    [Test]
    public async Task Verbose_mode_shows_no_content_changes_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "unchanged.csv",
            content: "same content",
            version: 2);
        var previousDoc = CreateContextDocument(
            documentName: "unchanged.csv",
            content: "same content",
            version: 1);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["unchanged.csv"],
            latestDocuments: new() { ["unchanged.csv"] = latestDoc },
            documentsByVersion: new() { [("unchanged.csv", 1)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("no content changes between v1 and v2");
    }

    [Test]
    public async Task Non_verbose_mode_does_not_show_no_content_changes_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "unchanged.csv",
            content: "same content",
            version: 2);
        var previousDoc = CreateContextDocument(
            documentName: "unchanged.csv",
            content: "same content",
            version: 1);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["unchanged.csv"],
            latestDocuments: new() { ["unchanged.csv"] = latestDoc },
            documentsByVersion: new() { [("unchanged.csv", 1)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("no content changes");
    }

    [Test]
    public async Task Verbose_mode_with_seed_shows_seed_in_selection_message()
    {
        // Arrange — more documents than count, with seed
        var latestDoc = CreateContextDocument(
            documentName: "doc",
            content: "data",
            version: 0);

        var documentNames = Enumerable.Range(1, 20).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--verbose", "--count", "5", "--seed", "42");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("seed: 42");
    }

    [Test]
    public async Task Verbose_mode_without_seed_does_not_show_seed_in_selection_message()
    {
        // Arrange — more documents than count, no seed
        var latestDoc = CreateContextDocument(
            documentName: "doc",
            content: "data",
            version: 0);

        var documentNames = Enumerable.Range(1, 20).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--verbose", "--count", "5");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Showing 5 of 20 documents");
        await Assert.That(output).DoesNotContain("seed:");
    }

    [Test]
    public async Task Verbose_mode_shows_document_selection_count_when_fewer_selected()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "doc",
            content: "data",
            version: 0);

        var documentNames = Enumerable.Range(1, 15).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--verbose", "--count", "3");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Showing 3 of 15 documents");
    }

    [Test]
    public async Task Verbose_mode_does_not_show_selection_message_when_all_documents_shown()
    {
        // Arrange — count >= number of documents
        var latestDoc = CreateContextDocument(
            documentName: "doc",
            content: "data",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-1.csv", "doc-2.csv"],
            latestDocuments: new()
            {
                ["doc-1.csv"] = latestDoc,
                ["doc-2.csv"] = latestDoc
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--verbose", "--count", "10");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Showing");
    }
}
