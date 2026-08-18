using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Shared;

/// <summary>Compares Bundesliga prompt provenance with the one current publication head for each reserved set.</summary>
public static class BundesligaPredictionOutdatedChecker
{
    public static async Task<bool> IsOutdatedAsync(
        IContextRepository contextRepository,
        IDocumentPublicationRepository publicationRepository,
        Match match,
        string communityContext,
        PredictionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var manifest = metadata.ResolvedContextManifest
            ?? throw new InvalidDataException("Bundesliga prediction is missing its immutable resolved-context manifest.");
        ResolvedMatchContextManifest.ValidateForMatch(manifest, match, communityContext);
        if (!string.Equals(publicationRepository.Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bundesliga outdated checks require the canonical publication repository.");
        }

        // The seven ordinary documents use exact versions. Neither timestamps nor payload equality can
        // turn a later version into the version that actually entered the prompt.
        foreach (var entry in manifest.Documents.Where(entry => !IsReserved(entry.Name)))
        {
            var latest = await contextRepository.GetLatestContextDocumentAsync(entry.Name, communityContext, cancellationToken);
            if (latest is null || !string.Equals(latest.DocumentName, entry.Name, StringComparison.Ordinal) || latest.Version != entry.Version)
            {
                return true;
            }
        }

        var rosters = await publicationRepository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, communityContext, cancellationToken)
            ?? throw new InvalidDataException("Current Bundesliga roster publication head is missing.");
        var elo = await publicationRepository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, communityContext, cancellationToken)
            ?? throw new InvalidDataException("Current Bundesliga Club Elo publication head is missing.");
        _ = BundesligaRosterPublication.ReconstructLastKnownGood(rosters);
        _ = BundesligaClubEloPublication.ReconstructLastKnownGood(elo);
        if (!string.Equals(rosters.Snapshot.SnapshotId, manifest.RosterPublicationSnapshotId, StringComparison.Ordinal)
            || !string.Equals(elo.Snapshot.SnapshotId, manifest.ClubEloPublicationSnapshotId, StringComparison.Ordinal))
        {
            return true;
        }

        // Snapshot content IDs deliberately exclude storage versions. A republished payload can
        // therefore retain its content ID while the immutable document identities advance. Do
        // not classify that prediction as current unless its two selected roster and Elo rows
        // still have the exact versions recorded in the manifest.
        foreach (var entry in manifest.Documents.Where(entry => IsReserved(entry.Name)))
        {
            var publication = entry.Name.StartsWith("roster-", StringComparison.Ordinal) ? rosters : elo;
            var current = publication.Documents.SingleOrDefault(document => string.Equals(document.Name, entry.Name, StringComparison.Ordinal));
            if (current is null || current.Version != entry.Version)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReserved(string name) =>
        name.StartsWith("roster-", StringComparison.Ordinal) || name.StartsWith("club-elo-", StringComparison.Ordinal);
}
