using System.ComponentModel;
using Spectre.Console.Cli;

#pragma warning disable CA1822 // these properties follow the Spectre.Console.Cli CommandSettings pattern

namespace Orchestrator.Commands.Operations.RandomMatch;

public class RandomMatchSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-2024-08-06, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to use (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("--with-justification")]
    [Description("Include model justification text alongside predictions")]
    [DefaultValue(false)]
    public bool WithJustification { get; set; }
}
