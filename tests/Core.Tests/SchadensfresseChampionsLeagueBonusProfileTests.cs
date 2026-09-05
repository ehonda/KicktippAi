using EHonda.KicktippAi.Core;

namespace Core.Tests;

public sealed class SchadensfresseChampionsLeagueBonusProfileTests
{
    [Test]
    public async Task Exact_frozen_invocation_is_the_only_admitted_zero_context_tuple()
    {
        var accepted = SchadensfresseChampionsLeagueBonusProfile.IsExactInvocation(
            SchadensfresseChampionsLeagueBonusProfile.ProfileId,
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "schadensfresse",
            "langfuse",
            SchadensfresseChampionsLeagueBonusProfile.PromptName,
            "production",
            1,
            "gpt-5.6-sol",
            "xhigh",
            10_000,
            0,
            0,
            "2026-09-08T16:45:00Z");

        var wrongContext = SchadensfresseChampionsLeagueBonusProfile.IsExactInvocation(
            SchadensfresseChampionsLeagueBonusProfile.ProfileId,
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "pes-squad",
            "langfuse",
            SchadensfresseChampionsLeagueBonusProfile.PromptName,
            "production",
            1,
            "gpt-5.6-sol",
            "xhigh",
            10_000,
            0,
            0,
            "2026-09-08T16:45:00Z");

        await Assert.That(accepted).IsTrue();
        await Assert.That(wrongContext).IsFalse();
    }

    [Test]
    public async Task Manifest_requires_the_exact_model_identity_and_empty_documents()
    {
        var config = PredictionModelConfig.Create(
            "gpt-5.6-sol", "xhigh", 10_000,
            SchadensfresseChampionsLeagueBonusProfile.PromptName, 1);
        var scope = SchadensfresseChampionsLeagueBonusPredictionScope.Create(CreateQuestions()[0], config);
        var manifest = SchadensfresseChampionsLeagueBonusManifest.Create(scope, "langfuse");

        manifest.Validate(scope);
        var invalid = manifest with { Documents = ["club-elo-rankings"] };
        await Assert.That(() => invalid.Validate(scope)).Throws<InvalidDataException>();
        await Assert.That(() => (manifest with { KicktippQuestionId = "1662326753" }).Validate(scope))
            .Throws<InvalidDataException>();
        await Assert.That(() => (manifest with { QuestionDefinitionSha256 = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[1].DefinitionSha256 }).Validate(scope))
            .Throws<InvalidDataException>();
        await Assert.That(() => (manifest with { ModelConfigKey = PredictionModelConfig.Create("gpt-5.6-sol", "high").IdentityKey }).Validate(scope))
            .Throws<InvalidDataException>();
        await Assert.That(() => (manifest with { PromptProvider = "" }).Validate(scope))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Embedded_seed_binds_the_exact_ordered_108_option_identities_and_six_keys()
    {
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default;
        await Assert.That(seed.Questions.Select(question => question.KicktippQuestionId)
            .SequenceEqual(new[] { "1662326752", "1662326753", "1662326754" }, StringComparer.Ordinal)).IsTrue();
        await Assert.That(seed.Questions.Sum(question => question.Options.Count)).IsEqualTo(108);
        await Assert.That(seed.Questions.Sum(question => question.FormKeys.Count)).IsEqualTo(6);
        await Assert.That(seed.Questions.SelectMany(question => question.Options).Any(option =>
            option.Id is "14958543" or "14958571" or "14958599")).IsFalse();
        SchadensfresseChampionsLeagueBonusProfile.ValidateQuestions(CreateQuestions());
    }

    [Test]
    public async Task Profile_rejects_reordered_questions_and_option_drift()
    {
        var questions = CreateQuestions();
        await Assert.That(() => SchadensfresseChampionsLeagueBonusProfile.ValidateQuestions(questions.AsEnumerable().Reverse().ToArray()))
            .Throws<InvalidDataException>();
        questions[0].Options.Reverse();
        await Assert.That(() => SchadensfresseChampionsLeagueBonusProfile.ValidateQuestions(questions))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_seed_rejects_unknown_fields_and_hash_or_order_drift()
    {
        var json = System.Text.Encoding.UTF8.GetString(
            System.Reflection.Assembly.GetAssembly(typeof(SchadensfresseChampionsLeagueBonusSeed))!
                .GetManifestResourceStream("EHonda.KicktippAi.Core.Data.SchadensfresseChampionsLeagueBonus.json")!
                .ReadAllBytes());

        await Assert.That(() => SchadensfresseChampionsLeagueBonusSeed.Parse(
                System.Text.Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"extra\": true", StringComparison.Ordinal))))
            .Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseChampionsLeagueBonusSeed.Parse(
                System.Text.Encoding.UTF8.GetBytes(json.Replace("15413244", "15413245", StringComparison.Ordinal))))
            .Throws<InvalidDataException>();
    }

    private static List<BonusQuestion> CreateQuestions() =>
        SchadensfresseChampionsLeagueBonusSeed.Default.Questions.Select(question => new BonusQuestion(
            question.Text,
            NodaTime.Text.InstantPattern.ExtendedIso.Parse(question.Deadline).Value.InUtc(),
            question.Options.Select(option => new BonusQuestionOption(option.Id, option.Text)).ToList(),
            question.MaxSelections,
            question.FormKeys[0])).ToList();
}

file static class StreamTestExtensions
{
    public static byte[] ReadAllBytes(this Stream stream)
    {
        using (stream)
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
