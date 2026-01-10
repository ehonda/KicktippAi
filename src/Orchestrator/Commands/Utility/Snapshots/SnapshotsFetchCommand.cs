using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Utility.Snapshots;

/// <summary>
/// Command for fetching HTML snapshots from Kicktipp.
/// </summary>
public class SnapshotsFetchCommand : AsyncCommand<SnapshotsFetchSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ILogger<SnapshotsFetchCommand> _logger;

    public SnapshotsFetchCommand(
        IAnsiConsole console,
        IKicktippClientFactory kicktippClientFactory,
        ILogger<SnapshotsFetchCommand> logger)
    {
        _console = console;
        _kicktippClientFactory = kicktippClientFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotsFetchSettings settings)
    {

        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.Community))
            {
                _console.MarkupLine("[red]Error: Community is required[/]");
                return 1;
            }

            _console.MarkupLine("[green]Fetching snapshots...[/]");
            _console.MarkupLine($"[blue]Community:[/] [yellow]{settings.Community}[/]");
            _console.MarkupLine($"[blue]Output directory:[/] [yellow]{settings.OutputDirectory}[/]");

            // Create output directory
            var outputPath = Path.GetFullPath(settings.OutputDirectory);
            Directory.CreateDirectory(outputPath);

            // Warn if not gitignored
            WarnIfNotGitignored(_console, outputPath);

            // Create snapshot client using factory (factory handles env var loading)
            var snapshotClient = _kicktippClientFactory.CreateSnapshotClient();

            var savedCount = await FetchSnapshotsAsync(_console, snapshotClient, settings.Community, outputPath);

            _console.WriteLine();
            _console.MarkupLine($"[green]Done![/] Saved {savedCount} snapshot(s) to [yellow]{outputPath}[/]");
            _console.WriteLine();
            _console.MarkupLine("[dim]Next step: Run 'snapshots encrypt' to encrypt them for committing[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching snapshots");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    internal static async Task<int> FetchSnapshotsAsync(IAnsiConsole console, SnapshotClient snapshotClient, string community, string outputPath)
    {
        var savedCount = 0;

        await console.Status()
            .StartAsync("Fetching snapshots...", async ctx =>
            {
                // 0. Login page (fetched without community context)
                ctx.Status("Fetching login page...");
                var loginContent = await snapshotClient.FetchLoginPageAsync();
                if (loginContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "login.html", loginContent);
                    savedCount++;
                    console.MarkupLine("[green]✓[/] Saved login.html");
                }
                else
                {
                    console.MarkupLine("[red]✗[/] Failed to fetch login page");
                }

                // 1. Tabellen (standings)
                ctx.Status("Fetching tabellen...");
                var tabellenContent = await snapshotClient.FetchStandingsPageAsync(community);
                if (tabellenContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tabellen.html", tabellenContent);
                    savedCount++;
                    console.MarkupLine("[green]✓[/] Saved tabellen.html");
                }
                else
                {
                    console.MarkupLine("[red]✗[/] Failed to fetch tabellen");
                }

                // 2. Tippabgabe (main betting page)
                ctx.Status("Fetching tippabgabe...");
                var tippabgabeContent = await snapshotClient.FetchTippabgabePageAsync(community);
                if (tippabgabeContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tippabgabe.html", tippabgabeContent);
                    savedCount++;
                    console.MarkupLine("[green]✓[/] Saved tippabgabe.html");
                }
                else
                {
                    console.MarkupLine("[red]✗[/] Failed to fetch tippabgabe");
                }

                // 3. Tippabgabe bonus (bonus questions)
                ctx.Status("Fetching tippabgabe-bonus...");
                var bonusContent = await snapshotClient.FetchBonusPageAsync(community);
                if (bonusContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tippabgabe-bonus.html", bonusContent);
                    savedCount++;
                    console.MarkupLine("[green]✓[/] Saved tippabgabe-bonus.html");
                }
                else
                {
                    console.MarkupLine("[red]✗[/] Failed to fetch tippabgabe-bonus");
                }

                // 4. Spielinfo pages (match details with history)
                ctx.Status("Fetching spielinfo pages...");
                var spielinfoPages = await snapshotClient.FetchAllSpielinfoAsync(community);

                foreach (var (fileName, content) in spielinfoPages)
                {
                    await SaveSnapshotAsync(outputPath, $"{fileName}.html", content);
                    savedCount++;
                }

                if (spielinfoPages.Count > 0)
                {
                    console.MarkupLine($"[green]✓[/] Saved {spielinfoPages.Count} spielinfo pages");
                }
                else
                {
                    console.MarkupLine("[yellow]![/] No spielinfo pages found");
                }

                // 5. Spielinfo pages with home/away history (ansicht=2)
                ctx.Status("Fetching spielinfo home/away pages...");
                var homeAwayPages = await snapshotClient.FetchAllSpielinfoHomeAwayAsync(community);

                foreach (var (fileName, content) in homeAwayPages)
                {
                    await SaveSnapshotAsync(outputPath, $"{fileName}.html", content);
                    savedCount++;
                }

                if (homeAwayPages.Count > 0)
                {
                    console.MarkupLine($"[green]✓[/] Saved {homeAwayPages.Count} spielinfo home/away pages");
                }
                else
                {
                    console.MarkupLine("[yellow]![/] No spielinfo home/away pages found");
                }

                // 6. Spielinfo pages with head-to-head history (ansicht=3)
                ctx.Status("Fetching spielinfo head-to-head pages...");
                var h2hPages = await snapshotClient.FetchAllSpielinfoHeadToHeadAsync(community);

                foreach (var (fileName, content) in h2hPages)
                {
                    await SaveSnapshotAsync(outputPath, $"{fileName}.html", content);
                    savedCount++;
                }

                if (h2hPages.Count > 0)
                {
                    console.MarkupLine($"[green]✓[/] Saved {h2hPages.Count} spielinfo head-to-head pages");
                }
                else
                {
                    console.MarkupLine("[yellow]![/] No spielinfo head-to-head pages found");
                }
            });

        return savedCount;
    }

    internal static async Task SaveSnapshotAsync(string outputPath, string fileName, string content)
    {
        var filePath = Path.Combine(outputPath, fileName);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static void WarnIfNotGitignored(IAnsiConsole console, string outputPath)
    {
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), outputPath);
        var directoryName = Path.GetFileName(outputPath.TrimEnd(Path.DirectorySeparatorChar));

        var commonIgnoredPatterns = new[] { "snapshots", "temp", "tmp", "output", "out", ".local" };
        var looksIgnored = commonIgnoredPatterns.Any(p =>
            directoryName.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (!looksIgnored)
        {
            console.MarkupLine(
                $"[yellow]⚠ Warning:[/] Output directory '{relativePath}' may not be gitignored.");
            console.MarkupLine(
                "[yellow]  Make sure to add it to .gitignore before committing![/]");
            console.WriteLine();
        }
    }
}
