using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Moq;
using Orchestrator.Commands.Observability.ReconstructPrompt;
using Orchestrator.Infrastructure.Factories;
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
            .Setup(repository => repository.GetStoredMatchAsync("Team A", "Team B", 7, "gpt-5", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        predictionRepository
            .Setup(repository => repository.GetPredictionMetadataAsync(
                It.Is<Match>(candidate =>
                    candidate.HomeTeam == match.HomeTeam &&
                    candidate.AwayTeam == match.AwayTeam &&
                    candidate.Matchday == match.Matchday &&
                    candidate.StartsAt.ToInstant() == match.StartsAt.ToInstant()),
                "gpt-5",
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
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository()).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository()).Returns(contextRepository.Object);
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
            .Setup(repository => repository.GetStoredMatchAsync("Team A", "Team B", 7, "gpt-5", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Match?)null);

        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreatePredictionRepository()).Returns(predictionRepository.Object);
        firebaseFactory.Setup(factory => factory.CreateContextRepository()).Returns(new Mock<IContextRepository>().Object);
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
}
