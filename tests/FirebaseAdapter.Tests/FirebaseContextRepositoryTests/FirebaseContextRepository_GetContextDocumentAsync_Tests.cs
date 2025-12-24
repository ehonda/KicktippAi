using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository.GetContextDocumentAsync method.
/// </summary>
public class FirebaseContextRepository_GetContextDocumentAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_non_existent_document_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetContextDocumentAsync(
            "non-existent",
            version: 0,
            "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_existing_document_by_version_returns_document()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "version 0 content",
            "test-community");

        await repository.SaveContextDocumentAsync(
            "test-document",
            "version 1 content",
            "test-community");

        // Act
        var result = await repository.GetContextDocumentAsync(
            "test-document",
            version: 0,
            "test-community");

        // Assert
        await Assert.That(result).IsNotNull()
            .And.Member(r => r!.Content, content => content.IsEqualTo("version 0 content"))
            .And.Member(r => r!.Version, version => version.IsEqualTo(0));
    }

    [Test]
    public async Task Getting_specific_version_returns_correct_content()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "version 0 content",
            "test-community");

        await repository.SaveContextDocumentAsync(
            "test-document",
            "version 1 content",
            "test-community");

        // Act
        var version0 = await repository.GetContextDocumentAsync(
            "test-document",
            version: 0,
            "test-community");

        var version1 = await repository.GetContextDocumentAsync(
            "test-document",
            version: 1,
            "test-community");

        // Assert
        await Assert.That(version0!.Content).IsEqualTo("version 0 content");
        await Assert.That(version1!.Content).IsEqualTo("version 1 content");
    }

    [Test]
    public async Task Getting_non_existent_version_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "content",
            "test-community");

        // Act
        var result = await repository.GetContextDocumentAsync(
            "test-document",
            version: 99,
            "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_document_from_different_community_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(
            "test-document",
            "content",
            "community-a");

        // Act
        var result = await repository.GetContextDocumentAsync(
            "test-document",
            version: 0,
            "community-b");

        // Assert
        await Assert.That(result).IsNull();
    }
}
