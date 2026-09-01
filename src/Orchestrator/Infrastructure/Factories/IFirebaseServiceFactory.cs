using EHonda.KicktippAi.Core;
using FirebaseAdapter;
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

    /// <summary>Creates the read-only legacy-ID adapter for Bundesliga 2025/26 experiments.</summary>
    IHistoricalExperimentContextReader CreateBundesliga2025_26HistoricalExperimentContextReader();

    /// <summary>Creates the read-only legacy-ID completed-fixture adapter for Bundesliga 2025/26 experiments.</summary>
    IHistoricalExperimentFixtureReader CreateBundesliga2025_26HistoricalExperimentFixtureReader();

    /// <summary>Creates the atomic mixed context/KPI publication repository.</summary>
    IDocumentPublicationRepository CreateDocumentPublicationRepository(string competition);

    /// <summary>Creates the directly addressed current resolved-typed-context publication binding repository.</summary>
    IResolvedTypedContextPublicationBindingRepository CreateResolvedTypedContextPublicationBindingRepository();

    /// <summary>
    /// Creates a match outcome repository instance.
    /// </summary>
    /// <returns>A match outcome repository instance.</returns>
    IMatchOutcomeRepository CreateMatchOutcomeRepository(string competition);

    /// <summary>Creates the fixed Bundesliga typed-v1 current authority repository.</summary>
    IBundesligaTypedPredictionAuthorityRepository CreateBundesligaTypedPredictionAuthorityRepository();

    ILegacyFirebaseMatchPredictionAuditCostReader CreateLegacyBundesligaMatchAuditCostReader();
    ILegacyFirebaseBonusPredictionAuditCostReader CreateLegacyBundesligaBonusAuditCostReader();
    ITypedFirebaseMatchPredictionAuditCostReader CreateTypedBundesligaMatchAuditCostReader();
    ITypedFirebaseBonusPredictionAuditCostReader CreateTypedBundesligaBonusAuditCostReader();
}
