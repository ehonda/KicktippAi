using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using TUnit.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ExportExperimentDatasetTests;

public class ExportExperimentDatasetCommand_Tests : ExportExperimentDatasetCommandTests_Base
{
    [Test]
    public async Task Exporting_dataset_filters_incomplete_matches_and_sorts_items()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-experiment-dataset.json");
        var (app, console, repository) = CreateCommandApp(new Dictionary<int, IReadOnlyList<PersistedMatchOutcome>>
        {
            [1] =
            [
                CreateOutcome(1, "Borussia Dortmund", "Team B", "2"),
                CreateOutcome(1, "FC Augsburg", "Team A", "1"),
                CreateOutcome(1, "Pending", "Team C", "3", MatchOutcomeAvailability.Pending, null, null)
            ],
            [2] = [CreateOutcome(2, "RB Leipzig", "Mainz", "4", homeGoals: 3, awayGoals: 0)]
        });

        try
        {
            var (exitCode, output) = await RunAsync(app, console, outputPath, "--matchdays", "2,1,1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Exported items:");
            repository.Verify(r => r.GetMatchdayOutcomesAsync(1, CommunityContext, It.IsAny<CancellationToken>()), Times.Once);
            repository.Verify(r => r.GetMatchdayOutcomesAsync(2, CommunityContext, It.IsAny<CancellationToken>()), Times.Once);

            var json = await File.ReadAllTextAsync(outputPath);
            using var document = JsonDocument.Parse(json);
            var items = document.RootElement.GetProperty("items");

            await Assert.That(items.GetArrayLength()).IsEqualTo(3);
            await Assert.That(items[0].GetProperty("id").GetString()).Contains("ts1");
            await Assert.That(items[1].GetProperty("id").GetString()).Contains("ts2");
            await Assert.That(items[2].GetProperty("id").GetString()).Contains("ts4");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Test]
    public async Task Exporting_dataset_with_default_matchdays_queries_full_bundesliga_range()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-experiment-dataset-default.json");
        var (app, console, repository) = CreateCommandApp(new Dictionary<int, IReadOnlyList<PersistedMatchOutcome>>
        {
            [34] = [CreateOutcome(34, "FC Bayern München", "Borussia Dortmund", "34")]
        });

        try
        {
            var (exitCode, output) = await RunAsync(app, console, outputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("First item id:");
            repository.Verify(r => r.GetMatchdayOutcomesAsync(1, CommunityContext, It.IsAny<CancellationToken>()), Times.Once);
            repository.Verify(r => r.GetMatchdayOutcomesAsync(34, CommunityContext, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Test]
    public async Task Exporting_dataset_returns_error_when_repository_throws()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-experiment-dataset-error.json");
        var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>();
        matchOutcomeRepository.Setup(repository => repository.GetMatchdayOutcomesAsync(
                It.IsAny<int>(),
                CommunityContext,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dataset boom"));

        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(matchOutcomeRepository: matchOutcomeRepository);
        var (app, console) = CreateCommandApp<ExportExperimentDatasetCommand>(
            "export-dataset",
            firebaseServiceFactory: firebaseFactory);

        var (exitCode, output) = await RunAsync(app, console, outputPath, "--matchdays", "1");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("dataset boom");
        await Assert.That(File.Exists(outputPath)).IsFalse();
    }
}
