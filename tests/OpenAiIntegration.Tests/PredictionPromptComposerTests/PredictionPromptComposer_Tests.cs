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
    public async Task Building_system_prompt_replaces_context_documents_placeholder()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt(
            "template\n\n{{context_documents}}",
            [
                new DocumentContext("Doc A", "Alpha")
            ]);

        var expected = """
            template

            ---
            Doc A

            Alpha
            ---
            """.Replace("\r\n", "\n");

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Bonus_context_budget_estimate_matches_exact_rendered_context_section_bytes()
    {
        var documents = new[]
        {
            new DocumentContext("club-elo-rankings", "Rang,Club\r\n1,FC Bayern München\r\n"),
            new DocumentContext("team-squad-summary", "Club,Players\r\nFC Bayern München,25\r\n")
        };

        var renderedSection = PredictionPromptComposer.BuildSystemPrompt("{{context_documents}}", documents);
        var measurement = BonusContextBudgetEstimator.Measure(documents);

        await Assert.That(measurement.Utf8Bytes)
            .IsEqualTo(System.Text.Encoding.UTF8.GetByteCount(renderedSection));
        await Assert.That(measurement.EstimatedTokens)
            .IsEqualTo((measurement.Utf8Bytes + 3) / 4);
    }

    [Test]
    public async Task Building_system_prompt_replaces_context_documents_placeholder_with_empty_string_when_no_context_exists()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt("template:{{context_documents}}", []);

        await Assert.That(result).IsEqualTo("template:");
    }

    [Test]
    public async Task Building_system_prompt_removes_justification_placeholder_when_justification_is_not_requested()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt(
            "Predict the score.{{justification_explainer}}",
            [],
            includeJustification: false);

        await Assert.That(result)
            .IsEqualTo("Predict the score.")
            .And.DoesNotContain("justification")
            .And.DoesNotContain("{{justification_explainer}}");
    }

    [Test]
    public async Task Building_system_prompt_expands_justification_placeholder_only_when_requested()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt(
            "Predict the score.{{justification_explainer}}",
            [],
            includeJustification: true);

        await Assert.That(result)
            .Contains("Populate the `justification` object concisely")
            .And.DoesNotContain("{{justification_explainer}}");
    }

    [Test]
    [Arguments("{{context_documents}}{{context_documents}}")]
    [Arguments("{{justification_explainer}}{{justification_explainer}}")]
    public async Task Building_system_prompt_rejects_duplicate_supported_placeholders(string template)
    {
        await Assert.That(() => PredictionPromptComposer.BuildSystemPrompt(
                template,
                [],
                includeJustification: true))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Building_system_prompt_rejects_an_unknown_template_placeholder()
    {
        await Assert.That(() => PredictionPromptComposer.BuildSystemPrompt(
                "Predict the score. {{unknown_variable}}",
                []))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Building_system_prompt_does_not_interpret_context_content_as_template_syntax()
    {
        var result = PredictionPromptComposer.BuildSystemPrompt(
            "{{context_documents}}",
            [new DocumentContext("literal.txt", "Retain {{literal_context_text}} exactly.")]);

        await Assert.That(result).Contains("Retain {{literal_context_text}} exactly.");
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
    public async Task Creating_world_cup_knockout_match_json_includes_nested_competition_data()
    {
        var match = new Match(
            "South Africa",
            "Canada",
            Instant.FromUtc(2026, 6, 28, 19, 0).InUtc(),
            37)
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };

        var result = PredictionPromptComposer.CreateMatchJson(match);

        await Assert.That(result).IsEqualTo(
            "{\"homeTeam\":\"South Africa\",\"awayTeam\":\"Canada\",\"startsAt\":\"2026-06-28T19:00:00 UTC (+00)\",\"competitionSpecificData\":{\"competition\":\"fifa-world-cup-2026\",\"isKnockoutStage\":true,\"stage\":\"roundOf32\",\"kicktippRoundName\":\"Sechzehntelfinale\",\"resultBasis\":\"finalScoreIncludingExtraTimeAndPenaltyShootout\"}}");
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
