using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Tests for FirebaseKpiRepository.GetKpiDocumentAsync methods.
/// </summary>
public class FirebaseKpiRepository_GetKpiDocumentAsync_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_non_existent_document_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetKpiDocumentAsync(
            "non-existent",
            "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_latest_document_returns_most_recent_version()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v1", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v2", "desc", "test-community");

        // Act
        var result = await repository.GetKpiDocumentAsync(
            "test-kpi",
            "test-community");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Version).IsEqualTo(2);
        await Assert.That(result.Content).IsEqualTo("v2");
    }

    [Test]
    public async Task Getting_specific_version_returns_correct_document()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v1", "desc", "test-community");

        // Act
        var version0 = await repository.GetKpiDocumentAsync("test-kpi", "test-community", version: 0);
        var version1 = await repository.GetKpiDocumentAsync("test-kpi", "test-community", version: 1);

        // Assert
        await Assert.That(version0!.Content).IsEqualTo("v0");
        await Assert.That(version1!.Content).IsEqualTo("v1");
    }

    [Test]
    public async Task Getting_non_existent_version_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "content", "desc", "test-community");

        // Act
        var result = await repository.GetKpiDocumentAsync(
            "test-kpi",
            "test-community",
            version: 99);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Getting_document_from_different_community_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "content", "desc", "community-a");

        // Act
        var result = await repository.GetKpiDocumentAsync(
            "test-kpi",
            "community-b");

        // Assert
        await Assert.That(result).IsNull();
    }
}
