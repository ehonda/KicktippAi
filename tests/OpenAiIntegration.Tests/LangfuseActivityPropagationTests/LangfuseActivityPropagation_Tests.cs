using System.Diagnostics;
using System.Text.Json;
using TUnit.Core;

namespace OpenAiIntegration.Tests.LangfuseActivityPropagationTests;

public class LangfuseActivityPropagation_Tests
{
    [Test]
    public async Task Setting_environment_adds_tag_and_baggage()
    {
        using var activity = new Activity("test");

        LangfuseActivityPropagation.SetEnvironment(activity, "production");

        await Assert.That(activity.GetTagItem("langfuse.environment")).IsEqualTo("production");
        await Assert.That(activity.Baggage.FirstOrDefault(pair => pair.Key == "langfuse.environment").Value)
            .IsEqualTo("production");
    }

    [Test]
    public async Task Setting_session_id_ignores_blank_values()
    {
        using var activity = new Activity("test");

        LangfuseActivityPropagation.SetSessionId(activity, " ");

        await Assert.That(activity.GetTagItem("langfuse.session.id")).IsNull();
        await Assert.That(activity.Baggage.Any(pair => pair.Key == "langfuse.session.id")).IsFalse();
    }

    [Test]
    public async Task Setting_trace_tags_serializes_and_propagates_tags()
    {
        using var activity = new Activity("test");

        LangfuseActivityPropagation.SetTraceTags(activity, ["tag-b", "tag-a"]);

        var expected = JsonSerializer.Serialize(new[] { "tag-b", "tag-a" });
        await Assert.That(activity.GetTagItem("langfuse.trace.tags")).IsEqualTo(expected);
        await Assert.That(activity.Baggage.FirstOrDefault(pair => pair.Key == "langfuse.trace.tags").Value)
            .IsEqualTo(expected);
    }

    [Test]
    public async Task Getting_observation_metadata_returns_empty_for_default_trace_id()
    {
        using var activity = new Activity("test");

        var metadata = LangfuseActivityPropagation.GetObservationMetadata(activity).ToArray();

        await Assert.That(metadata).IsEmpty();
    }

    [Test]
    public async Task Setting_trace_metadata_with_propagation_registers_observation_metadata()
    {
        using var activity = new Activity("test").Start();

        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", "test-community");
        var metadata = LangfuseActivityPropagation.GetObservationMetadata(activity).ToArray();

        await Assert.That(activity.GetTagItem("langfuse.trace.metadata.community")).IsEqualTo("test-community");
        await Assert.That(activity.Baggage.FirstOrDefault(pair => pair.Key == "langfuse.observation.metadata.community").Value)
            .IsEqualTo("test-community");
        await Assert.That(metadata).HasCount().EqualTo(1);
        await Assert.That(metadata[0].Key).IsEqualTo("langfuse.observation.metadata.community");
        await Assert.That(metadata[0].Value).IsEqualTo("test-community");
    }

    [Test]
    public async Task Clearing_trace_metadata_removes_registered_metadata()
    {
        using var activity = new Activity("test").Start();

        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", "test-community");
        LangfuseActivityPropagation.ClearTraceMetadata(activity);
        var metadata = LangfuseActivityPropagation.GetObservationMetadata(activity).ToArray();

        await Assert.That(metadata).IsEmpty();
    }

    [Test]
    public async Task Setting_trace_metadata_without_propagation_keeps_observation_metadata_empty()
    {
        using var activity = new Activity("test").Start();

        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", "test-community", propagateToObservations: false);
        var metadata = LangfuseActivityPropagation.GetObservationMetadata(activity).ToArray();

        await Assert.That(activity.GetTagItem("langfuse.trace.metadata.community")).IsEqualTo("test-community");
        await Assert.That(activity.Baggage.Any(pair => pair.Key == "langfuse.observation.metadata.community")).IsFalse();
        await Assert.That(metadata).IsEmpty();
    }
}
