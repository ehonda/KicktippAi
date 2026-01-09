using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Factory for creating Firebase-related services.
/// </summary>
/// <remarks>
/// The factory initializes <see cref="FirestoreDb"/> from environment variables
/// on construction and provides parameterless methods to create repositories.
/// </remarks>
public interface IFirebaseServiceFactory
{
    /// <summary>
    /// Gets the initialized Firestore database instance.
    /// </summary>
    FirestoreDb FirestoreDb { get; }

    /// <summary>
    /// Creates a prediction repository instance.
    /// </summary>
    /// <returns>A prediction repository instance.</returns>
    IPredictionRepository CreatePredictionRepository();

    /// <summary>
    /// Creates a KPI repository instance.
    /// </summary>
    /// <returns>A KPI repository instance.</returns>
    IKpiRepository CreateKpiRepository();

    /// <summary>
    /// Creates a context repository instance.
    /// </summary>
    /// <returns>A context repository instance.</returns>
    IContextRepository CreateContextRepository();
}
