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
    internal const string DeadlineExample = "2026-08-28T18:30:00Z";

    private static readonly InstantPattern DeadlinePattern = InstantPattern.ExtendedIso;

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

        return new BonusReferenceQuestionProjection(
            targetQuestion,
            null,
            ComputeNormalizedTextSha256(targetQuestion.Text),
            ComputeNormalizedTextSha256(targetQuestion.Text));
    }

    private static string ComputeNormalizedTextSha256(string text)
    {
        var normalizedText = BonusQuestionCompatibilityManifest.NormalizeText(text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
    }
}
