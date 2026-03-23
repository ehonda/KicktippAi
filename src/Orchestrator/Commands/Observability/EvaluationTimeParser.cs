using NodaTime;
using NodaTime.Text;

namespace Orchestrator.Commands.Observability;

internal static class EvaluationTimeParser
{
    private static readonly ZonedDateTimePattern EvaluationTimePattern =
        ZonedDateTimePattern.GeneralFormatOnlyIso.WithZoneProvider(DateTimeZoneProviders.Tzdb);

    private const string ExampleValue = "2026-03-15T12:00:00 Europe/Berlin (+01)";

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

        value = Normalize(value);

        try
        {
            return EvaluationTimePattern.Parse(value).GetValueOrThrow().ToDateTimeOffset();
        }
        catch (UnparsableValueException ex)
        {
            throw new ArgumentException(
                $"Evaluation time must use NodaTime's invariant ZonedDateTime 'G' pattern, for example '{ExampleValue}'. {ex.Message}",
                ex);
        }
    }

    private static string Normalize(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value;
    }
}
