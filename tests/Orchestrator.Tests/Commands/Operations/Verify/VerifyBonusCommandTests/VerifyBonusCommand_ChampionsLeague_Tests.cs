using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

public sealed class VerifyBonusCommand_ChampionsLeague_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Exact_three_firestore_rows_and_full_kicktipp_readback_pass()
    {
        var (repository, firebaseFactory) = CreateRepository(missingQuestionId: null);
        var kicktipp = CreateKicktipp(CreateSnapshot(placedQuestionCount: 3));
        var context = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktipp));

        var (exitCode, output) = await RunCommandAsync(context.App, context.Console, ExactArguments().ToArray());

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All three strict CL bonus predictions match");
        repository.Verify(value => value.GetCurrentAsync(
            It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Missing_exact_firestore_row_fails_verification()
    {
        var (_, firebaseFactory) = CreateRepository(missingQuestionId: "1662326753");
        var context = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: CreateMockKicktippClientFactory(CreateKicktipp(CreateSnapshot(3))));

        var (exitCode, output) = await RunCommandAsync(context.App, context.Console, ExactArguments().ToArray());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("No exact CL lineage row exists");
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Empty_or_partial_kicktipp_readback_fails_verification(int placedQuestionCount)
    {
        var (_, firebaseFactory) = CreateRepository(missingQuestionId: null);
        var context = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: CreateMockKicktippClientFactory(CreateKicktipp(CreateSnapshot(placedQuestionCount))));

        var (exitCode, _) = await RunCommandAsync(context.App, context.Console, ExactArguments().ToArray());

        await Assert.That(exitCode).IsEqualTo(1);
    }

    private static (Mock<ISchadensfresseChampionsLeagueBonusPredictionRepository> Repository, Mock<IFirebaseServiceFactory> Factory)
        CreateRepository(string? missingQuestionId)
    {
        var repository = new Mock<ISchadensfresseChampionsLeagueBonusPredictionRepository>(MockBehavior.Strict);
        var genericRepository = repository.As<IPredictionRepository>();
        repository.Setup(value => value.GetCurrentAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchadensfresseChampionsLeagueBonusPredictionScope scope, CancellationToken _) =>
            {
                if (scope.SeedQuestion.KicktippQuestionId == missingQuestionId) return null;
                var prediction = CreatePrediction(scope.SeedQuestion);
                return new BonusPredictionMetadata(
                    prediction,
                    DateTimeOffset.Parse("2026-09-05T00:00:00Z"),
                    [],
                    SchadensfresseChampionsLeagueBonusManifest: SchadensfresseChampionsLeagueBonusManifest.Create(scope, "langfuse"));
            });
        var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        factory.Setup(value => value.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(genericRepository.Object);
        return (repository, factory);
    }

    private static Mock<IKicktippClient> CreateKicktipp(ChampionsLeagueBonusFormSnapshot snapshot)
    {
        var client = new Mock<IKicktippClient>(MockBehavior.Strict);
        client.Setup(value => value.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .ReturnsAsync(snapshot);
        return client;
    }

    private static ChampionsLeagueBonusFormSnapshot CreateSnapshot(int placedQuestionCount)
    {
        var questions = SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select((seed, index) =>
            new ChampionsLeagueBonusQuestionSnapshot(
                seed.KicktippQuestionId,
                new BonusQuestion(
                    seed.Text,
                    NodaTime.Text.InstantPattern.ExtendedIso.Parse(seed.Deadline).Value.InUtc(),
                    seed.Options.Select(option => new BonusQuestionOption(option.Id, option.Text)).ToList(),
                    seed.MaxSelections,
                    seed.FormKeys[0]),
                seed.FormKeys,
                index < placedQuestionCount
                    ? CreatePrediction(seed).SelectedOptionIds.Cast<string?>().ToArray()
                    : Enumerable.Repeat<string?>(null, seed.FormKeys.Count).ToArray())).ToArray();
        return new ChampionsLeagueBonusFormSnapshot(
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe"),
            "POST", questions, [], "submitbutton", "tippsSpeichern", true);
    }

    private static BonusPrediction CreatePrediction(SchadensfresseChampionsLeagueBonusSeedQuestion seed) =>
        new(seed.Options.Take(seed.MaxSelections).Select(option => option.Id).ToList());

    private static List<string> ExactArguments() =>
    [
        "verify-bonus", "gpt-5.6-sol", "--community", "schadensfresse", "--community-context", "schadensfresse",
        "--competition", "bundesliga-2026-27", "--reasoning-effort", "xhigh", "--max-output-tokens", "10000",
        "--prompt-source", "langfuse", "--langfuse-prompt-name", SchadensfresseChampionsLeagueBonusProfile.PromptName,
        "--langfuse-prompt-label", "production", "--langfuse-prompt-version", "1",
        "--bonus-profile", SchadensfresseChampionsLeagueBonusProfile.ProfileId,
        "--bonus-context-document-budget", "0", "--bonus-context-token-budget", "0",
        "--bonus-deadline-at-or-before", SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc
    ];
}
