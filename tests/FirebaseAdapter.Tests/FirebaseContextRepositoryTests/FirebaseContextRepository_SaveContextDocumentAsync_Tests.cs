using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
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
        var logger = new FakeLogger<FirebaseContextRepository>();
        var repository = new FirebaseContextRepository(Fixture.Db, logger);

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
        var logger = new FakeLogger<FirebaseContextRepository>();
        var repository = new FirebaseContextRepository(Fixture.Db, logger);

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
        var logger = new FakeLogger<FirebaseContextRepository>();
        var repository = new FirebaseContextRepository(Fixture.Db, logger);

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
        var logger = new FakeLogger<FirebaseContextRepository>();
        var repository = new FirebaseContextRepository(Fixture.Db, logger);

        // Act
        await repository.SaveContextDocumentAsync(
            "test-document",
            "test content",
            "test-community");

        var retrieved = await repository.GetLatestContextDocumentAsync(
            "test-document",
            "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.DocumentName).IsEqualTo("test-document");
        await Assert.That(retrieved.Content).IsEqualTo("test content");
        await Assert.That(retrieved.Version).IsEqualTo(0);
    }
}
