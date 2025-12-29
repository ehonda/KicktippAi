using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Snapshots;

/// <summary>
/// Base settings shared by all snapshot subcommands.
/// </summary>
public class SnapshotsBaseSettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}

/// <summary>
/// Settings for the 'snapshots fetch' subcommand.
/// </summary>
public class SnapshotsFetchSettings : SnapshotsBaseSettings
{
    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to fetch snapshots from (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("-o|--output")]
    [Description("Output directory for snapshots (default: kicktipp-snapshots/)")]
    [DefaultValue("kicktipp-snapshots")]
    public string OutputDirectory { get; set; } = "kicktipp-snapshots";
}

/// <summary>
/// Settings for the 'snapshots encrypt' subcommand.
/// </summary>
public class SnapshotsEncryptSettings : SnapshotsBaseSettings
{
    [CommandOption("-i|--input")]
    [Description("Input directory containing HTML files to encrypt (default: kicktipp-snapshots/)")]
    [DefaultValue("kicktipp-snapshots")]
    public string InputDirectory { get; set; } = "kicktipp-snapshots";

    [CommandOption("-o|--output")]
    [Description("Output directory for encrypted files (default: tests/KicktippIntegration.Tests/Fixtures/Html/)")]
    [DefaultValue("tests/KicktippIntegration.Tests/Fixtures/Html")]
    public string OutputDirectory { get; set; } = "tests/KicktippIntegration.Tests/Fixtures/Html";

    [CommandOption("--delete-originals")]
    [Description("Delete original HTML files after encryption")]
    [DefaultValue(false)]
    public bool DeleteOriginals { get; set; }
}

/// <summary>
/// Settings for the 'snapshots all' subcommand.
/// </summary>
public class SnapshotsAllSettings : SnapshotsBaseSettings
{
    [CommandOption("-c|--community")]
    [Description("The Kicktipp community to fetch snapshots from (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

    [CommandOption("--snapshots-dir")]
    [Description("Intermediate directory for unencrypted snapshots (default: kicktipp-snapshots/)")]
    [DefaultValue("kicktipp-snapshots")]
    public string SnapshotsDirectory { get; set; } = "kicktipp-snapshots";

    [CommandOption("-o|--output")]
    [Description("Output directory for encrypted files (default: tests/KicktippIntegration.Tests/Fixtures/Html/)")]
    [DefaultValue("tests/KicktippIntegration.Tests/Fixtures/Html")]
    public string OutputDirectory { get; set; } = "tests/KicktippIntegration.Tests/Fixtures/Html";

    [CommandOption("--keep-originals")]
    [Description("Keep original HTML files after encryption (by default they are deleted)")]
    [DefaultValue(false)]
    public bool KeepOriginals { get; set; }
}
