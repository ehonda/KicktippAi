using EHonda.KicktippAi.Core;
using NodaTime;

namespace Core.Tests;

internal static class BundesligaPredictionContractTestData
{
    public const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string ShaC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    public const string MatchRoute = "synthetic-bundesliga-match-v1";
    public const string BonusRoute = "synthetic-bundesliga-bonus-v1";
    public const string MatchPrompt = "kicktippai/bundesliga-2026-27/predict-one-match";
    public const string MatchTime = "2026-09-01T18:00:00Z";

    public static BundesligaPredictionRouteCatalog Routes() => new(
    [
        new BundesligaPredictionRouteContract(
            MatchRoute,
            BundesligaPredictionItemKind.Match,
            BundesligaSeasonSubcompetition.Bundesliga),
        new BundesligaPredictionRouteContract(
            BonusRoute,
            BundesligaPredictionItemKind.Bonus,
            BundesligaSeasonSubcompetition.Bundesliga)
    ]);

    public static StableLocalItemKey MatchKey(
        string community = "pes-squad",
        string id = "42") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Match,
            id);

    public static StableLocalItemKey BonusKey(
        string community = "pes-squad",
        string id = "84") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Bonus,
            id);

    public static TypedMatchSnapshot Match(
        string community = "pes-squad",
        string id = "42",
        string scheduledInstant = MatchTime,
        string round = "1. Spieltag")
    {
        var resolved = BundesligaScheduledInstantResolver.Resolve(
            new BundesligaFixtureScheduleEvidence(id, false, scheduledInstant),
            [new BundesligaFixtureDetailScheduleEvidence(id, scheduledInstant)]);
        return TypedMatchSnapshot.Create(
            MatchKey(community, id),
            BundesligaSeasonSubcompetition.Bundesliga,
            round,
            ResultBasis.RegularTime90Minutes,
            "FC Example",
            "SV Sample",
            1,
            resolved);
    }

    public static TypedBonusSnapshot Bonus(
        string community = "pes-squad",
        string id = "84",
        IEnumerable<TypedBonusSnapshotOption>? options = null) =>
        TypedBonusSnapshot.Create(
            BonusKey(community, id),
            BundesligaSeasonSubcompetition.Bundesliga,
            "Wer wird Meister?",
            Instant.FromUtc(2026, 8, 28, 16, 30),
            1,
            options ??
            [
                new TypedBonusSnapshotOption("a", "FC Example"),
                new TypedBonusSnapshotOption("b", "SV Sample")
            ]);

    public static BundesligaIdentitySeedGeneration Seed(
        string community = "pes-squad",
        int generation = 1,
        BundesligaGenerationPredecessor? predecessor = null,
        string matchTime = MatchTime)
    {
        var routes = Routes();
        return BundesligaIdentitySeedGeneration.Create(
            community,
            generation,
            predecessor,
            $"synthetic-evidence-{community}-g{generation}",
            [
                BundesligaIdentitySeedEntry.ForBonus(BonusRoute, Bonus(community), routes),
                BundesligaIdentitySeedEntry.ForMatch(MatchRoute, Match(community, scheduledInstant: matchTime), routes)
            ],
            routes);
    }

    public static BundesligaCopyBindingGeneration Binding(
        BundesligaIdentitySeedGeneration? postingSeed = null,
        BundesligaIdentitySeedGeneration? sourceSeed = null)
    {
        postingSeed ??= Seed("relaxdays-tippt");
        sourceSeed ??= Seed("pes-squad");
        var routes = Routes();
        return BundesligaCopyBindingGeneration.Create(
            postingSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            1,
            null,
            "synthetic-copy-evidence-g1",
            postingSeed,
            sourceSeed,
            [
                BundesligaCopyBindingEntry.CreateBonus(
                    BonusRoute,
                    postingSeed,
                    BonusKey(postingSeed.PostingCommunity),
                    sourceSeed,
                    BonusKey(sourceSeed.PostingCommunity),
                    [
                        new BundesligaBonusOptionProjection("a", "a"),
                        new BundesligaBonusOptionProjection("b", "b")
                    ],
                    routes),
                BundesligaCopyBindingEntry.CreateMatch(
                    MatchRoute,
                    postingSeed,
                    MatchKey(postingSeed.PostingCommunity),
                    sourceSeed,
                    MatchKey(sourceSeed.PostingCommunity),
                    routes)
            ]);
    }

    public static BundesligaPredictionAuthority DirectAuthority(
        BundesligaIdentitySeedGeneration? seed = null)
    {
        seed ??= Seed();
        return BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            seed.PostingCommunity,
            seed.PostingCommunity,
            seed.PostingCommunity,
            seed.Reference,
            seed.Reference);
    }

    public static BundesligaPredictionAuthority CopyAuthority(
        BundesligaIdentitySeedGeneration? postingSeed = null,
        BundesligaIdentitySeedGeneration? sourceSeed = null,
        BundesligaCopyBindingGeneration? binding = null)
    {
        postingSeed ??= Seed("relaxdays-tippt");
        sourceSeed ??= Seed("pes-squad");
        binding ??= Binding(postingSeed, sourceSeed);
        return BundesligaPredictionAuthority.CreateCopy(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            postingSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            postingSeed.Reference,
            sourceSeed.Reference,
            binding.Reference);
    }

    public static PredictionPromptProvenanceV2 Prompt(bool fallback = false) =>
        PredictionPromptProvenanceV2.Create(
            fallback ? PredictionPromptSourceV2.CheckedInFallback : PredictionPromptSourceV2.Hosted,
            MatchPrompt,
            3,
            ShaA,
            "production",
            true,
            fallback ? "prompts/bundesliga-2026-27/predict-one-match.md" : null,
            fallback ? ShaB : null);

    public static PredictionModelConfig Model() =>
        PredictionModelConfig.Create(
            "gpt-5.6-sol",
            "xhigh",
            10_000,
            MatchPrompt,
            3);

    public static PredictionContextProvenanceV2 Context() =>
        PredictionContextProvenanceV2.Create(
            "context-manifest-v1",
            ShaA,
            "rules-manifest-v1",
            ShaB,
            [new PredictionContextDocumentIdentityV2("community-rules.md@7", ShaC)]);

    public static PredictionGenerationProvenanceV2 DirectProvenance(
        TypedMatchSnapshot? snapshot = null,
        PredictionGenerationUsageV2? usage = null)
    {
        snapshot ??= Match();
        return PredictionGenerationProvenanceV2.Create(
            DirectAuthority(),
            "match-predictions-bundesliga-2026-27-typed-v1",
            snapshot.Key,
            snapshot.SnapshotHash,
            snapshot.Key,
            snapshot.SnapshotHash,
            MatchRoute,
            "bundesliga-match-primary-v1",
            null,
            Prompt(),
            Model(),
            PredictionServiceTierProvenanceV2.Create("flex", "standard", true, "capacity fallback"),
            Context(),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            "prediction-42-r0",
            0,
            usage ?? new PredictionGenerationUsageV2(1200, 200, 0.01m));
    }
}
