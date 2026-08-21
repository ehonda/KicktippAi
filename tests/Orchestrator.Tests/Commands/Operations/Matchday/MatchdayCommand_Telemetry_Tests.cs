using System.Diagnostics;
using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>Tests matchday activity naming and Langfuse environment tagging.</summary>
public class MatchdayCommand_Telemetry_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Bundesliga_prediction_telemetry_carries_exact_resolved_context_provenance()
    {
        PredictionTelemetryMetadata? captured = null;
        var predictionService = CreateMockPredictionService();
        predictionService.Setup(service => service.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .Callback((Match _, IEnumerable<DocumentContext> _, bool _, PredictionTelemetryMetadata? metadata, CancellationToken _) =>
                captured = metadata)
            .ReturnsAsync(new Prediction(2, 1, null));
        var ctx = CreateMatchdayCommandApp(
            existingPrediction: (Prediction?)null,
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        var expected = CreateCanonicalBundesligaResolvedContextManifest(CreateBayernVsDortmundMatch());
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.ContextDocumentNames!.SequenceEqual(expected.Documents.Select(document => document.Name))).IsTrue();
        await Assert.That(captured.RosterPublicationSnapshotId).IsEqualTo(expected.RosterPublicationSnapshotId);
        await Assert.That(captured.ClubEloPublicationSnapshotId).IsEqualTo(expected.ClubEloPublicationSnapshotId);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Root_activity_is_named_matchday()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        var rootActivity = FindMatchdayActivity(capturedActivities, "test-community");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.OperationName).IsEqualTo("matchday");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Production_community_sets_environment_to_production()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "pes-squad");

        var rootActivity = FindMatchdayActivity(capturedActivities, "pes-squad");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Rabetrabauken2026_sets_environment_to_production()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "rabetrabauken2026");

        var rootActivity = FindMatchdayActivity(capturedActivities, "rabetrabauken2026");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Non_production_community_sets_environment_to_development()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "ehonda-test-buli");

        var rootActivity = FindMatchdayActivity(capturedActivities, "ehonda-test-buli");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }

    private static Activity? FindMatchdayActivity(IEnumerable<Activity> activities, string community) =>
        activities.LastOrDefault(activity =>
            activity.OperationName == "matchday"
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.community") as string,
                community,
                StringComparison.Ordinal));
}
