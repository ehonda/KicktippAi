using System.Diagnostics;

namespace OpenAiIntegration;

/// <summary>
/// Shared telemetry infrastructure for OpenTelemetry instrumentation.
/// When no OTel listener is registered, <see cref="Source"/>.<see cref="ActivitySource.StartActivity(string)"/>
/// returns <c>null</c> and all instrumentation becomes a no-op.
/// </summary>
public static class Telemetry
{
    public static readonly ActivitySource Source = new("KicktippAi");
}
