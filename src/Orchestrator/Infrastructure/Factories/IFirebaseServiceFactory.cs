using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Factory for creating Firebase-related services.
/// </summary>
/// <remarks>
/// The factory initializes <see cref="FirestoreDb"/> from environment variables
/// on construction and creates repositories for an explicit competition.
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
    IPredictionRepository CreatePredictionRepository(string competition);

    /// <summary>
    /// Creates a KPI repository instance.
    /// </summary>
    /// <returns>A KPI repository instance.</returns>
    IKpiRepository CreateKpiRepository(string competition);

    /// <summary>
    /// Creates a context repository instance.
    /// </summary>
    /// <returns>A context repository instance.</returns>
    IContextRepository CreateContextRepository(string competition);

    /// <summary>Creates the atomic mixed context/KPI publication repository.</summary>
    IDocumentPublicationRepository CreateDocumentPublicationRepository(string competition);

    /// <summary>
    /// Creates a match outcome repository instance.
    /// </summary>
    /// <returns>A match outcome repository instance.</returns>
    IMatchOutcomeRepository CreateMatchOutcomeRepository(string competition);
}
