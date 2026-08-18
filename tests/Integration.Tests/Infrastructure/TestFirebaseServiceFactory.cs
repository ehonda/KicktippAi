using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Infrastructure.Factories;

namespace Integration.Tests.Infrastructure;

internal sealed class TestFirebaseServiceFactory(FirestoreDb firestoreDb) : IFirebaseServiceFactory
{
    public FirestoreDb FirestoreDb { get; } = firestoreDb;

    public IPredictionRepository CreatePredictionRepository(string competition)
    {
        return new FirebasePredictionRepository(FirestoreDb, new FakeLogger<FirebasePredictionRepository>(), competition);
    }

    public IKpiRepository CreateKpiRepository(string competition)
    {
        return new FirebaseKpiRepository(FirestoreDb, new FakeLogger<FirebaseKpiRepository>(), competition);
    }

    public IContextRepository CreateContextRepository(string competition)
    {
        return new FirebaseContextRepository(FirestoreDb, new FakeLogger<FirebaseContextRepository>(), competition);
    }

    public IDocumentPublicationRepository CreateDocumentPublicationRepository(string competition)
    {
        return new FirebaseDocumentPublicationRepository(
            FirestoreDb,
            new FakeLogger<FirebaseDocumentPublicationRepository>(),
            competition);
    }

    public IMatchOutcomeRepository CreateMatchOutcomeRepository(string competition)
    {
        return new FirebaseMatchOutcomeRepository(FirestoreDb, new FakeLogger<FirebaseMatchOutcomeRepository>(), competition);
    }
}
