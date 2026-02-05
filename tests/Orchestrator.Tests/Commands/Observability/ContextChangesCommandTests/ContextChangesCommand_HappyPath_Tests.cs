using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ContextChangesCommandTests;

/// <summary>
/// Tests for the core happy-path workflow of ContextChangesCommand.
/// </summary>
public class ContextChangesCommand_HappyPath_Tests : ContextChangesCommandTests_Base
{
    [Test]
    public async Task Running_command_displays_community_context_in_header()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: []);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "my-test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("my-test-community");
    }

    [Test]
    public async Task Running_command_with_no_documents_shows_no_documents_message()
    {
        // Arrange
        var mockRepo = CreateContextChangesRepository(
            documentNames: []);
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "empty-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No context documents found for this community");
    }

    [Test]
    public async Task Running_command_with_changed_document_shows_diff_output()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "standings.csv",
            content: "Position,Team\n1,Bayern\n2,Dortmund",
            version: 2);
        var previousDoc = CreateContextDocument(
            documentName: "standings.csv",
            content: "Position,Team\n1,Dortmund\n2,Bayern",
            version: 1);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["standings.csv"],
            latestDocuments: new() { ["standings.csv"] = latestDoc },
            documentsByVersion: new() { [("standings.csv", 1)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("standings.csv");
        await Assert.That(output).Contains("Found changes in 1 document(s)");
    }

    [Test]
    public async Task Running_command_with_identical_content_shows_no_changes_message()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "rules.md",
            content: "Same content",
            version: 2);
        var previousDoc = CreateContextDocument(
            documentName: "rules.md",
            content: "Same content",
            version: 1);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["rules.md"],
            latestDocuments: new() { ["rules.md"] = latestDoc },
            documentsByVersion: new() { [("rules.md", 1)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }

    [Test]
    public async Task Running_command_with_mixed_documents_counts_only_changed_ones()
    {
        // Arrange — two docs: one changed, one identical
        var changedLatest = CreateContextDocument(
            documentName: "doc-a.csv",
            content: "new content",
            version: 2);
        var changedPrevious = CreateContextDocument(
            documentName: "doc-a.csv",
            content: "old content",
            version: 1);

        var unchangedLatest = CreateContextDocument(
            documentName: "doc-b.csv",
            content: "same",
            version: 3);
        var unchangedPrevious = CreateContextDocument(
            documentName: "doc-b.csv",
            content: "same",
            version: 2);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["doc-a.csv", "doc-b.csv"],
            latestDocuments: new()
            {
                ["doc-a.csv"] = changedLatest,
                ["doc-b.csv"] = unchangedLatest
            },
            documentsByVersion: new()
            {
                [("doc-a.csv", 1)] = changedPrevious,
                [("doc-b.csv", 2)] = unchangedPrevious
            });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found changes in 1 document(s)");
    }

    [Test]
    public async Task Running_command_with_single_version_document_shows_no_changes()
    {
        // Arrange — document at version 0 (only one version)
        var latestDoc = CreateContextDocument(
            documentName: "new-doc.md",
            content: "first version",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["new-doc.md"],
            latestDocuments: new() { ["new-doc.md"] = latestDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No changes found between versions");
    }

    [Test]
    public async Task Diff_output_shows_added_and_removed_lines()
    {
        // Arrange
        var latestDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Header\nNewLine",
            version: 1);
        var previousDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Header\nOldLine",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["data.csv"],
            latestDocuments: new() { ["data.csv"] = latestDoc },
            documentsByVersion: new() { [("data.csv", 0)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // The diff should show the changed content
        await Assert.That(output).Contains("OldLine");
        await Assert.That(output).Contains("NewLine");
    }

    [Test]
    public async Task Diff_output_shows_lines_only_added_at_end()
    {
        // Arrange — new doc has extra lines at the end
        var latestDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Line1\nLine2\nLine3",
            version: 1);
        var previousDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Line1",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["data.csv"],
            latestDocuments: new() { ["data.csv"] = latestDoc },
            documentsByVersion: new() { [("data.csv", 0)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Line2");
        await Assert.That(output).Contains("Line3");
    }

    [Test]
    public async Task Diff_output_shows_lines_only_removed_at_end()
    {
        // Arrange — old doc has extra lines that were removed
        var latestDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Line1",
            version: 1);
        var previousDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Line1\nLine2\nLine3",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["data.csv"],
            latestDocuments: new() { ["data.csv"] = latestDoc },
            documentsByVersion: new() { [("data.csv", 0)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Line2");
        await Assert.That(output).Contains("Line3");
    }

    [Test]
    public async Task Diff_output_escapes_markup_characters_in_content()
    {
        // Arrange — content with brackets that need escaping for Spectre.Console markup
        var latestDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Value [new]",
            version: 1);
        var previousDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "Value [old]",
            version: 0);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["data.csv"],
            latestDocuments: new() { ["data.csv"] = latestDoc },
            documentsByVersion: new() { [("data.csv", 0)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert — command should not crash on bracket content
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found changes in 1 document(s)");
    }

    [Test]
    public async Task Diff_output_shows_version_timestamps()
    {
        // Arrange
        var oldTimestamp = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var newTimestamp = new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero);

        var latestDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "new",
            version: 2,
            createdAt: newTimestamp);
        var previousDoc = CreateContextDocument(
            documentName: "data.csv",
            content: "old",
            version: 1,
            createdAt: oldTimestamp);

        var mockRepo = CreateContextChangesRepository(
            documentNames: ["data.csv"],
            latestDocuments: new() { ["data.csv"] = latestDoc },
            documentsByVersion: new() { [("data.csv", 1)] = previousDoc });
        var context = CreateContextChangesCommandApp(mockRepo);

        // Act
        var (exitCode, output) = await RunContextChangesAsync(context, "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("v1");
        await Assert.That(output).Contains("v2");
    }
}
