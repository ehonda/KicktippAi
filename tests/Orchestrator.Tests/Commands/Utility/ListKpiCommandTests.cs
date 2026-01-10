using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Utility.ListKpi;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

/// <summary>
/// Tests for the <see cref="ListKpiCommand"/>.
/// </summary>
public class ListKpiCommandTests
{
    [Test]
    public async Task Listing_kpi_documents_displays_community_context_in_header()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository();
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "my-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("my-community");
    }

    [Test]
    public async Task Listing_kpi_documents_displays_table_with_documents()
    {
        // Arrange
        var documents = new List<KpiDocument>
        {
            CreateKpiDocument(documentName: "team-data", content: "team content", description: "Team info", version: 1),
            CreateKpiDocument(documentName: "manager-data", content: "manager content", description: "Manager info", version: 2)
        };
        var mockRepo = CreateMockKpiRepository(documents: documents);
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("team-data");
        await Assert.That(output).Contains("manager-data");
        await Assert.That(output).Contains("v1");
        await Assert.That(output).Contains("v2");
        await Assert.That(output).Contains("Found 2 KPI document(s)");
    }

    [Test]
    public async Task Listing_kpi_documents_with_no_results_shows_zero_count()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository(documents: new List<KpiDocument>());
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "empty-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 0 KPI document(s)");
    }

    [Test]
    public async Task Listing_kpi_documents_truncates_long_content()
    {
        // Arrange
        var longContent = new string('x', 150);
        var documents = new List<KpiDocument>
        {
            CreateKpiDocument(content: longContent)
        };
        var mockRepo = CreateMockKpiRepository(documents: documents);
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Content should be truncated - the "..." indicates truncation
        await Assert.That(output).Contains("...");
        // Full content should not appear (150 chars won't fit without truncation)
        await Assert.That(output).DoesNotContain(longContent);
    }

    [Test]
    public async Task Listing_kpi_documents_truncates_long_description()
    {
        // Arrange
        var longDescription = new string('y', 80);
        var documents = new List<KpiDocument>
        {
            CreateKpiDocument(description: longDescription)
        };
        var mockRepo = CreateMockKpiRepository(documents: documents);
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Description should be truncated - the "..." indicates truncation
        await Assert.That(output).Contains("...");
        // Full description should not appear (80 chars won't fit without truncation)
        await Assert.That(output).DoesNotContain(longDescription);
    }

    [Test]
    public async Task Listing_kpi_documents_shows_full_content_when_short()
    {
        // Arrange
        var shortContent = "Short content";
        var documents = new List<KpiDocument>
        {
            CreateKpiDocument(content: shortContent)
        };
        var mockRepo = CreateMockKpiRepository(documents: documents);
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains(shortContent);
    }

    [Test]
    public async Task Listing_kpi_documents_replaces_newlines_and_tabs_in_content_preview()
    {
        // Arrange
        var contentWithNewlines = "Line1\nLine2\tTabbed";
        var documents = new List<KpiDocument>
        {
            CreateKpiDocument(content: contentWithNewlines)
        };
        var mockRepo = CreateMockKpiRepository(documents: documents);
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Newlines and tabs should be replaced with spaces
        await Assert.That(output).Contains("Line1 Line2 Tabbed");
    }

    [Test]
    public async Task Listing_kpi_documents_with_verbose_flag_shows_verbose_message()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository();
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Listing_kpi_documents_without_verbose_flag_does_not_show_verbose_message()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository();
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
    }

    [Test]
    public async Task Listing_kpi_documents_when_repository_throws_returns_error_exit_code()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Connection failed");
    }

    [Test]
    public async Task Listing_kpi_documents_calls_repository_with_correct_community_context()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository(communityContext: "specific-community");
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, _) = await RunCommandAsync(app, console, "list-kpi", "-c", "specific-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        mockRepo.Verify(r => r.GetAllKpiDocumentsAsync("specific-community", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Listing_kpi_documents_displays_table_headers()
    {
        // Arrange
        var mockRepo = CreateMockKpiRepository();
        var mockFactory = CreateMockFirebaseServiceFactory(mockRepo);
        var (app, console) = CreateCommandApp<ListKpiCommand>("list-kpi", firebaseServiceFactory: mockFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "list-kpi", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Document Name");
        await Assert.That(output).Contains("Version");
        await Assert.That(output).Contains("Content Preview");
        await Assert.That(output).Contains("Description");
    }
}
