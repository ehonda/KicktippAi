using System.Diagnostics;
using System.Text.Json;

namespace OpenAiIntegration;

/// <summary>
/// Utilities for propagating shared Langfuse trace context from a root activity to child observations.
/// </summary>
public static class LangfuseActivityPropagation
{
    public static void SetEnvironment(Activity? activity, string? environment)
    {
        SetTagAndBaggage(activity, "langfuse.environment", environment);
    }

    public static void SetSessionId(Activity? activity, string? sessionId)
    {
        SetTagAndBaggage(activity, "langfuse.session.id", sessionId);
    }

    public static void SetTraceTags(Activity? activity, IReadOnlyCollection<string> tags)
    {
        if (activity is null || tags.Count == 0)
        {
            return;
        }

        var serializedTags = JsonSerializer.Serialize(tags);
        SetTagAndBaggage(activity, "langfuse.trace.tags", serializedTags);
    }

    public static void SetTraceMetadata(Activity? activity, string metadataKey, string? value, bool propagateToObservations = true)
    {
        if (activity is null || string.IsNullOrWhiteSpace(metadataKey) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        activity.SetTag($"langfuse.trace.metadata.{metadataKey}", value);

        if (propagateToObservations)
        {
            activity.AddBaggage($"langfuse.observation.metadata.{metadataKey}", value);
        }
    }

    private static void SetTagAndBaggage(Activity? activity, string attributeName, string? value)
    {
        if (activity is null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        activity.SetTag(attributeName, value);
        activity.AddBaggage(attributeName, value);
    }
}
