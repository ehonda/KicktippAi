using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Moq;
using Orchestrator.Commands.Observability.ReconstructPrompt;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.ReconstructPromptCommandTests;

public class ReconstructPromptCommand_Tests
{
    [Test]
    public async Task Running_command_with_matching_prediction_prints_reconstructed_prompt()
    {
        var match = new Match("Team A", "Team B", NodaTime.Instant.FromUtc(2025, 10, 30, 15, 30).InUtc(), 7);
        var predictionRepository = new Mock<IPredictionRepository>();
        predictionRepository
            .Setup(repository => repository.GetStoredMatchAsync(
                "Team A",
                "Team B",
                7,
                It.Is<PredictionModelConfig>(config =>
                    config.Model == "gpt-5" &&
                    config.ReasoningEffort == "none" &&
                    config.MaxOutputTokenCount == 8000 &&
                    config.PromptName == "kicktippai/bundesliga-2026-27/custom-match" &&
                    config.PromptVersion == 7),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        predictionRepository
            .Setup(repository => repository.GetPredictionMetadataAsync(
                It.Is<Match>(candidate =>
                    candidate.HomeTeam == match.HomeTeam &&
                    candidate.AwayTeam == match.AwayTeam &&
                    candidate.Matchday == match.Matchday &&
                    candidate.StartsAt.ToInstant() == match.StartsAt.ToInstant()),
                It.Is<PredictionModelConfig>(config =>
                    config.Model == "gpt-5" &&
                    config.ReasoningEffort == "none" &&
                    config.MaxOutputTokenCount == 8000 &&
                    config.PromptName == "kicktippai/bundesliga-2026-27/custom-match" &&
                    config.PromptVersion == 7),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                new Prediction(2, 1),
                new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
                ["doc-a"]));

        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                "doc-a",
                It.IsAny<DateTimeOffset>(),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("doc-a", "Alpha", 3, new DateTimeOffset(2026, 3, 10, 11, 30, 0, TimeSpan.Zero)));

        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27)).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27)).Returns(contextRepository.Object);
        firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

        var context = CreateCommandApp<ReconstructPromptCommand>(
            "reconstruct-prompt",
            firebaseServiceFactory: firebaseFactory);

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "reconstruct-prompt",
            "gpt-5",
            "--community-context",
            "test-community",
            "--home",
            "Team A",
            "--away",
            "Team B",
            "--matchday",
            "7",
            "--reasoning-effort",
            "none",
            "--max-output-tokens",
            "8000",
            "--prompt-source",
            "langfuse",
            "--langfuse-prompt-name",
            "kicktippai/bundesliga-2026-27/custom-match",
            "--langfuse-prompt-version",
            "7");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Resolved context versions:");
        await Assert.That(output).Contains("doc-a | v3");
        await Assert.That(output).Contains("System prompt:");
        await Assert.That(output).Contains("Alpha");
    }

    [Test]
    public async Task Running_command_with_unknown_match_returns_error()
    {
        var predictionRepository = new Mock<IPredictionRepository>();
        predictionRepository
            .Setup(repository => repository.GetStoredMatchAsync(
                "Team A",
                "Team B",
                7,
                It.Is<PredictionModelConfig>(config =>
                    config.Model == "gpt-5" &&
                    config.ReasoningEffort == null &&
                    config.MaxOutputTokenCount == 10000 &&
                    config.PromptName == "kicktippai/bundesliga-2026-27/predict-one-match" &&
                    config.PromptVersion == CompetitionResolver.BundesligaMatchPromptVersion),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Match?)null);

        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27)).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27)).Returns(new Mock<IContextRepository>().Object);
        firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

        var context = CreateCommandApp<ReconstructPromptCommand>(
            "reconstruct-prompt",
            firebaseServiceFactory: firebaseFactory);

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "reconstruct-prompt",
            "gpt-5",
            "--community-context",
            "test-community",
            "--home",
            "Team A",
            "--away",
            "Team B",
            "--matchday",
            "7");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Match not found on matchday 7");
    }

    [Test]
    public async Task Explicit_competition_selects_its_repository()
    {
        var predictionRepository = new Mock<IPredictionRepository>();
        predictionRepository
            .Setup(repository => repository.GetStoredMatchAsync(
                "Team A",
                "Team B",
                7,
                It.Is<PredictionModelConfig>(config => config.IdentityKey == "gpt-5"),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Match?)null);

        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.FifaWorldCup2026)).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository(CompetitionIds.FifaWorldCup2026)).Returns(new Mock<IContextRepository>().Object);
        firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

        var context = CreateCommandApp<ReconstructPromptCommand>(
            "reconstruct-prompt",
            firebaseServiceFactory: firebaseFactory);

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "reconstruct-prompt",
            "gpt-5",
            "--community-context", "test-community",
            "--competition", "fifa-world-cup-2026",
            "--prompt-source", "local",
            "--home", "Team A",
            "--away", "Team B",
            "--matchday", "7");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Match not found on matchday 7");
        firebaseFactory.Verify(factory => factory.CreatePredictionRepository(CompetitionIds.FifaWorldCup2026), Times.Once);
    }

    [Test]
    public async Task Explicit_timestamp_reconstruction_rejects_bundesliga_reserved_context()
    {
        var match = new Match("FC Bayern München", "Borussia Dortmund", NodaTime.Instant.FromUtc(2025, 10, 30, 15, 30).InUtc(), 7);
        var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
        predictionRepository
            .Setup(repository => repository.GetStoredMatchAsync("FC Bayern München", "Borussia Dortmund", 7, (PredictionModelConfig?)null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        var exactTimestamp = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1));
        var contextRepository = new Mock<IContextRepository>();
        foreach (var documentName in MatchContextDocumentCatalog.ForMatch("FC Bayern München", "Borussia Dortmund", "test-community", CompetitionIds.Bundesliga2026_27).RequiredDocumentNames)
        {
            contextRepository
                .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                    documentName,
                    exactTimestamp,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContextDocument(documentName, $"content:{documentName}", 1, exactTimestamp.AddMinutes(-5)));
        }

        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository(CompetitionIds.Bundesliga2026_27)).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27)).Returns(contextRepository.Object);
        firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

        var context = CreateCommandApp<ReconstructPromptCommand>(
            "reconstruct-prompt",
            firebaseServiceFactory: firebaseFactory);

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "reconstruct-prompt",
            "gpt-5",
            "--community-context",
            "test-community",
            "--home",
            "FC Bayern München",
            "--away",
            "Borussia Dortmund",
            "--matchday",
            "7",
            "--evaluation-time",
            "\"2026-03-15T12:00:00 Europe/Berlin (+01)\"");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Timestamp-only reconstruction cannot resolve Bundesliga roster")
            .And.Contains("Elo context");
    }
}
