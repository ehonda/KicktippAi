using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EHonda.KicktippAi.Core;

public enum BundesligaContextSourceScope { ProductionLive, Development }
public enum BundesligaContextSource { ClubElo, Rosters }
public enum BundesligaContextSourceCycleStatus { Claiming, ObservationsFinalized, BundleVerified, UploadReserved, HandoffReady, Complete, Aborted }
public enum BundesligaContextSourceSourceStatus { Claimed, Finalized, Complete, Aborted }
public enum BundesligaContextSourceClaimDisposition { NewClaim, OwnedClaim, ExistingFinalized, Busy, ExistingAborted }
public enum BundesligaContextSourceDisposition { ArtifactCaptured, MetadataUnchanged, Rejected }
public enum BundesligaContextSourceError { AcquisitionInterrupted, FinalizedPayloadUnavailable, HandoffUploadFailed, HandoffArtifactMissing, HandoffArtifactConflict, LocalHandoffMissing, SupersededIncompleteCycle, LateCycle, StateConflict }

public static partial class BundesligaContextSourceContract
{
    public const string Competition = "bundesliga-2026-27";
    public const string ProductionScope = "production-live";
    public const string DevelopmentScope = "development";
    public const string DevelopmentLane = "development-profile";
    public const string DevelopmentCommunity = "ehonda-dev-buli-2627";
    public static readonly IReadOnlyList<string> DevelopmentConsumers = [DevelopmentLane];
    public static readonly IReadOnlyList<string> ProductionConsumers =
    [
        "pes-squad-context", "schadensfresse-context", "relaxdays-tippt-context",
        "arena-sol-xhigh-context", "arena-sol-high-context", "arena-luna-medium-context",
        "arena-terra-xhigh-context", "arena-luna-none-context"
    ];

    public static string ScopeValue(BundesligaContextSourceScope scope) => scope switch
    {
        BundesligaContextSourceScope.ProductionLive => ProductionScope,
        BundesligaContextSourceScope.Development => DevelopmentScope,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    public static string SourceValue(BundesligaContextSource source) => source switch
    {
        BundesligaContextSource.ClubElo => "club-elo",
        BundesligaContextSource.Rosters => "rosters",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static BundesligaContextSource ParseSource(string value) => value switch
    {
        "club-elo" => BundesligaContextSource.ClubElo,
        "rosters" => BundesligaContextSource.Rosters,
        _ => throw new InvalidDataException($"Unknown context source '{value}'.")
    };

    public static string ErrorValue(BundesligaContextSourceError error) => error switch
    {
        BundesligaContextSourceError.AcquisitionInterrupted => "ACQUISITION_INTERRUPTED",
        BundesligaContextSourceError.FinalizedPayloadUnavailable => "FINALIZED_PAYLOAD_UNAVAILABLE",
        BundesligaContextSourceError.HandoffUploadFailed => "HANDOFF_UPLOAD_FAILED",
        BundesligaContextSourceError.HandoffArtifactMissing => "HANDOFF_ARTIFACT_MISSING",
        BundesligaContextSourceError.HandoffArtifactConflict => "HANDOFF_ARTIFACT_CONFLICT",
        BundesligaContextSourceError.LocalHandoffMissing => "LOCAL_HANDOFF_MISSING",
        BundesligaContextSourceError.SupersededIncompleteCycle => "SUPERSEDED_INCOMPLETE_CYCLE",
        BundesligaContextSourceError.LateCycle => "LATE_CYCLE",
        BundesligaContextSourceError.StateConflict => "STATE_CONFLICT",
        _ => throw new ArgumentOutOfRangeException(nameof(error))
    };

    public static BundesligaContextSourceError ParseError(string value) => value switch
    {
        "ACQUISITION_INTERRUPTED" => BundesligaContextSourceError.AcquisitionInterrupted,
        "FINALIZED_PAYLOAD_UNAVAILABLE" => BundesligaContextSourceError.FinalizedPayloadUnavailable,
        "HANDOFF_UPLOAD_FAILED" => BundesligaContextSourceError.HandoffUploadFailed,
        "HANDOFF_ARTIFACT_MISSING" => BundesligaContextSourceError.HandoffArtifactMissing,
        "HANDOFF_ARTIFACT_CONFLICT" => BundesligaContextSourceError.HandoffArtifactConflict,
        "LOCAL_HANDOFF_MISSING" => BundesligaContextSourceError.LocalHandoffMissing,
        "SUPERSEDED_INCOMPLETE_CYCLE" => BundesligaContextSourceError.SupersededIncompleteCycle,
        "LATE_CYCLE" => BundesligaContextSourceError.LateCycle,
        "STATE_CONFLICT" => BundesligaContextSourceError.StateConflict,
        _ => throw new InvalidDataException($"Unknown context-source error '{value}'.")
    };

    public static string FormatUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new InvalidDataException("UTC timestamps must use whole seconds and a zero offset.");
        return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            || FormatUtc(parsed) != value)
            throw new InvalidDataException("UTC timestamp is not canonical second-precision Z form.");
        return parsed;
    }

    public static string NewClaimToken() => Guid.NewGuid().ToString("D").ToLowerInvariant();

    public static void ValidateClaimToken(string token)
    {
        if (!UuidV4Regex().IsMatch(token)) throw new InvalidDataException("Claim token must be a lowercase UUIDv4.");
    }

    public static void ValidateConsumers(BundesligaContextSourceScope scope, string producer, IReadOnlyList<string> consumers)
    {
        if (!Enum.IsDefined(scope)) throw new InvalidDataException("Context-source scope is invalid.");
        var expected = scope == BundesligaContextSourceScope.ProductionLive ? ProductionConsumers : DevelopmentConsumers;
        var expectedProducer = scope == BundesligaContextSourceScope.ProductionLive ? ProductionConsumers[0] : DevelopmentLane;
        if (!string.Equals(producer, expectedProducer, StringComparison.Ordinal)
            || !consumers.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("Producer and consumers must equal the frozen lane contract.");
    }

    public static void ValidateConsumerAuthority(BundesligaContextSourceScope scope, string consumerLane, string communityContext)
    {
        if (scope == BundesligaContextSourceScope.Development)
        {
            if (consumerLane != DevelopmentLane || communityContext != DevelopmentCommunity)
                throw new InvalidDataException("Development source cycles are restricted to the exact development lane and community.");
            return;
        }
        if (scope != BundesligaContextSourceScope.ProductionLive) throw new InvalidDataException("Context-source scope is invalid.");
        var expectedCommunity = consumerLane switch
        {
            "pes-squad-context" => "pes-squad",
            "schadensfresse-context" => "schadensfresse",
            "relaxdays-tippt-context" => "relaxdays-tippt",
            "arena-sol-xhigh-context" or "arena-sol-high-context" or "arena-luna-medium-context" or "arena-terra-xhigh-context" or "arena-luna-none-context" => "ehonda-ai-arena",
            _ => throw new InvalidDataException("Production source-cycle lane is not authorized.")
        };
        if (communityContext != expectedCommunity) throw new InvalidDataException("Production source-cycle lane is not authorized for this community.");
    }

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex UuidV4Regex();
}

public sealed record BundesligaContextSourceCycleIdentity(string Competition, BundesligaContextSourceScope Scope, string CycleId, long Sequence)
{
    private static readonly Regex ProductionRegex = new("^gha:[1-9][0-9]{0,18}:[1-9][0-9]{0,18}$", RegexOptions.CultureInvariant);
    private static readonly Regex DevelopmentRegex = new("^local:[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.CultureInvariant);

    public string ScopeValue => BundesligaContextSourceContract.ScopeValue(Scope);
    public string StorageId => BundesligaContextSourceHashing.CycleStorageId(Competition, ScopeValue, CycleId);

    public static BundesligaContextSourceCycleIdentity Production(string competition, long repositoryId, long runId)
    {
        if (repositoryId <= 0 || runId <= 0) throw new ArgumentOutOfRangeException(nameof(repositoryId));
        return Create(competition, BundesligaContextSourceScope.ProductionLive, $"gha:{repositoryId}:{runId}", runId);
    }

    public static BundesligaContextSourceCycleIdentity Development(string competition, string uuidV7)
        => Create(competition, BundesligaContextSourceScope.Development, $"local:{uuidV7}", ParseUuidV7Sequence(uuidV7));

    public static BundesligaContextSourceCycleIdentity FromCycleId(string competition, BundesligaContextSourceScope scope, string cycleId)
    {
        ValidateCycleId(scope, cycleId);
        return scope switch
        {
            BundesligaContextSourceScope.ProductionLive => Create(competition, scope, cycleId, ParseProductionCycleId(cycleId).RunId),
            BundesligaContextSourceScope.Development => Create(competition, scope, cycleId, ParseUuidV7Sequence(cycleId[6..])),
            _ => throw new InvalidDataException("Context-source scope is invalid.")
        };
    }

    public static BundesligaContextSourceCycleIdentity Create(string competition, BundesligaContextSourceScope scope, string cycleId, long sequence)
    {
        if (competition != BundesligaContextSourceContract.Competition) throw new InvalidDataException("Competition identity is not canonical.");
        if (!Enum.IsDefined(scope)) throw new InvalidDataException("Context-source scope is invalid.");
        ValidateCycleId(scope, cycleId);
        if (scope == BundesligaContextSourceScope.ProductionLive)
        {
            var (_, runId) = ParseProductionCycleId(cycleId);
            if (sequence != runId) throw new InvalidDataException("Production sequence must equal github.run_id.");
        }
        else
        {
            if (sequence != ParseUuidV7Sequence(cycleId[6..])) throw new InvalidDataException("Development sequence must equal UUIDv7 Unix milliseconds.");
        }
        return new BundesligaContextSourceCycleIdentity(competition, scope, cycleId, sequence);
    }

    public static void ValidateCycleId(BundesligaContextSourceScope scope, string cycleId)
    {
        if (!Enum.IsDefined(scope)) throw new InvalidDataException("Context-source scope is invalid.");
        if (scope == BundesligaContextSourceScope.ProductionLive)
        {
            if (!ProductionRegex.IsMatch(cycleId)) throw new InvalidDataException("Production cycle ID is not canonical.");
            _ = ParseProductionCycleId(cycleId);
        }
        else if (scope == BundesligaContextSourceScope.Development && !DevelopmentRegex.IsMatch(cycleId))
        {
            throw new InvalidDataException("Development cycle ID is not a lowercase UUIDv7.");
        }
    }

    private static (long RepositoryId, long RunId) ParseProductionCycleId(string cycleId)
    {
        var firstSeparator = cycleId.IndexOf(':');
        var secondSeparator = cycleId.LastIndexOf(':');
        if (firstSeparator < 0 || secondSeparator <= firstSeparator
            || !long.TryParse(cycleId.AsSpan(firstSeparator + 1, secondSeparator - firstSeparator - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var repositoryId)
            || !long.TryParse(cycleId.AsSpan(secondSeparator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var runId)
            || repositoryId <= 0 || runId <= 0)
            throw new InvalidDataException("Production cycle ID components must be positive Int64 values.");
        return (repositoryId, runId);
    }

    private static long ParseUuidV7Sequence(string uuid)
    {
        if (!Regex.IsMatch(uuid, "^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Development UUID is not canonical UUIDv7.");
        return long.Parse(uuid.Replace("-", string.Empty, StringComparison.Ordinal).AsSpan(0, 12), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}

public sealed record BundesligaContextSourceOuterCycle(
    BundesligaContextSourceCycleIdentity Identity, DateTimeOffset StartedAtUtc, DateTimeOffset StalenessReferenceAtUtc,
    string ProducerLaneId, IReadOnlyList<string> ExpectedConsumers, IReadOnlyList<BundesligaContextSource> EnabledSources,
    BundesligaContextSourceCycleStatus Status, string? BundleSha256 = null, string? ArtifactName = null,
    BundesligaContextSourceError? AbortCode = null, DateTimeOffset? CompletedAtUtc = null)
{
    public const string Contract = "bundesliga-context-source-cycle/v1";
    public void Validate()
    {
        if (!Enum.IsDefined(Identity.Scope) || !Enum.IsDefined(Status)) throw new InvalidDataException("Cycle scope/status is invalid.");
        _ = BundesligaContextSourceCycleIdentity.Create(Identity.Competition, Identity.Scope, Identity.CycleId, Identity.Sequence);
        BundesligaContextSourceContract.FormatUtc(StartedAtUtc); BundesligaContextSourceContract.FormatUtc(StalenessReferenceAtUtc);
        if (StartedAtUtc != StalenessReferenceAtUtc) throw new InvalidDataException("Cycle timestamps must be identical.");
        BundesligaContextSourceContract.ValidateConsumers(Identity.Scope, ProducerLaneId, ExpectedConsumers);
        if (EnabledSources.Count == 0 || EnabledSources.Any(source => !Enum.IsDefined(source)) || EnabledSources.Distinct().Count() != EnabledSources.Count || !EnabledSources.SequenceEqual(EnabledSources.Order())) throw new InvalidDataException("Enabled sources are not canonical.");
        BundesligaContextSourceHashing.ValidateOptionalSha(BundleSha256);
        if (Identity.Scope == BundesligaContextSourceScope.Development && ArtifactName is not null) throw new InvalidDataException("Development cycles cannot reserve artifacts.");
        var requiresBundle = Status is BundesligaContextSourceCycleStatus.BundleVerified or BundesligaContextSourceCycleStatus.UploadReserved or BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete;
        var requiresArtifact = Identity.Scope == BundesligaContextSourceScope.ProductionLive
            && Status is BundesligaContextSourceCycleStatus.UploadReserved or BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete;
        if (requiresBundle) BundesligaContextSourceHashing.ValidateSha(BundleSha256);
        if (!requiresBundle && Status != BundesligaContextSourceCycleStatus.Aborted && BundleSha256 is not null) throw new InvalidDataException("Cycle bundle digest is not valid before bundle verification.");
        if (requiresArtifact && ArtifactName != $"bundesliga-context-source-bundle-{Identity.StorageId}") throw new InvalidDataException("Production artifact name is not canonical.");
        if (!requiresArtifact && Status != BundesligaContextSourceCycleStatus.Aborted && ArtifactName is not null) throw new InvalidDataException("Cycle artifact reservation is not valid in this state.");
        if (Status == BundesligaContextSourceCycleStatus.Aborted && ArtifactName is not null && BundleSha256 is null)
            throw new InvalidDataException("An aborted artifact reservation requires its bundle digest.");
        if (Status == BundesligaContextSourceCycleStatus.Aborted && ArtifactName is not null)
        {
            if (Identity.Scope != BundesligaContextSourceScope.ProductionLive || ArtifactName != $"bundesliga-context-source-bundle-{Identity.StorageId}") throw new InvalidDataException("Aborted cycle artifact reservation is not canonical.");
            BundesligaContextSourceHashing.ValidateSha(BundleSha256);
        }
        if (Status == BundesligaContextSourceCycleStatus.Aborted ^ AbortCode is not null) throw new InvalidDataException("Abort status/code matrix is invalid.");
        if (Status == BundesligaContextSourceCycleStatus.Complete ^ CompletedAtUtc is not null) throw new InvalidDataException("Completion status/time matrix is invalid.");
        if (CompletedAtUtc is not null) BundesligaContextSourceContract.FormatUtc(CompletedAtUtc.Value);
    }

    public byte[] CreateCanonicalUtf8()
    {
        Validate(); using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteString("contract", Contract); writer.WriteString("competition", Identity.Competition); writer.WriteString("scope", Identity.ScopeValue); writer.WriteString("cycleId", Identity.CycleId); writer.WriteNumber("cycleSequence", Identity.Sequence); writer.WriteString("startedAtUtc", BundesligaContextSourceContract.FormatUtc(StartedAtUtc)); writer.WriteString("stalenessReferenceAtUtc", BundesligaContextSourceContract.FormatUtc(StalenessReferenceAtUtc)); writer.WriteString("producerLaneId", ProducerLaneId); writer.WritePropertyName("expectedConsumers"); JsonSerializer.Serialize(writer, ExpectedConsumers); writer.WritePropertyName("enabledSources"); JsonSerializer.Serialize(writer, EnabledSources.Select(BundesligaContextSourceContract.SourceValue)); writer.WriteString("status", Status.ToString()); writer.WriteString("bundleSha256", BundleSha256); writer.WriteString("artifactName", ArtifactName); writer.WriteString("abortCode", AbortCode is null ? null : BundesligaContextSourceContract.ErrorValue(AbortCode.Value)); writer.WriteString("completedAtUtc", CompletedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(CompletedAtUtc.Value)); writer.WriteEndObject();
        } return stream.ToArray();
    }
}

public sealed record BundesligaContextSourceCycleClaim(
    BundesligaContextSourceCycleIdentity Identity, BundesligaContextSource Source, string AttemptId,
    BundesligaContextSourceSourceStatus Status, string ClaimToken, DateTimeOffset ClaimedAtUtc, DateTimeOffset LeaseExpiresAtUtc,
    DateTimeOffset? FinalizedAtUtc, string? ObservationDigest, BundesligaContextSourceObservation? Observation,
    BundesligaContextSourceError? AbortCode, IReadOnlyList<string> ReceivedConsumers, DateTimeOffset? CompletedAtUtc)
{
    public const string Contract = "bundesliga-context-source-cycle-observation/v1";
    public string StorageId => BundesligaContextSourceHashing.SourceCycleStorageId(Identity, Source);
    public void Validate(IReadOnlyList<string> expectedConsumers)
    {
        if (!Enum.IsDefined(Identity.Scope) || !Enum.IsDefined(Source) || !Enum.IsDefined(Status)) throw new InvalidDataException("Source-cycle scope/source/status is invalid.");
        _ = BundesligaContextSourceCycleIdentity.Create(Identity.Competition, Identity.Scope, Identity.CycleId, Identity.Sequence);
        BundesligaContextSourceContract.ValidateClaimToken(ClaimToken);
        if (AttemptId != BundesligaContextSourceHashing.AttemptId(Identity, Source)) throw new InvalidDataException("Attempt ID mismatch.");
        BundesligaContextSourceContract.FormatUtc(ClaimedAtUtc); BundesligaContextSourceContract.FormatUtc(LeaseExpiresAtUtc);
        if (LeaseExpiresAtUtc - ClaimedAtUtc != TimeSpan.FromMinutes(10)) throw new InvalidDataException("Source lease must be exactly ten minutes.");
        if (!expectedConsumers.Take(ReceivedConsumers.Count).SequenceEqual(ReceivedConsumers, StringComparer.Ordinal)) throw new InvalidDataException("Received consumers must be a prefix.");
        if ((Status == BundesligaContextSourceSourceStatus.Complete) != (ReceivedConsumers.Count == expectedConsumers.Count)) throw new InvalidDataException("Only complete sources may contain the full receipt list.");
        var hasAnyFinalization = FinalizedAtUtc is not null || ObservationDigest is not null || Observation is not null;
        var hasCompleteFinalization = FinalizedAtUtc is not null && ObservationDigest is not null && Observation is not null;
        if (hasAnyFinalization != hasCompleteFinalization) throw new InvalidDataException("Finalization state is incomplete.");
        if (Status is BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete && !hasCompleteFinalization)
            throw new InvalidDataException("Finalized source state requires its immutable observation.");
        if (ReceivedConsumers.Count != 0 && !hasCompleteFinalization)
            throw new InvalidDataException("Received consumers require a complete finalized observation.");
        if (Status == BundesligaContextSourceSourceStatus.Claimed && hasCompleteFinalization)
            throw new InvalidDataException("Claimed source state cannot contain a finalized observation.");
        if (Status == BundesligaContextSourceSourceStatus.Claimed && ReceivedConsumers.Count != 0)
            throw new InvalidDataException("Claimed sources cannot have receipts.");
        if (FinalizedAtUtc is not null)
        {
            BundesligaContextSourceContract.FormatUtc(FinalizedAtUtc.Value);
            if (FinalizedAtUtc < ClaimedAtUtc || FinalizedAtUtc >= LeaseExpiresAtUtc)
                throw new InvalidDataException("Finalization must occur during the source lease.");
        }
        if (Observation is not null && ObservationDigest != Observation.ObservationDigest) throw new InvalidDataException("Observation digest mismatch.");
        if (Observation is not null && (Observation.Source != Source || Observation.AttemptId != AttemptId))
            throw new InvalidDataException("Observation source/attempt identity mismatch.");
        if (Status == BundesligaContextSourceSourceStatus.Aborted ^ AbortCode is not null) throw new InvalidDataException("Source abort matrix is invalid.");
        if (Status == BundesligaContextSourceSourceStatus.Complete ^ CompletedAtUtc is not null) throw new InvalidDataException("Source completion matrix is invalid.");
        if (CompletedAtUtc is not null)
        {
            BundesligaContextSourceContract.FormatUtc(CompletedAtUtc.Value);
            if (CompletedAtUtc < FinalizedAtUtc) throw new InvalidDataException("Completion cannot precede finalization.");
        }
    }

    public byte[] CreateCanonicalUtf8(IReadOnlyList<string> expectedConsumers)
    {
        Validate(expectedConsumers); using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteString("contract", Contract); writer.WriteString("competition", Identity.Competition); writer.WriteString("scope", Identity.ScopeValue); writer.WriteString("cycleId", Identity.CycleId); writer.WriteString("source", BundesligaContextSourceContract.SourceValue(Source)); writer.WriteString("attemptId", AttemptId); writer.WriteString("status", Status.ToString()); writer.WriteString("claimToken", ClaimToken); writer.WriteString("claimedAtUtc", BundesligaContextSourceContract.FormatUtc(ClaimedAtUtc)); writer.WriteString("leaseExpiresAtUtc", BundesligaContextSourceContract.FormatUtc(LeaseExpiresAtUtc)); writer.WriteString("finalizedAtUtc", FinalizedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(FinalizedAtUtc.Value)); writer.WriteString("observationDigest", ObservationDigest); writer.WritePropertyName("observation"); if (Observation is null) writer.WriteNullValue(); else { using var observation = JsonDocument.Parse(Observation.CreateCanonicalUtf8()); observation.RootElement.WriteTo(writer); } writer.WriteString("abortCode", AbortCode is null ? null : BundesligaContextSourceContract.ErrorValue(AbortCode.Value)); writer.WritePropertyName("receivedConsumers"); JsonSerializer.Serialize(writer, ReceivedConsumers); writer.WriteString("completedAtUtc", CompletedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(CompletedAtUtc.Value)); writer.WriteEndObject();
        } return stream.ToArray();
    }
}

public sealed record BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition Disposition, BundesligaContextSourceCycleClaim Claim);
public sealed record BundesligaContextSourceCycleStartResult(BundesligaContextSourceOuterCycle Cycle, IReadOnlyList<BundesligaContextSource> SupersededSources)
{
    public void Validate()
    {
        Cycle.Validate();
        if (SupersededSources is null || SupersededSources.Any(source => !Enum.IsDefined(source))
            || !SupersededSources.SequenceEqual(SupersededSources.Distinct().Order()))
            throw new InvalidDataException("Superseded source result is not canonical.");
    }
}

public interface IBundesligaContextSourceCycleRepository
{
    Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default);
    async Task<BundesligaContextSourceCycleStartResult> CreateOrResumeCycleWithResultAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default)
        => new(await CreateOrResumeCycleAsync(requested, cancellationToken), []);
    Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceRetainedRosterDescriptor?> GetRetainedRosterDescriptorAsync(string competition, BundesligaContextSourceScope scope, CancellationToken cancellationToken = default)
        => Task.FromResult<BundesligaContextSourceRetainedRosterDescriptor?>(null);
    Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default);
}

public interface IBundesligaContextSourceObservationProvider
{
    BundesligaContextSource Source { get; }
    Task<BundesligaContextSourceObservationResult> ObserveAsync(BundesligaContextSourceCycleIdentity cycle, CancellationToken cancellationToken = default);
}

public sealed record BundesligaContextSourceObservationResult(BundesligaContextSourceObservation Observation, byte[]? PayloadBytes)
{
    public void Validate()
    {
        Observation.Validate();
        if (Observation.Payload is null ^ PayloadBytes is null) throw new InvalidDataException("Observation payload bytes/identity matrix is invalid.");
        if (PayloadBytes is not null && (PayloadBytes.LongLength != Observation.Payload!.ByteLength || BundesligaContextSourceHashing.Sha256(PayloadBytes) != Observation.Payload.Sha256)) throw new InvalidDataException("Observation payload bytes do not match their identity.");
    }
}

public static class BundesligaContextSourceHashing
{
    public static string CycleStorageId(string competition, string scope, string cycleId) => HashFields("context-cycle-storage/v1", competition, scope, cycleId);
    public static string SourceCycleStorageId(BundesligaContextSourceCycleIdentity cycle, BundesligaContextSource source) => HashFields("context-source-cycle-storage/v1", cycle.Competition, cycle.ScopeValue, cycle.CycleId, BundesligaContextSourceContract.SourceValue(source));
    public static string ReceiptStorageId(BundesligaContextSourceCycleIdentity cycle, BundesligaContextSource source, string lane) => HashFields("context-source-receipt-storage/v1", cycle.Competition, cycle.ScopeValue, cycle.CycleId, BundesligaContextSourceContract.SourceValue(source), lane);
    public static string HealthStorageId(string competition, string scope, BundesligaContextSource source) => HashFields("context-source-health-storage/v1", competition, scope, BundesligaContextSourceContract.SourceValue(source));
    public static string AttemptId(BundesligaContextSourceCycleIdentity cycle, BundesligaContextSource source) => HashFields("context-source-attempt/v1", cycle.Competition, cycle.ScopeValue, cycle.CycleId, BundesligaContextSourceContract.SourceValue(source));
    public static string HashFields(string domain, params string[] fields)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendLp32(hash, Encoding.UTF8.GetBytes(domain));
        foreach (var field in fields) AppendLp32(hash, Encoding.UTF8.GetBytes(field ?? throw new ArgumentNullException(nameof(fields))));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static void ValidateSha(string? value) { if (value is null || !Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant)) throw new InvalidDataException("SHA-256 must be lowercase 64-hex."); }
    public static void ValidateOptionalSha(string? value) { if (value is not null) ValidateSha(value); }
    public static void AppendLp32(IncrementalHash hash, ReadOnlySpan<byte> bytes) { Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length)); hash.AppendData(length); hash.AppendData(bytes); }
    public static void AppendLp64(IncrementalHash hash, ReadOnlySpan<byte> bytes) { Span<byte> length = stackalloc byte[8]; BinaryPrimitives.WriteUInt64BigEndian(length, checked((ulong)bytes.Length)); hash.AppendData(length); hash.AppendData(bytes); }
}
