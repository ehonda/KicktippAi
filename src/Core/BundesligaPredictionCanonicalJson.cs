using System.Security.Cryptography;
using System.Text.Json;
using NodaTime;
using NodaTime.Text;

namespace EHonda.KicktippAi.Core;

internal static class BundesligaPredictionCanonicalJson
{
    private static readonly InstantPattern CanonicalInstantPattern = NodaTime.Text.InstantPattern.ExtendedIso;

    public static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default
        }))
        {
            write(writer);
        }

        return stream.ToArray();
    }

    public static void ValidateInput(ReadOnlySpan<byte> bytes, string description)
    {
        if (bytes.IsEmpty || bytes[0] is 0xef or 0x20 or 0x09 or 0x0a or 0x0d)
        {
            throw new InvalidDataException($"{description} JSON is empty or has a non-canonical prefix.");
        }
    }

    public static void RequireCanonical(ReadOnlySpan<byte> bytes, byte[] canonical, string description)
    {
        if (!bytes.SequenceEqual(canonical))
        {
            throw new InvalidDataException($"{description} JSON is not byte-for-byte canonical.");
        }
    }

    public static void Properties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "JSON properties are missing, duplicate, unknown, or out of canonical order.");
        }
    }

    public static string String(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"'{propertyName}' must be a string.");
        }

        return value.GetString() ?? throw new InvalidDataException($"'{propertyName}' must not be null.");
    }

    public static string? NullableString(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString()
                ?? throw new InvalidDataException($"'{propertyName}' must not be a null string."),
            _ => throw new InvalidDataException($"'{propertyName}' must be a string or null.")
        };
    }

    public static int Int32(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            throw new InvalidDataException($"'{propertyName}' must be an Int32.");
        }

        return parsed;
    }

    public static long Int64(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
        {
            throw new InvalidDataException($"'{propertyName}' must be an Int64.");
        }

        return parsed;
    }

    public static decimal Decimal(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var parsed))
        {
            throw new InvalidDataException($"'{propertyName}' must be a decimal number.");
        }

        return parsed;
    }

    public static bool Boolean(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"'{propertyName}' must be a Boolean.")
        };
    }

    public static Instant Instant(JsonElement element, string propertyName) =>
        ParseInstant(String(element, propertyName), propertyName);

    public static Instant ParseInstant(string value, string field)
    {
        var result = CanonicalInstantPattern.Parse(value);
        if (!result.Success || result.Value == NodaTime.Instant.MinValue
            || !string.Equals(FormatInstant(result.Value), value, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{field}' must be an exact non-sentinel UTC instant.");
        }

        return result.Value;
    }

    public static string FormatInstant(Instant value)
    {
        if (value == NodaTime.Instant.MinValue)
        {
            throw new InvalidDataException("Instant.MinValue is not a scheduled instant.");
        }

        return CanonicalInstantPattern.Format(value);
    }

    public static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static JsonDocument Parse(ReadOnlySpan<byte> bytes, string description)
    {
        ValidateInput(bytes, description);
        try
        {
            return JsonDocument.Parse(bytes.ToArray());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{description} JSON is invalid.", exception);
        }
    }

    public static string ItemKind(BundesligaPredictionItemKind itemKind) => itemKind switch
    {
        BundesligaPredictionItemKind.Match => "match",
        BundesligaPredictionItemKind.Bonus => "bonus",
        _ => throw new ArgumentOutOfRangeException(nameof(itemKind), itemKind, "Unknown item kind.")
    };

    public static BundesligaPredictionItemKind ParseItemKind(string value) => value switch
    {
        "match" => BundesligaPredictionItemKind.Match,
        "bonus" => BundesligaPredictionItemKind.Bonus,
        _ => throw new InvalidDataException($"Unknown item kind '{value}'.")
    };

    public static string AuthorityMode(BundesligaPredictionAuthorityMode mode) => mode switch
    {
        BundesligaPredictionAuthorityMode.Direct => "direct",
        BundesligaPredictionAuthorityMode.Copy => "copy",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown authority mode.")
    };

    public static BundesligaPredictionAuthorityMode ParseAuthorityMode(string value) => value switch
    {
        "direct" => BundesligaPredictionAuthorityMode.Direct,
        "copy" => BundesligaPredictionAuthorityMode.Copy,
        _ => throw new InvalidDataException($"Unknown authority mode '{value}'.")
    };
}
