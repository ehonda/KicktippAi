using System.Runtime.CompilerServices;
using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Responses;

namespace OpenAiIntegration;

// ChatTokenUsage has no cache-write fields. Preserve Responses API details alongside
// the converted object until the SDK exposes them directly.
internal static class CacheWriteUsage
{
    private sealed record Counts(int Ttl30m, int Ttl12h);
    private static readonly ConditionalWeakTable<ChatTokenUsage, Counts> Values = new();

    public static void Attach(ChatTokenUsage target, ResponseTokenUsage source)
    {
        if (source.InputTokenDetails is null) return;
        using var json = JsonDocument.Parse(ModelReaderWriter.Write(source.InputTokenDetails, ModelReaderWriterOptions.Json));
        var root = json.RootElement;
        if (!root.TryGetProperty("cache_write_tokens", out var totalElement)) return;
        var total = totalElement.GetInt32();
        var thirty = total;
        var twelve = 0;
        if (root.TryGetProperty("cache_write_tokens_details", out var details))
        {
            thirty = details.GetProperty("ttl_30m_tokens").GetInt32();
            twelve = details.GetProperty("ttl_12h_tokens").GetInt32();
            if (thirty + twelve != total) throw new InvalidOperationException("Cache-write token counts do not add up.");
        }
        if (thirty < 0 || twelve < 0 || total + (target.InputTokenDetails?.CachedTokenCount ?? 0) > target.InputTokenCount)
            throw new InvalidOperationException("Invalid cache-write token counts.");
        Values.Add(target, new Counts(thirty, twelve));
    }

    public static (int Ttl30m, int Ttl12h) Get(ChatTokenUsage usage) =>
        Values.TryGetValue(usage, out var counts) ? (counts.Ttl30m, counts.Ttl12h) : (0, 0);
}
