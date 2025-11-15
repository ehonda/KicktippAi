using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TestUtilities;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictMatchAsync method
/// </summary>
public class PredictionService_PredictMatchAsync_Tests : PredictionServiceTests_Base
{
    [Test]
    [MethodDataSource(nameof(GetPredictMatchScenarios))]
    public async Task Predicting_match_scenarios(PredictMatchTestCase testCase)
    {
        var result = await RunPredictMatchScenarioAsync(testCase.Scenario);
        await testCase.AssertResult(result);
    }

    [Test]
    public async Task Predicting_match_calls_token_tracker_with_correct_usage()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var scenario = new PredictMatchScenario
        {
            Usage = usage
        };

        // Act
        await RunPredictMatchScenarioAsync(scenario);

        // Assert
        TokenUsageTracker.Verify(
            t => t.AddUsage(Model, usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var scenario = new PredictMatchScenario
        {
            Usage = usage
        };

        // Act
        await RunPredictMatchScenarioAsync(scenario);

        // Assert
        CostCalculationService.Verify(
            c => c.LogCostBreakdown(Model, usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_logs_information_message()
    {
        await RunPredictMatchScenarioAsync();

        // Assert
        Logger.AssertLogContains(LogLevel.Information, "Generating prediction for match");
    }

    [Test]
    public async Task Predicting_match_with_API_exception_returns_null()
    {
        var scenario = new PredictMatchScenario
        {
            ClientException = new InvalidOperationException("API error")
        };

        var result = await RunPredictMatchScenarioAsync(scenario);

        // Assert
        await Assert.That(result.Prediction).IsNull();
    }

    [Test]
    public async Task Predicting_match_with_exception_logs_error()
    {
        var scenario = new PredictMatchScenario
        {
            ClientException = new InvalidOperationException("API error")
        };

        await RunPredictMatchScenarioAsync(scenario);

        // Assert
        Logger.AssertLogContains(LogLevel.Error, "Error generating prediction");
    }

    [Test]
    public async Task Predicting_match_with_invalid_JSON_returns_null()
    {
        var scenario = new PredictMatchScenario
        {
            ResponseJson = """not valid json at all"""
        };

        var result = await RunPredictMatchScenarioAsync(scenario);

        // Assert
        await Assert.That(result.Prediction).IsNull();
    }

    [SuppressMessage("TUnit", "TUnit0046", Justification = "PredictMatchTestCase is immutable and safe to reuse.")]
    public static IEnumerable<PredictMatchTestCase> GetPredictMatchScenarios()
    {
        yield return new PredictMatchTestCase(
            "prediction_without_justification",
            new PredictMatchScenario(),
            async result =>
            {
                await Assert.That(result.Prediction).IsNotNull();
                var prediction = result.Prediction!;
                await Assert.That(prediction.HomeGoals).IsEqualTo(2);
                await Assert.That(prediction.AwayGoals).IsEqualTo(1);
                await Assert.That(prediction.Justification).IsNull();
            });

        yield return new PredictMatchTestCase(
            "prediction_with_justification",
            new PredictMatchScenario
            {
                IncludeJustification = true,
                ResponseJson = """
                {
                    "home": 3,
                    "away": 1,
                    "justification": {
                        "keyReasoning": "Bayern Munich has strong home form",
                        "contextSources": {
                            "mostValuable": [
                                {
                                    "documentName": "Team Stats",
                                    "details": "Bayern's recent winning streak"
                                }
                            ],
                            "leastValuable": []
                        },
                        "uncertainties": ["Weather conditions unclear"]
                    }
                }
                """
            },
            async result =>
            {
                await Assert.That(result.Prediction).IsNotNull();
                var prediction = result.Prediction!;
                await Assert.That(prediction.HomeGoals).IsEqualTo(3);
                await Assert.That(prediction.AwayGoals).IsEqualTo(1);
                await Assert.That(prediction.Justification).IsNotNull();
                await Assert.That(prediction.Justification!.KeyReasoning).IsEqualTo("Bayern Munich has strong home form");
                await Assert.That(prediction.Justification.ContextSources.MostValuable.Count).IsEqualTo(1);
                await Assert.That(prediction.Justification.ContextSources.MostValuable[0].DocumentName).IsEqualTo("Team Stats");
                await Assert.That(prediction.Justification.Uncertainties.Count).IsEqualTo(1);
            });

        yield return new PredictMatchTestCase(
            "prediction_with_empty_context_documents",
            new PredictMatchScenario
            {
                ResponseJson = """{"home": 1, "away": 1}""",
                ContextDocuments = Array.Empty<DocumentContext>()
            },
            async result =>
            {
                await Assert.That(result.Prediction).IsNotNull();
                var prediction = result.Prediction!;
                await Assert.That(prediction.HomeGoals).IsEqualTo(1);
                await Assert.That(prediction.AwayGoals).IsEqualTo(1);
            });
    }

    public record PredictMatchTestCase(
        string Name,
        PredictMatchScenario Scenario,
        Func<PredictMatchScenarioResult, Task> AssertResult)
    {
        public override string ToString() => Name;
    }
}
