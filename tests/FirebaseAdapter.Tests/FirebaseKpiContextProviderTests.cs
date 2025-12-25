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
}
