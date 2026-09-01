using EHonda.KicktippAi.Core;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Services;

namespace Orchestrator.Tests.Services;

internal static class BundesligaPredictionAuthorityKernelTestData
{
    internal const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    internal const string ShaC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    internal const string MatchRoute = "synthetic-bundesliga-match-v1";
    internal const string BonusRoute = "synthetic-bundesliga-bonus-v1";
    internal const string PromptName = "kicktippai/bundesliga-2026-27/predict-one-match";
    internal const string PromptTemplate = "Observed exact prompt\n";
    internal const string SourceCommunity = "pes-squad";
    internal const string TargetCommunity = "relaxdays-tippt";

    internal static BundesligaPredictionRouteCatalog Routes() => new(
    [
        MatchRouteContract(),
        BonusRouteContract()
    ]);

    internal static BundesligaPredictionRouteContract MatchRouteContract() => new(
        MatchRoute,
        BundesligaPredictionItemKind.Match,
        BundesligaSeasonSubcompetition.Bundesliga);

    internal static BundesligaPredictionRouteContract BonusRouteContract() => new(
        BonusRoute,
        BundesligaPredictionItemKind.Bonus,
        BundesligaSeasonSubcompetition.Bundesliga);

    internal static StableLocalItemKey MatchKey(string community, string id = "42") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Match,
            id);

    internal static StableLocalItemKey BonusKey(string community, string id = "84") =>
        StableLocalItemKey.Create(
            CompetitionIds.Bundesliga2026_27,
            community,
            BundesligaPredictionItemKind.Bonus,
            id);

    internal static TypedMatchSnapshot Match(string community, string id = "42")
    {
        const string scheduled = "2026-09-01T18:00:00Z";
        var resolved = BundesligaScheduledInstantResolver.Resolve(
            new BundesligaFixtureScheduleEvidence(id, false, scheduled),
            [new BundesligaFixtureDetailScheduleEvidence(id, scheduled)]);
        return TypedMatchSnapshot.Create(
            MatchKey(community, id),
            BundesligaSeasonSubcompetition.Bundesliga,
            "1. Spieltag",
            ResultBasis.RegularTime90Minutes,
            "FC Example",
            "SV Sample",
            1,
            resolved);
    }

    internal static TypedBonusSnapshot Bonus(
        string community,
        IEnumerable<TypedBonusSnapshotOption>? options = null) =>
        TypedBonusSnapshot.Create(
            BonusKey(community),
            BundesligaSeasonSubcompetition.Bundesliga,
            "Wer wird Meister?",
            Instant.FromUtc(2026, 8, 28, 16, 30),
            2,
            options ??
            [
                new TypedBonusSnapshotOption("a", "FC Example"),
                new TypedBonusSnapshotOption("b", "SV Sample")
            ]);

    internal static BundesligaIdentitySeedGeneration Seed(string community)
    {
        var routes = Routes();
        return BundesligaIdentitySeedGeneration.Create(
            community,
            1,
            null,
            $"synthetic-evidence-{community}",
            [
                BundesligaIdentitySeedEntry.ForBonus(BonusRoute, Bonus(community), routes),
                BundesligaIdentitySeedEntry.ForMatch(MatchRoute, Match(community), routes)
            ],
            routes);
    }

    internal static BundesligaCopyBindingGeneration Binding(
        BundesligaIdentitySeedGeneration targetSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        IEnumerable<BundesligaBonusOptionProjection>? bonusProjection = null)
    {
        var routes = Routes();
        return BundesligaCopyBindingGeneration.Create(
            targetSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            1,
            null,
            "synthetic-copy-evidence",
            targetSeed,
            sourceSeed,
            [
                BundesligaCopyBindingEntry.CreateMatch(
                    MatchRoute,
                    targetSeed,
                    MatchKey(targetSeed.PostingCommunity),
                    sourceSeed,
                    MatchKey(sourceSeed.PostingCommunity),
                    routes),
                BundesligaCopyBindingEntry.CreateBonus(
                    BonusRoute,
                    targetSeed,
                    BonusKey(targetSeed.PostingCommunity),
                    sourceSeed,
                    BonusKey(sourceSeed.PostingCommunity),
                    bonusProjection ??
                    [
                        new BundesligaBonusOptionProjection("a", "a"),
                        new BundesligaBonusOptionProjection("b", "b")
                    ],
                    routes)
            ]);
    }

    internal static BundesligaPredictionAuthority DirectAuthority(
        BundesligaIdentitySeedGeneration sourceSeed) =>
        BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            sourceSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            sourceSeed.Reference,
            sourceSeed.Reference);

    internal static BundesligaPredictionAuthority CopyAuthority(
        BundesligaIdentitySeedGeneration targetSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaCopyBindingGeneration binding) =>
        BundesligaPredictionAuthority.CreateCopy(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            targetSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            sourceSeed.PostingCommunity,
            targetSeed.Reference,
            sourceSeed.Reference,
            binding.Reference);

    internal static PredictionPromptProvenanceV2 Prompt() =>
        PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.Hosted,
            PromptName,
            3,
            PromptTemplateContentHash.ComputeSha256(PromptTemplate),
            "production",
            true);

    internal static PredictionModelConfig Model() =>
        PredictionModelConfig.Create("gpt-5.6-sol", "xhigh", 10_000, PromptName, 3);

    internal static PredictionPromptExecutionRequirement PromptRequirement(
        string? fallbackFile = null,
        string? fallbackSha256 = null) =>
        PredictionPromptExecutionRequirement.Create(
            Model(), PromptTemplateContentHash.ComputeSha256(PromptTemplate), "production",
            fallbackFile, fallbackSha256);

    internal static BundesligaGenerationInputContractReference GenerationInput() =>
        BundesligaGenerationInputContractReference.Create("synthetic-generation-input-v1", ShaC);

    internal static PredictionContextProvenanceV2 Context(PredictionRulesIdentityV2 rules) =>
        PredictionContextProvenanceV2.Create(
            "context-manifest-v1",
            ShaA,
            rules.Identity,
            rules.Sha256,
            [new PredictionContextDocumentIdentityV2("community-rules.md@7", ShaC)]);

    internal static PredictionCopyCompatibilityContractV2 MatchCompatibility(
        string context,
        string targetContext,
        string sourceContext,
        PredictionScoringIdentityV2? scoring = null,
        PredictionRulesIdentityV2? rules = null)
    {
        rules ??= PredictionRulesIdentityV2.Create("synthetic-rules-v1", ShaB);
        return PredictionCopyCompatibilityContractV2.CreateMatch(
            context,
            MatchRoute,
            BundesligaSeasonSubcompetition.Bundesliga,
            rules,
            scoring ?? PredictionScoringIdentityV2.Create("synthetic-scoring-v1", ShaA),
            PredictionResultBasisIdentityV2.Create(
                ResultBasis.RegularTime90Minutes,
                "regular-time-result-v1",
                ShaC),
            Prompt(),
            Model(),
            PredictionCopyPolicyIdentityV2.Create(
                "synthetic-match-copy-policy-v1",
                ShaA,
                MatchRoute,
                MatchRoute,
                targetContext,
                sourceContext));
    }

    internal static PredictionCopyCompatibilityContractV2 BonusCompatibility(
        string context,
        string targetContext,
        string sourceContext,
        PredictionRulesIdentityV2? rules = null)
    {
        rules ??= PredictionRulesIdentityV2.Create("synthetic-rules-v1", ShaB);
        return PredictionCopyCompatibilityContractV2.CreateBonus(
            context,
            BonusRoute,
            BundesligaSeasonSubcompetition.Bundesliga,
            rules,
            PredictionScoringIdentityV2.Create("synthetic-scoring-v1", ShaA),
            Prompt(),
            Model(),
            PredictionCopyPolicyIdentityV2.Create(
                "synthetic-bonus-copy-policy-v1",
                ShaC,
                BonusRoute,
                BonusRoute,
                targetContext,
                sourceContext),
            PredictionOptionMeaningIdentityV2.Create("synthetic-option-meaning-v1", ShaA));
    }

    internal static BundesligaPredictionRouteSelection MatchSelection(
        string id,
        string context,
        PredictionCopyCompatibilityContractV2? compatibility = null,
        PredictionPromptExecutionRequirement? promptRequirement = null) =>
        BundesligaPredictionRouteSelection.Create(
            id,
            MatchRouteContract(),
            context,
            "bundesliga-match-primary-v1",
            GenerationInput(),
            promptRequirement ?? PromptRequirement(),
            compatibility);

    internal static BundesligaPredictionRouteSelection BonusSelection(
        string id,
        string context,
        PredictionCopyCompatibilityContractV2? compatibility = null,
        PredictionPromptExecutionRequirement? promptRequirement = null) =>
        BundesligaPredictionRouteSelection.Create(
            id,
            BonusRouteContract(),
            context,
            "bundesliga-bonus-primary-v1",
            GenerationInput(),
            promptRequirement ?? PromptRequirement(),
            compatibility);

    internal static PredictionGenerationProvenanceV2 MatchProvenance(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> current,
        PredictionRulesIdentityV2 rules,
        string predictionIdentity = "source-match-prediction-r0") =>
        PredictionGenerationProvenanceV2.Create(
            current.Authority,
            "match-predictions-bundesliga-2026-27-typed-v1",
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
            Context(rules),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            predictionIdentity,
            0,
            new PredictionGenerationUsageV2(100, 20, 0.001m));

    internal static PredictionGenerationProvenanceV2 BonusProvenance(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> current,
        PredictionRulesIdentityV2 rules,
        string predictionIdentity = "source-bonus-prediction-r0") =>
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
            Context(rules),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            predictionIdentity,
            0,
            new PredictionGenerationUsageV2(100, 20, 0.001m));

    internal static (
        BundesligaValidatedMatchItem Target,
        BundesligaValidatedMatchItem Source,
        BundesligaCopyBindingGeneration Binding,
        BundesligaPredictionAuthority TargetAuthority,
        BundesligaPredictionAuthority SourceAuthority)
        MatchItems()
    {
        var targetSeed = Seed(TargetCommunity);
        var sourceSeed = Seed(SourceCommunity);
        var binding = Binding(targetSeed, sourceSeed);
        var targetAuthority = CopyAuthority(targetSeed, sourceSeed, binding);
        var sourceAuthority = DirectAuthority(sourceSeed);
        return (
            BundesligaPredictionInventoryGate.ValidateMatches(
                targetAuthority, targetSeed, [MatchKey(TargetCommunity)],
                [Match(TargetCommunity)], Routes()).Items.Single(),
            BundesligaPredictionInventoryGate.ValidateMatches(
                sourceAuthority, sourceSeed, [MatchKey(SourceCommunity)],
                [Match(SourceCommunity)], Routes()).Items.Single(),
            binding,
            targetAuthority,
            sourceAuthority);
    }

    internal static (
        BundesligaValidatedBonusItem Target,
        BundesligaValidatedBonusItem Source,
        BundesligaCopyBindingGeneration Binding)
        BonusItems(IEnumerable<BundesligaBonusOptionProjection>? projection = null)
    {
        var targetSeed = Seed(TargetCommunity);
        var sourceSeed = Seed(SourceCommunity);
        var binding = Binding(targetSeed, sourceSeed, projection);
        var targetAuthority = CopyAuthority(targetSeed, sourceSeed, binding);
        var sourceAuthority = DirectAuthority(sourceSeed);
        return (
            BundesligaPredictionInventoryGate.ValidateBonus(
                targetAuthority, targetSeed, [BonusKey(TargetCommunity)],
                [Bonus(TargetCommunity)], Routes()).Items.Single(),
            BundesligaPredictionInventoryGate.ValidateBonus(
                sourceAuthority, sourceSeed, [BonusKey(SourceCommunity)],
                [Bonus(SourceCommunity)], Routes()).Items.Single(),
            binding);
    }
}
