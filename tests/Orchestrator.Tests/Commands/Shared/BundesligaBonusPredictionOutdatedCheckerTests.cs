using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Shared;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Shared;

public sealed class BundesligaBonusPredictionOutdatedCheckerTests
{
    [Test]
    public async Task Exact_current_semantic_heads_and_selected_entries_are_current()
    {
        var question = CreateQuestion("Wer wird Deutscher Meister?", "FC Bayern München");
        var metadata = CreateCanonicalBundesligaBonusPredictionMetadata(question);

        var outdated = await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockBundesligaDocumentPublicationRepository().Object,
            question,
            "test-community",
            metadata);

        await Assert.That(outdated).IsFalse();
    }

    [Test]
    public async Task Missing_manifest_fails_closed()
    {
        var question = CreateQuestion("Wer wird Deutscher Meister?", "FC Bayern München");
        var metadata = CreateBonusPredictionMetadata(
            contextDocumentNames: new List<string> { "club-elo-rankings", "team-squad-summary" });

        await Assert.That(() => BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
                CreateMockBundesligaDocumentPublicationRepository().Object,
                question,
                "test-community",
                metadata))
            .Throws<InvalidDataException>()
            .WithMessageContaining("missing its immutable resolved bonus-context manifest");
    }

    [Test]
    public async Task Changed_exact_question_selection_is_outdated()
    {
        var baselineQuestion = CreateQuestion("Wer wird Deutscher Meister?", "FC Bayern München");
        var targetedQuestion = CreateQuestion(
            "Welche Mannschaft stellt den Spieler mit den meisten Toren?",
            "FC Bayern München");
        var metadata = CreateCanonicalBundesligaBonusPredictionMetadata(baselineQuestion);

        var outdated = await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockBundesligaDocumentPublicationRepository().Object,
            targetedQuestion,
            "test-community",
            metadata);

        await Assert.That(outdated).IsTrue();
    }

    [Test]
    public async Task Tampered_selected_document_hash_is_outdated()
    {
        var question = CreateQuestion("Wer wird Deutscher Meister?", "FC Bayern München");
        var current = CreateCanonicalBundesligaBonusPredictionMetadata(question);
        var manifest = current.ResolvedContextManifest!;
        var tampered = ResolvedBonusContextManifest.Create(
            manifest.Competition,
            manifest.CommunityContext,
            manifest.Documents.Select((document, index) => index == 0
                ? new ResolvedBonusContextDocument(
                    document.Kind,
                    document.Name,
                    document.Version,
                    new string('f', DocumentPublicationContract.Sha256HexLength))
                : document),
            manifest.RosterPublicationSnapshotId,
            manifest.ClubEloPublicationSnapshotId);

        var outdated = await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockBundesligaDocumentPublicationRepository().Object,
            question,
            "test-community",
            current with { ResolvedContextManifest = tampered });

        await Assert.That(outdated).IsTrue();
    }

    [Test]
    public async Task Missing_current_head_fails_closed()
    {
        var question = CreateQuestion("Wer wird Deutscher Meister?", "FC Bayern München");
        var metadata = CreateCanonicalBundesligaBonusPredictionMetadata(question);
        var repository = CreateMockBundesligaDocumentPublicationRepository();
        repository.Setup(value => value.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.Rosters,
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);

        await Assert.That(() => BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
                repository.Object,
                question,
                "test-community",
                metadata))
            .Throws<InvalidDataException>()
            .WithMessageContaining("roster publication head is missing");
    }

    private static BonusQuestion CreateQuestion(string text, params string[] options) =>
        CreateBonusQuestion(
            text: text,
            options: options.Select((option, index) => new BonusQuestionOption(index.ToString(), option)).ToList());
}
