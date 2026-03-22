using EHonda.KicktippAi.Core;
using Moq;
using NodaTime;
using EHonda.Optional.Core;
using OpenAiIntegration.Tests.PredictionServiceTests;
using TUnit.Core;

namespace OpenAiIntegration.Tests.MatchPromptReconstructionServiceTests;

public class MatchPromptReconstructionService_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task Reconstructing_match_prompt_without_prediction_returns_null()
    {
        var predictionRepository = new Mock<IPredictionRepository>();
        var contextRepository = new Mock<IContextRepository>();
        var templateProvider = CreateMockTemplateProvider();
        var service = new MatchPromptReconstructionService(
            predictionRepository.Object,
            contextRepository.Object,
            templateProvider.Object);

        var result = await service.ReconstructMatchPredictionPromptAsync(
            CreateTestMatch(),
            "gpt-5",
            "test-community");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Reconstructing_match_prompt_uses_metadata_order_and_resolved_versions()
    {
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var predictionRepository = new Mock<IPredictionRepository>();
        predictionRepository
            .Setup(repository => repository.GetPredictionMetadataAsync(match, "gpt-5", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                new Prediction(2, 1),
                predictionCreatedAt,
                ["doc-b", "doc-a"]));

        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-b", predictionCreatedAt, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-b", "Bravo", 7, predictionCreatedAt.AddMinutes(-5)));
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-a", predictionCreatedAt, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-a", "Alpha", 3, predictionCreatedAt.AddMinutes(-10)));

        var templateProvider = CreateMockTemplateProvider();
        var service = new MatchPromptReconstructionService(
            predictionRepository.Object,
            contextRepository.Object,
            templateProvider.Object);

        var result = await service.ReconstructMatchPredictionPromptAsync(
            match,
            "gpt-5",
            "test-community");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ContextDocumentNames).IsEquivalentTo(["doc-b", "doc-a"]);
        await Assert.That(result.ResolvedContextDocuments.Select(document => document.Version).ToArray())
            .IsEquivalentTo([7, 3]);

        var expectedPrompt = PredictionPromptComposer.BuildSystemPrompt(
            "You are a football prediction expert. Predict the match outcome.",
            [
                new DocumentContext("doc-b", "Bravo"),
                new DocumentContext("doc-a", "Alpha")
            ]);

        await Assert.That(result.SystemPrompt).IsEqualTo(expectedPrompt);
        await Assert.That(result.MatchJson).IsEqualTo(PredictionPromptComposer.CreateMatchJson(match));
        await Assert.That(result.PromptTemplatePath).Contains("match.md");
        await Assert.That(result.PromptTimestamp).IsEqualTo(predictionCreatedAt);
    }

    [Test]
    public async Task Reconstructing_match_prompt_with_justification_uses_justification_template()
    {
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var predictionRepository = new Mock<IPredictionRepository>();
        predictionRepository
            .Setup(repository => repository.GetPredictionMetadataAsync(match, "gpt-5", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                new Prediction(2, 1),
                predictionCreatedAt,
                ["doc-a"]));

        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-a", predictionCreatedAt, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-a", "Alpha", 3, predictionCreatedAt.AddMinutes(-10)));

        var templateProvider = CreateMockTemplateProvider();
        var service = new MatchPromptReconstructionService(
            predictionRepository.Object,
            contextRepository.Object,
            templateProvider.Object);

        var result = await service.ReconstructMatchPredictionPromptAsync(
            match,
            "gpt-5",
            "test-community",
            includeJustification: true);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PromptTemplatePath).Contains("match.justification.md");
        await Assert.That(result.SystemPrompt)
            .Contains("provide justification")
            .And.Contains("doc-a");
    }

    [Test]
    public async Task Reconstructing_match_prompt_at_explicit_timestamp_uses_required_and_available_optional_documents()
    {
        var match = CreateTestMatch();
        var promptTimestamp = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1));
        var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);

        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-a", promptTimestamp, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-a", "Alpha", 3, promptTimestamp.AddMinutes(-15)));
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-b", promptTimestamp, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-b", "Bravo", 7, promptTimestamp.AddMinutes(-10)));
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-optional", promptTimestamp, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-optional", "Optional", 2, promptTimestamp.AddMinutes(-5)));
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync("doc-missing", promptTimestamp, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var templateProvider = CreateMockTemplateProvider();
        var service = new MatchPromptReconstructionService(
            predictionRepository.Object,
            contextRepository.Object,
            templateProvider.Object);

        var result = await service.ReconstructMatchPredictionPromptAtTimestampAsync(
            match,
            "gpt-5",
            "test-community",
            promptTimestamp,
            ["doc-a", "doc-b"],
            ["doc-optional", "doc-missing"]);

        await Assert.That(result.ContextDocumentNames).IsEquivalentTo(["doc-a", "doc-b", "doc-optional"]);
        await Assert.That(result.ResolvedContextDocuments.Select(document => document.Version).ToArray())
            .IsEquivalentTo([3, 7, 2]);
        await Assert.That(result.PromptTimestamp).IsEqualTo(promptTimestamp);
        await Assert.That(result.SystemPrompt).Contains("Optional");
    }
}
