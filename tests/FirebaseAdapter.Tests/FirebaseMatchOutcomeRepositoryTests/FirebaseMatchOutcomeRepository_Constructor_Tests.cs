using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_Constructor_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public void Constructor_throws_when_firestoreDb_is_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateRepository(firestoreDb: null));
    }

    [Test]
    public void Constructor_throws_when_logger_is_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateRepository(logger: (FakeLogger<FirebaseMatchOutcomeRepository>?)null));
    }
}
