using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Orchestrator.Commands.Operations.Verify;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

public sealed class VerifyBonusCommand_SchadensfressePrimaryRoute_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Schadensfresse_fails_closed_before_factories_or_source_prediction_reads()
    {
        var context = CreateVerifyBonusCommandApp();

        var exitCode = await context.App.RunAsync(
        [
            "verify-bonus", "test-model",
            "--community", "schadensfresse",
            "--community-context", "pes-squad",
            "--competition", CompetitionIds.Bundesliga2026_27
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("requires exact bundesliga-2026-27");
        await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KicktippClientFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.CredentialLoader.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KicktippClient.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KpiRepository.Invocations).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Canonical_schadensfresse_CL_set_resolves_rules_context_then_stops_without_current_prediction_reads()
    {
        var (factory, bindings, documents) = CreateCanonicalTypedContextFactory();
        var context = CreateVerifyBonusCommandApp(
            bonusQuestions: CreateCanonicalQuestions(),
            firebaseServiceFactory: factory);

        var exitCode = await context.App.RunAsync(
        [
            "verify-bonus", "test-model",
            "--community", "schadensfresse",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("immutable CL prompt provenance");
        var expectedBindingKey = new ResolvedTypedContextPublicationBindingKey(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "schadensfresse-champions-league-bonus-rules-only-v1",
            BundesligaSeasonRoutingSeed.Default.CanonicalSha256);
        bindings.Verify(repository => repository.GetExactAsync(
            It.Is<ResolvedTypedContextPublicationBindingKey>(key => key == expectedBindingKey),
            It.IsAny<CancellationToken>()), Times.Once);
        documents.Verify(repository => repository.GetContextDocumentAsync(
            SchadensfresseTypedContextProfiles.RulesDocumentName, 7, "schadensfresse", It.IsAny<CancellationToken>()), Times.Once);
        context.KicktippClient.Verify(client => client.GetOpenBonusQuestionsAsync(
            "schadensfresse", It.IsAny<CancellationToken>()), Times.Once);
        factory.Verify(serviceFactory => serviceFactory.CreatePredictionRepository(It.IsAny<string>()), Times.Never);
        factory.Verify(serviceFactory => serviceFactory.CreateKpiRepository(It.IsAny<string>()), Times.Never);
        await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
        context.KicktippClient.Verify(client => client.GetPlacedBonusPredictionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Conflicting_open_target_subcompetition_fails_before_typed_context_or_current_prediction_reads()
    {
        var questions = CreateCanonicalQuestions();
        questions[0] = questions[0] with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.DfbPokal
        };
        var context = CreateVerifyBonusCommandApp(bonusQuestions: questions);

        var exitCode = await context.App.RunAsync(
        [
            "verify-bonus", "test-model",
            "--community", "schadensfresse",
            "--community-context", "schadensfresse",
            "--competition", CompetitionIds.Bundesliga2026_27
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
        context.KicktippClient.Verify(client => client.GetPlacedBonusPredictionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Every_incomplete_or_mutated_target_question_set_fails_before_context_or_current_prediction_reads()
    {
        foreach (var questions in CreateInvalidQuestionSets())
        {
            var context = CreateVerifyBonusCommandApp(bonusQuestions: questions);
            var exitCode = await context.App.RunAsync(
            [
                "verify-bonus", "test-model",
                "--community", "schadensfresse",
                "--community-context", "schadensfresse",
                "--competition", CompetitionIds.Bundesliga2026_27
            ]);

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
            await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
            context.KicktippClient.Verify(client => client.GetPlacedBonusPredictionsAsync(It.IsAny<string>()), Times.Never);
        }
    }

    [Test]
    public async Task Target_command_propagates_a_pre_cancelled_token_before_dependency_activity()
    {
        var context = CreateVerifyBonusCommandApp();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var command = new VerifyBonusCommand(
            context.Console, context.FirebaseServiceFactory.Object, context.KicktippClientFactory.Object,
            context.CredentialLoader.Object, NullLogger<VerifyBonusCommand>.Instance);
        await Assert.That(() => command.ExecuteWithSettingsAsync(new VerifyBonusSettings
        {
            Community = "schadensfresse", CommunityContext = "schadensfresse", Competition = CompetitionIds.Bundesliga2026_27
        }, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.KicktippClientFactory.Invocations).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Target_command_propagates_the_exact_open_question_cancellation_without_current_prediction_activity()
    {
        var context = CreateVerifyBonusCommandApp();
        using var source = new CancellationTokenSource();
        context.KicktippClient.Setup(client => client.GetOpenBonusQuestionsAsync("schadensfresse", source.Token))
            .ThrowsAsync(new OperationCanceledException(source.Token));
        var command = new VerifyBonusCommand(context.Console, context.FirebaseServiceFactory.Object,
            context.KicktippClientFactory.Object, context.CredentialLoader.Object, NullLogger<VerifyBonusCommand>.Instance);

        await Assert.That(() => command.ExecuteWithSettingsAsync(new VerifyBonusSettings
        {
            Community = "schadensfresse", CommunityContext = "schadensfresse", Competition = CompetitionIds.Bundesliga2026_27
        }, source.Token)).Throws<OperationCanceledException>();
        context.KicktippClient.Verify(client => client.GetOpenBonusQuestionsAsync("schadensfresse", source.Token), Times.Once);
        await Assert.That(context.FirebaseServiceFactory.Invocations).Count().IsEqualTo(0);
        await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
        context.KicktippClient.Verify(client => client.GetPlacedBonusPredictionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Missing_binding_or_wrong_immutable_document_stops_before_current_prediction_reads()
    {
        var (missingFactory, missingBindings, missingDocuments) = CreateCanonicalTypedContextFactory();
        missingBindings.Reset();
        missingBindings.Setup(repository => repository.GetExactAsync(
                It.Is<ResolvedTypedContextPublicationBindingKey>(key =>
                    key.SeasonPartition == CompetitionIds.Bundesliga2026_27 &&
                    key.CommunityContext == "schadensfresse" &&
                    key.ProfileId == "schadensfresse-champions-league-bonus-rules-only-v1" &&
                    key.RoutingSeedSha256 == BundesligaSeasonRoutingSeed.Default.CanonicalSha256),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResolvedTypedContextPublicationBinding?)null);
        var missingContext = CreateVerifyBonusCommandApp(bonusQuestions: CreateCanonicalQuestions(), firebaseServiceFactory: missingFactory);
        var missingExit = await missingContext.App.RunAsync(CanonicalArguments());
        await Assert.That(missingExit).IsEqualTo(1);
        missingDocuments.VerifyNoOtherCalls();
        await AssertNoCurrentPredictionActivity(missingContext);

        var (documentFactory, _, documents) = CreateCanonicalTypedContextFactory();
        documents.Reset();
        documents.Setup(repository => repository.GetContextDocumentAsync(
                SchadensfresseTypedContextProfiles.RulesDocumentName, 7, "schadensfresse", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("wrong-name.md", "exact rules", 7, DateTimeOffset.UtcNow));
        var documentContext = CreateVerifyBonusCommandApp(bonusQuestions: CreateCanonicalQuestions(), firebaseServiceFactory: documentFactory);
        var documentExit = await documentContext.App.RunAsync(CanonicalArguments());
        await Assert.That(documentExit).IsEqualTo(1);
        await AssertNoCurrentPredictionActivity(documentContext);
    }

    [Test]
    public async Task Stale_future_or_over_budget_typed_context_fails_before_current_prediction_reads()
    {
        var variants = new[]
        {
            (ObservedAt: (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(-2), Content: "exact rules"),
            (ObservedAt: (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(1), Content: "exact rules"),
            (ObservedAt: (DateTimeOffset?)null, Content: new string('x', 10_000))
        };
        foreach (var variant in variants)
        {
            var (factory, _, _) = CreateCanonicalTypedContextFactory(variant.ObservedAt, variant.Content);
            var context = CreateVerifyBonusCommandApp(bonusQuestions: CreateCanonicalQuestions(), firebaseServiceFactory: factory);
            var exitCode = await context.App.RunAsync(CanonicalArguments());
            await Assert.That(exitCode).IsEqualTo(1);
            await AssertNoCurrentPredictionActivity(context);
        }
    }

    private static List<BonusQuestion> CreateCanonicalQuestions() => BundesligaSeasonRoutingSeed.Default.Questions
        .Select(question => new BonusQuestion(
            question.Text,
            question.Deadline.InZone(DateTimeZone.Utc),
            question.Options.ToList(),
            question.MaxSelections,
            $"fragetippForms[{question.KicktippQuestionId}].antwortIds[0]")
        {
            KicktippQuestionId = question.KicktippQuestionId
        })
        .ToList();

    private static IReadOnlyList<List<BonusQuestion>> CreateInvalidQuestionSets()
    {
        var canonical = CreateCanonicalQuestions();
        var extra = CreateCanonicalQuestions();
        extra.Add(extra[0] with { KicktippQuestionId = "9999999999" });
        var duplicate = CreateCanonicalQuestions();
        duplicate[1] = duplicate[1] with { KicktippQuestionId = duplicate[0].KicktippQuestionId };
        return
        [
            canonical.Take(2).ToList(),
            canonical.Skip(1).ToList(),
            canonical.Take(1).Concat(canonical.Skip(2)).ToList(),
            duplicate,
            extra,
            [canonical[0] with { KicktippQuestionId = "9999999999" }, canonical[1], canonical[2]],
            [canonical[0] with { Text = canonical[0].Text + " drift" }, canonical[1], canonical[2]],
            [canonical[0] with { Options = canonical[0].Options.AsEnumerable().Reverse().ToList() }, canonical[1], canonical[2]],
            [canonical[0] with { MaxSelections = canonical[0].MaxSelections + 1 }, canonical[1], canonical[2]],
            [canonical[0] with { Deadline = canonical[0].Deadline.Plus(Duration.FromHours(1)) }, canonical[1], canonical[2]],
            [canonical[0] with { BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga }, canonical[1], canonical[2]]
        ];
    }

    private static string[] CanonicalArguments() =>
    [
        "verify-bonus", "test-model", "--community", "schadensfresse", "--community-context", "schadensfresse",
        "--competition", CompetitionIds.Bundesliga2026_27
    ];

    private static async Task AssertNoCurrentPredictionActivity(VerifyBonusCommandTestContext context)
    {
        await Assert.That(context.PredictionRepository.Invocations).Count().IsEqualTo(0);
        context.KicktippClient.Verify(client => client.GetPlacedBonusPredictionsAsync(It.IsAny<string>()), Times.Never);
    }

    private static (Mock<IFirebaseServiceFactory> Factory, Mock<IResolvedTypedContextPublicationBindingRepository> Bindings, Mock<IContextRepository> Documents) CreateCanonicalTypedContextFactory(DateTimeOffset? observedAt = null, string? rulesContent = null)
    {
        var content = rulesContent ?? "exact rules";
        var seed = BundesligaSeasonRoutingSeed.Default;
        var document = new ResolvedTypedContextDocument(
            SchadensfresseTypedContextProfiles.RulesDocumentKind,
            SchadensfresseTypedContextProfiles.RulesDocumentName,
            7,
            DocumentPublicationContract.ComputeContentSha256(content));
        var binding = new ResolvedTypedContextPublicationBinding(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "schadensfresse-champions-league-bonus-rules-only-v1",
            seed.CanonicalSha256,
            BundesligaSeasonSubcompetition.ChampionsLeague,
            observedAt ?? DateTimeOffset.UtcNow,
            seed.RulesSchemaVersion,
            seed.CanonicalRulesSha256,
            document);
        var bindings = new Mock<IResolvedTypedContextPublicationBindingRepository>();
        bindings.Setup(repository => repository.GetExactAsync(binding.Key, It.IsAny<CancellationToken>())).ReturnsAsync(binding);
        var documents = new Mock<IContextRepository>();
        documents.Setup(repository => repository.GetContextDocumentAsync(document.Name, document.Version, "schadensfresse", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(document.Name, content, document.Version, DateTimeOffset.UtcNow));
        return (CreateMockFirebaseServiceFactoryFull(
            contextRepository: documents,
            resolvedTypedContextPublicationBindingRepository: bindings), bindings, documents);
    }
}
