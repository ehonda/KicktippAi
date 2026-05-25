using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.CopyFirestoreContext;

public sealed class CopyFirestoreContextSettings : CommandSettings
{
    [CommandOption("--source-community-context <COMMUNITY_CONTEXT>")]
    [Description("Community context to copy documents from")]
    public string SourceCommunityContext { get; set; } = string.Empty;

    [CommandOption("--target-community-context <COMMUNITY_CONTEXT>")]
    [Description("Community context to copy documents to")]
    public string TargetCommunityContext { get; set; } = string.Empty;

    [CommandOption("--competition <COMPETITION>")]
    [Description("Competition identifier")]
    public string? Competition { get; set; }

    [CommandOption("--context-prefix <PREFIX>")]
    [Description("Comma-separated context document prefixes to copy")]
    public string? ContextPrefix { get; set; }

    [CommandOption("--kpi-document <DOCUMENT_NAME>")]
    [Description("Comma-separated KPI document names to copy")]
    public string? KpiDocument { get; set; }

    [CommandOption("--dry-run")]
    [Description("Validate and show the copy plan without writing Firestore documents")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceCommunityContext))
        {
            return ValidationResult.Error("--source-community-context is required");
        }

        if (string.IsNullOrWhiteSpace(TargetCommunityContext))
        {
            return ValidationResult.Error("--target-community-context is required");
        }

        if (string.Equals(SourceCommunityContext, TargetCommunityContext, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Error("source and target community contexts must differ");
        }

        if (string.IsNullOrWhiteSpace(ContextPrefix) && string.IsNullOrWhiteSpace(KpiDocument))
        {
            return ValidationResult.Error("at least one of --context-prefix or --kpi-document is required");
        }

        return ValidationResult.Success();
    }
}
