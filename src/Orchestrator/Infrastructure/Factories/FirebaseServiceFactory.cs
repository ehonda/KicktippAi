using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Default implementation of <see cref="IFirebaseServiceFactory"/>.
/// </summary>
/// <remarks>
/// Initializes <see cref="FirestoreDb"/> from environment variables on construction.
/// Throws if required environment variables are not set.
/// </remarks>
public sealed class FirebaseServiceFactory : IFirebaseServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<FirestoreDb> _firestoreDb;

    public FirebaseServiceFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _firestoreDb = new Lazy<FirestoreDb>(InitializeFirestoreDb);
    }

    /// <inheritdoc />
    public FirestoreDb FirestoreDb => _firestoreDb.Value;

    /// <inheritdoc />
    public IPredictionRepository CreatePredictionRepository(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var logger = _loggerFactory.CreateLogger<FirebasePredictionRepository>();
        return new FirebasePredictionRepository(FirestoreDb, logger, competition);
    }

    /// <inheritdoc />
    public IKpiRepository CreateKpiRepository(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var logger = _loggerFactory.CreateLogger<FirebaseKpiRepository>();
        return new FirebaseKpiRepository(FirestoreDb, logger, competition);
    }

    /// <inheritdoc />
    public IContextRepository CreateContextRepository(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var logger = _loggerFactory.CreateLogger<FirebaseContextRepository>();
        return new FirebaseContextRepository(FirestoreDb, logger, competition);
    }

    /// <inheritdoc />
    public IHistoricalExperimentContextReader CreateBundesliga2025_26HistoricalExperimentContextReader()
    {
        var logger = _loggerFactory.CreateLogger<Bundesliga2025_26HistoricalExperimentContextReader>();
        return new Bundesliga2025_26HistoricalExperimentContextReader(FirestoreDb, logger);
    }

    /// <inheritdoc />
    public IHistoricalExperimentFixtureReader CreateBundesliga2025_26HistoricalExperimentFixtureReader()
    {
        var logger = _loggerFactory.CreateLogger<Bundesliga2025_26HistoricalExperimentFixtureReader>();
        return new Bundesliga2025_26HistoricalExperimentFixtureReader(FirestoreDb, logger);
    }

    /// <inheritdoc />
    public IDocumentPublicationRepository CreateDocumentPublicationRepository(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var logger = _loggerFactory.CreateLogger<FirebaseDocumentPublicationRepository>();
        return new FirebaseDocumentPublicationRepository(FirestoreDb, logger, competition);
    }

    /// <inheritdoc />
    public IResolvedTypedContextPublicationBindingRepository CreateResolvedTypedContextPublicationBindingRepository()
    {
        var logger = _loggerFactory.CreateLogger<FirebaseResolvedTypedContextPublicationBindingRepository>();
        return new FirebaseResolvedTypedContextPublicationBindingRepository(FirestoreDb, logger);
    }

    /// <inheritdoc />
    public IMatchOutcomeRepository CreateMatchOutcomeRepository(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var logger = _loggerFactory.CreateLogger<FirebaseMatchOutcomeRepository>();
        return new FirebaseMatchOutcomeRepository(FirestoreDb, logger, competition);
    }

    /// <inheritdoc />
    public IBundesligaTypedPredictionAuthorityRepository CreateBundesligaTypedPredictionAuthorityRepository() =>
        new FirebaseBundesligaTypedPredictionAuthorityRepository(
            FirestoreDb,
            FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch);

    /// <inheritdoc />
    public ILegacyFirebaseMatchPredictionAuditCostReader CreateLegacyBundesligaMatchAuditCostReader() =>
        new FirebaseLegacyMatchPredictionAuditCostReader(FirestoreDb);

    /// <inheritdoc />
    public ILegacyFirebaseBonusPredictionAuditCostReader CreateLegacyBundesligaBonusAuditCostReader() =>
        new FirebaseLegacyBonusPredictionAuditCostReader(FirestoreDb);

    /// <inheritdoc />
    public ITypedFirebaseMatchPredictionAuditCostReader CreateTypedBundesligaMatchAuditCostReader() =>
        new FirebaseTypedMatchPredictionAuditCostReader(FirestoreDb);

    /// <inheritdoc />
    public ITypedFirebaseBonusPredictionAuditCostReader CreateTypedBundesligaBonusAuditCostReader() =>
        new FirebaseTypedBonusPredictionAuditCostReader(FirestoreDb);

    private FirestoreDb InitializeFirestoreDb()
    {
        var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var serviceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");

        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("FIREBASE_PROJECT_ID environment variable is required");
        }

        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            throw new InvalidOperationException("FIREBASE_SERVICE_ACCOUNT_JSON environment variable is required");
        }

        var logger = _loggerFactory.CreateLogger<FirebaseServiceFactory>();
        logger.LogInformation("Initializing Firebase Firestore for project: {ProjectId}", projectId);

        var firestoreDbBuilder = new FirestoreDbBuilder
        {
            ProjectId = projectId,
            JsonCredentials = serviceAccountJson
        };

        return firestoreDbBuilder.Build();
    }
}
