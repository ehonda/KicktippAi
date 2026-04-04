using EHonda.KicktippAi.Core;
using NodaTime;
using NodaTime.Text;

namespace Orchestrator.Commands.Observability;

internal sealed record EvaluationTimestampPolicy(
    string Kind,
    string Reference,
    Duration Offset)
{
    public const string RelativeKind = "relative";
    public const string StartsAtReference = "startsAt";

    public static EvaluationTimestampPolicy CreateRelativeToMatchStart(Duration offset)
    {
        return new EvaluationTimestampPolicy(RelativeKind, StartsAtReference, offset);
    }
}

internal static class EvaluationTimestampPolicyParser
{
    private static readonly DurationPattern[] SupportedDurationPatterns =
    [
        DurationPattern.JsonRoundtrip,
        DurationPattern.Roundtrip
    ];

    public static EvaluationTimestampPolicy? ParseOrNull(string? kind, string? offset)
    {
        if (string.IsNullOrWhiteSpace(kind) && string.IsNullOrWhiteSpace(offset))
        {
            return null;
        }

        return Parse(kind, offset);
    }

    public static EvaluationTimestampPolicy Parse(string? kind, string? offset)
    {
        kind = Normalize(kind, nameof(kind));
        offset = Normalize(offset, nameof(offset));

        if (!string.Equals(kind, EvaluationTimestampPolicy.RelativeKind, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Evaluation policy kind must currently be '{EvaluationTimestampPolicy.RelativeKind}'.",
                nameof(kind));
        }

        foreach (var pattern in SupportedDurationPatterns)
        {
            var parseResult = pattern.Parse(offset);
            if (parseResult.Success)
            {
                return EvaluationTimestampPolicy.CreateRelativeToMatchStart(parseResult.Value);
            }
        }

        throw new ArgumentException(
            "Evaluation policy offset must use a supported NodaTime Duration format, for example '-12:00:00' or '-0:12:00:00'.",
            nameof(offset));
    }

    private static string Normalize(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value;
    }
}

internal static class EvaluationTimestampResolver
{
    public static DateTimeOffset Resolve(Match match, EvaluationTimestampPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(policy);

        if (!string.Equals(policy.Kind, EvaluationTimestampPolicy.RelativeKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported evaluation policy kind '{policy.Kind}'.", nameof(policy));
        }

        if (!string.Equals(policy.Reference, EvaluationTimestampPolicy.StartsAtReference, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported evaluation policy reference '{policy.Reference}'.", nameof(policy));
        }

        return (match.StartsAt + policy.Offset).ToDateTimeOffset();
    }
}
