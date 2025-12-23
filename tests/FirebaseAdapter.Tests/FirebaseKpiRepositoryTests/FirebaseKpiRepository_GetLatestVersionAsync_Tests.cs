using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Tests for FirebaseKpiRepository.GetLatestVersionAsync method.
/// </summary>
public class FirebaseKpiRepository_GetLatestVersionAsync_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_latest_version_for_non_existent_document_returns_negative_one()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var version = await repository.GetLatestVersionAsync(
            "non-existent",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(-1);
    }

    [Test]
    public async Task Getting_latest_version_for_single_version_document_returns_zero()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "content", "desc", "test-community");

        // Act
        var version = await repository.GetLatestVersionAsync(
            "test-kpi",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(0);
    }

    [Test]
    public async Task Getting_latest_version_returns_highest_version_number()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v1", "desc", "test-community");
        await repository.SaveKpiDocumentAsync("test-kpi", "v2", "desc", "test-community");

        // Act
        var version = await repository.GetLatestVersionAsync(
            "test-kpi",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(2);
    }

    [Test]
    public async Task Getting_latest_version_filters_by_community()
    {
        // Arrange
        var repository = CreateRepository();

        // Save 3 versions in community-a
        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "community-a");
        await repository.SaveKpiDocumentAsync("test-kpi", "v1", "desc", "community-a");
        await repository.SaveKpiDocumentAsync("test-kpi", "v2", "desc", "community-a");

        // Save 1 version in community-b
        await repository.SaveKpiDocumentAsync("test-kpi", "v0", "desc", "community-b");

        // Act
        var versionA = await repository.GetLatestVersionAsync("test-kpi", "community-a");
        var versionB = await repository.GetLatestVersionAsync("test-kpi", "community-b");

        // Assert
        await Assert.That(versionA).IsEqualTo(2);
        await Assert.That(versionB).IsEqualTo(0);
    }
}
