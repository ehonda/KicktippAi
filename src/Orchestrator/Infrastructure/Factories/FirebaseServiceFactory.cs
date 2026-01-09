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
    public IPredictionRepository CreatePredictionRepository()
    {
        var logger = _loggerFactory.CreateLogger<FirebasePredictionRepository>();
        return new FirebasePredictionRepository(FirestoreDb, logger);
    }

    /// <inheritdoc />
    public IKpiRepository CreateKpiRepository()
    {
        var logger = _loggerFactory.CreateLogger<FirebaseKpiRepository>();
        return new FirebaseKpiRepository(FirestoreDb, logger);
    }

    /// <inheritdoc />
    public IContextRepository CreateContextRepository()
    {
        var logger = _loggerFactory.CreateLogger<FirebaseContextRepository>();
        return new FirebaseContextRepository(FirestoreDb, logger);
    }

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
