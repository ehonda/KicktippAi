namespace Orchestrator.Commands.Shared;

/// <summary>
/// Holds the unsafe schadensfresse command entrypoints closed until the typed,
/// target-owned primary route has its complete identity and provenance gates.
/// </summary>
internal static class SchadensfressePrimaryRouteGate
{
    internal const string Community = "schadensfresse";

    internal static void EnsureAvailable(string community)
    {
        if (string.Equals(community, Community, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "schadensfresse predictions are disabled until the typed target-owned primary command route is available.");
        }
    }
}
