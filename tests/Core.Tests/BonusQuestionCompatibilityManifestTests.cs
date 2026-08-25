using EHonda.KicktippAi.Core;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace Core.Tests;

public class BonusQuestionCompatibilityManifestTests
{
    [Test]
    public async Task Compatible_normalized_reordered_options_map_source_ids_to_target_ids()
    {
        var source = CreateBonusQuestion(
            text: "  Wer\t wird Meister?  ",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("source-bayern", "ＦＣ Bayern  München"),
                new BonusQuestionOption("source-bvb", "Borussia Dortmund")
            });
        var target = CreateBonusQuestion(
            text: "Wer wird Meister?",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("target-bvb", "Borussia   Dortmund"),
                new BonusQuestionOption("target-bayern", "FC Bayern München")
            });

        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);
        var result = sourceManifest.TryMapPrediction(
            target,
            new BonusPrediction(["source-bayern"]),
            out var mapped,
            out var targetManifest);

        await Assert.That(result).IsEqualTo(BonusPredictionCopyCompatibility.Compatible);
        await Assert.That(mapped).IsNotNull();
        await Assert.That(mapped!.SelectedOptionIds).IsEquivalentTo(["target-bayern"]);
        await Assert.That(targetManifest.CompatibilitySha256)
            .IsEqualTo(sourceManifest.CompatibilitySha256);
    }

    [Test]
    public async Task Changed_missing_or_extra_options_are_incompatible()
    {
        var source = CreateQuestion(
            new("a", "Option A"),
            new("b", "Option B"));
        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);
        var cases = new[]
        {
            CreateQuestion(new("a2", "Option changed"), new("b2", "Option B")),
            CreateQuestion(new BonusQuestionOption("a2", "Option A")),
            CreateQuestion(new("a2", "Option A"), new("b2", "Option B"), new("c2", "Option C"))
        };

        foreach (var target in cases)
        {
            var result = sourceManifest.TryMapPrediction(
                target,
                new BonusPrediction(["a"]),
                out var mapped,
                out _);

            await Assert.That(result).IsEqualTo(BonusPredictionCopyCompatibility.OptionSetMismatch);
            await Assert.That(mapped).IsNull();
        }
    }

    [Test]
    public async Task Question_and_max_selection_changes_are_incompatible()
    {
        var source = CreateQuestion(new("a", "Option A"), new("b", "Option B"));
        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);
        var changedQuestion = source with { Text = "A different question?" };
        var changedMaximum = source with { MaxSelections = 2 };

        var questionResult = sourceManifest.TryMapPrediction(
            changedQuestion,
            new BonusPrediction(["a"]),
            out _,
            out _);
        var maximumResult = sourceManifest.TryMapPrediction(
            changedMaximum,
            new BonusPrediction(["a"]),
            out _,
            out _);

        await Assert.That(questionResult).IsEqualTo(BonusPredictionCopyCompatibility.QuestionMismatch);
        await Assert.That(maximumResult).IsEqualTo(BonusPredictionCopyCompatibility.MaxSelectionsMismatch);
    }

    [Test]
    public async Task Question_and_option_comparisons_remain_case_and_accent_sensitive()
    {
        var source = CreateBonusQuestion(
            text: "Wer wird Torschützenkönig?",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("a", "FC Bayern München"),
                new BonusQuestionOption("b", "Borussia Dortmund")
            });
        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);

        var lowercaseQuestion = source with { Text = "wer wird Torschützenkönig?" };
        var unaccentedQuestion = source with { Text = "Wer wird Torschutzenkonig?" };
        var lowercaseOption = source with
        {
            Options =
            [
                new BonusQuestionOption("target-a", "fc Bayern München"),
                new BonusQuestionOption("target-b", "Borussia Dortmund")
            ]
        };
        var unaccentedOption = source with
        {
            Options =
            [
                new BonusQuestionOption("target-a", "FC Bayern Munchen"),
                new BonusQuestionOption("target-b", "Borussia Dortmund")
            ]
        };

        await Assert.That(sourceManifest.TryMapPrediction(
                lowercaseQuestion,
                new BonusPrediction(["a"]),
                out _,
                out _))
            .IsEqualTo(BonusPredictionCopyCompatibility.QuestionMismatch);
        await Assert.That(sourceManifest.TryMapPrediction(
                unaccentedQuestion,
                new BonusPrediction(["a"]),
                out _,
                out _))
            .IsEqualTo(BonusPredictionCopyCompatibility.QuestionMismatch);
        await Assert.That(sourceManifest.TryMapPrediction(
                lowercaseOption,
                new BonusPrediction(["a"]),
                out _,
                out _))
            .IsEqualTo(BonusPredictionCopyCompatibility.OptionSetMismatch);
        await Assert.That(sourceManifest.TryMapPrediction(
                unaccentedOption,
                new BonusPrediction(["a"]),
                out _,
                out _))
            .IsEqualTo(BonusPredictionCopyCompatibility.OptionSetMismatch);
    }

    [Test]
    public async Task Duplicate_normalized_target_options_fail_before_mapping()
    {
        var source = CreateQuestion(new("a", "Option A"), new("b", "Option B"));
        var target = CreateQuestion(new("x", "Option A"), new("y", " Option   A "));
        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);

        await Assert.That(() => sourceManifest.TryMapPrediction(
                target,
                new BonusPrediction(["a"]),
                out _,
                out _))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Unknown_or_duplicate_source_selections_are_rejected()
    {
        var source = CreateQuestion(new("a", "Option A"), new("b", "Option B"));
        var target = CreateQuestion(new("x", "Option A"), new("y", "Option B"));
        var sourceManifest = BonusQuestionCompatibilityManifest.Create(source);

        foreach (var prediction in new[]
                 {
                     new BonusPrediction(["missing"]),
                     new BonusPrediction(["a", "a"])
                 })
        {
            var result = sourceManifest.TryMapPrediction(target, prediction, out var mapped, out _);
            await Assert.That(result).IsEqualTo(BonusPredictionCopyCompatibility.InvalidSourceSelection);
            await Assert.That(mapped).IsNull();
        }
    }

    private static BonusQuestion CreateQuestion(params BonusQuestionOption[] options)
    {
        return CreateBonusQuestion(
            text: "Question?",
            options: options.ToList(),
            maxSelections: 1);
    }
}
