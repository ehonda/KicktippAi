using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class MatchdayDevSettings : DevParticipationSettings
{
    [CommandOption("--show-context-documents")]
    [Description("Show the content of context documents used for matchday prediction")]
    [DefaultValue(false)]
    public bool ShowContextDocuments { get; set; }
}
