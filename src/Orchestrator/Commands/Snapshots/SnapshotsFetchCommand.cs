using KicktippIntegration;
using KicktippIntegration.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Snapshots;

/// <summary>
/// Command for fetching HTML snapshots from Kicktipp.
/// </summary>
public class SnapshotsFetchCommand : AsyncCommand<SnapshotsFetchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotsFetchSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<SnapshotsFetchCommand>();

        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.Community))
            {
                AnsiConsole.MarkupLine("[red]Error: Community is required[/]");
                return 1;
            }

            // Load environment variables (for Kicktipp credentials)
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, logger);
            var serviceProvider = services.BuildServiceProvider();

            AnsiConsole.MarkupLine("[green]Fetching snapshots...[/]");
            AnsiConsole.MarkupLine($"[blue]Community:[/] [yellow]{settings.Community}[/]");
            AnsiConsole.MarkupLine($"[blue]Output directory:[/] [yellow]{settings.OutputDirectory}[/]");

            // Create output directory
            var outputPath = Path.GetFullPath(settings.OutputDirectory);
            Directory.CreateDirectory(outputPath);

            // Warn if not gitignored
            WarnIfNotGitignored(outputPath);

            // Create snapshot client
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Kicktipp");
            var snapshotClient = new SnapshotClient(httpClient, logger);

            var savedCount = await FetchSnapshotsAsync(snapshotClient, settings.Community, outputPath);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Done![/] Saved {savedCount} snapshot(s) to [yellow]{outputPath}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Next step: Run 'snapshots encrypt' to encrypt them for committing[/]");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching snapshots");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    internal static async Task<int> FetchSnapshotsAsync(SnapshotClient snapshotClient, string community, string outputPath)
    {
        var savedCount = 0;

        await AnsiConsole.Status()
            .StartAsync("Fetching snapshots...", async ctx =>
            {
                // 1. Tabellen (standings)
                ctx.Status("Fetching tabellen...");
                var tabellenContent = await snapshotClient.FetchStandingsPageAsync(community);
                if (tabellenContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tabellen.html", tabellenContent);
                    savedCount++;
                    AnsiConsole.MarkupLine("[green]✓[/] Saved tabellen.html");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Failed to fetch tabellen");
                }

                // 2. Tippabgabe (main betting page)
                ctx.Status("Fetching tippabgabe...");
                var tippabgabeContent = await snapshotClient.FetchTippabgabePageAsync(community);
                if (tippabgabeContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tippabgabe.html", tippabgabeContent);
                    savedCount++;
                    AnsiConsole.MarkupLine("[green]✓[/] Saved tippabgabe.html");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Failed to fetch tippabgabe");
                }

                // 3. Tippabgabe bonus (bonus questions)
                ctx.Status("Fetching tippabgabe-bonus...");
                var bonusContent = await snapshotClient.FetchBonusPageAsync(community);
                if (bonusContent != null)
                {
                    await SaveSnapshotAsync(outputPath, "tippabgabe-bonus.html", bonusContent);
                    savedCount++;
                    AnsiConsole.MarkupLine("[green]✓[/] Saved tippabgabe-bonus.html");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Failed to fetch tippabgabe-bonus");
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
                    AnsiConsole.MarkupLine($"[green]✓[/] Saved {spielinfoPages.Count} spielinfo pages");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]![/] No spielinfo pages found");
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
                    AnsiConsole.MarkupLine($"[green]✓[/] Saved {homeAwayPages.Count} spielinfo home/away pages");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]![/] No spielinfo home/away pages found");
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
                    AnsiConsole.MarkupLine($"[green]✓[/] Saved {h2hPages.Count} spielinfo head-to-head pages");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]![/] No spielinfo head-to-head pages found");
                }
            });

        return savedCount;
    }

    internal static void ConfigureServices(IServiceCollection services, ILogger logger)
    {
        // Add logging
        services.AddSingleton(logger);

        // Get Kicktipp credentials from environment
        var username = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME");
        var password = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD");
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("KICKTIPP_USERNAME and KICKTIPP_PASSWORD environment variables are required");
        }

        // Configure Kicktipp credentials
        services.Configure<KicktippOptions>(options =>
        {
            options.Username = username;
            options.Password = password;
        });

        // Register the authentication handler
        services.AddSingleton<KicktippAuthenticationHandler>();

        // Register HttpClient with authentication
        services.AddHttpClient("Kicktipp", client =>
            {
                client.BaseAddress = new Uri("https://www.kicktipp.de");
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            })
            .AddHttpMessageHandler<KicktippAuthenticationHandler>();
    }

    internal static async Task SaveSnapshotAsync(string outputPath, string fileName, string content)
    {
        var filePath = Path.Combine(outputPath, fileName);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static void WarnIfNotGitignored(string outputPath)
    {
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), outputPath);
        var directoryName = Path.GetFileName(outputPath.TrimEnd(Path.DirectorySeparatorChar));

        var commonIgnoredPatterns = new[] { "snapshots", "temp", "tmp", "output", "out", ".local" };
        var looksIgnored = commonIgnoredPatterns.Any(p =>
            directoryName.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (!looksIgnored)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]⚠ Warning:[/] Output directory '{relativePath}' may not be gitignored.");
            AnsiConsole.MarkupLine(
                "[yellow]  Make sure to add it to .gitignore before committing![/]");
            AnsiConsole.WriteLine();
        }
    }
}
