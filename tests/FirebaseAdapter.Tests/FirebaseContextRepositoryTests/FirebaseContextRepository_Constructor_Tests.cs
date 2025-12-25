using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Tests for FirebaseContextRepository constructor validation.
/// </summary>
public class FirebaseContextRepository_Constructor_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public void Constructor_throws_when_firestoreDb_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CreateRepository(firestoreDb: null));
    }

    [Test]
    public void Constructor_throws_when_logger_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CreateRepository(logger: (FakeLogger<FirebaseContextRepository>?)null));
    }
}
