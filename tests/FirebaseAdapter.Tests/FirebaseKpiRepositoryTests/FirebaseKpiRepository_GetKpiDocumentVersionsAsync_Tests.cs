using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Tests for FirebaseKpiRepository.GetKpiDocumentVersionsAsync method.
/// </summary>
public class FirebaseKpiRepository_GetKpiDocumentVersionsAsync_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_versions_for_non_existent_document_returns_empty_list()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var versions = await repository.GetKpiDocumentVersionsAsync(
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

        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v1", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v2", "desc", "test-community");

        // Act
        var versions = await repository.GetKpiDocumentVersionsAsync(
            "test-kpi",
            "test-community");

        // Assert
        await Assert.That(versions).HasCount().EqualTo(3);
        await Assert.That(versions[0]).Member(v => v.Version, v => v.IsEqualTo(0))
            .And.Member(v => v.Content, c => c.IsEqualTo("v0"));
        await Assert.That(versions[1]).Member(v => v.Version, v => v.IsEqualTo(1))
            .And.Member(v => v.Content, c => c.IsEqualTo("v1"));
        await Assert.That(versions[2]).Member(v => v.Version, v => v.IsEqualTo(2))
            .And.Member(v => v.Content, c => c.IsEqualTo("v2"));
    }

    [Test]
    public async Task Getting_versions_filters_by_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "content-a", "desc", "community-a");
        await repository.SaveKpiDocumentAsync("test-kpi", "content-b", "desc", "community-b");

        // Act
        var versions = await repository.GetKpiDocumentVersionsAsync(
            "test-kpi",
            "community-a");

        // Assert
        await Assert.That(versions).HasCount().EqualTo(1);
        await Assert.That(versions[0].Content).IsEqualTo("content-a");
    }
}
