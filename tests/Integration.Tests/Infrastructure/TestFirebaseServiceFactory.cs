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

    public IHistoricalExperimentContextReader CreateBundesliga2025_26HistoricalExperimentContextReader()
    {
        return new Bundesliga2025_26HistoricalExperimentContextReader(
            FirestoreDb,
            new FakeLogger<Bundesliga2025_26HistoricalExperimentContextReader>());
    }

    public IHistoricalExperimentFixtureReader CreateBundesliga2025_26HistoricalExperimentFixtureReader()
    {
        return new Bundesliga2025_26HistoricalExperimentFixtureReader(
            FirestoreDb,
            new FakeLogger<Bundesliga2025_26HistoricalExperimentFixtureReader>());
    }

    public IDocumentPublicationRepository CreateDocumentPublicationRepository(string competition)
    {
        return new FirebaseDocumentPublicationRepository(
            FirestoreDb,
            new FakeLogger<FirebaseDocumentPublicationRepository>(),
            competition);
    }

    public IResolvedTypedContextPublicationBindingRepository CreateResolvedTypedContextPublicationBindingRepository()
    {
        return new FirebaseResolvedTypedContextPublicationBindingRepository(
            FirestoreDb,
            new FakeLogger<FirebaseResolvedTypedContextPublicationBindingRepository>());
    }

    public IMatchOutcomeRepository CreateMatchOutcomeRepository(string competition)
    {
        return new FirebaseMatchOutcomeRepository(FirestoreDb, new FakeLogger<FirebaseMatchOutcomeRepository>(), competition);
    }
}
