using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Orchestrator.Infrastructure.Langfuse;

internal static class LangfuseJsonUtilities
{
    public static bool IsDefined(JsonElement element)
    {
        return element.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;
    }

    public static bool StableEquals(JsonElement left, JsonElement right)
    {
        return string.Equals(ToStableJson(left), ToStableJson(right), StringComparison.Ordinal);
    }

    public static string ToStableJson(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteStableJson(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteStableJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteStableJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteStableJson(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;

            case JsonValueKind.Number:
                if (value.TryGetInt64(out var int64Value))
                {
                    writer.WriteNumberValue(int64Value);
                }
                else if (value.TryGetDecimal(out var decimalValue))
                {
                    writer.WriteNumberValue(decimalValue);
                }
                else
                {
                    writer.WriteRawValue(value.GetRawText());
                }

                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "Unsupported JSON value kind '{0}'.", value.ValueKind));
        }
    }
}