using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for append-only context document versions.
/// </summary>
public class FirebaseContextRepository_UpdateContextDocumentVersionAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Changed_content_creates_a_new_version_without_changing_the_historical_version()
    {
        var repository = CreateRepository();
        await repository.SaveContextDocumentAsync(
            "test-document",
            "original content",
            "test-community");
        var newVersion = await repository.SaveContextDocumentAsync(
            "test-document",
            "updated content",
            "test-community");

        var original = await repository.GetContextDocumentAsync("test-document", 0, "test-community");
        var updated = await repository.GetContextDocumentAsync("test-document", 1, "test-community");

        await Assert.That(newVersion).IsEqualTo(1);
        await Assert.That(original!.Content).IsEqualTo("original content");
        await Assert.That(updated!.Content).IsEqualTo("updated content");
    }
}
