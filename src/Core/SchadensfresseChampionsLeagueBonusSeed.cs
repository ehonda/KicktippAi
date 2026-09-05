using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public sealed record SchadensfresseChampionsLeagueBonusSeedOption(string Id, string Text);

public sealed record SchadensfresseChampionsLeagueBonusSeedQuestion(
    string KicktippQuestionId,
    string Text,
    string Deadline,
    int MaxSelections,
    IReadOnlyList<string> FormKeys,
    IReadOnlyList<SchadensfresseChampionsLeagueBonusSeedOption> Options,
    string LiveEvidenceOptionArraySha256,
    string DefinitionSha256);

/// <summary>Strict, embedded seed for the one frozen Schadensfresse CL bonus route.</summary>
public sealed class SchadensfresseChampionsLeagueBonusSeed
{
    public const string RelativePath = "data/bundesliga-2026-27/schadensfresse-champions-league-bonus.json";
    private const string ResourceName = "EHonda.KicktippAi.Core.Data.SchadensfresseChampionsLeagueBonus.json";
    private static readonly string[] RootProperties =
        ["schemaVersion", "profileId", "sourceSnapshotSha256", "questions", "questionSetSha256", "historicalEvidenceQuestionSetSha256"];
    private static readonly string[] QuestionProperties =
        ["kicktippQuestionId", "text", "deadline", "maxSelections", "formKeys", "optionCount", "options", "liveEvidenceOptionArraySha256", "definitionSha256"];
    private static readonly string[] OptionProperties = ["id", "text"];
    private static readonly string[] OrderedQuestionIds = ["1662326752", "1662326753", "1662326754"];
    private static readonly Lazy<SchadensfresseChampionsLeagueBonusSeed> DefaultSeed = new(LoadEmbedded);

    private readonly IReadOnlyDictionary<string, SchadensfresseChampionsLeagueBonusSeedQuestion> _byId;

    private SchadensfresseChampionsLeagueBonusSeed(
        IReadOnlyList<SchadensfresseChampionsLeagueBonusSeedQuestion> questions,
        string sourceSnapshotSha256,
        string questionSetSha256,
        string historicalEvidenceQuestionSetSha256)
    {
        Questions = questions;
        SourceSnapshotSha256 = sourceSnapshotSha256;
        QuestionSetSha256 = questionSetSha256;
        HistoricalEvidenceQuestionSetSha256 = historicalEvidenceQuestionSetSha256;
        _byId = questions.ToDictionary(question => question.KicktippQuestionId, StringComparer.Ordinal);
    }

    public static SchadensfresseChampionsLeagueBonusSeed Default => DefaultSeed.Value;
    public IReadOnlyList<SchadensfresseChampionsLeagueBonusSeedQuestion> Questions { get; }
    public string SourceSnapshotSha256 { get; }
    public string QuestionSetSha256 { get; }
    public string HistoricalEvidenceQuestionSetSha256 { get; }

    public SchadensfresseChampionsLeagueBonusSeedQuestion GetQuestion(string questionId) =>
        _byId.TryGetValue(questionId, out var question)
            ? question
            : throw new KeyNotFoundException($"Question '{questionId}' is not in the frozen CL bonus seed.");

    public static SchadensfresseChampionsLeagueBonusSeed Parse(ReadOnlySpan<byte> bytes, string sourceName = RelativePath)
    {
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            throw Invalid(sourceName, "a UTF-8 BOM is not permitted");
        }

        try
        {
            var json = new UTF8Encoding(false, true).GetString(bytes);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var root = RequireObject(document.RootElement, RootProperties, "root", sourceName);
            RequireInt(root[0].Value, "schemaVersion", sourceName, expected: 1);
            RequireExact(root[1].Value, "profileId", SchadensfresseChampionsLeagueBonusProfile.ProfileId, sourceName);
            var sourceHash = RequireCanonicalString(root[2].Value, "sourceSnapshotSha256", sourceName);
            RequireExact(sourceHash, SchadensfresseChampionsLeagueBonusProfile.SourceSnapshotSha256, "sourceSnapshotSha256", sourceName);
            if (root[3].Value.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(sourceName, "questions must be an array");
            }

            var questions = root[3].Value.EnumerateArray().Select((element, index) => ParseQuestion(element, index, sourceName)).ToArray();
            if (!questions.Select(question => question.KicktippQuestionId).SequenceEqual(OrderedQuestionIds, StringComparer.Ordinal)
                || questions.Sum(question => question.Options.Count) != 108)
            {
                throw Invalid(sourceName, "questions must contain the exact ordered three IDs and 108 option identities");
            }

            var setHash = RequireCanonicalString(root[4].Value, "questionSetSha256", sourceName);
            var historicalHash = RequireCanonicalString(root[5].Value, "historicalEvidenceQuestionSetSha256", sourceName);
            RequireExact(setHash, ComputeQuestionSetSha256(questions), "questionSetSha256", sourceName);
            RequireExact(setHash, SchadensfresseChampionsLeagueBonusProfile.QuestionSetSha256, "questionSetSha256", sourceName);
            RequireExact(historicalHash, SchadensfresseChampionsLeagueBonusProfile.HistoricalEvidenceQuestionSetSha256, "historicalEvidenceQuestionSetSha256", sourceName);
            return new SchadensfresseChampionsLeagueBonusSeed(questions, sourceHash, setHash, historicalHash);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or FormatException or OverflowException)
        {
            throw Invalid(sourceName, "strict UTF-8 JSON parsing failed", exception);
        }
    }

    public static string ComputeQuestionDefinitionSha256(SchadensfresseChampionsLeagueBonusSeedQuestion question)
    {
        using var stream = new MemoryStream();
        WriteLiteral(stream, "cl-bonus-question-definition-v1\n");
        Append(stream, "schemaVersion", "1");
        Append(stream, "kicktippQuestionId", question.KicktippQuestionId);
        Append(stream, "text", question.Text);
        Append(stream, "deadline", question.Deadline);
        Append(stream, "maxSelections", question.MaxSelections.ToString(CultureInfo.InvariantCulture));
        Append(stream, "formKeyCount", question.FormKeys.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < question.FormKeys.Count; index++) Append(stream, $"formKey[{index}]", question.FormKeys[index]);
        Append(stream, "optionCount", question.Options.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < question.Options.Count; index++)
        {
            Append(stream, $"option[{index}].id", question.Options[index].Id);
            Append(stream, $"option[{index}].text", question.Options[index].Text);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static SchadensfresseChampionsLeagueBonusSeedQuestion ParseQuestion(JsonElement element, int index, string sourceName)
    {
        var properties = RequireObject(element, QuestionProperties, $"questions[{index}]", sourceName);
        var id = RequireCanonicalString(properties[0].Value, $"questions[{index}].kicktippQuestionId", sourceName);
        var text = RequireText(properties[1].Value, $"questions[{index}].text", sourceName);
        var deadline = RequireCanonicalString(properties[2].Value, $"questions[{index}].deadline", sourceName);
        RequireExact(deadline, SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc, $"questions[{index}].deadline", sourceName);
        var maxSelections = RequirePositiveInt(properties[3].Value, $"questions[{index}].maxSelections", sourceName);
        var formKeys = RequireStringArray(properties[4].Value, $"questions[{index}].formKeys", sourceName);
        var optionCount = RequirePositiveInt(properties[5].Value, $"questions[{index}].optionCount", sourceName);
        if (properties[6].Value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourceName, $"questions[{index}].options must be an array");
        }
        var options = properties[6].Value.EnumerateArray().Select((option, optionIndex) =>
        {
            var pair = RequireObject(option, OptionProperties, $"questions[{index}].options[{optionIndex}]", sourceName);
            return new SchadensfresseChampionsLeagueBonusSeedOption(
                RequireCanonicalString(pair[0].Value, $"questions[{index}].options[{optionIndex}].id", sourceName),
                RequireText(pair[1].Value, $"questions[{index}].options[{optionIndex}].text", sourceName));
        }).ToArray();
        if (optionCount != 36 || options.Length != optionCount
            || options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != optionCount
            || options.Select(option => option.Text).Distinct(StringComparer.Ordinal).Count() != optionCount)
        {
            throw Invalid(sourceName, $"questions[{index}] must contain 36 distinct ordered option IDs and texts");
        }
        if (formKeys.Length != maxSelections || formKeys.Distinct(StringComparer.Ordinal).Count() != formKeys.Length)
        {
            throw Invalid(sourceName, $"questions[{index}] form keys must be distinct and equal maxSelections");
        }

        var optionHash = RequireCanonicalString(properties[7].Value, $"questions[{index}].liveEvidenceOptionArraySha256", sourceName);
        RequireExact(optionHash, ComputeOptionArraySha256(options), $"questions[{index}].liveEvidenceOptionArraySha256", sourceName);
        var definitionHash = RequireCanonicalString(properties[8].Value, $"questions[{index}].definitionSha256", sourceName);
        var parsed = new SchadensfresseChampionsLeagueBonusSeedQuestion(id, text, deadline, maxSelections, formKeys, options, optionHash, definitionHash);
        RequireExact(definitionHash, ComputeQuestionDefinitionSha256(parsed), $"questions[{index}].definitionSha256", sourceName);
        return parsed;
    }

    private static string ComputeQuestionSetSha256(IReadOnlyList<SchadensfresseChampionsLeagueBonusSeedQuestion> questions)
    {
        using var stream = new MemoryStream();
        WriteLiteral(stream, "cl-bonus-question-set-v1\n");
        Append(stream, "schemaVersion", "1");
        Append(stream, "profileId", SchadensfresseChampionsLeagueBonusProfile.ProfileId);
        Append(stream, "questionCount", questions.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < questions.Count; index++) Append(stream, $"question[{index}].definitionSha256", questions[index].DefinitionSha256);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static string ComputeOptionArraySha256(IReadOnlyList<SchadensfresseChampionsLeagueBonusSeedOption> options)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options.Select(option => new { id = option.Id, text = option.Text }), new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = null
        });
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static JsonProperty[] RequireObject(JsonElement element, string[] expected, string path, string sourceName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(sourceName, $"{path} must be an object");
        }
        var properties = element.EnumerateObject().ToArray();
        if (!properties.Select(property => property.Name).SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw Invalid(sourceName, $"{path} has an unknown, missing, duplicate, or out-of-order field");
        }
        return properties;
    }

    private static string[] RequireStringArray(JsonElement element, string path, string sourceName)
    {
        if (element.ValueKind != JsonValueKind.Array) throw Invalid(sourceName, $"{path} must be an array");
        return element.EnumerateArray().Select((value, index) => RequireCanonicalString(value, $"{path}[{index}]", sourceName)).ToArray();
    }

    private static string RequireText(JsonElement element, string path, string sourceName)
    {
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { Length: > 0 } value
            || value.Contains('\0') || !value.IsNormalized(NormalizationForm.FormC))
        {
            throw Invalid(sourceName, $"{path} must be nonempty NFC text without NUL");
        }
        return value;
    }

    private static string RequireCanonicalString(JsonElement element, string path, string sourceName)
    {
        var value = RequireText(element, path, sourceName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)) throw Invalid(sourceName, $"{path} cannot have surrounding whitespace");
        return value;
    }

    private static int RequirePositiveInt(JsonElement element, string path, string sourceName)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value) || value <= 0) throw Invalid(sourceName, $"{path} must be a positive integer");
        return value;
    }

    private static void RequireInt(JsonElement element, string path, string sourceName, int expected)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value) || value != expected) throw Invalid(sourceName, $"{path} must be integer {expected}");
    }

    private static void RequireExact(JsonElement element, string path, string expected, string sourceName) =>
        RequireExact(RequireCanonicalString(element, path, sourceName), expected, path, sourceName);

    private static void RequireExact(string actual, string expected, string path, string sourceName)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw Invalid(sourceName, $"{path} does not match the frozen value");
    }

    private static void Append(Stream stream, string name, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteLiteral(stream, name);
        WriteLiteral(stream, "=");
        WriteLiteral(stream, bytes.Length.ToString(CultureInfo.InvariantCulture));
        WriteLiteral(stream, ":");
        stream.Write(bytes);
        stream.WriteByte((byte)'\n');
    }

    private static void WriteLiteral(Stream stream, string value) => stream.Write(Encoding.UTF8.GetBytes(value));

    private static SchadensfresseChampionsLeagueBonusSeed LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded CL bonus seed '{ResourceName}' was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(buffer.ToArray());
    }

    private static InvalidDataException Invalid(string sourceName, string message, Exception? inner = null) =>
        new($"Invalid Schadensfresse CL bonus seed '{sourceName}': {message}.", inner);
}
