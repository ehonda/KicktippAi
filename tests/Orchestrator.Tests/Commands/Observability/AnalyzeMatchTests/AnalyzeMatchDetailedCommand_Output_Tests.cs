using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchDetailedCommand_Output_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Summary_table_contains_expected_metric_labels()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Completed runs");
        await Assert.That(output).Contains("Successful predictions");
        await Assert.That(output).Contains("Total cost so far");
        await Assert.That(output).Contains("Average cost");
        await Assert.That(output).Contains("Projected total cost");
        await Assert.That(output).Contains("Average run time");
    }

    [Test]
    public async Task Successful_prediction_shows_checkmark()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Prediction:");
        await Assert.That(output).Contains("2:1");
    }

    [Test]
    public async Task Cost_formatting_uses_invariant_culture()
    {
        var mockTracker = CreateMockTokenUsageTracker(lastCost: 1.2345m);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(tokenUsageTracker: mockTracker);
        var context = CreateDetailedCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("$1.2345");
    }

    [Test]
    public async Task Verbose_context_retrieval_shows_document_lookup_count()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(output).Contains("Looking for 7 required context documents in database");
    }

    [Test]
    public async Task Verbose_context_retrieval_shows_retrieved_documents()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(output).Contains("Retrieved bundesliga-standings.csv");
    }

    [Test]
    public async Task Verbose_context_retrieval_shows_version_numbers()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(output).Contains("(version 1)");
    }

    [Test]
    public async Task Verbose_shows_missing_optional_transfer_documents()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates", "--verbose");

        // Transfers are optional and not included in default context documents
        await Assert.That(output).Contains("Missing optional");
    }

    [Test]
    public async Task Context_document_versions_shown_in_listing()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        // Version is displayed next to each document name
        await Assert.That(output).Contains("(v1)");
    }

    [Test]
    public async Task Show_context_documents_flag_displays_content_preview()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates", "--show-context-documents");

        // Context document content starts getting printed
        await Assert.That(output).Contains("Position,Team,Points");
    }

    [Test]
    public async Task Match_resolved_via_kicktipp_shows_schedule_message()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Using match metadata from Kicktipp schedule");
    }

    [Test]
    public async Task Runs_count_shown_in_initialization()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "5", "--no-live-estimates");

        await Assert.That(output).Contains("5");
    }

    [Test]
    public async Task No_context_documents_shows_warning()
    {
        var context = CreateDetailedCommandApp(
            contextDocuments: new Dictionary<string, ContextDocument>());
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("No context documents retrieved");
    }

    [Test]
    public async Task Justification_context_sources_are_displayed()
    {
        var prediction = CreatePredictionWithJustification();
        var context = CreateDetailedCommandApp(predictionResult: prediction);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("home-history-fcb.csv");
        await Assert.That(output).Contains("Strong home record");
    }

    [Test]
    public async Task Justification_uncertainties_are_displayed()
    {
        var prediction = CreatePredictionWithJustification();
        var context = CreateDetailedCommandApp(predictionResult: prediction);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Injury concerns for key players");
    }

    [Test]
    public async Task Usage_summary_shown_per_run()
    {
        var mockTracker = CreateMockTokenUsageTracker(compactSummary: "100/50/150/200/$0.01");
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(tokenUsageTracker: mockTracker);
        var context = CreateDetailedCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("100/50/150/200/$0.01");
    }

    [Test]
    public async Task Verbose_shows_retrieved_optional_transfer_documents()
    {
        var docs = CreateDefaultContextDocuments();
        docs[$"{HomeAbbreviation}-transfers.csv"] = CreateContextDocument(
            documentName: $"{HomeAbbreviation}-transfers.csv",
            content: "Transfer,Fee\nPlayer A,10M");
        var context = CreateDetailedCommandApp(contextDocuments: docs);
        var (_, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(output).Contains($"Retrieved optional {HomeAbbreviation}-transfers.csv");
    }

    [Test]
    public async Task Show_context_documents_truncates_long_content()
    {
        var longContent = string.Join("\n", Enumerable.Range(1, 25).Select(i => $"Line {i}"));
        var docs = CreateDefaultContextDocuments();
        docs["bundesliga-standings.csv"] = CreateContextDocument(
            documentName: "bundesliga-standings.csv",
            content: longContent);
        var context = CreateDetailedCommandApp(contextDocuments: docs);
        var (_, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates", "--show-context-documents");

        await Assert.That(output).Contains("15 more lines");
    }

    [Test]
    public async Task Unknown_team_names_use_fallback_abbreviations()
    {
        var docs = CreateMatchContextDocuments(homeAbbreviation: "ta", awayAbbreviation: "tb");
        var match = CreateMatchWithHistory(
            match: CreateMatch(homeTeam: "Team Alpha", awayTeam: "Team Beta", matchday: DefaultMatchday));
        var context = CreateDetailedCommandApp(contextDocuments: docs, matchesWithHistory: new List<MatchWithHistory> { match });
        var (exitCode, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates",
            "--home", "Team Alpha", "--away", "Team Beta");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Loaded context documents:");
    }
}
