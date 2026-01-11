using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> dry run mode.
/// </summary>
public class CollectContextKicktippCommand_DryRun_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_with_dry_run_displays_mode_message()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode enabled - no changes will be made to database");
    }

    [Test]
    public async Task Running_command_with_dry_run_shows_would_save_message()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run - would save:");
        await Assert.That(output).Contains("bundesliga-standings.csv");
    }

    [Test]
    public async Task Running_command_with_dry_run_does_not_save_to_database()
    {
        var ctx = CreateCollectContextCommandApp();

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--dry-run");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_with_dry_run_shows_completion_message()
    {
        var docs = new List<DocumentContext>
        {
            new("doc1.csv", "content1"),
            new("doc2.csv", "content2"),
            new("doc3.csv", "content3")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed - would have processed 3 documents");
    }
}
