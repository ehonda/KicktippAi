using System.ComponentModel;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Tests for <see cref="VerifyMatchdayCommand"/> outdated prediction detection logic.
/// </summary>
public class VerifyMatchdayCommand_Outdated_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    [Description(
        """
        Regression test for the stale-metadata root cause behind an unnecessary reprediction.
        The real production failure required two predictions to exist in storage: repredictionIndex 0 was once outdated,
        then repredictionIndex 1 was created from the updated context and was no longer outdated.
        This test recreates that exact inconsistency at the command boundary by mocking the repository so GetPredictionAsync returns
        the latest prediction while GetPredictionMetadataAsync incorrectly returns metadata for the older prediction.
        The verify command then marks the latest prediction as outdated even though the newer prediction already incorporated those context updates.
        The repository now prevents this by ordering metadata lookups by repredictionIndex descending, but this test intentionally mocks the old bug back in so the failure mode stays documented.
        """)]
    public async Task Stale_prediction_metadata_from_initial_prediction_can_mark_latest_prediction_as_outdated()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Bayer 04 Leverkusen", awayTeam: "FC Bayern München", matchday: 26);
        var initialPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var latestPrediction = CreatePrediction(homeGoals: 1, awayGoals: 3);
        var staleMetadata = CreatePredictionMetadata(
            prediction: initialPrediction,
            createdAt: new DateTimeOffset(2026, 3, 9, 1, 38, 45, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "recent-history-fcb.csv", "away-history-fcb.csv" });

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: latestPrediction,
            getPredictionMetadataResult: staleMetadata);

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-fcb.csv"] = CreateContextDocument(
                    documentName: "recent-history-fcb.csv",
                    createdAt: new DateTimeOffset(2026, 3, 11, 1, 37, 59, TimeSpan.Zero)),
                ["away-history-fcb.csv"] = CreateContextDocument(
                    documentName: "away-history-fcb.csv",
                    createdAt: new DateTimeOffset(2026, 3, 11, 1, 37, 59, TimeSpan.Zero))
            });

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 1, awayGoals: 3)),
            firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "o4-mini", "-c", "ehonda-ai-arena", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Status:").And.Contains("Outdated");
        await Assert.That(output).DoesNotContain("Mismatch");
    }

    [Test]
    [Description(
        """
        Regression test for the cancelled-match variant of the stale-metadata bug.
        Cancelled matches use team-name-only repository methods, but the failure mode is the same: the latest stored prediction can be current while
        GetCancelledMatchPredictionMetadataAsync accidentally returns metadata for an older prediction version.
        That stale timestamp makes VerifyMatchdayCommand treat the latest cancelled-match prediction as outdated.
        The repository fix now orders cancelled-match metadata lookups by repredictionIndex descending as well, but this test keeps the old inconsistency reproducible via mocks.
        """)]
    public async Task Stale_cancelled_match_prediction_metadata_can_mark_latest_prediction_as_outdated()
    {
        // Arrange
        var cancelledMatch = CreateMatch(
            homeTeam: "Bayer 04 Leverkusen",
            awayTeam: "FC Bayern München",
            matchday: 26,
            isCancelled: true);
        var initialPrediction = CreatePrediction(homeGoals: 0, awayGoals: 0);
        var latestPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var staleMetadata = CreatePredictionMetadata(
            prediction: initialPrediction,
            createdAt: new DateTimeOffset(2026, 3, 9, 1, 38, 45, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var mockPredictionRepository = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: latestPrediction,
            getCancelledMatchPredictionMetadataResult: staleMetadata);

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-fcb.csv"] = CreateContextDocument(
                    documentName: "recent-history-fcb.csv",
                    createdAt: new DateTimeOffset(2026, 3, 11, 1, 37, 59, TimeSpan.Zero))
            });

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(cancelledMatch, CreateBetPrediction(homeGoals: 1, awayGoals: 1)),
            firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "o4-mini", "-c", "ehonda-ai-arena", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Status:").And.Contains("Outdated");
    }

    [Test]
    public async Task Prediction_with_newer_context_document_is_outdated()
    {
        // Arrange
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero); // Later

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(
                documentName: "recent-history-fcb.csv",
                createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Verbose_mode_shows_context_document_update_timestamps_when_outdated()
    {
        // Arrange
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(
                documentName: "recent-history-fcb.csv",
                createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert
        await Assert.That(output).Contains("recent-history-fcb.csv").And.Contains("updated after prediction");
    }

    [Test]
    public async Task Prediction_with_older_context_document_is_not_outdated()
    {
        // Arrange
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 14, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero); // Earlier

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(
                documentName: "recent-history-fcb.csv",
                createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All predictions match - verification successful");
    }

    [Test]
    public async Task Bundesliga_standings_is_excluded_from_outdated_check()
    {
        // Arrange
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero); // Later

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "bundesliga-standings.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert - should still pass despite context being newer
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipping outdated check for 'bundesliga-standings.csv'");
    }

    [Test]
    public async Task Display_suffix_is_stripped_from_context_document_names()
    {
        // Arrange - document name with display suffix like " (kpi-context)"
        var match = CreateTestMatch();
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv (kpi-context)" });

        // Context repo stores without suffix
        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(
                documentName: "recent-history-fcb.csv",
                createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert - should detect outdated because suffix was stripped and match found
        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task Missing_context_document_in_repo_shows_warning_in_verbose()
    {
        // Arrange
        var match = CreateTestMatch();
        var metadata = CreatePredictionMetadata(
            createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "missing-document.csv" });

        // Empty context documents - document won't be found
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: new Dictionary<string, ContextDocument>());

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert - should pass (missing = not outdated) but show warning
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning: Context document 'missing-document.csv' not found in repository");
    }

    [Test]
    public async Task No_context_documents_used_means_not_outdated()
    {
        // Arrange
        var match = CreateTestMatch();
        var metadata = CreatePredictionMetadata(
            createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string>()); // Empty list

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Null_metadata_means_not_outdated()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: (PredictionMetadata?)null);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Agent_mode_shows_outdated_reason()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Team A vs Team B");
        await Assert.That(output).Contains("(outdated)");
    }

    [Test]
    public async Task Non_agent_mode_shows_outdated_status_details()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var contextUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var metadata = CreatePredictionMetadata(
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "recent-history-fcb.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(createdAt: contextUpdatedAt)
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(output).Contains("Status:").And.Contains("Outdated");
    }

    [Test]
    public async Task Verbose_mode_shows_context_document_check_count()
    {
        // Arrange
        var match = CreateTestMatch();
        var metadata = CreatePredictionMetadata(
            createdAt: new DateTimeOffset(2025, 1, 10, 14, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "doc1.csv", "doc2.csv", "doc3.csv" });

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["doc1.csv"] = CreateContextDocument(createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            ["doc2.csv"] = CreateContextDocument(createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            ["doc3.csv"] = CreateContextDocument(createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero))
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1),
            predictionMetadata: metadata,
            contextDocumentsByName: contextDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert
        await Assert.That(output).Contains("Checking 3 context documents for updates");
    }

    [Test]
    public async Task Check_outdated_without_database_prediction_skips_outdated_check()
    {
        // Arrange - kicktipp has prediction, database does not
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: (Prediction?)null);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert - fails due to mismatch, not outdated check
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).DoesNotContain("Checking").Or.DoesNotContain("context documents for updates");
    }
}
