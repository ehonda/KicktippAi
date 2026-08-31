using System.Text;
using EHonda.KicktippAi.Core;
using NodaTime;

namespace Core.Tests;

public class PredictionGenerationProvenanceV2Tests
{
    [Test]
    public async Task Direct_provenance_has_stable_canonical_bytes_hash_and_strict_round_trip()
    {
        var provenance = BundesligaPredictionContractTestData.DirectProvenance();
        var restored = PredictionGenerationProvenanceV2.DeserializeCanonical(provenance.SerializeCanonical());

        await Assert.That(restored.SerializeCanonical()).IsEquivalentTo(provenance.SerializeCanonical());
        await Assert.That(restored.CanonicalSha256).IsEqualTo(provenance.CanonicalSha256);
        await Assert.That(provenance.CanonicalSha256).IsEqualTo("bdcf9ad505816ae2aba0af20c6c85b45bc6a42c47d42407e34851b3eff1002ce");
        await Assert.That(restored.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Direct);

        var json = Encoding.UTF8.GetString(provenance.SerializeCanonical());
        foreach (var mutation in new[]
        {
            json.Replace("\"schemaVersion\":\"prediction-generation-provenance-v2\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
            json.Replace("\"physicalStorageNamespace\":", "\"unexpected\":true,\"physicalStorageNamespace\":", StringComparison.Ordinal),
            json.Replace("\"repredictionIndex\":0", "\"repredictionIndex\":\"0\"", StringComparison.Ordinal),
            " " + json
        })
        {
            await Assert.That(() => PredictionGenerationProvenanceV2.DeserializeCanonical(Encoding.UTF8.GetBytes(mutation)))
                .Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Copy_provenance_requires_copy_identity_and_truthful_zero_target_usage()
    {
        var postingSeed = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var sourceSeed = BundesligaPredictionContractTestData.Seed("pes-squad");
        var authority = BundesligaPredictionContractTestData.CopyAuthority(postingSeed, sourceSeed);
        var posting = postingSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt")).MatchSnapshot!;
        var source = sourceSeed.RequireEntry(BundesligaPredictionContractTestData.MatchKey("pes-squad")).MatchSnapshot!;

        PredictionGenerationProvenanceV2 Create(string? sourceIdentity, PredictionGenerationUsageV2 usage) =>
            PredictionGenerationProvenanceV2.Create(
                authority, "match-predictions-bundesliga-2026-27-typed-v1",
                posting.Key, posting.SnapshotHash, source.Key, source.SnapshotHash,
                BundesligaPredictionContractTestData.MatchRoute, "copy-profile-v1", sourceIdentity,
                BundesligaPredictionContractTestData.Prompt(), BundesligaPredictionContractTestData.Model(),
                PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
                BundesligaPredictionContractTestData.Context(), Instant.FromUtc(2026, 8, 31, 12, 0),
                "copied-prediction-42", 0, usage);

        var copy = Create("source-prediction-42", new PredictionGenerationUsageV2(0, 0, 0));
        await Assert.That(copy.TargetGenerationUsage.IsZero).IsTrue();
        await Assert.That(PredictionGenerationProvenanceV2.DeserializeCanonical(copy.SerializeCanonical()).SourcePredictionIdentity)
            .IsEqualTo("source-prediction-42");
        await Assert.That(() => Create(null, new PredictionGenerationUsageV2(0, 0, 0))).Throws<ArgumentException>();
        await Assert.That(() => Create("source-prediction-42", new PredictionGenerationUsageV2(1, 0, 0))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Prompt_service_context_and_direct_copy_invariants_reject_drift()
    {
        await Assert.That(() => PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.Hosted, BundesligaPredictionContractTestData.MatchPrompt, 3,
            BundesligaPredictionContractTestData.ShaA, "production", false)).Throws<InvalidDataException>();
        await Assert.That(() => PredictionServiceTierProvenanceV2.Create("flex", "standard", false))
            .Throws<InvalidDataException>();
        await Assert.That(() => PredictionContextProvenanceV2.Create(
            "manifest", BundesligaPredictionContractTestData.ShaA,
            "rules", BundesligaPredictionContractTestData.ShaB,
            [new("same", BundesligaPredictionContractTestData.ShaA), new("same", BundesligaPredictionContractTestData.ShaB)]))
            .Throws<InvalidDataException>();

        var snapshot = BundesligaPredictionContractTestData.Match();
        await Assert.That(() => PredictionGenerationProvenanceV2.Create(
            BundesligaPredictionContractTestData.DirectAuthority(),
            "bonus-predictions-bundesliga-2026-27-typed-v1",
            snapshot.Key, snapshot.SnapshotHash, snapshot.Key, snapshot.SnapshotHash,
            BundesligaPredictionContractTestData.MatchRoute, "profile", "forbidden-source",
            BundesligaPredictionContractTestData.Prompt(), BundesligaPredictionContractTestData.Model(),
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            BundesligaPredictionContractTestData.Context(), Instant.FromUtc(2026, 8, 31, 12, 0),
            "prediction", 0, new PredictionGenerationUsageV2(0, 0, 0))).Throws<InvalidDataException>();
    }
}
