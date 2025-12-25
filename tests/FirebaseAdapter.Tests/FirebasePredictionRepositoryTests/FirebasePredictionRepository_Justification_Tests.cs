using EHonda.KicktippAi.Core;
using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for prediction justification serialization and deserialization in FirebasePredictionRepository.
/// These tests verify that justification data is correctly persisted and retrieved.
/// </summary>
public class FirebasePredictionRepository_Justification_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_prediction_with_full_justification_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Home team has better form",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [
                    new PredictionJustificationContextSource("team-data", "Home team won 5 of last 6"),
                    new PredictionJustificationContextSource("standings", "Home team is 3rd")
                ],
                LeastValuable: [
                    new PredictionJustificationContextSource("weather-data", "Weather is neutral")
                ]),
            Uncertainties: ["Injury to key player uncertain", "Recent manager change"]);
        var prediction = new Prediction(2, 1, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: ["team-data", "standings"]);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.KeyReasoning).IsEqualTo("Home team has better form");
        await Assert.That(retrieved.Justification.ContextSources).IsNotNull();
        await Assert.That(retrieved.Justification.ContextSources!.MostValuable).HasCount().EqualTo(2);
        await Assert.That(retrieved.Justification.ContextSources.LeastValuable).HasCount().EqualTo(1);
        await Assert.That(retrieved.Justification.Uncertainties).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Saving_prediction_with_null_justification_stores_null()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var prediction = new Prediction(1, 0, null);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNull();
    }

    [Test]
    public async Task Saving_prediction_with_empty_justification_stores_null()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var emptyJustification = new PredictionJustification(
            KeyReasoning: "   ",
            ContextSources: new PredictionJustificationContextSources([], []),
            Uncertainties: []);
        var prediction = new Prediction(1, 0, emptyJustification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNull();
    }

    [Test]
    public async Task Saving_prediction_with_only_key_reasoning_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Simple reasoning",
            ContextSources: new PredictionJustificationContextSources([], []),
            Uncertainties: []);
        var prediction = new Prediction(2, 2, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.KeyReasoning).IsEqualTo("Simple reasoning");
    }

    [Test]
    public async Task Saving_prediction_with_only_context_sources_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: string.Empty,
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [new PredictionJustificationContextSource("team-data", "Key insight")],
                LeastValuable: []),
            Uncertainties: []);
        var prediction = new Prediction(0, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.ContextSources).IsNotNull();
        await Assert.That(retrieved.Justification.ContextSources!.MostValuable).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Saving_prediction_with_only_uncertainties_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: string.Empty,
            ContextSources: new PredictionJustificationContextSources([], []),
            Uncertainties: ["Injury concerns", "Weather uncertain"]);
        var prediction = new Prediction(1, 1, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.Uncertainties).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Justification_with_whitespace_only_uncertainties_filters_them_out()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Valid reasoning",
            ContextSources: new PredictionJustificationContextSources([], []),
            Uncertainties: ["Valid uncertainty", "  ", "", "Another valid one"]);
        var prediction = new Prediction(1, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.Uncertainties).HasCount().EqualTo(2);
        await Assert.That(retrieved.Justification.Uncertainties).Contains("Valid uncertainty");
        await Assert.That(retrieved.Justification.Uncertainties).Contains("Another valid one");
    }

    [Test]
    public async Task Justification_with_null_document_name_in_context_source_becomes_empty_string()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Reasoning",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [new PredictionJustificationContextSource(null!, "Details provided")],
                LeastValuable: []),
            Uncertainties: []);
        var prediction = new Prediction(2, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.ContextSources!.MostValuable.First().DocumentName).IsEqualTo(string.Empty);
        await Assert.That(retrieved.Justification.ContextSources.MostValuable.First().Details).IsEqualTo("Details provided");
    }

    [Test]
    public async Task Justification_with_null_details_in_context_source_becomes_empty_string()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Reasoning",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [new PredictionJustificationContextSource("team-data", null!)],
                LeastValuable: []),
            Uncertainties: []);
        var prediction = new Prediction(2, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.ContextSources!.MostValuable.First().DocumentName).IsEqualTo("team-data");
        await Assert.That(retrieved.Justification.ContextSources.MostValuable.First().Details).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Justification_trims_whitespace_from_all_string_fields()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "  Padded reasoning  ",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [new PredictionJustificationContextSource("  team-data  ", "  details  ")],
                LeastValuable: []),
            Uncertainties: ["  uncertainty  "]);
        var prediction = new Prediction(1, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.KeyReasoning).IsEqualTo("Padded reasoning");
        await Assert.That(retrieved.Justification.ContextSources!.MostValuable.First().DocumentName).IsEqualTo("team-data");
        await Assert.That(retrieved.Justification.ContextSources.MostValuable.First().Details).IsEqualTo("details");
        await Assert.That(retrieved.Justification.Uncertainties.First()).IsEqualTo("uncertainty");
    }

    [Test]
    public async Task Justification_with_empty_context_sources_lists_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Valid reasoning",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [],
                LeastValuable: []),
            Uncertainties: []);
        var prediction = new Prediction(1, 0, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.KeyReasoning).IsEqualTo("Valid reasoning");
        await Assert.That(retrieved.Justification.ContextSources!.MostValuable).IsEmpty();
        await Assert.That(retrieved.Justification.ContextSources.LeastValuable).IsEmpty();
    }

    [Test]
    public async Task Justification_with_both_most_and_least_valuable_sources_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: "Balanced analysis",
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [
                    new PredictionJustificationContextSource("standings", "Clear league position difference"),
                    new PredictionJustificationContextSource("head-to-head", "Historical advantage")
                ],
                LeastValuable: [
                    new PredictionJustificationContextSource("weather", "No significant impact"),
                    new PredictionJustificationContextSource("transfers", "Too recent to matter")
                ]),
            Uncertainties: []);
        var prediction = new Prediction(3, 1, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        
        var sources = retrieved.Justification!.ContextSources!;
        await Assert.That(sources.MostValuable).HasCount().EqualTo(2);
        await Assert.That(sources.LeastValuable).HasCount().EqualTo(2);
        await Assert.That(sources.MostValuable.Select(s => s.DocumentName)).IsEquivalentTo(["standings", "head-to-head"]);
        await Assert.That(sources.LeastValuable.Select(s => s.DocumentName)).IsEquivalentTo(["weather", "transfers"]);
    }

    [Test]
    public async Task Justification_with_only_least_valuable_sources_works()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch();
        var justification = new PredictionJustification(
            KeyReasoning: string.Empty,
            ContextSources: new PredictionJustificationContextSources(
                MostValuable: [],
                LeastValuable: [
                    new PredictionJustificationContextSource("weather", "Not relevant")
                ]),
            Uncertainties: []);
        var prediction = new Prediction(1, 1, justification);

        // Act
        await repository.SavePredictionAsync(
            match, prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");

        // Assert - HasJustificationContent returns true because LeastValuable has content
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Justification).IsNotNull();
        await Assert.That(retrieved.Justification!.ContextSources!.LeastValuable).HasCount().EqualTo(1);
        await Assert.That(retrieved.Justification.ContextSources.LeastValuable.First().DocumentName).IsEqualTo("weather");
    }
}
