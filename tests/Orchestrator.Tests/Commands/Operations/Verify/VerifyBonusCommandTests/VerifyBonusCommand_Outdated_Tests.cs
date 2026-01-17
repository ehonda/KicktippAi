using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> outdated prediction detection logic.
/// </summary>
public class VerifyBonusCommand_Outdated_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Prediction_with_newer_kpi_document_is_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var kpiUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero); // Later

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(
                documentName: "test-kpi.md",
                createdAt: kpiUpdatedAt)
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Verbose_mode_shows_kpi_document_update_timestamps_when_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var kpiUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(
                documentName: "test-kpi.md",
                createdAt: kpiUpdatedAt)
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert
        await Assert.That(output).Contains("test-kpi.md").And.Contains("updated after prediction");
    }

    [Test]
    public async Task Prediction_with_older_kpi_document_is_not_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 14, 0, 0, TimeSpan.Zero);
        var kpiUpdatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero); // Earlier

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(
                documentName: "test-kpi.md",
                createdAt: kpiUpdatedAt)
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }

    [Test]
    public async Task Missing_kpi_document_in_repo_shows_warning_in_verbose()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "missing-kpi.md" });

        // Empty KPI documents - document won't be found
        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: new Dictionary<string, KpiDocument>());

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert - should pass (missing = not outdated) but show warning
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning: KPI document 'missing-kpi.md' not found");
    }

    [Test]
    public async Task No_metadata_assumes_not_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction);
        // Note: bonusPredictionMetadata defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }

    [Test]
    public async Task Multiple_kpi_documents_with_one_outdated_marks_prediction_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 11, 0, 0, TimeSpan.Zero);

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "kpi-1.md", "kpi-2.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            // First document is older - OK
            ["kpi-1.md"] = CreateKpiDocument(
                documentName: "kpi-1.md",
                createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            // Second document is newer - outdated
            ["kpi-2.md"] = CreateKpiDocument(
                documentName: "kpi-2.md",
                createdAt: new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero))
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert - should fail because one document is newer
        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task Verbose_mode_shows_kpi_document_version_when_not_outdated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 14, 0, 0, TimeSpan.Zero);

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(
                documentName: "test-kpi.md",
                version: 5,
                createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero))
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert
        await Assert.That(output).Contains("test-kpi.md").And.Contains("version 5").And.Contains("latest");
    }

    [Test]
    public async Task Agent_mode_shows_outdated_status_abbreviated()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Outdated question", formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var kpiUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(documentName: "test-kpi.md", createdAt: kpiUpdatedAt)
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Outdated question");
        await Assert.That(output).Contains("(outdated)");
    }

    [Test]
    public async Task Non_agent_mode_shows_detailed_outdated_info()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Outdated question", formFieldName: "bonus_q1");
        var predictionCreatedAt = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var kpiUpdatedAt = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: predictionCreatedAt,
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var kpiDocs = new Dictionary<string, KpiDocument>
        {
            ["test-kpi.md"] = CreateKpiDocument(documentName: "test-kpi.md", createdAt: kpiUpdatedAt)
        };

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction,
            bonusPredictionMetadata: metadata,
            kpiDocumentsByName: kpiDocs);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(output).Contains("✗ Outdated question");
        await Assert.That(output).Contains("outdated");
        await Assert.That(output).Contains("context updated after prediction");
    }
}
