using Orchestrator.Commands.Observability;

namespace Orchestrator.Tests.Commands.Observability.EvaluationTimeParserTests;

public class EvaluationTimeParser_Tests
{
    [Test]
    public async Task Parse_accepts_general_invariant_zoned_date_time_pattern()
    {
        var parsed = EvaluationTimeParser.Parse("2026-03-15T12:00:00 Europe/Berlin (+01)");

        await Assert.That(parsed).IsEqualTo(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    }

    [Test]
    public async Task Parse_rejects_old_local_date_time_plus_zone_only_shape()
    {
        var exception = Assert.Throws<ArgumentException>(() => EvaluationTimeParser.Parse("2026-03-15T12:00 Europe/Berlin"));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("ZonedDateTime 'G' pattern");
        await Assert.That(exception.Message).Contains("2026-03-15T12:00:00 Europe/Berlin (+01)");
    }
}
