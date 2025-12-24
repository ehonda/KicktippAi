using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository.UpdateContextDocumentVersionAsync method.
/// </summary>
public class FirebaseContextRepository_UpdateContextDocumentVersionAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Updating_non_existent_document_returns_false()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.UpdateContextDocumentVersionAsync(
            "non-existent",
            version: 0,
            "new content",
            "test-community");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Updating_existing_document_returns_true()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "original content",
            "test-community");

        // Act
        var result = await repository.UpdateContextDocumentVersionAsync(
            "test-document",
            version: 0,
            "updated content",
            "test-community");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Updated_document_content_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "original content",
            "test-community");

        // Act
        await repository.UpdateContextDocumentVersionAsync(
            "test-document",
            version: 0,
            "updated content",
            "test-community");

        var retrieved = await repository.GetContextDocumentAsync(
            "test-document",
            version: 0,
            "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull()
            .And.Member(r => r!.Content, c => c.IsEqualTo("updated content"));
    }

    [Test]
    public async Task Updating_specific_version_does_not_affect_other_versions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("test-document", "v0", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "v1", "test-community");

        // Act
        await repository.UpdateContextDocumentVersionAsync(
            "test-document",
            version: 0,
            "v0 updated",
            "test-community");

        var version0 = await repository.GetContextDocumentAsync("test-document", 0, "test-community");
        var version1 = await repository.GetContextDocumentAsync("test-document", 1, "test-community");

        // Assert
        await Assert.That(version0!.Content).IsEqualTo("v0 updated");
        await Assert.That(version1!.Content).IsEqualTo("v1");
    }

    [Test]
    public async Task Updating_non_existent_version_returns_false()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "content",
            "test-community");

        // Act
        var result = await repository.UpdateContextDocumentVersionAsync(
            "test-document",
            version: 99,
            "updated",
            "test-community");

        // Assert
        await Assert.That(result).IsFalse();
    }
}
