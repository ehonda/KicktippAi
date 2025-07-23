using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

public class BaseSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-2024-08-06, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--override-kicktipp")]
    [Description("Override existing predictions on Kicktipp (default: false)")]
    [DefaultValue(false)]
    public bool OverrideKicktipp { get; set; }

    [CommandOption("--override-database")]
    [Description("Override existing predictions in the database (default: false)")]
    [DefaultValue(false)]
    public bool OverrideDatabase { get; set; }

    [CommandOption("--agent")]
    [Description("Agent mode - hide prediction details from output (for automated environments)")]
    [DefaultValue(false)]
    public bool Agent { get; set; }

    [CommandOption("--dry-run")]
    [Description("Dry run mode - no changes will be made to database or Kicktipp")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }
}
