using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchComparisonCommand_Output_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Comparison_summary_table_is_displayed()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Comparison Summary");
    }

    [Test]
    public async Task Summary_table_contains_mode_columns()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Mode");
        await Assert.That(output).Contains("Successful");
        await Assert.That(output).Contains("Failed");
        await Assert.That(output).Contains("Total cost");
        await Assert.That(output).Contains("Average cost");
        // Column header may be wrapped across lines in table rendering
        await Assert.That(output).Contains("duration");
    }

    [Test]
    public async Task Summary_table_contains_both_mode_rows()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("With justification");
        await Assert.That(output).Contains("Without justification");
    }

    [Test]
    public async Task Summary_table_contains_combined_row()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Combined");
    }

    [Test]
    public async Task Prediction_distribution_table_is_displayed()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Prediction distribution");
    }

    [Test]
    public async Task Distribution_table_shows_prediction_scores()
    {
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var context = CreateComparisonCommandApp(predictionResult: prediction);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("2:1");
    }

    [Test]
    public async Task Cost_formatting_uses_invariant_culture()
    {
        var mockTracker = CreateMockTokenUsageTracker(lastCost: 1.2345m);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(tokenUsageTracker: mockTracker);
        var context = CreateComparisonCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("$1.2345");
    }

    [Test]
    public async Task Verbose_context_retrieval_shows_document_lookup_count()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1", "--verbose");

        await Assert.That(output).Contains("Looking for 7 required context documents in database");
    }

    [Test]
    public async Task Show_context_documents_flag_displays_content_preview()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(
            context, "--runs", "1", "--show-context-documents");

        await Assert.That(output).Contains("Position,Team,Points");
    }

    [Test]
    public async Task Usage_summary_shown_per_run()
    {
        var mockTracker = CreateMockTokenUsageTracker(compactSummary: "200/100/300/400/$0.02");
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(tokenUsageTracker: mockTracker);
        var context = CreateComparisonCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("200/100/300/400/$0.02");
    }

    [Test]
    public async Task No_context_documents_shows_warning()
    {
        var context = CreateComparisonCommandApp(
            contextDocuments: new Dictionary<string, ContextDocument>());
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("No context documents retrieved");
    }

    [Test]
    public async Task Match_resolved_via_kicktipp_shows_schedule_message()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Using match metadata from Kicktipp schedule");
    }

    [Test]
    public async Task Verbose_shows_retrieved_optional_transfer_documents()
    {
        var docs = CreateDefaultContextDocuments();
        docs[$"{HomeAbbreviation}-transfers.csv"] = CreateContextDocument(
            documentName: $"{HomeAbbreviation}-transfers.csv",
            content: "Transfer,Fee\nPlayer A,10M");
        var context = CreateComparisonCommandApp(contextDocuments: docs);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1", "--verbose");

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
        var context = CreateComparisonCommandApp(contextDocuments: docs);
        var (_, output) = await RunComparisonAsync(
            context, "--runs", "1", "--show-context-documents");

        await Assert.That(output).Contains("15 more lines");
    }

    [Test]
    public async Task Unknown_team_names_use_fallback_abbreviations()
    {
        var docs = CreateMatchContextDocuments(homeAbbreviation: "ta", awayAbbreviation: "tb");
        var match = CreateMatchWithHistory(
            match: CreateMatch(homeTeam: "Team Alpha", awayTeam: "Team Beta", matchday: DefaultMatchday));
        var context = CreateComparisonCommandApp(contextDocuments: docs, matchesWithHistory: new List<MatchWithHistory> { match });
        var (exitCode, output) = await RunComparisonAsync(
            context, "--runs", "1",
            "--home", "Team Alpha", "--away", "Team Beta");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Loaded context documents:");
    }
}
