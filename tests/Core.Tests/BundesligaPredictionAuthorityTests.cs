using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaPredictionAuthorityTests
{
    [Test]
    public async Task Direct_and_copy_authority_require_exact_fixed_scope_and_complete_references()
    {
        var direct = BundesligaPredictionContractTestData.DirectAuthority();
        var copy = BundesligaPredictionContractTestData.CopyAuthority();

        await Assert.That(direct.SeasonPartition).IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(direct.AuthorityEpoch).IsEqualTo("bundesliga-2026-27-typed-v1");
        await Assert.That(direct.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Direct);
        await Assert.That(direct.CopyBinding).IsNull();
        await Assert.That(copy.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Copy);
        await Assert.That(copy.CopyBinding).IsNotNull();

        var seed = BundesligaPredictionContractTestData.Seed();
        await Assert.That(() => BundesligaPredictionAuthority.CreateDirect(
            "fifa-world-cup-2026",
            BundesligaPredictionAuthority.AuthorityEpochValue,
            "pes-squad",
            "pes-squad",
            "pes-squad",
            seed.Reference,
            seed.Reference)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            "latest",
            "pes-squad",
            "pes-squad",
            "pes-squad",
            seed.Reference,
            seed.Reference)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            "pes-squad",
            "relaxdays-tippt",
            "pes-squad",
            seed.Reference,
            seed.Reference)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Local_item_identity_is_community_and_kind_scoped_not_global_or_participant_scoped()
    {
        var pes = BundesligaPredictionContractTestData.MatchKey("pes-squad", "42");
        var relaxdays = BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt", "42");
        var arenaParticipantOne = BundesligaPredictionContractTestData.MatchKey("ehonda-ai-arena", "42");
        var arenaParticipantTwo = BundesligaPredictionContractTestData.MatchKey("ehonda-ai-arena", "42");
        var arenaBonus = BundesligaPredictionContractTestData.BonusKey("ehonda-ai-arena", "42");

        await Assert.That(pes).IsNotEqualTo(relaxdays);
        await Assert.That(arenaParticipantOne).IsEqualTo(arenaParticipantTwo);
        await Assert.That(arenaParticipantOne).IsNotEqualTo(arenaBonus);
        await Assert.That(() => StableLocalItemKey.Create(
            "fifa-world-cup-2026",
            "ehonda-ai-arena",
            BundesligaPredictionItemKind.Match,
            "42")).Throws<InvalidDataException>();
    }
}
