using System.ComponentModel;
using Spectre.Console.Cli;

namespace PromptSampleTests.Commands;

public class BaseSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-2024-08-06, o4-mini)")]
    public string Model { get; set; } = string.Empty;
}
