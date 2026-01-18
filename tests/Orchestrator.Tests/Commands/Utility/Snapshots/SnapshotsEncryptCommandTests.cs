using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Commands.Utility.Snapshots;
using Orchestrator.Infrastructure;
using Orchestrator.Tests.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Orchestrator.Tests.Commands.Utility.Snapshots;

/// <summary>
/// Tests for <see cref="SnapshotsEncryptCommand"/>.
/// Tests modify KICKTIPP_FIXTURE_KEY environment variable, so they must not run in parallel.
/// </summary>
[NotInParallel("KICKTIPP_FIXTURE_KEY")]
public class SnapshotsEncryptCommandTests : TempDirectoryWithEncryptionKeyTestBase
{
    protected override string TestDirectoryName => "SnapshotsEncryptTests";

    private (CommandApp App, TestConsole Console) CreateEncryptCommandApp()
    {
        var testConsole = new TestConsole();
        var fakeLogger = new FakeLogger<SnapshotsEncryptCommand>();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton<ILogger<SnapshotsEncryptCommand>>(fakeLogger);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<SnapshotsEncryptCommand>("snapshots-encrypt");
        });

        return (app, testConsole);
    }

    private void CreateHtmlFile(string directory, string fileName, string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    [Test]
    public async Task Encrypting_html_files_creates_encrypted_files_in_output_directory()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var inputDir = Path.Combine(TestDirectory, "input");
        var outputDir = Path.Combine(TestDirectory, "output");
        CreateHtmlFile(inputDir, "test1.html", "<html>Test 1</html>");
        CreateHtmlFile(inputDir, "test2.html", "<html>Test 2</html>");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Encrypting snapshots");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("Done!");
        await Assert.That(output).Contains("Encrypted 2 file(s)");

        // Verify encrypted files exist in community subdirectory
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "test1.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "test2.html.enc"))).IsTrue();

        // Verify original files still exist (no --delete-originals)
        await Assert.That(File.Exists(Path.Combine(inputDir, "test1.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(inputDir, "test2.html"))).IsTrue();
    }

    [Test]
    public async Task Encrypting_with_delete_originals_removes_source_files()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var inputDir = Path.Combine(TestDirectory, "input");
        var outputDir = Path.Combine(TestDirectory, "output");
        CreateHtmlFile(inputDir, "delete-me.html", "<html>Delete me</html>");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir,
            "--delete-originals"
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Deleted 1 original HTML file(s)");

        // Verify original file was deleted
        await Assert.That(File.Exists(Path.Combine(inputDir, "delete-me.html"))).IsFalse();

        // Verify encrypted file exists
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "delete-me.html.enc"))).IsTrue();
    }

    [Test]
    public async Task Encrypting_without_encryption_key_returns_error()
    {
        // Arrange
        SetEncryptionKey(null);

        var inputDir = Path.Combine(TestDirectory, "input");
        CreateHtmlFile(inputDir, "test.html", "<html>Test</html>");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("KICKTIPP_FIXTURE_KEY environment variable is not set");
        await Assert.That(output).Contains("Encrypt-Fixture.ps1 -GenerateKey");
    }

    [Test]
    public async Task Encrypting_with_nonexistent_input_directory_returns_error()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var nonExistentDir = Path.Combine(TestDirectory, "does-not-exist");
        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", nonExistentDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Input directory not found");
    }

    [Test]
    public async Task Encrypting_empty_directory_shows_warning()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var inputDir = Path.Combine(TestDirectory, "empty-input");
        Directory.CreateDirectory(inputDir);
        var outputDir = Path.Combine(TestDirectory, "output");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No HTML files found to encrypt");
        await Assert.That(output).Contains("Encrypted 0 file(s)");
    }

    [Test]
    public async Task Encrypting_only_encrypts_html_files()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var inputDir = Path.Combine(TestDirectory, "input");
        var outputDir = Path.Combine(TestDirectory, "output");
        CreateHtmlFile(inputDir, "page.html", "<html>HTML file</html>");
        File.WriteAllText(Path.Combine(inputDir, "readme.txt"), "Text file");
        File.WriteAllText(Path.Combine(inputDir, "data.json"), "{}");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Encrypted 1 file(s)");

        // Verify only HTML file was encrypted
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "page.html.enc"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "readme.txt.enc"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "data.json.enc"))).IsFalse();
    }

    [Test]
    public async Task Encrypted_content_is_different_from_original()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var originalContent = "<html><body>Sensitive content</body></html>";
        var inputDir = Path.Combine(TestDirectory, "input");
        var outputDir = Path.Combine(TestDirectory, "output");
        CreateHtmlFile(inputDir, "sensitive.html", originalContent);

        var (app, console) = CreateEncryptCommandApp();

        // Act
        await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir
        ]);

        // Assert
        var communityOutputDir = Path.Combine(outputDir, "my-community");
        var encryptedContent = await File.ReadAllTextAsync(Path.Combine(communityOutputDir, "sensitive.html.enc"));
        await Assert.That(encryptedContent).IsNotEqualTo(originalContent);
        await Assert.That(encryptedContent).DoesNotContain("Sensitive content");
    }

    [Test]
    public async Task Output_directory_is_created_if_it_does_not_exist()
    {
        // Arrange
        var encryptionKey = SnapshotEncryptor.GenerateKey();
        SetEncryptionKey(encryptionKey);

        var inputDir = Path.Combine(TestDirectory, "input");
        var outputDir = Path.Combine(TestDirectory, "nested", "output", "path");
        CreateHtmlFile(inputDir, "test.html", "<html>Test</html>");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", outputDir
        ]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);

        var communityOutputDir = Path.Combine(outputDir, "my-community");
        await Assert.That(Directory.Exists(communityOutputDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(communityOutputDir, "test.html.enc"))).IsTrue();
    }

    [Test]
    public async Task Encrypting_with_invalid_key_returns_error()
    {
        // Arrange - Set an invalid (too short) encryption key
        SetEncryptionKey("short-key");

        // Create a directory with a file
        var inputDir = Path.Combine(TestDirectory, "input");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "test.html"), "<html></html>");

        var (app, console) = CreateEncryptCommandApp();

        // Act
        var exitCode = await app.RunAsync([
            "snapshots-encrypt",
            "-c", "my-community",
            "-i", inputDir,
            "-o", Path.Combine(TestDirectory, "output")
        ]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
    }
}
