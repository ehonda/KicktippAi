using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using OpenAiIntegration;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

public sealed class BonusCommand_ChampionsLeague_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task First_run_stores_three_exact_rows_then_posts_one_complete_payload_and_verifies()
    {
        var blank = CreateSnapshot(placed: false);
        var placed = CreateSnapshot(placed: true);
        var stored = new Dictionary<string, BonusPredictionMetadata>(StringComparer.Ordinal);
        var strictRepository = new Mock<ISchadensfresseChampionsLeagueBonusPredictionRepository>(MockBehavior.Strict);
        var genericRepository = strictRepository.As<IPredictionRepository>();
        strictRepository.Setup(repository => repository.GetCurrentAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchadensfresseChampionsLeagueBonusPredictionScope scope, CancellationToken _) =>
                stored.GetValueOrDefault(scope.SeedQuestion.KicktippQuestionId));
        strictRepository.Setup(repository => repository.SaveAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(),
                It.IsAny<BonusPrediction>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), false,
                It.IsAny<CancellationToken>()))
            .Callback((SchadensfresseChampionsLeagueBonusPredictionScope scope, BonusPrediction prediction,
                string provider, string _, double _, bool _, CancellationToken _) =>
                stored[scope.SeedQuestion.KicktippQuestionId] = CreateMetadata(scope, prediction, provider))
            .Returns(Task.CompletedTask);
        var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(genericRepository.Object);

        var kicktipp = new Mock<IKicktippClient>(MockBehavior.Strict);
        kicktipp.Setup(client => client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .ReturnsAsync(blank);
        kicktipp.Setup(client => client.PlaceChampionsLeagueBonusPredictionsAsync(
                "schadensfresse", It.IsAny<ChampionsLeagueBonusFormSnapshot>(),
                It.Is<IReadOnlyList<(string QuestionId, BonusPrediction Prediction)>>(results => results.Count == 3), true))
            .ReturnsAsync(placed);
        var predictionService = CreateMockPredictionService();
        var openAiFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);
        var contextFactory = new Mock<IContextProviderFactory>(MockBehavior.Strict);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktipp),
            openAiServiceFactory: openAiFactory,
            contextProviderFactory: contextFactory);

        var exitCode = await context.App.RunAsync(ExactArguments());

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stored.Count).IsEqualTo(3);
        strictRepository.Verify(repository => repository.SaveAsync(
            It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<BonusPrediction>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), false, It.IsAny<CancellationToken>()), Times.Exactly(3));
        strictRepository.Verify(repository => repository.GetCurrentAsync(
            It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
        kicktipp.Verify(client => client.PlaceChampionsLeagueBonusPredictionsAsync(
            "schadensfresse", It.IsAny<ChampionsLeagueBonusFormSnapshot>(),
            It.IsAny<IReadOnlyList<(string QuestionId, BonusPrediction Prediction)>>(), true), Times.Once);
        predictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.Is<IEnumerable<DocumentContext>>(documents => !documents.Any()),
            It.Is<PredictionTelemetryMetadata?>(metadata => metadata!.BonusContextDocumentBudget == 0
                                                         && metadata.BonusContextEstimatedTokenBudget == 0),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        contextFactory.VerifyNoOtherCalls();
        firebaseFactory.Verify(factory => factory.CreateDocumentPublicationRepository(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Per_question_generation_failure_can_leave_bounded_cache_but_never_posts_partial_results()
    {
        var strictRepository = new Mock<ISchadensfresseChampionsLeagueBonusPredictionRepository>(MockBehavior.Strict);
        var genericRepository = strictRepository.As<IPredictionRepository>();
        strictRepository.Setup(repository => repository.GetCurrentAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPredictionMetadata?)null);
        strictRepository.Setup(repository => repository.SaveAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(),
                It.IsAny<BonusPrediction>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), false,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(genericRepository.Object);

        var kicktipp = new Mock<IKicktippClient>(MockBehavior.Strict);
        kicktipp.Setup(client => client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .ReturnsAsync(CreateSnapshot(placed: false));
        var predictionService = new Mock<IPredictionService>();
        predictionService.Setup(service => service.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(), It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusQuestion question, IEnumerable<DocumentContext> _, PredictionTelemetryMetadata? _, CancellationToken _) =>
                SchadensfresseChampionsLeagueBonusProfile.GetQuestionId(question) == "1662326753"
                    ? null
                    : new BonusPrediction(question.Options.Take(question.MaxSelections).Select(option => option.Id).ToList()));
        predictionService.Setup(service => service.GetBonusPromptPath()).Returns("prompts/bundesliga-2026-27/champions-league/bonus.md");
        var contextFactory = new Mock<IContextProviderFactory>(MockBehavior.Strict);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktipp),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService),
            contextProviderFactory: contextFactory);

        var exitCode = await context.App.RunAsync(ExactArguments());

        await Assert.That(exitCode).IsEqualTo(1);
        strictRepository.Verify(repository => repository.SaveAsync(
            It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<BonusPrediction>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        kicktipp.Verify(client => client.PlaceChampionsLeagueBonusPredictionsAsync(
            It.IsAny<string>(), It.IsAny<ChampionsLeagueBonusFormSnapshot>(),
            It.IsAny<IReadOnlyList<(string QuestionId, BonusPrediction Prediction)>>(), It.IsAny<bool>()), Times.Never);
        contextFactory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Complete_exact_cache_and_readback_return_before_context_or_model_construction()
    {
        var snapshot = CreateSnapshot(placed: true);
        var strictRepository = new Mock<ISchadensfresseChampionsLeagueBonusPredictionRepository>(MockBehavior.Strict);
        var genericRepository = strictRepository.As<IPredictionRepository>();
        strictRepository.Setup(repository => repository.GetCurrentAsync(
                It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SchadensfresseChampionsLeagueBonusPredictionScope scope, CancellationToken _) =>
            {
                var prediction = CreatePrediction(scope.SeedQuestion);
                return new BonusPredictionMetadata(
                    prediction,
                    DateTimeOffset.Parse("2026-09-05T00:00:00Z"),
                    [],
                    SchadensfresseChampionsLeagueBonusManifest: SchadensfresseChampionsLeagueBonusManifest.Create(scope, "langfuse"));
            });
        var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(genericRepository.Object);

        var kicktipp = new Mock<IKicktippClient>(MockBehavior.Strict);
        kicktipp.Setup(client => client.GetChampionsLeagueBonusFormSnapshotAsync("schadensfresse"))
            .ReturnsAsync(snapshot);
        var kicktippFactory = CreateMockKicktippClientFactory(kicktipp);
        var openAiFactory = CreateMockOpenAiServiceFactory();
        var contextFactory = new Mock<IContextProviderFactory>(MockBehavior.Strict);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: kicktippFactory,
            openAiServiceFactory: openAiFactory,
            contextProviderFactory: contextFactory);

        var exitCode = await context.App.RunAsync(ExactArguments());

        await Assert.That(exitCode).IsEqualTo(0);
        contextFactory.VerifyNoOtherCalls();
        openAiFactory.Verify(factory => factory.GetTokenUsageTracker(), Times.Once);
        openAiFactory.Verify(factory => factory.CreatePredictionService(
            It.IsAny<string>(), It.IsAny<PredictionServiceOptions>(), It.IsAny<IInstructionsTemplateProvider>()), Times.Never);
        kicktipp.Verify(client => client.PlaceChampionsLeagueBonusPredictionsAsync(
            It.IsAny<string>(), It.IsAny<ChampionsLeagueBonusFormSnapshot>(),
            It.IsAny<IReadOnlyList<(string QuestionId, BonusPrediction Prediction)>>(), It.IsAny<bool>()), Times.Never);
        firebaseFactory.Verify(factory => factory.CreateDocumentPublicationRepository(It.IsAny<string>()), Times.Never);
        strictRepository.Verify(repository => repository.GetCurrentAsync(
            It.IsAny<SchadensfresseChampionsLeagueBonusPredictionScope>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Near_match_profile_fails_before_credentials_clients_context_or_model()
    {
        var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        var kicktippFactory = new Mock<IKicktippClientFactory>(MockBehavior.Strict);
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
        var contextFactory = new Mock<IContextProviderFactory>(MockBehavior.Strict);
        var credentials = new Mock<ICommunityKicktippCredentialLoader>(MockBehavior.Strict);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: firebaseFactory,
            kicktippClientFactory: kicktippFactory,
            openAiServiceFactory: openAiFactory,
            contextProviderFactory: contextFactory,
            credentialLoader: credentials);
        var arguments = ExactArguments();
        arguments[arguments.IndexOf("10000")] = "9999";

        var exitCode = await context.App.RunAsync(arguments);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("does not match the")
            .And.Contains("complete frozen profile tuple");
        firebaseFactory.VerifyNoOtherCalls();
        kicktippFactory.VerifyNoOtherCalls();
        openAiFactory.VerifyNoOtherCalls();
        contextFactory.VerifyNoOtherCalls();
        credentials.VerifyNoOtherCalls();
    }

    private static List<string> ExactArguments() =>
    [
        "bonus", "gpt-5.6-sol", "--community", "schadensfresse", "--community-context", "schadensfresse",
        "--competition", "bundesliga-2026-27", "--reasoning-effort", "xhigh", "--max-output-tokens", "10000",
        "--prompt-source", "langfuse", "--langfuse-prompt-name", SchadensfresseChampionsLeagueBonusProfile.PromptName,
        "--langfuse-prompt-label", "production", "--langfuse-prompt-version", "1",
        "--bonus-profile", SchadensfresseChampionsLeagueBonusProfile.ProfileId,
        "--bonus-context-document-budget", "0", "--bonus-context-token-budget", "0",
        "--bonus-deadline-at-or-before", SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc,
        "--override-kicktipp"
    ];

    private static ChampionsLeagueBonusFormSnapshot CreateSnapshot(bool placed)
    {
        var questions = SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(seed =>
            new ChampionsLeagueBonusQuestionSnapshot(
                seed.KicktippQuestionId,
                new BonusQuestion(
                    seed.Text,
                    NodaTime.Text.InstantPattern.ExtendedIso.Parse(seed.Deadline).Value.InUtc(),
                    seed.Options.Select(option => new BonusQuestionOption(option.Id, option.Text)).ToList(),
                seed.MaxSelections,
                seed.FormKeys[0]),
                seed.FormKeys,
                placed
                    ? CreatePrediction(seed).SelectedOptionIds.Cast<string?>().ToArray()
                    : Enumerable.Repeat<string?>(null, seed.FormKeys.Count).ToArray())).ToArray();
        return new ChampionsLeagueBonusFormSnapshot(
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true"),
            new Uri("https://www.kicktipp.de/schadensfresse/tippabgabe"),
            "POST", questions, [new("csrf", "token")], "submitbutton", "tippsSpeichern", true);
    }

    private static BonusPrediction CreatePrediction(SchadensfresseChampionsLeagueBonusSeedQuestion seed) =>
        new(seed.Options.Take(seed.MaxSelections).Select(option => option.Id).ToList());

    private static BonusPredictionMetadata CreateMetadata(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        BonusPrediction prediction,
        string provider) =>
        new(
            prediction,
            DateTimeOffset.Parse("2026-09-05T00:00:00Z"),
            [],
            SchadensfresseChampionsLeagueBonusManifest: SchadensfresseChampionsLeagueBonusManifest.Create(scope, provider));
}
