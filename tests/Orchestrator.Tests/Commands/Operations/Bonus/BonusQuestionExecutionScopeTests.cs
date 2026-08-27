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
    [Arguments("1.BL: Welche Mannschaften belegen die Plätze 16-18?", "Welche Mannschaften belegen die Plätze 16-18?", "schadensfresse-buli-places-16-18-v1")]
    [Arguments("1.BL: Welche Mannschaft stellt den Spieler mit den meisten Toren?", "Welche Mannschaft stellt den Spieler mit den meisten Toren?", "schadensfresse-buli-top-scorer-v1")]
    [Arguments("1.BL: Wer wird Deutscher Meister?", "Wer wird Deutscher Meister?", "schadensfresse-buli-champion-v1")]
    [Arguments("1.BL: Wer wird Herbstmeister?", "Wer wird Herbstmeister?", "schadensfresse-buli-autumn-champion-v1")]
    [Arguments("1.BL: Wo findet der erste Trainerwechsel statt?", "Wo findet der erste Trainerwechsel statt?", "schadensfresse-buli-first-coach-change-v1")]
    public async Task Exact_schadensfresse_Bundesliga_alias_projects_only_source_text(
        string targetText,
        string sourceText,
        string aliasId)
    {
        var target = CreateBonusQuestion(
            text: targetText,
            formFieldName: "target-form");

        var projection = BonusQuestionExecutionScope.ResolveReferenceProjection(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "pes-squad",
            target);

        await Assert.That(projection.Question.Text).IsEqualTo(sourceText);
        await Assert.That(projection.Question.FormFieldName).IsEqualTo(target.FormFieldName);
        await Assert.That(projection.Question.Options).IsSameReferenceAs(target.Options);
        await Assert.That(projection.AliasId).IsEqualTo(aliasId);
        await Assert.That(projection.SourceNormalizedTextSha256)
            .IsNotEqualTo(projection.TargetNormalizedTextSha256);
        await Assert.That(target.Text).IsEqualTo(targetText);
    }

    [Test]
    [Arguments("bundesliga-2026-27", "another-community", "pes-squad", "1.BL: Wer wird Deutscher Meister?")]
    [Arguments("bundesliga-2026-27", "schadensfresse", "another-source", "1.BL: Wer wird Deutscher Meister?")]
    [Arguments("fifa-world-cup-2026", "schadensfresse", "pes-squad", "1.BL: Wer wird Deutscher Meister?")]
    [Arguments("bundesliga-2026-27", "schadensfresse", "pes-squad", "1.BL: Unmapped question?")]
    public async Task Alias_policy_does_not_strip_prefix_outside_exact_tuple_and_full_text_map(
        string competition,
        string targetCommunity,
        string sourceCommunityContext,
        string text)
    {
        var target = CreateBonusQuestion(text: text);

        var projection = BonusQuestionExecutionScope.ResolveReferenceProjection(
            competition,
            targetCommunity,
            sourceCommunityContext,
            target);

        await Assert.That(projection.Question).IsSameReferenceAs(target);
        await Assert.That(projection.AliasId).IsNull();
        await Assert.That(projection.SourceNormalizedTextSha256)
            .IsEqualTo(projection.TargetNormalizedTextSha256);
    }
}
