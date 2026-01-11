using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Tests for <see cref="VerifyMatchdayCommand"/> prediction comparison logic.
/// </summary>
public class VerifyMatchdayCommand_Comparison_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Matching_predictions_returns_success()
    {
        // Arrange
        var match = CreateTestMatch();
        var kicktippPrediction = CreateBetPrediction(homeGoals: 2, awayGoals: 1);
        var databasePrediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, kicktippPrediction),
            databasePrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All predictions match - verification successful");
    }

    [Test]
    public async Task Both_null_predictions_counts_as_match()
    {
        // Arrange
        var match = CreateTestMatch();

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, (BetPrediction?)null));
        // Note: databasePrediction defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All predictions match");
    }

    [Test]
    public async Task Kicktipp_has_prediction_but_database_does_not_returns_error()
    {
        // Arrange
        var match = CreateTestMatch();
        var kicktippPrediction = CreateBetPrediction(homeGoals: 2, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, kicktippPrediction));
        // Note: databasePrediction defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed - predictions do not match");
    }

    [Test]
    public async Task Database_has_prediction_but_kicktipp_does_not_returns_error()
    {
        // Arrange
        var match = CreateTestMatch();
        var databasePrediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, (BetPrediction?)null),
            databasePrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed - predictions do not match");
    }

    [Test]
    public async Task Different_home_goals_returns_error()
    {
        // Arrange
        var match = CreateTestMatch();
        var kicktippPrediction = CreateBetPrediction(homeGoals: 2, awayGoals: 1);
        var databasePrediction = CreatePrediction(homeGoals: 3, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, kicktippPrediction),
            databasePrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Different_away_goals_returns_error()
    {
        // Arrange
        var match = CreateTestMatch();
        var kicktippPrediction = CreateBetPrediction(homeGoals: 2, awayGoals: 1);
        var databasePrediction = CreatePrediction(homeGoals: 2, awayGoals: 0);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, kicktippPrediction),
            databasePrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Verification_summary_shows_correct_counts()
    {
        // Arrange - 2 matches: one matching, one not matching
        var match1 = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var match2 = CreateTestMatch(homeTeam: "Team C", awayTeam: "Team D");

        // Set up mock to return different predictions for different matches
        var mockPredictionRepo = CreateMockPredictionRepository();
        mockPredictionRepo.Setup(r => r.GetPredictionAsync(
                match1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePrediction(homeGoals: 2, awayGoals: 1));
        mockPredictionRepo.Setup(r => r.GetPredictionAsync(
                match2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EHonda.KicktippAi.Core.Prediction?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);

        var predictions = CreatePlacedPredictions(
            (match1, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            (match2, CreateBetPrediction(homeGoals: 1, awayGoals: 0)));

        var mockKicktippClient = CreateMockKicktippClient(placedPredictions: predictions);
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Total matches: 2");
        await Assert.That(output).Contains("Matches with Kicktipp predictions: 2");
        await Assert.That(output).Contains("Matches with database predictions: 1");
        await Assert.That(output).Contains("Matching predictions: 1");
        await Assert.That(output).Contains("Discrepancies found: 1");
    }

    [Test]
    public async Task No_matches_from_kicktipp_returns_success()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: new Dictionary<EHonda.KicktippAi.Core.Match, BetPrediction?>());

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found on Kicktipp");
    }

    [Test]
    public async Task Verification_displays_community_info()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "my-community");

        // Assert
        await Assert.That(output).Contains("Using community:").And.Contains("my-community");
        await Assert.That(output).Contains("Using community context:").And.Contains("my-community");
    }

    [Test]
    public async Task Cancelled_match_displays_warning_message()
    {
        // Arrange
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var kicktippPrediction = CreateBetPrediction(homeGoals: 2, awayGoals: 1);
        var databasePrediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(cancelledMatch, kicktippPrediction),
            databasePrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert - should display warning for cancelled match
        await Assert.That(output).Contains("FC Bayern München vs Borussia Dortmund is cancelled (Abgesagt)");
        await Assert.That(output).Contains("inherited time");
        // Should still succeed if predictions match
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Multiple_cancelled_matches_display_multiple_warnings()
    {
        // Arrange
        var cancelledMatch1 = CreateMatch(
            homeTeam: "Team A",
            awayTeam: "Team B",
            matchday: 16,
            isCancelled: true);
        var cancelledMatch2 = CreateMatch(
            homeTeam: "Team C",
            awayTeam: "Team D",
            matchday: 16,
            isCancelled: true);
        var normalMatch = CreateMatch(
            homeTeam: "Team E",
            awayTeam: "Team F",
            matchday: 16,
            isCancelled: false);

        var prediction = CreateBetPrediction(homeGoals: 1, awayGoals: 0);
        var dbPrediction = CreatePrediction(homeGoals: 1, awayGoals: 0);

        var placedPredictions = new Dictionary<Match, BetPrediction?>
        {
            [cancelledMatch1] = prediction,
            [cancelledMatch2] = prediction,
            [normalMatch] = prediction
        };

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: placedPredictions,
            databasePrediction: dbPrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test-community");

        // Assert - should display warning for both cancelled matches
        await Assert.That(output).Contains("Team A vs Team B is cancelled (Abgesagt)");
        await Assert.That(output).Contains("Team C vs Team D is cancelled (Abgesagt)");
        // Normal match should NOT have cancelled warning
        await Assert.That(output).DoesNotContain("Team E vs Team F is cancelled");
    }
}
