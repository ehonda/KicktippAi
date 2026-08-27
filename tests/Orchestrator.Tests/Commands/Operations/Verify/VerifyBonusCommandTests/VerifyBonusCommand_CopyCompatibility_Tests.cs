using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

public sealed class VerifyBonusCommand_CopyCompatibility_Tests : VerifyBonusCommandTests_Base
{
    private const string TargetCommunity = "ehonda-ai-arena";
    private const string SourceCommunityContext = "pes-squad";

    [Test]
    public async Task Compatible_reference_copy_maps_target_ids_and_verifies_without_a_prediction_service()
    {
        var sourceQuestion = CreateSourceQuestion();
        var targetQuestion = CreateTargetQuestion();
        var sourcePrediction = new BonusPrediction(["source-fcb"]);
        var mappedTargetPrediction = new BonusPrediction(["target-fcb"]);
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            sourceQuestion,
            sourcePrediction,
            communityContext: SourceCommunityContext);
        var repository = CreateMockPredictionRepository(
            getBonusPredictionCopyCandidateResult: candidate);
        var context = CreateContext(
            repository,
            targetQuestion,
            mappedTargetPrediction);

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains("All predictions are valid");
        repository.As<IBonusPredictionCopyRepository>().Verify(candidateRepository =>
            candidateRepository.GetBonusPredictionCopyCandidateAsync(
                targetQuestion,
                It.IsAny<PredictionModelConfig>(),
                SourceCommunityContext,
                It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            It.IsAny<PredictionModelConfig>(),
            TargetCommunity,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Incompatible_reference_source_verifies_exact_target_context_fallback()
    {
        var targetQuestion = CreateTargetQuestion();
        var incompatibleSourceQuestion = CreateSourceQuestion() with
        {
            Options =
            [
                new BonusQuestionOption("source-fcb", "A different club"),
                new BonusQuestionOption("source-bvb", "Borussia Dortmund")
            ]
        };
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            incompatibleSourceQuestion,
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext);
        var targetPrediction = new BonusPrediction(["target-bvb"]);
        var targetMetadata = CreateCanonicalBundesligaBonusPredictionMetadata(
            targetQuestion,
            targetPrediction,
            communityContext: TargetCommunity,
            predictionIdentity: "target-fallback-id");
        var repository = CreateReferenceRepository(candidate, targetQuestion, targetPrediction, targetMetadata);
        var context = CreateContext(repository, targetQuestion, targetPrediction);

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains("All predictions are valid");
        VerifyTargetFallbackReads(repository, targetQuestion, Times.Once());
    }

    [Test]
    public async Task Missing_target_context_fallback_fails_verification()
    {
        var targetQuestion = CreateTargetQuestion();
        var repository = CreateMockPredictionRepository(
            getBonusPredictionCopyCandidateResult: null);
        var context = CreateContext(
            repository,
            targetQuestion,
            new BonusPrediction(["target-fcb"]));

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("Questions with database predictions: 0");
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
            targetQuestion.Text,
            It.IsAny<PredictionModelConfig>(),
            TargetCommunity,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Outdated_target_context_fallback_fails_verification()
    {
        var targetQuestion = CreateTargetQuestion();
        var targetPrediction = new BonusPrediction(["target-fcb"]);
        var targetMetadata = CreateCanonicalBundesligaBonusPredictionMetadata(
            targetQuestion,
            targetPrediction,
            communityContext: TargetCommunity,
            predictionIdentity: "target-fallback-id") with
        {
            ResolvedContextManifest = null
        };
        var repository = CreateReferenceRepository(
            candidate: null,
            targetQuestion,
            targetPrediction,
            targetMetadata);
        var context = CreateContext(repository, targetQuestion, targetPrediction);

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("outdated");
        VerifyTargetFallbackReads(repository, targetQuestion, Times.Once());
    }

    [Test]
    public async Task Compatible_source_with_invalid_immutable_provenance_fails_without_target_fallback()
    {
        var sourceQuestion = CreateSourceQuestion();
        var targetQuestion = CreateTargetQuestion();
        var candidate = CreateCanonicalBundesligaBonusPredictionMetadata(
            sourceQuestion,
            new BonusPrediction(["source-fcb"]),
            communityContext: SourceCommunityContext) with
        {
            ResolvedContextManifest = null
        };
        var repository = CreateMockPredictionRepository(
            getBonusPredictionCopyCandidateResult: candidate);
        var context = CreateContext(
            repository,
            targetQuestion,
            new BonusPrediction(["target-fcb"]));

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("outdated");
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            It.IsAny<PredictionModelConfig>(),
            TargetCommunity,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Ambiguous_target_question_fails_closed_before_source_or_fallback_reads()
    {
        var targetQuestion = CreateTargetQuestion() with
        {
            Options =
            [
                new BonusQuestionOption("target-one", "FC Bayern München"),
                new BonusQuestionOption("target-two", "  FC Bayern München  ")
            ]
        };
        var repository = CreateMockPredictionRepository();
        var context = CreateContext(
            repository,
            targetQuestion,
            new BonusPrediction(["target-one"]));

        var exitCode = await RunReferenceVerificationAsync(context);

        await Assert.That(exitCode).IsEqualTo(1);
        repository.As<IBonusPredictionCopyRepository>().Verify(candidateRepository =>
            candidateRepository.GetBonusPredictionCopyCandidateAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            It.IsAny<PredictionModelConfig>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static VerifyBonusCommandTestContext CreateContext(
        Mock<IPredictionRepository> repository,
        BonusQuestion targetQuestion,
        BonusPrediction placedPrediction)
    {
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: repository);
        return CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { targetQuestion },
            placedBonusPredictions: CreatePlacedBonusPredictions(
                targetQuestion.FormFieldName!,
                placedPrediction),
            firebaseServiceFactory: firebaseFactory);
    }

    private static Mock<IPredictionRepository> CreateReferenceRepository(
        BonusPredictionMetadata? candidate,
        BonusQuestion targetQuestion,
        BonusPrediction targetPrediction,
        BonusPredictionMetadata targetMetadata)
    {
        var repository = CreateMockPredictionRepository(
            getBonusPredictionCopyCandidateResult: candidate);
        repository.Setup(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
                targetQuestion.Text,
                It.IsAny<PredictionModelConfig>(),
                TargetCommunity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetPrediction);
        repository.Setup(candidateRepository => candidateRepository.GetBonusPredictionMetadataByTextAsync(
                targetQuestion.Text,
                It.IsAny<PredictionModelConfig>(),
                TargetCommunity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetMetadata);
        return repository;
    }

    private static void VerifyTargetFallbackReads(
        Mock<IPredictionRepository> repository,
        BonusQuestion targetQuestion,
        Times times)
    {
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionByTextAsync(
            targetQuestion.Text,
            It.IsAny<PredictionModelConfig>(),
            TargetCommunity,
            It.IsAny<CancellationToken>()), times);
        repository.Verify(candidateRepository => candidateRepository.GetBonusPredictionMetadataByTextAsync(
            targetQuestion.Text,
            It.IsAny<PredictionModelConfig>(),
            TargetCommunity,
            It.IsAny<CancellationToken>()), times);
    }

    private static Task<int> RunReferenceVerificationAsync(VerifyBonusCommandTestContext context)
    {
        return context.App.RunAsync(
        [
            "verify-bonus",
            "test-model",
            "--community", TargetCommunity,
            "--community-context", SourceCommunityContext,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--check-outdated",
            "--agent"
        ]);
    }

    private static BonusQuestion CreateSourceQuestion()
    {
        return CreateTestBonusQuestion(
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("source-fcb", "FC Bayern München"),
                new BonusQuestionOption("source-bvb", "Borussia Dortmund")
            });
    }

    private static BonusQuestion CreateTargetQuestion()
    {
        return CreateTestBonusQuestion(
            formFieldName: "target-form-field",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("target-bvb", "Borussia Dortmund"),
                new BonusQuestionOption("target-fcb", "FC Bayern München")
            });
    }
}
