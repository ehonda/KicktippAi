using System.Text.Json;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareCommunityToDate;

public sealed class PrepareCommunityToDateCommand : AsyncCommand<PrepareCommunityToDateSettings>
{
    private const string Competition = "bundesliga-2025-26";

    private readonly IAnsiConsole _console;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ILogger<PrepareCommunityToDateCommand> _logger;

    public PrepareCommunityToDateCommand(
        IAnsiConsole console,
        IKicktippClientFactory kicktippClientFactory,
        ILogger<PrepareCommunityToDateCommand> logger)
    {
        _console = console;
        _kicktippClientFactory = kicktippClientFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PrepareCommunityToDateSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var kicktippClient = _kicktippClientFactory.CreateClient();
            var cutoffMatchday = settings.CutoffMatchday ?? await kicktippClient.GetCurrentTippuebersichtMatchdayAsync(settings.CommunityContext);
            var sourcePoolKey = string.IsNullOrWhiteSpace(settings.SourcePoolKey)
                ? $"through-md{cutoffMatchday:00}"
                : settings.SourcePoolKey.Trim();
            var sliceKey = string.IsNullOrWhiteSpace(settings.SliceKey)
                ? $"community-to-date-md{cutoffMatchday:00}"
                : settings.SliceKey.Trim();
            var sourceDatasetName = ExperimentArtifactSupport.BuildSourceDatasetName(settings.CommunityContext);
            var datasetName = settings.DatasetName
                ?? $"{sourceDatasetName}/community-to-date/{sourcePoolKey}/{sliceKey}";
            var outputDirectory = ResolveOutputDirectory(settings.OutputDirectory, settings.CommunityContext, sourcePoolKey, sliceKey);
            var sliceArtifactPath = Path.Combine(outputDirectory, "slice-dataset.json");
            var sliceManifestPath = Path.Combine(outputDirectory, "slice-manifest.json");

            Directory.CreateDirectory(outputDirectory);

            var sourceItems = new List<PreparedExperimentSourceItem>();
            var sourceItemsBySourceMatchId = new Dictionary<string, PreparedExperimentSourceItem>(StringComparer.Ordinal);
            var participantBuilders = new Dictionary<string, ParticipantBuilder>(StringComparer.Ordinal);

            for (var matchday = 1; matchday <= cutoffMatchday; matchday += 1)
            {
                var snapshot = await kicktippClient.GetCommunityMatchdaySnapshotAsync(settings.CommunityContext, matchday);
                if (snapshot is null)
                {
                    continue;
                }

                foreach (var outcome in snapshot.Outcomes.Where(candidate => candidate.HasOutcome && candidate.HomeGoals is not null && candidate.AwayGoals is not null))
                {
                    var datasetItem = ExperimentArtifactSupport.BuildHostedDatasetItem(outcome, settings.CommunityContext, Competition);
                    var sliceDatasetItemId = ExperimentArtifactSupport.BuildSliceDatasetItemId(datasetItem.Id, sliceKey);
                    var sourceItem = new PreparedExperimentSourceItem(
                        datasetItem.Id,
                        sliceDatasetItemId,
                        datasetItem.Id,
                        datasetItem.Metadata.Competition,
                        datasetItem.Metadata.Season,
                        datasetItem.Metadata.CommunityContext,
                        datasetItem.Metadata.Matchday,
                        datasetItem.Metadata.MatchdayLabel,
                        datasetItem.Metadata.HomeTeam,
                        datasetItem.Metadata.AwayTeam,
                        GetStartsAt(datasetItem),
                        datasetItem.Metadata.TippSpielId,
                        datasetItem.ExpectedOutput.HomeGoals,
                        datasetItem.ExpectedOutput.AwayGoals);

                    if (sourceItemsBySourceMatchId.TryAdd(sourceItem.TippSpielId, sourceItem))
                    {
                        sourceItems.Add(sourceItem);
                    }
                }

                foreach (var participant in snapshot.Participants)
                {
                    if (!participantBuilders.TryGetValue(participant.ParticipantId, out var builder))
                    {
                        builder = new ParticipantBuilder(participant.ParticipantId, participant.DisplayName);
                        participantBuilders.Add(participant.ParticipantId, builder);
                    }

                    foreach (var prediction in participant.Predictions)
                    {
                        var sourceMatchId = prediction.TippSpielId ?? prediction.SourceMatchId;
                        if (!sourceItemsBySourceMatchId.TryGetValue(sourceMatchId, out var sourceItem))
                        {
                            continue;
                        }

                        builder.Predictions[sourceItem.SourceDatasetItemId] = new PreparedExperimentParticipantPrediction
                        {
                            SourceDatasetItemId = sourceItem.SourceDatasetItemId,
                            Status = prediction.Status == KicktippCommunityPredictionStatus.Placed ? "placed" : "missed",
                            HomeGoals = prediction.Prediction?.HomeGoals,
                            AwayGoals = prediction.Prediction?.AwayGoals,
                            KicktippPoints = prediction.AwardedPoints
                        };
                    }
                }
            }

            if (sourceItems.Count == 0)
            {
                throw new InvalidOperationException("No completed Kicktipp matches were found for the requested community-to-date scope.");
            }

            var orderedSourceItems = sourceItems
                .OrderBy(item => item.Matchday)
                .ThenBy(item => item.SourceDatasetItemId, StringComparer.Ordinal)
                .ToList();
            var bundle = PreparedExperimentBundleBuilder.Build(
                orderedSourceItems,
                settings.CommunityContext,
                sourceDatasetName,
                datasetName,
                sliceKey,
                "community-to-date",
                "community-to-date",
                sourcePoolKey,
                null);

            var manifest = bundle.Manifest with
            {
                TaskType = "community-to-date",
                CutoffMatchday = cutoffMatchday,
                Participants = participantBuilders.Values
                    .OrderBy(builder => builder.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(builder => new PreparedExperimentParticipantManifest
                    {
                        ParticipantId = builder.ParticipantId,
                        DisplayName = builder.DisplayName,
                        Predictions = builder.Predictions.Values
                            .OrderBy(prediction => prediction.SourceDatasetItemId, StringComparer.Ordinal)
                            .ToList()
                    })
                    .ToList()
            };

            await WriteJsonFileAsync(sliceArtifactPath, bundle.Artifact, cancellationToken);
            await WriteJsonFileAsync(sliceManifestPath, manifest, cancellationToken);

            var summary = new
            {
                mode = "community-to-date",
                communityContext = settings.CommunityContext,
                cutoffMatchday,
                sourceDatasetName,
                datasetName = manifest.SliceDatasetName,
                manifest.SourcePoolKey,
                manifest.SliceKey,
                manifest.SampleSize,
                participantCount = manifest.Participants.Count,
                manifest.SelectedItemIdsHash,
                outputDirectory,
                sliceArtifactPath,
                sliceManifestPath
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing community-to-date experiment artifact");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static string GetStartsAt(HostedMatchExperimentDatasetItem item)
    {
        if (item.Input.ValueKind != JsonValueKind.Object
            || !item.Input.TryGetProperty("startsAt", out var startsAt)
            || startsAt.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(startsAt.GetString()))
        {
            throw new InvalidOperationException($"Dataset item '{item.Id}' is missing input.startsAt.");
        }

        return startsAt.GetString()!;
    }

    private static string ResolveOutputDirectory(
        string? outputDirectoryOverride,
        string communityContext,
        string sourcePoolKey,
        string sliceKey)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectoryOverride))
        {
            return Path.GetFullPath(outputDirectoryOverride);
        }

        return Path.GetFullPath(Path.Combine(
            "artifacts",
            "langfuse-experiments",
            "community-to-date",
            ExperimentArtifactSupport.Slugify(communityContext),
            sourcePoolKey,
            sliceKey));
    }

    private static Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PreparedExperimentCommandSupport.JsonOptions), cancellationToken);
    }

    private sealed class ParticipantBuilder
    {
        public ParticipantBuilder(string participantId, string displayName)
        {
            ParticipantId = participantId;
            DisplayName = displayName;
        }

        public string ParticipantId { get; }

        public string DisplayName { get; }

        public Dictionary<string, PreparedExperimentParticipantPrediction> Predictions { get; } = new(StringComparer.Ordinal);
    }
}
