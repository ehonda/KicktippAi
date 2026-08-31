using System.Collections.Immutable;

namespace EHonda.KicktippAi.Core;

public enum PredictionCopyCompatibilityV2Failure
{
    None,
    AuthorityMismatch,
    BindingIdentityMismatch,
    SnapshotHashMismatch,
    RouteMismatch,
    SubcompetitionMismatch,
    ResultBasisMismatch,
    MatchSemanticsMismatch,
    SelectionLimitMismatch,
    RulesIdentityMismatch,
    ScoringIdentityMismatch,
    PromptModelIdentityMismatch,
    CopyPolicyIdentityMismatch,
    OptionMeaningIdentityMismatch
}

public sealed record PredictionRulesIdentityV2
{
    private PredictionRulesIdentityV2(string identity, string sha256) => (Identity, Sha256) = (identity, sha256);
    public string Identity { get; }
    public string Sha256 { get; }
    public static PredictionRulesIdentityV2 Create(string identity, string sha256)
    {
        BundesligaPredictionContractValidation.Identifier(identity, nameof(identity));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new(identity, sha256);
    }
}

public sealed record PredictionScoringIdentityV2
{
    private PredictionScoringIdentityV2(string identity, string sha256) => (Identity, Sha256) = (identity, sha256);
    public string Identity { get; }
    public string Sha256 { get; }
    public static PredictionScoringIdentityV2 Create(string identity, string sha256)
    {
        BundesligaPredictionContractValidation.Identifier(identity, nameof(identity));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new(identity, sha256);
    }
}

public sealed record PredictionResultBasisIdentityV2
{
    private PredictionResultBasisIdentityV2(ResultBasis resultBasis, string identity, string sha256) =>
        (ResultBasis, Identity, Sha256) = (resultBasis, identity, sha256);
    public ResultBasis ResultBasis { get; }
    public string Identity { get; }
    public string Sha256 { get; }
    public static PredictionResultBasisIdentityV2 Create(ResultBasis resultBasis, string identity, string sha256)
    {
        BundesligaPredictionContractValidation.EnumValue(resultBasis, nameof(resultBasis));
        BundesligaPredictionContractValidation.Identifier(identity, nameof(identity));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new(resultBasis, identity, sha256);
    }
}

public sealed record PredictionCopyPolicyIdentityV2
{
    private PredictionCopyPolicyIdentityV2(
        string identity, string sha256, string targetRouteId, string sourceRouteId,
        string targetCommunityContext, string sourceCommunityContext) =>
        (Identity, Sha256, TargetRouteId, SourceRouteId, TargetCommunityContext, SourceCommunityContext) =
        (identity, sha256, targetRouteId, sourceRouteId, targetCommunityContext, sourceCommunityContext);
    public string Identity { get; }
    public string Sha256 { get; }
    public string TargetRouteId { get; }
    public string SourceRouteId { get; }
    public string TargetCommunityContext { get; }
    public string SourceCommunityContext { get; }
    public static PredictionCopyPolicyIdentityV2 Create(
        string identity, string sha256, string targetRouteId, string sourceRouteId,
        string targetCommunityContext, string sourceCommunityContext)
    {
        BundesligaPredictionContractValidation.Identifier(identity, nameof(identity));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        BundesligaPredictionContractValidation.Identifier(targetRouteId, nameof(targetRouteId));
        BundesligaPredictionContractValidation.Identifier(sourceRouteId, nameof(sourceRouteId));
        BundesligaPredictionContractValidation.Community(targetCommunityContext, nameof(targetCommunityContext));
        BundesligaPredictionContractValidation.Community(sourceCommunityContext, nameof(sourceCommunityContext));
        return new(identity, sha256, targetRouteId, sourceRouteId, targetCommunityContext, sourceCommunityContext);
    }
}

public sealed record PredictionOptionMeaningIdentityV2
{
    private PredictionOptionMeaningIdentityV2(string identity, string sha256) => (Identity, Sha256) = (identity, sha256);
    public string Identity { get; }
    public string Sha256 { get; }
    public static PredictionOptionMeaningIdentityV2 Create(string identity, string sha256)
    {
        BundesligaPredictionContractValidation.Identifier(identity, nameof(identity));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new(identity, sha256);
    }
}

/// <summary>Immutable typed semantics for one community's side of a copy.</summary>
public sealed record PredictionCopyCompatibilityContractV2
{
    private PredictionCopyCompatibilityContractV2(
        string communityContext,
        string routeId,
        BundesligaPredictionItemKind itemKind,
        BundesligaSeasonSubcompetition subcompetition,
        PredictionRulesIdentityV2 rules,
        PredictionScoringIdentityV2 scoring,
        PredictionResultBasisIdentityV2? resultBasis,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig model,
        PredictionCopyPolicyIdentityV2 copyPolicy,
        PredictionOptionMeaningIdentityV2? optionMeaning)
    {
        CommunityContext = communityContext;
        RouteId = routeId;
        ItemKind = itemKind;
        Subcompetition = subcompetition;
        Rules = rules;
        Scoring = scoring;
        ResultBasis = resultBasis;
        Prompt = prompt;
        Model = model;
        CopyPolicy = copyPolicy;
        OptionMeaning = optionMeaning;
    }

    public string CommunityContext { get; }
    public string RouteId { get; }
    public BundesligaPredictionItemKind ItemKind { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }
    public PredictionRulesIdentityV2 Rules { get; }
    public PredictionScoringIdentityV2 Scoring { get; }
    public PredictionResultBasisIdentityV2? ResultBasis { get; }
    public PredictionPromptProvenanceV2 Prompt { get; }
    public PredictionModelConfig Model { get; }
    public PredictionCopyPolicyIdentityV2 CopyPolicy { get; }
    public PredictionOptionMeaningIdentityV2? OptionMeaning { get; }

    public static PredictionCopyCompatibilityContractV2 CreateMatch(
        string communityContext,
        string routeId,
        BundesligaSeasonSubcompetition subcompetition,
        PredictionRulesIdentityV2 rules,
        PredictionScoringIdentityV2 scoring,
        PredictionResultBasisIdentityV2 resultBasis,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig model,
        PredictionCopyPolicyIdentityV2 copyPolicy)
    {
        ValidateCommon(communityContext, routeId, BundesligaPredictionItemKind.Match, subcompetition,
            rules, scoring, prompt, model, copyPolicy);
        ArgumentNullException.ThrowIfNull(resultBasis);
        return new(communityContext, routeId, BundesligaPredictionItemKind.Match, subcompetition,
            rules, scoring, resultBasis, prompt, model, copyPolicy, null);
    }

    public static PredictionCopyCompatibilityContractV2 CreateBonus(
        string communityContext,
        string routeId,
        BundesligaSeasonSubcompetition subcompetition,
        PredictionRulesIdentityV2 rules,
        PredictionScoringIdentityV2 scoring,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig model,
        PredictionCopyPolicyIdentityV2 copyPolicy,
        PredictionOptionMeaningIdentityV2 optionMeaning)
    {
        ValidateCommon(communityContext, routeId, BundesligaPredictionItemKind.Bonus, subcompetition,
            rules, scoring, prompt, model, copyPolicy);
        ArgumentNullException.ThrowIfNull(optionMeaning);
        return new(communityContext, routeId, BundesligaPredictionItemKind.Bonus, subcompetition,
            rules, scoring, null, prompt, model, copyPolicy, optionMeaning);
    }

    private static void ValidateCommon(
        string communityContext,
        string routeId,
        BundesligaPredictionItemKind itemKind,
        BundesligaSeasonSubcompetition subcompetition,
        PredictionRulesIdentityV2 rules,
        PredictionScoringIdentityV2 scoring,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig model,
        PredictionCopyPolicyIdentityV2 copyPolicy)
    {
        BundesligaPredictionContractValidation.Community(communityContext, nameof(communityContext));
        BundesligaPredictionContractValidation.Identifier(routeId, nameof(routeId));
        BundesligaPredictionContractValidation.EnumValue(itemKind, nameof(itemKind));
        BundesligaPredictionContractValidation.EnumValue(subcompetition, nameof(subcompetition));
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(copyPolicy);
    }
}

/// <summary>Complete immutable compatibility input, including both authorities.</summary>
public sealed class PredictionCopyCompatibilityV2Input<TSnapshot>
    where TSnapshot : class
{
    private PredictionCopyCompatibilityV2Input(
        BundesligaTypedCurrentRequest<TSnapshot> targetCurrent,
        BundesligaTypedCurrentRequest<TSnapshot> sourceCurrent,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaCopyBindingGeneration binding,
        BundesligaCopyBindingEntry bindingEntry,
        PredictionCopyCompatibilityContractV2 targetContract,
        PredictionCopyCompatibilityContractV2 sourceContract)
    {
        TargetCurrent = targetCurrent;
        SourceCurrent = sourceCurrent;
        PostingSeed = postingSeed;
        SourceSeed = sourceSeed;
        Binding = binding;
        BindingEntry = bindingEntry;
        TargetContract = targetContract;
        SourceContract = sourceContract;
    }

    public BundesligaTypedCurrentRequest<TSnapshot> TargetCurrent { get; }
    public BundesligaTypedCurrentRequest<TSnapshot> SourceCurrent { get; }
    public BundesligaIdentitySeedGeneration PostingSeed { get; }
    public BundesligaIdentitySeedGeneration SourceSeed { get; }
    public BundesligaCopyBindingGeneration Binding { get; }
    public BundesligaCopyBindingEntry BindingEntry { get; }
    public PredictionCopyCompatibilityContractV2 TargetContract { get; }
    public PredictionCopyCompatibilityContractV2 SourceContract { get; }

    public static PredictionCopyCompatibilityV2Input<TSnapshot> Create(
        BundesligaTypedCurrentRequest<TSnapshot> targetCurrent,
        BundesligaTypedCurrentRequest<TSnapshot> sourceCurrent,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaCopyBindingGeneration binding,
        BundesligaCopyBindingEntry bindingEntry,
        PredictionCopyCompatibilityContractV2 targetContract,
        PredictionCopyCompatibilityContractV2 sourceContract)
    {
        ArgumentNullException.ThrowIfNull(targetCurrent);
        ArgumentNullException.ThrowIfNull(sourceCurrent);
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(sourceSeed);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(bindingEntry);
        ArgumentNullException.ThrowIfNull(targetContract);
        ArgumentNullException.ThrowIfNull(sourceContract);

        var target = targetCurrent.Authority;
        var source = sourceCurrent.Authority;
        if (target.Mode != BundesligaPredictionAuthorityMode.Copy
            || source.Mode != BundesligaPredictionAuthorityMode.Direct
            || target.CopyBinding != binding.Reference
            || target.PostingSeed != postingSeed.Reference
            || target.SourceSeed != sourceSeed.Reference
            || target.PostingSeed != binding.PostingSeed
            || target.SourceSeed != binding.SourceSeed
            || source.PostingSeed != binding.SourceSeed
            || !string.Equals(target.PostingCommunity, binding.PostingCommunity, StringComparison.Ordinal)
            || !string.Equals(target.PredictionSourceCommunity, binding.SourceCommunity, StringComparison.Ordinal)
            || !string.Equals(source.PostingCommunity, binding.SourceCommunity, StringComparison.Ordinal)
            || !string.Equals(target.PredictionSourceCommunity, source.PostingCommunity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Copy compatibility input has incomplete or conflicting source/target authority.");
        }

        if (!binding.Entries.Contains(bindingEntry))
        {
            throw new InvalidDataException("Copy compatibility entry is not part of the exact binding generation.");
        }

        var targetSnapshot = Snapshot(targetCurrent.Snapshot);
        var sourceSnapshot = Snapshot(sourceCurrent.Snapshot);
        var postingSeedEntry = postingSeed.RequireEntry(targetSnapshot.Key);
        var sourceSeedEntry = sourceSeed.RequireEntry(sourceSnapshot.Key);
        if (bindingEntry.PostingKey != targetSnapshot.Key
            || bindingEntry.PostingSnapshotHash != targetSnapshot.Hash
            || bindingEntry.SourceKey != sourceSnapshot.Key
            || bindingEntry.SourceSnapshotHash != sourceSnapshot.Hash)
        {
            throw new InvalidDataException("Copy compatibility snapshots do not match the exact binding entry.");
        }

        if (!string.Equals(postingSeedEntry.RouteId, bindingEntry.RouteId, StringComparison.Ordinal)
            || !string.Equals(postingSeedEntry.RouteId, targetCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(sourceSeedEntry.RouteId, sourceCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(targetContract.RouteId, targetCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(sourceContract.RouteId, sourceCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(targetContract.CopyPolicy.TargetRouteId, targetCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(targetContract.CopyPolicy.SourceRouteId, sourceCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(targetContract.CopyPolicy.TargetCommunityContext, target.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(targetContract.CopyPolicy.SourceCommunityContext, source.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(sourceContract.CopyPolicy.TargetRouteId, targetCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(sourceContract.CopyPolicy.SourceRouteId, sourceCurrent.Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(sourceContract.CopyPolicy.TargetCommunityContext, target.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(sourceContract.CopyPolicy.SourceCommunityContext, source.CommunityContext, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Copy compatibility route relationship is not exact.");
        }

        if (!string.Equals(targetContract.CommunityContext, target.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(sourceContract.CommunityContext, source.CommunityContext, StringComparison.Ordinal)
            || targetContract.Model != targetCurrent.ModelConfig
            || sourceContract.Model != sourceCurrent.ModelConfig
            || targetContract.ItemKind != targetSnapshot.ItemKind
            || sourceContract.ItemKind != sourceSnapshot.ItemKind
            || targetContract.Subcompetition != targetSnapshot.Subcompetition
            || sourceContract.Subcompetition != sourceSnapshot.Subcompetition)
        {
            throw new InvalidDataException("Copy compatibility contracts do not match their exact authority/current identities.");
        }

        return new(targetCurrent, sourceCurrent, postingSeed, sourceSeed, binding, bindingEntry, targetContract, sourceContract);
    }

    private static (StableLocalItemKey Key, BundesligaPredictionSnapshotHash Hash,
        BundesligaPredictionItemKind ItemKind, BundesligaSeasonSubcompetition Subcompetition) Snapshot(TSnapshot snapshot) =>
        snapshot switch
        {
            TypedMatchSnapshot match => (match.Key, match.SnapshotHash, BundesligaPredictionItemKind.Match, match.Subcompetition),
            TypedBonusSnapshot bonus => (bonus.Key, bonus.SnapshotHash, BundesligaPredictionItemKind.Bonus, bonus.Subcompetition),
            _ => throw new InvalidDataException("Unsupported copy snapshot type.")
        };
}

public sealed class PredictionCopyCompatibilityV2Decision
{
    private readonly ImmutableArray<BundesligaBonusOptionProjection> _optionProjection;

    private PredictionCopyCompatibilityV2Decision(
        bool succeeded,
        PredictionCopyCompatibilityV2Failure failure,
        string boundFingerprint,
        BundesligaCopyBindingReference binding,
        string bindingEntrySha256,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
    {
        Succeeded = succeeded;
        Failure = failure;
        BoundFingerprint = boundFingerprint;
        Binding = binding;
        BindingEntrySha256 = bindingEntrySha256;
        _optionProjection = optionProjection.ToImmutableArray();
    }

    public bool Succeeded { get; }
    public PredictionCopyCompatibilityV2Failure Failure { get; }
    public string BoundFingerprint { get; }
    public BundesligaCopyBindingReference Binding { get; }
    public string BindingEntrySha256 { get; }
    public IReadOnlyList<BundesligaBonusOptionProjection> OptionProjection => _optionProjection;

    internal bool IsBoundTo<TSnapshot>(PredictionCopyCompatibilityV2Input<TSnapshot> input)
        where TSnapshot : class =>
        Binding == input.Binding.Reference
        && string.Equals(BindingEntrySha256, PredictionCopyCompatibilityV2.EntryFingerprint(input.BindingEntry), StringComparison.Ordinal)
        && string.Equals(BoundFingerprint, PredictionCopyCompatibilityV2.InputFingerprint(input), StringComparison.Ordinal);

    internal static PredictionCopyCompatibilityV2Decision Create<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        PredictionCopyCompatibilityV2Failure failure,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
        where TSnapshot : class =>
        new(failure == PredictionCopyCompatibilityV2Failure.None, failure,
            PredictionCopyCompatibilityV2.InputFingerprint(input), input.Binding.Reference,
            PredictionCopyCompatibilityV2.EntryFingerprint(input.BindingEntry), optionProjection);
}

public static class PredictionCopyCompatibilityV2
{
    public static PredictionCopyCompatibilityV2Decision Evaluate<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input)
        where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(input);
        var failure = ContractFailure(input.TargetContract, input.SourceContract);
        if (failure != PredictionCopyCompatibilityV2Failure.None)
        {
            return PredictionCopyCompatibilityV2Decision.Create(input, failure, []);
        }

        return (input.TargetCurrent.Snapshot, input.SourceCurrent.Snapshot) switch
        {
            (TypedMatchSnapshot target, TypedMatchSnapshot source) => EvaluateMatch(input, target, source),
            (TypedBonusSnapshot target, TypedBonusSnapshot source) => EvaluateBonus(input, target, source),
            _ => throw new InvalidDataException("Copy compatibility requires snapshots of one exact typed item kind.")
        };
    }

    private static PredictionCopyCompatibilityV2Decision EvaluateMatch<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        TypedMatchSnapshot target,
        TypedMatchSnapshot source)
        where TSnapshot : class
    {
        var targetBasis = input.TargetContract.ResultBasis!;
        var sourceBasis = input.SourceContract.ResultBasis!;
        if (targetBasis != sourceBasis
            || targetBasis.ResultBasis != target.ResultBasis
            || sourceBasis.ResultBasis != source.ResultBasis)
        {
            return PredictionCopyCompatibilityV2Decision.Create(input, PredictionCopyCompatibilityV2Failure.ResultBasisMismatch, []);
        }

        if (!string.Equals(target.HomeTeam, source.HomeTeam, StringComparison.Ordinal)
            || !string.Equals(target.AwayTeam, source.AwayTeam, StringComparison.Ordinal)
            || !string.Equals(target.ExactRound, source.ExactRound, StringComparison.Ordinal))
        {
            return PredictionCopyCompatibilityV2Decision.Create(input, PredictionCopyCompatibilityV2Failure.MatchSemanticsMismatch, []);
        }

        return PredictionCopyCompatibilityV2Decision.Create(input, PredictionCopyCompatibilityV2Failure.None, []);
    }

    private static PredictionCopyCompatibilityV2Decision EvaluateBonus<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        TypedBonusSnapshot target,
        TypedBonusSnapshot source)
        where TSnapshot : class
    {
        if (target.MaxSelections != source.MaxSelections)
        {
            return PredictionCopyCompatibilityV2Decision.Create(input, PredictionCopyCompatibilityV2Failure.SelectionLimitMismatch, []);
        }

        BundesligaCopyBindingEntry.EnsureExactProjection(
            source.Options.Select(option => option.Id),
            target.Options.Select(option => option.Id),
            input.BindingEntry.OptionProjection.ToArray());
        return PredictionCopyCompatibilityV2Decision.Create(
            input, PredictionCopyCompatibilityV2Failure.None, input.BindingEntry.OptionProjection);
    }

    private static PredictionCopyCompatibilityV2Failure ContractFailure(
        PredictionCopyCompatibilityContractV2 target,
        PredictionCopyCompatibilityContractV2 source)
    {
        if (target.Subcompetition != source.Subcompetition)
        {
            return PredictionCopyCompatibilityV2Failure.SubcompetitionMismatch;
        }
        if (target.Rules != source.Rules)
        {
            return PredictionCopyCompatibilityV2Failure.RulesIdentityMismatch;
        }
        if (target.Scoring != source.Scoring)
        {
            return PredictionCopyCompatibilityV2Failure.ScoringIdentityMismatch;
        }
        if (target.Prompt != source.Prompt || target.Model != source.Model)
        {
            return PredictionCopyCompatibilityV2Failure.PromptModelIdentityMismatch;
        }
        if (target.CopyPolicy != source.CopyPolicy)
        {
            return PredictionCopyCompatibilityV2Failure.CopyPolicyIdentityMismatch;
        }
        if (target.ItemKind == BundesligaPredictionItemKind.Bonus && target.OptionMeaning != source.OptionMeaning)
        {
            return PredictionCopyCompatibilityV2Failure.OptionMeaningIdentityMismatch;
        }
        return PredictionCopyCompatibilityV2Failure.None;
    }

    internal static string EntryFingerprint(BundesligaCopyBindingEntry entry) =>
        BundesligaPredictionCanonicalJson.Sha256(BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("routeId", entry.RouteId);
            WriteKey(writer, "postingKey", entry.PostingKey);
            writer.WriteString("postingSnapshotHash", entry.PostingSnapshotHash.Sha256);
            WriteSeed(writer, "postingSeed", entry.PostingSeed);
            WriteKey(writer, "sourceKey", entry.SourceKey);
            writer.WriteString("sourceSnapshotHash", entry.SourceSnapshotHash.Sha256);
            WriteSeed(writer, "sourceSeed", entry.SourceSeed);
            writer.WritePropertyName("optionProjection");
            writer.WriteStartArray();
            foreach (var option in entry.OptionProjection)
            {
                writer.WriteStartObject();
                writer.WriteString("source", option.SourceOptionId);
                writer.WriteString("posting", option.PostingOptionId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }));

    internal static string InputFingerprint<TSnapshot>(PredictionCopyCompatibilityV2Input<TSnapshot> input)
        where TSnapshot : class =>
        BundesligaPredictionCanonicalJson.Sha256(BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            WriteCurrent(writer, "target", input.TargetCurrent);
            WriteCurrent(writer, "source", input.SourceCurrent);
            writer.WriteString("postingSeedHash", input.PostingSeed.CanonicalSha256);
            writer.WriteString("sourceSeedHash", input.SourceSeed.CanonicalSha256);
            writer.WriteNumber("bindingGeneration", input.Binding.Generation);
            writer.WriteString("bindingHash", input.Binding.CanonicalSha256);
            writer.WriteString("bindingEntryHash", EntryFingerprint(input.BindingEntry));
            WriteContract(writer, "targetContract", input.TargetContract);
            WriteContract(writer, "sourceContract", input.SourceContract);
            writer.WriteEndObject();
        }));

    private static void WriteCurrent<TSnapshot>(System.Text.Json.Utf8JsonWriter writer, string name,
        BundesligaTypedCurrentRequest<TSnapshot> current) where TSnapshot : class
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        WriteAuthority(writer, current.Authority);
        var (key, _, _) = BundesligaTypedCurrentRequest<TSnapshot>.SnapshotIdentity(current.Snapshot);
        WriteKey(writer, "key", key);
        writer.WriteString("snapshotHash", current.Snapshot switch
        {
            TypedMatchSnapshot match => match.SnapshotHash.Sha256,
            TypedBonusSnapshot bonus => bonus.SnapshotHash.Sha256,
            _ => throw new InvalidDataException("Unsupported current snapshot.")
        });
        writer.WriteString("routeId", current.Identity.RouteId);
        writer.WriteString("profileId", current.Identity.ProfileId);
        writer.WriteString("generationInputId", current.Identity.GenerationInputContract.ContractId);
        writer.WriteString("generationInputHash", current.Identity.GenerationInputContract.Sha256);
        writer.WriteString("model", current.ModelConfig.IdentityKey);
        writer.WriteEndObject();
    }

    private static void WriteAuthority(System.Text.Json.Utf8JsonWriter writer, BundesligaPredictionAuthority authority)
    {
        writer.WriteString("mode", authority.Mode.ToString());
        writer.WriteString("season", authority.SeasonPartition);
        writer.WriteString("epoch", authority.AuthorityEpoch);
        writer.WriteString("posting", authority.PostingCommunity);
        writer.WriteString("source", authority.PredictionSourceCommunity);
        writer.WriteString("context", authority.CommunityContext);
        WriteSeed(writer, "postingSeed", authority.PostingSeed);
        WriteSeed(writer, "sourceSeed", authority.SourceSeed);
        writer.WriteString("copyBinding", authority.CopyBinding is null
            ? null : $"{authority.CopyBinding.Generation}:{authority.CopyBinding.Sha256}");
    }

    private static void WriteContract(System.Text.Json.Utf8JsonWriter writer, string name,
        PredictionCopyCompatibilityContractV2 contract)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("context", contract.CommunityContext);
        writer.WriteString("route", contract.RouteId);
        writer.WriteString("kind", contract.ItemKind.ToString());
        writer.WriteString("subcompetition", contract.Subcompetition.ToSerializedValue());
        writer.WriteString("rules", $"{contract.Rules.Identity}:{contract.Rules.Sha256}");
        writer.WriteString("scoring", $"{contract.Scoring.Identity}:{contract.Scoring.Sha256}");
        writer.WriteString("resultBasis", contract.ResultBasis is null ? null
            : $"{contract.ResultBasis.ResultBasis.ToSerializedValue()}:{contract.ResultBasis.Identity}:{contract.ResultBasis.Sha256}");
        writer.WriteString("prompt", $"{contract.Prompt.ActualSource}:{contract.Prompt.HostedName}:{contract.Prompt.HostedVersion}:{contract.Prompt.HostedNormalizedReadbackSha256}:{contract.Prompt.RequiredLabel}:{contract.Prompt.ActualFallbackFile}:{contract.Prompt.ActualFallbackSha256}");
        writer.WriteString("model", contract.Model.IdentityKey);
        writer.WriteString("copyPolicy", $"{contract.CopyPolicy.Identity}:{contract.CopyPolicy.Sha256}:{contract.CopyPolicy.TargetRouteId}:{contract.CopyPolicy.SourceRouteId}:{contract.CopyPolicy.TargetCommunityContext}:{contract.CopyPolicy.SourceCommunityContext}");
        writer.WriteString("optionMeaning", contract.OptionMeaning is null ? null
            : $"{contract.OptionMeaning.Identity}:{contract.OptionMeaning.Sha256}");
        writer.WriteEndObject();
    }

    private static void WriteKey(System.Text.Json.Utf8JsonWriter writer, string name, StableLocalItemKey key)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("season", key.SeasonPartition);
        writer.WriteString("community", key.PostingCommunity);
        writer.WriteString("kind", key.ItemKind.ToString());
        writer.WriteString("id", key.KicktippItemId);
        writer.WriteEndObject();
    }

    private static void WriteSeed(System.Text.Json.Utf8JsonWriter writer, string name, BundesligaIdentitySeedReference seed)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteNumber("generation", seed.Generation);
        writer.WriteString("sha256", seed.Sha256);
        writer.WriteEndObject();
    }
}
