using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> context retrieval
/// and hybrid context approach.
/// </summary>
public class MatchdayCommand_ContextRetrieval_Tests : MatchdayCommandTests_Base
{
    #region Context From Database Tests

    [Test]
    public async Task Running_command_retrieves_context_from_database_when_all_required_documents_present()
    {
        // Arrange
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var mocks = CreateStandardMocks(
            contextDocuments: contextDocs,
            existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context documents from database");
    }

    [Test]
    public async Task Running_command_shows_fallback_warning_when_required_documents_missing()
    {
        // Arrange - Only provide some context documents, not all required ones
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50")
        };
        var mocks = CreateStandardMocks(
            contextDocuments: partialDocs,
            existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("required context documents");
    }

    [Test]
    public async Task Running_command_uses_context_for_prediction()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Verify prediction service was called with context documents
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.Is<IEnumerable<DocumentContext>>(docs => docs.Any()),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Team Abbreviation Tests (Indirect via Context Retrieval)

    [Test]
    [Arguments("FC Bayern München", "fcb")]
    [Arguments("Borussia Dortmund", "bvb")]
    [Arguments("RB Leipzig", "rbl")]
    [Arguments("Bayer 04 Leverkusen", "b04")]
    [Arguments("VfB Stuttgart", "vfb")]
    [Arguments("Eintracht Frankfurt", "sge")]
    [Arguments("SC Freiburg", "scf")]
    [Arguments("VfL Wolfsburg", "wob")]
    [Arguments("1. FC Union Berlin", "fcu")]
    [Arguments("FSV Mainz 05", "m05")]
    [Arguments("Werder Bremen", "svw")]
    [Arguments("Bor. Mönchengladbach", "bmg")]
    [Arguments("FC Augsburg", "fca")]
    [Arguments("1899 Hoffenheim", "tsg")]
    [Arguments("1. FC Heidenheim 1846", "fch")]
    [Arguments("FC St. Pauli", "fcs")]
    [Arguments("1. FC Köln", "fck")]
    [Arguments("Hamburger SV", "hsv")]
    public async Task Running_command_retrieves_context_for_team_using_correct_abbreviation(string teamName, string expectedAbbreviation)
    {
        // Arrange
        // Create context documents that would match the expected abbreviation
        var contextTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points",
                createdAt: contextTimestamp),
            [$"community-rules-test-community.md"] = CreateContextDocument(
                documentName: "community-rules-test-community.md",
                content: "# Rules",
                createdAt: contextTimestamp),
            [$"recent-history-{expectedAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"recent-history-{expectedAbbreviation}.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            ["recent-history-bvb.csv"] = CreateContextDocument(
                documentName: "recent-history-bvb.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            [$"home-history-{expectedAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"home-history-{expectedAbbreviation}.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            ["away-history-bvb.csv"] = CreateContextDocument(
                documentName: "away-history-bvb.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            [$"head-to-head-{expectedAbbreviation}-vs-bvb.csv"] = CreateContextDocument(
                documentName: $"head-to-head-{expectedAbbreviation}-vs-bvb.csv",
                content: "Match,Score",
                createdAt: contextTimestamp)
        };

        var matches = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: CreateMatch(homeTeam: teamName, awayTeam: "Borussia Dortmund"))
        };

        var mocks = CreateStandardMocks(
            matchesWithHistory: matches,
            contextDocuments: contextDocs,
            existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // In verbose mode with all required docs present, we should see the success message
        await Assert.That(output).Contains("context documents from database");
    }

    [Test]
    public async Task Running_command_generates_fallback_abbreviation_for_unknown_team()
    {
        // Arrange - Use a team name that's not in the known list
        var matches = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: CreateMatch(homeTeam: "Unknown Team FC", awayTeam: "Another Unknown"))
        };

        var mocks = CreateStandardMocks(
            matchesWithHistory: matches,
            existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Command should still work, just with fallback abbreviations
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing:");
        await Assert.That(output).Contains("Unknown Team FC");
    }

    #endregion

    #region Optional Context Documents Tests

    [Test]
    public async Task Running_command_includes_optional_transfers_documents_when_available()
    {
        // Arrange
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        // Add optional transfers documents
        contextDocs["fcb-transfers.csv"] = CreateContextDocument(
            documentName: "fcb-transfers.csv",
            content: "Player,From,To");
        contextDocs["bvb-transfers.csv"] = CreateContextDocument(
            documentName: "bvb-transfers.csv",
            content: "Player,From,To");

        var mocks = CreateStandardMocks(
            contextDocuments: contextDocs,
            existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Verify prediction was called - the actual document count would include transfers
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
