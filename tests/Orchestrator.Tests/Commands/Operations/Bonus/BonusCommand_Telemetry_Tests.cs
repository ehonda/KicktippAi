using System.Diagnostics;
using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Bonus.BonusCommand"/> telemetry
/// (activity naming and Langfuse environment tagging).
/// </summary>
public class BonusCommand_Telemetry_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Bundesliga_prediction_telemetry_carries_exact_resolved_context_provenance()
    {
        PredictionTelemetryMetadata? captured = null;
        var predictionService = CreateMockPredictionService();
        predictionService.Setup(service => service.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .Callback((BonusQuestion _, IEnumerable<DocumentContext> _, PredictionTelemetryMetadata? metadata, CancellationToken _) =>
                captured = metadata)
            .ReturnsAsync(new BonusPrediction(["bayern"]));
        var question = CreateLeagueWinnerBonusQuestion();
        var ctx = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { question },
            existingBonusPrediction: (BonusPrediction?)null,
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "bonus", "gpt-4o", "-c", "test-community");

        var expected = CreateCanonicalBundesligaResolvedBonusContext(question, "test-community").Manifest;
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.ContextDocumentNames!.SequenceEqual(expected.Documents.Select(document => document.Name))).IsTrue();
        await Assert.That(captured.RosterPublicationSnapshotId).IsEqualTo(expected.RosterPublicationSnapshotId);
        await Assert.That(captured.ClubEloPublicationSnapshotId).IsEqualTo(expected.ClubEloPublicationSnapshotId);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Root_activity_is_named_bonus()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", "gpt-4o", "-c", "test-community");

        var rootActivity = capturedActivities.LastOrDefault(a => a.OperationName == "bonus");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.OperationName).IsEqualTo("bonus");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Production_community_sets_environment_to_production()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", "gpt-4o", "-c", "pes-squad");

        var rootActivity = capturedActivities.LastOrDefault(a => a.OperationName == "bonus");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Rabetrabauken2026_sets_environment_to_production()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", "gpt-4o", "-c", "rabetrabauken2026");

        var rootActivity = capturedActivities.LastOrDefault(a => a.OperationName == "bonus");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Non_production_community_sets_environment_to_development()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", "gpt-4o", "-c", "ehonda-test-buli");

        var rootActivity = capturedActivities.LastOrDefault(a => a.OperationName == "bonus");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }
}
