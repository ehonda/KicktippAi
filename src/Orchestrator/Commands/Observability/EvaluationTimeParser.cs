using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace Orchestrator.Commands.Observability;

internal static class EvaluationTimeParser
{
    public static DateTimeOffset? ParseOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Parse(value);
    }

    public static DateTimeOffset Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        var separatorIndex = value.LastIndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            throw new ArgumentException(
                "Evaluation time must use '<local-date-time> <tzdb-zone>' format, for example '2026-03-15T12:00 Europe/Berlin'.");
        }

        var localDateTimeText = value[..separatorIndex].Trim();
        var zoneId = value[(separatorIndex + 1)..].Trim();

        var localDateTimeResult = LocalDateTimePattern.VariablePrecisionIso.Parse(localDateTimeText);
        if (!localDateTimeResult.Success)
        {
            throw new ArgumentException(
            $"Invalid local date/time '{localDateTimeText}'. Use an ISO local date/time such as '2026-03-15T12:00' or '2026-03-15T12:00:00'.");
        }

        DateTimeZone zone;
        try
        {
            zone = DateTimeZoneProviders.Tzdb[zoneId];
        }
        catch (DateTimeZoneNotFoundException ex)
        {
            throw new ArgumentException($"Unknown TZDB time zone '{zoneId}'.", ex);
        }

        try
        {
            return localDateTimeResult.Value.InZoneStrictly(zone).ToDateTimeOffset();
        }
        catch (SkippedTimeException ex)
        {
            throw new ArgumentException(
                $"The local date/time '{localDateTimeText}' does not exist in time zone '{zoneId}' because of a clock change.",
                ex);
        }
        catch (AmbiguousTimeException ex)
        {
            throw new ArgumentException(
                $"The local date/time '{localDateTimeText}' is ambiguous in time zone '{zoneId}' because of a clock change.",
                ex);
        }
    }
}
