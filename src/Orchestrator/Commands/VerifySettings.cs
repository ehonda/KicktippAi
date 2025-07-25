using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

public class VerifySettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--agent")]
    [Description("Agent mode - hide prediction details from output (for automated environments)")]
    [DefaultValue(false)]
    public bool Agent { get; set; }

    [CommandOption("--init-matchday")]
    [Description("Init matchday mode - return error when no predictions exist to trigger initial prediction workflow")]
    [DefaultValue(false)]
    public bool InitMatchday { get; set; }
}
