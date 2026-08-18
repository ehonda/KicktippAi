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
    [Test]
    public async Task Running_command_retrieves_context_from_database_when_all_required_documents_present()
    {
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var ctx = CreateMatchdayCommandApp(contextDocuments: contextDocs, existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using 11 context documents");
    }

    [Test]
    public async Task Running_command_shows_fallback_warning_when_required_documents_missing()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateMatchdayCommandApp(contextDocuments: partialDocs, existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Missing required Bundesliga context document");
    }

    [Test]
    public async Task Running_command_uses_context_for_prediction()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.Is<IEnumerable<DocumentContext>>(docs => docs.Any()), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    [Arguments("FC Bayern München", "fcb")]
    [Arguments("Borussia Dortmund", "bvb")]
    [Arguments("RB Leipzig", "rbl")]
    [Arguments("Bayer 04 Leverkusen", "b04")]
    [Arguments("VfB Stuttgart", "vfb")]
    [Arguments("Eintracht Frankfurt", "sge")]
    [Arguments("SC Freiburg", "scf")]
    [Arguments("SV Elversberg", "sve")]
    [Arguments("1. FC Union Berlin", "fcu")]
    [Arguments("FSV Mainz 05", "m05")]
    [Arguments("Werder Bremen", "svw")]
    [Arguments("Bor. Mönchengladbach", "bmg")]
    [Arguments("FC Augsburg", "fca")]
    [Arguments("1899 Hoffenheim", "tsg")]
    [Arguments("SC Paderborn 07", "scp")]
    [Arguments("FC Schalke 04", "s04")]
    [Arguments("1. FC Köln", "fck")]
    [Arguments("Hamburger SV", "hsv")]
    public async Task Running_command_retrieves_context_for_team_using_correct_abbreviation(string teamName, string expectedAbbreviation)
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var awayTeam = teamName == "Borussia Dortmund" ? "FC Bayern München" : "Borussia Dortmund";
        var awayAbbreviation = awayTeam == "FC Bayern München" ? "fcb" : "bvb";
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
            [$"recent-history-{awayAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"recent-history-{awayAbbreviation}.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            [$"home-history-{expectedAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"home-history-{expectedAbbreviation}.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            [$"away-history-{awayAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"away-history-{awayAbbreviation}.csv",
                content: "Match,Result",
                createdAt: contextTimestamp),
            [$"head-to-head-{expectedAbbreviation}-vs-{awayAbbreviation}.csv"] = CreateContextDocument(
                documentName: $"head-to-head-{expectedAbbreviation}-vs-{awayAbbreviation}.csv",
                content: "Match,Score",
                createdAt: contextTimestamp)
        };

        var matches = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: CreateMatch(homeTeam: teamName, awayTeam: awayTeam))
        };

        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matches, contextDocuments: contextDocs, existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using 11 context documents");
    }

    [Test]
    public async Task Running_command_rejects_unknown_bundesliga_team_instead_of_generating_a_slug()
    {
        var matches = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: CreateMatch(homeTeam: "Unknown Team FC", awayTeam: "Another Unknown"))
        };
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matches, existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("automatic slug fallback is disabled");
    }

}
