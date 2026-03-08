namespace Orchestrator.Infrastructure;

/// <summary>
/// Provides the current Kicktipp season identifier used for observability metadata.
/// </summary>
public static class KicktippSeasonMetadata
{
    // TODO: Replace this fallback once we support communities that are not using 1. Bundesliga 2025/26.
    public const string Current = "bundesliga-2025-2026";
}
