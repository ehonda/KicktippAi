using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository.GetContextDocumentVersionsAsync method.
/// </summary>
public class FirebaseContextRepository_GetContextDocumentVersionsAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_versions_for_non_existent_document_returns_empty_list()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var versions = await repository.GetContextDocumentVersionsAsync(
            "non-existent",
            "test-community");

        // Assert
        await Assert.That(versions).IsEmpty();
    }

    [Test]
    public async Task Getting_versions_returns_all_versions_in_order()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("test-document", "v0", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "v1", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "v2", "test-community");

        // Act
        var versions = await repository.GetContextDocumentVersionsAsync(
            "test-document",
            "test-community");

        // Assert
        await Assert.That(versions).HasCount().EqualTo(3);
        await Assert.That(versions[0].Version).IsEqualTo(0);
        await Assert.That(versions[0].Content).IsEqualTo("v0");
        await Assert.That(versions[1].Version).IsEqualTo(1);
        await Assert.That(versions[1].Content).IsEqualTo("v1");
        await Assert.That(versions[2].Version).IsEqualTo(2);
        await Assert.That(versions[2].Content).IsEqualTo("v2");
    }

    [Test]
    public async Task Getting_versions_filters_by_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("test-document", "content-a", "community-a");
        await repository.SaveContextDocumentAsync("test-document", "content-b", "community-b");

        // Act
        var versions = await repository.GetContextDocumentVersionsAsync(
            "test-document",
            "community-a");

        // Assert
        await Assert.That(versions).HasCount().EqualTo(1);
        await Assert.That(versions[0].Content).IsEqualTo("content-a");
    }
}
