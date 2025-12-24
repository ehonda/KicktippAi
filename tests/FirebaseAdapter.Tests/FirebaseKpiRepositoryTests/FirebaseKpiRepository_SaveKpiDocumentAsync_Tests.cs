using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Tests for FirebaseKpiRepository.SaveKpiDocumentAsync method.
/// </summary>
public class FirebaseKpiRepository_SaveKpiDocumentAsync_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_new_document_returns_version_zero()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var version = await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "test content",
            "test description",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(0);
    }

    [Test]
    public async Task Saving_document_with_changed_content_increments_version()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "original content",
            "description",
            "test-community");

        // Act
        var version = await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "updated content",
            "description",
            "test-community");

        // Assert
        await Assert.That(version).IsEqualTo(1);
    }

    [Test]
    public async Task Saving_document_with_same_content_returns_same_version()
    {
        // Arrange
        var repository = CreateRepository();

        var firstVersion = await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "same content",
            "description",
            "test-community");

        // Act
        var secondVersion = await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "same content",
            "description",
            "test-community");

        // Assert
        await Assert.That(firstVersion).IsEqualTo(0);
        await Assert.That(secondVersion).IsEqualTo(0);
    }

    [Test]
    public async Task Saved_document_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        await repository.SaveKpiDocumentAsync(
            "test-kpi",
            "test content",
            "test description",
            "test-community");

        var retrieved = await repository.GetKpiDocumentAsync(
            "test-kpi",
            "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull()
            .And.Member(r => r!.DocumentName, n => n.IsEqualTo("test-kpi"))
            .And.Member(r => r!.Content, c => c.IsEqualTo("test content"))
            .And.Member(r => r!.Description, d => d.IsEqualTo("test description"))
            .And.Member(r => r!.Version, v => v.IsEqualTo(0));
    }
}
