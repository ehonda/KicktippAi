using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PromptSampleTests.Commands;

public class LiveSettings : BaseSettings
{
    [CommandOption("-h|--home")]
    [Description("Home team name (default: FC Bayern München)")]
    [DefaultValue("FC Bayern München")]
    public string HomeTeam { get; set; } = "FC Bayern München";

    [CommandOption("-a|--away")]
    [Description("Away team name (default: RB Leipzig)")]
    [DefaultValue("RB Leipzig")]
    public string AwayTeam { get; set; } = "RB Leipzig";

    [CommandOption("-m|--match")]
    [Description("Match number (0-8) to predict from available matches. Overrides home/away team settings.")]
    public int? MatchNumber { get; set; }
}

public class LiveCommand : AsyncCommand<LiveSettings>
{
    private readonly ILogger<LiveCommand> _logger;
    private readonly PromptTestRunner _runner;

    public LiveCommand()
    {
        _logger = LoggingConfiguration.CreateLogger<LiveCommand>();
        var runnerLogger = LoggingConfiguration.CreateLogger<PromptTestRunner>();
        _runner = new PromptTestRunner(runnerLogger);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, LiveSettings settings)
    {
        try
        {
            // Load environment variables from .env file
            EnvironmentHelper.LoadEnvironmentVariables(_logger);

            AnsiConsole.MarkupLine($"[green]Running live mode with model:[/] [yellow]{settings.Model}[/]");
            
            if (settings.MatchNumber.HasValue)
            {
                AnsiConsole.MarkupLine($"[green]Match:[/] [blue]#{settings.MatchNumber}[/] (from available matches)");
                AnsiConsole.WriteLine();
                
                await _runner.RunLiveModeWithMatchSelection(settings.Model, settings.MatchNumber.Value, settings.Verbose);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Match:[/] [blue]{settings.HomeTeam}[/] vs [blue]{settings.AwayTeam}[/]");
                AnsiConsole.WriteLine();
                
                await _runner.RunLiveMode(settings.Model, settings.HomeTeam, settings.AwayTeam, settings.Verbose);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            _logger.LogError(ex, "Error occurred while running live mode");
            return 1;
        }
    }
}
