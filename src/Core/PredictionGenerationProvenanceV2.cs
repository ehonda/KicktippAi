using System.Collections.Immutable;
using System.Text.Json;
using NodaTime;

namespace EHonda.KicktippAi.Core;

public enum PredictionPromptSourceV2
{
    Hosted,
    CheckedInFallback
}

public sealed record PredictionPromptProvenanceV2
{
    private PredictionPromptProvenanceV2(
        PredictionPromptSourceV2 actualSource,
        string hostedName,
        int hostedVersion,
        string hostedNormalizedReadbackSha256,
        string requiredLabel,
        bool requiredLabelMembership,
        string? actualFallbackFile,
        string? actualFallbackSha256)
    {
        ActualSource = actualSource;
        HostedName = hostedName;
        HostedVersion = hostedVersion;
        HostedNormalizedReadbackSha256 = hostedNormalizedReadbackSha256;
        RequiredLabel = requiredLabel;
        RequiredLabelMembership = requiredLabelMembership;
        ActualFallbackFile = actualFallbackFile;
        ActualFallbackSha256 = actualFallbackSha256;
    }

    public PredictionPromptSourceV2 ActualSource { get; }
    public string HostedName { get; }
    public int HostedVersion { get; }
    public string HostedNormalizedReadbackSha256 { get; }
    public string RequiredLabel { get; }
    public bool RequiredLabelMembership { get; }
    public string? ActualFallbackFile { get; }
    public string? ActualFallbackSha256 { get; }

    public static PredictionPromptProvenanceV2 Create(
        PredictionPromptSourceV2 actualSource,
        string hostedName,
        int hostedVersion,
        string hostedNormalizedReadbackSha256,
        string requiredLabel,
        bool requiredLabelMembership,
        string? actualFallbackFile = null,
        string? actualFallbackSha256 = null)
    {
        BundesligaPredictionContractValidation.Identifier(hostedName, nameof(hostedName));
        BundesligaPredictionContractValidation.Generation(hostedVersion, nameof(hostedVersion));
        BundesligaPredictionContractValidation.Sha256(
            hostedNormalizedReadbackSha256,
            nameof(hostedNormalizedReadbackSha256));
        BundesligaPredictionContractValidation.Identifier(requiredLabel, nameof(requiredLabel));
        if (!requiredLabelMembership)
        {
            throw new InvalidDataException("Required prompt label membership must be proven.");
        }

        if (actualSource == PredictionPromptSourceV2.Hosted
            && (actualFallbackFile is not null || actualFallbackSha256 is not null))
        {
            throw new InvalidDataException("Hosted prompt provenance cannot claim an actual fallback.");
        }

        if (actualSource == PredictionPromptSourceV2.CheckedInFallback)
        {
            BundesligaPredictionContractValidation.Identifier(
                actualFallbackFile ?? string.Empty,
                nameof(actualFallbackFile));
            BundesligaPredictionContractValidation.Sha256(
                actualFallbackSha256 ?? string.Empty,
                nameof(actualFallbackSha256));
        }

        return new PredictionPromptProvenanceV2(
            actualSource,
            hostedName,
            hostedVersion,
            hostedNormalizedReadbackSha256,
            requiredLabel,
            requiredLabelMembership,
            actualFallbackFile,
            actualFallbackSha256);
    }
}

public sealed record PredictionServiceTierProvenanceV2
{
    private PredictionServiceTierProvenanceV2(
        string requestedTier,
        string finalTier,
        bool fallbackOccurred,
        string? fallbackReason) =>
        (RequestedTier, FinalTier, FallbackOccurred, FallbackReason) =
        (requestedTier, finalTier, fallbackOccurred, fallbackReason);

    public string RequestedTier { get; }
    public string FinalTier { get; }
    public bool FallbackOccurred { get; }
    public string? FallbackReason { get; }

    public static PredictionServiceTierProvenanceV2 Create(
        string requestedTier,
        string finalTier,
        bool fallbackOccurred,
        string? fallbackReason = null)
    {
        BundesligaPredictionContractValidation.Identifier(requestedTier, nameof(requestedTier));
        BundesligaPredictionContractValidation.Identifier(finalTier, nameof(finalTier));
        if (!fallbackOccurred
            && (!string.Equals(requestedTier, finalTier, StringComparison.Ordinal)
                || fallbackReason is not null))
        {
            throw new InvalidDataException(
                "No-fallback service tier provenance requires identical tiers and no reason.");
        }

        if (fallbackOccurred)
        {
            BundesligaPredictionContractValidation.ExactText(
                fallbackReason ?? string.Empty,
                nameof(fallbackReason));
        }

        return new PredictionServiceTierProvenanceV2(
            requestedTier,
            finalTier,
            fallbackOccurred,
            fallbackReason);
    }
}

public sealed record PredictionContextDocumentIdentityV2
{
    public PredictionContextDocumentIdentityV2(string documentId, string contentSha256)
    {
        BundesligaPredictionContractValidation.Identifier(documentId, nameof(documentId));
        BundesligaPredictionContractValidation.Sha256(contentSha256, nameof(contentSha256));
        DocumentId = documentId;
        ContentSha256 = contentSha256;
    }

    public string DocumentId { get; }
    public string ContentSha256 { get; }
}

public sealed class PredictionContextProvenanceV2
{
    private readonly ImmutableArray<PredictionContextDocumentIdentityV2> _documents;

    private PredictionContextProvenanceV2(
        string contextManifestId,
        string contextManifestSha256,
        string rulesManifestId,
        string rulesManifestSha256,
        IEnumerable<PredictionContextDocumentIdentityV2> documents)
    {
        ContextManifestId = contextManifestId;
        ContextManifestSha256 = contextManifestSha256;
        RulesManifestId = rulesManifestId;
        RulesManifestSha256 = rulesManifestSha256;
        _documents = documents.ToImmutableArray();
    }

    public string ContextManifestId { get; }
    public string ContextManifestSha256 { get; }
    public string RulesManifestId { get; }
    public string RulesManifestSha256 { get; }
    public IReadOnlyList<PredictionContextDocumentIdentityV2> Documents => _documents;

    public static PredictionContextProvenanceV2 Create(
        string contextManifestId,
        string contextManifestSha256,
        string rulesManifestId,
        string rulesManifestSha256,
        IEnumerable<PredictionContextDocumentIdentityV2> documents)
    {
        BundesligaPredictionContractValidation.Identifier(contextManifestId, nameof(contextManifestId));
        BundesligaPredictionContractValidation.Sha256(contextManifestSha256, nameof(contextManifestSha256));
        BundesligaPredictionContractValidation.Identifier(rulesManifestId, nameof(rulesManifestId));
        BundesligaPredictionContractValidation.Sha256(rulesManifestSha256, nameof(rulesManifestSha256));
        ArgumentNullException.ThrowIfNull(documents);
        var materialized = documents.ToArray();
        if (materialized.Length == 0 || materialized.Any(document => document is null))
        {
            throw new InvalidDataException("At least one immutable context/rules document identity is required.");
        }

        var duplicate = materialized.GroupBy(document => document.DocumentId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate context document identity '{duplicate.Key}'.");
        }

        return new PredictionContextProvenanceV2(
            contextManifestId,
            contextManifestSha256,
            rulesManifestId,
            rulesManifestSha256,
            materialized);
    }
}

public sealed record PredictionGenerationUsageV2
{
    public PredictionGenerationUsageV2(long inputTokens, long outputTokens, decimal costUsd)
    {
        if (inputTokens < 0 || outputTokens < 0 || costUsd < 0)
        {
            throw new InvalidDataException("Generation usage and cost cannot be negative.");
        }

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
    }

    public long InputTokens { get; }
    public long OutputTokens { get; }
    public decimal CostUsd { get; }
    public bool IsZero => InputTokens == 0 && OutputTokens == 0 && CostUsd == 0;
}

public sealed class PredictionGenerationProvenanceV2
{
    public const string SchemaVersionValue = "prediction-generation-provenance-v2";

    private PredictionGenerationProvenanceV2(
        BundesligaPredictionAuthority authority,
        string physicalStorageNamespace,
        StableLocalItemKey postingKey,
        BundesligaPredictionSnapshotHash postingSnapshotHash,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceSnapshotHash,
        string routeId,
        string profileId,
        string? sourcePredictionIdentity,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig modelConfig,
        PredictionServiceTierProvenanceV2 serviceTier,
        PredictionContextProvenanceV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex,
        PredictionGenerationUsageV2 targetGenerationUsage)
    {
        Authority = authority;
        PhysicalStorageNamespace = physicalStorageNamespace;
        PostingKey = postingKey;
        PostingSnapshotHash = postingSnapshotHash;
        SourceKey = sourceKey;
        SourceSnapshotHash = sourceSnapshotHash;
        RouteId = routeId;
        ProfileId = profileId;
        SourcePredictionIdentity = sourcePredictionIdentity;
        Prompt = prompt;
        ModelConfig = modelConfig;
        ServiceTier = serviceTier;
        Context = context;
        GenerationTime = generationTime;
        PredictionIdentity = predictionIdentity;
        RepredictionIndex = repredictionIndex;
        TargetGenerationUsage = targetGenerationUsage;
        CanonicalSha256 = BundesligaPredictionCanonicalJson.Sha256(SerializeCanonical());
    }

    public string SchemaVersion => SchemaVersionValue;
    public BundesligaPredictionAuthority Authority { get; }
    public string PhysicalStorageNamespace { get; }
    public StableLocalItemKey PostingKey { get; }
    public BundesligaPredictionSnapshotHash PostingSnapshotHash { get; }
    public StableLocalItemKey SourceKey { get; }
    public BundesligaPredictionSnapshotHash SourceSnapshotHash { get; }
    public string RouteId { get; }
    public string ProfileId { get; }
    public string? SourcePredictionIdentity { get; }
    public PredictionPromptProvenanceV2 Prompt { get; }
    public PredictionModelConfig ModelConfig { get; }
    public PredictionServiceTierProvenanceV2 ServiceTier { get; }
    public PredictionContextProvenanceV2 Context { get; }
    public Instant GenerationTime { get; }
    public string PredictionIdentity { get; }
    public int RepredictionIndex { get; }
    public PredictionGenerationUsageV2 TargetGenerationUsage { get; }
    public string CanonicalSha256 { get; }

    public static PredictionGenerationProvenanceV2 Create(
        BundesligaPredictionAuthority authority,
        string physicalStorageNamespace,
        StableLocalItemKey postingKey,
        BundesligaPredictionSnapshotHash postingSnapshotHash,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceSnapshotHash,
        string routeId,
        string profileId,
        string? sourcePredictionIdentity,
        PredictionPromptProvenanceV2 prompt,
        PredictionModelConfig modelConfig,
        PredictionServiceTierProvenanceV2 serviceTier,
        PredictionContextProvenanceV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex,
        PredictionGenerationUsageV2 targetGenerationUsage)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(postingKey);
        ArgumentNullException.ThrowIfNull(postingSnapshotHash);
        ArgumentNullException.ThrowIfNull(sourceKey);
        ArgumentNullException.ThrowIfNull(sourceSnapshotHash);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(modelConfig);
        ArgumentNullException.ThrowIfNull(serviceTier);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(targetGenerationUsage);
        BundesligaPredictionContractValidation.Identifier(physicalStorageNamespace, nameof(physicalStorageNamespace));
        BundesligaPredictionContractValidation.Identifier(routeId, nameof(routeId));
        BundesligaPredictionContractValidation.Identifier(profileId, nameof(profileId));
        BundesligaPredictionContractValidation.Identifier(predictionIdentity, nameof(predictionIdentity));
        if (repredictionIndex < 0)
        {
            throw new InvalidDataException("Reprediction index cannot be negative.");
        }

        BundesligaPredictionCanonicalJson.FormatInstant(generationTime);
        ValidateScope(authority, physicalStorageNamespace, postingKey, sourceKey);
        ValidateModelAndPrompt(modelConfig, prompt);
        if (authority.Mode == BundesligaPredictionAuthorityMode.Direct)
        {
            if (postingKey != sourceKey
                || postingSnapshotHash != sourceSnapshotHash
                || sourcePredictionIdentity is not null
                || authority.CopyBinding is not null)
            {
                throw new InvalidDataException(
                    "Direct provenance requires identical posting/source identity and no copy fields.");
            }
        }
        else
        {
            BundesligaPredictionContractValidation.Identifier(
                sourcePredictionIdentity ?? string.Empty,
                nameof(sourcePredictionIdentity));
            if (authority.CopyBinding is null || !targetGenerationUsage.IsZero)
            {
                throw new InvalidDataException(
                    "Copy provenance requires a Copy Binding and truthful zero target generation usage/cost.");
            }
        }

        return new PredictionGenerationProvenanceV2(
            authority,
            physicalStorageNamespace,
            postingKey,
            postingSnapshotHash,
            sourceKey,
            sourceSnapshotHash,
            routeId,
            profileId,
            sourcePredictionIdentity,
            prompt,
            modelConfig,
            serviceTier,
            context,
            generationTime,
            predictionIdentity,
            repredictionIndex,
            targetGenerationUsage);
    }

    public byte[] SerializeCanonical() => PredictionGenerationProvenanceV2CanonicalJson.Serialize(this);

    public static PredictionGenerationProvenanceV2 DeserializeCanonical(ReadOnlySpan<byte> bytes) =>
        PredictionGenerationProvenanceV2CanonicalJson.Deserialize(bytes);

    private static void ValidateScope(
        BundesligaPredictionAuthority authority,
        string physicalStorageNamespace,
        StableLocalItemKey postingKey,
        StableLocalItemKey sourceKey)
    {
        if (!string.Equals(postingKey.PostingCommunity, authority.PostingCommunity, StringComparison.Ordinal)
            || !string.Equals(sourceKey.PostingCommunity, authority.PredictionSourceCommunity, StringComparison.Ordinal)
            || postingKey.ItemKind != sourceKey.ItemKind)
        {
            throw new InvalidDataException("Provenance item keys are outside the complete authority scope.");
        }

        var expectedNamespace = postingKey.ItemKind switch
        {
            BundesligaPredictionItemKind.Match =>
                "match-predictions-bundesliga-2026-27-typed-v1",
            BundesligaPredictionItemKind.Bonus =>
                "bonus-predictions-bundesliga-2026-27-typed-v1",
            _ => throw new InvalidDataException("Unknown provenance item kind.")
        };
        if (!string.Equals(physicalStorageNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Provenance physical namespace does not match its typed item kind.");
        }
    }

    private static void ValidateModelAndPrompt(
        PredictionModelConfig modelConfig,
        PredictionPromptProvenanceV2 prompt)
    {
        if (modelConfig.ReasoningEffort is null
            || modelConfig.MaxOutputTokenCount is null
            || !string.Equals(modelConfig.PromptName, prompt.HostedName, StringComparison.Ordinal)
            || modelConfig.PromptVersion != prompt.HostedVersion)
        {
            throw new InvalidDataException(
                "Generation provenance requires exact reasoning, output cap, and numbered prompt identity.");
        }
    }
}

internal static class PredictionGenerationProvenanceV2CanonicalJson
{
    public static byte[] Serialize(PredictionGenerationProvenanceV2 provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        return BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", provenance.SchemaVersion);
            writer.WritePropertyName("authority");
            WriteAuthority(writer, provenance.Authority);
            writer.WriteString("physicalStorageNamespace", provenance.PhysicalStorageNamespace);
            writer.WritePropertyName("postingKey");
            BundesligaPredictionSnapshotCanonicalJson.WriteKey(writer, provenance.PostingKey);
            writer.WritePropertyName("postingSnapshotHash");
            BundesligaPredictionSnapshotCanonicalJson.WriteHash(writer, provenance.PostingSnapshotHash);
            writer.WritePropertyName("sourceKey");
            BundesligaPredictionSnapshotCanonicalJson.WriteKey(writer, provenance.SourceKey);
            writer.WritePropertyName("sourceSnapshotHash");
            BundesligaPredictionSnapshotCanonicalJson.WriteHash(writer, provenance.SourceSnapshotHash);
            writer.WriteString("routeId", provenance.RouteId);
            writer.WriteString("profileId", provenance.ProfileId);
            writer.WriteString("sourcePredictionIdentity", provenance.SourcePredictionIdentity);
            writer.WritePropertyName("prompt");
            WritePrompt(writer, provenance.Prompt);
            writer.WritePropertyName("modelConfig");
            WriteModel(writer, provenance.ModelConfig);
            writer.WritePropertyName("serviceTier");
            WriteService(writer, provenance.ServiceTier);
            writer.WritePropertyName("context");
            WriteContext(writer, provenance.Context);
            writer.WriteString(
                "generationTime",
                BundesligaPredictionCanonicalJson.FormatInstant(provenance.GenerationTime));
            writer.WriteString("predictionIdentity", provenance.PredictionIdentity);
            writer.WriteNumber("repredictionIndex", provenance.RepredictionIndex);
            writer.WritePropertyName("targetGenerationUsage");
            WriteUsage(writer, provenance.TargetGenerationUsage);
            writer.WriteEndObject();
        });
    }

    public static PredictionGenerationProvenanceV2 Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Generation provenance v2");
        var root = document.RootElement;
        BundesligaPredictionCanonicalJson.Properties(
            root,
            "schemaVersion",
            "authority",
            "physicalStorageNamespace",
            "postingKey",
            "postingSnapshotHash",
            "sourceKey",
            "sourceSnapshotHash",
            "routeId",
            "profileId",
            "sourcePredictionIdentity",
            "prompt",
            "modelConfig",
            "serviceTier",
            "context",
            "generationTime",
            "predictionIdentity",
            "repredictionIndex",
            "targetGenerationUsage");
        if (!string.Equals(
            BundesligaPredictionCanonicalJson.String(root, "schemaVersion"),
            PredictionGenerationProvenanceV2.SchemaVersionValue,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unknown Generation Provenance schema.");
        }

        var provenance = PredictionGenerationProvenanceV2.Create(
            ReadAuthority(root.GetProperty("authority")),
            BundesligaPredictionCanonicalJson.String(root, "physicalStorageNamespace"),
            BundesligaPredictionSnapshotCanonicalJson.ReadKey(root.GetProperty("postingKey")),
            BundesligaPredictionSnapshotCanonicalJson.ReadHash(root.GetProperty("postingSnapshotHash")),
            BundesligaPredictionSnapshotCanonicalJson.ReadKey(root.GetProperty("sourceKey")),
            BundesligaPredictionSnapshotCanonicalJson.ReadHash(root.GetProperty("sourceSnapshotHash")),
            BundesligaPredictionCanonicalJson.String(root, "routeId"),
            BundesligaPredictionCanonicalJson.String(root, "profileId"),
            BundesligaPredictionCanonicalJson.NullableString(root, "sourcePredictionIdentity"),
            ReadPrompt(root.GetProperty("prompt")),
            ReadModel(root.GetProperty("modelConfig")),
            ReadService(root.GetProperty("serviceTier")),
            ReadContext(root.GetProperty("context")),
            BundesligaPredictionCanonicalJson.Instant(root, "generationTime"),
            BundesligaPredictionCanonicalJson.String(root, "predictionIdentity"),
            BundesligaPredictionCanonicalJson.Int32(root, "repredictionIndex"),
            ReadUsage(root.GetProperty("targetGenerationUsage")));
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, Serialize(provenance), "Generation provenance v2");
        return provenance;
    }

    private static void WriteAuthority(Utf8JsonWriter writer, BundesligaPredictionAuthority authority)
    {
        writer.WriteStartObject();
        writer.WriteString("seasonPartition", authority.SeasonPartition);
        writer.WriteString("authorityEpoch", authority.AuthorityEpoch);
        writer.WriteString("mode", BundesligaPredictionCanonicalJson.AuthorityMode(authority.Mode));
        writer.WriteString("postingCommunity", authority.PostingCommunity);
        writer.WriteString("predictionSourceCommunity", authority.PredictionSourceCommunity);
        writer.WriteString("communityContext", authority.CommunityContext);
        writer.WritePropertyName("postingSeed");
        WriteSeed(writer, authority.PostingSeed);
        writer.WritePropertyName("sourceSeed");
        WriteSeed(writer, authority.SourceSeed);
        writer.WritePropertyName("copyBinding");
        WriteCopyBinding(writer, authority.CopyBinding);
        writer.WriteEndObject();
    }

    private static BundesligaPredictionAuthority ReadAuthority(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "seasonPartition",
            "authorityEpoch",
            "mode",
            "postingCommunity",
            "predictionSourceCommunity",
            "communityContext",
            "postingSeed",
            "sourceSeed",
            "copyBinding");
        var mode = BundesligaPredictionCanonicalJson.ParseAuthorityMode(
            BundesligaPredictionCanonicalJson.String(element, "mode"));
        var season = BundesligaPredictionCanonicalJson.String(element, "seasonPartition");
        var epoch = BundesligaPredictionCanonicalJson.String(element, "authorityEpoch");
        var posting = BundesligaPredictionCanonicalJson.String(element, "postingCommunity");
        var source = BundesligaPredictionCanonicalJson.String(element, "predictionSourceCommunity");
        var context = BundesligaPredictionCanonicalJson.String(element, "communityContext");
        var postingSeed = ReadSeed(element.GetProperty("postingSeed"));
        var sourceSeed = ReadSeed(element.GetProperty("sourceSeed"));
        var copy = ReadCopyBinding(element.GetProperty("copyBinding"));
        return mode switch
        {
            BundesligaPredictionAuthorityMode.Direct when copy is null =>
                BundesligaPredictionAuthority.CreateDirect(
                    season,
                    epoch,
                    posting,
                    source,
                    context,
                    postingSeed,
                    sourceSeed),
            BundesligaPredictionAuthorityMode.Copy when copy is not null =>
                BundesligaPredictionAuthority.CreateCopy(
                    season,
                    epoch,
                    posting,
                    source,
                    context,
                    postingSeed,
                    sourceSeed,
                    copy),
            _ => throw new InvalidDataException("Authority mode and Copy Binding presence conflict.")
        };
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

    private static PredictionPromptProvenanceV2 ReadPrompt(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "actualSource",
            "hostedName",
            "hostedVersion",
            "hostedNormalizedReadbackSha256",
            "requiredLabel",
            "requiredLabelMembership",
            "actualFallbackFile",
            "actualFallbackSha256");
        var source = BundesligaPredictionCanonicalJson.String(element, "actualSource") switch
        {
            "hosted" => PredictionPromptSourceV2.Hosted,
            "checked-in-fallback" => PredictionPromptSourceV2.CheckedInFallback,
            var unknown => throw new InvalidDataException($"Unknown actual prompt source '{unknown}'.")
        };
        return PredictionPromptProvenanceV2.Create(
            source,
            BundesligaPredictionCanonicalJson.String(element, "hostedName"),
            BundesligaPredictionCanonicalJson.Int32(element, "hostedVersion"),
            BundesligaPredictionCanonicalJson.String(element, "hostedNormalizedReadbackSha256"),
            BundesligaPredictionCanonicalJson.String(element, "requiredLabel"),
            BundesligaPredictionCanonicalJson.Boolean(element, "requiredLabelMembership"),
            BundesligaPredictionCanonicalJson.NullableString(element, "actualFallbackFile"),
            BundesligaPredictionCanonicalJson.NullableString(element, "actualFallbackSha256"));
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

    private static PredictionModelConfig ReadModel(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "model",
            "reasoningEffort",
            "maxOutputTokenCount",
            "promptName",
            "promptVersion");
        return PredictionModelConfig.Create(
            BundesligaPredictionCanonicalJson.String(element, "model"),
            BundesligaPredictionCanonicalJson.String(element, "reasoningEffort"),
            BundesligaPredictionCanonicalJson.Int32(element, "maxOutputTokenCount"),
            BundesligaPredictionCanonicalJson.String(element, "promptName"),
            BundesligaPredictionCanonicalJson.Int32(element, "promptVersion"));
    }

    private static void WriteService(Utf8JsonWriter writer, PredictionServiceTierProvenanceV2 service)
    {
        writer.WriteStartObject();
        writer.WriteString("requestedTier", service.RequestedTier);
        writer.WriteString("finalTier", service.FinalTier);
        writer.WriteBoolean("fallbackOccurred", service.FallbackOccurred);
        writer.WriteString("fallbackReason", service.FallbackReason);
        writer.WriteEndObject();
    }

    private static PredictionServiceTierProvenanceV2 ReadService(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "requestedTier",
            "finalTier",
            "fallbackOccurred",
            "fallbackReason");
        return PredictionServiceTierProvenanceV2.Create(
            BundesligaPredictionCanonicalJson.String(element, "requestedTier"),
            BundesligaPredictionCanonicalJson.String(element, "finalTier"),
            BundesligaPredictionCanonicalJson.Boolean(element, "fallbackOccurred"),
            BundesligaPredictionCanonicalJson.NullableString(element, "fallbackReason"));
    }

    private static void WriteContext(Utf8JsonWriter writer, PredictionContextProvenanceV2 context)
    {
        writer.WriteStartObject();
        writer.WriteString("contextManifestId", context.ContextManifestId);
        writer.WriteString("contextManifestSha256", context.ContextManifestSha256);
        writer.WriteString("rulesManifestId", context.RulesManifestId);
        writer.WriteString("rulesManifestSha256", context.RulesManifestSha256);
        writer.WritePropertyName("documents");
        writer.WriteStartArray();
        foreach (var document in context.Documents)
        {
            writer.WriteStartObject();
            writer.WriteString("documentId", document.DocumentId);
            writer.WriteString("contentSha256", document.ContentSha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static PredictionContextProvenanceV2 ReadContext(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "contextManifestId",
            "contextManifestSha256",
            "rulesManifestId",
            "rulesManifestSha256",
            "documents");
        var documentsElement = element.GetProperty("documents");
        if (documentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Provenance context documents must be an array.");
        }

        var documents = documentsElement.EnumerateArray().Select(document =>
        {
            BundesligaPredictionCanonicalJson.Properties(document, "documentId", "contentSha256");
            return new PredictionContextDocumentIdentityV2(
                BundesligaPredictionCanonicalJson.String(document, "documentId"),
                BundesligaPredictionCanonicalJson.String(document, "contentSha256"));
        }).ToArray();
        return PredictionContextProvenanceV2.Create(
            BundesligaPredictionCanonicalJson.String(element, "contextManifestId"),
            BundesligaPredictionCanonicalJson.String(element, "contextManifestSha256"),
            BundesligaPredictionCanonicalJson.String(element, "rulesManifestId"),
            BundesligaPredictionCanonicalJson.String(element, "rulesManifestSha256"),
            documents);
    }

    private static void WriteUsage(Utf8JsonWriter writer, PredictionGenerationUsageV2 usage)
    {
        writer.WriteStartObject();
        writer.WriteNumber("inputTokens", usage.InputTokens);
        writer.WriteNumber("outputTokens", usage.OutputTokens);
        writer.WriteNumber("costUsd", usage.CostUsd);
        writer.WriteEndObject();
    }

    private static PredictionGenerationUsageV2 ReadUsage(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(element, "inputTokens", "outputTokens", "costUsd");
        return new PredictionGenerationUsageV2(
            BundesligaPredictionCanonicalJson.Int64(element, "inputTokens"),
            BundesligaPredictionCanonicalJson.Int64(element, "outputTokens"),
            BundesligaPredictionCanonicalJson.Decimal(element, "costUsd"));
    }

    private static void WriteSeed(Utf8JsonWriter writer, BundesligaIdentitySeedReference seed)
    {
        writer.WriteStartObject();
        writer.WriteNumber("generation", seed.Generation);
        writer.WriteString("sha256", seed.Sha256);
        writer.WriteEndObject();
    }

    private static BundesligaIdentitySeedReference ReadSeed(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(element, "generation", "sha256");
        return BundesligaIdentitySeedReference.Create(
            BundesligaPredictionCanonicalJson.Int32(element, "generation"),
            BundesligaPredictionCanonicalJson.String(element, "sha256"));
    }

    private static void WriteCopyBinding(
        Utf8JsonWriter writer,
        BundesligaCopyBindingReference? binding)
    {
        if (binding is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("generation", binding.Generation);
        writer.WriteString("sha256", binding.Sha256);
        writer.WriteEndObject();
    }

    private static BundesligaCopyBindingReference? ReadCopyBinding(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        BundesligaPredictionCanonicalJson.Properties(element, "generation", "sha256");
        return BundesligaCopyBindingReference.Create(
            BundesligaPredictionCanonicalJson.Int32(element, "generation"),
            BundesligaPredictionCanonicalJson.String(element, "sha256"));
    }
}
