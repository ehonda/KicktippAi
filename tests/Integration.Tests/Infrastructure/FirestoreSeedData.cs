using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using NodaTime;

namespace Integration.Tests.Infrastructure;

internal static class FirestoreSeedData
{
    private const string Competition = "bundesliga-2025-26";

    public static async Task SeedMatchPredictionAsync(
        FirestoreDb firestoreDb,
        Match match,
        Prediction prediction,
        string model,
        string communityContext,
        IReadOnlyList<string> contextDocumentNames,
        int repredictionIndex,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? documentId = null)
    {
        var docRef = firestoreDb.Collection("match-predictions")
            .Document(documentId ?? Guid.NewGuid().ToString());

        var firestorePrediction = new FirestoreMatchPrediction
        {
            Id = docRef.Id,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            StartsAt = ToTimestamp(match.StartsAt),
            Matchday = match.Matchday,
            HomeGoals = prediction.HomeGoals,
            AwayGoals = prediction.AwayGoals,
            Justification = null,
            CreatedAt = Timestamp.FromDateTimeOffset(createdAt),
            UpdatedAt = Timestamp.FromDateTimeOffset(updatedAt),
            Competition = Competition,
            Model = model,
            TokenUsage = "{}",
            Cost = 0.01,
            CommunityContext = communityContext,
            ContextDocumentNames = contextDocumentNames.ToArray(),
            RepredictionIndex = repredictionIndex
        };

        await docRef.SetAsync(firestorePrediction);
    }

    public static async Task SeedContextDocumentAsync(
        FirestoreDb firestoreDb,
        string documentName,
        string communityContext,
        int version,
        DateTimeOffset createdAt,
        string? content = null)
    {
        var documentId = $"{documentName}_{communityContext}_{version}";
        var docRef = firestoreDb.Collection("context-documents").Document(documentId);

        var firestoreDocument = new FirestoreContextDocument
        {
            Id = documentId,
            DocumentName = documentName,
            Content = content ?? "test content",
            Version = version,
            CreatedAt = Timestamp.FromDateTimeOffset(createdAt),
            Competition = Competition,
            CommunityContext = communityContext
        };

        await docRef.SetAsync(firestoreDocument);
    }

    public static async Task<IReadOnlyList<FirestoreMatchPrediction>> GetMatchPredictionsAsync(
        FirestoreDb firestoreDb,
        Match match,
        string model,
        string communityContext)
    {
        var snapshot = await firestoreDb.Collection("match-predictions")
            .WhereEqualTo("homeTeam", match.HomeTeam)
            .WhereEqualTo("awayTeam", match.AwayTeam)
            .WhereEqualTo("startsAt", ToTimestamp(match.StartsAt))
            .WhereEqualTo("competition", Competition)
            .WhereEqualTo("model", model)
            .WhereEqualTo("communityContext", communityContext)
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
            .OrderBy(prediction => prediction.RepredictionIndex)
            .ToList()
            .AsReadOnly();
    }

    private static Timestamp ToTimestamp(ZonedDateTime startsAt)
    {
        return Timestamp.FromDateTime(startsAt.ToInstant().ToDateTimeUtc());
    }
}
