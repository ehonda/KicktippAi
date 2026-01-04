using System.ComponentModel;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.Snapshots;

/// <summary>
/// Base settings shared by all snapshot subcommands.
/// </summary>
public class SnapshotsBaseSettings : CommandSettings
{
    [CommandOption("-c|--community")]
    [Description("The Kicktipp community (e.g., ehonda-test-buli)")]
    public required string Community { get; set; }

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
    [Description("Output base directory for encrypted files (default: tests/KicktippIntegration.Tests/Fixtures/Html/Real)")]
    [DefaultValue("tests/KicktippIntegration.Tests/Fixtures/Html/Real")]
    public string OutputDirectory { get; set; } = "tests/KicktippIntegration.Tests/Fixtures/Html/Real";

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
    [CommandOption("--snapshots-dir")]
    [Description("Intermediate directory for unencrypted snapshots (default: kicktipp-snapshots/)")]
    [DefaultValue("kicktipp-snapshots")]
    public string SnapshotsDirectory { get; set; } = "kicktipp-snapshots";

    [CommandOption("-o|--output")]
    [Description("Output base directory for encrypted files (default: tests/KicktippIntegration.Tests/Fixtures/Html/Real)")]
    [DefaultValue("tests/KicktippIntegration.Tests/Fixtures/Html/Real")]
    public string OutputDirectory { get; set; } = "tests/KicktippIntegration.Tests/Fixtures/Html/Real";

    [CommandOption("--keep-originals")]
    [Description("Keep original HTML files after encryption (by default they are deleted)")]
    [DefaultValue(false)]
    public bool KeepOriginals { get; set; }
}
