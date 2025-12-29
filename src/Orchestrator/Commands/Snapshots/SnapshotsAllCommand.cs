using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Snapshots;

/// <summary>
/// Command that fetches snapshots and encrypts them in one step.
/// </summary>
public class SnapshotsAllCommand : AsyncCommand<SnapshotsAllSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotsAllSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<SnapshotsAllCommand>();

        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.Community))
            {
                AnsiConsole.MarkupLine("[red]Error: Community is required[/]");
                return 1;
            }

            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            // Check encryption key early
            var encryptionKey = Environment.GetEnvironmentVariable("KICKTIPP_FIXTURE_KEY");
            if (string.IsNullOrEmpty(encryptionKey))
            {
                AnsiConsole.MarkupLine("[red]Error: KICKTIPP_FIXTURE_KEY environment variable is not set.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]To generate a new key:[/]");
                AnsiConsole.MarkupLine("[dim]  .\\Encrypt-Fixture.ps1 -GenerateKey[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]Fetching and encrypting snapshots...[/]");
            AnsiConsole.MarkupLine($"[blue]Community:[/] [yellow]{settings.Community}[/]");
            AnsiConsole.MarkupLine($"[blue]Snapshots directory:[/] [yellow]{settings.SnapshotsDirectory}[/]");
            AnsiConsole.MarkupLine($"[blue]Output directory:[/] [yellow]{settings.OutputDirectory}[/]");
            AnsiConsole.WriteLine();

            // Setup dependency injection for fetching
            var services = new ServiceCollection();
            SnapshotsFetchCommand.ConfigureServices(services, logger);
            var serviceProvider = services.BuildServiceProvider();

            var snapshotsPath = Path.GetFullPath(settings.SnapshotsDirectory);
            var outputPath = Path.GetFullPath(settings.OutputDirectory);

            // Create snapshots directory
            Directory.CreateDirectory(snapshotsPath);

            // Create snapshot client
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Kicktipp");
            var snapshotClient = new SnapshotClient(httpClient, logger);

            // Step 1: Fetch snapshots
            AnsiConsole.MarkupLine("[bold]Step 1: Fetching snapshots[/]");
            var fetchedCount = await SnapshotsFetchCommand.FetchSnapshotsAsync(
                snapshotClient, settings.Community, snapshotsPath);

            if (fetchedCount == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No snapshots fetched, nothing to encrypt[/]");
                return 0;
            }

            AnsiConsole.WriteLine();

            // Step 2: Encrypt snapshots
            AnsiConsole.MarkupLine("[bold]Step 2: Encrypting snapshots[/]");
            var deleteOriginals = !settings.KeepOriginals;
            var (encryptedCount, deletedCount) = await SnapshotsEncryptCommand.EncryptSnapshotsAsync(
                snapshotsPath, outputPath, encryptionKey, deleteOriginals);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Done![/] Fetched {fetchedCount}, encrypted {encryptedCount} snapshot(s)");
            AnsiConsole.MarkupLine($"[dim]Encrypted files saved to: {outputPath}[/]");

            if (deletedCount > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Deleted {deletedCount} original HTML file(s)[/]");
            }
            else if (!settings.KeepOriginals && fetchedCount > 0)
            {
                AnsiConsole.MarkupLine("[dim]Original HTML files kept (use default behavior to delete)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in snapshots all command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
