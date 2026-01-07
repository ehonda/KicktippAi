using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.Snapshots;

/// <summary>
/// Command for encrypting HTML snapshots for safe committing.
/// </summary>
public class SnapshotsEncryptCommand : AsyncCommand<SnapshotsEncryptSettings>
{
    private readonly IAnsiConsole _console;

    public SnapshotsEncryptCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotsEncryptSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<SnapshotsEncryptCommand>();

        try
        {
            // Load environment to get encryption key
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var encryptionKey = Environment.GetEnvironmentVariable("KICKTIPP_FIXTURE_KEY");
            if (string.IsNullOrEmpty(encryptionKey))
            {
                _console.MarkupLine("[red]Error: KICKTIPP_FIXTURE_KEY environment variable is not set.[/]");
                _console.WriteLine();
                _console.MarkupLine("[yellow]To generate a new key:[/]");
                _console.MarkupLine("[dim]  .\\Encrypt-Fixture.ps1 -GenerateKey[/]");
                _console.WriteLine();
                _console.MarkupLine("[yellow]Then store the key in:[/]");
                _console.MarkupLine("[dim]  <repo>/../KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env[/]");
                return 1;
            }

            var inputPath = Path.GetFullPath(settings.InputDirectory);
            // Output to community-specific subdirectory
            var outputPath = Path.GetFullPath(Path.Combine(settings.OutputDirectory, settings.Community));

            if (!Directory.Exists(inputPath))
            {
                _console.MarkupLine($"[red]Error: Input directory not found: {inputPath}[/]");
                return 1;
            }

            _console.MarkupLine("[green]Encrypting snapshots...[/]");
            _console.MarkupLine($"[blue]Community:[/] [yellow]{settings.Community}[/]");
            _console.MarkupLine($"[blue]Input directory:[/] [yellow]{inputPath}[/]");
            _console.MarkupLine($"[blue]Output directory:[/] [yellow]{outputPath}[/]");
            _console.WriteLine();

            var (encryptedCount, deletedCount) = await EncryptSnapshotsAsync(
                _console, inputPath, outputPath, encryptionKey, settings.DeleteOriginals);

            _console.WriteLine();
            _console.MarkupLine($"[green]Done![/] Encrypted {encryptedCount} file(s) to [yellow]{outputPath}[/]");

            if (deletedCount > 0)
            {
                _console.MarkupLine($"[dim]Deleted {deletedCount} original HTML file(s)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error encrypting snapshots");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    internal static async Task<(int encryptedCount, int deletedCount)> EncryptSnapshotsAsync(
        IAnsiConsole console,
        string inputPath,
        string outputPath,
        string encryptionKey,
        bool deleteOriginals)
    {
        var encryptedCount = 0;
        var deletedCount = 0;

        // Create output directory
        Directory.CreateDirectory(outputPath);

        // Find all HTML files
        var htmlFiles = Directory.GetFiles(inputPath, "*.html");

        if (htmlFiles.Length == 0)
        {
            console.MarkupLine("[yellow]No HTML files found to encrypt[/]");
            return (0, 0);
        }

        await console.Status()
            .StartAsync("Encrypting files...", async ctx =>
            {
                foreach (var htmlFile in htmlFiles)
                {
                    var fileName = Path.GetFileName(htmlFile);
                    ctx.Status($"Encrypting {fileName}...");

                    // Read and encrypt
                    var content = await File.ReadAllTextAsync(htmlFile);
                    var encrypted = SnapshotEncryptor.Encrypt(content, encryptionKey);

                    // Write encrypted file
                    var outputFile = Path.Combine(outputPath, $"{fileName}.enc");
                    await File.WriteAllTextAsync(outputFile, encrypted);
                    encryptedCount++;

                    console.MarkupLine($"[green]✓[/] Encrypted {fileName} → {Path.GetFileName(outputFile)}");

                    // Delete original if requested
                    if (deleteOriginals)
                    {
                        File.Delete(htmlFile);
                        deletedCount++;
                    }
                }
            });

        return (encryptedCount, deletedCount);
    }
}
