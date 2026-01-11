using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> settings validation.
/// </summary>
public class CollectContextKicktippCommand_Settings_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_without_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }

    [Test]
    public async Task Running_command_with_empty_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }

    [Test]
    public async Task Running_command_with_whitespace_community_context_returns_error()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "   ");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Community context is required");
    }
}
