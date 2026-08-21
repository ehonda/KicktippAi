using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Matchday;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Bonus;

public sealed class BonusSettings : BaseSettings
{
    [CommandOption("--bonus-context-document-budget")]
    [Description("Maximum number of required Bundesliga bonus context documents")]
    public int? BonusContextDocumentBudget { get; set; }

    [CommandOption("--bonus-context-token-budget")]
    [Description("Maximum deterministic estimated tokens for the Bundesliga bonus context section")]
    public int? BonusContextEstimatedTokenBudget { get; set; }

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        if (BonusContextDocumentBudget is < BonusContextBudget.MinimumMaximumDocuments)
        {
            return ValidationResult.Error(
                $"--bonus-context-document-budget must be at least {BonusContextBudget.MinimumMaximumDocuments} when provided");
        }

        if (BonusContextEstimatedTokenBudget is < BonusContextBudget.MinimumMaximumEstimatedTokens)
        {
            return ValidationResult.Error(
                $"--bonus-context-token-budget must be at least {BonusContextBudget.MinimumMaximumEstimatedTokens} when provided");
        }

        return ValidationResult.Success();
    }
}
