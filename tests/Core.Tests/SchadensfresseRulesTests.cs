using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class SchadensfresseRulesTests
{
    private static readonly byte[] CanonicalBytes = SchadensfresseRulesCanonicalJson.Serialize(
        SchadensfresseRulesCanonicalJson.Expected);

    [Test]
    public async Task Canonical_v1_bytes_have_the_ADR_exact_length_hash_order_explicit_null_and_round_trip()
    {
        await Assert.That(CanonicalBytes.Length).IsEqualTo(822);
        await Assert.That(CanonicalBytes.Take(3).SequenceEqual([(byte)0xEF, (byte)0xBB, (byte)0xBF])).IsFalse();
        await Assert.That(CanonicalBytes[^1]).IsNotEqualTo((byte)'\n').And.IsNotEqualTo((byte)'\r');
        await Assert.That(Encoding.UTF8.GetString(CanonicalBytes)).Contains("\"goalDifferencePoints\":null");
        await Assert.That(SchadensfresseRulesCanonicalJson.ComputeSha256(SchadensfresseRulesCanonicalJson.Expected))
            .IsEqualTo(SchadensfresseRulesCanonicalJson.CanonicalSha256);
        var roundTrip = SchadensfresseRulesCanonicalJson.DeserializeCanonical(CanonicalBytes);
        await Assert.That(SchadensfresseRulesCanonicalJson.Serialize(roundTrip)).IsEquivalentTo(CanonicalBytes);
    }

    [Test]
    public async Task Canonical_JSON_systematically_rejects_extra_missing_null_wrong_type_and_order_mutations()
    {
        var canonical = Encoding.UTF8.GetString(CanonicalBytes);
        var mutations = new Dictionary<string, Func<string>>(StringComparer.Ordinal)
        {
            ["extra root property"] = () => canonical.Replace("{", "{\"extra\":true,", StringComparison.Ordinal),
            ["missing schema"] = () => RemoveProperty(canonical, "schemaVersion"),
            ["missing false boolean"] = () => RemoveProperty(canonical, "tipsVisibleBeforeDeadline"),
            ["missing zero number"] = () => RemoveProperty(canonical, "leadTimeMinutes"),
            ["missing result bases"] = () => RemoveProperty(canonical, "resultBases"),
            ["missing nested property"] = () => RemoveProperty(canonical, "answerOrderMatters"),
            ["null schema"] = () => ReplacePropertyValue(canonical, "schemaVersion", JsonValue.Create((string?)null)),
            ["null result bases"] = () => ReplacePropertyValue(canonical, "resultBases", null),
            ["null match scoring"] = () => ReplacePropertyValue(canonical, "matchScoring", null),
            ["null bonus scoring"] = () => ReplacePropertyValue(canonical, "bonusScoring", null),
            ["wrong boolean type"] = () => ReplacePropertyValue(canonical, "tipsVisibleBeforeDeadline", "false"),
            ["wrong number type"] = () => ReplacePropertyValue(canonical, "leadTimeMinutes", "0"),
            ["wrong object type"] = () => ReplacePropertyValue(canonical, "matchScoring", "scores"),
            ["wrong array type"] = () => ReplacePropertyValue(canonical, "resultBases", new JsonObject()),
            ["root property order"] = () => MovePropertyToEnd(canonical, "schemaVersion"),
            ["nested property order"] = () => MovePropertyToEnd(canonical, "tendencyPoints"),
            ["array order"] = () => SwapArrayEntries(canonical, 0, 1),
            ["array missing"] = () => RemoveArrayEntry(canonical, 1),
            ["array extra"] = () => AddArrayEntry(canonical),
            ["wrong enum"] = () => canonical.Replace("regularTime90Minutes", "regular-time", StringComparison.Ordinal),
            ["duplicate property"] = () => canonical.Replace("{", "{\"schemaVersion\":\"schadensfresse-live-rules-v1\",", StringComparison.Ordinal),
            ["leading whitespace"] = () => " " + canonical,
            ["trailing whitespace"] = () => canonical + " ",
            ["terminal LF"] = () => canonical + "\n",
            ["terminal CRLF"] = () => canonical + "\r\n",
            ["BOM"] = () => "\uFEFF" + canonical
        };

        foreach (var (name, mutation) in mutations)
        {
            var bytes = Encoding.UTF8.GetBytes(mutation());
            try
            {
                SchadensfresseRulesCanonicalJson.DeserializeCanonical(bytes);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            throw new InvalidOperationException($"Canonical JSON mutation '{name}' was unexpectedly accepted.");
        }
    }

    [Test]
    public async Task Markdown_projection_systematically_rejects_missing_duplicate_extra_ambiguous_and_contradictory_claims()
    {
        var bytes = ReadMarkdownBytes();
        var markdown = Encoding.UTF8.GetString(bytes);
        var mutations = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
        {
            ["missing schema"] = value => value.Replace("Schema version: `schadensfresse-live-rules-v1`\n\n", "", StringComparison.Ordinal),
            ["missing root claim"] = value => value.Replace("- Prediction mode: `exact-score`\n", "", StringComparison.Ordinal),
            ["duplicate root claim"] = value => value.Replace("- Prediction mode: `exact-score`\n", "- Prediction mode: `exact-score`\n- Prediction mode: `exact-score`\n", StringComparison.Ordinal),
            ["extra semantic claim"] = value => value + "\n- Lead time minutes: `1`\n",
            ["ambiguous visibility"] = value => value + "\nTips may be visible before the deadline.\n",
            ["contradictory score"] = value => value + "\nA win exact result awards 4 points.\n",
            ["contradictory result basis"] = value => value + "\nBundesliga is scored after extra time.\n",
            ["contradictory lead time"] = value => value + "\nTips close 5 minutes early.\n",
            ["contradictory bonus"] = value => value + "\nCorrect bonus answers award 8 points.\n",
            ["missing result basis"] = value => value.Replace("2. `dfb-pokal` | `DFB-Pokal 2026/27` | `finalScoreIncludingExtraTimeAndPenaltyShootout`\n", "", StringComparison.Ordinal),
            ["duplicate result basis"] = value => value.Replace("2. `dfb-pokal`", "2. `bundesliga`", StringComparison.Ordinal),
            ["reordered result bases"] = value => SwapLines(value, "1. `bundesliga`", "2. `dfb-pokal`"),
            ["missing score row"] = value => value.Replace("| draw | 3 | null | 5 |\n", "", StringComparison.Ordinal),
            ["duplicate score row"] = value => value.Replace("| draw | 3 | null | 5 |\n", "| draw | 3 | null | 5 |\n| draw | 3 | null | 5 |\n", StringComparison.Ordinal),
            ["ambiguous null sentinel"] = value => value.Replace("| draw | 3 | null | 5 |", "| draw | 3 | - | 5 |", StringComparison.Ordinal),
            ["missing bonus claim"] = value => value.Replace("- Answer order matters: `false`\n", "", StringComparison.Ordinal),
            ["unparsable value"] = value => value.Replace("- Lead time minutes: `0`", "- Lead time minutes: zero", StringComparison.Ordinal)
        };

        foreach (var (name, mutation) in mutations)
        {
            var mutated = mutation(markdown);
            try
            {
                SchadensfresseRulesMarkdown.ExtractAndValidate(mutated);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            throw new InvalidOperationException($"Markdown mutation '{name}' was unexpectedly accepted.");
        }
    }

    [Test]
    public async Task Publication_gate_rejects_each_schema_canonical_content_name_version_and_freshness_mismatch()
    {
        var bytes = ReadMarkdownBytes();
        var seed = BundesligaSeasonRoutingSeed.Default;
        var hash = SchadensfresseRulesMarkdown.ComputeContentSha256(bytes);
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var readback = new SchadensfresseRulesPublicationReadback(
            SchadensfresseRulesPublicationGate.DocumentName,
            7,
            hash);

        SchadensfresseRulesPublicationGate.Validate(
            SchadensfresseRulesCanonicalJson.Expected,
            now,
            now,
            seed.RulesSchemaVersion,
            seed.CanonicalRulesSha256,
            bytes,
            hash,
            SchadensfresseRulesPublicationGate.DocumentName,
            7,
            readback);

        var failures = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            ["schema mismatch"] = () => Validate(seed.RulesSchemaVersion + "-drift", seed.CanonicalRulesSha256, bytes, hash, now, now, 7, readback),
            ["canonical mismatch"] = () => Validate(seed.RulesSchemaVersion, new string('0', 64), bytes, hash, now, now, 7, readback),
            ["file content mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes.Concat([(byte)'x']).ToArray(), hash, now, now, 7, readback),
            ["seed content mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, new string('0', 64), now, now, 7, readback),
            ["name mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 7, readback with { DocumentName = "latest" }),
            ["expected name mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 7, readback, "latest"),
            ["expected version mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 8, readback),
            ["readback version mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 7, readback with { Version = 8 }),
            ["readback content mismatch"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 7, readback with { ContentSha256 = new string('0', 64) }),
            ["missing readback"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, 7, null),
            ["negative expected version"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now, now, -1, readback),
            ["stale observation"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now.AddHours(-24).AddTicks(-1), now, 7, readback),
            ["future observation"] = () => Validate(seed.RulesSchemaVersion, seed.CanonicalRulesSha256, bytes, hash, now.AddTicks(1), now, 7, readback)
        };

        foreach (var (name, failure) in failures)
        {
            try
            {
                failure();
            }
            catch (InvalidDataException)
            {
                continue;
            }

            throw new InvalidOperationException($"Publication mismatch '{name}' was unexpectedly accepted.");
        }
    }

    [Test]
    public async Task Atomic_save_result_preserves_legacy_created_version_and_distinguishes_effective_version()
    {
        var created = new ContextDocumentSaveResult("rules", 4, 4);
        var unchanged = new ContextDocumentSaveResult("rules", null, 4);
        var legacy = new ContextDocumentSaveResult("rules", null);

        await Assert.That(created.Version).IsEqualTo(4);
        await Assert.That(created.CreatedVersion).IsEqualTo(4);
        await Assert.That(created.EffectiveVersion).IsEqualTo(4);
        await Assert.That(unchanged.Version).IsNull();
        await Assert.That(unchanged.CreatedVersion).IsNull();
        await Assert.That(unchanged.EffectiveVersion).IsEqualTo(4);
        await Assert.That(legacy.Version).IsNull();
        await Assert.That(legacy.EffectiveVersion).IsNull();
    }

    private static void Validate(
        string schema,
        string canonicalHash,
        byte[] markdown,
        string contentHash,
        DateTimeOffset observedAt,
        DateTimeOffset now,
        int expectedVersion,
        SchadensfresseRulesPublicationReadback? readback,
        string expectedDocumentName = SchadensfresseRulesPublicationGate.DocumentName) =>
        SchadensfresseRulesPublicationGate.Validate(
            SchadensfresseRulesCanonicalJson.Expected,
            observedAt,
            now,
            schema,
            canonicalHash,
            markdown,
            contentHash,
            expectedDocumentName,
            expectedVersion,
            readback);

    private static byte[] ReadMarkdownBytes() => File.ReadAllBytes(Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        "community-rules",
        "schadensfresse.md"));

    private static string RemoveProperty(string json, string property)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        if (RemoveProperty(root, property)) return root.ToJsonString();
        throw new InvalidOperationException($"Property '{property}' was not found.");
    }

    private static bool RemoveProperty(JsonNode node, string property)
    {
        if (node is JsonObject obj)
        {
            if (obj.Remove(property)) return true;
            foreach (var child in obj.Select(pair => pair.Value).Where(value => value is not null))
            {
                if (RemoveProperty(child!, property)) return true;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.Where(value => value is not null))
            {
                if (RemoveProperty(child!, property)) return true;
            }
        }

        return false;
    }

    private static string ReplacePropertyValue(string json, string property, JsonNode? replacement)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        if (!ReplacePropertyValue(root, property, replacement))
            throw new InvalidOperationException($"Property '{property}' was not found.");
        return root.ToJsonString();
    }

    private static bool ReplacePropertyValue(JsonNode node, string property, JsonNode? replacement)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey(property))
            {
                obj[property] = replacement;
                return true;
            }
            foreach (var child in obj.Select(pair => pair.Value).Where(value => value is not null))
            {
                if (ReplacePropertyValue(child!, property, replacement)) return true;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.Where(value => value is not null))
            {
                if (ReplacePropertyValue(child!, property, replacement)) return true;
            }
        }

        return false;
    }

    private static string MovePropertyToEnd(string json, string property)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        if (!MovePropertyToEnd(root, property)) throw new InvalidOperationException($"Property '{property}' was not found.");
        return root.ToJsonString();
    }

    private static bool MovePropertyToEnd(JsonNode node, string property)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue(property, out var value))
            {
                obj.Remove(property);
                obj.Add(property, value);
                return true;
            }
            foreach (var child in obj.Select(pair => pair.Value).Where(value => value is not null))
            {
                if (MovePropertyToEnd(child!, property)) return true;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.Where(value => value is not null))
            {
                if (MovePropertyToEnd(child!, property)) return true;
            }
        }

        return false;
    }

    private static string SwapArrayEntries(string json, int first, int second)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var array = root["resultBases"]!.AsArray();
        var firstNode = array[first]!.DeepClone();
        var secondNode = array[second]!.DeepClone();
        array[first] = secondNode;
        array[second] = firstNode;
        return root.ToJsonString();
    }

    private static string RemoveArrayEntry(string json, int index)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root["resultBases"]!.AsArray().RemoveAt(index);
        return root.ToJsonString();
    }

    private static string AddArrayEntry(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var array = root["resultBases"]!.AsArray();
        array.Add(array[0]!.DeepClone());
        return root.ToJsonString();
    }

    private static string SwapLines(string value, string firstPrefix, string secondPrefix)
    {
        var lines = value.Split('\n').ToList();
        var first = lines.FindIndex(line => line.StartsWith(firstPrefix, StringComparison.Ordinal));
        var second = lines.FindIndex(line => line.StartsWith(secondPrefix, StringComparison.Ordinal));
        (lines[first], lines[second]) = (lines[second], lines[first]);
        return string.Join('\n', lines);
    }
}
