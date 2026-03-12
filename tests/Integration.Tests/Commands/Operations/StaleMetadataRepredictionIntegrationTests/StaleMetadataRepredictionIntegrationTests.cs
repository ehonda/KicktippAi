using EHonda.KicktippAi.Core;
using Integration.Tests.Infrastructure;
using Moq;
using static Integration.Tests.Infrastructure.OrchestratorIntegrationTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Integration.Tests.Commands.Operations.StaleMetadataRepredictionIntegrationTests;

public class StaleMetadataRepredictionIntegrationTests : StaleMetadataRepredictionIntegrationTests_Base
{
    public StaleMetadataRepredictionIntegrationTests(TestUtilities.FirestoreFixture fixture)
        : base(fixture)
    {
    }

    [Test]
    public async Task Stale_metadata_regression_does_not_create_unnecessary_matchday_reprediction()
    {
        var match = CreateRegressionMatch();
        var initialPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var latestPrediction = CreatePrediction(homeGoals: 1, awayGoals: 3);
        var initialPredictionCreatedAt = new DateTimeOffset(2026, 3, 9, 1, 38, 45, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2026, 3, 11, 1, 37, 59, TimeSpan.Zero);
        var latestPredictionCreatedAt = new DateTimeOffset(2026, 3, 12, 1, 0, 0, TimeSpan.Zero);
        var initialDocuments = new[]
        {
            "recent-history-fcb.csv",
            "away-history-fcb.csv"
        };
        var latestDocuments = new[]
        {
            "recent-history-fcb.csv",
            "away-history-fcb.csv",
            "recent-history-b04.csv"
        };

        await FirestoreSeedData.SeedMatchPredictionAsync(
            Fixture.Db,
            match,
            initialPrediction,
            Model,
            Community,
            initialDocuments,
            repredictionIndex: 0,
            createdAt: initialPredictionCreatedAt,
            updatedAt: initialPredictionCreatedAt);

        await FirestoreSeedData.SeedMatchPredictionAsync(
            Fixture.Db,
            match,
            latestPrediction,
            Model,
            Community,
            latestDocuments,
            repredictionIndex: 1,
            createdAt: latestPredictionCreatedAt,
            updatedAt: latestPredictionCreatedAt);

        foreach (var documentName in latestDocuments)
        {
            await FirestoreSeedData.SeedContextDocumentAsync(
                Fixture.Db,
                documentName,
                Community,
                version: 0,
                createdAt: contextUpdatedAt);
        }

        var repository = new TestFirebaseServiceFactory(Fixture.Db).CreatePredictionRepository();
        var latestMetadata = await repository.GetPredictionMetadataAsync(match, Model, Community);

        await Assert.That(latestMetadata).IsNotNull();
        await Assert.That(latestMetadata!.Prediction).IsEqualTo(latestPrediction);
        await Assert.That(latestMetadata.ContextDocumentNames).IsEquivalentTo(latestDocuments);

        var kicktippClient = CreateKicktippClientMock(match, latestPrediction);
        var openAiFactory = CreateOpenAiFactoryThatFailsOnPrediction(out var predictionService);
        var contextProviderFactory = CreateContextProviderFactory();
        var verifyContext = CreateIntegrationContext(kicktippClient, openAiFactory, contextProviderFactory);

        var (verifyExitCode, verifyOutput) = await RunCommandAsync(
            verifyContext.App,
            verifyContext.Console,
            "verify-matchday",
            Model,
            "-c",
            Community,
            "--check-outdated");

        await Assert.That(verifyExitCode).IsEqualTo(0);
        await Assert.That(verifyOutput).Contains("All predictions match - verification successful");
        await Assert.That(verifyOutput).DoesNotContain("Status: Outdated");
        await Assert.That(verifyOutput).DoesNotContain("context updated after prediction");

        var matchdayContext = CreateIntegrationContext(kicktippClient, openAiFactory, contextProviderFactory);

        var (matchdayExitCode, matchdayOutput) = await RunCommandAsync(
            matchdayContext.App,
            matchdayContext.Console,
            "matchday",
            Model,
            "-c",
            Community,
            "--repredict",
            "--dry-run");

        await Assert.That(matchdayExitCode).IsEqualTo(0);
        await Assert.That(matchdayOutput).Contains("Skipped reprediction");
        await Assert.That(matchdayOutput).Contains("up-to-date");
        await Assert.That(matchdayOutput).Contains("Latest prediction:").And.Contains("1:3").And.Contains("reprediction 1");

        predictionService.Verify(
            service => service.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var repredictionIndex = await repository.GetMatchRepredictionIndexAsync(match, Model, Community);
        await Assert.That(repredictionIndex).IsEqualTo(1);

        var storedPredictions = await FirestoreSeedData.GetMatchPredictionsAsync(Fixture.Db, match, Model, Community);
        await Assert.That(storedPredictions.Count).IsEqualTo(2);
        await Assert.That(storedPredictions.Select(prediction => prediction.RepredictionIndex)).IsEquivalentTo([0, 1]);
    }
}
