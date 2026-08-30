using EHonda.KicktippAi.Core;
using NodaTime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.Tests;

public class BundesligaSeasonRoutingSeedTests
{
    [Test]
    public async Task Checked_in_seed_has_exact_three_champions_league_questions_and_111_ordered_options()
    {
        var seed = BundesligaSeasonRoutingSeed.Default;

        await Assert.That(seed.Fixtures).IsEquivalentTo([
            new BundesligaSeasonFixtureRoutingIdentity("1662323362", BundesligaSeasonSubcompetition.Bundesliga, "1. Spieltag", ResultBasis.RegularTime90Minutes),
            new BundesligaSeasonFixtureRoutingIdentity("1662323366", BundesligaSeasonSubcompetition.Bundesliga, "1. Spieltag", ResultBasis.RegularTime90Minutes)]);
        await Assert.That(seed.Questions).Count().IsEqualTo(3);
        await Assert.That(seed.Questions.Sum(question => question.Options.Count)).IsEqualTo(111);
        await Assert.That(seed.CanonicalSha256).IsEqualTo("81b1c6ab0a6ad3159fcafebcbf1e3525df2cdf8e1279369f2515f001176008e5");
        await Assert.That(seed.Questions.Select(question => question.KicktippQuestionId)).IsEquivalentTo(["1662326752", "1662326753", "1662326754"]);
        await Assert.That(seed.Questions.Select(question => (question.Text, question.MaxSelections, question.Deadline))).IsEquivalentTo([
            ("CL: Welche Mannschaft stellt den Spieler mit den meisten Toren?", 1, Instant.FromUtc(2026, 9, 8, 16, 45)),
            ("CL: Wer erreicht das Halbfinale?", 4, Instant.FromUtc(2026, 9, 8, 16, 45)),
            ("CL: Wer gewinnt die Champions League?", 1, Instant.FromUtc(2026, 9, 8, 16, 45))]);
        await Assert.That(seed.Questions.Select(question => question.EvidenceOptionSetSha256)).IsEquivalentTo([
            "e29a9636d4d2e4fd7ac48a371dfe650c242e041b006cc3d3fc31986a539f1c55",
            "d1e7ed3827d6d07daf2416edc8862466885f0f80d115886203840850ec1b5920",
            "e5c1f2949d8cb7d8675f901c97fc09e8e24c6df892376f698caf4e6d28c1be9d"]);
        foreach (var question in seed.Questions)
        {
            var independentlyComputed = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(question.Options))));
            await Assert.That(independentlyComputed).IsEqualTo(question.EvidenceOptionSetSha256);
        }
    }

    [Test]
    public async Task Exact_bonus_identity_classifies_only_inside_the_Bundesliga_season_partition()
    {
        var seed = BundesligaSeasonRoutingSeed.Default;
        var expected = seed.Questions.Single(question => question.KicktippQuestionId == "1662326753");
        var question = new BonusQuestion(expected.Text, expected.Deadline.InZone(DateTimeZone.Utc), expected.Options.ToList(), expected.MaxSelections)
        {
            KicktippQuestionId = expected.KicktippQuestionId,
            BundesligaSeasonSubcompetition = expected.BundesligaSeasonSubcompetition
        };
        var classifier = new BundesligaSeasonRoutingClassifier(seed);

        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, question, out var identity)).IsTrue();
        await Assert.That(identity).IsEqualTo(expected);
        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.FifaWorldCup2026, question, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, question with { Text = "CL: Wer erreicht" }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, question with { Options = question.Options.Skip(1).ToList() }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, question with { KicktippQuestionId = "unknown" }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyBonusQuestion(CompetitionIds.Bundesliga2026_27, question with { KicktippQuestionId = null }, out _)).IsFalse();
    }

    [Test]
    public async Task Generic_typed_values_have_the_ADR_exact_serialization()
    {
        await Assert.That(BundesligaSeasonSubcompetition.ChampionsLeague.ToSerializedValue()).IsEqualTo("uefa-champions-league");
        await Assert.That(BundesligaSeasonSubcompetition.DfbPokal.ToSerializedValue()).IsEqualTo("dfb-pokal");
        await Assert.That(ResultBasis.RegularTime90Minutes.ToSerializedValue()).IsEqualTo("regularTime90Minutes");
        await Assert.That(ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout.ToSerializedValue()).IsEqualTo("finalScoreIncludingExtraTimeAndPenaltyShootout");
        await Assert.That(BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition("CL:", out _)).IsFalse();
        await Assert.That(BundesligaSeasonRoutingIdentityValues.TryParseResultBasis("regular", out _)).IsFalse();
        var json = JsonSerializer.Serialize(new { bundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague, resultBasis = ResultBasis.RegularTime90Minutes });
        await Assert.That(json).IsEqualTo("{\"bundesligaSeasonSubcompetition\":\"uefa-champions-league\",\"resultBasis\":\"regularTime90Minutes\"}");

        var match = new Match("Home", "Away", SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZone.Utc), 1)
        {
            KicktippFixtureId = "fixture-1",
            KicktippRoundName = "Exact round",
            ResultBasis = ResultBasis.RegularTime90Minutes,
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga
        };
        var matchJson = JsonSerializer.Serialize(match);
        await Assert.That(matchJson).Contains("\"kicktippFixtureId\":\"fixture-1\"").And.Contains("\"kicktippRoundName\":\"Exact round\"").And.Contains("\"resultBasis\":\"regularTime90Minutes\"").And.Contains("\"bundesligaSeasonSubcompetition\":\"bundesliga\"");
    }

    [Test]
    public async Task Exact_seeded_match_identity_classifies_and_every_incomplete_unknown_or_drifted_value_fails_closed()
    {
        var seed = BundesligaSeasonRoutingSeed.Default;
        var classifier = new BundesligaSeasonRoutingClassifier(seed);
        var match = new Match("SC Freiburg", "Werder Bremen", SystemClock.Instance.GetCurrentInstant().InZone(DateTimeZone.Utc), 1) { KicktippFixtureId = "1662323362", KicktippRoundName = "1. Spieltag", ResultBasis = ResultBasis.RegularTime90Minutes, BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga };

        await Assert.That(classifier.TryClassifyMatch(CompetitionIds.Bundesliga2026_27, match, out var identity)).IsTrue();
        await Assert.That(identity).IsEqualTo(seed.Fixtures[0]);
        await Assert.That(classifier.TryClassifyMatch(CompetitionIds.Bundesliga2026_27, match with { KicktippFixtureId = null }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyMatch(CompetitionIds.Bundesliga2026_27, match with { KicktippFixtureId = "unknown" }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyMatch(CompetitionIds.Bundesliga2026_27, match with { KicktippRoundName = "1. Spiel" }, out _)).IsFalse();
        await Assert.That(classifier.TryClassifyMatch(CompetitionIds.Bundesliga2026_27, match with { ResultBasis = ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout }, out _)).IsFalse();
    }

    [Test]
    public async Task Loader_rejects_duplicate_missing_and_drifted_seed_content()
    {
        var path = Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaSeasonRoutingSeed.RelativePath);
        var canonical = File.ReadAllText(path);

        await Assert.That(() => BundesligaSeasonRoutingSeed.Parse(canonical.Replace("\"1662326754\"", "\"1662326753\"", StringComparison.Ordinal))).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaSeasonRoutingSeed.Parse(canonical.Replace("\"maxSelections\": 4", "\"maxSelections\": 0", StringComparison.Ordinal))).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaSeasonRoutingSeed.Parse(canonical.Replace("Viking Stavanger", "Viking Stavanger FC", StringComparison.Ordinal))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Loader_rejects_option_drift_even_when_the_canonical_self_hash_is_updated()
    {
        var canonical = ReadCanonicalSeed();
        var root = JsonNode.Parse(canonical)!.AsObject();
        root["questions"]!.AsArray()[0]!["options"]!.AsArray()[0]!["text"] = "AEK Athen drifted";
        root["canonicalSha256"] = ComputeCanonicalSeedHash(root);

        await Assert.That(() => BundesligaSeasonRoutingSeed.Parse(root.ToJsonString()))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task BonusQuestion_keeps_its_legacy_constructor_and_five_value_deconstruction()
    {
        var deadline = Instant.FromUtc(2026, 9, 8, 16, 45).InZone(DateTimeZone.Utc);
        var options = new List<BonusQuestionOption> { new("1", "One") };
        var question = new BonusQuestion("Question", deadline, options, 1, "form-field")
        {
            KicktippQuestionId = "1662326752",
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };

        var (text, actualDeadline, actualOptions, maxSelections, formFieldName) = question;

        await Assert.That(text).IsEqualTo("Question");
        await Assert.That(actualDeadline).IsEqualTo(deadline);
        await Assert.That(actualOptions).IsEqualTo(options);
        await Assert.That(maxSelections).IsEqualTo(1);
        await Assert.That(formFieldName).IsEqualTo("form-field");
        await Assert.That(question.KicktippQuestionId).IsEqualTo("1662326752");
    }

    [Test]
    public async Task Lookup_and_classifier_null_inputs_fail_closed_with_null_outputs()
    {
        var seed = BundesligaSeasonRoutingSeed.Default;
        var classifier = new BundesligaSeasonRoutingClassifier(seed);

        await Assert.That(seed.TryGetFixture(null, out var fixture)).IsFalse();
        await Assert.That(fixture).IsNull();
        await Assert.That(seed.TryGetQuestion(null, out var question)).IsFalse();
        await Assert.That(question).IsNull();
        await Assert.That(classifier.TryClassifyMatch(null, null, out fixture)).IsFalse();
        await Assert.That(fixture).IsNull();
        await Assert.That(classifier.TryClassifyBonusQuestion(null, null, out question)).IsFalse();
        await Assert.That(question).IsNull();
    }

    private static string ReadCanonicalSeed() => File.ReadAllText(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaSeasonRoutingSeed.RelativePath));

    private static string ComputeCanonicalSeedHash(JsonObject root)
    {
        var builder = new StringBuilder("bundesliga-season-routing-v1\n");
        foreach (var fixture in root["fixtures"]!.AsArray().Select(item => item!.AsObject()).OrderBy(item => item["kicktippFixtureId"]!.GetValue<string>(), StringComparer.Ordinal))
        {
            Append(builder, fixture["kicktippFixtureId"]!.GetValue<string>());
            Append(builder, fixture["bundesligaSeasonSubcompetition"]!.GetValue<string>());
            Append(builder, fixture["kicktippRoundName"]!.GetValue<string>());
            Append(builder, fixture["resultBasis"]!.GetValue<string>());
        }
        foreach (var question in root["questions"]!.AsArray().Select(item => item!.AsObject()).OrderBy(item => item["kicktippQuestionId"]!.GetValue<string>(), StringComparer.Ordinal))
        {
            Append(builder, question["kicktippQuestionId"]!.GetValue<string>());
            Append(builder, question["text"]!.GetValue<string>());
            Append(builder, DateTimeOffset.Parse(question["deadline"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, question["maxSelections"]!.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, question["bundesligaSeasonSubcompetition"]!.GetValue<string>());
            Append(builder, question["evidenceOptionSetSha256"]!.GetValue<string>());
            foreach (var option in question["options"]!.AsArray().Select(item => item!.AsObject()))
            {
                Append(builder, option["id"]!.GetValue<string>());
                Append(builder, option["text"]!.GetValue<string>());
            }
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string value) => builder.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value).Append('\n');
}
