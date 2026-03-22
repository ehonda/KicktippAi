using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_Constructor_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Creating_repository_with_valid_dependencies_succeeds()
    {
        var repository = CreateRepository();

        await Assert.That(repository).IsNotNull();
    }

    [Test]
    public async Task Creating_repository_with_null_firestore_db_throws()
    {
        await Assert.That(() => CreateRepository(firestoreDb: NullableOption.Some<FirestoreDb>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("firestoreDb");
    }

    [Test]
    public async Task Creating_repository_with_null_logger_throws()
    {
        await Assert.That(() => CreateRepository(logger: NullableOption.Some<FakeLogger<FirebaseMatchOutcomeRepository>>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
