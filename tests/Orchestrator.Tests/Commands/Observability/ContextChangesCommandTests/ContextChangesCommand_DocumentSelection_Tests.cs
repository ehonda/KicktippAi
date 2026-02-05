using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ContextChangesCommandTests;

/// <summary>
/// Tests for the document selection logic (--count, --seed) in ContextChangesCommand.
/// </summary>
public class ContextChangesCommand_DocumentSelection_Tests : ContextChangesCommandTests_Base
{
    [Test]
    public async Task Count_larger_than_available_documents_shows_all_documents()
    {
        // Arrange — 3 documents, count is 10
        var latestDoc = CreateContextDocument(content: "same", version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-1.csv", "doc-2.csv", "doc-3.csv"],
            latestDocuments: new()
            {
                ["doc-1.csv"] = latestDoc,
                ["doc-2.csv"] = latestDoc,
                ["doc-3.csv"] = latestDoc
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--count", "10", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // All 3 documents should be checked
        await Assert.That(output).Contains("Checking document: doc-1.csv");
        await Assert.That(output).Contains("Checking document: doc-2.csv");
        await Assert.That(output).Contains("Checking document: doc-3.csv");
    }

    [Test]
    public async Task Count_smaller_than_available_documents_shows_only_count_documents()
    {
        // Arrange — 5 documents, count is 2, use seed for determinism
        var latestDoc = CreateContextDocument(content: "same", version: 0);

        var documentNames = Enumerable.Range(1, 5).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--count", "2", "--seed", "42", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Showing 2 of 5 documents");
        // Count occurrences of "Checking document:" to verify only 2 were processed
        var checkingCount = output.Split("Checking document:").Length - 1;
        await Assert.That(checkingCount).IsEqualTo(2);
    }

    [Test]
    public async Task Same_seed_produces_same_document_selection()
    {
        // Arrange — use the same seed twice and verify identical output
        var latestDoc = CreateContextDocument(content: "same", version: 0);

        var documentNames = Enumerable.Range(1, 10).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo1 = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context1 = CreateContextChangesCommandApp(mockRepo1);

        var mockRepo2 = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context2 = CreateContextChangesCommandApp(mockRepo2);

        // Act
        var (_, output1) = await RunContextChangesAsync(
            context1, "-c", "test-community", "--count", "3", "--seed", "123", "--verbose");
        var (_, output2) = await RunContextChangesAsync(
            context2, "-c", "test-community", "--count", "3", "--seed", "123", "--verbose");

        // Assert — both runs should produce the same "Checking document:" lines
        var checkingLines1 = output1.Split('\n')
            .Where(l => l.Contains("Checking document:"))
            .ToList();
        var checkingLines2 = output2.Split('\n')
            .Where(l => l.Contains("Checking document:"))
            .ToList();
        await Assert.That(checkingLines1).IsEquivalentTo(checkingLines2);
    }

    [Test]
    public async Task Default_count_is_ten()
    {
        // Arrange — 15 documents, no --count specified (default is 10)
        var latestDoc = CreateContextDocument(content: "same", version: 0);

        var documentNames = Enumerable.Range(1, 15).Select(i => $"doc-{i}.csv").ToList();
        var latestDocs = documentNames.ToDictionary(n => n, _ => (ContextDocument?)latestDoc);

        var mockRepo = CreateContextChangesRepository(
            documentNames: documentNames,
            latestDocuments: latestDocs);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--seed", "1", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Showing 10 of 15 documents");
    }

    [Test]
    public async Task Count_equal_to_available_documents_shows_all()
    {
        // Arrange — exactly 3 documents, count is 3
        var latestDoc = CreateContextDocument(content: "same", version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-1.csv", "doc-2.csv", "doc-3.csv"],
            latestDocuments: new()
            {
                ["doc-1.csv"] = latestDoc,
                ["doc-2.csv"] = latestDoc,
                ["doc-3.csv"] = latestDoc
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(
            context, "-c", "test-community", "--count", "3", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // When count >= doc count, no "Showing X of Y" message
        await Assert.That(output).DoesNotContain("Showing");
        // All 3 checked
        await Assert.That(output).Contains("Checking document: doc-1.csv");
        await Assert.That(output).Contains("Checking document: doc-2.csv");
        await Assert.That(output).Contains("Checking document: doc-3.csv");
    }
}
