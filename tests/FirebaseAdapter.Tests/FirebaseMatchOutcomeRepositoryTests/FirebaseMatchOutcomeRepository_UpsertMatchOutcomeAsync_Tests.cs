using EHonda.KicktippAi.Core;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_UpsertMatchOutcomeAsync_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Upserting_new_outcome_creates_document()
    {
        var repository = CreateRepository();
        var outcome = CreateOutcome();

        var result = await repository.UpsertMatchOutcomeAsync(outcome, "test-community");

        await Assert.That(result.Disposition).IsEqualTo(MatchOutcomeWriteDisposition.Created);
        await Assert.That(result.Outcome.HomeTeam).IsEqualTo(outcome.HomeTeam);
        await Assert.That(result.Outcome.AwayGoals).IsEqualTo(outcome.AwayGoals);
        await Assert.That(result.Outcome.CommunityContext).IsEqualTo("test-community");
    }

    [Test]
    public async Task Upserting_unchanged_outcome_returns_unchanged()
    {
        var repository = CreateRepository();
        var outcome = CreateOutcome();

        await repository.UpsertMatchOutcomeAsync(outcome, "test-community");
        var result = await repository.UpsertMatchOutcomeAsync(outcome, "test-community");

        await Assert.That(result.Disposition).IsEqualTo(MatchOutcomeWriteDisposition.Unchanged);
        await Assert.That(result.Outcome.HomeGoals).IsEqualTo(2);
        await Assert.That(result.Outcome.AwayGoals).IsEqualTo(1);
    }

    [Test]
    public async Task Upserting_changed_outcome_updates_existing_document()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(CreateOutcome(), "test-community");
        var result = await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeGoals: 3, awayGoals: 2),
            "test-community");

        await Assert.That(result.Disposition).IsEqualTo(MatchOutcomeWriteDisposition.Updated);
        await Assert.That(result.Outcome.HomeGoals).IsEqualTo(3);
        await Assert.That(result.Outcome.AwayGoals).IsEqualTo(2);
    }

    [Test]
    public async Task Upserting_changed_availability_updates_existing_document()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(availability: MatchOutcomeAvailability.Pending, homeGoals: null, awayGoals: null),
            "test-community");

        var result = await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(availability: MatchOutcomeAvailability.Completed, homeGoals: 2, awayGoals: 1),
            "test-community");

        await Assert.That(result.Disposition).IsEqualTo(MatchOutcomeWriteDisposition.Updated);
        await Assert.That(result.Outcome.Availability).IsEqualTo(MatchOutcomeAvailability.Completed);
    }

    [Test]
    public async Task Upserting_outcome_without_tippspiel_id_throws()
    {
        var repository = CreateRepository();

        await Assert.That(async () => await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(tippSpielId: null),
                "test-community"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("tippspielId is missing");
    }
}
