using System.Diagnostics;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionTelemetryMetadataTests;

public class PredictionTelemetryMetadata_Tests
{
    [Test]
    public async Task Applying_to_null_activity_does_nothing()
    {
        var metadata = new PredictionTelemetryMetadata("Bayern", "Dortmund", 2);

        metadata.ApplyToObservation(null);

        await Assert.That(metadata.RepredictionIndex).IsEqualTo(2);
    }

    [Test]
    public async Task Applying_to_activity_sets_expected_tags()
    {
        using var activity = new Activity("test");
        var metadata = new PredictionTelemetryMetadata("Bayern", "Dortmund", 2);

        metadata.ApplyToObservation(activity);

        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam")).IsEqualTo("Bayern");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.awayTeam")).IsEqualTo("Dortmund");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.repredictionIndex")).IsEqualTo("2");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.match")).IsEqualTo("Bayern vs Dortmund");
    }

    [Test]
    public async Task Applying_to_activity_skips_blank_values()
    {
        using var activity = new Activity("test");
        var metadata = new PredictionTelemetryMetadata("Bayern", " ", null);

        metadata.ApplyToObservation(activity);

        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam")).IsEqualTo("Bayern");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.awayTeam")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.repredictionIndex")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.match")).IsNull();
    }

    [Test]
    public async Task Building_delimited_filter_value_sorts_trims_and_deduplicates()
    {
        var value = PredictionTelemetryMetadata.BuildDelimitedFilterValue([" Dortmund ", "Bayern", "Bayern", "", " "]);

        await Assert.That(value).IsEqualTo("|Bayern|Dortmund|");
    }

    [Test]
    public async Task Building_delimited_filter_value_returns_empty_for_no_usable_values()
    {
        var value = PredictionTelemetryMetadata.BuildDelimitedFilterValue(["", " ", "\t"]);

        await Assert.That(value).IsEqualTo(string.Empty);
    }
}
