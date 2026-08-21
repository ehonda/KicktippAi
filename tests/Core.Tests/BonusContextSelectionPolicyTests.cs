using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BonusContextSelectionPolicyTests
{
    [Test]
    [Arguments("Wer wird Deutscher Meister?", BundesligaBonusQuestionCategory.Champion)]
    [Arguments("Which club will win the league?", BundesligaBonusQuestionCategory.Champion)]
    [Arguments("Welche drei Mannschaften steigen in die 2. Liga ab?", BundesligaBonusQuestionCategory.Relegation)]
    [Arguments("Which clubs will be relegated?", BundesligaBonusQuestionCategory.Relegation)]
    [Arguments("Wer wird Torschützenkönig?", BundesligaBonusQuestionCategory.TopScorer)]
    [Arguments("Who will be the top scorer?", BundesligaBonusQuestionCategory.TopScorer)]
    [Arguments("Which player will score the most goals?", BundesligaBonusQuestionCategory.TopScorer)]
    [Arguments("Welcher Trainer wird zuerst entlassen?", BundesligaBonusQuestionCategory.Coach)]
    [Arguments("Which head coach will be sacked first?", BundesligaBonusQuestionCategory.Coach)]
    [Arguments("Wie viele Tore fallen am ersten Spieltag?", BundesligaBonusQuestionCategory.Unknown)]
    public async Task Classification_supports_fixed_German_and_English_variants(
        string text,
        BundesligaBonusQuestionCategory expected)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Der Meistertrainer erhält eine Ehrung")]
    [Arguments("Welche Abstiegsplatzierung wird erreicht?")]
    [Arguments("Welche Torschützenstatistik ist korrekt?")]
    [Arguments("Welche Coachingsession beginnt zuerst?")]
    [Arguments("Which teams qualify for the Champions League?")]
    public async Task Category_signal_substrings_inside_longer_tokens_are_false_positives(string text)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Unknown);
    }

    [Test]
    [Arguments("Wer wird Champions-League-Meister?")]
    [Arguments("Wer wird Champions League Meister?")]
    [Arguments("Who will be Champions-League champion?")]
    [Arguments("Who will be Champions League champion?")]
    public async Task Champions_League_titles_do_not_classify_as_Bundesliga_champion(string text)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Unknown);
    }

    [Test]
    [Arguments("Wer wird Bundesliga-Meister?")]
    [Arguments("Who will be Bundesliga champion?")]
    public async Task Bundesliga_champion_phrases_remain_supported(string text)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Champion);
    }

    [Test]
    [Arguments("\U00010400meister")]
    [Arguments("\U000104A0meister")]
    [Arguments("meister\U00010400")]
    [Arguments("meister\U000104A0")]
    public async Task Supplementary_letter_or_digit_adjacency_is_not_a_phrase_boundary(string text)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Unknown);
    }

    [Test]
    [Arguments("🏆Meister!")]
    [Arguments("Meister🏆")]
    public async Task Punctuation_and_supplementary_emoji_remain_valid_phrase_boundaries(string text)
    {
        var category = BonusContextSelectionPolicy.Classify(Question(text, "FC Bayern München"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Champion);
    }

    [Test]
    public async Task Options_do_not_classify_a_question()
    {
        var category = BonusContextSelectionPolicy.Classify(
            Question("Wer gewinnt diese Auswahl?", "Trainer", "Top scorer", "Abstieg", "Meister"));

        await Assert.That(category).IsEqualTo(BundesligaBonusQuestionCategory.Unknown);
    }

    [Test]
    public async Task Multi_category_question_fails_instead_of_using_precedence()
    {
        await Assert.That(() => BonusContextSelectionPolicy.SelectBundesliga(
                Question("Welcher Trainer wird mit seinem Club Deutscher Meister?", "Niko Kovač"),
                Rosters()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("matches multiple context categories");
    }

    [Test]
    [Arguments("Wer wird Deutscher Meister?", BundesligaBonusQuestionCategory.Champion)]
    [Arguments("Welche drei Mannschaften steigen ab?", BundesligaBonusQuestionCategory.Relegation)]
    [Arguments("Wie viele Tore fallen am ersten Spieltag?", BundesligaBonusQuestionCategory.Unknown)]
    public async Task Aggregate_categories_use_only_the_ordered_baseline(
        string text,
        BundesligaBonusQuestionCategory expectedCategory)
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question(text, "FC Bayern München", "Borussia Dortmund"),
            Rosters());

        await Assert.That(selection.Category).IsEqualTo(expectedCategory);
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary"
        ])).IsTrue();
        await Assert.That(selection.TargetedTeamSlugs).IsEmpty();
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name)).DoesNotContain("team-rosters");
    }

    [Test]
    public async Task Top_scorer_team_question_targets_only_exact_manifest_options_in_slug_order()
    {
        var selection = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Welche Mannschaft stellt den Spieler mit den meisten Toren?", "Borussia Dortmund", "FC Bayern München"),
            Rosters());

        await Assert.That(selection.Category).IsEqualTo(BundesligaBonusQuestionCategory.TopScorer);
        await Assert.That(selection.TargetedTeamSlugs.SequenceEqual(["bvb", "fcb"])).IsTrue();
        await Assert.That(selection.RequiredDocuments.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary",
            "roster-bvb",
            "roster-fcb"
        ])).IsTrue();
    }

    [Test]
    public async Task Top_scorer_player_and_coach_options_target_only_the_relevant_current_roles()
    {
        var scorer = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Who will be the top scorer?", "Harry Kane", "Niko Kovač"),
            Rosters());
        var coach = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Which coach will be dismissed first?", "Harry Kane", "Niko Kovač"),
            Rosters());

        await Assert.That(scorer.TargetedTeamSlugs).IsEquivalentTo(["fcb"]);
        await Assert.That(coach.TargetedTeamSlugs).IsEquivalentTo(["bvb"]);
    }

    [Test]
    public async Task Exact_member_identity_in_question_text_is_accepted_but_longer_token_is_not()
    {
        var exact = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Will Harry Kane become the top scorer?", "Other"),
            Rosters());

        await Assert.That(exact.TargetedTeamSlugs).IsEquivalentTo(["fcb"]);
        await Assert.That(() => BonusContextSelectionPolicy.SelectBundesliga(
                Question("Will Harry Kanes goals make him top scorer?", "Other"),
                Rosters()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("requires targeted roster context");
    }

    [Test]
    public async Task Roster_relevant_question_without_an_exact_target_fails_instead_of_loading_every_roster()
    {
        await Assert.That(() => BonusContextSelectionPolicy.SelectBundesliga(
                Question("Welcher Trainer wird zuerst entlassen?", "Unbekannte Person"),
                Rosters()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("requires targeted roster context");
    }

    [Test]
    public async Task Selection_reports_prohibited_and_soft_roster_exclusions_deterministically()
    {
        var aggregate = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Wer wird Deutscher Meister?", "FC Bayern München"),
            Rosters());
        var targeted = BonusContextSelectionPolicy.SelectBundesliga(
            Question("Wer wird Torschützenkönig?", "Harry Kane"),
            Rosters());

        await Assert.That(aggregate.ExcludedDocuments[0]).IsEqualTo(new BonusContextDocumentExclusion(
            new DocumentPublicationKey(DocumentPublicationKind.Context, "team-rosters"),
            BonusContextExclusionReason.ProhibitedAggregate));
        await Assert.That(aggregate.ExcludedDocuments.Skip(1).All(exclusion =>
            exclusion.Reason == BonusContextExclusionReason.CategoryDoesNotUseRoster)).IsTrue();
        await Assert.That(targeted.ExcludedDocuments.Select(exclusion => exclusion.Document.Name))
            .DoesNotContain("roster-fcb");
        await Assert.That(targeted.ExcludedDocuments.Skip(1).All(exclusion =>
            exclusion.Reason == BonusContextExclusionReason.NoExactIdentity)).IsTrue();
        await Assert.That(targeted.ExcludedDocuments.Skip(1).Select(exclusion => exclusion.Document.Name)
            .SequenceEqual(targeted.ExcludedDocuments.Skip(1).Select(exclusion => exclusion.Document.Name)
                .Order(StringComparer.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Estimator_measures_exact_prompt_context_rendering_and_rounds_tokens_up()
    {
        var documents = new[]
        {
            new DocumentContext("ä", "A"),
            new DocumentContext("b", "é")
        };

        var measurement = BonusContextBudgetEstimator.Measure(documents);
        var rendered = "---\nä\n\nA\n---\nb\n\né\n---";
        var expectedBytes = System.Text.Encoding.UTF8.GetByteCount(rendered);

        await Assert.That(measurement.Utf8Bytes).IsEqualTo(expectedBytes);
        await Assert.That(measurement.EstimatedTokens).IsEqualTo((expectedBytes + 3) / 4);
    }

    [Test]
    public async Task Whole_selection_budget_fails_without_truncating_required_documents()
    {
        var documents = new[]
        {
            new DocumentContext("club-elo-rankings", "elo"),
            new DocumentContext("team-squad-summary", "summary"),
            new DocumentContext("roster-fcb", "roster")
        };
        var measurement = BonusContextBudgetEstimator.Measure(documents);

        await Assert.That(() => BonusContextBudgetEstimator.EnsureFits(
                documents.Length,
                measurement,
                new BonusContextBudget(2, 32_000)))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("all 3 selected documents");
        await Assert.That(documents.Select(document => document.Name).SequenceEqual(
            ["club-elo-rankings", "team-squad-summary", "roster-fcb"])).IsTrue();
    }

    [Test]
    public async Task Budget_rejects_values_below_explicit_guardrails()
    {
        await Assert.That(() => new BonusContextBudget(1, 32_000))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new BonusContextBudget(20, 255))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Resolved_result_rejects_selection_names_or_measurement_that_do_not_match_exact_documents()
    {
        var documents = new[]
        {
            new DocumentContext("club-elo-rankings", "elo"),
            new DocumentContext("team-squad-summary", "summary")
        };
        var manifest = ResolvedBonusContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            "test-community",
            documents.Select(document => new ResolvedBonusContextDocument(
                "Kpi",
                document.Name,
                1,
                DocumentPublicationContract.ComputeContentSha256(document.Content))),
            new string('a', 64),
            new string('b', 64));
        var measurement = BonusContextBudgetEstimator.Measure(documents);
        var canonicalChampionExclusions = BonusContextSelectionPolicy.GetCanonicalExclusions(
            BundesligaBonusQuestionCategory.Champion,
            documents.Select(document => document.Name));

        await Assert.That(() => new ResolvedBonusContext(
                documents,
                manifest,
                new ResolvedBonusContextSelection(
                    BundesligaBonusQuestionCategory.Champion,
                    ["team-squad-summary", "club-elo-rankings"],
                    canonicalChampionExclusions,
                    measurement.Utf8Bytes,
                    measurement.EstimatedTokens,
                    BonusContextBudget.Default)))
            .Throws<ArgumentException>()
            .WithMessageContaining("exact document names and order");

        await Assert.That(() => new ResolvedBonusContext(
                documents,
                manifest,
                new ResolvedBonusContextSelection(
                    BundesligaBonusQuestionCategory.Champion,
                    documents.Select(document => document.Name).ToImmutableArray(),
                    canonicalChampionExclusions,
                    measurement.Utf8Bytes + 1,
                    measurement.EstimatedTokens,
                    BonusContextBudget.Default)))
            .Throws<ArgumentException>()
            .WithMessageContaining("deterministic context-size estimate");

        await Assert.That(() => new ResolvedBonusContext(
                documents,
                manifest,
                new ResolvedBonusContextSelection(
                    BundesligaBonusQuestionCategory.Champion,
                    documents.Select(document => document.Name).ToImmutableArray(),
                    [new BonusContextDocumentExclusion(
                        new DocumentPublicationKey(DocumentPublicationKind.Kpi, "team-squad-summary"),
                        BonusContextExclusionReason.CategoryDoesNotUseRoster)],
                    measurement.Utf8Bytes,
                    measurement.EstimatedTokens,
                    BonusContextBudget.Default)))
            .Throws<ArgumentException>()
            .WithMessageContaining("disjoint canonical selected/excluded documents");
    }

    [Test]
    public async Task Resolved_result_requires_the_exact_canonical_exclusion_ledger()
    {
        var documents = new[]
        {
            new DocumentContext("club-elo-rankings", "elo"),
            new DocumentContext("team-squad-summary", "summary"),
            new DocumentContext("roster-fcb", "roster")
        };
        var selectedNames = documents.Select(document => document.Name).ToImmutableArray();
        var manifest = ResolvedBonusContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            "test-community",
            documents.Select((document, index) => new ResolvedBonusContextDocument(
                index < 2 ? "Kpi" : "Context",
                document.Name,
                1,
                DocumentPublicationContract.ComputeContentSha256(document.Content))),
            new string('a', 64),
            new string('b', 64));
        var measurement = BonusContextBudgetEstimator.Measure(documents);
        var expected = BonusContextSelectionPolicy.GetCanonicalExclusions(
            BundesligaBonusQuestionCategory.TopScorer,
            selectedNames);

        _ = new ResolvedBonusContext(
            documents,
            manifest,
            new ResolvedBonusContextSelection(
                BundesligaBonusQuestionCategory.TopScorer,
                selectedNames,
                expected,
                measurement.Utf8Bytes,
                measurement.EstimatedTokens,
                BonusContextBudget.Default));

        var invalidLedgers = new[]
        {
            expected.RemoveAt(expected.Length - 1),
            expected.Add(new BonusContextDocumentExclusion(
                new DocumentPublicationKey(DocumentPublicationKind.Context, "roster-extra"),
                BonusContextExclusionReason.NoExactIdentity)),
            expected.SetItem(0, expected[1]).SetItem(1, expected[0]),
            expected.SetItem(1, expected[1] with { Reason = (BonusContextExclusionReason)999 }),
            expected.SetItem(1, expected[1] with
            {
                Document = new DocumentPublicationKey(DocumentPublicationKind.Kpi, expected[1].Document.Name)
            }),
            expected.SetItem(1, expected[1] with
            {
                Reason = BonusContextExclusionReason.CategoryDoesNotUseRoster
            })
        };

        foreach (var invalidLedger in invalidLedgers)
        {
            await Assert.That(() => new ResolvedBonusContext(
                    documents,
                    manifest,
                    new ResolvedBonusContextSelection(
                        BundesligaBonusQuestionCategory.TopScorer,
                        selectedNames,
                        invalidLedger,
                        measurement.Utf8Bytes,
                        measurement.EstimatedTokens,
                        BonusContextBudget.Default)))
                .Throws<ArgumentException>()
                .WithMessageContaining("exact canonical exclusion ledger");
        }

        await Assert.That(() => new ResolvedBonusContext(
                documents,
                manifest,
                new ResolvedBonusContextSelection(
                    BundesligaBonusQuestionCategory.TopScorer,
                    selectedNames,
                    expected.Add(new BonusContextDocumentExclusion(
                        new DocumentPublicationKey(DocumentPublicationKind.Context, "roster-fcb"),
                        BonusContextExclusionReason.NoExactIdentity)),
                    measurement.Utf8Bytes,
                    measurement.EstimatedTokens,
                    BonusContextBudget.Default)))
            .Throws<ArgumentException>()
            .WithMessageContaining("disjoint canonical selected/excluded documents");
    }

    private static BonusQuestion Question(string text, params string[] options) => new(
        text,
        default,
        options.Select((option, index) => new BonusQuestionOption(index.ToString(), option)).ToList(),
        1);

    private static BundesligaRosterLastKnownGood Rosters()
    {
        var bayern = BundesligaTeamManifest.Default.GetByTeamSlug("fcb");
        var dortmund = BundesligaTeamManifest.Default.GetByTeamSlug("bvb");
        return new BundesligaRosterLastKnownGood(
            new string('a', 64),
            [
                new BundesligaRosterClubSnapshot(
                    bayern,
                    new DateOnly(2026, 8, 16),
                    BundesligaRosterMembershipSource.FallbackSeed,
                    [
                        new BundesligaRosterMember(BundesligaRosterRole.Coach, "Vincent Kompany"),
                        new BundesligaRosterMember(BundesligaRosterRole.Player, "Harry Kane")
                    ]),
                new BundesligaRosterClubSnapshot(
                    dortmund,
                    new DateOnly(2026, 8, 16),
                    BundesligaRosterMembershipSource.FallbackSeed,
                    [
                        new BundesligaRosterMember(BundesligaRosterRole.Coach, "Niko Kovač"),
                        new BundesligaRosterMember(BundesligaRosterRole.Player, "Serhou Guirassy")
                    ])
            ],
            [],
            string.Empty);
    }
}
