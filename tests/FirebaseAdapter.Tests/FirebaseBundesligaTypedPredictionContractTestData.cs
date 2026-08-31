using EHonda.KicktippAi.Core;
using NodaTime;

namespace FirebaseAdapter.Tests;

internal static class FirebaseBundesligaTypedPredictionContractTestData
{
    public const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string ShaC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    public const string MatchRoute = "synthetic-bundesliga-match-v1";
    public const string BonusRoute = "synthetic-bundesliga-bonus-v1";
    public const string PromptName = "kicktippai/bundesliga-2026-27/predict-one-match";

    public static BundesligaPredictionRouteCatalog Routes() => new(
    [
        new BundesligaPredictionRouteContract(
            MatchRoute, BundesligaPredictionItemKind.Match, BundesligaSeasonSubcompetition.Bundesliga),
        new BundesligaPredictionRouteContract(
            BonusRoute, BundesligaPredictionItemKind.Bonus, BundesligaSeasonSubcompetition.Bundesliga)
    ]);

    public static PredictionModelConfig Model() =>
        PredictionModelConfig.Create("gpt-5.6-sol", "xhigh", 10_000, PromptName, 3);

    public static PredictionPromptProvenanceV2 Prompt() =>
        PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.Hosted, PromptName, 3, ShaA, "production", true, null, null);

    public static StableLocalItemKey MatchKey(string community, string id = "42") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Match,
            id);

    public static StableLocalItemKey BonusKey(string community, string id = "84") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Bonus,
            id);

    public static TypedMatchSnapshot Match(string community, string id = "42")
    {
        const string scheduled = "2026-09-01T18:00:00Z";
        return TypedMatchSnapshot.Create(
            MatchKey(community, id),
            BundesligaSeasonSubcompetition.Bundesliga,
            "1. Spieltag",
            ResultBasis.RegularTime90Minutes,
            "FC Example",
            "SV Sample",
            1,
            BundesligaScheduledInstantResolver.Resolve(
                new BundesligaFixtureScheduleEvidence(id, false, scheduled),
                [new BundesligaFixtureDetailScheduleEvidence(id, scheduled)]));
    }

    public static TypedBonusSnapshot Bonus(string community, string id = "84") =>
        TypedBonusSnapshot.Create(
            BonusKey(community, id),
            BundesligaSeasonSubcompetition.Bundesliga,
            "Wer wird Meister?",
            Instant.FromUtc(2026, 8, 28, 16, 30),
            1,
            [
                new TypedBonusSnapshotOption("a", "FC Example"),
                new TypedBonusSnapshotOption("b", "SV Sample")
            ]);

    public static BundesligaIdentitySeedGeneration Seed(string community)
    {
        var routes = Routes();
        return BundesligaIdentitySeedGeneration.Create(
            community,
            1,
            null,
            $"synthetic-{community}",
            [
                BundesligaIdentitySeedEntry.ForBonus(BonusRoute, Bonus(community), routes),
                BundesligaIdentitySeedEntry.ForMatch(MatchRoute, Match(community), routes)
            ],
            routes);
    }

    public static BundesligaPredictionAuthority DirectAuthority(BundesligaIdentitySeedGeneration seed) =>
        BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            seed.PostingCommunity,
            seed.PostingCommunity,
            seed.PostingCommunity,
            seed.Reference,
            seed.Reference);

    public static BundesligaTypedCurrentRequest<TypedMatchSnapshot> MatchCurrent(
        string community = "pes-squad")
    {
        var seed = Seed(community);
        return BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            DirectAuthority(seed),
            seed.RequireEntry(MatchKey(community)).MatchSnapshot!,
            Model(),
            CurrentIdentity(MatchRoute, "bundesliga-match-primary-v1"),
            Routes());
    }

    public static BundesligaTypedCurrentRequest<TypedBonusSnapshot> BonusCurrent(
        string community = "pes-squad")
    {
        var seed = Seed(community);
        return BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            DirectAuthority(seed),
            seed.RequireEntry(BonusKey(community)).BonusSnapshot!,
            Model(),
            CurrentIdentity(BonusRoute, "bundesliga-bonus-primary-v1"),
            Routes());
    }

    public static BundesligaTypedCurrentIdentity CurrentIdentity(string route, string profile) =>
        BundesligaTypedCurrentIdentity.Create(
            route,
            profile,
            BundesligaGenerationInputContractReference.Create("synthetic-generation-input-v1", ShaC));

    public static PredictionGenerationProvenanceV2 MatchProvenance(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> current,
        int index = 0,
        string? predictionIdentity = null,
        string? physicalNamespace = null) =>
        Provenance(
            current.Authority,
            physicalNamespace ?? FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Identity,
            current.ModelConfig,
            null,
            index,
            predictionIdentity ?? $"match-{current.Snapshot.Key.KicktippItemId}-r{index}",
            new PredictionGenerationUsageV2(100 + index, 20, 0.01m));

    public static PredictionGenerationProvenanceV2 BonusProvenance(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> current,
        int index = 0,
        string? predictionIdentity = null,
        string? physicalNamespace = null) =>
        Provenance(
            current.Authority,
            physicalNamespace ?? FirebaseBundesligaTypedPredictionCollections.BonusPredictions,
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Identity,
            current.ModelConfig,
            null,
            index,
            predictionIdentity ?? $"bonus-{current.Snapshot.Key.KicktippItemId}-r{index}",
            new PredictionGenerationUsageV2(50 + index, 10, 0.005m));

    public static PredictionCopyCompatibilityV2Input<TypedMatchSnapshot> MatchCopyInput()
    {
        var posting = Seed("relaxdays-tippt");
        var source = Seed("pes-squad");
        var binding = Binding(posting, source);
        var targetCurrent = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            CopyAuthority(posting, source, binding),
            posting.RequireEntry(MatchKey(posting.PostingCommunity)).MatchSnapshot!,
            Model(), CurrentIdentity(MatchRoute, "bundesliga-match-primary-v1"), Routes());
        var sourceCurrent = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            DirectAuthority(source),
            source.RequireEntry(MatchKey(source.PostingCommunity)).MatchSnapshot!,
            Model(), CurrentIdentity(MatchRoute, "bundesliga-match-primary-v1"), Routes());
        return PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            targetCurrent,
            sourceCurrent,
            posting,
            source,
            binding,
            binding.RequirePostingItem(MatchKey(posting.PostingCommunity)),
            MatchCompatibility(targetCurrent.Authority.CommunityContext),
            MatchCompatibility(sourceCurrent.Authority.CommunityContext));
    }

    public static PredictionCopyCompatibilityV2Input<TypedBonusSnapshot> BonusCopyInput()
    {
        var posting = Seed("relaxdays-tippt");
        var source = Seed("pes-squad");
        var binding = Binding(posting, source);
        var targetCurrent = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            CopyAuthority(posting, source, binding),
            posting.RequireEntry(BonusKey(posting.PostingCommunity)).BonusSnapshot!,
            Model(), CurrentIdentity(BonusRoute, "bundesliga-bonus-primary-v1"), Routes());
        var sourceCurrent = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            DirectAuthority(source),
            source.RequireEntry(BonusKey(source.PostingCommunity)).BonusSnapshot!,
            Model(), CurrentIdentity(BonusRoute, "bundesliga-bonus-primary-v1"), Routes());
        return PredictionCopyCompatibilityV2Input<TypedBonusSnapshot>.Create(
            targetCurrent,
            sourceCurrent,
            posting,
            source,
            binding,
            binding.RequirePostingItem(BonusKey(posting.PostingCommunity)),
            BonusCompatibility(targetCurrent.Authority.CommunityContext),
            BonusCompatibility(sourceCurrent.Authority.CommunityContext));
    }

    public static PredictionGenerationProvenanceV2 MatchCopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedMatchSnapshot> input,
        string sourcePredictionIdentity) =>
        Provenance(
            input.TargetCurrent.Authority,
            FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
            input.TargetCurrent.Snapshot.Key,
            input.TargetCurrent.Snapshot.SnapshotHash,
            input.SourceCurrent.Snapshot.Key,
            input.SourceCurrent.Snapshot.SnapshotHash,
            input.TargetCurrent.Identity,
            input.TargetCurrent.ModelConfig,
            sourcePredictionIdentity,
            0,
            "copied-match-r0",
            new PredictionGenerationUsageV2(0, 0, 0));

    public static PredictionGenerationProvenanceV2 BonusCopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedBonusSnapshot> input,
        string sourcePredictionIdentity) =>
        Provenance(
            input.TargetCurrent.Authority,
            FirebaseBundesligaTypedPredictionCollections.BonusPredictions,
            input.TargetCurrent.Snapshot.Key,
            input.TargetCurrent.Snapshot.SnapshotHash,
            input.SourceCurrent.Snapshot.Key,
            input.SourceCurrent.Snapshot.SnapshotHash,
            input.TargetCurrent.Identity,
            input.TargetCurrent.ModelConfig,
            sourcePredictionIdentity,
            0,
            "copied-bonus-r0",
            new PredictionGenerationUsageV2(0, 0, 0));

    private static PredictionGenerationProvenanceV2 Provenance(
        BundesligaPredictionAuthority authority,
        string physicalNamespace,
        StableLocalItemKey postingKey,
        BundesligaPredictionSnapshotHash postingHash,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceHash,
        BundesligaTypedCurrentIdentity identity,
        PredictionModelConfig model,
        string? sourcePredictionIdentity,
        int index,
        string predictionIdentity,
        PredictionGenerationUsageV2 usage) =>
        PredictionGenerationProvenanceV2.Create(
            authority,
            physicalNamespace,
            postingKey,
            postingHash,
            sourceKey,
            sourceHash,
            identity.RouteId,
            identity.ProfileId,
            identity.GenerationInputContract,
            sourcePredictionIdentity,
            Prompt(),
            model,
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            PredictionContextProvenanceV2.Create(
                "context-v1", ShaA, "rules-v1", ShaB,
                [new PredictionContextDocumentIdentityV2("rules.md@1", ShaC)]),
            Instant.FromUtc(2026, 8, 31, 12, index),
            predictionIdentity,
            index,
            usage);

    private static BundesligaCopyBindingGeneration Binding(
        BundesligaIdentitySeedGeneration posting,
        BundesligaIdentitySeedGeneration source)
    {
        var routes = Routes();
        return BundesligaCopyBindingGeneration.Create(
            posting.PostingCommunity,
            source.PostingCommunity,
            1,
            null,
            "synthetic-binding",
            posting,
            source,
            [
                BundesligaCopyBindingEntry.CreateMatch(
                    MatchRoute, posting, MatchKey(posting.PostingCommunity),
                    source, MatchKey(source.PostingCommunity), routes),
                BundesligaCopyBindingEntry.CreateBonus(
                    BonusRoute, posting, BonusKey(posting.PostingCommunity),
                    source, BonusKey(source.PostingCommunity),
                    [
                        new BundesligaBonusOptionProjection("a", "a"),
                        new BundesligaBonusOptionProjection("b", "b")
                    ], routes)
            ]);
    }

    private static BundesligaPredictionAuthority CopyAuthority(
        BundesligaIdentitySeedGeneration posting,
        BundesligaIdentitySeedGeneration source,
        BundesligaCopyBindingGeneration binding) =>
        BundesligaPredictionAuthority.CreateCopy(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            posting.PostingCommunity,
            source.PostingCommunity,
            source.PostingCommunity,
            posting.Reference,
            source.Reference,
            binding.Reference);

    private static PredictionCopyCompatibilityContractV2 MatchCompatibility(string context) =>
        PredictionCopyCompatibilityContractV2.CreateMatch(
            context,
            MatchRoute,
            BundesligaSeasonSubcompetition.Bundesliga,
            PredictionRulesIdentityV2.Create("rules-v1", ShaA),
            PredictionScoringIdentityV2.Create("scoring-v1", ShaB),
            PredictionResultBasisIdentityV2.Create(
                ResultBasis.RegularTime90Minutes, "result-v1", ShaC),
            Prompt(),
            Model(),
            PredictionCopyPolicyIdentityV2.Create(
                "copy-policy-v1", ShaA, MatchRoute, MatchRoute, context, context));

    private static PredictionCopyCompatibilityContractV2 BonusCompatibility(string context) =>
        PredictionCopyCompatibilityContractV2.CreateBonus(
            context,
            BonusRoute,
            BundesligaSeasonSubcompetition.Bundesliga,
            PredictionRulesIdentityV2.Create("rules-v1", ShaA),
            PredictionScoringIdentityV2.Create("scoring-v1", ShaB),
            Prompt(),
            Model(),
            PredictionCopyPolicyIdentityV2.Create(
                "bonus-copy-policy-v1", ShaA, BonusRoute, BonusRoute, context, context),
            PredictionOptionMeaningIdentityV2.Create("options-v1", ShaC));
}
