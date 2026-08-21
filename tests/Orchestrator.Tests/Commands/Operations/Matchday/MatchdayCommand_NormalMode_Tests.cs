using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using Orchestrator.Infrastructure;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> normal mode workflow.
/// </summary>
public class MatchdayCommand_NormalMode_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Running_command_with_no_matches_shows_no_matches_message()
    {
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
    }

    [Test]
    public async Task Running_command_with_no_matches_does_not_call_prediction_service()
    {
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_with_matches_shows_match_count()
    {
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory(match: CreateMatch(homeTeam: "RB Leipzig", awayTeam: "VfB Stuttgart"))
        };
        var docs = CreateBayernVsDortmundContextDocuments();
        foreach (var pair in CreateMatchContextDocuments(homeAbbreviation: "rbl", awayAbbreviation: "vfb"))
        {
            docs[pair.Key] = pair.Value;
        }
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matches, contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 matches");
    }

    [Test]
    public async Task Running_command_with_matches_displays_match_processing()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing:");
        await Assert.That(output).Contains("FC Bayern München");
        await Assert.That(output).Contains("Borussia Dortmund");
    }

    [Test]
    public async Task Running_command_uses_cached_prediction_when_available()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--competition", CompetitionIds.Bundesliga2025_26);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("3:0");
        await Assert.That(output).Contains("from database");
    }

    [Test]
    public async Task Running_command_with_cached_prediction_does_not_call_prediction_service()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--competition", CompetitionIds.Bundesliga2025_26);

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Legacy_normal_mode_reuses_a_cached_prediction_without_running_staleness_checks()
    {
        var match = CreateBayernVsDortmundMatch();
        var cachedPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: cachedPrediction,
            getPredictionMetadataResult: new PredictionMetadata(
                cachedPrediction,
                DateTimeOffset.UtcNow.AddDays(-1),
                ["recent-history-fcb.csv"]));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(
                    new Dictionary<string, ContextDocument>
                    {
                        ["recent-history-fcb.csv"] = CreateContextDocument(
                            documentName: "recent-history-fcb.csv",
                            createdAt: DateTimeOffset.UtcNow)
                    })));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2025_26);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        predictionRepository.Verify(repository => repository.GetPredictionMetadataAsync(
            It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_cached_prediction_without_metadata_is_blocked_without_generation_or_submission()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: CreatePrediction(homeGoals: 3, awayGoals: 0));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Cancelled_bundesliga_cached_prediction_with_missing_provenance_is_blocked_without_generation_or_submission()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var predictionRepository = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: CreatePrediction(homeGoals: 3, awayGoals: 0));
        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments())));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Cancelled_Bundesliga_cached_prediction_with_same_version_ordinary_content_mutation_is_blocked()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var recordedDocuments = CreateBayernVsDortmundContextDocuments();
        var currentDocuments = new Dictionary<string, ContextDocument>(recordedDocuments, StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new ContextDocument(
                "bundesliga-standings.csv",
                "same version, changed bytes",
                recordedDocuments["bundesliga-standings.csv"].Version,
                recordedDocuments["bundesliga-standings.csv"].CreatedAt)
        };
        var predictionRepository = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: prediction,
            getCancelledMatchPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(
                prediction, cancelledMatch, recordedDocuments));
        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(currentDocuments)));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_cached_prediction_with_corrupt_provenance_is_blocked_without_submission()
    {
        var match = CreateBayernVsDortmundMatch();
        var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: prediction,
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(prediction, match));
        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repository => repository.GetLatestContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("Corrupt immutable context payload."));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: contextRepository));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_cached_prediction_with_same_version_ordinary_content_mutation_is_blocked_without_submission()
    {
        var match = CreateBayernVsDortmundMatch();
        var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var recordedDocuments = CreateBayernVsDortmundContextDocuments();
        var currentDocuments = new Dictionary<string, ContextDocument>(recordedDocuments, StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new ContextDocument(
                "bundesliga-standings.csv",
                "same version, changed bytes",
                recordedDocuments["bundesliga-standings.csv"].Version,
                recordedDocuments["bundesliga-standings.csv"].CreatedAt)
        };
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: prediction,
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(prediction, match, recordedDocuments));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(currentDocuments)));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_cached_prediction_with_independently_materialized_identical_justification_is_reused()
    {
        var match = CreateBayernVsDortmundMatch();
        var storedPrediction = CreateStructuredPrediction("uncertainty");
        var metadataPrediction = CreateStructuredPrediction("uncertainty");
        var documents = CreateBayernVsDortmundContextDocuments();
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: storedPrediction,
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(metadataPrediction, match, documents));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(documents)));

        var (exitCode, _) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(0);
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_cached_prediction_with_different_justification_does_not_cross_associate_metadata()
    {
        var match = CreateBayernVsDortmundMatch();
        var documents = CreateBayernVsDortmundContextDocuments();
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreateStructuredPrediction("stored uncertainty"),
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(
                CreateStructuredPrediction("metadata uncertainty"), match, documents));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: CreateMockContextRepositoryWithDocuments(documents)));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("lacks valid immutable provenance");
        ctx.KicktippClient.Verify(client => client.PlaceBetsAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()), Times.Never);
    }

    private static Prediction CreateStructuredPrediction(string uncertainty) =>
        new(
            3,
            0,
            new PredictionJustification(
                "reasoning",
                new PredictionJustificationContextSources(
                    [new PredictionJustificationContextSource("bundesliga-standings.csv", "useful")],
                    [new PredictionJustificationContextSource("recent-history-fcb.csv", "less useful")]),
                [uncertainty]));

    [Test]
    public async Task Running_command_in_agent_mode_with_cached_prediction_hides_score()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent", "--competition", CompetitionIds.Bundesliga2025_26);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).DoesNotContain("3:0");
    }

    [Test]
    public async Task Running_command_generates_new_prediction_when_no_cached_prediction_exists()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        await Assert.That(output).Contains("Generated prediction");
    }

    [Test]
    public async Task Running_command_calls_prediction_service_when_no_cached_prediction_exists()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_saves_new_prediction_to_database()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionRepository.As<IResolvedMatchContextPredictionRepository>().Verify(
            r => r.SavePredictionWithResolvedContextAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.Is<ResolvedMatchContextManifest>(manifest => manifest.Documents.Length == 11), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Bundesliga_validation_run_saves_exact_match_runtime_identity()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, _) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-5.6-luna",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.Bundesliga2026_27,
            "--reasoning-effort",
            "none",
            "--max-output-tokens",
            "10000");

        await Assert.That(exitCode).IsEqualTo(0);
        ctx.PredictionRepository.As<IResolvedMatchContextPredictionRepository>().Verify(
            repository => repository.SavePredictionWithResolvedContextAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.Is<PredictionModelConfig>(config =>
                    config.Model == "gpt-5.6-luna" &&
                    config.ReasoningEffort == "none" &&
                    config.MaxOutputTokenCount == 10_000 &&
                    config.PromptName == CompetitionResolver.BundesligaMatchPromptName &&
                    config.PromptVersion == CompetitionResolver.BundesligaMatchPromptVersion),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ResolvedMatchContextManifest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_displays_generated_prediction_score()
    {
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction:");
        await Assert.That(output).Contains("2:1");
    }

    [Test]
    public async Task Running_command_in_agent_mode_hides_generated_prediction_score()
    {
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction");
        await Assert.That(output).DoesNotContain("2:1");
    }

    [Test]
    public async Task Running_command_with_override_database_generates_new_prediction_even_when_cached_exists()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var newPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction, predictionResult: newPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-database");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_places_bets_to_kicktipp()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Placing");
        await Assert.That(output).Contains("predictions to Kicktipp");
        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_shows_success_when_bets_placed_successfully()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, placeBetsResult: true);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Successfully placed all");
    }

    [Test]
    public async Task Running_command_shows_failure_when_bets_fail_to_place()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, placeBetsResult: false);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to place");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_passes_override_flag_to_kicktipp_client()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-kicktipp");

        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), true),
            Times.Once);
    }

    [Test]
    public async Task Running_command_displays_token_usage_summary()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }

    [Test]
    public async Task Running_command_shows_failure_when_prediction_service_returns_null()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to generate prediction");
        await Assert.That(output).Contains("prediction_generation_failed");
    }

    [Test]
    public async Task Running_command_with_all_predictions_failed_shows_no_predictions_message()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("No valid predictions available");
    }
}
