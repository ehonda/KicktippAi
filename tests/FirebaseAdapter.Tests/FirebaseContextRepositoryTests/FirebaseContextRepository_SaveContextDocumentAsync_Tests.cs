using FirebaseAdapter.Models;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using TestUtilities;
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

    [Test]
    public async Task Concurrent_changed_writers_receive_distinct_append_only_versions()
    {
        var repository = CreateRepository();
        var first = repository.SaveContextDocumentAsync("test-document", "first", "test-community");
        var second = repository.SaveContextDocumentAsync("test-document", "second", "test-community");

        var versions = await Task.WhenAll(first, second);
        var stored = await repository.GetContextDocumentVersionsAsync("test-document", "test-community");

        await Assert.That(versions.Where(version => version is not null).Select(version => version!.Value))
            .IsEquivalentTo([0, 1]);
        await Assert.That(stored.Select(document => document.Version)).IsEquivalentTo([0, 1]);
        await Assert.That(stored.Select(document => document.Content)).IsEquivalentTo(["first", "second"]);
    }

    [Test]
    public async Task Publication_scoped_rows_raise_the_version_ceiling_without_becoming_an_ordinary_no_op()
    {
        await Fixture.Db.Collection("context-documents").Document("publication-payload")
            .SetAsync(new FirestoreContextDocument
            {
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = "test-community",
                PublicationSet = "diagnostics",
                DocumentName = "test-document",
                Content = "same content",
                Version = 4,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            });

        var repository = CreateRepository();
        var saved = await repository.SaveContextDocumentAsync("test-document", "same content", "test-community");
        var unchanged = await repository.SaveContextDocumentAsync("test-document", "same content", "test-community");

        await Assert.That(saved).IsEqualTo(5);
        await Assert.That(unchanged).IsNull();
    }
}
