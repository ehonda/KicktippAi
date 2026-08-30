using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using NodaTime;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaSeasonFixtureRoutingIdentity(string KicktippFixtureId, BundesligaSeasonSubcompetition BundesligaSeasonSubcompetition, string KicktippRoundName, ResultBasis ResultBasis);
public sealed record BundesligaSeasonBonusRoutingIdentity(string KicktippQuestionId, string Text, Instant Deadline, int MaxSelections, BundesligaSeasonSubcompetition BundesligaSeasonSubcompetition, IReadOnlyList<BonusQuestionOption> Options, string EvidenceOptionSetSha256);

/// <summary>Exact-ID routing seed. Incomplete live fixture metadata is deliberately not represented.</summary>
public sealed class BundesligaSeasonRoutingSeed
{
    public const string RelativePath = "data/bundesliga-2026-27/schadensfresse-routing-seed.json";
    private const string ResourceName = "EHonda.KicktippAi.Core.Data.Bundesliga2026_27SchadensfresseRoutingSeed.json";
    private static readonly Lazy<BundesligaSeasonRoutingSeed> DefaultSeed = new(LoadEmbedded);
    private readonly IReadOnlyDictionary<string, BundesligaSeasonFixtureRoutingIdentity> _fixtures;
    private readonly IReadOnlyDictionary<string, BundesligaSeasonBonusRoutingIdentity> _questions;

    private BundesligaSeasonRoutingSeed(IReadOnlyList<BundesligaSeasonFixtureRoutingIdentity> fixtures, IReadOnlyList<BundesligaSeasonBonusRoutingIdentity> questions, string canonicalSha256)
    {
        Fixtures = fixtures; Questions = questions; CanonicalSha256 = canonicalSha256;
        _fixtures = fixtures.ToDictionary(item => item.KicktippFixtureId, StringComparer.Ordinal);
        _questions = questions.ToDictionary(item => item.KicktippQuestionId, StringComparer.Ordinal);
    }

    public static BundesligaSeasonRoutingSeed Default => DefaultSeed.Value;
    public IReadOnlyList<BundesligaSeasonFixtureRoutingIdentity> Fixtures { get; }
    public IReadOnlyList<BundesligaSeasonBonusRoutingIdentity> Questions { get; }
    public string CanonicalSha256 { get; }
    public bool TryGetFixture(string? id, [NotNullWhen(true)] out BundesligaSeasonFixtureRoutingIdentity? identity)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            identity = null;
            return false;
        }

        return _fixtures.TryGetValue(id, out identity);
    }

    public bool TryGetQuestion(string? id, [NotNullWhen(true)] out BundesligaSeasonBonusRoutingIdentity? identity)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            identity = null;
            return false;
        }

        return _questions.TryGetValue(id, out identity);
    }

    public static BundesligaSeasonRoutingSeed Parse(string content, string sourceName = RelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        try
        {
            var document = JsonSerializer.Deserialize<SeedDocument>(content, JsonOptions) ?? throw Invalid(sourceName, "JSON content is empty");
            if (document.SchemaVersion != 1 || document.SeasonPartition != CompetitionIds.Bundesliga2026_27) throw Invalid(sourceName, "schemaVersion must be 1 and seasonPartition must be bundesliga-2026-27");
            var fixtures = (document.Fixtures ?? []).Select(item => new BundesligaSeasonFixtureRoutingIdentity(Required(item.KicktippFixtureId, sourceName, "fixture ID"), ParseSubcompetition(item.BundesligaSeasonSubcompetition, sourceName), Required(item.KicktippRoundName, sourceName, "fixture round"), ParseResultBasis(item.ResultBasis, sourceName))).ToArray();
            EnsureUnique(fixtures.Select(item => item.KicktippFixtureId), sourceName, "fixture ID");
            var questions = (document.Questions ?? []).Select(item => new BundesligaSeasonBonusRoutingIdentity(Required(item.KicktippQuestionId, sourceName, "question ID"), Required(item.Text, sourceName, "question text"), ParseInstant(item.Deadline, sourceName), item.MaxSelections, ParseSubcompetition(item.BundesligaSeasonSubcompetition, sourceName), ParseOptions(item.Options, sourceName), Required(item.EvidenceOptionSetSha256, sourceName, "option set evidence SHA-256"))).ToArray();
            EnsureUnique(questions.Select(item => item.KicktippQuestionId), sourceName, "question ID");
            foreach (var question in questions)
            {
                if (question.MaxSelections < 1 || question.MaxSelections > question.Options.Count) throw Invalid(sourceName, $"question '{question.KicktippQuestionId}' has invalid maxSelections");
                EnsureUnique(question.Options.Select(option => option.Id), sourceName, $"option ID for question '{question.KicktippQuestionId}'");
                EnsureUnique(question.Options.Select(option => option.Text), sourceName, $"option text for question '{question.KicktippQuestionId}'");
                if (!IsLowercaseSha256(question.EvidenceOptionSetSha256)
                    || !string.Equals(
                        ComputeEvidenceOptionSetSha256(question.Options),
                        question.EvidenceOptionSetSha256,
                        StringComparison.Ordinal))
                {
                    throw Invalid(sourceName, $"question '{question.KicktippQuestionId}' option set evidence SHA-256 does not match its exact ordered options");
                }
            }
            var actualHash = ComputeCanonicalSha256(fixtures, questions);
            if (!string.Equals(actualHash, document.CanonicalSha256, StringComparison.Ordinal)) throw Invalid(sourceName, $"canonicalSha256 does not match exact seed content (expected {actualHash})");
            return new BundesligaSeasonRoutingSeed(fixtures, questions, actualHash);
        }
        catch (JsonException exception) { throw Invalid(sourceName, "JSON parsing failed", exception); }
    }

    private static IReadOnlyList<BonusQuestionOption> ParseOptions(SeedOption[]? options, string sourceName)
    {
        if (options is not { Length: > 0 }) throw Invalid(sourceName, "question options are missing");
        return options.Select(option => new BonusQuestionOption(Required(option.Id, sourceName, "option ID"), Required(option.Text, sourceName, "option text"))).ToArray();
    }
    private static Instant ParseInstant(string? value, string sourceName) => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed) ? Instant.FromDateTimeOffset(parsed) : throw Invalid(sourceName, $"deadline '{value}' is not a UTC instant");
    private static BundesligaSeasonSubcompetition ParseSubcompetition(string? value, string sourceName) => BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(value, out var parsed) ? parsed : throw Invalid(sourceName, $"unknown Bundesliga season subcompetition '{value}'");
    private static ResultBasis ParseResultBasis(string? value, string sourceName) => BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(value, out var parsed) ? parsed : throw Invalid(sourceName, $"unknown result basis '{value}'");
    private static string Required(string? value, string sourceName, string field) => string.IsNullOrWhiteSpace(value) ? throw Invalid(sourceName, $"{field} is required") : value;
    private static void EnsureUnique(IEnumerable<string> values, string sourceName, string field) { var duplicate = values.GroupBy(value => value, StringComparer.Ordinal).FirstOrDefault(group => group.Count() != 1); if (duplicate is not null) throw Invalid(sourceName, $"duplicate {field} '{duplicate.Key}'"); }
    private static bool IsLowercaseSha256(string value) => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string ComputeEvidenceOptionSetSha256(IReadOnlyList<BonusQuestionOption> options) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(options))));
    private static string ComputeCanonicalSha256(IEnumerable<BundesligaSeasonFixtureRoutingIdentity> fixtures, IEnumerable<BundesligaSeasonBonusRoutingIdentity> questions)
    {
        var builder = new StringBuilder("bundesliga-season-routing-v1\n");
        foreach (var fixture in fixtures.OrderBy(item => item.KicktippFixtureId, StringComparer.Ordinal)) { Append(builder, fixture.KicktippFixtureId); Append(builder, fixture.BundesligaSeasonSubcompetition.ToSerializedValue()); Append(builder, fixture.KicktippRoundName); Append(builder, fixture.ResultBasis.ToSerializedValue()); }
        foreach (var question in questions.OrderBy(item => item.KicktippQuestionId, StringComparer.Ordinal)) { Append(builder, question.KicktippQuestionId); Append(builder, question.Text); Append(builder, question.Deadline.ToDateTimeOffset().ToString("O", System.Globalization.CultureInfo.InvariantCulture)); Append(builder, question.MaxSelections.ToString(System.Globalization.CultureInfo.InvariantCulture)); Append(builder, question.BundesligaSeasonSubcompetition.ToSerializedValue()); Append(builder, question.EvidenceOptionSetSha256); foreach (var option in question.Options) { Append(builder, option.Id); Append(builder, option.Text); } }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
    private static void Append(StringBuilder builder, string value) => builder.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value).Append('\n');
    private static BundesligaSeasonRoutingSeed LoadEmbedded() { using var stream = typeof(BundesligaSeasonRoutingSeed).Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException($"Embedded routing seed '{ResourceName}' was not found."); using var reader = new StreamReader(stream, Encoding.UTF8, true); return Parse(reader.ReadToEnd()); }
    private static InvalidDataException Invalid(string sourceName, string reason, Exception? inner = null) => new($"Invalid Bundesliga season routing seed '{sourceName}': {reason}.", inner);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record SeedDocument(int SchemaVersion, string? SeasonPartition, SeedFixture[]? Fixtures, SeedQuestion[]? Questions, string? CanonicalSha256);
    private sealed record SeedFixture(string? KicktippFixtureId, string? BundesligaSeasonSubcompetition, string? KicktippRoundName, string? ResultBasis);
    private sealed record SeedQuestion(string? KicktippQuestionId, string? Text, string? Deadline, int MaxSelections, string? BundesligaSeasonSubcompetition, SeedOption[]? Options, string? EvidenceOptionSetSha256);
    private sealed record SeedOption(string? Id, string? Text);
}

/// <summary>Fail-closed exact-ID classifier for use before Bundesliga-season service construction.</summary>
public sealed class BundesligaSeasonRoutingClassifier(BundesligaSeasonRoutingSeed seed)
{
    public bool TryClassifyMatch(string? seasonPartition, Match? match, [NotNullWhen(true)] out BundesligaSeasonFixtureRoutingIdentity? identity)
    {
        identity = null;
        if (!IsBundesligaSeason(seasonPartition) || match is null || string.IsNullOrWhiteSpace(match.KicktippFixtureId) || string.IsNullOrWhiteSpace(match.KicktippRoundName) || match.ResultBasis is null || match.BundesligaSeasonSubcompetition is null || !seed.TryGetFixture(match.KicktippFixtureId, out var expected) || !string.Equals(match.KicktippRoundName, expected.KicktippRoundName, StringComparison.Ordinal) || match.ResultBasis != expected.ResultBasis || match.BundesligaSeasonSubcompetition != expected.BundesligaSeasonSubcompetition) return false;
        identity = expected; return true;
    }
    public bool TryClassifyBonusQuestion(string? seasonPartition, BonusQuestion? question, [NotNullWhen(true)] out BundesligaSeasonBonusRoutingIdentity? identity)
    {
        identity = null;
        if (!IsBundesligaSeason(seasonPartition) || question is null || string.IsNullOrWhiteSpace(question.KicktippQuestionId) || question.BundesligaSeasonSubcompetition is null || !seed.TryGetQuestion(question.KicktippQuestionId, out var expected) || question.BundesligaSeasonSubcompetition != expected.BundesligaSeasonSubcompetition || !string.Equals(question.Text, expected.Text, StringComparison.Ordinal) || question.MaxSelections != expected.MaxSelections || question.Deadline.ToInstant() != expected.Deadline || question.Options is null || question.Options.Count != expected.Options.Count || !question.Options.SequenceEqual(expected.Options)) return false;
        identity = expected; return true;
    }
    private static bool IsBundesligaSeason(string? seasonPartition) => string.Equals(seasonPartition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal);
}
