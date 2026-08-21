using System.Diagnostics;
using System.Globalization;

namespace OpenAiIntegration;

/// <summary>
/// Filterable Langfuse metadata for prediction observations.
/// </summary>
public sealed record PredictionTelemetryMetadata(
    string? HomeTeam = null,
    string? AwayTeam = null,
    int? RepredictionIndex = null,
    string? Competition = null,
    IReadOnlyList<string>? ContextDocumentNames = null,
    string? RosterPublicationSnapshotId = null,
    string? ClubEloPublicationSnapshotId = null,
    string? BonusContextCategory = null,
    IReadOnlyList<string>? BonusContextSelectedDocuments = null,
    IReadOnlyList<string>? BonusContextExcludedDocuments = null,
    int? BonusContextEstimatedUtf8Bytes = null,
    int? BonusContextEstimatedTokens = null,
    int? BonusContextDocumentBudget = null,
    int? BonusContextEstimatedTokenBudget = null)
{
    public void ApplyToObservation(Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        SetObservationMetadata(activity, "homeTeam", HomeTeam);
        SetObservationMetadata(activity, "awayTeam", AwayTeam);
        SetObservationMetadata(activity, "competition", Competition);
        SetObservationMetadata(
            activity,
            "contextDocuments",
            ContextDocumentNames is null
                ? null
                : string.Join(',', ContextDocumentNames));
        SetObservationMetadata(activity, "rosterPublicationSnapshotId", RosterPublicationSnapshotId);
        SetObservationMetadata(activity, "clubEloPublicationSnapshotId", ClubEloPublicationSnapshotId);
        SetObservationMetadata(activity, "bonusContextCategory", BonusContextCategory);
        SetObservationMetadata(
            activity,
            "bonusContextSelectedDocuments",
            BonusContextSelectedDocuments is null
                ? null
                : string.Join(',', BonusContextSelectedDocuments));
        SetObservationMetadata(
            activity,
            "bonusContextExcludedDocuments",
            BonusContextExcludedDocuments is null
                ? null
                : string.Join(',', BonusContextExcludedDocuments));
        SetObservationMetadata(
            activity,
            "bonusContextEstimatedUtf8Bytes",
            BonusContextEstimatedUtf8Bytes?.ToString(CultureInfo.InvariantCulture));
        SetObservationMetadata(
            activity,
            "bonusContextEstimatedTokens",
            BonusContextEstimatedTokens?.ToString(CultureInfo.InvariantCulture));
        SetObservationMetadata(
            activity,
            "bonusContextDocumentBudget",
            BonusContextDocumentBudget?.ToString(CultureInfo.InvariantCulture));
        SetObservationMetadata(
            activity,
            "bonusContextEstimatedTokenBudget",
            BonusContextEstimatedTokenBudget?.ToString(CultureInfo.InvariantCulture));

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
