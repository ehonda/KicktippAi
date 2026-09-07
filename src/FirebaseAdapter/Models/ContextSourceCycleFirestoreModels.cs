using Google.Cloud.Firestore;

namespace FirebaseAdapter.Models;

[FirestoreData]
public sealed class FirestoreContextSourceCycle
{
    [FirestoreProperty("contract")] public string Contract { get; set; } = string.Empty;
    [FirestoreProperty("competition")] public string Competition { get; set; } = string.Empty;
    [FirestoreProperty("scope")] public string Scope { get; set; } = string.Empty;
    [FirestoreProperty("cycleId")] public string CycleId { get; set; } = string.Empty;
    [FirestoreProperty("cycleSequence")] public long CycleSequence { get; set; }
    [FirestoreProperty("startedAtUtc")] public string StartedAtUtc { get; set; } = string.Empty;
    [FirestoreProperty("stalenessReferenceAtUtc")] public string StalenessReferenceAtUtc { get; set; } = string.Empty;
    [FirestoreProperty("producerLaneId")] public string ProducerLaneId { get; set; } = string.Empty;
    [FirestoreProperty("expectedConsumers")] public List<string> ExpectedConsumers { get; set; } = [];
    [FirestoreProperty("enabledSources")] public List<string> EnabledSources { get; set; } = [];
    [FirestoreProperty("status")] public string Status { get; set; } = string.Empty;
    [FirestoreProperty("bundleSha256")] public string? BundleSha256 { get; set; }
    [FirestoreProperty("artifactName")] public string? ArtifactName { get; set; }
    [FirestoreProperty("abortCode")] public string? AbortCode { get; set; }
    [FirestoreProperty("completedAtUtc")] public string? CompletedAtUtc { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceCycleObservation
{
    [FirestoreProperty("contract")] public string Contract { get; set; } = string.Empty;
    [FirestoreProperty("competition")] public string Competition { get; set; } = string.Empty;
    [FirestoreProperty("scope")] public string Scope { get; set; } = string.Empty;
    [FirestoreProperty("cycleId")] public string CycleId { get; set; } = string.Empty;
    [FirestoreProperty("source")] public string Source { get; set; } = string.Empty;
    [FirestoreProperty("attemptId")] public string AttemptId { get; set; } = string.Empty;
    [FirestoreProperty("status")] public string Status { get; set; } = string.Empty;
    [FirestoreProperty("claimToken")] public string ClaimToken { get; set; } = string.Empty;
    [FirestoreProperty("claimedAtUtc")] public string ClaimedAtUtc { get; set; } = string.Empty;
    [FirestoreProperty("leaseExpiresAtUtc")] public string LeaseExpiresAtUtc { get; set; } = string.Empty;
    [FirestoreProperty("finalizedAtUtc")] public string? FinalizedAtUtc { get; set; }
    [FirestoreProperty("observationDigest")] public string? ObservationDigest { get; set; }
    [FirestoreProperty("observation")] public Dictionary<string, object?>? Observation { get; set; }
    [FirestoreProperty("abortCode")] public string? AbortCode { get; set; }
    [FirestoreProperty("receivedConsumers")] public List<string> ReceivedConsumers { get; set; } = [];
    [FirestoreProperty("completedAtUtc")] public string? CompletedAtUtc { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceReceipt
{
    [FirestoreProperty("contract")] public string Contract { get; set; } = string.Empty;
    [FirestoreProperty("competition")] public string Competition { get; set; } = string.Empty;
    [FirestoreProperty("scope")] public string Scope { get; set; } = string.Empty;
    [FirestoreProperty("cycleId")] public string CycleId { get; set; } = string.Empty;
    [FirestoreProperty("source")] public string Source { get; set; } = string.Empty;
    [FirestoreProperty("consumerLaneId")] public string ConsumerLaneId { get; set; } = string.Empty;
    [FirestoreProperty("communityContext")] public string CommunityContext { get; set; } = string.Empty;
    [FirestoreProperty("recordedAtUtc")] public string RecordedAtUtc { get; set; } = string.Empty;
    [FirestoreProperty("observationDigest")] public string ObservationDigest { get; set; } = string.Empty;
    [FirestoreProperty("bundleDigest")] public string BundleDigest { get; set; } = string.Empty;
    [FirestoreProperty("selectionDisposition")] public string SelectionDisposition { get; set; } = string.Empty;
    [FirestoreProperty("selectedSnapshotId")] public string SelectedSnapshotId { get; set; } = string.Empty;
    [FirestoreProperty("selectedOrigin")] public string SelectedOrigin { get; set; } = string.Empty;
    [FirestoreProperty("publicationDisposition")] public string PublicationDisposition { get; set; } = string.Empty;
    [FirestoreProperty("sourceDates")] public FirestoreContextSourceDates SourceDates { get; set; } = new();
    [FirestoreProperty("rosterRevision")] public string? RosterRevision { get; set; }
    [FirestoreProperty("carriedFields")] public FirestoreContextSourceCarriedFields CarriedFields { get; set; } = new();
    [FirestoreProperty("activeConditions")] public List<string> ActiveConditions { get; set; } = [];
}

[FirestoreData]
public sealed class FirestoreContextSourceDates
{
    [FirestoreProperty("ratedAt")] public string? RatedAt { get; set; }
    [FirestoreProperty("membershipCapturedAt")] public string? MembershipCapturedAt { get; set; }
    [FirestoreProperty("membershipEffectiveAt")] public string? MembershipEffectiveAt { get; set; }
    [FirestoreProperty("enrichmentCapturedAt")] public string? EnrichmentCapturedAt { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceCarriedFields
{
    [FirestoreProperty("ageCount")] public int AgeCount { get; set; }
    [FirestoreProperty("positionCount")] public int PositionCount { get; set; }
    [FirestoreProperty("marketValueCount")] public int MarketValueCount { get; set; }
    [FirestoreProperty("oldestFieldEffectiveAt")] public string? OldestFieldEffectiveAt { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceHealth
{
    [FirestoreProperty("contract")] public string Contract { get; set; } = string.Empty;
    [FirestoreProperty("competition")] public string Competition { get; set; } = string.Empty;
    [FirestoreProperty("scope")] public string Scope { get; set; } = string.Empty;
    [FirestoreProperty("source")] public string Source { get; set; } = string.Empty;
    [FirestoreProperty("watermark")] public FirestoreContextSourceWatermark Watermark { get; set; } = new();
    [FirestoreProperty("lastCompletedCycleId")] public string? LastCompletedCycleId { get; set; }
    [FirestoreProperty("consecutiveFailures")] public FirestoreContextSourceFailures ConsecutiveFailures { get; set; } = new();
    [FirestoreProperty("lastSuccessfulSourceDates")] public FirestoreContextSourceSuccessfulDates LastSuccessfulSourceDates { get; set; } = new();
    [FirestoreProperty("rosterRevisionState")] public FirestoreContextSourceRosterRevisionState? RosterRevisionState { get; set; }
    [FirestoreProperty("communitySelections")] public List<FirestoreContextSourceCommunitySelection> CommunitySelections { get; set; } = [];
    [FirestoreProperty("activeConditions")] public List<string> ActiveConditions { get; set; } = [];
    [FirestoreProperty("desiredIssueProjection")] public FirestoreContextSourceIssueProjection? DesiredIssueProjection { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceWatermark { [FirestoreProperty("sequence")] public long Sequence { get; set; } [FirestoreProperty("cycleId")] public string CycleId { get; set; } = string.Empty; }
[FirestoreData]
public sealed class FirestoreContextSourceFailures { [FirestoreProperty("acquisition")] public int Acquisition { get; set; } [FirestoreProperty("membership")] public int Membership { get; set; } [FirestoreProperty("enrichment")] public int Enrichment { get; set; } [FirestoreProperty("handoff")] public int Handoff { get; set; } }

[FirestoreData]
public sealed class FirestoreContextSourceSuccessfulDates
{
    [FirestoreProperty("ratedAt")] public string? RatedAt { get; set; }
    [FirestoreProperty("membershipEffectiveAt")] public string? MembershipEffectiveAt { get; set; }
    [FirestoreProperty("enrichmentCapturedAt")] public string? EnrichmentCapturedAt { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceRemoteIdentity
{
    [FirestoreProperty("etag")] public string? Etag { get; set; }
    [FirestoreProperty("byteLength")] public long? ByteLength { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceAcceptedRevision
{
    [FirestoreProperty("revision")] public string Revision { get; set; } = string.Empty;
    [FirestoreProperty("remoteIdentity")] public FirestoreContextSourceRemoteIdentity RemoteIdentity { get; set; } = new();
    [FirestoreProperty("policySha256")] public string PolicySha256 { get; set; } = string.Empty;
    [FirestoreProperty("descriptorSha256")] public string DescriptorSha256 { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class FirestoreContextSourcePendingRevision
{
    [FirestoreProperty("revision")] public string Revision { get; set; } = string.Empty;
    [FirestoreProperty("remoteIdentity")] public FirestoreContextSourceRemoteIdentity RemoteIdentity { get; set; } = new();
    [FirestoreProperty("policySha256")] public string PolicySha256 { get; set; } = string.Empty;
    [FirestoreProperty("firstSeenCycleId")] public string FirstSeenCycleId { get; set; } = string.Empty;
    [FirestoreProperty("lastFailureCode")] public string LastFailureCode { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class FirestoreContextSourceRosterRevisionState
{
    [FirestoreProperty("accepted")] public FirestoreContextSourceAcceptedRevision? Accepted { get; set; }
    [FirestoreProperty("pending")] public FirestoreContextSourcePendingRevision? Pending { get; set; }
}

[FirestoreData]
public sealed class FirestoreContextSourceCommunitySelection
{
    [FirestoreProperty("consumerLaneId")] public string ConsumerLaneId { get; set; } = string.Empty;
    [FirestoreProperty("communityContext")] public string CommunityContext { get; set; } = string.Empty;
    [FirestoreProperty("selectedSnapshotId")] public string SelectedSnapshotId { get; set; } = string.Empty;
    [FirestoreProperty("selectedOrigin")] public string SelectedOrigin { get; set; } = string.Empty;
    [FirestoreProperty("ratedAt")] public string? RatedAt { get; set; }
    [FirestoreProperty("membershipCapturedAt")] public string? MembershipCapturedAt { get; set; }
    [FirestoreProperty("membershipEffectiveAt")] public string? MembershipEffectiveAt { get; set; }
    [FirestoreProperty("enrichmentCapturedAt")] public string? EnrichmentCapturedAt { get; set; }
    [FirestoreProperty("conditions")] public List<string> Conditions { get; set; } = [];
}

[FirestoreData]
public sealed class FirestoreContextSourceIssueProjection
{
    [FirestoreProperty("marker")] public string Marker { get; set; } = string.Empty;
    [FirestoreProperty("title")] public string Title { get; set; } = string.Empty;
    [FirestoreProperty("bodySha256")] public string BodySha256 { get; set; } = string.Empty;
    [FirestoreProperty("desiredState")] public string DesiredState { get; set; } = string.Empty;
    [FirestoreProperty("appliedBodySha256")] public string? AppliedBodySha256 { get; set; }
    [FirestoreProperty("synchronizationStatus")] public string SynchronizationStatus { get; set; } = string.Empty;
    [FirestoreProperty("lastAttemptedAtUtc")] public string? LastAttemptedAtUtc { get; set; }
    [FirestoreProperty("lastErrorCode")] public string? LastErrorCode { get; set; }
}
