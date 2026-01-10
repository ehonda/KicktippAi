using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Utility.Snapshots;

/// <summary>
/// Command that fetches snapshots and encrypts them in one step.
/// </summary>
public class SnapshotsAllCommand : AsyncCommand<SnapshotsAllSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ILogger<SnapshotsAllCommand> _logger;

    public SnapshotsAllCommand(
        IAnsiConsole console,
        IKicktippClientFactory kicktippClientFactory,
        ILogger<SnapshotsAllCommand> logger)
    {
        _console = console;
        _kicktippClientFactory = kicktippClientFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotsAllSettings settings)
    {

        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.Community))
            {
                _console.MarkupLine("[red]Error: Community is required[/]");
                return 1;
            }

            // Check encryption key early (loaded at startup)
            var encryptionKey = Environment.GetEnvironmentVariable("KICKTIPP_FIXTURE_KEY");
            if (string.IsNullOrEmpty(encryptionKey))
            {
                _console.MarkupLine("[red]Error: KICKTIPP_FIXTURE_KEY environment variable is not set.[/]");
                _console.WriteLine();
                _console.MarkupLine("[yellow]To generate a new key:[/]");
                _console.MarkupLine("[dim]  .\\Encrypt-Fixture.ps1 -GenerateKey[/]");
                return 1;
            }

            _console.MarkupLine("[green]Fetching and encrypting snapshots...[/]");
            _console.MarkupLine($"[blue]Community:[/] [yellow]{settings.Community}[/]");
            _console.MarkupLine($"[blue]Snapshots directory:[/] [yellow]{settings.SnapshotsDirectory}[/]");
            _console.MarkupLine($"[blue]Output directory:[/] [yellow]{settings.OutputDirectory}[/]");
            _console.WriteLine();

            var snapshotsPath = Path.GetFullPath(settings.SnapshotsDirectory);
            var outputPath = Path.GetFullPath(settings.OutputDirectory);

            // Create snapshots directory
            Directory.CreateDirectory(snapshotsPath);

            // Create snapshot client using factory (factory handles env var loading)
            var snapshotClient = _kicktippClientFactory.CreateSnapshotClient();

            // Step 1: Fetch snapshots
            _console.MarkupLine("[bold]Step 1: Fetching snapshots[/]");
            var fetchedCount = await SnapshotsFetchCommand.FetchSnapshotsAsync(
                _console, snapshotClient, settings.Community, snapshotsPath);

            if (fetchedCount == 0)
            {
                _console.MarkupLine("[yellow]No snapshots fetched, nothing to encrypt[/]");
                return 0;
            }

            _console.WriteLine();

            // Step 2: Encrypt snapshots to community-specific subdirectory
            _console.MarkupLine("[bold]Step 2: Encrypting snapshots[/]");
            var deleteOriginals = !settings.KeepOriginals;
            var communityOutputPath = Path.Combine(outputPath, settings.Community);
            var (encryptedCount, deletedCount) = await SnapshotsEncryptCommand.EncryptSnapshotsAsync(
                _console, snapshotsPath, communityOutputPath, encryptionKey, deleteOriginals);

            _console.WriteLine();
            _console.MarkupLine($"[green]Done![/] Fetched {fetchedCount}, encrypted {encryptedCount} snapshot(s)");
            _console.MarkupLine($"[dim]Encrypted files saved to: {communityOutputPath}[/]");

            if (deletedCount > 0)
            {
                _console.MarkupLine($"[dim]Deleted {deletedCount} original HTML file(s)[/]");
            }
            else if (!settings.KeepOriginals && fetchedCount > 0)
            {
                _console.MarkupLine("[dim]Original HTML files kept (use default behavior to delete)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in snapshots all command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
