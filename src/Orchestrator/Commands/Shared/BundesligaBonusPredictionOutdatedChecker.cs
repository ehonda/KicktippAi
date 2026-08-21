using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Shared;

/// <summary>
/// Compares persisted Bundesliga bonus provenance with the exact current semantic publication heads.
/// </summary>
public static class BundesligaBonusPredictionOutdatedChecker
{
    public static async Task<bool> IsOutdatedAsync(
        IDocumentPublicationRepository publicationRepository,
        BonusQuestion question,
        string communityContext,
        BonusPredictionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publicationRepository);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        ArgumentNullException.ThrowIfNull(metadata);

        var manifest = metadata.ResolvedContextManifest
            ?? throw new InvalidDataException(
                "Bundesliga bonus prediction is missing its immutable resolved bonus-context manifest.");
        ResolvedBonusContextManifest.ValidateForCommunity(manifest, communityContext);
        if (!string.Equals(
                publicationRepository.Competition,
                CompetitionIds.Bundesliga2026_27,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Bundesliga bonus outdated checks require the canonical publication repository.");
        }

        var rosters = await publicationRepository.GetLastKnownGoodAsync(
                          BundesligaDocumentPublication.Rosters,
                          communityContext,
                          cancellationToken)
                      ?? throw new InvalidDataException(
                          "Current Bundesliga roster publication head is missing.");
        var elo = await publicationRepository.GetLastKnownGoodAsync(
                      BundesligaDocumentPublication.ClubElo,
                      communityContext,
                      cancellationToken)
                  ?? throw new InvalidDataException(
                      "Current Bundesliga Club Elo publication head is missing.");

        var reconstructedRosters = BundesligaRosterPublication.ReconstructLastKnownGood(rosters);
        _ = BundesligaClubEloPublication.ReconstructLastKnownGood(elo);
        var selection = BonusContextSelectionPolicy.SelectBundesliga(question, reconstructedRosters);
        var selectedKeys = selection.RequiredDocuments;
        if (!manifest.Documents.Select(document => new DocumentPublicationKey(
                    Enum.Parse<DocumentPublicationKind>(document.Kind, ignoreCase: false),
                    document.Name))
                .SequenceEqual(selectedKeys))
        {
            return true;
        }

        if (!string.Equals(
                rosters.Snapshot.SnapshotId,
                manifest.RosterPublicationSnapshotId,
                StringComparison.Ordinal)
            || !string.Equals(
                elo.Snapshot.SnapshotId,
                manifest.ClubEloPublicationSnapshotId,
                StringComparison.Ordinal))
        {
            return true;
        }

        var currentByKey = rosters.Documents.Concat(elo.Documents).ToDictionary(document => document.Key);
        foreach (var entry in manifest.Documents)
        {
            var key = new DocumentPublicationKey(
                Enum.Parse<DocumentPublicationKind>(entry.Kind, ignoreCase: false),
                entry.Name);
            if (!currentByKey.TryGetValue(key, out var current)
                || current.Version != entry.Version
                || !string.Equals(
                    DocumentPublicationContract.ComputeContentSha256(current.Content),
                    entry.ContentSha256,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
