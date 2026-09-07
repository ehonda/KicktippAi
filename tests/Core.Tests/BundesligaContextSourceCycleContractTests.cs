using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaContextSourceCycleContractTests
{
    [Test]
    public async Task Storage_and_attempt_hashes_match_the_LP32_known_answer()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 456);
        await Assert.That(BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.Rosters))
            .IsEqualTo("66799c0fdf6cfc33c56dce3e73f1cda4013b2771e6be1fb31e41e502e7d76227");
        await Assert.That(cycle.StorageId).Matches("^[0-9a-f]{64}$");
        await Assert.That(BundesligaContextSourceHashing.SourceCycleStorageId(cycle, BundesligaContextSource.Rosters)).IsNotEqualTo(cycle.StorageId);
        await Assert.That(BundesligaContextSourceHashing.ReceiptStorageId(cycle, BundesligaContextSource.Rosters, "pes-squad-context")).Matches("^[0-9a-f]{64}$");
    }

    [Test]
    public async Task Production_identity_rejects_leading_zero_overflow_and_sequence_substitution()
    {
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:01:2", 2)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:9223372036854775808:2", 2)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:1:9223372036854775808", 1)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:1:2", 3)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition, (BundesligaContextSourceScope)999, "local:0198f865-1467-7000-8000-000000000000", 1)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Development_identity_derives_sequence_from_lowercase_uuid_v7()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000");
        await Assert.That(cycle.Sequence).IsEqualTo(0x0198f8651467L);
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198F865-1467-7000-8000-000000000000")).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-4000-8000-000000000000")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Cycle_id_reconstruction_derives_production_and_local_sequences_without_a_duplicate_parser()
    {
        var production = BundesligaContextSourceCycleIdentity.FromCycleId(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:987:654");
        var local = BundesligaContextSourceCycleIdentity.FromCycleId(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.Development, "local:0198f865-1467-7000-8000-000000000000");

        await Assert.That(production).IsEqualTo(BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 987, 654));
        await Assert.That(local).IsEqualTo(BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000"));
        await Assert.That(() => BundesligaContextSourceCycleIdentity.FromCycleId(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.ProductionLive, "gha:01:654")).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceCycleIdentity.FromCycleId(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.Development, "local:0198F865-1467-7000-8000-000000000000")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Outer_cycle_json_has_exact_order_seconds_Z_and_explicit_nulls()
    {
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var cycle = new BundesligaContextSourceOuterCycle(BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2), now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
        var json = Encoding.UTF8.GetString(cycle.CreateCanonicalUtf8());
        await Assert.That(json).StartsWith("{\"contract\":\"bundesliga-context-source-cycle/v1\",\"competition\":");
        await Assert.That(json).Contains("\"startedAtUtc\":\"2026-09-06T12:00:00Z\"");
        await Assert.That(json).EndsWith("\"bundleSha256\":null,\"artifactName\":null,\"abortCode\":null,\"completedAtUtc\":null}");
        await Assert.That(() => (cycle with { StartedAtUtc = now.AddTicks(1) }).CreateCanonicalUtf8()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Claim_requires_uuid_v4_exact_ten_minute_lease_and_prefix_receipts()
    {
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var claim = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), BundesligaContextSourceSourceStatus.Claimed, "11111111-1111-4111-8111-111111111111", now, now.AddMinutes(10), null, null, null, null, [], null);
        claim.Validate(BundesligaContextSourceContract.ProductionConsumers);
        await Assert.That(() => (claim with { LeaseExpiresAtUtc = now.AddMinutes(9) }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        await Assert.That(() => (claim with { ClaimToken = "11111111-1111-7111-8111-111111111111" }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        await Assert.That(() => (claim with { ReceivedConsumers = ["pes-squad-context"] }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();

        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.Rejected, BundesligaContextSourceBundleContractTests.RejectedEloDescriptor(), null, ["UNKNOWN_SOURCE_DATE"]);
        var complete = new BundesligaContextSourceCycleClaim(identity, BundesligaContextSource.ClubElo, observation.AttemptId, BundesligaContextSourceSourceStatus.Complete, claim.ClaimToken, now, now.AddMinutes(10), now, observation.ObservationDigest, observation, null, BundesligaContextSourceContract.ProductionConsumers.Take(BundesligaContextSourceContract.ProductionConsumers.Count - 1).ToArray(), now);
        await Assert.That(() => complete.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();

        var finalizedWithAllReceipts = complete with { Status = BundesligaContextSourceSourceStatus.Finalized, ReceivedConsumers = BundesligaContextSourceContract.ProductionConsumers, CompletedAtUtc = null };
        await Assert.That(() => finalizedWithAllReceipts.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();

        var finalized = complete with { Status = BundesligaContextSourceSourceStatus.Finalized, ReceivedConsumers = [], CompletedAtUtc = null };
        var abortedWithoutFinalization = claim with { Status = BundesligaContextSourceSourceStatus.Aborted, AbortCode = BundesligaContextSourceError.AcquisitionInterrupted, ReceivedConsumers = [BundesligaContextSourceContract.ProductionConsumers[0]] };
        await Assert.That(() => abortedWithoutFinalization.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        var abortedWithFullPrefix = complete with { Status = BundesligaContextSourceSourceStatus.Aborted, AbortCode = BundesligaContextSourceError.AcquisitionInterrupted, ReceivedConsumers = BundesligaContextSourceContract.ProductionConsumers, CompletedAtUtc = null };
        await Assert.That(() => abortedWithFullPrefix.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        (finalized with { Status = BundesligaContextSourceSourceStatus.Aborted, AbortCode = BundesligaContextSourceError.AcquisitionInterrupted, ReceivedConsumers = [BundesligaContextSourceContract.ProductionConsumers[0]] }).Validate(BundesligaContextSourceContract.ProductionConsumers);
        var wrongSource = finalized with { Source = BundesligaContextSource.Rosters, AttemptId = BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters) };
        await Assert.That(() => wrongSource.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        var wrongAttemptObservation = observation with { AttemptId = BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters) };
        var wrongAttempt = finalized with { Observation = wrongAttemptObservation, ObservationDigest = wrongAttemptObservation.ObservationDigest };
        await Assert.That(() => wrongAttempt.Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();

        (finalized with { FinalizedAtUtc = now }).Validate(BundesligaContextSourceContract.ProductionConsumers);
        await Assert.That(() => (finalized with { FinalizedAtUtc = now.AddMinutes(-1) }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        await Assert.That(() => (finalized with { FinalizedAtUtc = now.AddMinutes(10) }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
        var completed = finalized with { Status = BundesligaContextSourceSourceStatus.Complete, ReceivedConsumers = BundesligaContextSourceContract.ProductionConsumers, CompletedAtUtc = now };
        completed.Validate(BundesligaContextSourceContract.ProductionConsumers);
        await Assert.That(() => (completed with { CompletedAtUtc = now.AddSeconds(-1) }).Validate(BundesligaContextSourceContract.ProductionConsumers)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Aborted_outer_cycle_preserves_pre_verification_post_verification_or_reserved_shape()
    {
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var abortedBeforeReservation = new BundesligaContextSourceOuterCycle(identity, now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Aborted, AbortCode: BundesligaContextSourceError.AcquisitionInterrupted);
        abortedBeforeReservation.Validate();
        await Assert.That(Encoding.UTF8.GetString(abortedBeforeReservation.CreateCanonicalUtf8())).Contains("\"bundleSha256\":null,\"artifactName\":null,\"abortCode\":\"ACQUISITION_INTERRUPTED\"");

        var digest = new string('a', 64); var artifact = $"bundesliga-context-source-bundle-{identity.StorageId}";
        var abortedAfterVerification = abortedBeforeReservation with { BundleSha256 = digest };
        abortedAfterVerification.Validate();
        await Assert.That(Encoding.UTF8.GetString(abortedAfterVerification.CreateCanonicalUtf8())).Contains($"\"bundleSha256\":\"{digest}\",\"artifactName\":null");
        (abortedBeforeReservation with { BundleSha256 = digest, ArtifactName = artifact }).Validate();
        await Assert.That(() => (abortedBeforeReservation with { ArtifactName = artifact }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (abortedBeforeReservation with { ArtifactName = "wrong" }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (abortedBeforeReservation with { Status = BundesligaContextSourceCycleStatus.UploadReserved, AbortCode = null }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Validators_reject_undefined_numeric_enum_values()
    {
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var cycle = new BundesligaContextSourceOuterCycle(identity, now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], (BundesligaContextSourceCycleStatus)999);
        await Assert.That(cycle.Validate).Throws<InvalidDataException>();
        await Assert.That(() => (cycle with { Status = BundesligaContextSourceCycleStatus.Claiming, EnabledSources = [(BundesligaContextSource)999] }).Validate()).Throws<InvalidDataException>();
    }
}
