using System.Security.Cryptography;
using System.Text;
using EHonda.KicktippAi.Core;
using NodaTime;
using NodaTime.Text;

namespace Orchestrator.Commands.Operations.Bonus;

internal sealed record BonusReferenceQuestionProjection(
    BonusQuestion Question,
    string? AliasId,
    string SourceNormalizedTextSha256,
    string TargetNormalizedTextSha256);

internal static class BonusQuestionExecutionScope
{
    internal const string SchadensfresseCommunity = "schadensfresse";
    internal const string PesSquadCommunityContext = "pes-squad";
    internal const string DeadlineExample = "2026-08-28T18:30:00Z";

    private static readonly InstantPattern DeadlinePattern = InstantPattern.ExtendedIso;
    private static readonly IReadOnlyDictionary<string, (string SourceText, string AliasId)>
        SchadensfresseBundesligaQuestionAliases =
            new Dictionary<string, (string SourceText, string AliasId)>(StringComparer.Ordinal)
            {
                ["1.BL: Welche Mannschaften belegen die Plätze 16-18?"] =
                    ("Welche Mannschaften belegen die Plätze 16-18?", "schadensfresse-buli-places-16-18-v1"),
                ["1.BL: Welche Mannschaft stellt den Spieler mit den meisten Toren?"] =
                    ("Welche Mannschaft stellt den Spieler mit den meisten Toren?", "schadensfresse-buli-top-scorer-v1"),
                ["1.BL: Wer wird Deutscher Meister?"] =
                    ("Wer wird Deutscher Meister?", "schadensfresse-buli-champion-v1"),
                ["1.BL: Wer wird Herbstmeister?"] =
                    ("Wer wird Herbstmeister?", "schadensfresse-buli-autumn-champion-v1"),
                ["1.BL: Wo findet der erste Trainerwechsel statt?"] =
                    ("Wo findet der erste Trainerwechsel statt?", "schadensfresse-buli-first-coach-change-v1")
            };

    internal static bool TryParseDeadlineAtOrBefore(
        string? value,
        out Instant? deadline,
        out string? validationError)
    {
        deadline = null;
        validationError = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var result = DeadlinePattern.Parse(value);
        if (!result.Success || result.Value == Instant.MinValue)
        {
            validationError =
                $"--bonus-deadline-at-or-before must be an exact non-minimum UTC instant such as '{DeadlineExample}'";
            return false;
        }

        deadline = result.Value;
        return true;
    }

    internal static IReadOnlyList<BonusQuestion> SelectAtOrBefore(
        IEnumerable<BonusQuestion> questions,
        string? deadlineAtOrBefore)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (!TryParseDeadlineAtOrBefore(deadlineAtOrBefore, out var deadline, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(deadlineAtOrBefore));
        }

        return deadline is null
            ? questions.ToArray()
            : questions.Where(question => question.Deadline.ToInstant() <= deadline.Value).ToArray();
    }

    internal static BonusReferenceQuestionProjection ResolveReferenceProjection(
        string competition,
        string targetCommunity,
        string sourceCommunityContext,
        BonusQuestion targetQuestion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCommunity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCommunityContext);
        ArgumentNullException.ThrowIfNull(targetQuestion);

        var sourceQuestion = targetQuestion;
        string? aliasId = null;
        if (string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
            && string.Equals(targetCommunity, SchadensfresseCommunity, StringComparison.Ordinal)
            && string.Equals(sourceCommunityContext, PesSquadCommunityContext, StringComparison.Ordinal)
            && SchadensfresseBundesligaQuestionAliases.TryGetValue(targetQuestion.Text, out var alias))
        {
            sourceQuestion = targetQuestion with { Text = alias.SourceText };
            aliasId = alias.AliasId;
        }

        return new BonusReferenceQuestionProjection(
            sourceQuestion,
            aliasId,
            ComputeNormalizedTextSha256(sourceQuestion.Text),
            ComputeNormalizedTextSha256(targetQuestion.Text));
    }

    private static string ComputeNormalizedTextSha256(string text)
    {
        var normalizedText = BonusQuestionCompatibilityManifest.NormalizeText(text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
    }
}
