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

    public static BundesligaGenerationInputContractReference GenerationInput(
        string id = "synthetic-generation-input-v1",
        string sha256 = ShaC) =>
        BundesligaGenerationInputContractReference.Create(id, sha256);

    public static BundesligaTypedCurrentIdentity CurrentIdentity(
        string routeId = MatchRoute,
        string profileId = "bundesliga-match-primary-v1",
        BundesligaGenerationInputContractReference? generationInput = null) =>
        BundesligaTypedCurrentIdentity.Create(routeId, profileId, generationInput ?? GenerationInput());

    public static PredictionCopyCompatibilityContractV2 MatchCompatibilityContract(
        string communityContext = "pes-squad",
        string routeId = MatchRoute,
        PredictionRulesIdentityV2? rules = null,
        PredictionScoringIdentityV2? scoring = null,
        PredictionResultBasisIdentityV2? resultBasis = null,
        PredictionPromptProvenanceV2? prompt = null,
        PredictionModelConfig? model = null,
        PredictionCopyPolicyIdentityV2? policy = null) =>
        PredictionCopyCompatibilityContractV2.CreateMatch(
            communityContext,
            routeId,
            BundesligaSeasonSubcompetition.Bundesliga,
            rules ?? PredictionRulesIdentityV2.Create("synthetic-rules-v1", ShaA),
            scoring ?? PredictionScoringIdentityV2.Create("synthetic-scoring-v1", ShaB),
            resultBasis ?? PredictionResultBasisIdentityV2.Create(
                ResultBasis.RegularTime90Minutes, "regular-time-result-v1", ShaC),
            prompt ?? Prompt(),
            model ?? Model(),
            policy ?? PredictionCopyPolicyIdentityV2.Create(
                "synthetic-copy-policy-v1", ShaA, MatchRoute, MatchRoute,
                communityContext, communityContext));

    public static PredictionCopyCompatibilityContractV2 BonusCompatibilityContract(
        string communityContext = "pes-squad",
        PredictionRulesIdentityV2? rules = null,
        PredictionScoringIdentityV2? scoring = null,
        PredictionOptionMeaningIdentityV2? optionMeaning = null) =>
        PredictionCopyCompatibilityContractV2.CreateBonus(
            communityContext,
            BonusRoute,
            BundesligaSeasonSubcompetition.Bundesliga,
            rules ?? PredictionRulesIdentityV2.Create("synthetic-rules-v1", ShaA),
            scoring ?? PredictionScoringIdentityV2.Create("synthetic-scoring-v1", ShaB),
            Prompt(),
            Model(),
            PredictionCopyPolicyIdentityV2.Create(
                "synthetic-bonus-copy-policy-v1", ShaC, BonusRoute, BonusRoute,
                communityContext, communityContext),
            optionMeaning ?? PredictionOptionMeaningIdentityV2.Create("synthetic-option-meaning-v1", ShaA));

    public static PredictionCopyCompatibilityV2Input<TypedMatchSnapshot> MatchCopyInput(
        BundesligaIdentitySeedGeneration? postingSeed = null,
        BundesligaIdentitySeedGeneration? sourceSeed = null,
        BundesligaCopyBindingGeneration? binding = null,
        PredictionCopyCompatibilityContractV2? targetContract = null,
        PredictionCopyCompatibilityContractV2? sourceContract = null,
        BundesligaPredictionAuthority? sourceAuthority = null)
    {
        postingSeed ??= Seed("relaxdays-tippt");
        sourceSeed ??= Seed("pes-squad");
        binding ??= Binding(postingSeed, sourceSeed);
        var target = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            CopyAuthority(postingSeed, sourceSeed, binding),
            postingSeed.RequireEntry(MatchKey(postingSeed.PostingCommunity)).MatchSnapshot!,
            Model(), CurrentIdentity(), Routes());
        var source = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            sourceAuthority ?? DirectAuthority(sourceSeed),
            sourceSeed.RequireEntry(MatchKey(sourceSeed.PostingCommunity)).MatchSnapshot!,
            Model(), CurrentIdentity(), Routes());
        return PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            target, source, postingSeed, sourceSeed, binding,
            binding.RequirePostingItem(MatchKey(postingSeed.PostingCommunity)),
            targetContract ?? MatchCompatibilityContract(target.Authority.CommunityContext),
            sourceContract ?? MatchCompatibilityContract(source.Authority.CommunityContext));
    }

    public static PredictionCopyCompatibilityV2Input<TypedBonusSnapshot> BonusCopyInput(
        PredictionCopyCompatibilityContractV2? targetContract = null,
        PredictionCopyCompatibilityContractV2? sourceContract = null)
    {
        var postingSeed = Seed("relaxdays-tippt");
        var sourceSeed = Seed("pes-squad");
        var binding = Binding(postingSeed, sourceSeed);
        var target = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            CopyAuthority(postingSeed, sourceSeed, binding),
            postingSeed.RequireEntry(BonusKey(postingSeed.PostingCommunity)).BonusSnapshot!,
            Model(), CurrentIdentity(BonusRoute, "bundesliga-bonus-primary-v1"), Routes());
        var source = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            DirectAuthority(sourceSeed),
            sourceSeed.RequireEntry(BonusKey(sourceSeed.PostingCommunity)).BonusSnapshot!,
            Model(), CurrentIdentity(BonusRoute, "bundesliga-bonus-primary-v1"), Routes());
        return PredictionCopyCompatibilityV2Input<TypedBonusSnapshot>.Create(
            target, source, postingSeed, sourceSeed, binding,
            binding.RequirePostingItem(BonusKey(postingSeed.PostingCommunity)),
            targetContract ?? BonusCompatibilityContract(target.Authority.CommunityContext),
            sourceContract ?? BonusCompatibilityContract(source.Authority.CommunityContext));
    }

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
            GenerationInput(),
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

    public static PredictionGenerationProvenanceV2 CopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedMatchSnapshot> input,
        string sourcePredictionIdentity = "prediction-42-r0")
    {
        var target = input.TargetCurrent;
        var source = input.SourceCurrent;
        return PredictionGenerationProvenanceV2.Create(
            target.Authority,
            "match-predictions-bundesliga-2026-27-typed-v1",
            target.Snapshot.Key,
            target.Snapshot.SnapshotHash,
            source.Snapshot.Key,
            source.Snapshot.SnapshotHash,
            target.Identity.RouteId,
            target.Identity.ProfileId,
            target.Identity.GenerationInputContract,
            sourcePredictionIdentity,
            Prompt(),
            target.ModelConfig,
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            Context(),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            "copied-prediction-42",
            0,
            new PredictionGenerationUsageV2(0, 0, 0));
    }

    public static PredictionGenerationProvenanceV2 BonusDirectProvenance(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> current,
        string predictionIdentity = "bonus-prediction-84") =>
        PredictionGenerationProvenanceV2.Create(
            current.Authority,
            "bonus-predictions-bundesliga-2026-27-typed-v1",
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Snapshot.Key,
            current.Snapshot.SnapshotHash,
            current.Identity.RouteId,
            current.Identity.ProfileId,
            current.Identity.GenerationInputContract,
            null,
            Prompt(),
            current.ModelConfig,
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            Context(),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            predictionIdentity,
            0,
            new PredictionGenerationUsageV2(100, 20, 0.001m));

    public static PredictionGenerationProvenanceV2 BonusCopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedBonusSnapshot> input,
        string sourcePredictionIdentity = "bonus-prediction-84") =>
        PredictionGenerationProvenanceV2.Create(
            input.TargetCurrent.Authority,
            "bonus-predictions-bundesliga-2026-27-typed-v1",
            input.TargetCurrent.Snapshot.Key,
            input.TargetCurrent.Snapshot.SnapshotHash,
            input.SourceCurrent.Snapshot.Key,
            input.SourceCurrent.Snapshot.SnapshotHash,
            input.TargetCurrent.Identity.RouteId,
            input.TargetCurrent.Identity.ProfileId,
            input.TargetCurrent.Identity.GenerationInputContract,
            sourcePredictionIdentity,
            Prompt(),
            input.TargetCurrent.ModelConfig,
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            Context(),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            "copied-bonus-prediction-84",
            0,
            new PredictionGenerationUsageV2(0, 0, 0));
}
