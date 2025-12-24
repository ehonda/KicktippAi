using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository.SaveContextDocumentAsync method.
/// </summary>
public class FirebaseContextRepository_SaveContextDocumentAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_new_document_returns_version_zero()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var version = await repository.SaveContextDocumentAsync(
            "test-document",
            "test content",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(0);
    }

    [Test]
    public async Task Saving_document_with_changed_content_increments_version()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "original content",
            "test-community");

        // Act
        var version = await repository.SaveContextDocumentAsync(
            "test-document",
            "updated content",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(1);
    }

    [Test]
    public async Task Saving_document_with_same_content_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "same content",
            "test-community");

        // Act
        var version = await repository.SaveContextDocumentAsync(
            "test-document",
            "same content",
            "test-community");

        // Assert
        await Assert.That(version).IsNull();
    }

    [Test]
    public async Task Saved_document_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        await repository.SaveContextDocumentAsync(
            "test-document",
            "test content",
            "test-community");

        var retrieved = await repository.GetLatestContextDocumentAsync(
            "test-document",
            "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull()
            .And.Member(r => r!.DocumentName, n => n.IsEqualTo("test-document"))
            .And.Member(r => r!.Content, c => c.IsEqualTo("test content"))
            .And.Member(r => r!.Version, v => v.IsEqualTo(0));
    }
}
