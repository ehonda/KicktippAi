using EHonda.KicktippAi.Core;
using NodaTime;
using Orchestrator.Commands.Operations.Bonus;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

public sealed class BonusQuestionExecutionScopeTests
{
    [Test]
    public async Task Inclusive_deadline_ceiling_selects_only_questions_at_or_before_cutoff()
    {
        var before = CreateBonusQuestion(
            text: "before",
            deadline: Instant.FromUtc(2026, 8, 28, 18, 29).InUtc());
        var exact = CreateBonusQuestion(
            text: "exact",
            deadline: Instant.FromUtc(2026, 8, 28, 18, 30).InUtc());
        var later = CreateBonusQuestion(
            text: "later",
            deadline: Instant.FromUtc(2026, 9, 9, 10, 0).InUtc());

        var selected = BonusQuestionExecutionScope.SelectAtOrBefore(
            [later, exact, before],
            "2026-08-28T18:30:00Z");

        await Assert.That(selected.Select(question => question.Text))
            .IsEquivalentTo(["exact", "before"]);
    }

    [Test]
    [Arguments("not-a-time")]
    [Arguments("   ")]
    [Arguments("-9998-01-01T00:00:00Z")]
    public async Task Invalid_or_minimum_deadline_ceiling_fails_closed(string value)
    {
        var valid = BonusQuestionExecutionScope.TryParseDeadlineAtOrBefore(
            value,
            out _,
            out var validationError);

        await Assert.That(valid).IsFalse();
        await Assert.That(validationError).Contains("--bonus-deadline-at-or-before");
    }

    [Test]
    [Arguments("1.BL: Welche Mannschaften belegen die Plätze 16-18?")]
    [Arguments("1.BL: Welche Mannschaft stellt den Spieler mit den meisten Toren?")]
    [Arguments("1.BL: Wer wird Deutscher Meister?")]
    [Arguments("1.BL: Wer wird Herbstmeister?")]
    [Arguments("1.BL: Wo findet der erste Trainerwechsel statt?")]
    public async Task Schadensfresse_never_normalizes_a_pes_squad_bonus_alias(string targetText)
    {
        var target = CreateBonusQuestion(
            text: targetText,
            formFieldName: "target-form");

        var projection = BonusQuestionExecutionScope.ResolveReferenceProjection(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "pes-squad",
            target);

        await Assert.That(projection.Question).IsSameReferenceAs(target);
        await Assert.That(projection.Question.Text).IsEqualTo(targetText);
        await Assert.That(projection.Question.FormFieldName).IsEqualTo(target.FormFieldName);
        await Assert.That(projection.Question.Options).IsSameReferenceAs(target.Options);
        await Assert.That(projection.AliasId).IsNull();
        await Assert.That(projection.SourceNormalizedTextSha256)
            .IsEqualTo(projection.TargetNormalizedTextSha256);
        await Assert.That(target.Text).IsEqualTo(targetText);
    }

    [Test]
    public async Task Reference_projection_preserves_target_question_for_other_copy_communities()
    {
        var target = CreateBonusQuestion(text: "1.BL: Wer wird Deutscher Meister?");

        var projection = BonusQuestionExecutionScope.ResolveReferenceProjection(
            CompetitionIds.Bundesliga2026_27,
            "ehonda-ai-arena",
            "pes-squad",
            target);

        await Assert.That(projection.Question).IsSameReferenceAs(target);
        await Assert.That(projection.AliasId).IsNull();
        await Assert.That(projection.SourceNormalizedTextSha256)
            .IsEqualTo(projection.TargetNormalizedTextSha256);
    }
}
