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
        const string community = "test-community";
        const string model = "gpt-4o";
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", model, "-c", community);

        var rootActivity = FindMatchdayActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.OperationName).IsEqualTo("matchday");
    }

    [Test]
    [Arguments("pes-squad")]
    [Arguments("schadensfresse")]
    [Arguments("relaxdays-tippt")]
    [NotInParallel("Telemetry")]
    public async Task Production_community_sets_environment_to_production(string community)
    {
        const string model = "telemetry-production-model";
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", model, "-c", community);

        var capturedTarget = FindMatchdayActivity(capturedActivities, community, model);
        await Assert.That(capturedTarget).IsNotNull();

        using var foreignActivity = new Activity("matchday");
        foreignActivity.SetTag("langfuse.trace.metadata.community", community);
        foreignActivity.SetTag("langfuse.trace.metadata.model", "foreign-telemetry-model");
        foreignActivity.SetTag("langfuse.environment", "development");
        var stableActivities = new[] { capturedTarget!, foreignActivity };

        var rootActivity = FindMatchdayActivity(stableActivities, community, model);
        await Assert.That(stableActivities.Last().GetTagItem("langfuse.environment") as string)
            .IsEqualTo("development");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Arena_Luna_Bundesliga_validation_path_keeps_production_environment_and_exact_identity()
    {
        const string community = "ehonda-ai-arena";
        const string model = "gpt-5.6-luna";
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp(
            contextDocuments: CreateBayernVsDortmundContextDocuments(
                communityContext: community));

        var (exitCode, _) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            model,
            "-c", community,
            "--community-context", community,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--reasoning-effort", "none",
            "--max-output-tokens", "10000",
            "--prompt-source", "langfuse",
            "--langfuse-prompt-name", "kicktippai/bundesliga-2026-27/predict-one-match",
            "--langfuse-prompt-label", "production",
            "--langfuse-prompt-version", "3");

        var rootActivity = FindMatchdayActivity(capturedActivities, community, model);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.communityContext") as string).IsEqualTo("ehonda-ai-arena");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.competition") as string).IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.model") as string).IsEqualTo("gpt-5.6-luna");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.reasoningEffort") as string).IsEqualTo("none");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.maxOutputTokens") as string).IsEqualTo("10000");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.promptName") as string).IsEqualTo("kicktippai/bundesliga-2026-27/predict-one-match");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.promptVersion") as string).IsEqualTo("3");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Rabetrabauken2026_sets_environment_to_production()
    {
        const string community = "rabetrabauken2026";
        const string model = "gpt-4o";
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", model, "-c", community);

        var rootActivity = FindMatchdayActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Non_production_community_sets_environment_to_development()
    {
        const string community = "ehonda-test-buli";
        const string model = "gpt-4o";
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", model, "-c", community);

        var rootActivity = FindMatchdayActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }

    private static Activity? FindMatchdayActivity(
        IEnumerable<Activity> activities,
        string community,
        string model) =>
        activities.LastOrDefault(activity =>
            activity.OperationName == "matchday"
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.community") as string,
                community,
                StringComparison.Ordinal)
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.model") as string,
                model,
                StringComparison.Ordinal));
}
