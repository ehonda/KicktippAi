using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Read-only adapter for the canonical Bundesliga 2025/26 context rows whose Firestore document
/// identities predate competition-prefixed IDs. It is intentionally unavailable through
/// <see cref="IContextRepository"/> so live/runtime validation cannot be relaxed accidentally.
/// </summary>
public sealed class Bundesliga2025_26HistoricalExperimentContextReader : IHistoricalExperimentContextReader
{
    private const string ContextDocumentsCollection = "context-documents";
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<Bundesliga2025_26HistoricalExperimentContextReader> _logger;

    public Bundesliga2025_26HistoricalExperimentContextReader(
        FirestoreDb firestoreDb,
        ILogger<Bundesliga2025_26HistoricalExperimentContextReader> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ContextDocument?> GetContextDocumentAtOrBeforeAsync(
        string documentName,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(documentName, communityContext);
        try
        {
            var query = _firestoreDb.Collection(ContextDocumentsCollection)
                .WhereEqualTo("documentName", documentName)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", CompetitionIds.Bundesliga2025_26)
                .WhereLessThanOrEqualTo("createdAt", Timestamp.FromDateTime(evaluationTimestamp.UtcDateTime))
                .OrderByDescending("createdAt")
                .OrderByDescending("version");
            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            foreach (var document in snapshot.Documents)
            {
                var value = document.ConvertTo<FirestoreContextDocument>();
                if (string.IsNullOrEmpty(value.PublicationSet))
                {
                    return ValidateDocument(
                        document,
                        value,
                        documentName,
                        communityContext,
                        expectedVersion: null,
                        evaluationTimestamp);
                }
            }

            return null;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to read historical experiment context {DocumentName} at {EvaluationTimestamp} for {CommunityContext}",
                documentName,
                evaluationTimestamp,
                communityContext);
            throw;
        }
    }

    public async Task<ContextDocument?> GetContextDocumentAsync(
        string documentName,
        int version,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(documentName, communityContext);
        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Historical context version must be nonnegative.");
        }

        try
        {
            var documentId = ResolvedHistoricalExperimentContextManifest.BuildLegacyDocumentId(
                documentName,
                communityContext,
                version);
            var snapshot = await _firestoreDb.Collection(ContextDocumentsCollection)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);
            return snapshot.Exists
                ? ValidateDocument(
                    snapshot,
                    snapshot.ConvertTo<FirestoreContextDocument>(),
                    documentName,
                    communityContext,
                    version,
                    evaluationTimestamp: null)
                : null;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to read historical experiment context {DocumentName} v{Version} for {CommunityContext}",
                documentName,
                version,
                communityContext);
            throw;
        }
    }

    private static ContextDocument ValidateDocument(
        DocumentSnapshot snapshot,
        FirestoreContextDocument value,
        string documentName,
        string communityContext,
        int? expectedVersion,
        DateTimeOffset? evaluationTimestamp)
    {
        var createdAt = value.CreatedAt.ToDateTimeOffset();
        if (!string.Equals(
                snapshot.Id,
                ResolvedHistoricalExperimentContextManifest.BuildLegacyDocumentId(documentName, communityContext, value.Version),
                StringComparison.Ordinal)
            || !string.Equals(value.Competition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
            || !string.Equals(value.CommunityContext, communityContext, StringComparison.Ordinal)
            || !string.Equals(value.DocumentName, documentName, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(value.PublicationSet)
            || expectedVersion is not null && value.Version != expectedVersion.Value
            || evaluationTimestamp is not null && createdAt > evaluationTimestamp.Value)
        {
            throw new InvalidDataException("Historical experiment context scope or exact legacy identity is corrupt.");
        }

        return new ContextDocument(value.DocumentName, value.Content, value.Version, createdAt);
    }

    private static void ValidateArguments(string documentName, string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
    }
}
