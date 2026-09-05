using System.ComponentModel;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Matchday;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Bonus;

public sealed class BonusSettings : BaseSettings
{
    [CommandOption("--bonus-profile")]
    [Description("Optional exact specialized bonus profile identifier")]
    public string? BonusProfile { get; set; }

    [CommandOption("--bonus-context-document-budget")]
    [Description("Maximum number of required Bundesliga bonus context documents")]
    public int? BonusContextDocumentBudget { get; set; }

    [CommandOption("--bonus-context-token-budget")]
    [Description("Maximum deterministic estimated tokens for the Bundesliga bonus context section")]
    public int? BonusContextEstimatedTokenBudget { get; set; }

    [CommandOption("--bonus-deadline-at-or-before")]
    [Description("Optional exact UTC deadline ceiling for selected open bonus questions (for example 2026-08-28T18:30:00Z)")]
    public string? BonusDeadlineAtOrBefore { get; set; }

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        var isFrozenClProfile = string.Equals(
            BonusProfile,
            SchadensfresseChampionsLeagueBonusProfile.ProfileId,
            StringComparison.Ordinal);
        if (BonusContextDocumentBudget is < BonusContextBudget.MinimumMaximumDocuments
            && !(isFrozenClProfile && BonusContextDocumentBudget == 0))
        {
            return ValidationResult.Error(
                $"--bonus-context-document-budget must be at least {BonusContextBudget.MinimumMaximumDocuments} when provided");
        }

        if (BonusContextEstimatedTokenBudget is < BonusContextBudget.MinimumMaximumEstimatedTokens
            && !(isFrozenClProfile && BonusContextEstimatedTokenBudget == 0))
        {
            return ValidationResult.Error(
                $"--bonus-context-token-budget must be at least {BonusContextBudget.MinimumMaximumEstimatedTokens} when provided");
        }

        if (!BonusQuestionExecutionScope.TryParseDeadlineAtOrBefore(
                BonusDeadlineAtOrBefore,
                out _,
                out var validationError))
        {
            return ValidationResult.Error(validationError!);
        }

        return ValidationResult.Success();
    }
}
