using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

/// <summary>
/// Tests for FirebaseKpiContextProvider.
/// These are unit tests using mocks, so they can run in parallel with other tests.
/// </summary>
public class FirebaseKpiContextProviderTests
{
    private const string TopScorerTeamQuestion = "Welche Mannschaft stellt den Spieler mit den meisten Toren?";
    private const string FictionalLineupsContent =
        "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\n" +
        "Exampleland,2026-05-25,Player,Alex Example,24,Forward,1000000\n" +
        "Exampleland,2026-05-25,Coach,Casey Sample,51,Coach,";

    /// <summary>
    /// Creates a FirebaseKpiContextProvider instance with optional dependency overrides.
    /// </summary>
    private static FirebaseKpiContextProvider CreateProvider(
        Option<Mock<IKpiRepository>> kpiRepository = default,
        Option<FakeLogger<FirebaseKpiContextProvider>> logger = default)
    {
        var actualRepository = kpiRepository.Or(() => new Mock<IKpiRepository>()).Object;
        var actualLogger = logger.Or(() => new FakeLogger<FirebaseKpiContextProvider>());
        return new FirebaseKpiContextProvider(actualRepository, actualLogger);
    }

    [Test]
    public async Task GetContextAsync_returns_all_kpi_documents_as_document_contexts()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetContextAsync("test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        var expected = new List<DocumentContext>
        {
            new("team-data", "team content"),
            new("manager-data", "manager content")
        };
        await Assert.That(contexts).IsEquivalentTo(expected);
    }

    [Test]
    public async Task GetContextAsync_returns_empty_when_no_documents_exist()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>());

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetContextAsync("test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEmpty();
    }

    [Test]
    public async Task GetBonusQuestionContextByCommunityAsync_returns_same_as_GetContextAsync()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextByCommunityAsync("test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEquivalentTo([new DocumentContext("team-data", "team content")]);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_includes_team_data_for_any_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("some random question", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEquivalentTo([new DocumentContext("team-data", "team content")]);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_includes_fifa_rankings_for_any_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("fifa-rankings", "Rank,Team,ELO,Published_At\n8,Marokko,1755.87,2026-05-25T10:00:00.0000000+00:00", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("some random question", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEquivalentTo(
            [new DocumentContext("fifa-rankings", "Rank,Team,ELO,Published_At\n8,Marokko,1755.87,2026-05-25T10:00:00.0000000+00:00")]);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_includes_lineups_for_exact_top_scorer_team_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("lineups", FictionalLineupsContent, "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync(TopScorerTeamQuestion, "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEquivalentTo(
            [
                new DocumentContext("team-data", "team content"),
                new DocumentContext("lineups", FictionalLineupsContent)
            ]);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_excludes_lineups_for_non_exact_top_scorer_team_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("lineups", FictionalLineupsContent, "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync($"{TopScorerTeamQuestion} ", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts).IsEquivalentTo([new DocumentContext("team-data", "team content")]);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_includes_manager_data_for_trainer_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Wie viele Trainerwechsel gibt es?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        var expected = new List<DocumentContext>
        {
            new("team-data", "team content"),
            new("manager-data", "manager content")
        };
        await Assert.That(contexts).IsEquivalentTo(expected);
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_includes_manager_data_for_relegation_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Wer belegt die Plätze 16-18?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        var expected = new List<DocumentContext>
        {
            new("team-data", "team content"),
            new("manager-data", "manager content")
        };
        await Assert.That(contexts).IsEquivalentTo(expected);
    }

    [Test]
    public async Task GetKpiDocumentContextAsync_returns_document_context_when_document_exists()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetKpiDocumentAsync("team-data", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KpiDocument("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow));

        var provider = CreateProvider(mockRepo);

        // Act
        var context = await provider.GetKpiDocumentContextAsync("team-data", "test-community");

        // Assert
        await Assert.That(context).IsEqualTo(new DocumentContext("team-data", "team content"));
    }

    [Test]
    public async Task GetKpiDocumentContextAsync_returns_null_when_document_not_found()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetKpiDocumentAsync("non-existent", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KpiDocument?)null);

        var provider = CreateProvider(mockRepo);

        // Act
        var context = await provider.GetKpiDocumentContextAsync("non-existent", "test-community");

        // Assert
        await Assert.That(context).IsNull();
    }

    [Test]
    public void Constructor_throws_when_kpiRepository_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirebaseKpiContextProvider(null!, new FakeLogger<FirebaseKpiContextProvider>()));
    }

    [Test]
    public void Constructor_throws_when_logger_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirebaseKpiContextProvider(new Mock<IKpiRepository>().Object, null!));
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_cheftrainer_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Welcher Cheftrainer wird zuerst entlassen?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert - Should include manager-data because "cheftrainer" is detected
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_entlassung_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Welche Entlassung kommt zuerst?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_coach_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Which coach will be fired first?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_returns_only_team_data_for_empty_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert - Empty string should not match trainer keywords, only return team-data
        await Assert.That(contexts).HasCount().EqualTo(1);
        await Assert.That(contexts.First().Name).IsEqualTo("team-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_returns_only_team_data_for_whitespace_question()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("   ", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert - Whitespace should not match trainer keywords, only return team-data
        await Assert.That(contexts).HasCount().EqualTo(1);
        await Assert.That(contexts.First().Name).IsEqualTo("team-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_abstieg_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Wer steigt in den Abstieg?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_absteiger_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Welche drei Absteiger?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_relegation_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Who faces relegation?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task GetBonusQuestionContextAsync_detects_abstiegsplaetze_keyword()
    {
        // Arrange
        var mockRepo = new Mock<IKpiRepository>();
        mockRepo.Setup(r => r.GetAllKpiDocumentsAsync("test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KpiDocument>
            {
                new("team-data", "team content", "desc", 0, DateTimeOffset.UtcNow),
                new("manager-data", "manager content", "desc", 0, DateTimeOffset.UtcNow)
            });

        var provider = CreateProvider(mockRepo);

        // Act
        var contexts = new List<DocumentContext>();
        await foreach (var context in provider.GetBonusQuestionContextAsync("Wer belegt die Abstiegsplätze?", "test-community"))
        {
            contexts.Add(context);
        }

        // Assert
        await Assert.That(contexts.Select(c => c.Name)).Contains("manager-data");
    }

    [Test]
    public async Task Bundesliga_champion_question_reads_only_the_two_headed_aggregate_documents()
    {
        var kpiRepository = new Mock<IKpiRepository>(MockBehavior.Strict);
        var publicationRepository = CreateBundesligaPublicationRepository();
        var provider = CreateBundesligaProvider(kpiRepository, publicationRepository);

        var contexts = await ReadAsync(provider.GetBonusQuestionContextAsync(
            Question("Wer wird Deutscher Meister?", "FC Bayern München", "Borussia Dortmund"),
            "test-community"));

        await Assert.That(contexts.Select(context => context.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary"
        ])).IsTrue();
        kpiRepository.Verify(
            repository => repository.GetAllKpiDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publicationRepository.Verify(repository => repository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.ClubElo,
            "test-community",
            It.IsAny<CancellationToken>()), Times.Once);
        publicationRepository.Verify(repository => repository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.Rosters,
            "test-community",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Bundesliga_resolved_context_records_exact_ordered_versions_hashes_and_heads()
    {
        var publicationRepository = CreateBundesligaPublicationRepository();
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            publicationRepository);
        var question = Question(TopScorerTeamQuestion, "Borussia Dortmund", "FC Bayern München");

        var resolved = await provider.ResolveBonusQuestionContextAsync(question, "test-community");

        await Assert.That(resolved.Documents.Select(document => document.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary",
            "roster-bvb",
            "roster-fcb"
        ])).IsTrue();
        await Assert.That(resolved.Manifest.Documents.Select(document => document.Kind).SequenceEqual(
            ["Kpi", "Kpi", "Context", "Context"])).IsTrue();
        await Assert.That(resolved.Manifest.Documents.Select(document => document.Name)
            .SequenceEqual(resolved.Documents.Select(document => document.Name))).IsTrue();
        await Assert.That(resolved.Manifest.Documents.Zip(resolved.Documents).All(pair =>
            string.Equals(
                pair.First.ContentSha256,
                DocumentPublicationContract.ComputeContentSha256(pair.Second.Content),
                StringComparison.Ordinal))).IsTrue();
        await Assert.That(DocumentPublicationContract.IsLowercaseSha256(
            resolved.Manifest.RosterPublicationSnapshotId)).IsTrue();
        await Assert.That(DocumentPublicationContract.IsLowercaseSha256(
            resolved.Manifest.ClubEloPublicationSnapshotId)).IsTrue();
        await Assert.That(resolved.Selection.Category).IsEqualTo(BundesligaBonusQuestionCategory.TopScorer);
        await Assert.That(resolved.Selection.SelectedDocumentNames.SequenceEqual(
            resolved.Documents.Select(document => document.Name))).IsTrue();
        await Assert.That(resolved.Selection.EstimatedUtf8Bytes).IsGreaterThan(0);
        await Assert.That(resolved.Selection.EstimatedTokens)
            .IsEqualTo((resolved.Selection.EstimatedUtf8Bytes + 3) / 4);
        await Assert.That(resolved.Selection.Budget).IsEqualTo(BonusContextBudget.Default);
        await Assert.That(resolved.Selection.ExcludedDocuments[0].Document.Name).IsEqualTo("team-rosters");
        await Assert.That(resolved.Selection.ExcludedDocuments[0].Reason)
            .IsEqualTo(BonusContextExclusionReason.ProhibitedAggregate);
    }

    [Test]
    public async Task Bundesliga_budget_rejects_the_complete_selected_set_without_partial_roster_context()
    {
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            CreateBundesligaPublicationRepository());
        var question = Question(TopScorerTeamQuestion, "Borussia Dortmund", "FC Bayern München");

        await Assert.That(async () => await provider.ResolveBonusQuestionContextAsync(
                question,
                "test-community",
                budget: new BonusContextBudget(3, 32_000)))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("all 4 selected documents");
    }

    [Test]
    public async Task Fixed_representative_categories_keep_P0_document_sets_and_fit_default_budgets()
    {
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            CreateBundesligaPublicationRepository());
        var rosters = BundesligaRosterPublication.ReconstructLastKnownGood(CreateCanonicalRosterPublication());
        var player = rosters.Snapshots
            .SelectMany(snapshot => snapshot.Members
                .Where(member => member.Role == BundesligaRosterRole.Player)
                .Select(member => member.Name))
            .First();
        var coach = rosters.Snapshots
            .SelectMany(snapshot => snapshot.Members
                .Where(member => member.Role == BundesligaRosterRole.Coach)
                .Select(member => member.Name))
            .First();
        var questions = new[]
        {
            Question("Wer wird Deutscher Meister?", "FC Bayern München"),
            Question("Welche drei Mannschaften steigen ab?", "FC Bayern München"),
            Question("Wer wird Torschützenkönig?", player),
            Question("Welcher Trainer wird zuerst entlassen?", coach),
            Question("Wie viele Tore fallen am ersten Spieltag?", "Mehr", "Weniger")
        };

        var resolved = new List<ResolvedBonusContext>();
        foreach (var question in questions)
        {
            resolved.Add(await provider.ResolveBonusQuestionContextAsync(question, "test-community"));
        }

        await Assert.That(resolved.Select(value => value.Selection.Category).SequenceEqual(
        [
            BundesligaBonusQuestionCategory.Champion,
            BundesligaBonusQuestionCategory.Relegation,
            BundesligaBonusQuestionCategory.TopScorer,
            BundesligaBonusQuestionCategory.Coach,
            BundesligaBonusQuestionCategory.Unknown
        ])).IsTrue();
        await Assert.That(resolved.Select(value => value.Documents.Length).SequenceEqual([2, 2, 3, 3, 2])).IsTrue();
        await Assert.That(resolved.All(value =>
            value.Documents.Length <= BonusContextBudget.DefaultMaximumDocuments
            && value.Selection.EstimatedTokens <= BonusContextBudget.DefaultMaximumEstimatedTokens)).IsTrue();

        var measurements = string.Join(';', resolved.Select(value =>
            $"{value.Selection.Category}={value.Documents.Length}/{value.Selection.EstimatedUtf8Bytes}/{value.Selection.EstimatedTokens}"));
        await Assert.That(measurements).IsEqualTo(
            "Champion=2/2250/563;Relegation=2/2250/563;TopScorer=3/4506/1127;" +
            "Coach=3/4506/1127;Unknown=2/2250/563");
    }

    [Test]
    public async Task Bundesliga_top_scorer_question_adds_only_the_exact_option_team_roster()
    {
        var publicationRepository = CreateBundesligaPublicationRepository();
        var provider = CreateBundesligaProvider(new Mock<IKpiRepository>(MockBehavior.Strict), publicationRepository);

        var contexts = await ReadAsync(provider.GetBonusQuestionContextAsync(
            Question(TopScorerTeamQuestion, "FC Bayern München"),
            "test-community"));

        await Assert.That(contexts.Select(context => context.Name).SequenceEqual(
        [
            "club-elo-rankings",
            "team-squad-summary",
            "roster-fcb"
        ])).IsTrue();
        await Assert.That(contexts.Select(context => context.Name)).DoesNotContain("team-rosters");
        await Assert.That(contexts.Select(context => context.Name)).DoesNotContain("fifa-rankings");
        await Assert.That(contexts.Select(context => context.Name)).DoesNotContain("lineups");
    }

    [Test]
    public async Task Bundesliga_coach_question_maps_an_exact_roster_member_option()
    {
        var rosterPublication = CreateCanonicalRosterPublication();
        var reconstructed = BundesligaRosterPublication.ReconstructLastKnownGood(rosterPublication);
        var target = reconstructed.Snapshots
            .Select(snapshot => new
            {
                snapshot.Team.TeamSlug,
                Coach = snapshot.Members.Single(member => member.Role == BundesligaRosterRole.Coach).Name
            })
            .First();
        var publicationRepository = CreateBundesligaPublicationRepository(rosterPublication);
        var provider = CreateBundesligaProvider(new Mock<IKpiRepository>(MockBehavior.Strict), publicationRepository);

        var contexts = await ReadAsync(provider.GetBonusQuestionContextAsync(
            Question("Welcher Trainer wird zuerst entlassen?", target.Coach),
            "test-community"));

        await Assert.That(contexts.Select(context => context.Name))
            .Contains($"roster-{target.TeamSlug}");
        await Assert.That(contexts.Select(context => context.Name)).DoesNotContain("manager-data");
    }

    [Test]
    public async Task Bundesliga_roster_question_without_exact_identity_fails_actionably()
    {
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            CreateBundesligaPublicationRepository());

        await Assert.That(async () => await ReadAsync(provider.GetBonusQuestionContextAsync(
                Question("Welcher Trainer wird zuerst entlassen?", "Unbekannte Person"),
                "test-community")))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("requires targeted roster context");
    }

    [Test]
    public async Task Bundesliga_missing_publication_fails_with_the_collection_command()
    {
        var publicationRepository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        publicationRepository.Setup(repository => repository.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.ClubElo,
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            publicationRepository);

        await Assert.That(async () => await ReadAsync(provider.GetBonusQuestionContextAsync(
                Question("Wer wird Deutscher Meister?", "FC Bayern München"),
                "test-community")))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("collect-context club-elo");
    }

    [Test]
    public async Task Bundesliga_string_only_selection_is_rejected_before_any_repository_read()
    {
        var publicationRepository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        var provider = CreateBundesligaProvider(
            new Mock<IKpiRepository>(MockBehavior.Strict),
            publicationRepository);

        await Assert.That(async () => await ReadAsync(provider.GetBonusQuestionContextAsync(
                TopScorerTeamQuestion,
                "test-community")))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("complete BonusQuestion");
    }

    [Test]
    public async Task World_cup_full_question_keeps_fifa_and_lineups_without_Bundesliga_documents()
    {
        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(repository => repository.GetAllKpiDocumentsAsync(
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new KpiDocument("fifa-rankings", "fifa", "desc", 1, DateTimeOffset.UnixEpoch),
                new KpiDocument("lineups", "lineups", "desc", 1, DateTimeOffset.UnixEpoch),
                new KpiDocument("club-elo-rankings", "elo", "desc", 1, DateTimeOffset.UnixEpoch),
                new KpiDocument("team-squad-summary", "summary", "desc", 1, DateTimeOffset.UnixEpoch)
            ]);
        var provider = new FirebaseKpiContextProvider(
            CompetitionIds.FifaWorldCup2026,
            kpiRepository.Object,
            null,
            new FakeLogger<FirebaseKpiContextProvider>());

        var contexts = await ReadAsync(provider.GetBonusQuestionContextAsync(
            Question(TopScorerTeamQuestion, "Deutschland"),
            "test-community"));

        await Assert.That(contexts.Select(context => context.Name).SequenceEqual(
        [
            "fifa-rankings",
            "lineups"
        ])).IsTrue();
    }

    private static FirebaseKpiContextProvider CreateBundesligaProvider(
        Mock<IKpiRepository> kpiRepository,
        Mock<IDocumentPublicationRepository> publicationRepository) => new(
        CompetitionIds.Bundesliga2026_27,
        kpiRepository.Object,
        publicationRepository.Object,
        new FakeLogger<FirebaseKpiContextProvider>());

    private static Mock<IDocumentPublicationRepository> CreateBundesligaPublicationRepository(
        LoadedDocumentPublication? rosterPublication = null)
    {
        var repository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.ClubElo,
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalClubEloPublication());
        repository.Setup(value => value.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.Rosters,
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rosterPublication ?? CreateCanonicalRosterPublication());
        return repository;
    }

    private static LoadedDocumentPublication CreateCanonicalRosterPublication()
    {
        var snapshots = BundesligaRosterSeed.Default.Entries
            .GroupBy(entry => entry.TeamSlug)
            .Select(group => new BundesligaRosterClubSnapshot(
                BundesligaTeamManifest.Default.GetByTeamSlug(group.Key),
                group.First().MembershipAsOf,
                BundesligaRosterMembershipSource.FallbackSeed,
                group.Select(entry => new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId)).ToArray()))
            .OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal)
            .ToArray();
        var rows = snapshots.Select(snapshot =>
        {
            var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
            var diagnostics = players.Any(player => player.TransfermarktPlayerId is null)
                ? new[] { $"MISSING_STABLE_PLAYER_IDS:{players.Count(player => player.TransfermarktPlayerId is null)}" }
                : [];
            return new BundesligaRosterQualityReportRow(
                snapshot.Team,
                snapshot.MembershipSource,
                snapshot.MembershipAsOf,
                [snapshot.Team.OfficialRosterSourceUrl],
                null,
                null,
                null,
                players.Length,
                1,
                players.Count(player => player.TransfermarktPlayerId is not null),
                0,
                0,
                0,
                BundesligaRosterDuckDbGateResult.NotAvailable,
                "DUCKDB_NOT_AVAILABLE_USE_FALLBACK_SEED",
                diagnostics);
        }).ToArray();
        var build = BundesligaRosterPublication.Build(snapshots, rows);
        return CreateCanonicalPublication(BundesligaDocumentPublication.Rosters, build.Documents, build.MetadataJson);
    }

    private static LoadedDocumentPublication CreateCanonicalClubEloPublication()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default,
            BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        return CreateCanonicalPublication(BundesligaDocumentPublication.ClubElo, build.Documents, build.MetadataJson);
    }

    private static LoadedDocumentPublication CreateCanonicalPublication(
        DocumentPublicationDefinition definition,
        IReadOnlyList<DocumentPublicationPayload> payloads,
        string metadataJson)
    {
        var documents = payloads.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27,
            "test-community",
            definition.PublicationSet,
            payload.Kind,
            payload.Name,
            index + 1,
            payload.Content,
            payload.Description,
            DateTimeOffset.UnixEpoch)).ToArray();
        var snapshotId = DocumentPublicationContract.ComputeSnapshotId(payloads);
        return new LoadedDocumentPublication(
            new DocumentPublicationSnapshot(
                CompetitionIds.Bundesliga2026_27,
                "test-community",
                definition.PublicationSet,
                snapshotId,
                null,
                DateTimeOffset.UnixEpoch,
                metadataJson,
                documents.Select(document => new DocumentPublicationEntry(
                    document.Kind,
                    document.Name,
                    document.Version,
                    DocumentPublicationContract.ComputeContentSha256(document.Content)))),
            documents);
    }

    private static BonusQuestion Question(string text, params string[] options) => new(
        text,
        default,
        options.Select((option, index) => new BonusQuestionOption(index.ToString(), option)).ToList(),
        1);

    private static async Task<IReadOnlyList<DocumentContext>> ReadAsync(
        IAsyncEnumerable<DocumentContext> values)
    {
        var result = new List<DocumentContext>();
        await foreach (var value in values)
        {
            result.Add(value);
        }

        return result;
    }
}
