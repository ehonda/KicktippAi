using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> history document processing.
/// </summary>
public class CollectContextKicktippCommand_HistoryProcessing_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_adds_data_collected_at_for_recent_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-fcb.csv",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_adds_data_collected_at_for_home_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("home-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "home-history-fcb.csv",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_adds_data_collected_at_for_away_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("away-history-bvb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Dortmund,Mainz,3-0,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "away-history-bvb.csv",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_does_not_modify_non_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "bundesliga-standings.csv",
                It.Is<string>(content => !content.Contains("Data_Collected_At")),
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_fetches_previous_version_for_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.GetLatestContextDocumentAsync("recent-history-fcb.csv", "test-community", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_does_not_fetch_previous_version_for_non_history_documents()
    {
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.GetLatestContextDocumentAsync("bundesliga-standings.csv", "test-community", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_preserves_existing_dates_from_previous_version()
    {
        var previousContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var previousDoc = CreateContextDocument(
            documentName: "recent-history-fcb.csv",
            content: previousContent);
        var previousDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = previousDoc
        };
        var docs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,\nBundesliga,Bayern,Mainz,3-0,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs, previousContextDocuments: previousDocs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-fcb.csv",
                It.Is<string>(content =>
                    content.Contains("Data_Collected_At") &&
                    content.Contains("2025-01-01")), // Preserved from previous
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_handles_case_insensitive_history_prefix()
    {
        var docs = new List<DocumentContext>
        {
            new("Recent-History-FCB.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "Recent-History-FCB.csv",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
