using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Infrastructure.Factories;

namespace Integration.Tests.Infrastructure;

internal sealed class TestFirebaseServiceFactory(FirestoreDb firestoreDb) : IFirebaseServiceFactory
{
    public FirestoreDb FirestoreDb { get; } = firestoreDb;

    public IPredictionRepository CreatePredictionRepository(string? competition = null)
    {
        return new FirebasePredictionRepository(FirestoreDb, new FakeLogger<FirebasePredictionRepository>(), competition);
    }

    public IKpiRepository CreateKpiRepository(string? competition = null)
    {
        return new FirebaseKpiRepository(FirestoreDb, new FakeLogger<FirebaseKpiRepository>(), competition);
    }

    public IContextRepository CreateContextRepository(string? competition = null)
    {
        return new FirebaseContextRepository(FirestoreDb, new FakeLogger<FirebaseContextRepository>(), competition);
    }

    public IMatchOutcomeRepository CreateMatchOutcomeRepository(string? competition = null)
    {
        return new FirebaseMatchOutcomeRepository(FirestoreDb, new FakeLogger<FirebaseMatchOutcomeRepository>(), competition);
    }
}
