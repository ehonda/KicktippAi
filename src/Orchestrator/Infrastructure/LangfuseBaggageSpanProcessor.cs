using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Copies Langfuse-related baggage entries onto newly started activities so child observations inherit shared trace context.
/// </summary>
public sealed class LangfuseBaggageSpanProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        foreach (var baggage in data.Baggage)
        {
            if (!baggage.Key.StartsWith("langfuse.", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(baggage.Value))
            {
                continue;
            }

            if (data.GetTagItem(baggage.Key) is null)
            {
                data.SetTag(baggage.Key, baggage.Value);
            }
        }
    }
}
