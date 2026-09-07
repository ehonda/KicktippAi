using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public enum BundesligaContextSourceHealthCondition { AcquisitionFailed, HandoffIncomplete, CycleAborted, ClubEloSourceRejected, ClubEloStaleGt7Days, RosterMembershipRejected, RosterEnrichmentRejected, RosterMembershipDateUnknown, RosterMembershipStaleGt14Days, RosterMembershipStaleGt30Days, RosterEnrichmentDateUnknown, RosterEnrichmentStaleGt14Days, RosterEnrichmentStaleGt30Days }
public enum BundesligaContextSourceSelectionDisposition { NetworkAccepted, NetworkCandidateRejected, NetworkCandidateStale, NetworkCandidateNotNewer, DuckDbAccepted, MixedPerClubSelection, CandidateRejected, MetadataUnchanged }
public enum BundesligaContextSourceSelectedOrigin { NetworkCandidate, LaunchSeed, LastKnownGood, DuckDb, Mixed, FallbackSeed }
public enum BundesligaContextSourcePublicationDisposition { Published, Unchanged, Reactivated, NotAttempted }
public enum BundesligaContextSourceIssueState { Open, Closed }
public enum BundesligaContextSourceIssueSynchronization { Synchronized, Pending }
public enum BundesligaContextSourceIssueError { GithubIssueListFailed, GithubIssueCreateFailed, GithubIssueUpdateFailed, GithubIssueCloseFailed }

public sealed record BundesligaContextSourceDates(DateOnly? RatedAt, DateOnly? MembershipCapturedAt, DateOnly? MembershipEffectiveAt, DateOnly? EnrichmentCapturedAt);
public sealed record BundesligaContextSourceCarriedFields(int AgeCount, int PositionCount, int MarketValueCount, DateOnly? OldestFieldEffectiveAt);

public sealed record BundesligaContextSourceReceiptRequest(
    BundesligaContextSourceCycleIdentity Identity, BundesligaContextSource Source, string ConsumerLaneId, string CommunityContext,
    string ObservationDigest, string BundleDigest, BundesligaContextSourceSelectionDisposition SelectionDisposition,
    string SelectedSnapshotId, BundesligaContextSourceSelectedOrigin SelectedOrigin,
    BundesligaContextSourcePublicationDisposition PublicationDisposition, BundesligaContextSourceDates SourceDates,
    string? RosterRevision, BundesligaContextSourceCarriedFields CarriedFields,
    IReadOnlyList<BundesligaContextSourceHealthCondition> ActiveConditions)
{
    public string StorageId => BundesligaContextSourceHashing.ReceiptStorageId(Identity, Source, ConsumerLaneId);
    public void Validate()
    {
        if (!Enum.IsDefined(Identity.Scope) || !Enum.IsDefined(Source) || !Enum.IsDefined(SelectionDisposition)
            || !Enum.IsDefined(SelectedOrigin) || !Enum.IsDefined(PublicationDisposition))
            throw new InvalidDataException("Receipt scope/source/disposition enums are invalid.");
        _ = BundesligaContextSourceCycleIdentity.Create(Identity.Competition, Identity.Scope, Identity.CycleId, Identity.Sequence);
        BundesligaContextSourceHashing.ValidateSha(ObservationDigest); BundesligaContextSourceHashing.ValidateSha(BundleDigest); BundesligaContextSourceHashing.ValidateSha(SelectedSnapshotId);
        if (string.IsNullOrWhiteSpace(CommunityContext)) throw new InvalidDataException("Receipt community is required.");
        var expected = Identity.Scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        if (!expected.Contains(ConsumerLaneId, StringComparer.Ordinal)) throw new InvalidDataException("Receipt lane is not in the frozen consumer list.");
        BundesligaContextSourceContract.ValidateConsumerAuthority(Identity.Scope, ConsumerLaneId, CommunityContext);
        if (CarriedFields.AgeCount < 0 || CarriedFields.PositionCount < 0 || CarriedFields.MarketValueCount < 0) throw new InvalidDataException("Carry counts must be non-negative.");
        ValidateReceiptConditionVocabulary();
        if (!BundesligaContextSourceHealth.ConditionsAreCanonical(ActiveConditions)) throw new InvalidDataException("Receipt conditions must be unique and contract-code ordered.");
        if (Source == BundesligaContextSource.ClubElo)
        {
            if (SelectionDisposition is not (BundesligaContextSourceSelectionDisposition.NetworkAccepted or BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected or BundesligaContextSourceSelectionDisposition.NetworkCandidateStale or BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer)) throw new InvalidDataException("Elo receipt selection is invalid.");
            if (SelectedOrigin is not (BundesligaContextSourceSelectedOrigin.NetworkCandidate or BundesligaContextSourceSelectedOrigin.LaunchSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood)) throw new InvalidDataException("Elo receipt origin is invalid.");
            if (SourceDates.RatedAt is null || SourceDates.MembershipCapturedAt is not null || SourceDates.MembershipEffectiveAt is not null || SourceDates.EnrichmentCapturedAt is not null || RosterRevision is not null) throw new InvalidDataException("Elo source-date matrix is invalid.");
            if (CarriedFields != new BundesligaContextSourceCarriedFields(0, 0, 0, null)) throw new InvalidDataException("Elo receipts cannot carry roster fields.");
        }
        else
        {
            if (SelectionDisposition is not (BundesligaContextSourceSelectionDisposition.DuckDbAccepted or BundesligaContextSourceSelectionDisposition.MixedPerClubSelection or BundesligaContextSourceSelectionDisposition.CandidateRejected or BundesligaContextSourceSelectionDisposition.MetadataUnchanged)) throw new InvalidDataException("Roster receipt selection is invalid.");
            if (SelectedOrigin is not (BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed or BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood)) throw new InvalidDataException("Roster receipt origin is invalid.");
            if (SourceDates.RatedAt is not null || SourceDates.MembershipEffectiveAt is null || string.IsNullOrEmpty(RosterRevision) || RosterRevision.Length != 40 || RosterRevision.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) throw new InvalidDataException("Roster source-date/revision matrix is invalid.");
            var unknown = ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
            if (SourceDates.EnrichmentCapturedAt is null != unknown) throw new InvalidDataException("Roster enrichment date/unknown condition mismatch.");
            if (SelectedOrigin is BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed
                && SourceDates.MembershipCapturedAt is null)
                throw new InvalidDataException("Artifact-selected roster membership requires a capture date.");
            if (SelectedOrigin == BundesligaContextSourceSelectedOrigin.FallbackSeed
                && SourceDates.MembershipCapturedAt is not null)
                throw new InvalidDataException("Fallback roster membership cannot claim an artifact capture date.");
            var carried = CarriedFields.AgeCount + CarriedFields.PositionCount + CarriedFields.MarketValueCount;
            if (carried == 0 && CarriedFields.OldestFieldEffectiveAt is not null) throw new InvalidDataException("Zero carries require a null oldest date.");
            if (carried > 0 && CarriedFields.OldestFieldEffectiveAt is null && !unknown) throw new InvalidDataException("Unknown legacy carries require their explicit condition.");
        }
    }

    private void ValidateReceiptConditionVocabulary()
    {
        foreach (var condition in ActiveConditions)
        {
            if (!Enum.IsDefined(condition)) throw new InvalidDataException("Receipt condition is not in the canonical vocabulary.");
            var valid = condition == BundesligaContextSourceHealthCondition.AcquisitionFailed
                || Source == BundesligaContextSource.ClubElo && condition is (BundesligaContextSourceHealthCondition.ClubEloSourceRejected or BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days)
                || Source == BundesligaContextSource.Rosters && condition is (BundesligaContextSourceHealthCondition.RosterMembershipRejected or BundesligaContextSourceHealthCondition.RosterEnrichmentRejected or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days);
            if (!valid) throw new InvalidDataException("Receipt condition is not valid for this source.");
        }
    }
}

public static class BundesligaContextSourceReceiptContract
{
    public static void ValidateAgainstObservation(BundesligaContextSourceReceiptRequest receipt, BundesligaContextSourceObservation observation)
    {
        receipt.Validate(); observation.Validate();
        if (receipt.Source != observation.Source || receipt.ObservationDigest != observation.ObservationDigest)
            throw new InvalidDataException("Receipt does not identify the finalized observation.");

        using var document = JsonDocument.Parse(observation.DescriptorJson);
        var descriptor = document.RootElement;
        var evaluation = descriptor.GetProperty("evaluation").GetString();
        var acquisitionFailed = receipt.Source == BundesligaContextSource.ClubElo
            ? observation.Disposition == BundesligaContextSourceDisposition.Rejected
            : evaluation is "TransportRejected" or "SizeRejected" or "RemoteDriftRejected" or "HashRejected" or "RevisionRejected" or "SchemaRejected";
        if (receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.HandoffIncomplete)
            || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.CycleAborted)
            || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.AcquisitionFailed) != acquisitionFailed)
            throw new InvalidDataException("Completed receipt conditions contradict the finalized observation.");
        if (receipt.Source == BundesligaContextSource.ClubElo)
        {
            var eligible = observation.Disposition == BundesligaContextSourceDisposition.ArtifactCaptured && evaluation == "Eligible";
            var selectionValid = observation.Disposition switch
            {
                BundesligaContextSourceDisposition.ArtifactCaptured => receipt.SelectionDisposition is BundesligaContextSourceSelectionDisposition.NetworkAccepted or BundesligaContextSourceSelectionDisposition.NetworkCandidateStale or BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer,
                BundesligaContextSourceDisposition.Rejected => receipt.SelectionDisposition == BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected,
                _ => false
            };
            var originValid = receipt.SelectionDisposition == BundesligaContextSourceSelectionDisposition.NetworkAccepted
                ? receipt.SelectedOrigin == BundesligaContextSourceSelectedOrigin.NetworkCandidate
                : receipt.SelectedOrigin is BundesligaContextSourceSelectedOrigin.LaunchSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood;
            var publicationValid = receipt.SelectionDisposition == BundesligaContextSourceSelectionDisposition.NetworkAccepted
                ? receipt.PublicationDisposition is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.Unchanged
                : receipt.PublicationDisposition is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.NotAttempted or BundesligaContextSourcePublicationDisposition.Reactivated;
            DateOnly? providerRatedAt = descriptor.GetProperty("providerRatedAt").ValueKind == JsonValueKind.Null
                ? null
                : DateOnly.ParseExact(descriptor.GetProperty("providerRatedAt").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var acceptedCandidateDateMatches = receipt.SelectionDisposition != BundesligaContextSourceSelectionDisposition.NetworkAccepted
                || eligible && receipt.SourceDates.RatedAt == providerRatedAt;
            var authoritativeConditions = DeriveHealthConditions(receipt, observation, null);
            if (!selectionValid || !originValid || !publicationValid || !acceptedCandidateDateMatches
                || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.ClubEloSourceRejected)
                    && !authoritativeConditions.Contains(BundesligaContextSourceHealthCondition.ClubEloSourceRejected))
                throw new InvalidDataException("Elo receipt contradicts the finalized observation.");
            return;
        }

        var rosterSelectionValid = observation.Disposition switch
        {
            BundesligaContextSourceDisposition.ArtifactCaptured => receipt.SelectionDisposition is BundesligaContextSourceSelectionDisposition.DuckDbAccepted or BundesligaContextSourceSelectionDisposition.MixedPerClubSelection or BundesligaContextSourceSelectionDisposition.CandidateRejected,
            BundesligaContextSourceDisposition.MetadataUnchanged => receipt.SelectionDisposition == BundesligaContextSourceSelectionDisposition.MetadataUnchanged,
            BundesligaContextSourceDisposition.Rejected => receipt.SelectionDisposition == BundesligaContextSourceSelectionDisposition.CandidateRejected,
            _ => false
        };
        var rosterOriginValid = receipt.SelectionDisposition switch
        {
            BundesligaContextSourceSelectionDisposition.DuckDbAccepted => receipt.SelectedOrigin == BundesligaContextSourceSelectedOrigin.DuckDb,
            BundesligaContextSourceSelectionDisposition.MixedPerClubSelection => receipt.SelectedOrigin == BundesligaContextSourceSelectedOrigin.Mixed,
            BundesligaContextSourceSelectionDisposition.CandidateRejected => receipt.SelectedOrigin is BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood,
            BundesligaContextSourceSelectionDisposition.MetadataUnchanged => receipt.SelectedOrigin is BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed or BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood,
            _ => false
        };
        var rosterPublicationValid = receipt.SelectionDisposition switch
        {
            BundesligaContextSourceSelectionDisposition.DuckDbAccepted or BundesligaContextSourceSelectionDisposition.MixedPerClubSelection => receipt.PublicationDisposition is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.Unchanged or BundesligaContextSourcePublicationDisposition.Reactivated,
            BundesligaContextSourceSelectionDisposition.CandidateRejected when receipt.SelectedOrigin == BundesligaContextSourceSelectedOrigin.FallbackSeed => receipt.PublicationDisposition is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.NotAttempted or BundesligaContextSourcePublicationDisposition.Reactivated,
            BundesligaContextSourceSelectionDisposition.CandidateRejected when receipt.SelectedOrigin == BundesligaContextSourceSelectedOrigin.LastKnownGood => receipt.PublicationDisposition == BundesligaContextSourcePublicationDisposition.NotAttempted,
            BundesligaContextSourceSelectionDisposition.MetadataUnchanged => receipt.PublicationDisposition == BundesligaContextSourcePublicationDisposition.Unchanged,
            _ => false
        };
        var advertisedRevision = descriptor.GetProperty("advertisedRevision").GetString();
        var artifactMembershipCaptureMatches = receipt.SelectionDisposition is not (BundesligaContextSourceSelectionDisposition.DuckDbAccepted or BundesligaContextSourceSelectionDisposition.MixedPerClubSelection)
            || observation.Disposition == BundesligaContextSourceDisposition.ArtifactCaptured
                && receipt.SourceDates.MembershipCapturedAt == DescriptorDate(descriptor, "artifactCaptureDate");
        var duckDbMembershipEffectiveDateMatches = receipt.SelectionDisposition != BundesligaContextSourceSelectionDisposition.DuckDbAccepted
            || receipt.SourceDates.MembershipEffectiveAt == DescriptorDate(descriptor, "membershipEffectiveDate");
        var retainedOutcomeMatches = receipt.SelectionDisposition != BundesligaContextSourceSelectionDisposition.MetadataUnchanged
            || MetadataUnchangedOutcomeMatches(receipt, descriptor);
        var authoritativeRosterConditions = DeriveHealthConditions(receipt, observation, null);
        var injectsRejection = receipt.ActiveConditions.Any(condition =>
            (condition is BundesligaContextSourceHealthCondition.RosterMembershipRejected or BundesligaContextSourceHealthCondition.RosterEnrichmentRejected)
            && !authoritativeRosterConditions.Contains(condition));
        if (!rosterSelectionValid || !rosterOriginValid || !rosterPublicationValid || receipt.RosterRevision != advertisedRevision
            || !artifactMembershipCaptureMatches || !duckDbMembershipEffectiveDateMatches || !retainedOutcomeMatches || injectsRejection)
            throw new InvalidDataException("Roster receipt contradicts the finalized observation.");
    }

    public static void ValidateFreshnessConditions(
        BundesligaContextSourceReceiptRequest receipt,
        DateOnly stalenessReferenceDate)
    {
        receipt.Validate();
        var expected = new HashSet<BundesligaContextSourceHealthCondition>();
        if (receipt.Source == BundesligaContextSource.ClubElo)
        {
            if (stalenessReferenceDate.DayNumber - receipt.SourceDates.RatedAt!.Value.DayNumber > 7)
                expected.Add(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        }
        else
        {
            ApplyRosterDateConditions(receipt.SourceDates.MembershipEffectiveAt, true, stalenessReferenceDate, expected);
            ApplyRosterDateConditions(receipt.SourceDates.EnrichmentCapturedAt, false, stalenessReferenceDate, expected);
        }

        var actual = receipt.ActiveConditions.Where(IsFreshnessCondition);
        if (!BundesligaContextSourceHealth.OrderConditions(actual).SequenceEqual(BundesligaContextSourceHealth.OrderConditions(expected)))
            throw new InvalidDataException("Receipt freshness conditions do not match the cycle staleness reference.");
    }

    internal static IReadOnlyList<BundesligaContextSourceHealthCondition> DeriveHealthConditions(
        BundesligaContextSourceReceiptRequest receipt,
        BundesligaContextSourceObservation observation,
        DateOnly? stalenessReferenceDate)
    {
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        var descriptor = document.RootElement;
        var evaluation = descriptor.GetProperty("evaluation").GetString()!;
        var conditions = new HashSet<BundesligaContextSourceHealthCondition>();
        var acquisitionFailed = receipt.Source == BundesligaContextSource.ClubElo
            ? observation.Disposition == BundesligaContextSourceDisposition.Rejected
            : evaluation is "TransportRejected" or "SizeRejected" or "RemoteDriftRejected" or "HashRejected" or "RevisionRejected" or "SchemaRejected";
        if (acquisitionFailed) conditions.Add(BundesligaContextSourceHealthCondition.AcquisitionFailed);

        if (receipt.Source == BundesligaContextSource.ClubElo)
        {
            if (observation.Disposition == BundesligaContextSourceDisposition.Rejected)
                conditions.Add(BundesligaContextSourceHealthCondition.ClubEloSourceRejected);
            if (stalenessReferenceDate is { } eloReference && receipt.SourceDates.RatedAt is { } ratedAt && eloReference.DayNumber - ratedAt.DayNumber > 7)
                conditions.Add(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
            return BundesligaContextSourceHealth.OrderConditions(conditions);
        }

        var outcomeEvaluation = evaluation;
        var diagnostics = observation.Diagnostics.ToHashSet(StringComparer.Ordinal);
        if (evaluation == "MetadataUnchanged")
        {
            outcomeEvaluation = descriptor.GetProperty("retainedEvaluation").GetString()!;
            diagnostics = descriptor.GetProperty("retainedDiagnostics").EnumerateArray().Select(value => value.GetString()!).ToHashSet(StringComparer.Ordinal);
        }
        if (outcomeEvaluation is "SourceDateRejected" or "SeasonRejected" or "IdentityRejected"
            || receipt.SelectionDisposition is BundesligaContextSourceSelectionDisposition.MixedPerClubSelection or BundesligaContextSourceSelectionDisposition.CandidateRejected
            || evaluation == "MetadataUnchanged" && receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected)
            || diagnostics.Contains("ROSTER_MEMBERSHIP_REJECTED"))
            conditions.Add(BundesligaContextSourceHealthCondition.RosterMembershipRejected);
        if (diagnostics.Contains("ROSTER_ENRICHMENT_REJECTED")
            || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected))
            conditions.Add(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);

        ApplyRosterDateConditions(receipt.SourceDates.MembershipEffectiveAt, true, stalenessReferenceDate, conditions);
        ApplyRosterDateConditions(receipt.SourceDates.EnrichmentCapturedAt, false, stalenessReferenceDate, conditions);
        return BundesligaContextSourceHealth.OrderConditions(conditions);
    }

    private static void ApplyRosterDateConditions(DateOnly? sourceDate, bool membership, DateOnly? referenceDate, ISet<BundesligaContextSourceHealthCondition> conditions)
    {
        if (sourceDate is null)
        {
            conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown : BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
            return;
        }
        if (referenceDate is not { } reference) return;
        var age = reference.DayNumber - sourceDate.Value.DayNumber;
        if (age > 14) conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days : BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days);
        if (age > 30) conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days : BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days);
    }

    private static DateOnly? DescriptorDate(JsonElement descriptor, string propertyName)
        => descriptor.GetProperty(propertyName).ValueKind == JsonValueKind.Null
            ? null
            : DateOnly.ParseExact(descriptor.GetProperty(propertyName).GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool IsFreshnessCondition(BundesligaContextSourceHealthCondition condition)
        => condition is BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days
            or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown
            or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days
            or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days
            or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown
            or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days
            or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days;

    private static bool MetadataUnchangedOutcomeMatches(BundesligaContextSourceReceiptRequest receipt, JsonElement descriptor)
    {
        var retainedEvaluation = descriptor.GetProperty("retainedEvaluation").GetString();
        var retainedDiagnostics = descriptor.GetProperty("retainedDiagnostics").EnumerateArray().Select(value => value.GetString()!).ToHashSet(StringComparer.Ordinal);
        var membershipRejected = retainedEvaluation is "SourceDateRejected" or "SeasonRejected" or "IdentityRejected";
        var enrichmentRejected = retainedDiagnostics.Contains("ROSTER_ENRICHMENT_REJECTED");
        return !receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.AcquisitionFailed)
            && (!membershipRejected || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected))
            && (!enrichmentRejected || receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected));
    }

    public static void ValidateMetadataUnchangedAgainstPriorReceipt(
        BundesligaContextSourceReceiptRequest receipt,
        BundesligaContextSourceReceipt priorReceipt)
    {
        receipt.Validate();
        priorReceipt.Validate();
        var repeatsMembershipOutcome = receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected)
            == (priorReceipt.Request.SelectionDisposition == BundesligaContextSourceSelectionDisposition.MixedPerClubSelection
                || priorReceipt.Request.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected));
        var repeatsEnrichmentOutcome = receipt.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected)
            == priorReceipt.Request.ActiveConditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
        if (receipt.SelectionDisposition != BundesligaContextSourceSelectionDisposition.MetadataUnchanged
            || receipt.Source != BundesligaContextSource.Rosters
            || priorReceipt.Request.Source != receipt.Source
            || priorReceipt.Request.ConsumerLaneId != receipt.ConsumerLaneId
            || priorReceipt.Request.CommunityContext != receipt.CommunityContext
            || priorReceipt.Request.SelectedSnapshotId != receipt.SelectedSnapshotId
            || priorReceipt.Request.SelectedOrigin != receipt.SelectedOrigin
            || priorReceipt.Request.SourceDates != receipt.SourceDates
            || priorReceipt.Request.CarriedFields != receipt.CarriedFields
            || !repeatsMembershipOutcome
            || !repeatsEnrichmentOutcome)
            throw new InvalidDataException("MetadataUnchanged receipt does not preserve the exact prior lane selection.");
    }
}

public sealed record BundesligaContextSourceReceipt(BundesligaContextSourceReceiptRequest Request, DateTimeOffset RecordedAtUtc)
{
    public const string Contract = "bundesliga-context-source-receipt/v1";
    public void Validate() { Request.Validate(); BundesligaContextSourceContract.FormatUtc(RecordedAtUtc); }
}

public sealed record BundesligaContextSourceWatermark(long Sequence, string CycleId) : IComparable<BundesligaContextSourceWatermark>
{
    public int CompareTo(BundesligaContextSourceWatermark? other) => other is null ? 1 : Sequence != other.Sequence ? Sequence.CompareTo(other.Sequence) : string.CompareOrdinal(CycleId, other.CycleId);
}
public sealed record BundesligaContextSourceFailures(int Acquisition, int Membership, int Enrichment, int Handoff);
public sealed record BundesligaContextSourceSuccessfulDates(DateOnly? RatedAt, DateOnly? MembershipEffectiveAt, DateOnly? EnrichmentCapturedAt);
public sealed record BundesligaContextSourceRemoteIdentity(string? Etag, long? ByteLength);
public sealed record BundesligaContextSourceAcceptedRevision(string Revision, BundesligaContextSourceRemoteIdentity RemoteIdentity, string PolicySha256, string DescriptorSha256);
public sealed record BundesligaContextSourcePendingRevision(string Revision, BundesligaContextSourceRemoteIdentity RemoteIdentity, string PolicySha256, string FirstSeenCycleId, string LastFailureCode);
public sealed record BundesligaContextSourceRosterRevisionState(BundesligaContextSourceAcceptedRevision? Accepted, BundesligaContextSourcePendingRevision? Pending);
public sealed record BundesligaContextSourceRetainedRosterDescriptor(
    string DescriptorSha256,
    string Revision,
    BundesligaContextSourceRemoteIdentity RemoteIdentity,
    string PolicySha256,
    string Evaluation,
    IReadOnlyList<string> Diagnostics)
{
    public void Validate()
    {
        BundesligaContextSourceHashing.ValidateSha(DescriptorSha256);
        BundesligaContextSourceHashing.ValidateSha(PolicySha256);
        if (Revision is null || Revision.Length != 40 || Revision.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character))) throw new InvalidDataException("Retained roster revision is invalid.");
        if (RemoteIdentity is null || (RemoteIdentity.Etag is null && RemoteIdentity.ByteLength is null) || (RemoteIdentity.Etag is not null && string.IsNullOrWhiteSpace(RemoteIdentity.Etag)) || RemoteIdentity.ByteLength < 0) throw new InvalidDataException("Retained roster remote identity is invalid.");
        if (Evaluation is not ("Eligible" or "SchemaRejected" or "SourceDateRejected" or "SeasonRejected" or "IdentityRejected")) throw new InvalidDataException("Retained roster evaluation is invalid.");
        if (Diagnostics is null || Diagnostics.Any(string.IsNullOrWhiteSpace) || !Diagnostics.SequenceEqual(Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Retained roster diagnostics are not canonical.");
    }
}
public sealed record BundesligaContextSourceCommunitySelection(string ConsumerLaneId, string CommunityContext, string SelectedSnapshotId, BundesligaContextSourceSelectedOrigin SelectedOrigin, DateOnly? RatedAt, DateOnly? MembershipCapturedAt, DateOnly? MembershipEffectiveAt, DateOnly? EnrichmentCapturedAt, IReadOnlyList<BundesligaContextSourceHealthCondition> Conditions);
public sealed record BundesligaContextSourceIssueProjection(string Marker, string Title, string BodySha256, BundesligaContextSourceIssueState DesiredState, string? AppliedBodySha256, BundesligaContextSourceIssueSynchronization SynchronizationStatus, DateTimeOffset? LastAttemptedAtUtc, BundesligaContextSourceIssueError? LastErrorCode);

public sealed record BundesligaContextSourceHealth(
    string Competition, BundesligaContextSourceScope Scope, BundesligaContextSource Source,
    BundesligaContextSourceWatermark Watermark, string? LastCompletedCycleId,
    BundesligaContextSourceFailures ConsecutiveFailures, BundesligaContextSourceSuccessfulDates LastSuccessfulSourceDates,
    BundesligaContextSourceRosterRevisionState? RosterRevisionState,
    IReadOnlyList<BundesligaContextSourceCommunitySelection> CommunitySelections,
    IReadOnlyList<BundesligaContextSourceHealthCondition> ActiveConditions,
    BundesligaContextSourceIssueProjection? DesiredIssueProjection)
{
    public const string Contract = "bundesliga-context-source-health/v1";
    public string StorageId => BundesligaContextSourceHashing.HealthStorageId(Competition, BundesligaContextSourceContract.ScopeValue(Scope), Source);

    public void Validate()
    {
        if (!Enum.IsDefined(Scope) || !Enum.IsDefined(Source)) throw new InvalidDataException("Health scope/source is invalid.");
        if (Competition != BundesligaContextSourceContract.Competition) throw new InvalidDataException("Health identity is invalid.");
        _ = BundesligaContextSourceCycleIdentity.Create(Competition, Scope, Watermark.CycleId, Watermark.Sequence);
        if (LastCompletedCycleId is not null
            && new BundesligaContextSourceWatermark(BundesligaContextSourceCycleIdentity.FromCycleId(Competition, Scope, LastCompletedCycleId).Sequence, LastCompletedCycleId).CompareTo(Watermark) > 0)
            throw new InvalidDataException("Completed cycle cannot be newer than the health watermark.");
        if (ConsecutiveFailures.Acquisition < 0 || ConsecutiveFailures.Membership < 0 || ConsecutiveFailures.Enrichment < 0 || ConsecutiveFailures.Handoff < 0) throw new InvalidDataException("Health counters must be non-negative.");
        if (Source == BundesligaContextSource.ClubElo && (RosterRevisionState is not null || LastSuccessfulSourceDates.MembershipEffectiveAt is not null || LastSuccessfulSourceDates.EnrichmentCapturedAt is not null || ConsecutiveFailures.Membership != 0 || ConsecutiveFailures.Enrichment != 0)) throw new InvalidDataException("Elo health null/counter matrix is invalid.");
        if (Source == BundesligaContextSource.Rosters && (RosterRevisionState is null || LastSuccessfulSourceDates.RatedAt is not null)) throw new InvalidDataException("Roster health null matrix is invalid.");
        ValidateRevisionState();
        var lanes = CommunitySelections.Select(x => x.ConsumerLaneId).ToArray();
        var expectedLanes = Scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        if (lanes.Distinct(StringComparer.Ordinal).Count() != lanes.Length || !lanes.SequenceEqual(lanes.Order(StringComparer.Ordinal), StringComparer.Ordinal) || lanes.Any(lane => !expectedLanes.Contains(lane, StringComparer.Ordinal))) throw new InvalidDataException("Health selections must contain unique frozen lanes in ordinal order.");
        if (LastCompletedCycleId == Watermark.CycleId && !lanes.SequenceEqual(expectedLanes.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Completed health requires complete frozen-lane selection coverage.");
        foreach (var selection in CommunitySelections) ValidateSelection(selection);
        if (!ConditionsAreCanonical(ActiveConditions)) throw new InvalidDataException("Health conditions must be unique and contract-code ordered.");
        if (Source == BundesligaContextSource.ClubElo && ActiveConditions.Any(IsRosterOnlyCondition)) throw new InvalidDataException("Elo health cannot contain roster-only conditions.");
        if (Source == BundesligaContextSource.Rosters && ActiveConditions.Any(condition => condition is BundesligaContextSourceHealthCondition.ClubEloSourceRejected or BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days)) throw new InvalidDataException("Roster health cannot contain Elo-only conditions.");
        if (Scope == BundesligaContextSourceScope.Development && DesiredIssueProjection is not null) throw new InvalidDataException("Development health cannot project GitHub issues.");
        if (Scope == BundesligaContextSourceScope.ProductionLive && DesiredIssueProjection is null) throw new InvalidDataException("Production health requires an issue projection.");
        if (DesiredIssueProjection is { } issue)
        {
            var source = BundesligaContextSourceContract.SourceValue(Source);
            var marker = $"<!-- kicktippai:context-source-health:bundesliga-2026-27:{source} -->";
            var title = $"[KicktippAi] Bundesliga 2026/27 {source} context-source health";
            var expectedBodySha = HashIssueBody(CreateIssueBody(marker, Competition, Source, Watermark, ActiveConditions));
            var shouldOpen = ConsecutiveFailures.Acquisition >= 2 || ConsecutiveFailures.Membership >= 2 || ConsecutiveFailures.Enrichment >= 2 || ConsecutiveFailures.Handoff >= 2 || ActiveConditions.Any(IsStaleOrUnknown);
            if (issue.Marker != marker || issue.Title != title || issue.BodySha256 != expectedBodySha || issue.DesiredState != (shouldOpen ? BundesligaContextSourceIssueState.Open : BundesligaContextSourceIssueState.Closed)) throw new InvalidDataException("Issue projection does not match canonical health state.");
        }
        DesiredIssueProjection?.Validate();
    }

    public static bool ConditionsAreCanonical(IReadOnlyList<BundesligaContextSourceHealthCondition> conditions)
        => conditions.SequenceEqual(OrderConditions(conditions));

    public static BundesligaContextSourceHealthCondition[] OrderConditions(IEnumerable<BundesligaContextSourceHealthCondition> conditions)
        => conditions.Distinct().OrderBy(ToContractValue, StringComparer.Ordinal).ToArray();

    private void ValidateRevisionState()
    {
        if (RosterRevisionState is null) return;
        if (RosterRevisionState.Accepted is { } accepted)
        {
            ValidateRevision(accepted.Revision); ValidateRemoteIdentity(accepted.RemoteIdentity);
            BundesligaContextSourceHashing.ValidateSha(accepted.PolicySha256); BundesligaContextSourceHashing.ValidateSha(accepted.DescriptorSha256);
        }
        if (RosterRevisionState.Pending is { } pending)
        {
            ValidateRevision(pending.Revision); ValidateRemoteIdentity(pending.RemoteIdentity); BundesligaContextSourceHashing.ValidateSha(pending.PolicySha256);
            if (Source != BundesligaContextSource.Rosters) throw new InvalidDataException("Only roster health can retain a pending revision.");
            var firstSeen = BundesligaContextSourceCycleIdentity.FromCycleId(Competition, Scope, pending.FirstSeenCycleId);
            if (new BundesligaContextSourceWatermark(firstSeen.Sequence, firstSeen.CycleId).CompareTo(Watermark) > 0)
                throw new InvalidDataException("Pending revision cannot be first seen after the health watermark.");
            if (pending.LastFailureCode is not ("TransportRejected" or "SizeRejected" or "RemoteDriftRejected" or "HashRejected" or "RevisionRejected")) throw new InvalidDataException("Pending roster failure is not canonical.");
        }
        if (RosterRevisionState.Accepted is { } a && RosterRevisionState.Pending is { } p && a.Revision == p.Revision && a.RemoteIdentity == p.RemoteIdentity && a.PolicySha256 == p.PolicySha256) throw new InvalidDataException("Accepted and pending revision tuples cannot match.");
    }

    private void ValidateSelection(BundesligaContextSourceCommunitySelection selection)
    {
        if (!Enum.IsDefined(selection.SelectedOrigin)) throw new InvalidDataException("Health selection origin is invalid.");
        if (string.IsNullOrWhiteSpace(selection.CommunityContext)) throw new InvalidDataException("Health selection community is required.");
        BundesligaContextSourceContract.ValidateConsumerAuthority(Scope, selection.ConsumerLaneId, selection.CommunityContext);
        BundesligaContextSourceHashing.ValidateSha(selection.SelectedSnapshotId);
        if (!ConditionsAreCanonical(selection.Conditions)) throw new InvalidDataException("Health selection conditions are not canonical.");
        if (Source == BundesligaContextSource.ClubElo)
        {
            if (selection.SelectedOrigin is not (BundesligaContextSourceSelectedOrigin.NetworkCandidate or BundesligaContextSourceSelectedOrigin.LaunchSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood)
                || selection.RatedAt is null || selection.MembershipCapturedAt is not null || selection.MembershipEffectiveAt is not null || selection.EnrichmentCapturedAt is not null
                || selection.Conditions.Any(IsRosterOnlyCondition))
                throw new InvalidDataException("Elo health selection matrix is invalid.");
        }
        else
        {
            var unknown = selection.Conditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
            if (selection.SelectedOrigin is not (BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed or BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood)
                || selection.RatedAt is not null || selection.MembershipEffectiveAt is null || (selection.EnrichmentCapturedAt is null) != unknown
                || (selection.SelectedOrigin is BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed) && selection.MembershipCapturedAt is null
                || selection.SelectedOrigin == BundesligaContextSourceSelectedOrigin.FallbackSeed && selection.MembershipCapturedAt is not null
                || selection.Conditions.Any(condition => condition is BundesligaContextSourceHealthCondition.ClubEloSourceRejected or BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days))
                throw new InvalidDataException("Roster health selection matrix is invalid.");
        }
    }

    private static void ValidateRevision(string revision)
    {
        if (revision.Length != 40 || revision.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character))) throw new InvalidDataException("Roster revision must be lowercase 40-hex.");
    }

    private static void ValidateRemoteIdentity(BundesligaContextSourceRemoteIdentity identity)
    {
        if ((identity.Etag is null && identity.ByteLength is null) || (identity.Etag is not null && string.IsNullOrWhiteSpace(identity.Etag)) || identity.ByteLength < 0) throw new InvalidDataException("Roster remote identity is invalid.");
    }

    private static bool IsStaleOrUnknown(BundesligaContextSourceHealthCondition condition)
        => condition is BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days;

    private static bool IsRosterOnlyCondition(BundesligaContextSourceHealthCondition condition)
        => condition is BundesligaContextSourceHealthCondition.RosterMembershipRejected or BundesligaContextSourceHealthCondition.RosterEnrichmentRejected or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days;

    public static string CreateIssueBody(string marker, string competition, BundesligaContextSource source, BundesligaContextSourceWatermark watermark, IEnumerable<BundesligaContextSourceHealthCondition> conditions)
    {
        var ordered = conditions.Select(ToContractValue).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(condition => $"- `{condition}`").ToArray();
        return string.Join('\n', [marker, $"Competition: `{competition}`", "Scope: `production-live`", $"Source: `{BundesligaContextSourceContract.SourceValue(source)}`", $"Watermark: `{watermark.Sequence}:{watermark.CycleId}`", ..(ordered.Length == 0 ? ["- `NONE`"] : ordered)]) + "\n";
    }
    public static string HashIssueBody(string body) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    public static string ToContractValue(BundesligaContextSourceHealthCondition condition) => condition switch
    {
        BundesligaContextSourceHealthCondition.AcquisitionFailed => "ACQUISITION_FAILED", BundesligaContextSourceHealthCondition.HandoffIncomplete => "HANDOFF_INCOMPLETE", BundesligaContextSourceHealthCondition.CycleAborted => "CYCLE_ABORTED", BundesligaContextSourceHealthCondition.ClubEloSourceRejected => "CLUB_ELO_SOURCE_REJECTED", BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days => "CLUB_ELO_STALE_GT_7_DAYS", BundesligaContextSourceHealthCondition.RosterMembershipRejected => "ROSTER_MEMBERSHIP_REJECTED", BundesligaContextSourceHealthCondition.RosterEnrichmentRejected => "ROSTER_ENRICHMENT_REJECTED", BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown => "ROSTER_MEMBERSHIP_DATE_UNKNOWN", BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days => "ROSTER_MEMBERSHIP_STALE_GT_14_DAYS", BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days => "ROSTER_MEMBERSHIP_STALE_GT_30_DAYS", BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown => "ROSTER_ENRICHMENT_DATE_UNKNOWN", BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days => "ROSTER_ENRICHMENT_STALE_GT_14_DAYS", BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days => "ROSTER_ENRICHMENT_STALE_GT_30_DAYS", _ => throw new ArgumentOutOfRangeException(nameof(condition))
    };
}

public static class BundesligaContextSourceIssueProjectionExtensions
{
    public static void Validate(this BundesligaContextSourceIssueProjection value)
    {
        if (!Enum.IsDefined(value.DesiredState) || !Enum.IsDefined(value.SynchronizationStatus) || value.LastErrorCode is { } error && !Enum.IsDefined(error)) throw new InvalidDataException("Issue projection enums are invalid.");
        BundesligaContextSourceHashing.ValidateSha(value.BodySha256); BundesligaContextSourceHashing.ValidateOptionalSha(value.AppliedBodySha256);
        if (value.LastAttemptedAtUtc is not null) BundesligaContextSourceContract.FormatUtc(value.LastAttemptedAtUtc.Value);
        if (value.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Synchronized && (value.AppliedBodySha256 != value.BodySha256 || value.LastAttemptedAtUtc is null || value.LastErrorCode is not null)) throw new InvalidDataException("Synchronized issue projection is inconsistent.");
        if (value.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Pending && value.LastAttemptedAtUtc is null && (value.AppliedBodySha256 is not null || value.LastErrorCode is not null)) throw new InvalidDataException("Unattempted pending issue projection is inconsistent.");
        if (value.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Pending && value.LastAttemptedAtUtc is not null && value.LastErrorCode is null) throw new InvalidDataException("Failed pending issue projection requires an error.");
    }
}

public static class BundesligaContextSourceHealthReducer
{
    private static readonly HashSet<string> AcquisitionFailures = ["TransportRejected", "SizeRejected", "RemoteDriftRejected", "HashRejected", "RevisionRejected", "SchemaRejected"];

    public static BundesligaContextSourceHealth ReduceCompleted(BundesligaContextSourceHealth? previous, BundesligaContextSourceOuterCycle cycle, BundesligaContextSourceCycleClaim sourceCycle, IReadOnlyList<BundesligaContextSourceReceipt> receipts)
    {
        cycle.Validate(); sourceCycle.Validate(cycle.ExpectedConsumers);
        var watermark = new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId);
        if (previous is not null && watermark.CompareTo(previous.Watermark) < 0) return previous;
        if (previous?.LastCompletedCycleId == cycle.Identity.CycleId) return previous;
        if (receipts.Count != cycle.ExpectedConsumers.Count || !receipts.Select(x => x.Request.ConsumerLaneId).SequenceEqual(cycle.ExpectedConsumers, StringComparer.Ordinal)) throw new InvalidDataException("A complete reduction requires all receipts in exact consumer order.");
        var advertisedRevision = sourceCycle.Source == BundesligaContextSource.Rosters ? AdvertisedRevision(sourceCycle.Observation!) : null;
        foreach (var receipt in receipts) { receipt.Validate(); BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt.Request, sourceCycle.Observation!); if (receipt.Request.Identity != cycle.Identity || receipt.Request.Source != sourceCycle.Source || receipt.Request.ObservationDigest != sourceCycle.ObservationDigest || (advertisedRevision is not null && receipt.Request.RosterRevision != advertisedRevision)) throw new InvalidDataException("Receipt identity/digest/revision mismatch."); }
        var prior = previous?.ConsecutiveFailures ?? new BundesligaContextSourceFailures(0, 0, 0, 0);
        var evaluation = Evaluation(sourceCycle.Observation!);
        var acquisitionFailed = sourceCycle.Source == BundesligaContextSource.ClubElo ? sourceCycle.Observation!.Disposition == BundesligaContextSourceDisposition.Rejected : AcquisitionFailures.Contains(evaluation);
        var referenceDate = DateOnly.FromDateTime(cycle.StalenessReferenceAtUtc.UtcDateTime);
        var derivedReceiptConditions = receipts.ToDictionary(
            receipt => receipt.Request.ConsumerLaneId,
            receipt => BundesligaContextSourceReceiptContract.DeriveHealthConditions(receipt.Request, sourceCycle.Observation!, referenceDate),
            StringComparer.Ordinal);
        var membershipFailed = sourceCycle.Source == BundesligaContextSource.Rosters && derivedReceiptConditions.Values.Any(conditions => conditions.Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected));
        var enrichmentFailed = sourceCycle.Source == BundesligaContextSource.Rosters && derivedReceiptConditions.Values.Any(conditions => conditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected));
        var failures = new BundesligaContextSourceFailures(acquisitionFailed ? prior.Acquisition + 1 : 0, membershipFailed ? prior.Membership + 1 : 0, enrichmentFailed ? prior.Enrichment + 1 : 0, 0);
        var conditions = derivedReceiptConditions.Values.SelectMany(value => value).ToHashSet();
        var dates = sourceCycle.Source == BundesligaContextSource.ClubElo
            ? new BundesligaContextSourceSuccessfulDates(receipts.Min(x => x.Request.SourceDates.RatedAt), null, null)
            : new BundesligaContextSourceSuccessfulDates(null, receipts.Min(x => x.Request.SourceDates.MembershipEffectiveAt), receipts.Where(x => x.Request.SourceDates.EnrichmentCapturedAt is not null).Select(x => x.Request.SourceDates.EnrichmentCapturedAt).DefaultIfEmpty(null).Min());
        ApplyStaleness(sourceCycle.Source, referenceDate, dates, conditions);
        var revisionState = sourceCycle.Source == BundesligaContextSource.Rosters ? ReduceRevision(previous?.RosterRevisionState, sourceCycle, cycle.Identity.CycleId, evaluation) : null;
        var selections = receipts.Select(x => new BundesligaContextSourceCommunitySelection(x.Request.ConsumerLaneId, x.Request.CommunityContext, x.Request.SelectedSnapshotId, x.Request.SelectedOrigin, x.Request.SourceDates.RatedAt, x.Request.SourceDates.MembershipCapturedAt, x.Request.SourceDates.MembershipEffectiveAt, x.Request.SourceDates.EnrichmentCapturedAt, derivedReceiptConditions[x.Request.ConsumerLaneId])).OrderBy(x => x.ConsumerLaneId, StringComparer.Ordinal).ToArray();
        var orderedConditions = BundesligaContextSourceHealth.OrderConditions(conditions);
        BundesligaContextSourceIssueProjection? projection = null;
        if (cycle.Identity.Scope == BundesligaContextSourceScope.ProductionLive)
        {
            var source = BundesligaContextSourceContract.SourceValue(sourceCycle.Source); var marker = $"<!-- kicktippai:context-source-health:bundesliga-2026-27:{source} -->";
            var body = BundesligaContextSourceHealth.CreateIssueBody(marker, cycle.Identity.Competition, sourceCycle.Source, watermark, orderedConditions);
            var open = failures.Acquisition >= 2 || failures.Membership >= 2 || failures.Enrichment >= 2 || failures.Handoff >= 2 || orderedConditions.Any(IsStaleOrUnknown);
            projection = new BundesligaContextSourceIssueProjection(marker, $"[KicktippAi] Bundesliga 2026/27 {source} context-source health", BundesligaContextSourceHealth.HashIssueBody(body), open ? BundesligaContextSourceIssueState.Open : BundesligaContextSourceIssueState.Closed, null, BundesligaContextSourceIssueSynchronization.Pending, null, null);
        }
        var health = new BundesligaContextSourceHealth(cycle.Identity.Competition, cycle.Identity.Scope, sourceCycle.Source, watermark, cycle.Identity.CycleId, failures, dates, revisionState, selections, orderedConditions, projection);
        health.Validate(); return health;
    }

    private static string Evaluation(BundesligaContextSourceObservation observation) { using var doc = JsonDocument.Parse(observation.DescriptorJson); return doc.RootElement.GetProperty("evaluation").GetString()!; }
    private static string AdvertisedRevision(BundesligaContextSourceObservation observation) { using var doc = JsonDocument.Parse(observation.DescriptorJson); return doc.RootElement.GetProperty("advertisedRevision").GetString()!; }
    private static void ApplyStaleness(BundesligaContextSource source, DateOnly reference, BundesligaContextSourceSuccessfulDates dates, ISet<BundesligaContextSourceHealthCondition> conditions)
    {
        if (source == BundesligaContextSource.ClubElo) { if (dates.RatedAt is not null && reference.DayNumber - dates.RatedAt.Value.DayNumber > 7) conditions.Add(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days); return; }
        if (dates.MembershipEffectiveAt is null) conditions.Add(BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown); else { var age = reference.DayNumber - dates.MembershipEffectiveAt.Value.DayNumber; if (age > 14) conditions.Add(BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days); if (age > 30) conditions.Add(BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days); }
        if (dates.EnrichmentCapturedAt is null) conditions.Add(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown); else { var age = reference.DayNumber - dates.EnrichmentCapturedAt.Value.DayNumber; if (age > 14) conditions.Add(BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days); if (age > 30) conditions.Add(BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days); }
    }
    private static bool IsStaleOrUnknown(BundesligaContextSourceHealthCondition c) => c is BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days;
    private static BundesligaContextSourceRosterRevisionState ReduceRevision(BundesligaContextSourceRosterRevisionState? previous, BundesligaContextSourceCycleClaim claim, string cycleId, string evaluation)
    {
        using var doc = JsonDocument.Parse(claim.Observation!.DescriptorJson); var root = doc.RootElement;
        if (evaluation == "MetadataUnchanged") return new BundesligaContextSourceRosterRevisionState(previous?.Accepted, null);
        var revision = root.GetProperty("advertisedRevision").GetString()!; var policy = root.GetProperty("policySha256").GetString()!; var before = root.GetProperty("remoteIdentityBefore");
        if (before.ValueKind == JsonValueKind.Null) return previous ?? new BundesligaContextSourceRosterRevisionState(null, null);
        var remote = new BundesligaContextSourceRemoteIdentity(before.GetProperty("etag").ValueKind == JsonValueKind.Null ? null : before.GetProperty("etag").GetString(), before.GetProperty("byteLength").ValueKind == JsonValueKind.Null ? null : before.GetProperty("byteLength").GetInt64());
        if (evaluation is "Eligible" or "SchemaRejected" or "SourceDateRejected" or "SeasonRejected" or "IdentityRejected") return new BundesligaContextSourceRosterRevisionState(new BundesligaContextSourceAcceptedRevision(revision, remote, policy, claim.Observation.DescriptorSha256), null);
        var first = previous?.Pending is { } pending && pending.Revision == revision && pending.RemoteIdentity == remote && pending.PolicySha256 == policy ? pending.FirstSeenCycleId : cycleId;
        return new BundesligaContextSourceRosterRevisionState(previous?.Accepted, new BundesligaContextSourcePendingRevision(revision, remote, policy, first, evaluation));
    }
}

public sealed record BundesligaContextSourceIssueProjectionAttempt(string? AppliedBodySha256, BundesligaContextSourceIssueError? ErrorCode)
{
    public static BundesligaContextSourceIssueProjectionAttempt Synchronized(string bodySha256) => new(bodySha256, null);
    public static BundesligaContextSourceIssueProjectionAttempt Failed(BundesligaContextSourceIssueError errorCode, string? appliedBodySha256 = null) => new(appliedBodySha256, errorCode);
}

public interface IBundesligaContextSourceIssueProjector
{
    Task<BundesligaContextSourceIssueProjectionAttempt> ProjectAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken = default);
}
