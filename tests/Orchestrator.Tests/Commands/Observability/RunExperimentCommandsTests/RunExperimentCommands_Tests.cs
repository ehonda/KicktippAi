using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Observability;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.RunExperimentCommandsTests;

[NotInParallel("Telemetry")]
public class RunExperimentCommands_Tests
{
    [Test]
    public async Task Marked_historical_context_drift_fails_before_run_delete_prompt_fetch_or_model_construction()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var manifest = CreateHistoricalPreparedManifest();
            var manifestPath = Path.Combine(temporaryDirectory.FullName, "historical.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, PreparedExperimentCommandSupport.JsonOptions));
            var recorded = manifest.Items.Single().HistoricalContextManifest!;
            var reader = new Mock<IHistoricalExperimentContextReader>(MockBehavior.Strict);
            foreach (var document in recorded.Documents)
            {
                reader.Setup(repository => repository.GetContextDocumentAsync(
                        document.Name,
                        document.Version,
                        "pes-squad",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(
                        document.Name,
                        document == recorded.Documents[^1] ? "drifted" : $"content:{document.Name}",
                        document.Version,
                        document.CreatedAt));
            }

            var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            firebaseFactory.Setup(factory => factory.CreateBundesliga2025_26HistoricalExperimentContextReader())
                .Returns(reader.Object);
            var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            var executor = new PreparedExperimentRunExecutor(
                firebaseFactory.Object,
                openAiFactory.Object,
                langfuseClient.Object);

            await Assert.That(() => executor.ExecuteAsync(
                    "repeated-match-slice",
                    new PreparedExperimentRunRequest(
                        manifestPath,
                        "historical-run",
                        null,
                        null,
                        true,
                        CreateHistoricalRunOptions()),
                    CancellationToken.None))
                .Throws<InvalidDataException>()
                .WithMessageContaining("drifted");

            reader.Verify(repository => repository.GetContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<int>(), "pes-squad", It.IsAny<CancellationToken>()), Times.Exactly(7));
            langfuseClient.VerifyNoOtherCalls();
            openAiFactory.VerifyNoOtherCalls();
            firebaseFactory.Verify(factory => factory.CreatePredictionRepository(It.IsAny<string>()), Times.Never);
            firebaseFactory.Verify(factory => factory.CreateContextRepository(It.IsAny<string>()), Times.Never);
            firebaseFactory.Verify(factory => factory.CreateMatchOutcomeRepository(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Marked_historical_route_mismatch_fails_before_firestore_langfuse_or_model_access()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var manifestPath = Path.Combine(temporaryDirectory.FullName, "historical.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(CreateHistoricalPreparedManifest(), PreparedExperimentCommandSupport.JsonOptions));
            var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            var executor = new PreparedExperimentRunExecutor(
                firebaseFactory.Object,
                openAiFactory.Object,
                langfuseClient.Object);
            var wrongRoute = CreateHistoricalRunOptions() with
            {
                PromptSource = "local",
                LangfusePromptName = null,
                LangfusePromptLabel = null,
                LangfusePromptVersion = null
            };

            await Assert.That(() => executor.ExecuteAsync(
                    "repeated-match-slice",
                    new PreparedExperimentRunRequest(
                        manifestPath,
                        "historical-run",
                        null,
                        null,
                        true,
                        wrongRoute),
                    CancellationToken.None))
                .Throws<InvalidOperationException>()
                .WithMessageContaining("prompt route");

            firebaseFactory.VerifyNoOtherCalls();
            langfuseClient.VerifyNoOtherCalls();
            openAiFactory.VerifyNoOtherCalls();
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("wrong-name", 2, true)]
    [Arguments("kicktippai/bundesliga-2026-27/predict-one-match", 3, true)]
    [Arguments("kicktippai/bundesliga-2026-27/predict-one-match", 2, false)]
    public async Task Marked_historical_resolved_prompt_mismatch_fails_before_run_delete_or_model_construction(
        string resolvedName,
        int resolvedVersion,
        bool hasProductionLabel)
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var manifest = CreateHistoricalPreparedManifest();
            var manifestPath = Path.Combine(temporaryDirectory.FullName, "historical.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, PreparedExperimentCommandSupport.JsonOptions));
            var recorded = manifest.Items.Single().HistoricalContextManifest!;
            var reader = new Mock<IHistoricalExperimentContextReader>(MockBehavior.Strict);
            foreach (var document in recorded.Documents)
            {
                reader.Setup(repository => repository.GetContextDocumentAsync(
                        document.Name,
                        document.Version,
                        "pes-squad",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(
                        document.Name,
                        $"content:{document.Name}",
                        document.Version,
                        document.CreatedAt));
            }

            var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            firebaseFactory.Setup(factory => factory.CreateBundesliga2025_26HistoricalExperimentContextReader())
                .Returns(reader.Object);
            var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient.Setup(client => client.GetPromptAsync(
                    PreparedHistoricalExperimentCompatibility.PromptName,
                    PreparedHistoricalExperimentCompatibility.PromptLabel,
                    PreparedHistoricalExperimentCompatibility.PromptVersion,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePrompt(
                    resolvedName,
                    resolvedVersion,
                    "text",
                    JsonSerializer.SerializeToElement("{{context_documents}}"),
                    hasProductionLabel ? [PreparedHistoricalExperimentCompatibility.PromptLabel] : ["staging"],
                    [],
                    JsonSerializer.SerializeToElement(new { })));
            var executor = new PreparedExperimentRunExecutor(
                firebaseFactory.Object,
                openAiFactory.Object,
                langfuseClient.Object);

            await Assert.That(() => executor.ExecuteAsync(
                    "repeated-match-slice",
                    new PreparedExperimentRunRequest(
                        manifestPath,
                        "historical-run",
                        null,
                        null,
                        true,
                        CreateHistoricalRunOptions()),
                    CancellationToken.None))
                .Throws<InvalidDataException>()
                .WithMessageContaining("production-label binding");

            langfuseClient.Verify(client => client.GetPromptAsync(
                PreparedHistoricalExperimentCompatibility.PromptName,
                PreparedHistoricalExperimentCompatibility.PromptLabel,
                PreparedHistoricalExperimentCompatibility.PromptVersion,
                It.IsAny<CancellationToken>()), Times.Once);
            langfuseClient.VerifyNoOtherCalls();
            openAiFactory.VerifyNoOtherCalls();
            reader.Verify(repository => repository.GetContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<int>(), "pes-squad", It.IsAny<CancellationToken>()), Times.Exactly(7));
            firebaseFactory.Verify(factory => factory.CreateBundesliga2025_26HistoricalExperimentContextReader(), Times.Once);
            firebaseFactory.VerifyNoOtherCalls();
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Marked_historical_run_uses_cached_seven_documents_and_embedded_score_without_live_repositories()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var manifest = CreateHistoricalPreparedManifest();
            var manifestPath = Path.Combine(temporaryDirectory.FullName, "historical.json");
            var runMetadataPath = Path.Combine(temporaryDirectory.FullName, "tampered-run-metadata.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, PreparedExperimentCommandSupport.JsonOptions));
            await File.WriteAllTextAsync(
                runMetadataPath,
                JsonSerializer.Serialize(
                    PreparedExperimentSupport.BuildRunMetadata(manifest, CreateHistoricalRunOptions()) with
                    {
                        SelectedItemIdsCount = 999,
                        SelectedItemIdsHash = new string('a', 64)
                    },
                    PreparedExperimentCommandSupport.JsonOptions));
            var recorded = manifest.Items.Single().HistoricalContextManifest!;
            var reader = new Mock<IHistoricalExperimentContextReader>(MockBehavior.Strict);
            foreach (var document in recorded.Documents)
            {
                reader.Setup(repository => repository.GetContextDocumentAsync(
                        document.Name,
                        document.Version,
                        "pes-squad",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(
                        document.Name,
                        $"content:{document.Name}",
                        document.Version,
                        document.CreatedAt));
            }

            var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            firebaseFactory.Setup(factory => factory.CreateBundesliga2025_26HistoricalExperimentContextReader())
                .Returns(reader.Object);
            IReadOnlyList<DocumentContext>? usedContext = null;
            var predictionService = CreateMockPredictionService(predictMatchResult: new Prediction(2, 1));
            predictionService.Setup(service => service.PredictMatchAsync(
                    It.IsAny<Match>(),
                    It.IsAny<IEnumerable<DocumentContext>>(),
                    It.IsAny<bool>(),
                    It.IsAny<PredictionTelemetryMetadata?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((Match _, IEnumerable<DocumentContext> documents, bool _, PredictionTelemetryMetadata? _, CancellationToken _) =>
                    usedContext = documents.ToList())
                .ReturnsAsync(new Prediction(2, 1));
            var openAiFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            LangfuseCreateDatasetRunItemRequest? createdRunItem = null;
            langfuseClient.Setup(client => client.GetPromptAsync(
                    PreparedHistoricalExperimentCompatibility.PromptName,
                    PreparedHistoricalExperimentCompatibility.PromptLabel,
                    PreparedHistoricalExperimentCompatibility.PromptVersion,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePrompt(
                    PreparedHistoricalExperimentCompatibility.PromptName,
                    PreparedHistoricalExperimentCompatibility.PromptVersion,
                    "text",
                    JsonSerializer.SerializeToElement("{{context_documents}}"),
                    [PreparedHistoricalExperimentCompatibility.PromptLabel],
                    [],
                    JsonSerializer.SerializeToElement(new { })));
            langfuseClient.Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(), It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) =>
                    createdRunItem = request)
                .ReturnsAsync(new LangfuseDatasetRunItem(
                    "run-item-1", "run-1", "historical-run", "slice-1", "trace", null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            langfuseClient.Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score"));
            langfuseClient.Setup(client => client.GetDatasetRunAsync(
                    "historical-dataset", "historical-run", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    "run-1", "historical-run", "dataset-1", "historical-dataset", null, default, []));
            langfuseClient.Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1", "historical-run", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [], new LangfusePaginationMeta(1, 100, 1, 1)));
            var executor = new PreparedExperimentRunExecutor(
                firebaseFactory.Object,
                openAiFactory.Object,
                langfuseClient.Object);

            var summary = await executor.ExecuteAsync(
                "repeated-match-slice",
                new PreparedExperimentRunRequest(
                    manifestPath,
                    "historical-run",
                    null,
                    runMetadataPath,
                    false,
                    CreateHistoricalRunOptions()),
                CancellationToken.None);

            await Assert.That(summary.ExecutionCount).IsEqualTo(1);
            await Assert.That(summary.AggregateScores.TotalKicktippPoints).IsEqualTo(4);
            await Assert.That(usedContext).IsNotNull();
            await Assert.That(usedContext!.Count).IsEqualTo(7);
            await Assert.That(createdRunItem).IsNotNull();
            var createdRunMetadata = JsonSerializer.SerializeToElement(
                createdRunItem!.Metadata,
                PreparedExperimentCommandSupport.JsonOptions);
            await Assert.That(createdRunMetadata.GetProperty("selectedItemIdsCount").GetInt32())
                .IsEqualTo(manifest.SelectedItemIds.Count);
            await Assert.That(createdRunMetadata.GetProperty("selectedItemIdsHash").GetString())
                .IsEqualTo(manifest.SelectedItemIdsHash);
            firebaseFactory.Verify(factory => factory.CreatePredictionRepository(It.IsAny<string>()), Times.Never);
            firebaseFactory.Verify(factory => factory.CreateContextRepository(It.IsAny<string>()), Times.Never);
            firebaseFactory.Verify(factory => factory.CreateMatchOutcomeRepository(It.IsAny<string>()), Times.Never);
            firebaseFactory.Verify(factory => factory.CreateDocumentPublicationRepository(It.IsAny<string>()), Times.Never);
            reader.Verify(repository => repository.GetContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<int>(), "pes-squad", It.IsAny<CancellationToken>()), Times.Exactly(7));
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Historical_artifact_hash_binds_completed_score_cutoff_route_and_context()
    {
        var manifest = CreateHistoricalPreparedManifest();

        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    Items = [manifest.Items.Single() with { ExpectedHomeGoals = 9 }]
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("artifact hash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    StartsAfter = "2026-02-17T00:00:00 Europe/Berlin (+01)",
                    HistoricalCompatibility = manifest.HistoricalCompatibility! with
                    {
                        OfficialKnowledgeCutoff = "2026-02-15",
                        SamplingCutoff = "2026-02-17T00:00:00 Europe/Berlin (+01)"
                    }
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("artifact hash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { SampleSeed = 99 }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("artifact hash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { SliceDatasetName = "another-historical-dataset" }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("artifact hash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    HistoricalCompatibility = manifest.HistoricalCompatibility! with
                    {
                        EligibleFixtureIdsHash = new string('a', 64)
                    }
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("artifact hash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    HistoricalCompatibility = manifest.HistoricalCompatibility! with
                    {
                        EligibilityPolicy = "wrong-policy"
                    }
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("complete context-eligible pool");
    }

    [Test]
    public async Task Historical_topology_accepts_exact_one_by_one_and_five_by_four_manifests()
    {
        var oneByOne = PreparedExperimentCommandSupport.ValidateManifest(CreateHistoricalPreparedManifest());
        var fiveByFour = PreparedExperimentCommandSupport.ValidateManifest(CreateHistoricalPreparedManifest(5, 4));
        var runMetadata = PreparedExperimentSupport.BuildRunMetadata(oneByOne, CreateHistoricalRunOptions());
        var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(runMetadata);

        await Assert.That(oneByOne.Items.Count).IsEqualTo(1);
        await Assert.That(fiveByFour.Items.Count).IsEqualTo(20);
        await Assert.That(fiveByFour.SelectedItemIds.Count).IsEqualTo(5);
        await Assert.That(runMetadata.HistoricalEligibilityPolicy)
            .IsEqualTo(PreparedHistoricalExperimentCompatibility.RequiredEligibilityPolicy);
        await Assert.That(runMetadata.HistoricalEligibleFixtureCount).IsEqualTo(5);
        await Assert.That(runMetadata.HistoricalEligibleFixtureIdsHash)
            .IsEqualTo(oneByOne.HistoricalCompatibility!.EligibleFixtureIdsHash);
        await Assert.That(propagatedMetadata["historicalEligibilityPolicy"])
            .IsEqualTo(PreparedHistoricalExperimentCompatibility.RequiredEligibilityPolicy);
        await Assert.That(propagatedMetadata["historicalEligibleFixtureCount"]).IsEqualTo("5");
        await Assert.That(propagatedMetadata["historicalEligibleFixtureIdsHash"])
            .IsEqualTo(oneByOne.HistoricalCompatibility!.EligibleFixtureIdsHash);
    }

    [Test]
    public async Task Historical_run_metadata_forces_validated_manifest_selected_identity_over_caller_tampering()
    {
        var manifest = PreparedExperimentCommandSupport.ValidateManifest(CreateHistoricalPreparedManifest(5, 4));
        var callerMetadata = new PreparedExperimentRunMetadata
        {
            SelectedItemIdsCount = 999,
            SelectedItemIdsHash = new string('a', 64)
        };

        var normalized = PreparedExperimentCommandSupport.NormalizeRunMetadata(
            callerMetadata,
            manifest,
            CreateHistoricalRunOptions());
        var propagated = PreparedExperimentSupport.DerivePropagatedMetadata(normalized);
        var langfuseMetadata = PreparedExperimentSupport.BuildLangfuseExperimentMetadata(
            normalized,
            "historical-cost-estimate",
            "historical-cost-estimate__run");

        await Assert.That(normalized.SelectedItemIdsCount).IsEqualTo(manifest.SelectedItemIds.Count);
        await Assert.That(normalized.SelectedItemIdsHash).IsEqualTo(manifest.SelectedItemIdsHash);
        await Assert.That(propagated["selectedItemIdsCount"])
            .IsEqualTo(manifest.SelectedItemIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(propagated["selectedItemIdsHash"]).IsEqualTo(manifest.SelectedItemIdsHash);
        await Assert.That(langfuseMetadata.GetProperty("selectedItemIdsCount").GetInt32())
            .IsEqualTo(manifest.SelectedItemIds.Count);
        await Assert.That(langfuseMetadata.GetProperty("selectedItemIdsHash").GetString())
            .IsEqualTo(manifest.SelectedItemIdsHash);
    }

    [Test]
    public async Task Historical_topology_rejects_partial_identities_and_generated_id_or_dimension_drift()
    {
        var manifest = CreateHistoricalPreparedManifest(5, 4);
        var first = manifest.Items[0];

        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { Items = [first with { TippSpielId = null }, .. manifest.Items.Skip(1)] }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    Items = [first, manifest.Items[1] with { TippSpielId = null }, .. manifest.Items.Skip(2)]
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("partial or inconsistent");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { Items = [first with { FixtureIndex = null }, .. manifest.Items.Skip(1)] }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { Items = [first with { RepetitionIndex = null }, .. manifest.Items.Skip(1)] }))
            .Throws<InvalidOperationException>();
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { SampleSize = 19 }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("matchCount multiplied by repetitions");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { Items = [first with { SourceDatasetItemId = "wrong" }, .. manifest.Items.Skip(1)] }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("sourceDatasetItemId");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { Items = [first with { SliceDatasetItemId = "wrong" }, .. manifest.Items.Skip(1)] }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("generated repeated-match-slice identity");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { SelectedItemIdsHash = new string('a', 64) }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("selectedItemIdsHash");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with { SelectedItemIds = ["wrong", .. manifest.SelectedItemIds.Skip(1)] }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("selectedItemIds");
        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    HistoricalCompatibility = manifest.HistoricalCompatibility! with
                    {
                        EligibleFixtureCount = 4
                    }
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("exceeds the bound complete context-eligible pool");
    }

    [Test]
    public async Task Historical_sampling_cutoff_requires_exact_two_day_berlin_local_midnight_margin()
    {
        var manifest = CreateHistoricalPreparedManifest();
        var wrongCompatibility = manifest.HistoricalCompatibility! with
        {
            SamplingCutoff = "2026-02-18T01:00:00 Europe/Berlin (+01)"
        };
        var wrongCutoff = manifest with
        {
            StartsAfter = wrongCompatibility.SamplingCutoff,
            HistoricalCompatibility = wrongCompatibility
        };
        wrongCutoff = wrongCutoff with
        {
            HistoricalArtifactSha256 = PreparedExperimentCommandSupport.ComputeHistoricalArtifactSha256(wrongCutoff)
        };

        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(wrongCutoff))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("local midnight two days");
        await Assert.That(PreparedExperimentCommandSupport.BuildRequiredHistoricalSamplingCutoff(
                new DateOnly(2026, 2, 16)))
            .IsEqualTo("2026-02-18T00:00:00 Europe/Berlin (+01)");
    }

    [Test]
    public async Task Historical_artifact_hash_without_the_explicit_compatibility_contract_fails_closed()
    {
        var manifest = CreateHistoricalPreparedManifest();

        await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(
                manifest with
                {
                    HistoricalCompatibility = null,
                    Items = manifest.Items.Select(item => item with
                    {
                        HistoricalContextManifest = null,
                        ExpectedHomeGoals = null,
                        ExpectedAwayGoals = null
                    }).ToArray()
                }))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("historicalCompatibility");
    }

    [Test]
    public async Task Prepared_bundesliga_2026_27_item_without_immutable_manifest_is_rejected_before_execution()
    {
        var manifest = new PreparedExperimentManifest
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = "test-community",
            Items =
            [
                new PreparedExperimentManifestItem
                {
                    SourceDatasetItemId = "source-item",
                    SliceDatasetItemId = "slice-item",
                    HomeTeam = "FC Bayern München",
                    AwayTeam = "Borussia Dortmund",
                    Matchday = 1,
                    StartsAt = "2026-08-21T20:30:00 Europe/Berlin (+02)"
                }
            ]
        };

        var exception = await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(manifest))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("resolvedContextManifest");
    }

    [Test]
    public async Task Prepared_bundesliga_2025_26_item_remains_valid_without_immutable_manifest()
    {
        var manifest = new PreparedExperimentManifest
        {
            Competition = "bundesliga-2025-26",
            CommunityContext = "test-community",
            Items =
            [
                new PreparedExperimentManifestItem
                {
                    SourceDatasetItemId = "source-item",
                    SliceDatasetItemId = "slice-item",
                    HomeTeam = "FC Bayern München",
                    AwayTeam = "Borussia Dortmund",
                    Matchday = 1,
                    StartsAt = "2025-08-22T20:30:00 Europe/Berlin (+02)"
                }
            ]
        };

        PreparedExperimentCommandSupport.ValidateManifest(manifest);
        await Assert.That(manifest.Items.Single().ResolvedContextManifest).IsNull();
    }

    [Test]
    public async Task Prepared_bundesliga_case_variant_is_canonicalized_but_embedded_scope_must_match()
    {
        var match = new Match("FC Bayern München", "Borussia Dortmund", default, 1);
        var resolved = CreateCanonicalBundesligaResolvedContextManifest(match);
        var manifest = new PreparedExperimentManifest
        {
            Competition = "BUNDESLIGA-2026-27", CommunityContext = "test-community",
            Items = [new PreparedExperimentManifestItem
            {
                SourceDatasetItemId = "source", SliceDatasetItemId = "slice", HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam, Matchday = 1, StartsAt = "2026-08-21T20:30:00Z",
                ResolvedContextManifest = resolved, PredictionCreatedAt = DateTimeOffset.UtcNow
            }]
        };

        var normalized = PreparedExperimentCommandSupport.ValidateManifest(manifest);
        await Assert.That(normalized.Competition).IsEqualTo(CompetitionIds.Bundesliga2026_27);
    }

    [Test]
    public async Task Prepared_bundesliga_embedded_manifest_competition_or_community_mismatch_is_rejected()
    {
        var match = new Match("FC Bayern München", "Borussia Dortmund", default, 1);
        var matching = CreateCanonicalBundesligaResolvedContextManifest(match);
        var wrongCommunity = CreateCanonicalBundesligaResolvedContextManifest(match, communityContext: "other-community");

        var communityException = await Assert.That(() => PreparedExperimentCommandSupport.ValidateManifest(new PreparedExperimentManifest
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = "test-community",
            Items = [CreateBundesligaPreparedItem(match, wrongCommunity)]
        })).Throws<InvalidOperationException>();

        await Assert.That(communityException!.Message).Contains("community scope");

        var matchingManifest = new PreparedExperimentManifest
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = "test-community",
            Items = [CreateBundesligaPreparedItem(match, matching)]
        };
        var json = JsonNode.Parse(JsonSerializer.Serialize(matchingManifest, PreparedExperimentCommandSupport.JsonOptions))!;
        json["items"]![0]!["resolvedContextManifest"]!["competition"] = CompetitionIds.Bundesliga2025_26;

        var exception = await Assert.That(() => JsonSerializer.Deserialize<PreparedExperimentManifest>(
                json.ToJsonString(), PreparedExperimentCommandSupport.JsonOptions))
            .Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("invalid");
    }

    [Test]
    public async Task Prepared_run_metadata_scope_override_is_rejected()
    {
        var manifest = new PreparedExperimentManifest { Competition = CompetitionIds.Bundesliga2025_26, CommunityContext = "community-a", Items = [new PreparedExperimentManifestItem { SourceDatasetItemId = "source", SliceDatasetItemId = "slice", HomeTeam = "A", AwayTeam = "B", Matchday = 1, StartsAt = "x" }] };
        var options = new PreparedExperimentRunOptions("gpt-5", "prompt", false, null, null, null, null, "local", null, null, null, "simple");
        await Assert.That(() => PreparedExperimentCommandSupport.NormalizeRunMetadata(new PreparedExperimentRunMetadata { Competition = CompetitionIds.FifaWorldCup2026 }, manifest, options)).Throws<InvalidOperationException>();
        await Assert.That(() => PreparedExperimentCommandSupport.NormalizeRunMetadata(new PreparedExperimentRunMetadata { CommunityContext = "community-b" }, manifest, options)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Non_historical_run_metadata_preserves_existing_selected_identity_override_behavior()
    {
        var manifest = new PreparedExperimentManifest
        {
            Competition = CompetitionIds.FifaWorldCup2026,
            CommunityContext = "community-a",
            SelectedItemIds = ["source"],
            SelectedItemIdsHash = ExperimentArtifactSupport.ComputeSelectedItemIdsHash(["source"]),
            Items = [new PreparedExperimentManifestItem
            {
                SourceDatasetItemId = "source",
                SliceDatasetItemId = "slice",
                HomeTeam = "A",
                AwayTeam = "B",
                Matchday = 1,
                StartsAt = "x"
            }]
        };
        var callerHash = new string('a', 64);
        var normalized = PreparedExperimentCommandSupport.NormalizeRunMetadata(
            new PreparedExperimentRunMetadata
            {
                SelectedItemIdsCount = 999,
                SelectedItemIdsHash = callerHash
            },
            manifest,
            new PreparedExperimentRunOptions(
                "gpt-5", "prompt", false, null, null, null, null, "local", null, null, null, "simple"));
        var propagated = PreparedExperimentSupport.DerivePropagatedMetadata(normalized);

        await Assert.That(normalized.SelectedItemIdsCount).IsEqualTo(999);
        await Assert.That(normalized.SelectedItemIdsHash).IsEqualTo(callerHash);
        await Assert.That(propagated.ContainsKey("selectedItemIdsCount")).IsFalse();
        await Assert.That(propagated["selectedItemIdsHash"]).IsEqualTo(callerHash);
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task File_backed_bundesliga_manifest_round_trips_and_reconstructs_recorded_context_after_heads_advance()
    {
        var temporaryDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var match = new Match("FC Bayern München", "Borussia Dortmund", default, 1);
            var recordedOrdinaryDocuments = CreateMatchContextDocuments();
            var manifest = new PreparedExperimentManifest
            {
                TaskType = "slice",
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = "test-community",
                SliceDatasetName = "test-dataset",
                Items = [CreateBundesligaPreparedItem(
                    match,
                    CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: recordedOrdinaryDocuments))]
            };
            var manifestPath = Path.Combine(temporaryDirectory.FullName, "prepared-bundesliga.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, PreparedExperimentCommandSupport.JsonOptions));

            // The generic rows have since advanced from the exact version recorded in the
            // artifact.  A reconstruction must never fall back to these current heads.
            var advancedOrdinaryDocuments = recordedOrdinaryDocuments.ToDictionary(
                entry => entry.Key,
                entry => new ContextDocument(entry.Key, $"advanced:{entry.Value.Content}", entry.Value.Version + 1, DateTimeOffset.UtcNow),
                StringComparer.Ordinal);
            var contextRepository = new Mock<IContextRepository>(MockBehavior.Strict);
            contextRepository
                .Setup(repository => repository.GetContextDocumentAsync(
                    It.IsAny<string>(), 1, "test-community", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, int _, string _, CancellationToken _) => recordedOrdinaryDocuments[name]);
            contextRepository
                .Setup(repository => repository.GetLatestContextDocumentAsync(
                    It.IsAny<string>(), "test-community", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, string _, CancellationToken _) => advancedOrdinaryDocuments[name]);

            var recordedPublicationRepository = CreateMockBundesligaDocumentPublicationRepository();
            var recordedRoster = await recordedPublicationRepository.Object.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.Rosters, "test-community");
            var recordedElo = await recordedPublicationRepository.Object.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.ClubElo, "test-community");
            var advancedRoster = CreateAdvancedHead(recordedRoster!, 'a');
            var advancedElo = CreateAdvancedHead(recordedElo!, 'b');
            var publicationRepository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
            publicationRepository.SetupGet(repository => repository.Competition).Returns(CompetitionIds.Bundesliga2026_27);
            publicationRepository
                .Setup(repository => repository.GetSnapshotAsync(
                    BundesligaDocumentPublication.Rosters,
                    "test-community",
                    recordedRoster!.Snapshot.SnapshotId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(recordedRoster);
            publicationRepository
                .Setup(repository => repository.GetSnapshotAsync(
                    BundesligaDocumentPublication.ClubElo,
                    "test-community",
                    recordedElo!.Snapshot.SnapshotId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(recordedElo);
            // These represent post-prediction heads with different snapshot identities.
            // Recorded reconstruction is required to use the two immutable snapshot IDs above,
            // never either current head.
            publicationRepository
                .Setup(repository => repository.GetLastKnownGoodAsync(
                    BundesligaDocumentPublication.Rosters, "test-community", It.IsAny<CancellationToken>()))
                .ReturnsAsync(advancedRoster);
            publicationRepository
                .Setup(repository => repository.GetLastKnownGoodAsync(
                    BundesligaDocumentPublication.ClubElo, "test-community", It.IsAny<CancellationToken>()))
                .ReturnsAsync(advancedElo);

            var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
            predictionRepository
                .Setup(repository => repository.GetStoredMatchAsync(
                    match.HomeTeam, match.AwayTeam, match.Matchday, (PredictionModelConfig?)null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(match);
            var outcomeRepository = CreateMockMatchOutcomeRepository(matchdayOutcomes:
            new List<PersistedMatchOutcome>
            {
                new(
                "test-community", CompetitionIds.Bundesliga2026_27, match.HomeTeam, match.AwayTeam, match.StartsAt,
                match.Matchday, 2, 1, MatchOutcomeAvailability.Completed, "tippspiel-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            });
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: contextRepository,
                matchOutcomeRepository: outcomeRepository,
                documentPublicationRepository: publicationRepository);

            IReadOnlyList<DocumentContext>? usedContext = null;
            var predictionService = CreateMockPredictionService(predictMatchResult: new Prediction(2, 1));
            predictionService
                .Setup(service => service.PredictMatchAsync(
                    It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
                    It.IsAny<PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()))
                .Callback((Match _, IEnumerable<DocumentContext> documents, bool _, PredictionTelemetryMetadata? _, CancellationToken _) =>
                    usedContext = documents.ToList())
                .ReturnsAsync(new Prediction(2, 1));

            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(It.IsAny<LangfuseCreateDatasetRunItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunItem("run-item-1", "run-1", "recorded-run", "slice", "trace", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(It.IsAny<LangfuseCreateScoreRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync("test-dataset", "recorded-run", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems("run-1", "recorded-run", "dataset-1", "test-dataset", null, default, []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync("dataset-1", "recorded-run", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>([], new LangfusePaginationMeta(1, 100, 1, 1)));

            var executor = new PreparedExperimentRunExecutor(
                firebaseFactory.Object,
                CreateMockOpenAiServiceFactory(predictionService: predictionService).Object,
                langfuseClient.Object);
            var summary = await executor.ExecuteAsync(
                "slice",
                new PreparedExperimentRunRequest(
                    manifestPath,
                    "recorded-run",
                    null,
                    null,
                    false,
                    new PreparedExperimentRunOptions(
                        "gpt-5-nano", "prompt", false, "2026-08-21T20:30:00 Europe/Berlin (+02)", null, null,
                        "test-dataset", "local", null, null, null, "simple-batched", BatchSize: 1)),
                CancellationToken.None);

            await Assert.That(summary.ExecutionCount).IsEqualTo(1);
            await Assert.That(usedContext).HasCount().EqualTo(11);
            await Assert.That(usedContext!.Where(document => recordedOrdinaryDocuments.ContainsKey(document.Name))
                .Select(document => document.Content)).IsEquivalentTo(recordedOrdinaryDocuments.Values.Select(document => document.Content));
            await Assert.That(usedContext!.Select(document => document.Content)).DoesNotContain(advancedOrdinaryDocuments.Values.First().Content);
            contextRepository.Verify(repository => repository.GetLatestContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            publicationRepository.Verify(repository => repository.GetLastKnownGoodAsync(
                It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            publicationRepository.Verify(repository => repository.GetSnapshotAsync(
                BundesligaDocumentPublication.Rosters, "test-community", recordedRoster!.Snapshot.SnapshotId, It.IsAny<CancellationToken>()), Times.Once);
            publicationRepository.Verify(repository => repository.GetSnapshotAsync(
                BundesligaDocumentPublication.ClubElo, "test-community", recordedElo!.Snapshot.SnapshotId, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Warmup_then_batch_chunks_with_twenty_five_items_and_three_batches_runs_warmup_then_three_equal_batches()
    {
        var items = Enumerable.Range(1, 25).ToArray();

        var chunks = PreparedExperimentSupport.CreateWarmupThenBatchChunks(items, 3);

        await Assert.That(chunks.Select(chunk => chunk.Count).ToArray()).IsEquivalentTo([1, 8, 8, 8]);
        await Assert.That(chunks[0]).IsEquivalentTo([1]);
        await Assert.That(chunks[1]).IsEquivalentTo([2, 3, 4, 5, 6, 7, 8, 9]);
        await Assert.That(chunks[2]).IsEquivalentTo([10, 11, 12, 13, 14, 15, 16, 17]);
        await Assert.That(chunks[3]).IsEquivalentTo([18, 19, 20, 21, 22, 23, 24, 25]);
    }

    [Test]
    public async Task Repeated_match_slice_batches_keep_per_fixture_warmup_batches_and_limit_parallel_workflows()
    {
        var items = Enumerable.Range(1, 3)
            .SelectMany(fixtureIndex => Enumerable.Range(1, 3).Select(repetitionIndex => new PreparedExperimentManifestItem
            {
                SourceDatasetItemId = $"source-{fixtureIndex}",
                SliceDatasetItemId = $"source-{fixtureIndex}__repeated-match-slice__random-3x3__m{fixtureIndex:00}__{repetitionIndex:00}",
                HomeTeam = $"Home {fixtureIndex}",
                AwayTeam = $"Away {fixtureIndex}",
                Matchday = fixtureIndex,
                StartsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)",
                FixtureIndex = fixtureIndex,
                RepetitionIndex = repetitionIndex
            }))
            .ToList();

        var batches = PreparedExperimentRunExecutor.CreateRepeatedMatchSliceBatches(
            items,
            batchCount: 2,
            parallelism: 2);

        await Assert.That(batches.Select(batch => batch.Count).ToArray()).IsEquivalentTo([2, 2, 2, 1, 1, 1]);
        await Assert.That(batches[0].Select(item => item.FixtureIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([1, 2]);
        await Assert.That(batches[0].All(item => item.RepetitionIndex == 1)).IsTrue();
        await Assert.That(batches[1].Select(item => item.RepetitionIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([2, 2]);
        await Assert.That(batches[2].Select(item => item.RepetitionIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([3, 3]);
        await Assert.That(batches[3].Single().FixtureIndex).IsEqualTo(3);
        await Assert.That(batches[3].Single().RepetitionIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Repeated_match_slice_score_summary_averages_totals_by_repetition()
    {
        var summaries = new[]
        {
            CreateExecutionSummary("fixture-1", 1, 1, 4),
            CreateExecutionSummary("fixture-2", 2, 1, 2),
            CreateExecutionSummary("fixture-1", 1, 2, 0),
            CreateExecutionSummary("fixture-2", 2, 2, 2)
        };

        var scores = PreparedExperimentSupport.SummarizeExecutionScores(
            summaries,
            "repeated-match-slice");

        await Assert.That(scores.TotalKicktippPoints).IsEqualTo(8);
        await Assert.That(scores.AvgKicktippPoints).IsEqualTo(4);
    }

    [Test]
    public async Task Run_repeated_match_slice_settings_default_parallelism_to_five_and_validate_bounds()
    {
        var settings = new RunRepeatedMatchSliceSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name"
        };
        var invalidSettings = new RunRepeatedMatchSliceSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            Parallelism = 0
        };

        var result = settings.Validate();
        var invalidResult = invalidSettings.Validate();
        var options = settings.ToRunOptions();

        await Assert.That(result.Successful).IsTrue();
        await Assert.That(options.BatchCount).IsEqualTo(3);
        await Assert.That(options.Parallelism).IsEqualTo(5);
        await Assert.That(invalidResult.Successful).IsFalse();
        await Assert.That(invalidResult.Message).Contains("--parallelism must be at least 1");
    }

    [Test]
    public async Task Run_experiment_settings_require_langfuse_prompt_name_for_langfuse_prompt_source()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            PromptSource = "langfuse"
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--langfuse-prompt-name is required");
    }

    private static PreparedExperimentManifestItem CreateBundesligaPreparedItem(
        Match match,
        ResolvedMatchContextManifest resolvedContextManifest) =>
        new()
        {
            SourceDatasetItemId = "source",
            SliceDatasetItemId = "slice",
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            Matchday = match.Matchday,
            StartsAt = "2026-08-21T20:30:00Z",
            ResolvedContextManifest = resolvedContextManifest,
            PredictionCreatedAt = DateTimeOffset.UtcNow
        };

    private static PreparedExperimentManifest CreateHistoricalPreparedManifest(
        int matchCount = 1,
        int repetitions = 1)
    {
        const string community = "pes-squad";
        const string sliceKey = "historical-topology-test";
        var fixtureTeams = new[]
        {
            (Home: "VfL Wolfsburg", Away: "Eintracht Frankfurt"),
            (Home: "FC Bayern München", Away: "Borussia Dortmund"),
            (Home: "RB Leipzig", Away: "VfB Stuttgart"),
            (Home: "SC Freiburg", Away: "1. FC Köln"),
            (Home: "FC St. Pauli", Away: "Hamburger SV")
        };
        if (matchCount < 1 || matchCount > fixtureTeams.Length || repetitions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(matchCount));
        }

        var eligibleFixtureIds = Enumerable.Range(1, fixtureTeams.Length)
            .Select(index => ExperimentArtifactSupport.BuildHostedDatasetItemId(
                CompetitionIds.Bundesliga2025_26,
                community,
                (1423757340 + index).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        var compatibility = new PreparedHistoricalExperimentCompatibility
        {
            Mode = ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1,
            OfficialKnowledgeCutoff = "2026-02-16",
            SamplingCutoff = "2026-02-18T00:00:00 Europe/Berlin (+01)",
            BoundPromptSource = PreparedHistoricalExperimentCompatibility.PromptSource,
            BoundPromptName = PreparedHistoricalExperimentCompatibility.PromptName,
            BoundPromptLabel = PreparedHistoricalExperimentCompatibility.PromptLabel,
            BoundPromptVersion = PreparedHistoricalExperimentCompatibility.PromptVersion,
            BoundEvaluationPolicyKind = PreparedHistoricalExperimentCompatibility.EvaluationPolicyKind,
            BoundEvaluationPolicyReference = PreparedHistoricalExperimentCompatibility.EvaluationPolicyReference,
            BoundEvaluationPolicyOffset = PreparedHistoricalExperimentCompatibility.EvaluationPolicyOffset,
            ContextDocumentCount = 7,
            EligibilityPolicy = PreparedHistoricalExperimentCompatibility.RequiredEligibilityPolicy,
            EligibleFixtureCount = eligibleFixtureIds.Length,
            EligibleFixtureIdsHash = ExperimentArtifactSupport.ComputeSelectedItemIdsHash(eligibleFixtureIds)
        };
        var selectedItemIds = new List<string>(matchCount);
        var items = new List<PreparedExperimentManifestItem>(matchCount * repetitions);
        for (var fixtureIndex = 1; fixtureIndex <= matchCount; fixtureIndex += 1)
        {
            var teams = fixtureTeams[fixtureIndex - 1];
            var day = 10 + fixtureIndex;
            var startsAt = $"2026-04-{day:D2}T16:30:00 Europe/Berlin (+02)";
            var startsAtInstant = new DateTimeOffset(2026, 4, day, 16, 30, 0, TimeSpan.FromHours(2));
            var evaluation = startsAtInstant.AddHours(-12);
            var match = new Match(
                teams.Home,
                teams.Away,
                NodaTime.Instant.FromDateTimeOffset(startsAtInstant).InZone(NodaTime.DateTimeZone.Utc),
                28 + fixtureIndex);
            var entries = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
                    match,
                    community).RequiredDocumentNames
                .Select((name, index) => new ResolvedHistoricalExperimentContextDocument(
                    name,
                    index,
                    ResolvedHistoricalExperimentContextManifest.BuildLegacyDocumentId(name, community, index),
                    evaluation.AddMinutes(-(index + 1)),
                    DocumentPublicationContract.ComputeContentSha256($"content:{name}")))
                .ToArray();
            var historicalContext = ResolvedHistoricalExperimentContextManifest.Create(
                community,
                evaluation,
                entries);
            var tippSpielId = (1423757340 + fixtureIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var sourceDatasetItemId = ExperimentArtifactSupport.BuildHostedDatasetItemId(
                CompetitionIds.Bundesliga2025_26,
                community,
                tippSpielId);
            selectedItemIds.Add(sourceDatasetItemId);
            for (var repetitionIndex = 1; repetitionIndex <= repetitions; repetitionIndex += 1)
            {
                items.Add(new PreparedExperimentManifestItem
                {
                    SourceDatasetItemId = sourceDatasetItemId,
                    SliceDatasetItemId = ExperimentArtifactSupport.BuildRepeatedMatchSliceDatasetItemId(
                        sourceDatasetItemId,
                        sliceKey,
                        fixtureIndex,
                        matchCount,
                        repetitionIndex,
                        repetitions),
                    HomeTeam = match.HomeTeam,
                    AwayTeam = match.AwayTeam,
                    Matchday = match.Matchday,
                    StartsAt = startsAt,
                    TippSpielId = tippSpielId,
                    FixtureIndex = fixtureIndex,
                    RepetitionIndex = repetitionIndex,
                    HistoricalContextManifest = historicalContext,
                    ExpectedHomeGoals = 2,
                    ExpectedAwayGoals = 1
                });
            }
        }

        var manifest = new PreparedExperimentManifest
        {
            TaskType = "repeated-match-slice",
            Competition = CompetitionIds.Bundesliga2025_26,
            CommunityContext = community,
            Season = ExperimentArtifactSupport.Season,
            SliceKey = sliceKey,
            SliceKind = "repeated-match-slice",
            SampleMethod = "repeated-match-slice",
            SourcePoolKey = "all-matchdays-after-20260217t230000z",
            SourceDatasetName = ExperimentArtifactSupport.BuildSourceDatasetName(community),
            SliceDatasetName = "historical-dataset",
            SampleSeed = 42,
            SampleSize = matchCount * repetitions,
            MatchCount = matchCount,
            Repetitions = repetitions,
            SelectedItemIds = selectedItemIds,
            SelectedItemIdsHash = ExperimentArtifactSupport.ComputeSelectedItemIdsHash(selectedItemIds),
            StartsAfter = compatibility.SamplingCutoff,
            HistoricalCompatibility = compatibility,
            Items = items
        };
        return manifest with
        {
            HistoricalArtifactSha256 = PreparedExperimentCommandSupport.ComputeHistoricalArtifactSha256(manifest)
        };
    }

    private static PreparedExperimentRunOptions CreateHistoricalRunOptions() =>
        new(
            "gpt-5.6-luna",
            "bundesliga-match-v2",
            false,
            null,
            PreparedHistoricalExperimentCompatibility.EvaluationPolicyKind,
            PreparedHistoricalExperimentCompatibility.EvaluationPolicyOffset,
            "historical-dataset",
            PreparedHistoricalExperimentCompatibility.PromptSource,
            PreparedHistoricalExperimentCompatibility.PromptName,
            PreparedHistoricalExperimentCompatibility.PromptLabel,
            PreparedHistoricalExperimentCompatibility.PromptVersion,
            "warmup-plus-batches",
            BatchCount: 1,
            ReasoningEffort: "none",
            MaxOutputTokenCount: 10000,
            Parallelism: 1);

    private static LoadedDocumentPublication CreateAdvancedHead(
        LoadedDocumentPublication recorded,
        char snapshotCharacter) =>
        new(
            new DocumentPublicationSnapshot(
                recorded.Snapshot.Competition,
                recorded.Snapshot.CommunityContext,
                recorded.Snapshot.PublicationSet,
                new string(snapshotCharacter, DocumentPublicationContract.Sha256HexLength),
                recorded.Snapshot.SnapshotId,
                recorded.Snapshot.CreatedAt.AddMinutes(1),
                recorded.Snapshot.MetadataJson,
                recorded.Snapshot.Documents),
            recorded.Documents);

    private static PreparedExperimentExecutionSummary CreateExecutionSummary(
        string sourceDatasetItemId,
        int fixtureIndex,
        int repetitionIndex,
        int kicktippPoints)
    {
        return new PreparedExperimentExecutionSummary(
            $"{sourceDatasetItemId}__repeated-match-slice__slice__m{fixtureIndex:00}__{repetitionIndex:00}",
            sourceDatasetItemId,
            "run-name",
            $"trace-{fixtureIndex}-{repetitionIndex}",
            null,
            new ExperimentItemScores(kicktippPoints),
            [],
            null,
            "placed",
            fixtureIndex,
            repetitionIndex);
    }

    [Test]
    public async Task Run_experiment_settings_accept_langfuse_prompt_source_with_justification()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            PromptSource = "langfuse",
            LangfusePromptName = CompetitionResolver.BundesligaMatchPromptName,
            IncludeJustification = true
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsTrue();
    }

    [Test]
    public async Task Run_experiment_settings_reject_world_cup_hosted_prompt_with_justification()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            PromptSource = "langfuse",
            LangfusePromptName = CompetitionResolver.WorldCupMatchPromptName,
            IncludeJustification = true
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("WM 2026");
    }

    [Test]
    public async Task Run_experiment_settings_accept_and_normalize_reasoning_effort_values()
    {
        var noneSettings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = "None"
        };
        var xhighSettings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = " XHIGH "
        };

        var noneResult = noneSettings.Validate();
        var xhighResult = xhighSettings.Validate();

        await Assert.That(noneResult.Successful).IsTrue();
        await Assert.That(xhighResult.Successful).IsTrue();
        await Assert.That(noneSettings.ReasoningEffort).IsEqualTo("none");
        await Assert.That(xhighSettings.ReasoningEffort).IsEqualTo("xhigh");
    }

    [Test]
    public async Task Run_experiment_settings_reject_invalid_reasoning_effort_values()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = "maximum"
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--reasoning-effort must be one of");
    }

    [Test]
    public async Task Run_experiment_settings_reject_invalid_max_output_token_count()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            MaxOutputTokenCount = 0
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--max-output-tokens must be at least 1");
    }

    [Test]
    public async Task Langfuse_prompt_run_options_flow_into_experiment_metadata_tags_and_propagated_metadata()
    {
        var manifest = new PreparedExperimentManifest
        {
            SliceKey = "repeat-25",
            SliceKind = "repeated-match",
            SampleMethod = "repeated-match",
            CommunityContext = "pes-squad",
            SourcePoolKey = "md26-vfb-stuttgart-vs-rb-leipzig",
            SourceDatasetName = "match-predictions/bundesliga-2025-26/pes-squad",
            SliceDatasetName = "match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25",
            Competition = "bundesliga-2025-26",
            Season = "2025/2026",
            SampleSize = 25,
            Items =
            [
                new PreparedExperimentManifestItem
                {
                    SourceDatasetItemId = "source-item",
                    SliceDatasetItemId = "slice-item",
                    HomeTeam = "VfB Stuttgart",
                    AwayTeam = "RB Leipzig",
                    Matchday = 26,
                    StartsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)"
                }
            ]
        };
        var options = new PreparedExperimentRunOptions(
            "gpt-5.5",
            "langfuse-o3-poc",
            false,
            "2026-03-15T12:00:00 Europe/Berlin (+01)",
            null,
            null,
            null,
            "langfuse",
            "kicktippai/predict-one-match-o3-poc",
            "poc",
            7,
            "warmup-plus-batches",
            null,
            3,
            "xhigh",
            20_000);

        var metadata = PreparedExperimentSupport.BuildRunMetadata(manifest, options);
        var tags = PreparedExperimentSupport.DeriveTraceTags(metadata);
        var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(metadata);

        await Assert.That(metadata.PromptSource).IsEqualTo("langfuse");
        await Assert.That(metadata.LangfusePromptName).IsEqualTo("kicktippai/predict-one-match-o3-poc");
        await Assert.That(metadata.LangfusePromptLabel).IsEqualTo("poc");
        await Assert.That(metadata.LangfusePromptVersion).IsEqualTo(7);
        await Assert.That(metadata.ReasoningEffort).IsEqualTo("xhigh");
        await Assert.That(metadata.MaxOutputTokenCount).IsEqualTo(20_000);
        await Assert.That(metadata.RunSubjectId).IsEqualTo("gpt-5.5:reasoning-effort:xhigh");
        await Assert.That(metadata.RunSubjectDisplayName).IsEqualTo("gpt-5.5 (xhigh)");
        await Assert.That(tags).Contains("prompt-source:langfuse");
        await Assert.That(tags).Contains("langfuse-prompt:kicktippai/predict-one-match-o3-poc");
        await Assert.That(tags).Contains("langfuse-prompt-label:poc");
        await Assert.That(tags).Contains("langfuse-prompt-version:7");
        await Assert.That(tags).Contains("reasoning-effort:xhigh");
        await Assert.That(tags).Contains("max-output-tokens:20000");
        await Assert.That(propagatedMetadata["promptSource"]).IsEqualTo("langfuse");
        await Assert.That(propagatedMetadata["langfusePromptVersion"]).IsEqualTo("7");
        await Assert.That(propagatedMetadata["reasoningEffort"]).IsEqualTo("xhigh");
        await Assert.That(propagatedMetadata["maxOutputTokens"]).IsEqualTo("20000");
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_slice_reconstructs_predicts_and_posts_scores()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var runMetadataPath = Path.Combine(tempDirectory.FullName, "run-metadata.json");
            var runName = "slice__test-community__gpt-5-nano__prompt-v1__random-1-seed-20251011__startsat-12h__2026-01-10t12-00-00z";
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/slices/all-matchdays/random-1-seed-20251011";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20251011";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    sliceKey = "random-1-seed-20251011",
                    sliceKind = "random-sample",
                    sampleMethod = "random-sample",
                    communityContext = "test-community",
                    sourcePoolKey = "all-matchdays",
                    canonicalDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSeed = 20251011,
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-123",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "FC Bayern München",
                            awayTeam = "RB Leipzig",
                            matchday = 7,
                            startsAt = "2025-10-30T15:30:00 Europe/Berlin (+01)"
                        }
                    }
                }));

            await File.WriteAllTextAsync(
                runMetadataPath,
                JsonSerializer.Serialize(new
                {
                    runner = "match-experiment-runner",
                    task = "slice",
                    communityContext = "test-community",
                    competition = "bundesliga-2025-26",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    datasetName,
                    promptKey = "prompt-v1",
                    sliceKind = "random-sample",
                    sliceKey = "random-1-seed-20251011",
                    sourcePoolKey = "all-matchdays",
                    selectedItemIdsHash = "hash-123",
                    selectedItemIdsCount = 1,
                    sampleSize = 1,
                    evaluationTimestampPolicyKey = "startsat-12h",
                    evaluationTimestampPolicy = new
                    {
                        kind = "relative",
                        reference = "startsAt",
                        offset = "-12:00:00"
                    },
                    startedAtUtc = "2026-01-10T12:00:00Z",
                    sampleSeed = 20251011,
                    sampleMethod = "random-sample",
                    includeJustification = false,
                    promptVersion = "prompt-v1",
                    sourceDatasetKind = "slice",
                    datasetItemIdMap = new Dictionary<string, string>
                    {
                        [sourceDatasetItemId] = sliceDatasetItemId
                    },
                    model = "gpt-5-nano",
                    batchStrategy = "simple-batched",
                    batchSize = 1
                }));

            var match = new Match(
                "FC Bayern München",
                "RB Leipzig",
                NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc(),
                7);
            var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
            predictionRepository
                .Setup(repository => repository.GetStoredMatchAsync(
                    "FC Bayern München",
                    "RB Leipzig",
                    7,
                    (PredictionModelConfig?)null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(match);

            var evaluationTimestamp = new DateTimeOffset(2025, 10, 30, 3, 30, 0, TimeSpan.FromHours(1));
            var selection = MatchContextDocumentCatalog.ForMatch("FC Bayern München", "RB Leipzig", "test-community");
            var contextRepository = new Mock<IContextRepository>();
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        evaluationTimestamp,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(documentName, $"content:{documentName}", 1, evaluationTimestamp.AddMinutes(-5)));
            }


            var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    7,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "FC Bayern München",
                        "RB Leipzig",
                        NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc(),
                        7,
                        2,
                        1,
                        MatchOutcomeAvailability.Completed,
                        "123",
                        evaluationTimestamp,
                        evaluationTimestamp)
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory.Setup(factory => factory.CreatePredictionRepository(It.IsAny<string>())).Returns(predictionRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateContextRepository(It.IsAny<string>())).Returns(contextRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateMatchOutcomeRepository(It.IsAny<string>())).Returns(matchOutcomeRepository.Object);
            firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

            var predictionService = CreateMockPredictionService(
                predictMatchResult: new Prediction(2, 1),
                matchPromptPath: "prompts/gpt-5/match.md");
            predictionService
                .Setup(service => service.PredictMatchAsync(
                    It.IsAny<Match>(),
                    It.IsAny<IEnumerable<DocumentContext>>(),
                    It.IsAny<bool>(),
                    It.IsAny<PredictionTelemetryMetadata?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    using var childActivity = Telemetry.Source.StartActivity("predict-match");
                    childActivity?.SetTag("langfuse.observation.type", "generation");
                    return new Prediction(2, 1);
                });
            var openAiServiceFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var createdDatasetRunItems = new List<LangfuseCreateDatasetRunItemRequest>();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => createdDatasetRunItems.Add(request))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    "dataset-run-item-1",
                    "dataset-run-1",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    runName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    runName,
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        runName,
                        sliceDatasetItemId,
                        "trace-id",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunSliceCommand>(
                "run-slice",
                firebaseServiceFactory: firebaseFactory,
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-slice",
                "gpt-5-nano",
                "--manifest",
                manifestPath,
                "--run-name",
                runName,
                "--run-metadata-file",
                runMetadataPath,
                "--batch-size",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"executionCount\": 1");
            await Assert.That(output).Contains("\"taskType\": \"slice\"");
            await Assert.That(output).Contains("\"total_kicktipp_points\": 4");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo(["avg_kicktipp_points", "kicktipp_points", "total_kicktipp_points"]);
            await Assert.That(postedScores.All(score => !string.IsNullOrWhiteSpace(score.Id))).IsTrue();
            await Assert.That(postedScores.Select(score => score.Id).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(3);
            var experimentItemRun = capturedActivities.Single(activity => activity.OperationName == "experiment-item-run");
            var experimentItemInput = experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString();
            await Assert.That(experimentItemInput).Contains("RB Leipzig");
            await Assert.That(experimentItemInput).Contains("2025-10-30T15:30:00 Europe/Berlin");
            await Assert.That(experimentItemInput).DoesNotContain("datasetName");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.input")?.ToString()).IsEqualTo(experimentItemInput);
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.expected_output")?.ToString())
                .IsEqualTo("{\"score\":\"2:1\"}");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.metadata")?.ToString())
                .Contains(sliceDatasetItemId);
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.output")?.ToString()).Contains("\"homeGoals\":2");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.tags")?.ToString()).DoesNotContain("phase-2");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.tags")?.ToString()).DoesNotContain("experiment");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.id")).IsEqualTo("dataset-run-1");
            await Assert.That(createdDatasetRunItems.Single().ObservationId).IsEqualTo(experimentItemRun.SpanId.ToString());
            var predictMatchActivity = capturedActivities.Single(activity => activity.OperationName == "predict-match");
            await Assert.That(predictMatchActivity.GetBaggageItem("langfuse.experiment.id")).IsEqualTo("dataset-run-1");
            await Assert.That(predictMatchActivity.GetBaggageItem("langfuse.experiment.item.id")).IsEqualTo(sliceDatasetItemId);

            langfuseClient.Verify(client => client.CreateDatasetRunItemAsync(It.IsAny<LangfuseCreateDatasetRunItemRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            langfuseClient.Verify(client => client.CreateScoreAsync(It.IsAny<LangfuseCreateScoreRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_repeated_match_without_metadata_file_uses_direct_settings_and_exact_evaluation_time()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var runName = "repeated-match__test-community__gpt-5-nano__prompt-v1__repeat-1__exact-time__2026-03-15t12-00-00z";
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__repeated-match__repeat-1__01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    sliceKey = "repeat-1",
                    sliceKind = "repeated-match",
                    sampleMethod = "repeated-match",
                    communityContext = "test-community",
                    sourcePoolKey = "md26-vfb-stuttgart-vs-rb-leipzig",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-456",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "VfB Stuttgart",
                            awayTeam = "RB Leipzig",
                            matchday = 26,
                            startsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)"
                        }
                    }
                }));

            var exactEvaluationTime = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1));
            var match = new Match(
                "VfB Stuttgart",
                "RB Leipzig",
                NodaTime.Instant.FromUtc(2026, 3, 15, 14, 30).InUtc(),
                26);

            var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
            predictionRepository
                .Setup(repository => repository.GetStoredMatchAsync(
                    "VfB Stuttgart",
                    "RB Leipzig",
                    26,
                    (PredictionModelConfig?)null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(match);

            var selection = MatchContextDocumentCatalog.ForMatch("VfB Stuttgart", "RB Leipzig", "test-community");
            var contextRepository = new Mock<IContextRepository>();
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        exactEvaluationTime,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(documentName, $"content:{documentName}", 1, exactEvaluationTime.AddMinutes(-5)));
            }


            var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    26,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "VfB Stuttgart",
                        "RB Leipzig",
                        NodaTime.Instant.FromUtc(2026, 3, 15, 14, 30).InUtc(),
                        26,
                        2,
                        1,
                        MatchOutcomeAvailability.Completed,
                        "123",
                        exactEvaluationTime,
                        exactEvaluationTime)
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory.Setup(factory => factory.CreatePredictionRepository(It.IsAny<string>())).Returns(predictionRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateContextRepository(It.IsAny<string>())).Returns(contextRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateMatchOutcomeRepository(It.IsAny<string>())).Returns(matchOutcomeRepository.Object);
            firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((Google.Cloud.Firestore.FirestoreDb)null!);

            var predictionService = CreateMockPredictionService(
                predictMatchResult: new Prediction(2, 1),
                matchPromptPath: "prompts/gpt-5/match.md");
            var openAiServiceFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    "dataset-run-item-1",
                    "dataset-run-1",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    runName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    runName,
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        runName,
                        sliceDatasetItemId,
                        "trace-id",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunRepeatedMatchCommand>(
                "run-repeated-match",
                firebaseServiceFactory: firebaseFactory,
                configureServices: new Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-repeated-match",
                "gpt-5-nano",
                "--manifest",
                manifestPath,
                "--run-name",
                runName,
                "--prompt-key",
                "prompt-v1",
                "--evaluation-time",
                "2026-03-15T12:00:00 Europe/Berlin (+01)",
                "--batch-count",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"executionCount\": 1");
            await Assert.That(output).Contains("\"taskType\": \"repeated-match\"");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo(["avg_kicktipp_points", "kicktipp_points", "total_kicktipp_points"]);
            var experimentItemRun = capturedActivities.Single(activity => activity.OperationName == "experiment-item-run");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString())
                .Contains("VfB Stuttgart vs RB Leipzig");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString())
                .DoesNotContain("datasetName");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.expected_output")?.ToString())
                .IsEqualTo("{\"score\":\"2:1\"}");

            contextRepository.Verify(repository => repository.GetContextDocumentByTimestampAsync(
                It.IsAny<string>(),
                exactEvaluationTime,
                "test-community",
                It.IsAny<CancellationToken>()), Times.AtLeast(selection.RequiredDocumentNames.Count));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_community_to_date_creates_one_dataset_run_per_participant()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/community-to-date/through-md01/community-to-date-md01";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__slice__community-to-date-md01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    task = "community-to-date",
                    sliceKey = "community-to-date-md01",
                    sliceKind = "community-to-date",
                    sampleMethod = "community-to-date",
                    communityContext = "test-community",
                    sourcePoolKey = "through-md01",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-community-1",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "Team A",
                            awayTeam = "Team B",
                            matchday = 1,
                            startsAt = "2025-08-22T20:30:00 Europe/Berlin (+02)",
                            tippSpielId = "123"
                        }
                    },
                    participants = new object[]
                    {
                        new
                        {
                            participantId = "p1",
                            displayName = "Alice",
                            predictions = new[]
                            {
                                new
                                {
                                    sourceDatasetItemId,
                                    status = "placed",
                                    homeGoals = 2,
                                    awayGoals = 1,
                                    kicktippPoints = 4
                                }
                            }
                        },
                        new
                        {
                            participantId = "p2",
                            displayName = "Bob",
                            predictions = new[]
                            {
                                new
                                {
                                    sourceDatasetItemId,
                                    status = "missed",
                                    homeGoals = (int?)null,
                                    awayGoals = (int?)null,
                                    kicktippPoints = 0
                                }
                            }
                        }
                    }
                }));

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var openAiServiceFactory = CreateMockOpenAiServiceFactory();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    $"{request.RunName}-item-1",
                    request.RunName.Contains("alice", StringComparison.Ordinal) ? "dataset-run-1" : "dataset-run-2",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    It.Is<string>(runName => runName.Contains("alice", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string runName, CancellationToken _) => new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    It.Is<string>(runName => runName.Contains("bob", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string runName, CancellationToken _) => new LangfuseDatasetRunWithItems(
                    "dataset-run-2",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    It.Is<string>(runName => runName.Contains("alice", StringComparison.Ordinal)),
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__alice-p1",
                        sliceDatasetItemId,
                        "trace-alice",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    It.Is<string>(runName => runName.Contains("bob", StringComparison.Ordinal)),
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-2",
                        "dataset-run-2",
                        "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__bob-p2",
                        sliceDatasetItemId,
                        "trace-bob",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunCommunityToDateCommand>(
                "run-community-to-date",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-community-to-date",
                "--manifest",
                manifestPath,
                "--run-family-name",
                "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z",
                "--participant-limit",
                "2",
                "--batch-size",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"taskType\": \"community-to-date\"");
            await Assert.That(output).Contains("\"runCount\": 2");
            await Assert.That(output).Contains("\"executionCount\": 2");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo([
                    "avg_kicktipp_points",
                    "avg_kicktipp_points",
                    "kicktipp_points",
                    "kicktipp_points",
                    "total_kicktipp_points",
                    "total_kicktipp_points"
                ]);
            await Assert.That(postedScores.All(score => !string.IsNullOrWhiteSpace(score.Id))).IsTrue();
            await Assert.That(postedScores.Select(score => score.Id).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(6);
            await Assert.That(capturedActivities.Any(activity => activity.OperationName == "community-match-prediction")).IsTrue();
            await Assert.That(postedScores.Where(score => score.Name == "kicktipp_points").All(score => !string.IsNullOrWhiteSpace(score.ObservationId))).IsTrue();

            var experimentItemRuns = capturedActivities
                .Where(activity => activity.OperationName == "experiment-item-run")
                .ToList();
            await Assert.That(experimentItemRuns.Count).IsEqualTo(2);
            await Assert.That(experimentItemRuns.All(activity =>
            {
                var input = activity.GetTagItem("langfuse.observation.input")?.ToString();
                return input is not null && input.Contains("Team A vs Team B", StringComparison.Ordinal);
            })).IsTrue();
            await Assert.That(experimentItemRuns.All(activity =>
            {
                var input = activity.GetTagItem("langfuse.observation.input")?.ToString();
                return input is not null && !input.Contains("datasetName", StringComparison.Ordinal);
            })).IsTrue();

            var predictionObservations = capturedActivities
                .Where(activity => activity.OperationName == "community-match-prediction")
                .ToList();
            await Assert.That(predictionObservations.Count).IsEqualTo(2);
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.homeTeam")?.ToString(), "Team A", StringComparison.Ordinal))).IsTrue();
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.awayTeam")?.ToString(), "Team B", StringComparison.Ordinal))).IsTrue();
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.match")?.ToString(), "Team A vs Team B", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
