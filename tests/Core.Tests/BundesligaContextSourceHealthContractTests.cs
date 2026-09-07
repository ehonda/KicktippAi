using EHonda.KicktippAi.Core;
using System.Text.Json;

namespace Core.Tests;

public class BundesligaContextSourceHealthContractTests
{
    [Test]
    public async Task Issue_body_is_exact_LF_ordered_and_hash_bound()
    {
        var watermark = new BundesligaContextSourceWatermark(1, "gha:1:1");
        var body = BundesligaContextSourceHealth.CreateIssueBody("<!-- marker -->", BundesligaContextSourceContract.Competition, BundesligaContextSource.Rosters, watermark, [BundesligaContextSourceHealthCondition.HandoffIncomplete, BundesligaContextSourceHealthCondition.RosterEnrichmentRejected, BundesligaContextSourceHealthCondition.CycleAborted, BundesligaContextSourceHealthCondition.AcquisitionFailed]);
        await Assert.That(body).IsEqualTo("<!-- marker -->\nCompetition: `bundesliga-2026-27`\nScope: `production-live`\nSource: `rosters`\nWatermark: `1:gha:1:1`\n- `ACQUISITION_FAILED`\n- `CYCLE_ABORTED`\n- `HANDOFF_INCOMPLETE`\n- `ROSTER_ENRICHMENT_REJECTED`\n");
        await Assert.That(BundesligaContextSourceHealth.HashIssueBody(body)).Matches("^[0-9a-f]{64}$");
    }

    [Test]
    public async Task Receipt_enforces_Elo_and_roster_null_matrices()
    {
        var elo = Receipt(BundesligaContextSource.ClubElo, new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null), null, [], BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, BundesligaContextSourceSelectedOrigin.LaunchSeed);
        elo.Validate();
        await Assert.That(() => (elo with { SourceDates = elo.SourceDates with { MembershipEffectiveAt = new DateOnly(2026, 9, 1) } }).Validate()).Throws<InvalidDataException>();
        var roster = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed);
        roster.Validate();
        await Assert.That(() => (roster with { ActiveConditions = [] }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (roster with { SourceDates = roster.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 8, 1) } }).Validate()).Throws<InvalidDataException>();

        var artifactMembership = roster with
        {
            SelectionDisposition = BundesligaContextSourceSelectionDisposition.DuckDbAccepted,
            SelectedOrigin = BundesligaContextSourceSelectedOrigin.DuckDb,
            PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published
        };
        await Assert.That(() => artifactMembership.Validate()).Throws<InvalidDataException>();
        (artifactMembership with { SourceDates = artifactMembership.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 8, 1) } }).Validate();

        var mixedMembership = artifactMembership with
        {
            SelectionDisposition = BundesligaContextSourceSelectionDisposition.MixedPerClubSelection,
            SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed
        };
        await Assert.That(() => (mixedMembership with { SourceDates = mixedMembership.SourceDates with { MembershipCapturedAt = null } }).Validate()).Throws<InvalidDataException>();
        (mixedMembership with { SourceDates = mixedMembership.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 8, 1) } }).Validate();

        (roster with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood }).Validate();
        (roster with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood, SourceDates = roster.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 7, 1) } }).Validate();
    }

    [Test]
    public async Task Receipt_condition_vocabulary_is_source_scoped_and_excludes_health_only_codes()
    {
        var elo = Receipt(BundesligaContextSource.ClubElo, new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null), null, [BundesligaContextSourceHealthCondition.AcquisitionFailed], BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, BundesligaContextSourceSelectedOrigin.LaunchSeed);
        var roster = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed);
        elo.Validate(); roster.Validate();

        foreach (var condition in new[] { BundesligaContextSourceHealthCondition.ClubEloSourceRejected, BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days })
            (elo with { ActiveConditions = [condition] }).Validate();
        foreach (var condition in new[] { BundesligaContextSourceHealthCondition.RosterMembershipRejected, BundesligaContextSourceHealthCondition.RosterEnrichmentRejected, BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days, BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days, BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown, BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days, BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days })
            (roster with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions([BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown, condition]) }).Validate();

        await Assert.That(() => (elo with { ActiveConditions = [BundesligaContextSourceHealthCondition.RosterMembershipRejected] }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (roster with { ActiveConditions = [BundesligaContextSourceHealthCondition.ClubEloSourceRejected] }).Validate()).Throws<InvalidDataException>();
        foreach (var healthOnly in new[] { BundesligaContextSourceHealthCondition.HandoffIncomplete, BundesligaContextSourceHealthCondition.CycleAborted, BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown })
        {
            await Assert.That(() => (elo with { ActiveConditions = [healthOnly] }).Validate()).Throws<InvalidDataException>();
            await Assert.That(() => (roster with { ActiveConditions = [healthOnly] }).Validate()).Throws<InvalidDataException>();
        }
        await Assert.That(() => (elo with { ActiveConditions = [(BundesligaContextSourceHealthCondition)999] }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Health_cycle_references_accept_lower_and_equal_but_reject_future_for_local_and_production()
    {
        var local = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1468-7000-8000-000000000000");
        CreateEloHealth(local, "local:0198f865-1467-7000-8000-000000000000", false).Validate();
        CreateEloHealth(local, local.CycleId, true).Validate();
        await Assert.That(() => CreateEloHealth(local, "local:0198f865-1469-7000-8000-000000000000", false).Validate()).Throws<InvalidDataException>();

        var production = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 2, 100);
        CreateEloHealth(production, "gha:1:100", false).Validate();
        CreateEloHealth(production, production.CycleId, true).Validate();
        await Assert.That(() => CreateEloHealth(production, "gha:3:100", false).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Roster_pending_first_seen_references_are_causal_for_local_and_production_ties()
    {
        var local = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1468-7000-8000-000000000002");
        CreateRosterHealth(local, "local:0198f865-1467-7000-8000-000000000000").Validate();
        CreateRosterHealth(local, local.CycleId).Validate();
        CreateRosterHealth(local, "local:0198f865-1468-7000-8000-000000000001").Validate();
        await Assert.That(() => CreateRosterHealth(local, "local:0198f865-1468-7000-8000-000000000003").Validate()).Throws<InvalidDataException>();

        var production = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 2, 100);
        CreateRosterHealth(production, "gha:1:100").Validate();
        CreateRosterHealth(production, production.CycleId).Validate();
        await Assert.That(() => CreateRosterHealth(production, "gha:3:100").Validate()).Throws<InvalidDataException>();
        await Assert.That(() => CreateRosterHealth(production, local.CycleId).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reducer_counts_once_marks_staleness_and_preserves_late_state()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2); var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.HandoffReady, new string('b', 64), $"bundesliga-context-source-bundle-{identity.StorageId}");
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), now, BundesligaContextSourceDisposition.Rejected, RosterDescriptor("SourceDateRejected"), null, ["UNKNOWN_SOURCE_DATE"]);
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var receipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 7, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed, identity, lane, observation.ObservationDigest, new string('b', 64)), now)).ToArray();
        var wrongRevision = receipts.ToArray(); wrongRevision[0] = wrongRevision[0] with { Request = wrongRevision[0].Request with { RosterRevision = new string('f', 40) } };
        await Assert.That(() => BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, wrongRevision)).Throws<InvalidDataException>();
        var first = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, receipts);
        var replay = BundesligaContextSourceHealthReducer.ReduceCompleted(first, outer, claim, receipts);
        await Assert.That(first.ConsecutiveFailures.Membership).IsEqualTo(1);
        await Assert.That(first.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected);
        var issueBody = BundesligaContextSourceHealth.CreateIssueBody(first.DesiredIssueProjection!.Marker, first.Competition, first.Source, first.Watermark, first.ActiveConditions);
        await Assert.That(issueBody).Contains("- `ROSTER_MEMBERSHIP_REJECTED`\n");
        await Assert.That(BundesligaContextSourceHealth.HashIssueBody(issueBody)).IsEqualTo(first.DesiredIssueProjection.BodySha256);
        await Assert.That(first.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days);
        await Assert.That(replay).IsSameReferenceAs(first);
    }

    [Test]
    [Arguments("SourceDateRejected")]
    [Arguments("SeasonRejected")]
    [Arguments("IdentityRejected")]
    public async Task Completed_semantic_roster_rejection_sets_the_counter_condition_and_issue_body(string evaluation)
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 3);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var bundle = new string('b', 64);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.HandoffReady, bundle, $"bundesliga-context-source-bundle-{identity.StorageId}");
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), now, BundesligaContextSourceDisposition.Rejected, RosterDescriptor(evaluation), null, [evaluation]);
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var receipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 9, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed, identity, lane, observation.ObservationDigest, bundle), now)).ToArray();

        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, receipts);
        var body = BundesligaContextSourceHealth.CreateIssueBody(health.DesiredIssueProjection!.Marker, health.Competition, health.Source, health.Watermark, health.ActiveConditions);

        await Assert.That(health.ConsecutiveFailures.Membership).IsEqualTo(1);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected);
        await Assert.That(body).Contains("- `ROSTER_MEMBERSHIP_REJECTED`\n");
        await Assert.That(BundesligaContextSourceHealth.HashIssueBody(body)).IsEqualTo(health.DesiredIssueProjection.BodySha256);
    }

    [Test]
    public async Task Health_validation_rejects_semantically_corrupt_revision_selection_condition_and_issue_state()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2); var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.HandoffReady, new string('b', 64), $"bundesliga-context-source-bundle-{identity.StorageId}");
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), now, BundesligaContextSourceDisposition.Rejected, RosterDescriptor("SourceDateRejected"), null, ["UNKNOWN_SOURCE_DATE"]);
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var receipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed, identity, lane, observation.ObservationDigest, new string('b', 64)), now)).ToArray();
        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, receipts); var accepted = health.RosterRevisionState!.Accepted!;

        await Assert.That(() => (health with { RosterRevisionState = new BundesligaContextSourceRosterRevisionState(accepted with { Revision = new string('A', 40) }, null) }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { RosterRevisionState = new BundesligaContextSourceRosterRevisionState(accepted with { RemoteIdentity = new BundesligaContextSourceRemoteIdentity(null, null) }, null) }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { RosterRevisionState = new BundesligaContextSourceRosterRevisionState(accepted, new BundesligaContextSourcePendingRevision(new string('f', 40), new BundesligaContextSourceRemoteIdentity("y", 1), new string('e', 64), identity.CycleId, "SchemaRejected")) }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = [health.CommunitySelections[0], health.CommunitySelections[0]] }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = [health.CommunitySelections[0] with { SelectedSnapshotId = "bad" }] }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = [health.CommunitySelections[0] with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.NetworkCandidate }] }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = health.CommunitySelections.Select((selection, index) => index == 0 ? selection with { MembershipCapturedAt = new DateOnly(2026, 8, 1) } : selection).ToArray() }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = health.CommunitySelections.Select((selection, index) => index == 0 ? selection with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.DuckDb } : selection).ToArray() }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = health.CommunitySelections.Select((selection, index) => index == 0 ? selection with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed } : selection).ToArray() }).Validate()).Throws<InvalidDataException>();
        (health with { CommunitySelections = health.CommunitySelections.Select(selection => selection with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood }).ToArray() }).Validate();
        (health with { CommunitySelections = health.CommunitySelections.Select(selection => selection with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood, MembershipCapturedAt = new DateOnly(2026, 7, 1) }).ToArray() }).Validate();
        await Assert.That(() => (health with { ActiveConditions = health.ActiveConditions.Reverse().ToArray() }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { DesiredIssueProjection = health.DesiredIssueProjection! with { Marker = "<!-- wrong -->" } }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { DesiredIssueProjection = health.DesiredIssueProjection! with { Title = "wrong" } }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { CommunitySelections = health.CommunitySelections.Skip(1).ToArray() }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { DesiredIssueProjection = health.DesiredIssueProjection! with { SynchronizationStatus = (BundesligaContextSourceIssueSynchronization)999 } }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Receipt_observation_matrix_is_frozen_for_every_selection_origin_and_publication_combination()
    {
        var observations = new[]
        {
            Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.Rejected),
            Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.ArtifactCaptured),
            Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.Rejected),
            Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.MetadataUnchanged),
            Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.ArtifactCaptured)
        };
        foreach (var observation in observations)
        foreach (var selection in Enum.GetValues<BundesligaContextSourceSelectionDisposition>())
        foreach (var origin in Enum.GetValues<BundesligaContextSourceSelectedOrigin>())
        foreach (var publication in Enum.GetValues<BundesligaContextSourcePublicationDisposition>())
        {
            var selectedArtifactRoster = observation.Source == BundesligaContextSource.Rosters
                && observation.Disposition == BundesligaContextSourceDisposition.ArtifactCaptured
                && selection is BundesligaContextSourceSelectionDisposition.DuckDbAccepted or BundesligaContextSourceSelectionDisposition.MixedPerClubSelection;
            var rejectedArtifactRoster = observation.Source == BundesligaContextSource.Rosters
                && observation.Disposition == BundesligaContextSourceDisposition.ArtifactCaptured
                && selection == BundesligaContextSourceSelectionDisposition.CandidateRejected;
            var retainedArtifactRoster = observation.Source == BundesligaContextSource.Rosters
                && observation.Disposition == BundesligaContextSourceDisposition.MetadataUnchanged
                && origin is BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed;
            IReadOnlyList<BundesligaContextSourceHealthCondition> conditions = observation.Source switch
            {
                BundesligaContextSource.ClubElo when observation.Disposition == BundesligaContextSourceDisposition.Rejected => [BundesligaContextSourceHealthCondition.AcquisitionFailed],
                BundesligaContextSource.Rosters when rejectedArtifactRoster => BundesligaContextSourceHealth.OrderConditions([BundesligaContextSourceHealthCondition.RosterMembershipRejected, BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]),
                BundesligaContextSource.Rosters when !selectedArtifactRoster && !retainedArtifactRoster => [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown],
                _ => []
            };
            var dates = observation.Source == BundesligaContextSource.ClubElo
                ? new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null)
                : selectedArtifactRoster || retainedArtifactRoster
                    ? new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1))
                    : new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null);
            var request = Receipt(observation.Source, dates, observation.Source == BundesligaContextSource.Rosters ? new string('a', 40) : null, conditions, selection, origin, observation: observation.ObservationDigest, publication: publication);
            var expected = IsAllowed(observation.Disposition, observation.Source, selection, origin, publication);
            var actual = true;
            try { BundesligaContextSourceReceiptContract.ValidateAgainstObservation(request, observation); }
            catch (InvalidDataException) { actual = false; }
            if (actual != expected) throw new InvalidOperationException($"Unexpected receipt matrix result: {observation.Source}/{observation.Disposition}/{selection}/{origin}/{publication}.");
        }
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Eligible_Elo_receipt_is_bound_to_provider_rated_at_and_rejected_observations_cannot_claim_candidate_acceptance()
    {
        var eligible = Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.ArtifactCaptured);
        var accepted = Receipt(BundesligaContextSource.ClubElo, new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null), null, [], BundesligaContextSourceSelectionDisposition.NetworkAccepted, BundesligaContextSourceSelectedOrigin.NetworkCandidate, observation: eligible.ObservationDigest, publication: BundesligaContextSourcePublicationDisposition.Published);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(accepted, eligible);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(accepted with { SourceDates = accepted.SourceDates with { RatedAt = new DateOnly(2026, 9, 3) } }, eligible)).Throws<InvalidDataException>();

        var rejectedRoster = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.Rejected);
        var retained = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown], BundesligaContextSourceSelectionDisposition.CandidateRejected, BundesligaContextSourceSelectedOrigin.FallbackSeed, observation: rejectedRoster.ObservationDigest, publication: BundesligaContextSourcePublicationDisposition.NotAttempted);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(retained with { SelectionDisposition = BundesligaContextSourceSelectionDisposition.DuckDbAccepted, SelectedOrigin = BundesligaContextSourceSelectedOrigin.DuckDb, PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published }, rejectedRoster)).Throws<InvalidDataException>();
        foreach (var forbidden in new[] { BundesligaContextSourceHealthCondition.HandoffIncomplete, BundesligaContextSourceHealthCondition.CycleAborted, BundesligaContextSourceHealthCondition.AcquisitionFailed })
            await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(retained with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(retained.ActiveConditions.Append(forbidden)) }, rejectedRoster)).Throws<InvalidDataException>();
        await Assert.That(() => (retained with { PublicationDisposition = (BundesligaContextSourcePublicationDisposition)999 }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Rejected_Elo_without_a_retained_head_can_publish_the_launch_seed_fallback()
    {
        var observation = Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.Rejected);
        var receipt = Receipt(
            BundesligaContextSource.ClubElo,
            new BundesligaContextSourceDates(new DateOnly(2026, 8, 20), null, null, null),
            null,
            [BundesligaContextSourceHealthCondition.AcquisitionFailed],
            BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected,
            BundesligaContextSourceSelectedOrigin.LaunchSeed,
            observation: observation.ObservationDigest,
            publication: BundesligaContextSourcePublicationDisposition.Published);

        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt, observation);

        await Assert.That(receipt.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
    }

    [Test]
    public async Task Rejected_roster_without_a_retained_head_can_publish_the_fallback_seed()
    {
        var observation = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.Rejected);
        var receipt = Receipt(
            BundesligaContextSource.Rosters,
            new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null),
            new string('a', 40),
            [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown],
            BundesligaContextSourceSelectionDisposition.CandidateRejected,
            BundesligaContextSourceSelectedOrigin.FallbackSeed,
            observation: observation.ObservationDigest,
            publication: BundesligaContextSourcePublicationDisposition.Published);

        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt, observation);

        await Assert.That(receipt.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
    }

    [Test]
    public async Task Retained_Elo_dates_and_selected_roster_dates_describe_the_selected_head()
    {
        var eligibleElo = Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.ArtifactCaptured);
        var retainedElo = Receipt(BundesligaContextSource.ClubElo, new BundesligaContextSourceDates(new DateOnly(2026, 8, 20), null, null, null), null, [], BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, BundesligaContextSourceSelectedOrigin.LastKnownGood, observation: eligibleElo.ObservationDigest);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(retainedElo, eligibleElo);

        var productionIdentity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 20);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        eligibleElo = eligibleElo with { AttemptId = BundesligaContextSourceHashing.AttemptId(productionIdentity, BundesligaContextSource.ClubElo), ObservedAtUtc = now };
        var outer = new BundesligaContextSourceOuterCycle(productionIdentity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.HandoffReady, new string('b', 64), $"bundesliga-context-source-bundle-{productionIdentity.StorageId}");
        var claim = new BundesligaContextSourceCycleClaim(productionIdentity, BundesligaContextSource.ClubElo, eligibleElo.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, eligibleElo.ObservationDigest, eligibleElo, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var retainedReceipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(Receipt(BundesligaContextSource.ClubElo, retainedElo.SourceDates, null, [], BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, BundesligaContextSourceSelectedOrigin.LastKnownGood, productionIdentity, lane, eligibleElo.ObservationDigest, new string('b', 64)), now)).ToArray();
        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, retainedReceipts);
        await Assert.That(health.LastSuccessfulSourceDates.RatedAt).IsEqualTo(new DateOnly(2026, 8, 20));
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);

        var eligibleRoster = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.ArtifactCaptured);
        var selectedDates = new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
        var acceptedRoster = Receipt(BundesligaContextSource.Rosters, selectedDates, new string('a', 40), [], BundesligaContextSourceSelectionDisposition.DuckDbAccepted, BundesligaContextSourceSelectedOrigin.DuckDb, observation: eligibleRoster.ObservationDigest, publication: BundesligaContextSourcePublicationDisposition.Published);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(acceptedRoster, eligibleRoster);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(acceptedRoster with { PublicationDisposition = BundesligaContextSourcePublicationDisposition.Reactivated }, eligibleRoster);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(acceptedRoster with { SourceDates = selectedDates with { MembershipCapturedAt = new DateOnly(2026, 9, 2) } }, eligibleRoster)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(acceptedRoster with { SourceDates = selectedDates with { MembershipEffectiveAt = new DateOnly(2026, 9, 2) } }, eligibleRoster)).Throws<InvalidDataException>();

        var mixedRoster = acceptedRoster with
        {
            SelectionDisposition = BundesligaContextSourceSelectionDisposition.MixedPerClubSelection,
            SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed,
            PublicationDisposition = BundesligaContextSourcePublicationDisposition.Reactivated,
            SourceDates = selectedDates with { MembershipEffectiveAt = new DateOnly(2026, 8, 30) },
            ActiveConditions = [BundesligaContextSourceHealthCondition.RosterMembershipRejected]
        };
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(mixedRoster, eligibleRoster);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(mixedRoster with { SourceDates = mixedRoster.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 9, 2) } }, eligibleRoster)).Throws<InvalidDataException>();

        var carriedArtifact = acceptedRoster with
        {
            SourceDates = selectedDates with { EnrichmentCapturedAt = new DateOnly(2026, 8, 1) },
            CarriedFields = new BundesligaContextSourceCarriedFields(1, 0, 0, new DateOnly(2026, 8, 1))
        };
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(carriedArtifact, eligibleRoster);

        var carriedLegacy = acceptedRoster with
        {
            SourceDates = selectedDates with { EnrichmentCapturedAt = null },
            CarriedFields = new BundesligaContextSourceCarriedFields(1, 0, 0, null),
            ActiveConditions = [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]
        };
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(carriedLegacy, eligibleRoster);
    }

    [Test]
    public async Task Fresh_Elo_rejects_false_rejection_and_recomputes_caller_staleness()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 21);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var observation = Observation(BundesligaContextSource.ClubElo, BundesligaContextSourceDisposition.ArtifactCaptured) with
        {
            AttemptId = BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo),
            ObservedAtUtc = now
        };
        var bundle = new string('b', 64);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.HandoffReady, bundle, $"bundesliga-context-source-bundle-{identity.StorageId}");
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.ClubElo, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var hostileReceipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(
            Receipt(BundesligaContextSource.ClubElo, new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null), null, [BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days], BundesligaContextSourceSelectionDisposition.NetworkAccepted, BundesligaContextSourceSelectedOrigin.NetworkCandidate, identity, lane, observation.ObservationDigest, bundle, BundesligaContextSourcePublicationDisposition.Published), now)).ToArray();

        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, hostileReceipts);

        await Assert.That(health.ActiveConditions).IsEmpty();
        await Assert.That(health.CommunitySelections.All(selection => selection.Conditions.Count == 0)).IsTrue();
        await Assert.That(health.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Closed);
        var falseRejection = hostileReceipts[0].Request with { ActiveConditions = [BundesligaContextSourceHealthCondition.ClubEloSourceRejected] };
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(falseRejection, observation)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Known_current_roster_rejects_false_membership_rejection_and_recomputes_caller_staleness()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 22);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var observation = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.ArtifactCaptured) with
        {
            AttemptId = BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters),
            ObservedAtUtc = now
        };
        var dates = new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
        var bundle = new string('b', 64);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.HandoffReady, bundle, $"bundesliga-context-source-bundle-{identity.StorageId}");
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var injectedStaleness = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days,
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days,
            BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days,
            BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days]);
        var hostileReceipts = BundesligaContextSourceContract.ProductionConsumers.Select(lane => new BundesligaContextSourceReceipt(
            Receipt(BundesligaContextSource.Rosters, dates, new string('a', 40), injectedStaleness, BundesligaContextSourceSelectionDisposition.DuckDbAccepted, BundesligaContextSourceSelectedOrigin.DuckDb, identity, lane, observation.ObservationDigest, bundle, BundesligaContextSourcePublicationDisposition.Published), now)).ToArray();

        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, hostileReceipts);

        await Assert.That(health.ActiveConditions).IsEmpty();
        await Assert.That(health.CommunitySelections.All(selection => selection.Conditions.Count == 0)).IsTrue();
        await Assert.That(health.ConsecutiveFailures).IsEqualTo(new BundesligaContextSourceFailures(0, 0, 0, 0));
        await Assert.That(health.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Closed);
        var falseRejection = hostileReceipts[0].Request with { ActiveConditions = [BundesligaContextSourceHealthCondition.RosterMembershipRejected] };
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(falseRejection, observation)).Throws<InvalidDataException>();
        var falseUnknown = hostileReceipts[0].Request with { ActiveConditions = [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown] };
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(falseUnknown, observation)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Eligible_roster_receipt_may_report_enrichment_rejection_independently_of_observation_diagnostics()
    {
        var observation = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.ArtifactCaptured);
        var dates = new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
        var receipt = Receipt(
            BundesligaContextSource.Rosters,
            dates,
            new string('a', 40),
            [BundesligaContextSourceHealthCondition.RosterEnrichmentRejected],
            BundesligaContextSourceSelectionDisposition.DuckDbAccepted,
            BundesligaContextSourceSelectedOrigin.DuckDb,
            observation: observation.ObservationDigest,
            publication: BundesligaContextSourcePublicationDisposition.Published);

        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt, observation);
        await Assert.That(receipt.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
    }

    [Test]
    public async Task Eligible_roster_observation_may_leave_every_community_on_fallback_or_last_known_good()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 23);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var observation = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.ArtifactCaptured) with
        {
            AttemptId = BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters),
            ObservedAtUtc = now
        };
        var bundle = new string('b', 64);
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.HandoffReady, bundle, $"bundesliga-context-source-bundle-{identity.StorageId}");
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers, now);
        var conditions = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterMembershipRejected,
            BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]);
        var receipts = BundesligaContextSourceContract.ProductionConsumers.Select((lane, index) =>
        {
            var fallback = index < 3;
            var origin = fallback ? BundesligaContextSourceSelectedOrigin.FallbackSeed : BundesligaContextSourceSelectedOrigin.LastKnownGood;
            var publication = index switch
            {
                0 => BundesligaContextSourcePublicationDisposition.Published,
                1 => BundesligaContextSourcePublicationDisposition.Reactivated,
                _ => BundesligaContextSourcePublicationDisposition.NotAttempted
            };
            DateOnly? membershipCapturedAt = !fallback && index % 2 == 0 ? new DateOnly(2026, 8, 1) : null;
            var request = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, membershipCapturedAt, new DateOnly(2026, 8, 1), null), new string('a', 40), conditions, BundesligaContextSourceSelectionDisposition.CandidateRejected, origin, identity, lane, observation.ObservationDigest, bundle, publication);
            BundesligaContextSourceReceiptContract.ValidateAgainstObservation(request, observation);
            return new BundesligaContextSourceReceipt(request, now);
        }).ToArray();

        var health = BundesligaContextSourceHealthReducer.ReduceCompleted(null, outer, claim, receipts);

        await Assert.That(health.RosterRevisionState!.Accepted!.Revision).IsEqualTo(new string('a', 40));
        await Assert.That(health.ConsecutiveFailures.Membership).IsEqualTo(1);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterMembershipRejected);
        await Assert.That(health.CommunitySelections.All(selection => selection.SelectedOrigin is BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood)).IsTrue();
        await Assert.That(health.CommunitySelections.Where(selection => selection.SelectedOrigin == BundesligaContextSourceSelectedOrigin.LastKnownGood).Any(selection => selection.MembershipCapturedAt is null)).IsTrue();
        await Assert.That(health.CommunitySelections.Where(selection => selection.SelectedOrigin == BundesligaContextSourceSelectedOrigin.LastKnownGood).Any(selection => selection.MembershipCapturedAt is not null)).IsTrue();
    }

    [Test]
    public async Task Metadata_unchanged_receipt_repeats_retained_membership_and_enrichment_outcomes()
    {
        var metadata = Observation(BundesligaContextSource.Rosters, BundesligaContextSourceDisposition.MetadataUnchanged);
        metadata = metadata with
        {
            DescriptorJson = metadata.DescriptorJson
                .Replace("\"retainedEvaluation\":\"Eligible\"", "\"retainedEvaluation\":\"SourceDateRejected\"", StringComparison.Ordinal)
                .Replace("\"retainedDiagnostics\":[]", "\"retainedDiagnostics\":[\"ROSTER_ENRICHMENT_REJECTED\",\"UNKNOWN_SOURCE_DATE\"]", StringComparison.Ordinal)
        };
        metadata.Validate();
        var retainedConditions = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterMembershipRejected,
            BundesligaContextSourceHealthCondition.RosterEnrichmentRejected,
            BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]);
        var receipt = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), retainedConditions, BundesligaContextSourceSelectionDisposition.MetadataUnchanged, BundesligaContextSourceSelectedOrigin.LastKnownGood, observation: metadata.ObservationDigest, publication: BundesligaContextSourcePublicationDisposition.Unchanged);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt, metadata);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt with { ActiveConditions = [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown] }, metadata)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(retainedConditions.Append(BundesligaContextSourceHealthCondition.AcquisitionFailed)) }, metadata)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Metadata_unchanged_receipt_preserves_the_exact_prior_lane_selection()
    {
        var currentIdentity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1468-7000-8000-000000000002");
        var priorIdentity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000001");
        var conditions = new[] { BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown };
        var current = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('a', 40), conditions, BundesligaContextSourceSelectionDisposition.MetadataUnchanged, BundesligaContextSourceSelectedOrigin.LastKnownGood, currentIdentity);
        var priorRequest = current with
        {
            Identity = priorIdentity,
            ObservationDigest = new string('d', 64),
            BundleDigest = new string('e', 64),
            SelectionDisposition = BundesligaContextSourceSelectionDisposition.CandidateRejected,
            PublicationDisposition = BundesligaContextSourcePublicationDisposition.NotAttempted
        };
        var prior = new BundesligaContextSourceReceipt(priorRequest, new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));

        BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current, prior);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current with { SelectedSnapshotId = new string('f', 64) }, prior)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.FallbackSeed }, prior)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current with { SourceDates = current.SourceDates with { MembershipEffectiveAt = new DateOnly(2026, 8, 2) } }, prior)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current with { CarriedFields = current.CarriedFields with { AgeCount = 1 } }, prior)).Throws<InvalidDataException>();

        var mixedPrior = prior with
        {
            Request = prior.Request with
            {
                SelectionDisposition = BundesligaContextSourceSelectionDisposition.MixedPerClubSelection,
                SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed,
                SourceDates = prior.Request.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 8, 1) },
                ActiveConditions = BundesligaContextSourceHealth.OrderConditions(prior.Request.ActiveConditions.Append(BundesligaContextSourceHealthCondition.RosterMembershipRejected))
            }
        };
        var mixedCurrent = current with
        {
            SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed,
            SourceDates = current.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 8, 1) },
            ActiveConditions = BundesligaContextSourceHealth.OrderConditions(current.ActiveConditions.Append(BundesligaContextSourceHealthCondition.RosterMembershipRejected))
        };
        BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(mixedCurrent, mixedPrior);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(mixedCurrent with { ActiveConditions = conditions }, mixedPrior)).Throws<InvalidDataException>();

        var enrichmentPrior = prior with { Request = prior.Request with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(prior.Request.ActiveConditions.Append(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected)) } };
        var enrichmentCurrent = current with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(current.ActiveConditions.Append(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected)) };
        BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(enrichmentCurrent, enrichmentPrior);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(current, enrichmentPrior)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Receipt_freshness_conditions_are_exact_for_the_cycle_reference()
    {
        var reference = new DateOnly(2026, 9, 6);
        var fresh = Receipt(BundesligaContextSource.Rosters, new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)), new string('a', 40), [BundesligaContextSourceHealthCondition.RosterEnrichmentRejected], BundesligaContextSourceSelectionDisposition.DuckDbAccepted, BundesligaContextSourceSelectedOrigin.DuckDb, publication: BundesligaContextSourcePublicationDisposition.Published);
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(fresh, reference);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(fresh with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(fresh.ActiveConditions.Append(BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days)) }, reference)).Throws<InvalidDataException>();

        var stale = fresh with
        {
            SourceDates = fresh.SourceDates with { MembershipEffectiveAt = new DateOnly(2026, 8, 1), EnrichmentCapturedAt = null },
            ActiveConditions = BundesligaContextSourceHealth.OrderConditions([
                BundesligaContextSourceHealthCondition.RosterEnrichmentRejected,
                BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days])
        };
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(stale, reference);
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(stale with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(stale.ActiveConditions.Where(condition => condition != BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days)) }, reference)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Elo_health_rejects_roster_counters_and_conditions()
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000");
        var health = new BundesligaContextSourceHealth(identity.Competition, identity.Scope, BundesligaContextSource.ClubElo,
            new BundesligaContextSourceWatermark(identity.Sequence, identity.CycleId), null,
            new BundesligaContextSourceFailures(0, 0, 0, 0), new BundesligaContextSourceSuccessfulDates(null, null, null), null, [],
            [BundesligaContextSourceHealthCondition.HandoffIncomplete], null);
        health.Validate();
        await Assert.That(() => (health with { ConsecutiveFailures = health.ConsecutiveFailures with { Membership = 1 } }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (health with { ActiveConditions = [BundesligaContextSourceHealthCondition.RosterMembershipRejected] }).Validate()).Throws<InvalidDataException>();
    }

    private static BundesligaContextSourceReceiptRequest Receipt(BundesligaContextSource source, BundesligaContextSourceDates dates, string? revision, IReadOnlyList<BundesligaContextSourceHealthCondition> conditions, BundesligaContextSourceSelectionDisposition selection, BundesligaContextSourceSelectedOrigin origin, BundesligaContextSourceCycleIdentity? identity = null, string? lane = null, string? observation = null, string? bundle = null, BundesligaContextSourcePublicationDisposition publication = BundesligaContextSourcePublicationDisposition.NotAttempted)
    {
        var resolvedIdentity = identity ?? BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000");
        var resolvedLane = lane ?? BundesligaContextSourceContract.DevelopmentLane;
        var community = resolvedIdentity.Scope == BundesligaContextSourceScope.Development ? BundesligaContextSourceContract.DevelopmentCommunity : resolvedLane switch { "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse", "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena" };
        return new BundesligaContextSourceReceiptRequest(resolvedIdentity, source, resolvedLane, community, observation ?? new string('a', 64), bundle ?? new string('b', 64), selection, new string('c', 64), origin, publication, dates, revision, new BundesligaContextSourceCarriedFields(0, 0, 0, null), conditions);
    }

    private static BundesligaContextSourceHealth CreateEloHealth(BundesligaContextSourceCycleIdentity identity, string? lastCompletedCycleId, bool complete)
    {
        IReadOnlyList<string> lanes = identity.Scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers.Order(StringComparer.Ordinal).ToArray() : BundesligaContextSourceContract.DevelopmentConsumers;
        var selections = complete ? lanes.Select(lane => new BundesligaContextSourceCommunitySelection(lane, CommunityFor(lane, identity.Scope), new string('a', 64), BundesligaContextSourceSelectedOrigin.LaunchSeed, new DateOnly(2026, 9, 1), null, null, null, [])).ToArray() : [];
        var watermark = new BundesligaContextSourceWatermark(identity.Sequence, identity.CycleId);
        var issue = identity.Scope == BundesligaContextSourceScope.ProductionLive
            ? new BundesligaContextSourceIssueProjection("<!-- kicktippai:context-source-health:bundesliga-2026-27:club-elo -->", "[KicktippAi] Bundesliga 2026/27 club-elo context-source health", BundesligaContextSourceHealth.HashIssueBody(BundesligaContextSourceHealth.CreateIssueBody("<!-- kicktippai:context-source-health:bundesliga-2026-27:club-elo -->", identity.Competition, BundesligaContextSource.ClubElo, watermark, [])), BundesligaContextSourceIssueState.Closed, null, BundesligaContextSourceIssueSynchronization.Pending, null, null)
            : null;
        return new BundesligaContextSourceHealth(identity.Competition, identity.Scope, BundesligaContextSource.ClubElo, watermark, lastCompletedCycleId, new BundesligaContextSourceFailures(0, 0, 0, 0), new BundesligaContextSourceSuccessfulDates(new DateOnly(2026, 9, 1), null, null), null, selections, [], issue);
    }

    private static BundesligaContextSourceHealth CreateRosterHealth(BundesligaContextSourceCycleIdentity identity, string firstSeenCycleId)
    {
        var watermark = new BundesligaContextSourceWatermark(identity.Sequence, identity.CycleId);
        var pending = new BundesligaContextSourcePendingRevision(new string('a', 40), new BundesligaContextSourceRemoteIdentity("x", 1), new string('b', 64), firstSeenCycleId, "RevisionRejected");
        var issue = identity.Scope == BundesligaContextSourceScope.ProductionLive
            ? new BundesligaContextSourceIssueProjection("<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->", "[KicktippAi] Bundesliga 2026/27 rosters context-source health", BundesligaContextSourceHealth.HashIssueBody(BundesligaContextSourceHealth.CreateIssueBody("<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->", identity.Competition, BundesligaContextSource.Rosters, watermark, [])), BundesligaContextSourceIssueState.Closed, null, BundesligaContextSourceIssueSynchronization.Pending, null, null)
            : null;
        return new BundesligaContextSourceHealth(identity.Competition, identity.Scope, BundesligaContextSource.Rosters, watermark, null, new BundesligaContextSourceFailures(0, 0, 0, 0), new BundesligaContextSourceSuccessfulDates(null, null, null), new BundesligaContextSourceRosterRevisionState(null, pending), [], [], issue);
    }

    private static string CommunityFor(string lane, BundesligaContextSourceScope scope) => scope == BundesligaContextSourceScope.Development ? BundesligaContextSourceContract.DevelopmentCommunity : lane switch { "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse", "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena" };

    private static bool IsAllowed(BundesligaContextSourceDisposition disposition, BundesligaContextSource source, BundesligaContextSourceSelectionDisposition selection, BundesligaContextSourceSelectedOrigin origin, BundesligaContextSourcePublicationDisposition publication)
    {
        if (source == BundesligaContextSource.ClubElo)
            return disposition switch
            {
                BundesligaContextSourceDisposition.ArtifactCaptured when selection == BundesligaContextSourceSelectionDisposition.NetworkAccepted => origin == BundesligaContextSourceSelectedOrigin.NetworkCandidate && publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.Unchanged,
                BundesligaContextSourceDisposition.ArtifactCaptured when selection is BundesligaContextSourceSelectionDisposition.NetworkCandidateStale or BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer => origin is BundesligaContextSourceSelectedOrigin.LaunchSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood && publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.NotAttempted or BundesligaContextSourcePublicationDisposition.Reactivated,
                BundesligaContextSourceDisposition.Rejected when selection == BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected => origin is BundesligaContextSourceSelectedOrigin.LaunchSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood && publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.NotAttempted or BundesligaContextSourcePublicationDisposition.Reactivated,
                _ => false
            };
        return disposition switch
        {
            BundesligaContextSourceDisposition.ArtifactCaptured when selection == BundesligaContextSourceSelectionDisposition.DuckDbAccepted => origin == BundesligaContextSourceSelectedOrigin.DuckDb && publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.Unchanged or BundesligaContextSourcePublicationDisposition.Reactivated,
            BundesligaContextSourceDisposition.ArtifactCaptured when selection == BundesligaContextSourceSelectionDisposition.MixedPerClubSelection => origin == BundesligaContextSourceSelectedOrigin.Mixed && publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.Unchanged or BundesligaContextSourcePublicationDisposition.Reactivated,
            BundesligaContextSourceDisposition.ArtifactCaptured when selection == BundesligaContextSourceSelectionDisposition.CandidateRejected => IsAllowedRosterRejection(origin, publication),
            BundesligaContextSourceDisposition.MetadataUnchanged when selection == BundesligaContextSourceSelectionDisposition.MetadataUnchanged => origin is BundesligaContextSourceSelectedOrigin.DuckDb or BundesligaContextSourceSelectedOrigin.Mixed or BundesligaContextSourceSelectedOrigin.FallbackSeed or BundesligaContextSourceSelectedOrigin.LastKnownGood && publication == BundesligaContextSourcePublicationDisposition.Unchanged,
            BundesligaContextSourceDisposition.Rejected when selection == BundesligaContextSourceSelectionDisposition.CandidateRejected => IsAllowedRosterRejection(origin, publication),
            _ => false
        };
    }

    private static bool IsAllowedRosterRejection(
        BundesligaContextSourceSelectedOrigin origin,
        BundesligaContextSourcePublicationDisposition publication)
        => origin switch
        {
            BundesligaContextSourceSelectedOrigin.FallbackSeed => publication is BundesligaContextSourcePublicationDisposition.Published or BundesligaContextSourcePublicationDisposition.NotAttempted or BundesligaContextSourcePublicationDisposition.Reactivated,
            BundesligaContextSourceSelectedOrigin.LastKnownGood => publication == BundesligaContextSourcePublicationDisposition.NotAttempted,
            _ => false
        };

    private static BundesligaContextSourceObservation Observation(BundesligaContextSource source, BundesligaContextSourceDisposition disposition)
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000");
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        if (source == BundesligaContextSource.ClubElo)
        {
            var descriptor = disposition == BundesligaContextSourceDisposition.Rejected ? BundesligaContextSourceBundleContractTests.RejectedEloDescriptor() : EligibleEloDescriptor();
            var payload = disposition == BundesligaContextSourceDisposition.ArtifactCaptured ? new BundesligaContextSourcePayload("club-elo/source.csv", 1, new string('a', 64)) : null;
            return new BundesligaContextSourceObservation(source, BundesligaContextSourceHashing.AttemptId(identity, source), now, disposition, descriptor, payload, disposition == BundesligaContextSourceDisposition.Rejected ? ["UNKNOWN_SOURCE_DATE"] : []);
        }
        var evaluation = disposition switch { BundesligaContextSourceDisposition.ArtifactCaptured => "Eligible", BundesligaContextSourceDisposition.MetadataUnchanged => "MetadataUnchanged", _ => "SourceDateRejected" };
        var rosterDescriptor = RosterDescriptor(evaluation);
        var rosterPayload = disposition == BundesligaContextSourceDisposition.ArtifactCaptured ? new BundesligaContextSourcePayload("rosters/source.duckdb", 1, new string('c', 64)) : null;
        return new BundesligaContextSourceObservation(source, BundesligaContextSourceHashing.AttemptId(identity, source), now, disposition, rosterDescriptor, rosterPayload, disposition == BundesligaContextSourceDisposition.Rejected ? ["UNKNOWN_SOURCE_DATE"] : []);
    }

    private static string EligibleEloDescriptor()
    {
        var rows = BundesligaTeamManifest.Default.Entries.Select((entry, index) => new { teamSlug = entry.TeamSlug, providerName = $"Team {index + 1:00}", globalRank = index + 1, elo = 1500 }).ToArray();
        return JsonSerializer.Serialize(new { contract = "club-elo-direct-csv-descriptor/v1", sourceUrl = "https://example.test/elo.csv", rawSha256 = new string('a', 64), rawByteLength = 1, csvHeader = "Rank,Club,Country,Level,Elo,From,To", providerRatedAt = "2026-09-04", providerDateEvidence = new { kind = "ProviderCsvField", recipeId = "recipe/v1", field = "From", rawValue = "2026-09-04", ratedAt = "2026-09-04" }, nameMappingContract = "map/v1", nameMappingSha256 = new string('b', 64), sourceRows = rows, evaluation = "Eligible" });
    }

    private static string RosterDescriptor(string evaluation)
    {
        if (evaluation == "MetadataUnchanged") return $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"https://example.test/meta\",\"artifactUrl\":\"https://example.test/db\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"AcceptedRevisionUnchanged\",\"remoteIdentityAfter\":null,\"embeddedRevision\":null,\"rawSha256\":null,\"expectedRawSha256\":null,\"rawByteLength\":null,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":\"{new string('e', 64)}\",\"retainedEvaluation\":\"Eligible\",\"retainedDiagnostics\":[],\"evaluation\":\"MetadataUnchanged\"}}";
        var dates = evaluation is "Eligible" or "SeasonRejected" or "IdentityRejected" ? "\"2026-09-01\"" : "null";
        return $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"https://example.test/meta\",\"artifactUrl\":\"https://example.test/db\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":{dates},\"membershipEffectiveDate\":{dates},\"enrichmentCaptureDate\":{dates},\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"{evaluation}\"}}";
    }
}
