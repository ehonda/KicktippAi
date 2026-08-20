using FirebaseAdapter.Models;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using TestUtilities;
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

    [Test]
    public async Task Exact_read_fails_closed_when_the_stored_scope_envelope_is_corrupt()
    {
        const string documentName = "test-document";
        const string community = "test-community";
        await Fixture.Db.Collection("context-documents")
            .Document("bundesliga-2026-27_test-document_test-community_0")
            .SetAsync(new FirestoreContextDocument
            {
                Competition = "bundesliga-2025-26",
                CommunityContext = community,
                DocumentName = documentName,
                Content = "content",
                Version = 0,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            });

        await Assert.That(() => CreateRepository().GetContextDocumentAsync(documentName, 0, community))
            .Throws<InvalidDataException>()
            .WithMessageContaining("scope or exact identity");
    }

    [Test]
    public async Task Ordinary_latest_timestamp_and_version_list_reads_ignore_a_newer_nonreserved_publication_row()
    {
        const string documentName = "test-document";
        const string community = "test-community";
        var ordinaryCreatedAt = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        var publicationCreatedAt = ordinaryCreatedAt.AddMinutes(1);
        await Fixture.Db.Collection("context-documents")
            .Document("bundesliga-2026-27_test-document_test-community_0")
            .SetAsync(new FirestoreContextDocument
            {
                Id = "bundesliga-2026-27_test-document_test-community_0",
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = community,
                DocumentName = documentName,
                Content = "ordinary",
                Version = 0,
                CreatedAt = Timestamp.FromDateTime(ordinaryCreatedAt.UtcDateTime)
            });
        await Fixture.Db.Collection("context-documents")
            .Document("newer-custom-publication-row")
            .SetAsync(new FirestoreContextDocument
            {
                Id = "newer-custom-publication-row",
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = community,
                PublicationSet = "custom-nonreserved-set",
                DocumentName = documentName,
                Content = "publication payload",
                Version = 99,
                CreatedAt = Timestamp.FromDateTime(publicationCreatedAt.UtcDateTime)
            });

        var repository = CreateRepository();
        var latest = await repository.GetLatestContextDocumentAsync(documentName, community);
        var timestamp = await repository.GetContextDocumentByTimestampAsync(documentName, publicationCreatedAt, community);
        var versions = await repository.GetContextDocumentVersionsAsync(documentName, community);

        await Assert.That(latest!.Content).IsEqualTo("ordinary");
        await Assert.That(timestamp!.Content).IsEqualTo("ordinary");
        await Assert.That(versions).HasCount().EqualTo(1);
        await Assert.That(versions.Single().Content).IsEqualTo("ordinary");
    }
}
