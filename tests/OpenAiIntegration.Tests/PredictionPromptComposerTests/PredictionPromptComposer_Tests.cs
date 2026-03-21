using EHonda.KicktippAi.Core;
using NodaTime;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionPromptComposerTests;

public class PredictionPromptComposer_Tests
{
    [Test]
    public async Task Building_system_prompt_with_no_context_returns_template_unchanged()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt("template", []);

        await Assert.That(result).IsEqualTo("template");
    }

    [Test]
    public async Task Building_system_prompt_with_multiple_context_documents_preserves_order_and_format()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt(
            "template",
            [
                new DocumentContext("Doc A", "Alpha"),
                new DocumentContext("Doc B", "Beta")
            ]);

        var expected = """
            template
            ---
            Doc A

            Alpha
            ---
            Doc B

            Beta
            ---
            """.Replace("\r\n", "\n");

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Creating_match_json_uses_expected_payload_shape()
    {
        var match = new Match(
            "Team A",
            "Team B",
            Instant.FromUtc(2025, 10, 30, 15, 30).InUtc(),
            7);

        var result = PredictionPromptComposer.CreateMatchJson(match);

        await Assert.That(result).IsEqualTo(
            "{\"homeTeam\":\"Team A\",\"awayTeam\":\"Team B\",\"startsAt\":\"2025-10-30T15:30:00 UTC (+00)\"}");
    }

    [Test]
    public async Task Creating_bonus_question_json_uses_expected_payload_shape()
    {
        var bonusQuestion = new BonusQuestion(
            "Who will score first?",
            Instant.FromUtc(2025, 10, 30, 15, 30).InUtc(),
            [
                new BonusQuestionOption("a", "Team A"),
                new BonusQuestionOption("b", "Team B")
            ],
            1);

        var result = PredictionPromptComposer.CreateBonusQuestionJson(bonusQuestion);

        await Assert.That(result).IsEqualTo(
            "{\"text\":\"Who will score first?\",\"options\":[{\"id\":\"a\",\"text\":\"Team A\"},{\"id\":\"b\",\"text\":\"Team B\"}],\"maxSelections\":1}");
    }
}
