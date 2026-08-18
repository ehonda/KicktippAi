using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
public sealed class FirebaseRepositoryCompetitionGuardTests(FirestoreFixture fixture)
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Repository_constructors_reject_missing_competition(string? competition)
    {
        var db = fixture.Db;

        await Assert.That(() => new FirebasePredictionRepository(
                db,
                new FakeLogger<FirebasePredictionRepository>(),
                competition!))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
        await Assert.That(() => new FirebaseContextRepository(
                db,
                new FakeLogger<FirebaseContextRepository>(),
                competition!))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
        await Assert.That(() => new FirebaseKpiRepository(
                db,
                new FakeLogger<FirebaseKpiRepository>(),
                competition!))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
        await Assert.That(() => new FirebaseMatchOutcomeRepository(
                db,
                new FakeLogger<FirebaseMatchOutcomeRepository>(),
                competition!))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
        await Assert.That(() => new FirebaseDocumentPublicationRepository(
                db,
                new FakeLogger<FirebaseDocumentPublicationRepository>(),
                competition!))
            .Throws<ArgumentException>()
            .WithParameterName("competition");
    }
}
