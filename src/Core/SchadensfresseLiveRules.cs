using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EHonda.KicktippAi.Core;

/// <summary>The only semantic identity accepted for current schadensfresse rules.</summary>
public sealed record SchadensfresseLiveRulesV1(
    [property: JsonPropertyName("schemaVersion"), JsonPropertyOrder(0)] string SchemaVersion,
    [property: JsonPropertyName("tipsVisibleBeforeDeadline"), JsonPropertyOrder(1)] bool TipsVisibleBeforeDeadline,
    [property: JsonPropertyName("predictionMode"), JsonPropertyOrder(2)] string PredictionMode,
    [property: JsonPropertyName("resultBases"), JsonPropertyOrder(3)] IReadOnlyList<SchadensfresseResultBasis> ResultBases,
    [property: JsonPropertyName("tieBreak"), JsonPropertyOrder(4)] string TieBreak,
    [property: JsonPropertyName("leadTimeMinutes"), JsonPropertyOrder(5)] int LeadTimeMinutes,
    [property: JsonPropertyName("matchScoring"), JsonPropertyOrder(6)] SchadensfresseMatchScoring MatchScoring,
    [property: JsonPropertyName("bonusScoring"), JsonPropertyOrder(7)] SchadensfresseBonusScoring BonusScoring);

public sealed record SchadensfresseResultBasis(
    [property: JsonPropertyName("subcompetition"), JsonPropertyOrder(0)] string Subcompetition,
    [property: JsonPropertyName("sourceLabel"), JsonPropertyOrder(1)] string SourceLabel,
    [property: JsonPropertyName("resultBasis"), JsonPropertyOrder(2)] string ResultBasis);

public sealed record SchadensfresseMatchScoring(
    [property: JsonPropertyName("win"), JsonPropertyOrder(0)] SchadensfresseScore Win,
    [property: JsonPropertyName("draw"), JsonPropertyOrder(1)] SchadensfresseScore Draw);

public sealed record SchadensfresseScore(
    [property: JsonPropertyName("tendencyPoints"), JsonPropertyOrder(0)] int TendencyPoints,
    [property: JsonPropertyName("goalDifferencePoints"), JsonPropertyOrder(1)] int? GoalDifferencePoints,
    [property: JsonPropertyName("exactResultPoints"), JsonPropertyOrder(2)] int ExactResultPoints);

public sealed record SchadensfresseBonusScoring(
    [property: JsonPropertyName("pointsPerCorrectAnswer"), JsonPropertyOrder(0)] int PointsPerCorrectAnswer,
    [property: JsonPropertyName("answerOrderMatters"), JsonPropertyOrder(1)] bool AnswerOrderMatters);

/// <summary>Strict byte-level serialization and reconstruction for ADR-0059.</summary>
public static class SchadensfresseRulesCanonicalJson
{
    public const string SchemaVersion = "schadensfresse-live-rules-v1";
    public const string CanonicalSha256 = "1fac1a26a539a8c20b5f71be6e6e6dccb622528fc8aa40cdea22e6b21d994d90";
    public const string ScoringTableSha256 = "4ea1a5203ec2870141e59aa5573559a3945741984411f0d5cd3c66fb3a5f473e";
    public const string LegacyNormalizedSha256 = "b6d27eba00e58ba7e98613f24d4669d115302a92c26f83c153b69c97d4949c03";

    public static readonly SchadensfresseLiveRulesV1 Expected = new(
        SchemaVersion, false, "exact-score",
        [
            new("bundesliga", "1. Bundesliga 2026/27", "regularTime90Minutes"),
            new("dfb-pokal", "DFB-Pokal 2026/27", "finalScoreIncludingExtraTimeAndPenaltyShootout"),
            new("uefa-champions-league", "Champions League 2026/27", "finalScoreIncludingExtraTimeAndPenaltyShootout")
        ],
        "matchday-wins-unless-otherwise-agreed", 0,
        new(new(2, 3, 5), new(3, null, 5)), new(9, false));

    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Default,
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static byte[] Serialize(SchadensfresseLiveRulesV1 rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ValidateSemanticValue(rules);
        return JsonSerializer.SerializeToUtf8Bytes(rules, Options);
    }

    public static string ComputeSha256(SchadensfresseLiveRulesV1 rules) =>
        Convert.ToHexStringLower(SHA256.HashData(Serialize(rules)));

    public static SchadensfresseLiveRulesV1 DeserializeCanonical(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes[0] == 0xEF || bytes[^1] is (byte)'\n' or (byte)'\r')
            throw new InvalidDataException("Rules JSON must be nonempty UTF-8 without a BOM or terminal newline.");
        try
        {
            var rules = JsonSerializer.Deserialize<SchadensfresseLiveRulesV1>(bytes, Options)
                ?? throw new InvalidDataException("Rules JSON is empty.");
            ValidateSemanticValue(rules);
            if (!bytes.SequenceEqual(Serialize(rules)))
                throw new InvalidDataException("Rules JSON is not byte-for-byte canonical.");
            return rules;
        }
        catch (JsonException exception) { throw new InvalidDataException("Rules JSON is invalid.", exception); }
    }

    public static void ValidateSemanticValue(SchadensfresseLiveRulesV1 rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!string.Equals(rules.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || rules.TipsVisibleBeforeDeadline
            || !string.Equals(rules.PredictionMode, "exact-score", StringComparison.Ordinal)
            || !string.Equals(rules.TieBreak, "matchday-wins-unless-otherwise-agreed", StringComparison.Ordinal)
            || rules.LeadTimeMinutes != 0
            || rules.ResultBases is null
            || rules.MatchScoring is null
            || rules.BonusScoring is null
            || !rules.ResultBases.SequenceEqual(Expected.ResultBases)
            || rules.MatchScoring.Win != Expected.MatchScoring.Win
            || rules.MatchScoring.Draw != Expected.MatchScoring.Draw
            || rules.BonusScoring != Expected.BonusScoring)
            throw new InvalidDataException("Rules record is not the exact schadensfresse-live-rules-v1 contract.");
    }

    public static string ComputeScoringTableSha256(IReadOnlyList<IReadOnlyList<string>> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(matrix, Options);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}

/// <summary>Fail-closed semantic markdown projection. The deliberately exact grammar prevents prose reinterpretation.</summary>
public static class SchadensfresseRulesMarkdown
{
    public static SchadensfresseLiveRulesV1 ExtractAndValidate(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        // All semantics are present once, in fixed order. The rule document is intentionally a constrained
        // publication payload rather than a free-form prompt note.
        var expected = """
# Schadensfresse Live Rules

Schema version: `schadensfresse-live-rules-v1`

- Tips are visible before the deadline: `false`
- Prediction mode: `exact-score`
- Tie break: `matchday-wins-unless-otherwise-agreed`
- Lead time minutes: `0`

## Result bases

1. `bundesliga` | `1. Bundesliga 2026/27` | `regularTime90Minutes`
2. `dfb-pokal` | `DFB-Pokal 2026/27` | `finalScoreIncludingExtraTimeAndPenaltyShootout`
3. `uefa-champions-league` | `Champions League 2026/27` | `finalScoreIncludingExtraTimeAndPenaltyShootout`

## Match scoring

| result | tendencyPoints | goalDifferencePoints | exactResultPoints |
| --- | ---: | ---: | ---: |
| win | 2 | 3 | 5 |
| draw | 3 | null | 5 |

## Bonus scoring

- Points per correct answer: `9`
- Answer order matters: `false`
""";
        if (!string.Equals(markdown.ReplaceLineEndings("\n").TrimEnd('\n'), expected.ReplaceLineEndings("\n").TrimEnd('\n'), StringComparison.Ordinal))
            throw new InvalidDataException("Community rules markdown does not have the exact v1 semantic projection.");
        return SchadensfresseRulesCanonicalJson.Expected;
    }

    public static string ComputeContentSha256(ReadOnlySpan<byte> markdownBytes) =>
        Convert.ToHexStringLower(SHA256.HashData(markdownBytes));
}

public sealed record SchadensfresseRulesPublicationReadback(string DocumentName, int Version, string ContentSha256);

/// <summary>Shared no-write gate used before rules publication or mixed-competition generation construction.</summary>
public static class SchadensfresseRulesPublicationGate
{
    public const string DocumentName = "community-rules-schadensfresse.md";

    public static void Validate(
        SchadensfresseLiveRulesV1 liveRules,
        DateTimeOffset rulesObservedAt,
        DateTimeOffset now,
        string expectedSchemaVersion,
        string expectedCanonicalSha256,
        ReadOnlySpan<byte> markdownBytes,
        string expectedMarkdownContentSha256,
        string expectedDocumentName,
        int expectedVersion,
        SchadensfresseRulesPublicationReadback? readback)
    {
        var contentHash = ValidateCandidate(liveRules, rulesObservedAt, now, expectedSchemaVersion, expectedCanonicalSha256, markdownBytes, expectedMarkdownContentSha256);
        if (readback is null) throw new InvalidDataException("Immutable community-rules publication readback is required.");
        ValidateReadback(readback, expectedDocumentName, expectedVersion, contentHash);
    }

    public static string ValidateCandidate(
        SchadensfresseLiveRulesV1 liveRules,
        DateTimeOffset rulesObservedAt,
        DateTimeOffset now,
        string expectedSchemaVersion,
        string expectedCanonicalSha256,
        ReadOnlySpan<byte> markdownBytes,
        string expectedMarkdownContentSha256)
    {
        SchadensfresseRulesCanonicalJson.ValidateSemanticValue(liveRules);
        if (rulesObservedAt > now || now - rulesObservedAt > TimeSpan.FromHours(24))
            throw new InvalidDataException("Live rules observation is future-dated or older than 24 hours.");
        if (!string.Equals(expectedSchemaVersion, SchadensfresseRulesCanonicalJson.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(expectedCanonicalSha256, SchadensfresseRulesCanonicalJson.CanonicalSha256, StringComparison.Ordinal)
            || !string.Equals(SchadensfresseRulesCanonicalJson.ComputeSha256(liveRules), expectedCanonicalSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Live rules schema or canonical SHA-256 does not match the routing seed.");
        var markdown = new UTF8Encoding(false, true).GetString(markdownBytes);
        var markdownRules = SchadensfresseRulesMarkdown.ExtractAndValidate(markdown);
        if (!SchadensfresseRulesCanonicalJson.Serialize(markdownRules).SequenceEqual(SchadensfresseRulesCanonicalJson.Serialize(liveRules)))
            throw new InvalidDataException("Markdown rules do not equal the authenticated live rules.");
        var contentHash = SchadensfresseRulesMarkdown.ComputeContentSha256(markdownBytes);
        if (!string.Equals(contentHash, expectedMarkdownContentSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Community rules markdown bytes do not match the seed content SHA-256.");
        return contentHash;
    }

    public static void ValidateReadback(
        SchadensfresseRulesPublicationReadback readback,
        string expectedDocumentName,
        int expectedVersion,
        string expectedContentSha256)
    {
        ArgumentNullException.ThrowIfNull(readback);
        if (!string.Equals(expectedDocumentName, DocumentName, StringComparison.Ordinal)
            || !string.Equals(readback.DocumentName, expectedDocumentName, StringComparison.Ordinal)
            || expectedVersion < 0
            || readback.Version != expectedVersion
            || !string.Equals(readback.ContentSha256, expectedContentSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Immutable community-rules publication readback does not match the exact expected version, name, or markdown bytes.");
    }
}
