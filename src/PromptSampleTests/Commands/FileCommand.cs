using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PromptSampleTests.Commands;

public class FileSettings : BaseSettings
{
    [CommandArgument(1, "<DIRECTORY>")]
    [Description("Path to the directory containing instructions.md and match.json files")]
    public string Directory { get; set; } = string.Empty;
}

public class FileCommand : AsyncCommand<FileSettings>
{
    private readonly ILogger<FileCommand> _logger;
    private readonly PromptTestRunner _runner;

    public FileCommand()
    {
        _logger = LoggingConfiguration.CreateLogger<FileCommand>();
        var runnerLogger = LoggingConfiguration.CreateLogger<PromptTestRunner>();
        _runner = new PromptTestRunner(runnerLogger);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, FileSettings settings)
    {
        try
        {
            // Load environment variables from .env file
            EnvironmentHelper.LoadEnvironmentVariables(_logger);

            AnsiConsole.MarkupLine($"[green]Running file mode with model:[/] [yellow]{settings.Model}[/]");
            AnsiConsole.MarkupLine($"[green]Directory:[/] [blue]{settings.Directory}[/]");
            AnsiConsole.WriteLine();

            await _runner.RunFileMode(settings.Model, settings.Directory, settings.Verbose);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            _logger.LogError(ex, "Error occurred while running file mode");
            return 1;
        }
    }
}
