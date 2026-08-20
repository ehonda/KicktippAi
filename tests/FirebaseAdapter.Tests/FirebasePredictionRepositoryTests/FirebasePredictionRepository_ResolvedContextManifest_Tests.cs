using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using System.Text.Json;
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
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund", matchday: 1);
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest(match, "ehonda-dev-buli-2627");

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

    [Test]
    public async Task Ordinary_Bundesliga_save_entrypoints_reject_manifestless_new_writes()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var config = PredictionModelConfig.Create("gpt-5");

        await Assert.That(() => repository.SavePredictionAsync(
                match, new Prediction(2, 1), config, "{}", 0.01, "test-community", []))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.SaveRepredictionAsync(
                match, new Prediction(2, 1), config, "{}", 0.01, "test-community", [], 0))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Provenance_capable_Bundesliga_save_entrypoints_reject_mismatched_manifest_scope()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var manifest = CreateManifest(match, "different-community");
        var config = PredictionModelConfig.Create("gpt-5");

        await Assert.That(() => repository.SavePredictionWithResolvedContextAsync(
                match, new Prediction(2, 1), config, "{}", 0.01, "test-community",
                manifest.Documents.Select(document => document.Name), manifest))
            .Throws<ArgumentException>();
        await Assert.That(() => repository.SaveRepredictionWithResolvedContextAsync(
                match, new Prediction(2, 1), config, "{}", 0.01, "test-community",
                manifest.Documents.Select(document => document.Name), -1, 1, manifest))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Transactional_Bundesliga_reprediction_allocation_allows_only_one_concurrent_writer()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest(match, "test-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();
        await repository.SavePredictionWithResolvedContextAsync(match, new Prediction(1, 0), config, "{}", 0.01,
            "test-community", names, manifest);

        await Assert.That(() => repository.SaveRepredictionWithResolvedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
                "test-community", names, -1, 1, manifest))
            .Throws<InvalidOperationException>();

        var first = repository.SaveRepredictionWithResolvedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "test-community", names, 0, 1, manifest);
        var second = repository.SaveRepredictionWithResolvedContextAsync(match, new Prediction(3, 1), config, "{}", 0.01,
            "test-community", names, 0, 1, manifest);

        try
        {
            await Task.WhenAll(first, second);
        }
        catch (InvalidOperationException)
        {
            // One stale CAS contender must fail after the other allocates index 1.
        }

        await Assert.That(new[] { first, second }.Count(task => task.Status == TaskStatus.RanToCompletion)).IsEqualTo(1);
        await Assert.That(await repository.GetMatchRepredictionIndexAsync(match, config, "test-community")).IsEqualTo(1);
    }

    [Test]
    public async Task Transactional_Bundesliga_reprediction_allocation_enforces_the_configured_maximum_and_cancelled_lookup()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var storedMatch = CreateCanonicalBundesligaMatch(isCancelled: true);
        var rescheduledCancelledMatch = storedMatch with { StartsAt = storedMatch.StartsAt.PlusHours(2) };
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest(rescheduledCancelledMatch, "test-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();
        await repository.SavePredictionWithResolvedContextAsync(storedMatch, new Prediction(1, 0), config, "{}", 0.01,
            "test-community", names, CreateManifest(storedMatch, "test-community"));

        await repository.SaveRepredictionWithResolvedContextAsync(rescheduledCancelledMatch, new Prediction(2, 1), config, "{}", 0.01,
            "test-community", names, 0, 1, manifest);
        await Assert.That(await repository.GetCancelledMatchRepredictionIndexAsync(
            storedMatch.HomeTeam, storedMatch.AwayTeam, config, "test-community")).IsEqualTo(1);
        await Assert.That(() => repository.SaveRepredictionWithResolvedContextAsync(rescheduledCancelledMatch, new Prediction(3, 1), config,
                "{}", 0.01, "test-community", names, 1, 1, manifest))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Transactional_Bundesliga_reprediction_allocation_rejects_the_int32_maximum_boundary()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest(match, "test-community");
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("match-predictions").Document("int32-max-reprediction").SetAsync(new FirestoreMatchPrediction
        {
            Id = "int32-max-reprediction",
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            StartsAt = Timestamp.FromDateTime(match.StartsAt.ToInstant().ToDateTimeUtc()),
            Matchday = match.Matchday,
            HomeGoals = 1,
            AwayGoals = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Competition = CompetitionIds.Bundesliga2026_27,
            Model = config.Model,
            ModelConfigKey = config.IdentityKey,
            ReasoningEffort = config.ReasoningEffort,
            TokenUsage = "{}",
            CommunityContext = manifest.CommunityContext,
            RepredictionIndex = int.MaxValue
        });

        await Assert.That(() => repository.SaveRepredictionWithResolvedContextAsync(
                match, new Prediction(2, 1), config, "{}", 0.01, manifest.CommunityContext,
                manifest.Documents.Select(document => document.Name), int.MaxValue, int.MaxValue, manifest))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("overflow");
    }

    [Test]
    public async Task Metadata_read_preserves_direct_seeded_manifestless_bundesliga_history()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var config = PredictionModelConfig.Create("gpt-5");
        await SeedManifestlessHistoricalPredictionAsync(match, config, "historical-normal");

        var metadata = await repository.GetPredictionMetadataAsync(match, config, "test-community");

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNull();
    }

    [Test]
    public async Task Cancelled_metadata_read_preserves_direct_seeded_manifestless_bundesliga_history()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch(isCancelled: true);
        var config = PredictionModelConfig.Create("gpt-5");
        await SeedManifestlessHistoricalPredictionAsync(match, config, "historical-cancelled");

        var metadata = await repository.GetCancelledMatchPredictionMetadataAsync(
            match.HomeTeam, match.AwayTeam, config, "test-community");

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNull();
    }

    [Test]
    public async Task Cancelled_metadata_read_still_fails_closed_for_a_non_null_corrupt_manifest()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch(isCancelled: true);
        var config = PredictionModelConfig.Create("gpt-5");
        await SeedManifestlessHistoricalPredictionAsync(match, config, "historical-cancelled-corrupt");
        await Fixture.Db.Collection("match-predictions").Document("historical-cancelled-corrupt")
            .UpdateAsync("resolvedContextManifest", "{\"competition\":\"bundesliga-2026-27\"}");

        await Assert.That(() => repository.GetCancelledMatchPredictionMetadataAsync(
                match.HomeTeam, match.AwayTeam, config, "test-community"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Metadata_read_fails_closed_for_direct_seeded_corrupt_resolved_context_manifest_json()
    {
        var (repository, match, config, manifest, reference) = await SavePredictionForDirectManifestMutationAsync();
        await reference.UpdateAsync("resolvedContextManifest", "{\"competition\":\"bundesliga-2026-27\"}");

        await Assert.That(() => repository.GetPredictionMetadataAsync(match, config, manifest.CommunityContext))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Metadata_read_fails_closed_for_direct_seeded_reordered_resolved_context_manifest_json()
    {
        var (repository, match, config, manifest, reference) = await SavePredictionForDirectManifestMutationAsync();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
        var root = json.RootElement;
        var reordered = string.Concat(
            "{\"communityContext\":", root.GetProperty("communityContext").GetRawText(),
            ",\"competition\":", root.GetProperty("competition").GetRawText(),
            ",\"documents\":", root.GetProperty("documents").GetRawText(),
            ",\"rosterPublicationSnapshotId\":", root.GetProperty("rosterPublicationSnapshotId").GetRawText(),
            ",\"clubEloPublicationSnapshotId\":", root.GetProperty("clubEloPublicationSnapshotId").GetRawText(),
            "}");
        await reference.UpdateAsync("resolvedContextManifest", reordered);

        await Assert.That(() => repository.GetPredictionMetadataAsync(match, config, manifest.CommunityContext))
            .Throws<InvalidDataException>();
    }

    private static Match CreateCanonicalBundesligaMatch(bool isCancelled = false) =>
        CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund", matchday: 1, isCancelled: isCancelled);

    private static ResolvedMatchContextManifest CreateManifest(Match match, string communityContext) =>
        ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            communityContext,
            MatchContextDocumentCatalog.ForMatch(match, communityContext, CompetitionIds.Bundesliga2026_27)
                .RequiredDocumentNames.Select((name, index) => new ResolvedMatchContextDocument(
                    name, index + 1, "Context", DocumentPublicationContract.ComputeContentSha256(name))),
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private async Task<(FirebasePredictionRepository Repository, Match Match, PredictionModelConfig Config,
        ResolvedMatchContextManifest Manifest, Google.Cloud.Firestore.DocumentReference Reference)>
        SavePredictionForDirectManifestMutationAsync()
    {
        var repository = CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));
        var match = CreateCanonicalBundesligaMatch();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest(match, "test-community");
        await repository.SavePredictionWithResolvedContextAsync(
            match, new Prediction(2, 1), config, "{}", 0.01, manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name), manifest);
        var snapshot = await Fixture.Db.Collection("match-predictions")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .WhereEqualTo("homeTeam", match.HomeTeam)
            .WhereEqualTo("awayTeam", match.AwayTeam)
            .WhereEqualTo("model", config.Model)
            .WhereEqualTo("communityContext", manifest.CommunityContext)
            .GetSnapshotAsync();

        return (repository, match, config, manifest, snapshot.Documents.Single().Reference);
    }

    private async Task SeedManifestlessHistoricalPredictionAsync(
        Match match,
        PredictionModelConfig config,
        string documentId)
    {
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("match-predictions").Document(documentId).SetAsync(new FirestoreMatchPrediction
        {
            Id = documentId,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            StartsAt = Timestamp.FromDateTime(match.StartsAt.ToInstant().ToDateTimeUtc()),
            Matchday = match.Matchday,
            HomeGoals = 2,
            AwayGoals = 1,
            CreatedAt = now,
            UpdatedAt = now,
            Competition = CompetitionIds.Bundesliga2026_27,
            Model = config.Model,
            ModelConfigKey = config.IdentityKey,
            ReasoningEffort = config.ReasoningEffort,
            TokenUsage = "{}",
            CommunityContext = "test-community",
            ContextDocumentNames = [],
            ResolvedContextManifest = null,
            RepredictionIndex = 0
        });
    }
}
