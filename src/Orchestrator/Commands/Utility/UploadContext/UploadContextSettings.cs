using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.UploadContext;

public sealed class UploadContextSettings : CommandSettings
{
    [CommandOption("--input <PATH>")]
    [Description("Path to a context document JSON file with documentName, content, and communityContext")]
    public string Input { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("Competition identifier (defaults from the JSON community context)")]
    public string? Competition { get; set; }

    [CommandOption("--dry-run")]
    [Description("Validate and show the upload without writing Firestore documents")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        return string.IsNullOrWhiteSpace(Input)
            ? ValidationResult.Error("--input is required")
            : ValidationResult.Success();
    }
}
