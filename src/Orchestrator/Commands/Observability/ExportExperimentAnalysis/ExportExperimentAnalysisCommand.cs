using System.Globalization;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using CursorBasedComposite = EHonda.Pagination.CursorBased.Composite;
using EHonda.Pagination.OffsetBased;
using OffsetBasedComposite = EHonda.Pagination.OffsetBased.Composite;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentAnalysis;

public sealed class ExportExperimentAnalysisCommand : AsyncCommand<ExportExperimentAnalysisSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ILangfusePublicApiClient _langfuseClient;
    private readonly ILogger<ExportExperimentAnalysisCommand> _logger;

    public ExportExperimentAnalysisCommand(
        IAnsiConsole console,
        ILangfusePublicApiClient langfuseClient,
        ILogger<ExportExperimentAnalysisCommand> logger)
    {
        _console = console;
        _langfuseClient = langfuseClient;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ExportExperimentAnalysisSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var runNames = settings.GetParsedRunNames();
            var runContexts = new List<RunContext>();

            _logger.LogInformation(
                "Exporting experiment analysis for dataset {DatasetName} across {RunCount} runs.",
                settings.DatasetName,
                runNames.Count);

            foreach (var runName in runNames)
            {
                var datasetRun = await _langfuseClient.GetDatasetRunAsync(settings.DatasetName, runName, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Dataset run '{runName}' could not be found in dataset '{settings.DatasetName}'.");

                var runMetadata = DeserializeRunMetadata(datasetRun.Metadata, runName);
                var datasetRunItems = await ListAllDatasetRunItemsAsync(datasetRun, cancellationToken);
                EnsureDistinctDatasetItems(datasetRunItems, runName);
                var aggregateScores = await LoadAggregateScoresAsync(datasetRun.Id, runMetadata, cancellationToken);

                runContexts.Add(new RunContext(datasetRun, runMetadata, datasetRunItems, aggregateScores));
            }

            ValidateComparableRuns(runContexts);

            var datasetItemsById = await LoadDatasetItemsAsync(settings.DatasetName, runContexts, cancellationToken);
            var tracesById = await LoadTracesAsync(runContexts, cancellationToken);
            var rows = BuildRows(runContexts, datasetItemsById, tracesById);
            var bundle = BuildBundle(settings.DatasetName, runContexts, rows);

            var outputPath = ResolveOutputPath(settings, bundle.TaskType, settings.DatasetName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(bundle, PreparedExperimentCommandSupport.JsonOptions),
                cancellationToken);

            var summary = new
            {
                settings.DatasetName,
                bundle.TaskType,
                bundle.PrimaryMetricName,
                runCount = bundle.Runs.Count,
                rowCount = bundle.Rows.Count,
                outputPath
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting experiment analysis bundle");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task<Dictionary<string, LangfuseDatasetItem>> LoadDatasetItemsAsync(
        string datasetName,
        IReadOnlyList<RunContext> runContexts,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var requiredDatasetItemIds = runContexts
            .SelectMany(context => context.DatasetRunItems.Select(item => item.DatasetItemId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, LangfuseDatasetItem>(StringComparer.Ordinal);

        if (requiredDatasetItemIds.Count == 0)
        {
            return result;
        }

        await foreach (var datasetItem in EnumerateOffsetPaginatedItemsAsync(
                           (page, ct) => _langfuseClient.ListDatasetItemsAsync(
                               new LangfuseListDatasetItemsRequest(DatasetName: datasetName, Page: page, Limit: pageSize),
                               ct),
                           cancellationToken))
        {
            if (requiredDatasetItemIds.Contains(datasetItem.Id))
            {
                result[datasetItem.Id] = datasetItem;
            }

            if (result.Count == requiredDatasetItemIds.Count)
            {
                break;
            }
        }

        var missingDatasetItemIds = requiredDatasetItemIds
            .Except(result.Keys, StringComparer.Ordinal)
            .ToArray();
        if (missingDatasetItemIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Dataset item(s) could not be loaded from Langfuse: {string.Join(", ", missingDatasetItemIds)}.");
        }

        _logger.LogInformation(
            "Loaded {Count} dataset items for dataset {DatasetName} via paginated dataset listing.",
            result.Count,
            datasetName);

        return result;
    }

    private async Task<Dictionary<string, LangfuseTraceWithDetails>> LoadTracesAsync(
        IReadOnlyList<RunContext> runContexts,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, LangfuseTraceWithDetails>(StringComparer.Ordinal);

        foreach (var runContext in runContexts)
        {
            var expectedTraceIds = runContext.DatasetRunItems
                .Select(item => item.TraceId)
                .ToHashSet(StringComparer.Ordinal);
            var traces = await ListRunTracesAsync(runContext.DatasetRun.Name, expectedTraceIds, cancellationToken);
            var observationsByTraceId = await ListRunObservationsAsync(runContext.DatasetRun.Name, expectedTraceIds, cancellationToken);

            foreach (var traceId in expectedTraceIds)
            {
                if (!traces.TryGetValue(traceId, out var trace))
                {
                    throw new InvalidOperationException($"Trace '{traceId}' could not be loaded from Langfuse.");
                }

                result[traceId] = new LangfuseTraceWithDetails(
                    trace.Id,
                    trace.Name,
                    trace.Metadata,
                    trace.Output,
                    [],
                    observationsByTraceId.TryGetValue(traceId, out var observations) ? observations : [],
                    trace.Tags);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} traces for comparable export using per-run session batching.",
            result.Count);

        return result;
    }

    private async Task<Dictionary<string, LangfuseTraceWithDetails>> ListRunTracesAsync(
        string runName,
        IReadOnlySet<string> expectedTraceIds,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var result = new Dictionary<string, LangfuseTraceWithDetails>(StringComparer.Ordinal);

        if (expectedTraceIds.Count == 0)
        {
            return result;
        }

        await foreach (var trace in EnumerateOffsetPaginatedItemsAsync(
                           (page, ct) => _langfuseClient.ListTracesAsync(
                               new LangfuseListTracesRequest(SessionId: runName, Page: page, Limit: pageSize, Fields: "io"),
                               ct),
                           cancellationToken))
        {
            if (expectedTraceIds.Contains(trace.Id))
            {
                result[trace.Id] = trace;
            }

            if (result.Count == expectedTraceIds.Count)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Loaded {LoadedCount}/{ExpectedCount} trace shells for run {RunName} via sessionId listing.",
            result.Count,
            expectedTraceIds.Count,
            runName);

        return result;
    }

    private async Task<Dictionary<string, IReadOnlyList<LangfuseObservationDetail>>> ListRunObservationsAsync(
        string runName,
        IReadOnlySet<string> expectedTraceIds,
        CancellationToken cancellationToken)
    {
        const int limit = 1000;
        var observationsByTraceId = new Dictionary<string, List<LangfuseObservationDetail>>(StringComparer.Ordinal);

        if (expectedTraceIds.Count == 0)
        {
            return observationsByTraceId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<LangfuseObservationDetail>)pair.Value,
                StringComparer.Ordinal);
        }

        await foreach (var observation in EnumerateCursorPaginatedItemsAsync(
                           (cursor, ct) => _langfuseClient.ListObservationsAsync(
                               new LangfuseListObservationsRequest(SessionId: runName, Limit: limit, Cursor: cursor, Fields: "basic,io"),
                               ct),
                           cancellationToken))
        {
            if (!expectedTraceIds.Contains(observation.TraceId))
            {
                continue;
            }

            if (!observationsByTraceId.TryGetValue(observation.TraceId, out var observations))
            {
                observations = [];
                observationsByTraceId[observation.TraceId] = observations;
            }

            observations.Add(observation);
        }

        _logger.LogInformation(
            "Loaded observations for {TraceCount} traces in run {RunName} via sessionId observation listing.",
            observationsByTraceId.Count,
            runName);

        return observationsByTraceId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<LangfuseObservationDetail>)pair.Value,
            StringComparer.Ordinal);
    }

    private static PreparedExperimentAnalysisBundle BuildBundle(
        string datasetName,
        IReadOnlyList<RunContext> runContexts,
        IReadOnlyList<PreparedExperimentAnalysisRow> rows)
    {
        var taskType = runContexts[0].RunMetadata.TaskType ?? throw new InvalidOperationException("Run metadata missing task type.");
        var primaryMetricName = ResolvePrimaryMetricName(taskType);
        var runSummaries = runContexts.Select(context => new PreparedExperimentAnalysisRun(
                context.DatasetRun.Name,
                context.DatasetRun.Id,
                taskType,
            ResolveRunDisplayName(context.RunMetadata, context.DatasetRun.Name),
                context.RunMetadata.PromptKey,
                context.RunMetadata.SliceKind,
                context.RunMetadata.SliceKey,
                context.RunMetadata.SourcePoolKey,
                context.RunMetadata.SelectedItemIdsHash,
                context.RunMetadata.SelectedItemIdsCount,
                context.RunMetadata.SampleSize,
                context.RunMetadata.EvaluationTimestampPolicyKey,
                context.RunMetadata.EvaluationTime,
                context.RunMetadata.StartedAtUtc,
                context.AggregateScores,
                ResolvePrimaryMetricValue(taskType, context.AggregateScores),
                context.DatasetRunItems.Count,
                context.RunMetadata.RunSubjectKind,
                context.RunMetadata.RunSubjectId,
                context.RunMetadata.RunSubjectDisplayName))
            .OrderBy(run => run.RunName, StringComparer.Ordinal)
            .ToList();

        return new PreparedExperimentAnalysisBundle(
            datasetName,
            taskType,
            primaryMetricName,
            ExperimentArtifactSupport.FormatStartedAtUtc(DateTimeOffset.UtcNow),
            runSummaries,
            rows);
    }

    private static List<PreparedExperimentAnalysisRow> BuildRows(
        IReadOnlyList<RunContext> runContexts,
        IReadOnlyDictionary<string, LangfuseDatasetItem> datasetItemsById,
        IReadOnlyDictionary<string, LangfuseTraceWithDetails> tracesById)
    {
        var rows = new List<PreparedExperimentAnalysisRow>();

        foreach (var context in runContexts.OrderBy(context => context.DatasetRun.Name, StringComparer.Ordinal))
        {
            foreach (var datasetRunItem in context.DatasetRunItems.OrderBy(item => item.DatasetItemId, StringComparer.Ordinal))
            {
                var datasetItem = datasetItemsById[datasetRunItem.DatasetItemId];
                var trace = tracesById[datasetRunItem.TraceId];
                var predictionObservation = SelectPredictionObservation(trace, context.RunMetadata);
                var prediction = ExtractPrediction(trace.Output, predictionObservation?.Output, context.DatasetRun.Name, datasetRunItem.TraceId);
                var expectedOutput = ExtractExpectedOutput(datasetItem.ExpectedOutput, context.DatasetRun.Name, datasetRunItem.DatasetItemId);
                var metadata = ExtractDatasetItemMetadata(datasetItem.Input, datasetItem.Metadata, datasetRunItem.DatasetItemId);
                var kicktippPoints = CalculateKicktippPoints(prediction, expectedOutput);
                var sourceDatasetItemId = ResolveSourceDatasetItemId(
                    context.RunMetadata,
                    datasetRunItem.DatasetItemId,
                    trace.Metadata,
                    context.DatasetRun.Name);

                rows.Add(new PreparedExperimentAnalysisRow(
                    datasetRunItem.DatasetItemId,
                    context.DatasetRun.Id,
                    context.DatasetRun.Name,
                    context.RunMetadata.TaskType ?? throw new InvalidOperationException($"Run '{context.DatasetRun.Name}' is missing task type metadata."),
                    ResolveRunDisplayName(context.RunMetadata, context.DatasetRun.Name),
                    context.RunMetadata.PromptKey,
                    context.RunMetadata.SliceKind,
                    context.RunMetadata.SliceKey,
                    context.RunMetadata.SourcePoolKey,
                    datasetRunItem.DatasetItemId,
                    sourceDatasetItemId,
                    datasetRunItem.TraceId,
                    predictionObservation?.Id,
                    metadata.Matchday,
                    metadata.HomeTeam,
                    metadata.AwayTeam,
                    metadata.StartsAt,
                    metadata.TippSpielId,
                    prediction.HomeGoals,
                    prediction.AwayGoals,
                    expectedOutput.HomeGoals,
                    expectedOutput.AwayGoals,
                    kicktippPoints,
                    prediction.Status,
                    context.RunMetadata.RunSubjectKind,
                    context.RunMetadata.RunSubjectId,
                    context.RunMetadata.RunSubjectDisplayName));
            }
        }

        return rows;
    }

    private static void ValidateComparableRuns(IReadOnlyList<RunContext> runContexts)
    {
        if (runContexts.Count < 2)
        {
            throw new InvalidOperationException("At least two runs are required to export a comparable analysis bundle.");
        }

        var baseline = runContexts[0];
        var baselineTaskType = baseline.RunMetadata.TaskType ?? throw new InvalidOperationException(
            $"Run '{baseline.DatasetRun.Name}' is missing task type metadata.");
        var baselineItemIds = baseline.DatasetRunItems
            .Select(item => item.DatasetItemId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        foreach (var candidate in runContexts.Skip(1))
        {
            var taskType = candidate.RunMetadata.TaskType ?? throw new InvalidOperationException(
                $"Run '{candidate.DatasetRun.Name}' is missing task type metadata.");
            if (!string.Equals(baselineTaskType, taskType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Run '{candidate.DatasetRun.Name}' has task type '{taskType}', but expected '{baselineTaskType}'.");
            }

            if (!string.IsNullOrWhiteSpace(baseline.RunMetadata.SelectedItemIdsHash)
                && !string.IsNullOrWhiteSpace(candidate.RunMetadata.SelectedItemIdsHash)
                && !string.Equals(
                    baseline.RunMetadata.SelectedItemIdsHash,
                    candidate.RunMetadata.SelectedItemIdsHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Run '{candidate.DatasetRun.Name}' has selectedItemIdsHash '{candidate.RunMetadata.SelectedItemIdsHash}', but expected '{baseline.RunMetadata.SelectedItemIdsHash}'.");
            }

            var candidateItemIds = candidate.DatasetRunItems
                .Select(item => item.DatasetItemId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            if (!baselineItemIds.SequenceEqual(candidateItemIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Run '{candidate.DatasetRun.Name}' does not contain the same prepared dataset item set as '{baseline.DatasetRun.Name}'.");
            }
        }
    }

    private static void EnsureDistinctDatasetItems(
        IReadOnlyList<LangfuseDatasetRunItem> datasetRunItems,
        string runName)
    {
        var duplicateItemId = datasetRunItems
            .GroupBy(item => item.DatasetItemId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateItemId is not null)
        {
            throw new InvalidOperationException(
                $"Run '{runName}' contains duplicate dataset item id '{duplicateItemId}', which is not supported for comparable exports.");
        }
    }

    private async Task<IReadOnlyList<LangfuseDatasetRunItem>> ListAllDatasetRunItemsAsync(
        LangfuseDatasetRunWithItems datasetRun,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var items = new List<LangfuseDatasetRunItem>();

        await foreach (var item in EnumerateOffsetPaginatedItemsAsync(
                           (page, ct) => _langfuseClient.ListDatasetRunItemsAsync(
                               datasetRun.DatasetId,
                               datasetRun.Name,
                               page,
                               pageSize,
                               ct),
                           cancellationToken))
        {
            items.Add(item);
        }

        return items;
    }

    private async Task<ExperimentAggregateScores> LoadAggregateScoresAsync(
        string datasetRunId,
        PreparedExperimentRunMetadata runMetadata,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var scores = new List<LangfuseScore>();

        await foreach (var score in EnumerateOffsetPaginatedItemsAsync(
                           (page, ct) => _langfuseClient.ListScoresAsync(
                               new LangfuseListScoresRequest(DatasetRunId: datasetRunId, Page: page, Limit: pageSize),
                               ct),
                           cancellationToken))
        {
            scores.Add(score);
        }

        var total = scores.FirstOrDefault(score => string.Equals(score.Name, "total_kicktipp_points", StringComparison.Ordinal));
        var average = scores.FirstOrDefault(score => string.Equals(score.Name, "avg_kicktipp_points", StringComparison.Ordinal));

        if (total?.Value is null || average?.Value is null)
        {
            var runName = string.IsNullOrWhiteSpace(runMetadata.StartedAtUtc)
                ? datasetRunId
                : runMetadata.StartedAtUtc;
            throw new InvalidOperationException(
                $"Dataset run '{runName}' is missing one or more aggregate Langfuse scores (expected total_kicktipp_points and avg_kicktipp_points)."
            );
        }

        return new ExperimentAggregateScores(total.Value.Value, average.Value.Value);
    }

    private IAsyncEnumerable<TItem> EnumerateOffsetPaginatedItemsAsync<TItem>(
        Func<int, CancellationToken, Task<LangfusePaginatedResponse<TItem>>> pageRetriever,
        CancellationToken cancellationToken)
    {
        var paginationHandler = new OffsetBasedComposite.PaginationHandlerBuilder<LangfusePaginatedResponse<TItem>, TItem>()
            .WithPageRetriever((previousPage, ct) => pageRetriever(GetNextPageNumber(previousPage), ct))
            .WithOffsetStateExtractor(static page => new OffsetState<int>(checked(page.Meta.Page * page.Meta.Limit), page.Meta.TotalItems))
            .WithItemExtractor(static page => page.Data)
            .Build();

        return paginationHandler.GetAllItemsAsync(cancellationToken);
    }

    private IAsyncEnumerable<TItem> EnumerateCursorPaginatedItemsAsync<TItem>(
        Func<string?, CancellationToken, Task<LangfuseCursorPaginatedResponse<TItem>>> pageRetriever,
        CancellationToken cancellationToken)
    {
        var paginationHandler = new CursorBasedComposite.PaginationHandlerBuilder<LangfuseCursorPaginatedResponse<TItem>, TItem>()
            .WithPageRetriever((previousPage, ct) => pageRetriever(previousPage?.Meta.Cursor, ct))
            .WithCursorExtractor(static page => page.Meta.Cursor)
            .WithItemExtractor(static page => page.Data)
            .Build();

        return paginationHandler.GetAllItemsAsync(cancellationToken);
    }

    private static int GetNextPageNumber<TItem>(LangfusePaginatedResponse<TItem>? previousPage)
    {
        return previousPage is null ? 1 : previousPage.Meta.Page + 1;
    }

    private static PreparedExperimentRunMetadata DeserializeRunMetadata(JsonElement metadata, string runName)
    {
        if (!LangfuseJsonUtilities.IsDefined(metadata))
        {
            throw new InvalidOperationException($"Dataset run '{runName}' is missing run metadata.");
        }

        try
        {
            var deserialized = metadata.Deserialize<PreparedExperimentRunMetadata>(PreparedExperimentCommandSupport.JsonOptions);
            return deserialized ?? throw new InvalidOperationException($"Dataset run '{runName}' metadata could not be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Dataset run '{runName}' metadata could not be deserialized.", ex);
        }
    }

    private static PredictionOutput ExtractPrediction(
        JsonElement traceOutput,
        JsonElement? observationOutput,
        string runName,
        string traceId)
    {
        if (TryExtractPredictionOutput(traceOutput, out var prediction))
        {
            return prediction;
        }

        if (observationOutput is JsonElement candidate && TryExtractPredictionOutput(candidate, out prediction))
        {
            return prediction;
        }

        throw new InvalidOperationException(
            $"Run '{runName}' trace '{traceId}' does not expose a parseable prediction payload in trace or observation output.");
    }

    private static ExpectedOutput ExtractExpectedOutput(JsonElement expectedOutput, string runName, string datasetItemId)
    {
        if (expectedOutput.ValueKind == JsonValueKind.Object)
        {
            if (expectedOutput.TryGetProperty("homeGoals", out var homeGoals)
                && expectedOutput.TryGetProperty("awayGoals", out var awayGoals)
                && homeGoals.TryGetInt32(out var home)
                && awayGoals.TryGetInt32(out var away))
            {
                return new ExpectedOutput(home, away);
            }

            if (expectedOutput.TryGetProperty("score", out var score)
                && score.ValueKind == JsonValueKind.String
                && TryParseScoreString(score.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException(
            $"Run '{runName}' dataset item '{datasetItemId}' does not expose a parseable expected scoreline.");
    }

    private static DatasetItemMetadata ExtractDatasetItemMetadata(JsonElement input, JsonElement metadata, string datasetItemId)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Dataset item '{datasetItemId}' metadata is missing or not an object.");
        }

        return new DatasetItemMetadata(
            GetRequiredStringProperty(metadata, "homeTeam", datasetItemId),
            GetRequiredStringProperty(metadata, "awayTeam", datasetItemId),
            GetRequiredIntProperty(metadata, "matchday", datasetItemId),
            GetRequiredStringProperty(input, "startsAt", datasetItemId),
            GetOptionalStringProperty(metadata, "tippSpielId"));
    }

    private static int CalculateKicktippPoints(PredictionOutput prediction, ExpectedOutput expectedOutput)
    {
        if (!string.Equals(prediction.Status, "placed", StringComparison.OrdinalIgnoreCase)
            || prediction.HomeGoals is not int homeGoals
            || prediction.AwayGoals is not int awayGoals)
        {
            return 0;
        }

        return PreparedExperimentSupport.CalculateScores(
            new Prediction(homeGoals, awayGoals),
            expectedOutput.HomeGoals,
            expectedOutput.AwayGoals).KicktippPoints;
    }

    private static LangfuseObservationDetail? SelectPredictionObservation(
        LangfuseTraceWithDetails trace,
        PreparedExperimentRunMetadata runMetadata)
    {
        var observations = trace.Observations ?? [];
        var preferredObservationName = string.IsNullOrWhiteSpace(runMetadata.ObservationName)
            ? string.Equals(runMetadata.TaskType, "community-to-date", StringComparison.OrdinalIgnoreCase)
                ? "community-match-prediction"
                : "predict-match"
            : runMetadata.ObservationName;

        return observations.FirstOrDefault(observation => string.Equals(observation.Name, preferredObservationName, StringComparison.OrdinalIgnoreCase))
               ?? observations.FirstOrDefault(observation => string.Equals(observation.Name, "community-match-prediction", StringComparison.OrdinalIgnoreCase))
               ?? observations.FirstOrDefault(observation => string.Equals(observation.Name, "predict-match", StringComparison.OrdinalIgnoreCase))
               ?? observations.FirstOrDefault(observation => string.Equals(observation.Type, "GENERATION", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveSourceDatasetItemId(
        PreparedExperimentRunMetadata runMetadata,
        string datasetItemId,
        JsonElement traceMetadata,
        string runName)
    {
        if (runMetadata.DatasetItemIdMap.Count > 0)
        {
            var reverseMatch = runMetadata.DatasetItemIdMap
                .FirstOrDefault(pair => string.Equals(pair.Value, datasetItemId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(reverseMatch.Key))
            {
                return reverseMatch.Key;
            }
        }

        if (TryDeriveSourceDatasetItemId(datasetItemId, out var derived))
        {
            return derived;
        }

        var fromTraceMetadata = GetOptionalStringProperty(traceMetadata, "sourceDatasetItemId");
        if (!string.IsNullOrWhiteSpace(fromTraceMetadata))
        {
            return fromTraceMetadata!;
        }

        throw new InvalidOperationException(
            $"Run '{runName}' dataset item '{datasetItemId}' could not be mapped back to a source dataset item id.");
    }

    private static bool TryDeriveSourceDatasetItemId(string datasetItemId, out string sourceDatasetItemId)
    {
        var repeatedMatchIndex = datasetItemId.IndexOf("__repeated-match__", StringComparison.Ordinal);
        if (repeatedMatchIndex >= 0)
        {
            sourceDatasetItemId = datasetItemId[..repeatedMatchIndex];
            return true;
        }

        var sliceIndex = datasetItemId.IndexOf("__slice__", StringComparison.Ordinal);
        if (sliceIndex >= 0)
        {
            sourceDatasetItemId = datasetItemId[..sliceIndex];
            return true;
        }

        sourceDatasetItemId = string.Empty;
        return false;
    }

    private static bool TryExtractPredictionOutput(JsonElement value, out PredictionOutput prediction)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("status", out var statusProperty)
                && statusProperty.ValueKind == JsonValueKind.String)
            {
                var status = NormalizePredictionStatus(statusProperty.GetString());
                var hasHomeGoals = TryGetNullableIntProperty(value, "homeGoals", out var parsedHomeGoals);
                var hasAwayGoals = TryGetNullableIntProperty(value, "awayGoals", out var parsedAwayGoals);

                if (hasHomeGoals && hasAwayGoals)
                {
                    prediction = new PredictionOutput(status, parsedHomeGoals, parsedAwayGoals);
                    return true;
                }

                if (string.Equals(status, "missed", StringComparison.OrdinalIgnoreCase))
                {
                    prediction = new PredictionOutput("missed", null, null);
                    return true;
                }
            }

            if (value.TryGetProperty("homeGoals", out var homeGoals)
                && value.TryGetProperty("awayGoals", out var awayGoals)
                && homeGoals.TryGetInt32(out var home)
                && awayGoals.TryGetInt32(out var away))
            {
                prediction = new PredictionOutput("placed", home, away);
                return true;
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (TryParseScoreString(raw, out var parsedScore))
            {
                prediction = new PredictionOutput("placed", parsedScore.HomeGoals, parsedScore.AwayGoals);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using var document = JsonDocument.Parse(raw);
                    if (TryExtractPredictionOutput(document.RootElement, out prediction))
                    {
                        return true;
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        prediction = default;
        return false;
    }

    private static bool TryGetNullableIntProperty(JsonElement value, string propertyName, out int? result)
    {
        result = null;
        if (!value.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.TryGetInt32(out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseScoreString(string? value, out ExpectedOutput expectedOutput)
    {
        expectedOutput = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var homeGoals)
            || !int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var awayGoals))
        {
            return false;
        }

        expectedOutput = new ExpectedOutput(homeGoals, awayGoals);
        return true;
    }

    private static string NormalizePredictionStatus(string? status)
    {
        return string.Equals(status, "missed", StringComparison.OrdinalIgnoreCase)
            ? "missed"
            : "placed";
    }

    private static string ResolveRunDisplayName(PreparedExperimentRunMetadata runMetadata, string runName)
    {
        if (!string.IsNullOrWhiteSpace(runMetadata.RunSubjectDisplayName))
        {
            return runMetadata.RunSubjectDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.Model))
        {
            return runMetadata.Model;
        }

        throw new InvalidOperationException($"Run '{runName}' is missing comparable display metadata.");
    }

    private static string ResolvePrimaryMetricName(string taskType)
    {
        return string.Equals(taskType, "repeated-match", StringComparison.OrdinalIgnoreCase)
            ? "avg_kicktipp_points"
            : "total_kicktipp_points";
    }

    private static double ResolvePrimaryMetricValue(string taskType, ExperimentAggregateScores aggregateScores)
    {
        return string.Equals(taskType, "repeated-match", StringComparison.OrdinalIgnoreCase)
            ? aggregateScores.AvgKicktippPoints
            : aggregateScores.TotalKicktippPoints;
    }

    private static string ResolveOutputPath(
        ExportExperimentAnalysisSettings settings,
        string taskType,
        string datasetName)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            return Path.GetFullPath(settings.OutputPath);
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ", CultureInfo.InvariantCulture).ToLowerInvariant();
        var datasetSegments = datasetName.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0)
            .ToList();

        return Path.GetFullPath(Path.Combine(
            ["artifacts", "langfuse-experiments", "analysis", taskType, .. datasetSegments, $"comparison-{timestamp}.json"]));
    }

    private static string GetRequiredStringProperty(JsonElement metadata, string propertyName, string datasetItemId)
    {
        if (!metadata.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"Dataset item '{datasetItemId}' metadata is missing '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static int GetRequiredIntProperty(JsonElement metadata, string propertyName, string datasetItemId)
    {
        if (!metadata.TryGetProperty(propertyName, out var property)
            || !property.TryGetInt32(out var value))
        {
            throw new InvalidOperationException($"Dataset item '{datasetItemId}' metadata is missing '{propertyName}'.");
        }

        return value;
    }

    private static string? GetOptionalStringProperty(JsonElement metadata, string propertyName)
    {
        return metadata.ValueKind == JsonValueKind.Object
               && metadata.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()
            : null;
    }

    private sealed record RunContext(
        LangfuseDatasetRunWithItems DatasetRun,
        PreparedExperimentRunMetadata RunMetadata,
        IReadOnlyList<LangfuseDatasetRunItem> DatasetRunItems,
        ExperimentAggregateScores AggregateScores);

    private readonly record struct PredictionOutput(string Status, int? HomeGoals, int? AwayGoals);

    private readonly record struct ExpectedOutput(int HomeGoals, int AwayGoals);

    private readonly record struct DatasetItemMetadata(
        string HomeTeam,
        string AwayTeam,
        int Matchday,
        string StartsAt,
        string? TippSpielId);
}
