using System.Text.Json;
using EHonda.KicktippAi.Core;
using TUnit.Core;

namespace Orchestrator.Tests.Commands.Observability.ExportExperimentItemTests;

public class ExportExperimentItemCommand_Tests : ExportExperimentItemCommandTests_Base
{
    [Test]
    public async Task Exporting_experiment_item_writes_expected_json_file()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-experiment-item.json");
        var (app, console, _, _, _) = CreateItemCommandApp(storedMatch: CreateStoredMatch());

        try
        {
            var (exitCode, output) = await RunAsync(app, console, outputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Wrote experiment item");

            var json = await File.ReadAllTextAsync(outputPath);
            using var document = JsonDocument.Parse(json);

            await Assert.That(document.RootElement.GetProperty("datasetItem").GetProperty("id").GetString())
                .StartsWith("bundesliga-2025-26__test-community__");
            await Assert.That(document.RootElement.GetProperty("runnerPayload").GetProperty("matchJson").GetString())
                .Contains("\"homeTeam\":\"FC Bayern München\"");
            await Assert.That(document.RootElement.GetProperty("datasetItem").GetProperty("expectedOutput").GetProperty("homeGoals").GetInt32())
                .IsEqualTo(2);
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
    public async Task Exporting_experiment_item_returns_error_when_stored_match_is_missing()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing-match.json");
        var (app, console, _, _, _) = CreateItemCommandApp(storedMatch: null, predictionMetadata: CreatePredictionMetadata(DateTimeOffset.UtcNow));

        var (exitCode, output) = await RunAsync(app, console, outputPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Stored match not found");
        await Assert.That(File.Exists(outputPath)).IsFalse();
    }

    [Test]
    public async Task Exporting_experiment_item_returns_error_when_outcome_is_incomplete()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-incomplete-outcome.json");
        var (app, console, _, _, _) = CreateItemCommandApp(
            storedMatch: CreateStoredMatch(),
            outcomes: [CreateOutcome(MatchOutcomeAvailability.Pending, null, null)]);

        var (exitCode, output) = await RunAsync(app, console, outputPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("does not have a completed persisted outcome");
        await Assert.That(File.Exists(outputPath)).IsFalse();
    }

    [Test]
    public async Task Exporting_experiment_item_logs_errors_from_repository_failures()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-repository-error.json");
        var (app, console, _, _, _) = CreateItemCommandApp(storedMatchException: new InvalidOperationException("boom"));

        var (exitCode, output) = await RunAsync(app, console, outputPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("boom");
        await Assert.That(File.Exists(outputPath)).IsFalse();
    }
}
