using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands;

public class UploadKpiSettings : CommandSettings
{
    [CommandArgument(0, "<DOCUMENT_NAME>")]
    [Description("The name of the KPI document to upload (without file extension)")]
    public string DocumentName { get; set; } = string.Empty;

    [CommandOption("-c|--community-context")]
    [Description("The community context to use (e.g., ehonda-test-buli)")]
    public required string CommunityContext { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output to show detailed information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}
