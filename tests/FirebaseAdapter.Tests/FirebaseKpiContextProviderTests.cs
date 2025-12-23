using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

/// <summary>
/// Tests for FirebaseKpiContextProvider.
/// </summary>
[NotInParallel("FirestoreEmulator")]
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
        await Assert.That(contexts).HasCount().EqualTo(2);
        await Assert.That(contexts[0].Name).IsEqualTo("team-data");
        await Assert.That(contexts[0].Content).IsEqualTo("team content");
        await Assert.That(contexts[1].Name).IsEqualTo("manager-data");
        await Assert.That(contexts[1].Content).IsEqualTo("manager content");
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
        await Assert.That(contexts).HasCount().EqualTo(1);
        await Assert.That(contexts[0].Name).IsEqualTo("team-data");
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
        await Assert.That(contexts).HasCount().EqualTo(1);
        await Assert.That(contexts[0].Name).IsEqualTo("team-data");
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
        await Assert.That(contexts).HasCount().EqualTo(2);
        await Assert.That(contexts.Any(c => c.Name == "team-data")).IsTrue();
        await Assert.That(contexts.Any(c => c.Name == "manager-data")).IsTrue();
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
        await Assert.That(contexts).HasCount().EqualTo(2);
        await Assert.That(contexts.Any(c => c.Name == "team-data")).IsTrue();
        await Assert.That(contexts.Any(c => c.Name == "manager-data")).IsTrue();
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
        await Assert.That(context).IsNotNull();
        await Assert.That(context!.Name).IsEqualTo("team-data");
        await Assert.That(context.Content).IsEqualTo("team content");
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
}
