using System.Diagnostics;
using System.Globalization;

namespace OpenAiIntegration;

/// <summary>
/// Filterable Langfuse metadata for prediction observations.
/// </summary>
public sealed record PredictionTelemetryMetadata(
    string? HomeTeam = null,
    string? AwayTeam = null,
    int? RepredictionIndex = null)
{
    public void ApplyToObservation(Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        SetObservationMetadata(activity, "homeTeam", HomeTeam);
        SetObservationMetadata(activity, "awayTeam", AwayTeam);

        if (RepredictionIndex.HasValue)
        {
            SetObservationMetadata(activity, "repredictionIndex", RepredictionIndex.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(HomeTeam) && !string.IsNullOrWhiteSpace(AwayTeam))
        {
            SetObservationMetadata(activity, "match", $"{HomeTeam} vs {AwayTeam}");
        }
    }

    public static string BuildDelimitedFilterValue(IEnumerable<string> values)
    {
        var normalizedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return normalizedValues.Length == 0
            ? string.Empty
            : $"|{string.Join("|", normalizedValues)}|";
    }

    private static void SetObservationMetadata(Activity activity, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        activity.SetTag($"langfuse.observation.metadata.{key}", value);
    }
}
