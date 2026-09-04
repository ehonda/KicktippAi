using EHonda.KicktippAi.Core;

namespace Core.Tests;

public sealed class BundesligaPredictionContextObservationV2Tests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Test]
    public async Task Observation_is_immutable_and_rejects_noncanonical_identity()
    {
        var provenance = Context();
        var observation = BundesligaPredictionContextObservationV2.Create(
            "pes-squad", "bundesliga-primary-v1", provenance);

        await Assert.That(observation.CommunityContext).IsEqualTo("pes-squad");
        await Assert.That(observation.ProfileId).IsEqualTo("bundesliga-primary-v1");
        await Assert.That(observation.Provenance).IsSameReferenceAs(provenance);
        await Assert.That(() => BundesligaPredictionContextObservationV2.Create(
            "Pes Squad", "bundesliga-primary-v1", provenance)).Throws<ArgumentException>();
        await Assert.That(() => BundesligaPredictionContextObservationV2.Create(
            "pes-squad", " profile ", provenance)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Require_binds_exact_authority_context_and_current_profile()
    {
        var seed = BundesligaIdentitySeedReference.Create(1, Sha);
        var authority = BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27, BundesligaPredictionAuthority.AuthorityEpochValue,
            "pes-squad", "pes-squad", "pes-squad", seed, seed);
        var identity = BundesligaTypedCurrentIdentity.Create(
            "route-v1", "profile-v1", BundesligaGenerationInputContractReference.Create("input-v1", Sha));
        var observation = BundesligaPredictionContextObservationV2.Create(
            "pes-squad", "profile-v1", Context());

        observation.Require(authority, identity);

        var wrong = BundesligaPredictionContextObservationV2.Create(
            "pes-squad", "other-profile", Context());
        await Assert.That(() => wrong.Require(authority, identity)).Throws<InvalidDataException>();
    }

    private static PredictionContextProvenanceV2 Context() => PredictionContextProvenanceV2.Create(
        "context-v1", Sha, "rules-v1", Sha,
        [new PredictionContextDocumentIdentityV2("rules.md@1", Sha)]);
}
