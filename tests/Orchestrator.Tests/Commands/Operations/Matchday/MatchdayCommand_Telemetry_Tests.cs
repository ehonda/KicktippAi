using System.Diagnostics;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> telemetry
/// (activity naming and Langfuse environment tagging).
/// </summary>
public class MatchdayCommand_Telemetry_Tests : MatchdayCommandTests_Base
{
    [Test]
    [NotInParallel("Telemetry")]
    public async Task Root_activity_is_named_matchday()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var ctx = CreateMatchdayCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        var rootActivity = capturedActivities.FirstOrDefault(a => a.Parent == null && a.OperationName == "matchday");
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

        var rootActivity = capturedActivities.FirstOrDefault(a => a.Parent == null && a.OperationName == "matchday");
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

        var rootActivity = capturedActivities.FirstOrDefault(a => a.Parent == null && a.OperationName == "matchday");
        await Assert.That(rootActivity).IsNotNull();
        await Assert.That(rootActivity!.GetTagItem("langfuse.environment") as string).IsEqualTo("development");
    }
}
