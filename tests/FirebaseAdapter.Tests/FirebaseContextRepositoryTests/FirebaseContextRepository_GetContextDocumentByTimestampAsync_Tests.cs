using Google.Cloud.Firestore;
using TUnit.Core;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

public class FirebaseContextRepository_GetContextDocumentByTimestampAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_document_by_exact_timestamp_returns_matching_version()
    {
        var repository = CreateRepository();
        var versionOneTimestamp = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        await repository.SaveContextDocumentAsync("test-document", "version 0 content", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "version 1 content", "test-community");
        await SetCreatedAtAsync("test-document", 0, "test-community", versionOneTimestamp.AddHours(-1));
        await SetCreatedAtAsync("test-document", 1, "test-community", versionOneTimestamp);

        var result = await repository.GetContextDocumentByTimestampAsync(
            "test-document",
            versionOneTimestamp,
            "test-community");

        await Assert.That(result).IsNotNull()
            .And.Member(document => document!.Version, version => version.IsEqualTo(1))
            .And.Member(document => document!.Content, content => content.IsEqualTo("version 1 content"));
    }

    [Test]
    public async Task Getting_document_by_timestamp_between_versions_returns_latest_earlier_version()
    {
        var repository = CreateRepository();
        var firstTimestamp = new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var secondTimestamp = firstTimestamp.AddHours(2);

        await repository.SaveContextDocumentAsync("test-document", "version 0 content", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "version 1 content", "test-community");
        await SetCreatedAtAsync("test-document", 0, "test-community", firstTimestamp);
        await SetCreatedAtAsync("test-document", 1, "test-community", secondTimestamp);

        var result = await repository.GetContextDocumentByTimestampAsync(
            "test-document",
            firstTimestamp.AddMinutes(30),
            "test-community");

        await Assert.That(result).IsNotNull()
            .And.Member(document => document!.Version, version => version.IsEqualTo(0))
            .And.Member(document => document!.Content, content => content.IsEqualTo("version 0 content"));
    }

    [Test]
    public async Task Getting_document_by_timestamp_before_first_version_returns_null()
    {
        var repository = CreateRepository();
        var firstTimestamp = new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

        await repository.SaveContextDocumentAsync("test-document", "version 0 content", "test-community");
        await SetCreatedAtAsync("test-document", 0, "test-community", firstTimestamp);

        var result = await repository.GetContextDocumentByTimestampAsync(
            "test-document",
            firstTimestamp.AddMinutes(-1),
            "test-community");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_document_by_timestamp_from_different_community_returns_null()
    {
        var repository = CreateRepository();
        var timestamp = new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

        await repository.SaveContextDocumentAsync("test-document", "content", "community-a");
        await SetCreatedAtAsync("test-document", 0, "community-a", timestamp);

        var result = await repository.GetContextDocumentByTimestampAsync(
            "test-document",
            timestamp,
            "community-b");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_document_by_timestamp_with_equal_createdAt_prefers_higher_version()
    {
        var repository = CreateRepository();
        var timestamp = new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

        await repository.SaveContextDocumentAsync("test-document", "version 0 content", "test-community");
        await repository.SaveContextDocumentAsync("test-document", "version 1 content", "test-community");
        await SetCreatedAtAsync("test-document", 0, "test-community", timestamp);
        await SetCreatedAtAsync("test-document", 1, "test-community", timestamp);

        var result = await repository.GetContextDocumentByTimestampAsync(
            "test-document",
            timestamp,
            "test-community");

        await Assert.That(result).IsNotNull()
            .And.Member(document => document!.Version, version => version.IsEqualTo(1))
            .And.Member(document => document!.Content, content => content.IsEqualTo("version 1 content"));
    }

    private async Task SetCreatedAtAsync(string documentName, int version, string communityContext, DateTimeOffset createdAt)
    {
        var documentId = $"{documentName}_{communityContext}_{version}";
        var documentReference = Fixture.Db.Collection("context-documents").Document(documentId);
        await documentReference.UpdateAsync("createdAt", Timestamp.FromDateTime(createdAt.UtcDateTime));
    }
}
