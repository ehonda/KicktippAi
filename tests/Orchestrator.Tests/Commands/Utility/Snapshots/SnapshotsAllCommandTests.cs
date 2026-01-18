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
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility.Snapshots;

/// <summary>
/// Tests for <see cref="SnapshotsAllCommand"/>.
/// Tests modify KICKTIPP_FIXTURE_KEY environment variable, so they must not run in parallel.
/// </summary>
[NotInParallel("KICKTIPP_FIXTURE_KEY")]
public class SnapshotsAllCommandTests : TempDirectoryWithEncryptionKeyTestBase
{
    protected override string TestDirectoryName => "SnapshotsAllTests";

    private (CommandApp App, TestConsole Console, Mock<ISnapshotClient> SnapshotClient) CreateAllCommandApp(
        Option<Mock<ISnapshotClient>> snapshotClient = default)
    {
        var testConsole = new TestConsole();
        var fakeLogger = new FakeLogger<SnapshotsAllCommand>();

        var mockSnapshotClient = snapshotClient.Or(() => CreateMockSnapshotClient());
        var mockFactory = CreateMockKicktippClientFactoryWithSnapshotClient(mockSnapshotClient);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton<IKicktippClientFactory>(mockFactory.Object);
        services.AddSingleton<ILogger<SnapshotsAllCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<SnapshotsAllCommand>("snapshots-all");
        });

        return (app, testConsole, mockSnapshotClient);
    }

    [Test]
    public async Task Running_all_fetches_and_encrypts_snapshots()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>",
            standingsPageContent: "<html>Tabellen</html>",
            tippabgabePageContent: "<html>Tippabgabe</html>",
            bonusPageContent: "<html>Bonus</html>");

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Fetching and encrypting snapshots");
        await Assert.That(output).Contains("Step 1: Fetching snapshots");
        await Assert.That(output).Contains("Step 2: Encrypting snapshots");
        await Assert.That(output).Contains("Done!");
        await Assert.That(output).Contains("Fetched 4, encrypted 4 snapshot(s)");

        // Verify encrypted files exist in community subdirectory
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "login.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "tabellen.html.enc"))).IsTrue();
    }

    [Test]
    public async Task Running_all_with_missing_community_returns_error()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var (app, console, _) = CreateAllCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", ""
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Community is required");
    }

    [Test]
    public async Task Running_all_without_encryption_key_returns_error()
    {
        // Arrange
        SetEncryptionKey(null);

        var (app, console, _) = CreateAllCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community"
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("KICKTIPP_FIXTURE_KEY environment variable is not set");
    }

    [Test]
    public async Task Running_all_with_zero_snapshots_fetched_exits_early()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        // All fetch operations return null (failure)
        var mockSnapshotClient = CreateMockSnapshotClient();

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No snapshots fetched, nothing to encrypt");
        await Assert.That(output).DoesNotContain("Step 2: Encrypting");
    }

    [Test]
    public async Task Running_all_deletes_originals_by_default()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Deleted 1 original HTML file(s)");

        // Verify original was deleted
        await Assert.That(File.Exists(Path.Combine(snapshotsDir, "login.html"))).IsFalse();
    }

    [Test]
    public async Task Running_all_with_keep_originals_preserves_files()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir,
            "--keep-originals"
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Deleted");

        // Verify original still exists
        await Assert.That(File.Exists(Path.Combine(snapshotsDir, "login.html"))).IsTrue();

        // Verify encrypted file also exists
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "login.html.enc"))).IsTrue();
    }

    [Test]
    public async Task Running_all_creates_directories_if_they_do_not_exist()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "nested", "snapshots");
        var outputDir = Path.Combine(TestDirectory, "nested", "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(Directory.Exists(snapshotsDir)).IsTrue();

        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(Directory.Exists(communityOutputDir)).IsTrue();
    }

    [Test]
    public async Task Running_all_shows_output_path_in_completion_message()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>");

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "test-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("test-community");
        // Verify output mentions the encrypted files are saved (path may be wrapped by console)
        await Assert.That(output).Contains("Encrypted files saved to:");
    }

    [Test]
    public async Task Running_all_with_multiple_spielinfo_pages_encrypts_all()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient(
            loginPageContent: "<html>Login</html>",
            spielinfoPages: new List<(string, string)>
            {
                ("spielinfo-01", "<html>Match 1</html>"),
                ("spielinfo-02", "<html>Match 2</html>"),
                ("spielinfo-03", "<html>Match 3</html>")
            });

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir,
            "--keep-originals"
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Fetched 4, encrypted 4"); // login + 3 spielinfo

        // Verify all encrypted files exist
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "login.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "spielinfo-01.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "spielinfo-02.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "spielinfo-03.html.enc"))).IsTrue();
    }

    [Test]
    public async Task Running_all_handles_fetch_exception_and_returns_error()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var snapshotsDir = Path.Combine(TestDirectory, "snapshots");
        var outputDir = Path.Combine(TestDirectory, "output");

        var mockSnapshotClient = CreateMockSnapshotClient();
        mockSnapshotClient.Setup(c => c.FetchLoginPageAsync())
            .ThrowsAsync(new InvalidOperationException("Network failure"));

        var (app, console, _) = CreateAllCommandApp(mockSnapshotClient);

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-all",
            "-c", "my-community",
            "--snapshots-dir", snapshotsDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Network failure");
    }
}
