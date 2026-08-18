using EHonda.KicktippAi.Core;
using TestUtilities;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public sealed class FirebasePredictionRepository_ResolvedContextManifest_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_prediction_with_resolved_context_round_trips_the_exact_manifest()
    {
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund", matchday: 1);
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            "ehonda-dev-buli-2627",
            MatchContextDocumentCatalog.ForMatch(match, "ehonda-dev-buli-2627", CompetitionIds.Bundesliga2026_27)
                .RequiredDocumentNames.Select((name, index) => new ResolvedMatchContextDocument(name, index + 1)),
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

        await repository.SavePredictionWithResolvedContextAsync(
            match,
            new Prediction(2, 1),
            config,
            "{}",
            0.01,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            manifest);

        var metadata = await repository.GetPredictionMetadataAsync(match, config, manifest.CommunityContext);
        var loaded = await repository.GetResolvedMatchContextManifestAsync(match, config, manifest.CommunityContext);

        await Assert.That(metadata!.ResolvedContextManifest).IsNotNull();
        await Assert.That(loaded).IsNotNull();
        foreach (var roundTrippedManifest in new[] { metadata.ResolvedContextManifest!, loaded! })
        {
            await Assert.That(roundTrippedManifest.Competition).IsEqualTo(manifest.Competition);
            await Assert.That(roundTrippedManifest.CommunityContext).IsEqualTo(manifest.CommunityContext);
            await Assert.That(roundTrippedManifest.RosterPublicationSnapshotId).IsEqualTo(manifest.RosterPublicationSnapshotId);
            await Assert.That(roundTrippedManifest.ClubEloPublicationSnapshotId).IsEqualTo(manifest.ClubEloPublicationSnapshotId);
            await Assert.That(roundTrippedManifest.Documents).IsEquivalentTo(manifest.Documents);
        }
    }
}
