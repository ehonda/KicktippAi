using System.Diagnostics;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>Tests matchday activity naming and Langfuse environment tagging.</summary>
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
