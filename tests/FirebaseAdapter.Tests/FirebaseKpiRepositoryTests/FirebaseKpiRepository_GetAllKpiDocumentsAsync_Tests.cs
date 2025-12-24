using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Tests for FirebaseKpiRepository.GetAllKpiDocumentsAsync method.
/// </summary>
public class FirebaseKpiRepository_GetAllKpiDocumentsAsync_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_all_documents_from_empty_repository_returns_empty_list()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var documents = await repository.GetAllKpiDocumentsAsync("test-community");

        // Assert
        await Assert.That(documents).IsEmpty();
    }

    [Test]
    public async Task Getting_all_documents_returns_latest_version_of_each()
    {
        // Arrange
        var repository = CreateRepository();

        // Save multiple versions of document A
        await repository.SaveKpiDocumentAsync("doc-a", "a-v0", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("doc-a", "a-v1", "desc", "test-community");

        // Save single version of document B
        await repository.SaveKpiDocumentAsync("doc-b", "b-v0", "desc", "test-community");

        // Act
        var documents = await repository.GetAllKpiDocumentsAsync("test-community");

        // Assert
        await Assert.That(documents).HasCount().EqualTo(2);
        
        var docA = documents.FirstOrDefault(d => d.DocumentName == "doc-a");
        var docB = documents.FirstOrDefault(d => d.DocumentName == "doc-b");
        
        await Assert.That(docA).IsNotNull()
            .And.Member(d => d!.Content, c => c.IsEqualTo("a-v1"))
            .And.Member(d => d!.Version, v => v.IsEqualTo(1));
        
        await Assert.That(docB).IsNotNull()
            .And.Member(d => d!.Content, c => c.IsEqualTo("b-v0"))
            .And.Member(d => d!.Version, v => v.IsEqualTo(0));
    }

    [Test]
    public async Task Getting_all_documents_filters_by_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("doc-a", "content", "desc", "community-a");
        await repository.SaveKpiDocumentAsync("doc-b", "content", "desc", "community-b");

        // Act
        var documents = await repository.GetAllKpiDocumentsAsync("community-a");

        // Assert
        await Assert.That(documents).HasCount().EqualTo(1);
        await Assert.That(documents[0].DocumentName).IsEqualTo("doc-a");
    }
}
