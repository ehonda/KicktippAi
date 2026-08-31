using System.Collections.Immutable;
using System.Text.Json;

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
        if (!string.Equals(prompt.HostedName, model.PromptName, StringComparison.Ordinal)
            || prompt.HostedVersion != model.PromptVersion)
        {
            throw new InvalidDataException(
                "Compatibility prompt name/version must equal the pinned model prompt name/version.");
        }
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

public sealed class PredictionCopyCompatibilityV2CanonicalInput
{
    public const string SchemaVersionValue = "prediction-copy-compatibility-input-v2";
    private readonly byte[] _canonicalBytes;

    private PredictionCopyCompatibilityV2CanonicalInput(byte[] canonicalBytes)
    {
        _canonicalBytes = canonicalBytes.ToArray();
        Sha256 = BundesligaPredictionCanonicalJson.Sha256(_canonicalBytes);
    }

    public string SchemaVersion => SchemaVersionValue;
    public string Sha256 { get; }
    public byte[] SerializeCanonical() => _canonicalBytes.ToArray();

    internal static PredictionCopyCompatibilityV2CanonicalInput Create<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input) where TSnapshot : class =>
        new(PredictionCopyCompatibilityV2.SerializeCanonicalInput(input));

    public static PredictionCopyCompatibilityV2CanonicalInput DeserializeCanonical(ReadOnlySpan<byte> bytes)
    {
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Copy compatibility canonical input");
        ValidateRoot(document.RootElement);
        var canonical = BundesligaPredictionCanonicalJson.Write(writer => document.RootElement.WriteTo(writer));
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, canonical, "Copy compatibility canonical input");
        return new PredictionCopyCompatibilityV2CanonicalInput(canonical);
    }

    private static void ValidateRoot(JsonElement root)
    {
        BundesligaPredictionCanonicalJson.Properties(root,
            "schemaVersion", "targetCurrent", "sourceCurrent", "postingSeed", "sourceSeed",
            "binding", "bindingEntry", "targetContract", "sourceContract");
        if (!string.Equals(BundesligaPredictionCanonicalJson.String(root, "schemaVersion"), SchemaVersionValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unknown copy compatibility canonical input schema.");
        }
        ValidateCurrent(root.GetProperty("targetCurrent"));
        ValidateCurrent(root.GetProperty("sourceCurrent"));
        ValidateSeed(root.GetProperty("postingSeed"));
        ValidateSeed(root.GetProperty("sourceSeed"));
        ValidateBinding(root.GetProperty("binding"));
        ValidateEntry(root.GetProperty("bindingEntry"));
        ValidateContract(root.GetProperty("targetContract"));
        ValidateContract(root.GetProperty("sourceContract"));
    }

    private static void ValidateCurrent(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "authority", "key", "snapshotHash", "routeId", "profileId", "generationInputContract", "model");
        ValidateAuthority(value.GetProperty("authority"));
        ValidateKey(value.GetProperty("key"));
        ValidateSnapshotHash(value.GetProperty("snapshotHash"));
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(value, "routeId"), "routeId");
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(value, "profileId"), "profileId");
        ValidateContractIdentity(value.GetProperty("generationInputContract"), "contractId");
        ValidateModel(value.GetProperty("model"));
    }

    private static void ValidateAuthority(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "seasonPartition", "authorityEpoch", "mode", "postingCommunity",
            "predictionSourceCommunity", "communityContext", "postingSeed", "sourceSeed", "copyBinding");
        if (!string.Equals(BundesligaPredictionCanonicalJson.String(value, "seasonPartition"), BundesligaPredictionAuthority.SeasonPartitionValue, StringComparison.Ordinal)
            || !string.Equals(BundesligaPredictionCanonicalJson.String(value, "authorityEpoch"), BundesligaPredictionAuthority.AuthorityEpochValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Canonical authority has the wrong fixed scope.");
        }
        _ = BundesligaPredictionCanonicalJson.ParseAuthorityMode(BundesligaPredictionCanonicalJson.String(value, "mode"));
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(value, "postingCommunity"), "postingCommunity");
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(value, "predictionSourceCommunity"), "predictionSourceCommunity");
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(value, "communityContext"), "communityContext");
        ValidateSeed(value.GetProperty("postingSeed"));
        ValidateSeed(value.GetProperty("sourceSeed"));
        ValidateNullableReference(value.GetProperty("copyBinding"));
    }

    private static void ValidateKey(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value, "seasonPartition", "postingCommunity", "itemKind", "kicktippItemId");
        _ = StableLocalItemKey.Create(
            BundesligaPredictionCanonicalJson.String(value, "seasonPartition"),
            BundesligaPredictionCanonicalJson.String(value, "postingCommunity"),
            BundesligaPredictionCanonicalJson.ParseItemKind(BundesligaPredictionCanonicalJson.String(value, "itemKind")),
            BundesligaPredictionCanonicalJson.String(value, "kicktippItemId"));
    }

    private static void ValidateSnapshotHash(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value, "schemaVersion", "sha256");
        _ = BundesligaPredictionSnapshotHash.Create(
            BundesligaPredictionCanonicalJson.String(value, "schemaVersion"),
            BundesligaPredictionCanonicalJson.String(value, "sha256"));
    }

    private static void ValidateSeed(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value, "generation", "sha256");
        _ = BundesligaIdentitySeedReference.Create(
            BundesligaPredictionCanonicalJson.Int32(value, "generation"),
            BundesligaPredictionCanonicalJson.String(value, "sha256"));
    }

    private static void ValidateNullableReference(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return;
        BundesligaPredictionCanonicalJson.Properties(value, "generation", "sha256");
        _ = BundesligaCopyBindingReference.Create(
            BundesligaPredictionCanonicalJson.Int32(value, "generation"),
            BundesligaPredictionCanonicalJson.String(value, "sha256"));
    }

    private static void ValidateBinding(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value, "generation", "sha256");
        _ = BundesligaCopyBindingReference.Create(
            BundesligaPredictionCanonicalJson.Int32(value, "generation"),
            BundesligaPredictionCanonicalJson.String(value, "sha256"));
    }

    private static void ValidateEntry(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "routeId", "postingKey", "postingSnapshotHash", "postingSeed",
            "sourceKey", "sourceSnapshotHash", "sourceSeed", "optionProjection");
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(value, "routeId"), "routeId");
        ValidateKey(value.GetProperty("postingKey"));
        ValidateSnapshotHash(value.GetProperty("postingSnapshotHash"));
        ValidateSeed(value.GetProperty("postingSeed"));
        ValidateKey(value.GetProperty("sourceKey"));
        ValidateSnapshotHash(value.GetProperty("sourceSnapshotHash"));
        ValidateSeed(value.GetProperty("sourceSeed"));
        var options = value.GetProperty("optionProjection");
        if (options.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Option projection must be an array.");
        foreach (var option in options.EnumerateArray())
        {
            BundesligaPredictionCanonicalJson.Properties(option, "sourceOptionId", "postingOptionId");
            _ = new BundesligaBonusOptionProjection(
                BundesligaPredictionCanonicalJson.String(option, "sourceOptionId"),
                BundesligaPredictionCanonicalJson.String(option, "postingOptionId"));
        }
    }

    private static void ValidateContract(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "communityContext", "routeId", "itemKind", "subcompetition", "rules", "scoring",
            "resultBasis", "prompt", "model", "copyPolicy", "optionMeaning");
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(value, "communityContext"), "communityContext");
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(value, "routeId"), "routeId");
        _ = BundesligaPredictionCanonicalJson.ParseItemKind(BundesligaPredictionCanonicalJson.String(value, "itemKind"));
        if (!BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(
            BundesligaPredictionCanonicalJson.String(value, "subcompetition"), out _))
        {
            throw new InvalidDataException("Unknown compatibility subcompetition.");
        }
        ValidateContractIdentity(value.GetProperty("rules"), "identity");
        ValidateContractIdentity(value.GetProperty("scoring"), "identity");
        var basis = value.GetProperty("resultBasis");
        if (basis.ValueKind != JsonValueKind.Null)
        {
            BundesligaPredictionCanonicalJson.Properties(basis, "resultBasis", "identity", "sha256");
            if (!BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(
                BundesligaPredictionCanonicalJson.String(basis, "resultBasis"), out _))
            {
                throw new InvalidDataException("Unknown compatibility result basis.");
            }
            ValidateIdentityFields(basis, "identity");
        }
        ValidatePrompt(value.GetProperty("prompt"));
        ValidateModel(value.GetProperty("model"));
        var policy = value.GetProperty("copyPolicy");
        BundesligaPredictionCanonicalJson.Properties(policy,
            "identity", "sha256", "targetRouteId", "sourceRouteId", "targetCommunityContext", "sourceCommunityContext");
        ValidateIdentityFields(policy, "identity");
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(policy, "targetRouteId"), "targetRouteId");
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(policy, "sourceRouteId"), "sourceRouteId");
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(policy, "targetCommunityContext"), "targetCommunityContext");
        BundesligaPredictionContractValidation.Community(BundesligaPredictionCanonicalJson.String(policy, "sourceCommunityContext"), "sourceCommunityContext");
        var optionMeaning = value.GetProperty("optionMeaning");
        if (optionMeaning.ValueKind != JsonValueKind.Null) ValidateContractIdentity(optionMeaning, "identity");
    }

    private static void ValidatePrompt(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "actualSource", "hostedName", "hostedVersion", "hostedNormalizedReadbackSha256",
            "requiredLabel", "requiredLabelMembership", "actualFallbackFile", "actualFallbackSha256");
        var source = BundesligaPredictionCanonicalJson.String(value, "actualSource") switch
        {
            "hosted" => PredictionPromptSourceV2.Hosted,
            "checked-in-fallback" => PredictionPromptSourceV2.CheckedInFallback,
            _ => throw new InvalidDataException("Unknown prompt source.")
        };
        _ = PredictionPromptProvenanceV2.Create(source,
            BundesligaPredictionCanonicalJson.String(value, "hostedName"),
            BundesligaPredictionCanonicalJson.Int32(value, "hostedVersion"),
            BundesligaPredictionCanonicalJson.String(value, "hostedNormalizedReadbackSha256"),
            BundesligaPredictionCanonicalJson.String(value, "requiredLabel"),
            BundesligaPredictionCanonicalJson.Boolean(value, "requiredLabelMembership"),
            BundesligaPredictionCanonicalJson.NullableString(value, "actualFallbackFile"),
            BundesligaPredictionCanonicalJson.NullableString(value, "actualFallbackSha256"));
    }

    private static void ValidateModel(JsonElement value)
    {
        BundesligaPredictionCanonicalJson.Properties(value,
            "model", "reasoningEffort", "maxOutputTokenCount", "promptName", "promptVersion");
        _ = PredictionModelConfig.Create(
            BundesligaPredictionCanonicalJson.String(value, "model"),
            BundesligaPredictionCanonicalJson.String(value, "reasoningEffort"),
            BundesligaPredictionCanonicalJson.Int32(value, "maxOutputTokenCount"),
            BundesligaPredictionCanonicalJson.String(value, "promptName"),
            BundesligaPredictionCanonicalJson.Int32(value, "promptVersion"));
    }

    private static void ValidateContractIdentity(JsonElement value, string identityName)
    {
        BundesligaPredictionCanonicalJson.Properties(value, identityName, "sha256");
        ValidateIdentityFields(value, identityName);
    }

    private static void ValidateIdentityFields(JsonElement value, string identityName)
    {
        BundesligaPredictionContractValidation.Identifier(BundesligaPredictionCanonicalJson.String(value, identityName), identityName);
        BundesligaPredictionContractValidation.Sha256(BundesligaPredictionCanonicalJson.String(value, "sha256"), "sha256");
    }
}

public sealed class PredictionCopyCompatibilityV2Decision
{
    private readonly ImmutableArray<BundesligaBonusOptionProjection> _optionProjection;

    private PredictionCopyCompatibilityV2Decision(
        bool succeeded,
        PredictionCopyCompatibilityV2Failure failure,
        PredictionCopyCompatibilityV2CanonicalInput canonicalInput,
        BundesligaCopyBindingReference binding,
        string bindingEntrySha256,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
    {
        Succeeded = succeeded;
        Failure = failure;
        CanonicalInput = canonicalInput;
        Binding = binding;
        BindingEntrySha256 = bindingEntrySha256;
        _optionProjection = optionProjection.ToImmutableArray();
    }

    public bool Succeeded { get; }
    public PredictionCopyCompatibilityV2Failure Failure { get; }
    public PredictionCopyCompatibilityV2CanonicalInput CanonicalInput { get; }
    public string CanonicalInputSchemaVersion => CanonicalInput.SchemaVersion;
    public string BoundFingerprint => CanonicalInput.Sha256;
    public BundesligaCopyBindingReference Binding { get; }
    public string BindingEntrySha256 { get; }
    public IReadOnlyList<BundesligaBonusOptionProjection> OptionProjection => _optionProjection;

    internal bool IsBoundTo<TSnapshot>(PredictionCopyCompatibilityV2Input<TSnapshot> input)
        where TSnapshot : class =>
        Binding == input.Binding.Reference
        && string.Equals(BindingEntrySha256, PredictionCopyCompatibilityV2.EntryFingerprint(input.BindingEntry), StringComparison.Ordinal)
        && CanonicalInput.SerializeCanonical().SequenceEqual(
            PredictionCopyCompatibilityV2CanonicalInput.Create(input).SerializeCanonical());

    internal static PredictionCopyCompatibilityV2Decision Create<TSnapshot>(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        PredictionCopyCompatibilityV2Failure failure,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
        where TSnapshot : class =>
        new(failure == PredictionCopyCompatibilityV2Failure.None, failure,
            PredictionCopyCompatibilityV2CanonicalInput.Create(input), input.Binding.Reference,
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
            WriteEntry(writer, entry)));

    internal static byte[] SerializeCanonicalInput<TSnapshot>(PredictionCopyCompatibilityV2Input<TSnapshot> input)
        where TSnapshot : class => BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", PredictionCopyCompatibilityV2CanonicalInput.SchemaVersionValue);
            WriteCurrent(writer, "targetCurrent", input.TargetCurrent);
            WriteCurrent(writer, "sourceCurrent", input.SourceCurrent);
            WriteSeed(writer, "postingSeed", input.PostingSeed.Reference);
            WriteSeed(writer, "sourceSeed", input.SourceSeed.Reference);
            writer.WritePropertyName("binding");
            writer.WriteStartObject();
            writer.WriteNumber("generation", input.Binding.Generation);
            writer.WriteString("sha256", input.Binding.CanonicalSha256);
            writer.WriteEndObject();
            writer.WritePropertyName("bindingEntry");
            WriteEntry(writer, input.BindingEntry);
            WriteContract(writer, "targetContract", input.TargetContract);
            WriteContract(writer, "sourceContract", input.SourceContract);
            writer.WriteEndObject();
        });

    private static void WriteCurrent<TSnapshot>(System.Text.Json.Utf8JsonWriter writer, string name,
        BundesligaTypedCurrentRequest<TSnapshot> current) where TSnapshot : class
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WritePropertyName("authority");
        writer.WriteStartObject();
        WriteAuthority(writer, current.Authority);
        writer.WriteEndObject();
        var (key, _, _) = BundesligaTypedCurrentRequest<TSnapshot>.SnapshotIdentity(current.Snapshot);
        WriteKey(writer, "key", key);
        writer.WritePropertyName("snapshotHash");
        WriteSnapshotHash(writer, current.Snapshot switch
        {
            TypedMatchSnapshot match => match.SnapshotHash,
            TypedBonusSnapshot bonus => bonus.SnapshotHash,
            _ => throw new InvalidDataException("Unsupported current snapshot.")
        });
        writer.WriteString("routeId", current.Identity.RouteId);
        writer.WriteString("profileId", current.Identity.ProfileId);
        writer.WritePropertyName("generationInputContract");
        writer.WriteStartObject();
        writer.WriteString("contractId", current.Identity.GenerationInputContract.ContractId);
        writer.WriteString("sha256", current.Identity.GenerationInputContract.Sha256);
        writer.WriteEndObject();
        writer.WritePropertyName("model");
        WriteModel(writer, current.ModelConfig);
        writer.WriteEndObject();
    }

    private static void WriteAuthority(System.Text.Json.Utf8JsonWriter writer, BundesligaPredictionAuthority authority)
    {
        writer.WriteString("seasonPartition", authority.SeasonPartition);
        writer.WriteString("authorityEpoch", authority.AuthorityEpoch);
        writer.WriteString("mode", BundesligaPredictionCanonicalJson.AuthorityMode(authority.Mode));
        writer.WriteString("postingCommunity", authority.PostingCommunity);
        writer.WriteString("predictionSourceCommunity", authority.PredictionSourceCommunity);
        writer.WriteString("communityContext", authority.CommunityContext);
        WriteSeed(writer, "postingSeed", authority.PostingSeed);
        WriteSeed(writer, "sourceSeed", authority.SourceSeed);
        writer.WritePropertyName("copyBinding");
        if (authority.CopyBinding is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber("generation", authority.CopyBinding.Generation);
            writer.WriteString("sha256", authority.CopyBinding.Sha256);
            writer.WriteEndObject();
        }
    }

    private static void WriteContract(System.Text.Json.Utf8JsonWriter writer, string name,
        PredictionCopyCompatibilityContractV2 contract)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("communityContext", contract.CommunityContext);
        writer.WriteString("routeId", contract.RouteId);
        writer.WriteString("itemKind", BundesligaPredictionCanonicalJson.ItemKind(contract.ItemKind));
        writer.WriteString("subcompetition", contract.Subcompetition.ToSerializedValue());
        WriteIdentity(writer, "rules", contract.Rules.Identity, contract.Rules.Sha256);
        WriteIdentity(writer, "scoring", contract.Scoring.Identity, contract.Scoring.Sha256);
        writer.WritePropertyName("resultBasis");
        if (contract.ResultBasis is null) writer.WriteNullValue();
        else
        {
            writer.WriteStartObject();
            writer.WriteString("resultBasis", contract.ResultBasis.ResultBasis.ToSerializedValue());
            writer.WriteString("identity", contract.ResultBasis.Identity);
            writer.WriteString("sha256", contract.ResultBasis.Sha256);
            writer.WriteEndObject();
        }
        writer.WritePropertyName("prompt");
        WritePrompt(writer, contract.Prompt);
        writer.WritePropertyName("model");
        WriteModel(writer, contract.Model);
        writer.WritePropertyName("copyPolicy");
        writer.WriteStartObject();
        writer.WriteString("identity", contract.CopyPolicy.Identity);
        writer.WriteString("sha256", contract.CopyPolicy.Sha256);
        writer.WriteString("targetRouteId", contract.CopyPolicy.TargetRouteId);
        writer.WriteString("sourceRouteId", contract.CopyPolicy.SourceRouteId);
        writer.WriteString("targetCommunityContext", contract.CopyPolicy.TargetCommunityContext);
        writer.WriteString("sourceCommunityContext", contract.CopyPolicy.SourceCommunityContext);
        writer.WriteEndObject();
        writer.WritePropertyName("optionMeaning");
        if (contract.OptionMeaning is null) writer.WriteNullValue();
        else
        {
            writer.WriteStartObject();
            writer.WriteString("identity", contract.OptionMeaning.Identity);
            writer.WriteString("sha256", contract.OptionMeaning.Sha256);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteKey(System.Text.Json.Utf8JsonWriter writer, string name, StableLocalItemKey key)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("seasonPartition", key.SeasonPartition);
        writer.WriteString("postingCommunity", key.PostingCommunity);
        writer.WriteString("itemKind", BundesligaPredictionCanonicalJson.ItemKind(key.ItemKind));
        writer.WriteString("kicktippItemId", key.KicktippItemId);
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

    private static void WriteSnapshotHash(Utf8JsonWriter writer, BundesligaPredictionSnapshotHash hash)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", hash.SchemaVersion);
        writer.WriteString("sha256", hash.Sha256);
        writer.WriteEndObject();
    }

    private static void WriteEntry(Utf8JsonWriter writer, BundesligaCopyBindingEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("routeId", entry.RouteId);
        WriteKey(writer, "postingKey", entry.PostingKey);
        writer.WritePropertyName("postingSnapshotHash");
        WriteSnapshotHash(writer, entry.PostingSnapshotHash);
        WriteSeed(writer, "postingSeed", entry.PostingSeed);
        WriteKey(writer, "sourceKey", entry.SourceKey);
        writer.WritePropertyName("sourceSnapshotHash");
        WriteSnapshotHash(writer, entry.SourceSnapshotHash);
        WriteSeed(writer, "sourceSeed", entry.SourceSeed);
        writer.WritePropertyName("optionProjection");
        writer.WriteStartArray();
        foreach (var option in entry.OptionProjection)
        {
            writer.WriteStartObject();
            writer.WriteString("sourceOptionId", option.SourceOptionId);
            writer.WriteString("postingOptionId", option.PostingOptionId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteIdentity(Utf8JsonWriter writer, string name, string identity, string sha256)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("identity", identity);
        writer.WriteString("sha256", sha256);
        writer.WriteEndObject();
    }

    private static void WritePrompt(Utf8JsonWriter writer, PredictionPromptProvenanceV2 prompt)
    {
        writer.WriteStartObject();
        writer.WriteString("actualSource", prompt.ActualSource == PredictionPromptSourceV2.Hosted ? "hosted" : "checked-in-fallback");
        writer.WriteString("hostedName", prompt.HostedName);
        writer.WriteNumber("hostedVersion", prompt.HostedVersion);
        writer.WriteString("hostedNormalizedReadbackSha256", prompt.HostedNormalizedReadbackSha256);
        writer.WriteString("requiredLabel", prompt.RequiredLabel);
        writer.WriteBoolean("requiredLabelMembership", prompt.RequiredLabelMembership);
        writer.WriteString("actualFallbackFile", prompt.ActualFallbackFile);
        writer.WriteString("actualFallbackSha256", prompt.ActualFallbackSha256);
        writer.WriteEndObject();
    }

    private static void WriteModel(Utf8JsonWriter writer, PredictionModelConfig model)
    {
        writer.WriteStartObject();
        writer.WriteString("model", model.Model);
        writer.WriteString("reasoningEffort", model.ReasoningEffort);
        writer.WriteNumber("maxOutputTokenCount", model.MaxOutputTokenCount!.Value);
        writer.WriteString("promptName", model.PromptName);
        writer.WriteNumber("promptVersion", model.PromptVersion!.Value);
        writer.WriteEndObject();
    }
}
