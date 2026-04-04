using EHonda.KicktippAi.Core;
using NodaTime;
using Orchestrator.Commands.Observability;

namespace Orchestrator.Tests.Commands.Observability.EvaluationTimestampPolicyTests;

public class EvaluationTimestampPolicy_Tests
{
    [Test]
    public async Task Parse_accepts_relative_kind_with_json_roundtrip_duration()
    {
        var policy = EvaluationTimestampPolicyParser.Parse("relative", "-12:00:00");

        await Assert.That(policy.Kind).IsEqualTo("relative");
        await Assert.That(policy.Reference).IsEqualTo("startsAt");
        await Assert.That(policy.Offset).IsEqualTo(Duration.FromHours(-12));
    }

    [Test]
    public async Task Parse_rejects_unsupported_kind()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EvaluationTimestampPolicyParser.Parse("historical", "-12:00:00"));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("must currently be 'relative'");
    }

    [Test]
    public async Task Resolve_applies_relative_offset_to_match_start()
    {
        var match = new Match(
            "Team A",
            "Team B",
            Instant.FromUtc(2026, 3, 29, 17, 30).InZone(DateTimeZoneProviders.Tzdb["Europe/Berlin"]),
            27);
        var policy = EvaluationTimestampPolicy.CreateRelativeToMatchStart(Duration.FromHours(-12));

        var resolved = EvaluationTimestampResolver.Resolve(match, policy);

        await Assert.That(resolved).IsEqualTo(new DateTimeOffset(2026, 3, 29, 7, 30, 0, TimeSpan.FromHours(2)));
    }
}
