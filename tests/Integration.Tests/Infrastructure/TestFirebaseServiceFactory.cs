using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Infrastructure.Factories;

namespace Integration.Tests.Infrastructure;

internal sealed class TestFirebaseServiceFactory(FirestoreDb firestoreDb) : IFirebaseServiceFactory
{
    public FirestoreDb FirestoreDb { get; } = firestoreDb;

    public IPredictionRepository CreatePredictionRepository()
    {
        return new FirebasePredictionRepository(FirestoreDb, new FakeLogger<FirebasePredictionRepository>());
    }

    public IKpiRepository CreateKpiRepository()
    {
        return new FirebaseKpiRepository(FirestoreDb, new FakeLogger<FirebaseKpiRepository>());
    }

    public IContextRepository CreateContextRepository()
    {
        return new FirebaseContextRepository(FirestoreDb, new FakeLogger<FirebaseContextRepository>());
    }

    public IMatchOutcomeRepository CreateMatchOutcomeRepository()
    {
        return new FirebaseMatchOutcomeRepository(FirestoreDb, new FakeLogger<FirebaseMatchOutcomeRepository>());
    }
}
