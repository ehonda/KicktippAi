using System.Collections.Concurrent;
using System.Diagnostics;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

public sealed class BonusCommand_CopyCompatibility_Tests : BonusCommandTests_Base
{
    private const string TargetCommunity = "ehonda-ai-arena";
    private const string SourceCommunityContext = "pes-squad";

    [Test]
    public async Task Compatible_reference_copy_remaps_target_ids_without_model_call_or_persistence()
    {
        var sourceQuestion = CreateSourceQuestion();
        var targetQuestion = CreateTargetQuestion();
        var sourcePrediction = new BonusPrediction(["source-fcb"]);
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            sourceQuestion,
            sourcePrediction,
            communityContext: SourceCommunityContext);
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { targetQuestion },
            bonusPredictionCopyCandidate: candidate);

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains("Reused compatible reference prediction");
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(0);
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusPredictionWithResolvedContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ResolvedBonusContextManifest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        context.KpiContextProvider.As<IResolvedBonusContextProvider>().Verify(provider =>
            provider.ResolveBonusQuestionContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<BonusContextBudget?>()), Times.Never);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            TargetCommunity,
            It.Is<Dictionary<string, BonusPrediction>>(predictions =>
                predictions[targetQuestion.FormFieldName!].SelectedOptionIds.SequenceEqual(
                    new[] { "target-fcb" },
                    StringComparer.Ordinal)),
            false), Times.Once);
    }

    [Test]
    public async Task Changed_option_generates_once_and_persists_in_target_context()
    {
        var sourceQuestion = CreateSourceQuestion() with
        {
            Options =
            [
                new("source-fcb", "A different club"),
                new("source-bvb", "Borussia Dortmund")
            ]
        };
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            sourceQuestion,
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);

        await AssertIndependentFallbackAsync(
            candidate,
            "option_set_mismatch");
    }

    [Test]
    public async Task Missing_option_generates_once_and_persists_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        var targetQuestion = CreateTargetQuestion() with
        {
            Options = [new("target-bvb", "Borussia Dortmund")]
        };

        await AssertIndependentFallbackAsync(
            candidate,
            "option_set_mismatch",
            targetQuestion);
    }

    [Test]
    public async Task Extra_option_generates_once_and_persists_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        var targetQuestion = CreateTargetQuestion() with
        {
            Options =
            [
                .. CreateTargetQuestion().Options,
                new("target-rbl", "RB Leipzig")
            ]
        };

        await AssertIndependentFallbackAsync(
            candidate,
            "option_set_mismatch",
            targetQuestion);
    }

    [Test]
    public async Task Question_mismatch_generates_once_and_persists_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        var targetQuestion = CreateTargetQuestion() with { Text = "Who will finish second?" };

        await AssertIndependentFallbackAsync(
            candidate,
            "question_mismatch",
            targetQuestion);
    }

    [Test]
    public async Task Max_selection_mismatch_generates_once_and_persists_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        var targetQuestion = CreateTargetQuestion() with { MaxSelections = 2 };

        await AssertIndependentFallbackAsync(
            candidate,
            "max_selections_mismatch",
            targetQuestion,
            new BonusPrediction(["target-bvb", "target-fcb"]));
    }

    [Test]
    public async Task Duplicate_source_selection_generates_once_and_persists_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb", "source-fcb"]),
            communityContext: SourceCommunityContext);

        await AssertIndependentFallbackAsync(
            candidate,
            "invalid_source_selection");
    }

    [Test]
    public async Task Legacy_missing_option_provenance_generates_once_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext) with
        {
            QuestionCompatibilityManifest = null
        };

        await AssertIndependentFallbackAsync(
            candidate,
            "source_option_provenance_missing_or_invalid");
    }

    [Test]
    public async Task Malformed_option_provenance_generates_once_in_target_context()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        candidate = candidate with
        {
            QuestionCompatibilityManifest = candidate.QuestionCompatibilityManifest! with
            {
                CompatibilitySha256 = new string('0', 64)
            }
        };

        await AssertIndependentFallbackAsync(
            candidate,
            "source_option_provenance_missing_or_invalid");
    }

    [Test]
    public async Task Missing_source_candidate_generates_once_in_target_context()
    {
        await AssertIndependentFallbackAsync(
            null,
            "source_prediction_not_found");
    }

    [Test]
    public async Task Multiple_incompatible_questions_share_one_lazily_created_prediction_service()
    {
        var first = CreateTargetQuestion();
        var second = CreateTargetQuestion() with
        {
            Text = "Who will be the top scorer?",
            FormFieldName = "bonus_q2"
        };
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { first, second },
            bonusPredictionCopyCandidate: NullableOption.Some<BonusPredictionMetadata>(null),
            predictionResult: new BonusPrediction(["target-bvb"]));

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(1);
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Compatible_copy_trace_carries_payload_safe_source_identity()
    {
        const string predictionIdentity = "firestore-bonus-prediction-id";
        var activities = new ConcurrentQueue<Activity>();
        using var listener = CreateConcurrentActivityListener(activities);
        var sourceQuestion = CreateSourceQuestion();
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { CreateTargetQuestion() },
            bonusPredictionCopyCandidate: CreateCanonicalBundesligaBonusPredictionMetadata(
                sourceQuestion,
                new BonusPrediction(["source-fcb"]),
                communityContext: SourceCommunityContext,
                predictionIdentity: predictionIdentity));

        var exitCode = await RunCopyAsync(context);

        var expectedIdentityTag = $"|{predictionIdentity}|";
        var rootActivity = activities.SingleOrDefault(activity =>
            activity.OperationName == "bonus"
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.bonusCopySourcePredictionIdentities") as string,
                expectedIdentityTag,
                StringComparison.Ordinal));
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(rootActivity).IsNotNull();
        var compatibilityMetadata = rootActivity!.Tags
            .Where(tag => tag.Key.StartsWith("langfuse.trace.metadata.bonus", StringComparison.Ordinal))
            .ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
        var compatibilityHash = BonusQuestionCompatibilityManifest
            .Create(sourceQuestion)
            .CompatibilitySha256;
        await Assert.That(compatibilityMetadata).IsEquivalentTo(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["langfuse.trace.metadata.bonusPredictionMode"] = "reference-copy-with-independent-fallback",
                ["langfuse.trace.metadata.bonusCopySourceCommunityContext"] = SourceCommunityContext,
                ["langfuse.trace.metadata.bonusCopiedPredictionCount"] = "1",
                ["langfuse.trace.metadata.bonusIndependentFallbackCount"] = "0",
                ["langfuse.trace.metadata.bonusCopyCompatibilityHashes"] = $"|{compatibilityHash}|",
                ["langfuse.trace.metadata.bonusCopySourcePredictionIdentities"] = expectedIdentityTag
            });
    }

    [Test]
    public async Task Null_generated_prediction_fails_closed_before_target_write_or_post()
    {
        await AssertInvalidGeneratedPredictionFailsClosedAsync(null);
    }

    [Test]
    public async Task Unknown_generated_option_id_fails_closed_before_target_write_or_post()
    {
        await AssertInvalidGeneratedPredictionFailsClosedAsync(
            new BonusPrediction(["unknown-target-option"]));
    }

    [Test]
    public async Task Duplicate_generated_option_id_fails_closed_before_target_write_or_post()
    {
        var targetQuestion = CreateTargetQuestion() with { MaxSelections = 2 };

        await AssertInvalidGeneratedPredictionFailsClosedAsync(
            new BonusPrediction(["target-bvb", "target-bvb"]),
            targetQuestion);
    }

    [Test]
    public async Task Wrong_generated_selection_count_fails_closed_before_target_write_or_post()
    {
        var targetQuestion = CreateTargetQuestion() with { MaxSelections = 2 };

        await AssertInvalidGeneratedPredictionFailsClosedAsync(
            new BonusPrediction(["target-bvb"]),
            targetQuestion);
    }

    [Test]
    public async Task Invalid_target_option_definition_fails_before_model_or_posting()
    {
        var invalidTarget = CreateTargetQuestion() with
        {
            Options =
            [
                new("one", "Same option"),
                new("two", " Same   option ")
            ]
        };
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { invalidTarget });

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(0);
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Immutable_source_context_mismatch_fails_copy_without_model_or_posting()
    {
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateSourceQuestion(),
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        candidate = candidate with
        {
            ResolvedContextManifest = ResolvedBonusContextManifest.Create(
                candidate.ResolvedContextManifest!.Competition,
                "different-source-context",
                candidate.ResolvedContextManifest.Documents,
                candidate.ResolvedContextManifest.RosterPublicationSnapshotId,
                candidate.ResolvedContextManifest.ClubEloPublicationSnapshotId)
        };
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { CreateTargetQuestion() },
            bonusPredictionCopyCandidate: candidate);

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(0);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Immutable_target_context_failure_fails_copy_without_persistence_or_posting()
    {
        var contextProvider = CreateMockKpiContextProvider();
        contextProvider.As<IResolvedBonusContextProvider>()
            .Setup(provider => provider.ResolveBonusQuestionContextAsync(
                It.IsAny<BonusQuestion>(),
                TargetCommunity,
                It.IsAny<CancellationToken>(),
                It.IsAny<BonusContextBudget?>()))
            .ThrowsAsync(new InvalidOperationException("target publication head is unavailable"));
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { CreateTargetQuestion() },
            bonusPredictionCopyCandidate: NullableOption.Some<BonusPredictionMetadata>(null),
            contextProviderFactory: CreateMockContextProviderFactory(
                kpiContextProvider: contextProvider));

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusPredictionWithResolvedContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ResolvedBonusContextManifest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    private static async Task AssertIndependentFallbackAsync(
        BonusPredictionMetadata? candidate,
        string expectedReason,
        BonusQuestion? targetQuestion = null,
        BonusPrediction? generatedPrediction = null)
    {
        targetQuestion ??= CreateTargetQuestion();
        generatedPrediction ??= new BonusPrediction(["target-bvb"]);
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { targetQuestion },
            bonusPredictionCopyCandidate: NullableOption.Some<BonusPredictionMetadata>(candidate),
            predictionResult: generatedPrediction);

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains(expectedReason);
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(1);
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            targetQuestion,
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        context.KpiContextProvider.As<IResolvedBonusContextProvider>().Verify(provider =>
            provider.ResolveBonusQuestionContextAsync(
                targetQuestion,
                TargetCommunity,
                It.IsAny<CancellationToken>(),
                It.IsAny<BonusContextBudget?>()), Times.Once);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusPredictionWithResolvedContextAsync(
                targetQuestion,
                generatedPrediction,
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                TargetCommunity,
                It.IsAny<IEnumerable<string>>(),
                It.Is<ResolvedBonusContextManifest>(manifest =>
                    manifest.CommunityContext == TargetCommunity),
                false,
                It.IsAny<CancellationToken>()), Times.Once);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            TargetCommunity,
            It.Is<Dictionary<string, BonusPrediction>>(predictions =>
                BonusPredictionContentEquality.Equals(
                    predictions[targetQuestion.FormFieldName!],
                    generatedPrediction)),
            false), Times.Once);
    }

    private static async Task AssertInvalidGeneratedPredictionFailsClosedAsync(
        BonusPrediction? generated,
        BonusQuestion? targetQuestion = null)
    {
        targetQuestion ??= CreateTargetQuestion();
        var predictionService = CreateMockPredictionService();
        predictionService.Setup(service => service.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);
        var openAiServiceFactory = CreateMockOpenAiServiceFactory(
            predictionService: predictionService);
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { targetQuestion },
            bonusPredictionCopyCandidate: NullableOption.Some<BonusPredictionMetadata>(null),
            openAiServiceFactory: openAiServiceFactory);

        var exitCode = await RunCopyAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(CountPredictionServiceConstructions(context)).IsEqualTo(1);
        predictionService.Verify(service => service.PredictBonusQuestionAsync(
            targetQuestion,
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusPredictionWithResolvedContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ResolvedBonusContextManifest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusRepredictionWithResolvedContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<ResolvedBonusContextManifest>(),
                It.IsAny<CancellationToken>()), Times.Never);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    private static ActivityListener CreateConcurrentActivityListener(
        ConcurrentQueue<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "KicktippAi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static int CountPredictionServiceConstructions(BonusCommandTestContext context)
    {
        return context.OpenAiServiceFactory.Invocations.Count(invocation =>
            string.Equals(
                invocation.Method.Name,
                nameof(IOpenAiServiceFactory.CreatePredictionService),
                StringComparison.Ordinal));
    }

    private static Task<int> RunCopyAsync(BonusCommandTestContext context)
    {
        return context.App.RunAsync([
            "bonus",
            "test-model",
            "--community",
            TargetCommunity,
            "--community-context",
            SourceCommunityContext,
            "--competition",
            CompetitionIds.Bundesliga2026_27
        ]);
    }

    private static BonusQuestion CreateSourceQuestion()
    {
        return CreateLeagueWinnerBonusQuestion() with
        {
            Text = "  Who\t will win the league? ",
            Options =
            [
                new("source-fcb", "ＦＣ Bayern  München"),
                new("source-bvb", "Borussia Dortmund")
            ]
        };
    }

    private static BonusQuestion CreateTargetQuestion()
    {
        return CreateLeagueWinnerBonusQuestion() with
        {
            Text = "Who will win the league?",
            Options =
            [
                new("target-bvb", "Borussia   Dortmund"),
                new("target-fcb", "FC Bayern München")
            ]
        };
    }
}
