using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.Experiments;

public sealed class RunRepeatedMatchSettings : RunExperimentSettingsBase
{
    [CommandOption("--batch-count")]
    [Description("Optional number of post-warmup batches. Defaults to 3")]
    [DefaultValue(3)]
    public int BatchCount { get; set; } = 3;

    public override ValidationResult Validate()
    {
        var commonValidation = ValidateCommon();
        if (!commonValidation.Successful)
        {
            return commonValidation;
        }

        if (BatchCount < 1)
        {
            return ValidationResult.Error("--batch-count must be at least 1");
        }

        return ValidationResult.Success();
    }

    internal PreparedExperimentRunOptions ToRunOptions()
    {
        return CreateRunOptions("warmup-plus-batches", batchCount: BatchCount);
    }
}
