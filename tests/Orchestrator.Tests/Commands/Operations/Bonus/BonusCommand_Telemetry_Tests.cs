using System.Collections.Concurrent;
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

        var expected = CreateCanonicalBundesligaResolvedBonusContext(question, "test-community");
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.ContextDocumentNames!.SequenceEqual(expected.Documents.Select(document => document.Name))).IsTrue();
        await Assert.That(captured.RosterPublicationSnapshotId).IsEqualTo(expected.Manifest.RosterPublicationSnapshotId);
        await Assert.That(captured.ClubEloPublicationSnapshotId).IsEqualTo(expected.Manifest.ClubEloPublicationSnapshotId);
        await Assert.That(captured.BonusContextCategory).IsEqualTo("Champion");
        await Assert.That(captured.BonusContextSelectedDocuments!.SequenceEqual(expected.Selection.SelectedDocumentNames)).IsTrue();
        await Assert.That(captured.BonusContextExcludedDocuments!.First()).IsEqualTo("team-rosters=ProhibitedAggregate");
        await Assert.That(captured.BonusContextEstimatedUtf8Bytes).IsEqualTo(expected.Selection.EstimatedUtf8Bytes);
        await Assert.That(captured.BonusContextEstimatedTokens).IsEqualTo(expected.Selection.EstimatedTokens);
        await Assert.That(captured.BonusContextDocumentBudget).IsEqualTo(20);
        await Assert.That(captured.BonusContextEstimatedTokenBudget).IsEqualTo(32_000);
    }

    [Test]
    public async Task Bundesliga_prediction_metadata_is_rebuilt_per_question_without_category_or_roster_leakage()
    {
        var captured = new List<PredictionTelemetryMetadata>();
        var predictionService = CreateMockPredictionService();
        predictionService.Setup(service => service.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .Callback((BonusQuestion _, IEnumerable<DocumentContext> _, PredictionTelemetryMetadata? metadata, CancellationToken _) =>
                captured.Add(metadata!))
            .ReturnsAsync(new BonusPrediction(["bayern"]));
        var topScorer = new BonusQuestion(
            "Who will be the top scorer?",
            default,
            [new BonusQuestionOption("bayern", "FC Bayern München")],
            1,
            "q1");
        var unknown = new BonusQuestion(
            "How many goals will be scored?",
            default,
            [new BonusQuestionOption("bayern", "More")],
            1,
            "q2");
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { topScorer, unknown },
            existingBonusPrediction: (BonusPrediction?)null,
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, _) = await RunCommandAsync(
            context.App,
            context.Console,
            "bonus",
            "gpt-4o",
            "-c",
            "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(captured.Count).IsEqualTo(2);
        await Assert.That(captured[0].BonusContextCategory).IsEqualTo("TopScorer");
        await Assert.That(captured[0].BonusContextSelectedDocuments!).Contains("roster-fcb");
        await Assert.That(captured[1].BonusContextCategory).IsEqualTo("Unknown");
        await Assert.That(captured[1].BonusContextSelectedDocuments!).DoesNotContain("roster-fcb");
        await Assert.That(captured[1].BonusContextExcludedDocuments!).Contains("roster-fcb=CategoryDoesNotUseRoster");
        await Assert.That(captured[1].BonusContextExcludedDocuments!).DoesNotContain("roster-fcb=NoExactIdentity");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Root_activity_is_named_bonus()
    {
        const string community = "test-community";
        const string model = "telemetry-root-model";
        var capturedActivities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", model, "-c", community);

        var rootActivity = FindBonusActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.OperationName).IsEqualTo("bonus");
    }

    [Test]
    [Arguments("pes-squad")]
    [Arguments("schadensfresse")]
    [NotInParallel("Telemetry")]
    public async Task Production_community_sets_environment_to_production(string community)
    {
        const string model = "telemetry-production-model";
        var capturedActivities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", model, "-c", community);

        var rootActivity = FindBonusActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Arena_Luna_Bundesliga_validation_path_keeps_production_environment_and_exact_identity()
    {
        const string community = "ehonda-ai-arena";
        const string model = "gpt-5.6-luna";
        var capturedActivities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        var (exitCode, _) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "bonus",
            model,
            "-c", community,
            "--community-context", community,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--reasoning-effort", "none",
            "--max-output-tokens", "10000",
            "--prompt-source", "langfuse",
            "--langfuse-prompt-name", "kicktippai/bundesliga-2026-27/predict-bonus",
            "--langfuse-prompt-label", "production",
            "--langfuse-prompt-version", "1",
            "--bonus-context-document-budget", "20",
            "--bonus-context-token-budget", "32000");

        var rootActivity = FindBonusActivity(capturedActivities, community, model);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.community") as string).IsEqualTo("ehonda-ai-arena");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.competition") as string).IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.model") as string).IsEqualTo("gpt-5.6-luna");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.reasoningEffort") as string).IsEqualTo("none");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.maxOutputTokens") as string).IsEqualTo("10000");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.promptName") as string).IsEqualTo("kicktippai/bundesliga-2026-27/predict-bonus");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.promptVersion") as string).IsEqualTo("1");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Rabetrabauken2026_sets_environment_to_production()
    {
        const string community = "rabetrabauken2026";
        const string model = "telemetry-rabetrabauken-model";
        var capturedActivities = new ConcurrentQueue<Activity>();
        var ctx = CreateBonusCommandApp();

        Activity? capturedTarget;
        using (var listener = CreateActivityListener(capturedActivities))
        {
            await RunCommandAsync(ctx.App, ctx.Console, "bonus", model, "-c", community);
            capturedTarget = FindBonusActivity(capturedActivities, community, model);
        }

        await Assert.That(capturedTarget).IsNotNull();

        using var foreignActivity = new Activity("bonus");
        foreignActivity.SetTag("langfuse.trace.metadata.community", community);
        foreignActivity.SetTag("langfuse.trace.metadata.model", "foreign-telemetry-model");
        foreignActivity.SetTag("langfuse.environment", "development");
        var stableActivities = new[] { capturedTarget!, foreignActivity };

        var rootActivity = FindBonusActivity(stableActivities, community, model);
        await Assert.That(stableActivities.Last().GetTagItem("langfuse.environment") as string)
            .IsEqualTo("development");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("production");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Non_production_community_sets_environment_to_development()
    {
        const string community = "ehonda-test-buli";
        const string model = "telemetry-development-model";
        var capturedActivities = new ConcurrentQueue<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateBonusCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "bonus", model, "-c", community);

        var rootActivity = FindBonusActivity(capturedActivities, community, model);
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }

    private static Activity? FindBonusActivity(
        IEnumerable<Activity> activities,
        string community,
        string model) =>
        activities.SingleOrDefault(activity =>
            activity.OperationName == "bonus"
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.community") as string,
                community,
                StringComparison.Ordinal)
            && string.Equals(
                activity.GetTagItem("langfuse.trace.metadata.model") as string,
                model,
                StringComparison.Ordinal));
}
