using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using FirebaseAdapter;

namespace Orchestrator.Commands.Observability.ContextChanges;

/// <summary>
/// Command for showing changes between latest and previous versions of context documents.
/// </summary>
public class ContextChangesCommand : AsyncCommand<ContextChangesSettings>
{
    private readonly IAnsiConsole _console;

    public ContextChangesCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ContextChangesSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<ContextChangesCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            _console.MarkupLine($"[green]Context changes command initialized for community context:[/] [yellow]{settings.CommunityContext}[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            // Execute the context changes workflow
            await ExecuteContextChanges(serviceProvider, settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing context-changes command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private async Task ExecuteContextChanges(IServiceProvider serviceProvider, ContextChangesSettings settings, ILogger logger)
    {
        var contextRepository = serviceProvider.GetRequiredService<IContextRepository>();
        
        _console.MarkupLine($"[blue]Getting context document names for community:[/] [yellow]{settings.CommunityContext}[/]");
        
        // Get all context document names
        var allDocumentNames = await contextRepository.GetContextDocumentNamesAsync(settings.CommunityContext);
        
        if (!allDocumentNames.Any())
        {
            _console.MarkupLine("[yellow]No context documents found for this community[/]");
            return;
        }
        
        if (settings.Verbose)
        {
            _console.MarkupLine($"[dim]Found {allDocumentNames.Count} context document(s)[/]");
        }
        
        // Select documents to show (either random selection or all if count is larger)
        var documentsToShow = SelectDocuments(allDocumentNames, settings.Count, settings.Seed);
        
        if (settings.Verbose && documentsToShow.Count < allDocumentNames.Count)
        {
            _console.MarkupLine($"[dim]Showing {documentsToShow.Count} of {allDocumentNames.Count} documents{(settings.Seed.HasValue ? $" (seed: {settings.Seed})" : "")}[/]");
        }
        
        var changesFound = 0;
        
        foreach (var documentName in documentsToShow)
        {
            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Checking document: {documentName}[/]");
            }
            
            var hasChanges = await ShowDocumentChanges(contextRepository, documentName, settings.CommunityContext, settings.Verbose);
            if (hasChanges)
            {
                changesFound++;
            }
        }
        
        _console.WriteLine();
        if (changesFound == 0)
        {
            _console.MarkupLine("[yellow]No changes found between versions[/]");
        }
        else
        {
            _console.MarkupLine($"[green]Found changes in {changesFound} document(s)[/]");
        }
    }
    
    private static List<string> SelectDocuments(IReadOnlyList<string> allDocuments, int count, int? seed)
    {
        if (allDocuments.Count <= count)
        {
            return allDocuments.ToList();
        }
        
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        return allDocuments.OrderBy(x => random.Next()).Take(count).ToList();
    }
    
    private async Task<bool> ShowDocumentChanges(IContextRepository contextRepository, string documentName, string communityContext, bool verbose)
    {
        try
        {
            // Get the latest document
            var latestDocument = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
            
            if (latestDocument == null)
            {
                if (verbose)
                {
                    _console.MarkupLine($"[dim]Document '{documentName}' not found[/]");
                }
                return false;
            }
            
            if (latestDocument.Version == 0)
            {
                if (verbose)
                {
                    _console.MarkupLine($"[dim]Document '{documentName}' has only one version (v{latestDocument.Version})[/]");
                }
                return false;
            }
            
            // Get the previous version
            var previousDocument = await contextRepository.GetContextDocumentAsync(documentName, latestDocument.Version - 1, communityContext);
            
            if (previousDocument == null)
            {
                if (verbose)
                {
                    _console.MarkupLine($"[dim]Previous version of '{documentName}' not found[/]");
                }
                return false;
            }
            
            // Check if content actually differs
            if (latestDocument.Content == previousDocument.Content)
            {
                if (verbose)
                {
                    _console.MarkupLine($"[dim]Document '{documentName}' has no content changes between v{previousDocument.Version} and v{latestDocument.Version}[/]");
                }
                return false;
            }
            
            // Show the diff
            ShowDocumentDiff(documentName, previousDocument, latestDocument);
            return true;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error processing document '{documentName}': {ex.Message}[/]");
            return false;
        }
    }
    
    private void ShowDocumentDiff(string documentName, ContextDocument oldDocument, ContextDocument newDocument)
    {
        var panel = new Panel($"[bold]{documentName}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
        _console.Write(panel);
        
        _console.MarkupLine($"[dim]Changes from v{oldDocument.Version} ({oldDocument.CreatedAt:yyyy-MM-dd HH:mm}) to v{newDocument.Version} ({newDocument.CreatedAt:yyyy-MM-dd HH:mm})[/]");
        _console.WriteLine();
        
        // Simple line-by-line diff
        var oldLines = oldDocument.Content.Split('\n');
        var newLines = newDocument.Content.Split('\n');
        
        var diff = GenerateSimpleDiff(oldLines, newLines);
        
        var table = new Table();
        table.AddColumn("Line");
        table.AddColumn("Change");
        table.AddColumn("Content");
        table.Border = TableBorder.Minimal;
        
        foreach (var diffLine in diff)
        {
            var lineNumber = diffLine.LineNumber?.ToString() ?? "";
            var changeType = diffLine.Type switch
            {
                DiffLineType.Added => "[green]+[/]",
                DiffLineType.Removed => "[red]-[/]",
                DiffLineType.Unchanged => " ",
                _ => " "
            };
            
            var content = diffLine.Type switch
            {
                DiffLineType.Added => $"[green]{EscapeMarkup(diffLine.Content)}[/]",
                DiffLineType.Removed => $"[red]{EscapeMarkup(diffLine.Content)}[/]",
                DiffLineType.Unchanged => $"[dim]{EscapeMarkup(diffLine.Content)}[/]",
                _ => EscapeMarkup(diffLine.Content)
            };
            
            table.AddRow(lineNumber, changeType, content);
        }
        
        _console.Write(table);
        _console.WriteLine();
    }
    
    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
    
    private static List<DiffLine> GenerateSimpleDiff(string[] oldLines, string[] newLines)
    {
        var result = new List<DiffLine>();
        var oldIndex = 0;
        var newIndex = 0;
        
        while (oldIndex < oldLines.Length || newIndex < newLines.Length)
        {
            if (oldIndex >= oldLines.Length)
            {
                // Only new lines remaining
                result.Add(new DiffLine(DiffLineType.Added, newIndex + 1, newLines[newIndex]));
                newIndex++;
            }
            else if (newIndex >= newLines.Length)
            {
                // Only old lines remaining
                result.Add(new DiffLine(DiffLineType.Removed, oldIndex + 1, oldLines[oldIndex]));
                oldIndex++;
            }
            else if (oldLines[oldIndex] == newLines[newIndex])
            {
                // Lines are the same
                result.Add(new DiffLine(DiffLineType.Unchanged, newIndex + 1, newLines[newIndex]));
                oldIndex++;
                newIndex++;
            }
            else
            {
                // Lines differ - simple approach: mark old as removed, new as added
                result.Add(new DiffLine(DiffLineType.Removed, oldIndex + 1, oldLines[oldIndex]));
                result.Add(new DiffLine(DiffLineType.Added, newIndex + 1, newLines[newIndex]));
                oldIndex++;
                newIndex++;
            }
        }
        
        return result;
    }
    
    private static void ConfigureServices(IServiceCollection services, ContextChangesSettings settings, ILogger logger)
    {
        // Add logging services
        services.AddLogging();
        
        // Configure Firebase
        services.AddFirebaseDatabase(options =>
        {
            var serviceAccountPath = PathUtility.GetFirebaseJsonPath();
            var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
            
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new InvalidOperationException("FIREBASE_PROJECT_ID environment variable is required");
            }
            
            options.ServiceAccountPath = serviceAccountPath;
            options.ProjectId = projectId;
            
            logger.LogDebug("Firebase configured with project ID: {ProjectId}", projectId);
        });
        
        logger.LogDebug("Services configured for context-changes command");
    }
}

public enum DiffLineType
{
    Unchanged,
    Added,
    Removed
}

public record DiffLine(DiffLineType Type, int? LineNumber, string Content);
