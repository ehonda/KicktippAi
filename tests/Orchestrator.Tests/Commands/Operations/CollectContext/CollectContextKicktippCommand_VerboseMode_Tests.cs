using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> verbose mode.
/// </summary>
public class CollectContextKicktippCommand_VerboseMode_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_with_verbose_displays_mode_message()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_collected_document_names()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50"),
            new("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collected context document: bundesliga-standings.csv");
        await Assert.That(output).Contains("Collected context document: recent-history-fcb.csv");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_data_collected_at_addition_for_history_docs()
    {
        var docs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Added Data_Collected_At column to recent-history-fcb.csv");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_saved_version_numbers()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs, saveDocumentResult: 5);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved bundesliga-standings.csv as version 5");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_skipped_document_names()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var mockContextRepo = CreateMockContextRepositoryWithPreviousDocuments(
            new Dictionary<string, ContextDocument>(),
            saveResult: null); // null indicates document unchanged
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs, firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped bundesliga-standings.csv (content unchanged)");
    }

    [Test]
    public async Task Running_command_without_verbose_does_not_show_document_details()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Collected context document:");
        await Assert.That(output).DoesNotContain("as version");
    }
}
