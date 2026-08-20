using TestUtilities;
using TUnit.Core;
using FirebaseAdapter.Models;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository.GetContextDocumentNamesAsync method.
/// </summary>
public class FirebaseContextRepository_GetContextDocumentNamesAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_names_from_empty_repository_returns_empty_list()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var names = await repository.GetContextDocumentNamesAsync("test-community");

        // Assert
        await Assert.That(names).IsEmpty();
    }

    [Test]
    public async Task Getting_names_returns_all_distinct_document_names()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("doc-a", "content", "test-community");
        await repository.SaveContextDocumentAsync("doc-b", "content", "test-community");
        await repository.SaveContextDocumentAsync("doc-c", "content", "test-community");

        // Act
        var names = await repository.GetContextDocumentNamesAsync("test-community");

        // Assert
        await Assert.That(names).HasCount().EqualTo(3)
            .And.Contains("doc-a")
            .And.Contains("doc-b")
            .And.Contains("doc-c");
    }

    [Test]
    public async Task Getting_names_returns_distinct_names_when_multiple_versions_exist()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("doc-a", "v0", "test-community");
        await repository.SaveContextDocumentAsync("doc-a", "v1", "test-community");
        await repository.SaveContextDocumentAsync("doc-a", "v2", "test-community");

        // Act
        var names = await repository.GetContextDocumentNamesAsync("test-community");

        // Assert
        await Assert.That(names).HasCount().EqualTo(1)
            .And.Contains("doc-a");
    }

    [Test]
    public async Task Getting_names_filters_by_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync("doc-a", "content", "community-a");
        await repository.SaveContextDocumentAsync("doc-b", "content", "community-b");

        // Act
        var names = await repository.GetContextDocumentNamesAsync("community-a");

        // Assert
        await Assert.That(names).HasCount().EqualTo(1)
            .And.Contains("doc-a");
    }

    [Test]
    public async Task Getting_names_excludes_publication_rows_but_keeps_missing_and_empty_legacy_scope()
    {
        const string community = "test-community";
        await Fixture.Db.Collection("context-documents").Document("missing-publication-set").SetAsync(new Dictionary<string, object>
        {
            ["competition"] = CompetitionIds.Bundesliga2026_27,
            ["communityContext"] = community,
            ["documentName"] = "historical-missing.csv",
            ["content"] = "historical",
            ["version"] = 0,
            ["createdAt"] = Timestamp.GetCurrentTimestamp()
        });
        await Fixture.Db.Collection("context-documents").Document("empty-publication-set").SetAsync(new FirestoreContextDocument
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = community,
            PublicationSet = string.Empty,
            DocumentName = "ordinary-empty.csv",
            Content = "ordinary",
            Version = 1,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });
        await Fixture.Db.Collection("context-documents").Document("publication-row").SetAsync(new FirestoreContextDocument
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = community,
            PublicationSet = "custom-publication",
            DocumentName = "custom-published.csv",
            Content = "publication",
            Version = 2,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });

        var names = await CreateRepository().GetContextDocumentNamesAsync(community);

        await Assert.That(names).IsEquivalentTo(["historical-missing.csv", "ordinary-empty.csv"]);
    }
}
