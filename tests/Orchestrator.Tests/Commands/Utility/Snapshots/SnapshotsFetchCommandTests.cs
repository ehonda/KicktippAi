using EHonda.Optional.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.Snapshots;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Tests.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static EHonda.Optional.Core.NullableOption;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility.Snapshots;

/// <summary>
/// Tests for <see cref="SnapshotsFetchCommand"/>.
/// </summary>
public class SnapshotsFetchCommandTests : TempDirectoryTestBase
{
    protected override string TestDirectoryName => "SnapshotsFetchTests";

    private (CommandApp App, TestConsole Console, Mock<ISnapshotClient> SnapshotClient) CreateFetchCommandApp(
        Option<Mock<ISnapshotClient>> snapshotClient = default)
    {
        var testConsole = new TestConsole();
        var fakeLogger = new FakeLogger<SnapshotsFetchCommand>();

        var mockSnapshotClient = snapshotClient.Or(() => CreateMockSnapshotClient());
        var mockFactory = CreateMockKicktippClientFactoryWithSnapshotClient(mockSnapshotClient);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton<IKicktippClientFactory>(mockFactory.Object);
        services.AddSingleton<ILogger<SnapshotsFetchCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<SnapshotsFetchCommand>("snapshots-fetch");
        });

        return (app, testConsole, mockSnapshotClient);
    }

    [Test]
    public async Task Fetching_snapshots_saves_all_pages_to_output_directory()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>",
            standingsPageContent: "<html>Tabellen</html>",
            tippabgabePageContent: "<html>Tippabgabe</html>",
            bonusPageContent: "<html>Bonus</html>",
            spielinfoPages: new List<(string, string)>
            {
                ("spielinfo-01", "<html>Spielinfo 1</html>"),
                ("spielinfo-02", "<html>Spielinfo 2</html>")
            },
            spielinfoHomeAwayPages: new List<(string, string)>
            {
                ("spielinfo-01-homeaway", "<html>HomeAway 1</html>")
            },
            spielinfoH2hPages: new List<(string, string)>
            {
                ("spielinfo-01-h2h", "<html>H2H 1</html>")
            });

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Fetching snapshots");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("Done!");
        await Assert.That(output).Contains("Saved 8 snapshot(s)");

        // Verify all files exist
        await Assert.That(File.Exists(Path.Combine(outputDir, "login.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "tabellen.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "tippabgabe.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "tippabgabe-bonus.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "spielinfo-01.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "spielinfo-02.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "spielinfo-01-homeaway.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "spielinfo-01-h2h.html"))).IsTrue();
    }

    [Test]
    public async Task Fetching_with_missing_community_returns_error()
    {
        // Arrange
        var (app, console, _) = CreateFetchCommandApp();

        // Act - Note: We pass an empty string since -c is required but can be empty
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "",
            "-o", TestDirectory
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Community is required");
    }

    [Test]
    public async Task Fetching_with_failed_page_shows_failure_message()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>",
            standingsPageContent: Some<string>(null!), // Explicit null - failure
            tippabgabePageContent: "<html>Tippabgabe</html>",
            bonusPageContent: Some<string>(null!)); // Explicit null - failure

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0); // Command still succeeds with partial results
        await Assert.That(output).Contains("Saved login.html");
        await Assert.That(output).Contains("Failed to fetch tabellen");
        await Assert.That(output).Contains("Saved tippabgabe.html");
        await Assert.That(output).Contains("Failed to fetch tippabgabe-bonus");
        await Assert.That(output).Contains("Saved 2 snapshot(s)"); // Only 2 succeeded
    }

    [Test]
    public async Task Fetching_with_no_spielinfo_pages_shows_warning()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>",
            standingsPageContent: "<html>Tabellen</html>",
            tippabgabePageContent: "<html>Tippabgabe</html>",
            bonusPageContent: "<html>Bonus</html>",
            spielinfoPages: new List<(string, string)>(),
            spielinfoHomeAwayPages: new List<(string, string)>(),
            spielinfoH2hPages: new List<(string, string)>());

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No spielinfo pages found");
        await Assert.That(output).Contains("No spielinfo home/away pages found");
        await Assert.That(output).Contains("No spielinfo head-to-head pages found");
        await Assert.That(output).Contains("Saved 4 snapshot(s)"); // Only the 4 basic pages
    }

    [Test]
    public async Task Fetching_creates_output_directory_if_it_does_not_exist()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "nested", "output", "path");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(Directory.Exists(outputDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outputDir, "login.html"))).IsTrue();
    }

    [Test]
    public async Task Fetching_shows_next_step_hint()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Next step: Run 'snapshots encrypt'");
    }

    [Test]
    public async Task Fetching_to_potentially_unignored_directory_shows_warning()
    {
        // Arrange
        // Use a directory name that doesn't match typical ignore patterns
        var outputDir = Path.Combine(TestDirectory, "my-data");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("may not be gitignored");
    }

    [Test]
    public async Task Fetching_to_snapshots_directory_does_not_show_gitignore_warning()
    {
        // Arrange
        // Use a directory name that matches typical ignore patterns
        var outputDir = Path.Combine(TestDirectory, "kicktipp-snapshots");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("may not be gitignored");
    }

    [Test]
    public async Task Fetching_saves_content_to_files_correctly()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var expectedContent = "<html><head><title>Test</title></head><body>Content</body></html>";
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: expectedContent);

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);

        // Assert
        var savedContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "login.html"));
        await Assert.That(savedContent).IsEqualTo(expectedContent);
    }

    [Test]
    public async Task Fetching_calls_snapshot_client_with_correct_community()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, client) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        await app.RunAsync([
            "snapshots-fetch",
            "-c", "test-community-123",
            "-o", outputDir
        ]);

        // Assert
        client.Verify(c => c.FetchStandingsPageAsync("test-community-123"), Times.Once);
        client.Verify(c => c.FetchTippabgabePageAsync("test-community-123"), Times.Once);
        client.Verify(c => c.FetchBonusPageAsync("test-community-123"), Times.Once);
        client.Verify(c => c.FetchAllSpielinfoAsync("test-community-123"), Times.Once);
        client.Verify(c => c.FetchAllSpielinfoHomeAwayAsync("test-community-123"), Times.Once);
        client.Verify(c => c.FetchAllSpielinfoHeadToHeadAsync("test-community-123"), Times.Once);
    }

    [Test]
    public async Task Fetching_login_page_is_not_community_specific()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, client) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        await app.RunAsync([
            "snapshots-fetch",
            "-c", "any-community",
            "-o", outputDir
        ]);

        // Assert - login page fetch doesn't take community parameter
        client.Verify(c => c.FetchLoginPageAsync(), Times.Once);
    }

    [Test]
    public async Task Fetching_handles_exception_and_returns_error()
    {
        // Arrange
        var outputDir = Path.Combine(TestDirectory, "output");
        var mockSnapshotClient = CreateMockSnapshotClient();
        mockSnapshotClient.Setup(c => c.FetchLoginPageAsync())
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var (app, console, _) = CreateFetchCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-fetch",
            "-c", "my-community",
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Network error");
    }
}
