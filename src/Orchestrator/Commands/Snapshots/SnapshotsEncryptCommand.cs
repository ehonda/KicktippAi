using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Snapshots;

/// <summary>
/// Command for encrypting HTML snapshots for safe committing.
/// </summary>
public class SnapshotsEncryptCommand : AsyncCommand<SnapshotsEncryptSettings>
{
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
                AnsiConsole.MarkupLine("[red]Error: KICKTIPP_FIXTURE_KEY environment variable is not set.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]To generate a new key:[/]");
                AnsiConsole.MarkupLine("[dim]  .\\Encrypt-Fixture.ps1 -GenerateKey[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Then store the key in:[/]");
                AnsiConsole.MarkupLine("[dim]  <repo>/../KicktippAi.Secrets/tests/KicktippIntegration.Tests/.env[/]");
                return 1;
            }

            var inputPath = Path.GetFullPath(settings.InputDirectory);
            var outputPath = Path.GetFullPath(settings.OutputDirectory);

            if (!Directory.Exists(inputPath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Input directory not found: {inputPath}[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]Encrypting snapshots...[/]");
            AnsiConsole.MarkupLine($"[blue]Input directory:[/] [yellow]{inputPath}[/]");
            AnsiConsole.MarkupLine($"[blue]Output directory:[/] [yellow]{outputPath}[/]");
            AnsiConsole.WriteLine();

            var (encryptedCount, deletedCount) = await EncryptSnapshotsAsync(
                inputPath, outputPath, encryptionKey, settings.DeleteOriginals);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Done![/] Encrypted {encryptedCount} file(s) to [yellow]{outputPath}[/]");

            if (deletedCount > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Deleted {deletedCount} original HTML file(s)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error encrypting snapshots");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    internal static async Task<(int encryptedCount, int deletedCount)> EncryptSnapshotsAsync(
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
            AnsiConsole.MarkupLine("[yellow]No HTML files found to encrypt[/]");
            return (0, 0);
        }

        await AnsiConsole.Status()
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

                    AnsiConsole.MarkupLine($"[green]✓[/] Encrypted {fileName} → {Path.GetFileName(outputFile)}");

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
