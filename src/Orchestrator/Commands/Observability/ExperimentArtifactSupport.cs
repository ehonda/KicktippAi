using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Commands.Observability;

internal static class ExperimentArtifactSupport
{
    public const string Season = "2025/2026";

    private static readonly DateTimeZone BundesligaTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    public static HostedMatchExperimentDatasetItem BuildHostedDatasetItem(PersistedMatchOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var tippSpielId = outcome.TippSpielId ?? throw new InvalidOperationException(
            $"Persisted outcome for {outcome.HomeTeam} vs {outcome.AwayTeam} is missing tippspielId.");

        if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
        {
            throw new InvalidOperationException(
                $"Persisted outcome for {outcome.HomeTeam} vs {outcome.AwayTeam} does not contain a completed score.");
        }

        var promptMatch = RehydrateForPromptOutput(outcome);
        using var matchJsonDocument = JsonDocument.Parse(PredictionPromptComposer.CreateMatchJson(promptMatch));

        return new HostedMatchExperimentDatasetItem(
            BuildHostedDatasetItemId(outcome.Competition, outcome.CommunityContext, tippSpielId),
            matchJsonDocument.RootElement.Clone(),
            new HostedMatchExperimentExpectedOutput(
                outcome.HomeGoals.Value,
                outcome.AwayGoals.Value),
            new HostedMatchExperimentMetadata(
                outcome.Competition,
                Season,
                outcome.CommunityContext,
                outcome.Matchday,
                $"md{outcome.Matchday:00}",
                outcome.HomeTeam,
                outcome.AwayTeam,
                tippSpielId));
    }

    public static Match RehydrateForPromptOutput(PersistedMatchOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return RehydrateForPromptOutput(new Match(outcome.HomeTeam, outcome.AwayTeam, outcome.StartsAt, outcome.Matchday));
    }

    public static Match RehydrateForPromptOutput(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var instant = match.StartsAt.ToInstant();
        var offset = BundesligaTimeZone.GetUtcOffset(instant);
        var localizedStartsAt = instant.InZone(DateTimeZone.ForOffset(offset));
        return new Match(match.HomeTeam, match.AwayTeam, localizedStartsAt, match.Matchday, match.IsCancelled);
    }

    public static string BuildSourceDatasetName(string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        return $"match-predictions/bundesliga-2025-26/{communityContext}";
    }

    public static string BuildCanonicalDatasetName(string communityContext)
    {
        return BuildSourceDatasetName(communityContext);
    }
    public static string BuildHostedDatasetItemId(string competition, string communityContext, string tippSpielId)
    {
        return string.Join(
            "__",
            Slugify(competition),
            Slugify(communityContext),
            $"ts{Slugify(tippSpielId)}");
    }

    public static string BuildSliceDatasetItemId(string sourceItemId, string sliceKey)
    {
        return $"{sourceItemId}__slice__{sliceKey}";
    }

    public static string BuildRepeatedSliceDatasetItemId(
        string sourceItemId,
        string sliceKey,
        int repetitionIndex,
        int totalRepetitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sliceKey);

        if (repetitionIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(repetitionIndex), repetitionIndex, "Repetition index must be at least 1.");
        }

        if (totalRepetitions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRepetitions), totalRepetitions, "Total repetitions must be at least 1.");
        }

        var paddingWidth = Math.Max(2, totalRepetitions.ToString(CultureInfo.InvariantCulture).Length);
        var repetitionToken = repetitionIndex.ToString($"D{paddingWidth}", CultureInfo.InvariantCulture);
        return $"{sourceItemId}__repeated-match__{sliceKey}__{repetitionToken}";
    }

    public static string BuildRepeatedMatchSourcePoolKey(int matchday, string homeTeam, string awayTeam)
    {
        return $"md{matchday:00}-{Slugify(homeTeam)}-vs-{Slugify(awayTeam)}";
    }

    public static string BuildSingleMatchSourcePoolKey(int matchday, string homeTeam, string awayTeam)
    {
        return BuildRepeatedMatchSourcePoolKey(matchday, homeTeam, awayTeam);
    }

    public static string ComputeSelectedItemIdsHash(IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);

        var joined = string.Join("\n", itemIds.OrderBy(itemId => itemId, StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildRelativeEvaluationPolicyKey(EvaluationTimestampPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!string.Equals(policy.Kind, EvaluationTimestampPolicy.RelativeKind, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(policy.Reference, EvaluationTimestampPolicy.StartsAtReference, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only startsAt-relative evaluation policies can be converted to a policy key.", nameof(policy));
        }

        var timeSpan = policy.Offset.ToTimeSpan();
        var sign = timeSpan.Ticks < 0 ? "-" : "+";
        var absolute = timeSpan.Duration();
        var parts = new List<string>();

        if (absolute.Days != 0)
        {
            parts.Add($"{absolute.Days}d");
        }

        if (absolute.Hours != 0)
        {
            parts.Add($"{absolute.Hours}h");
        }

        if (absolute.Minutes != 0)
        {
            parts.Add($"{absolute.Minutes}m");
        }

        if (absolute.Seconds != 0)
        {
            parts.Add($"{absolute.Seconds}s");
        }

        if (parts.Count == 0)
        {
            parts.Add("0s");
        }

        return $"startsAt{sign}{string.Join(string.Empty, parts)}".ToLowerInvariant();
    }

    public static string FormatStartedAtUtc(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    public static string Slugify(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '-')
            {
                continue;
            }

            builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }
}
