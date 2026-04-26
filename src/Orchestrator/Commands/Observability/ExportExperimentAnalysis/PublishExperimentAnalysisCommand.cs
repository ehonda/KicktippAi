using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentAnalysis;

public sealed class PublishExperimentAnalysisCommand : AsyncCommand<PublishExperimentAnalysisSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ILangfusePublicApiClient _langfuseClient;
    private readonly ILogger<PublishExperimentAnalysisCommand> _logger;

    public PublishExperimentAnalysisCommand(
        IAnsiConsole console,
        ILangfusePublicApiClient langfuseClient,
        ILogger<PublishExperimentAnalysisCommand> logger)
    {
        _console = console;
        _langfuseClient = langfuseClient;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PublishExperimentAnalysisSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var bundle = await PreparedExperimentCommandSupport.LoadJsonFileAsync<PreparedExperimentAnalysisBundle>(
                settings.InputPath,
                cancellationToken);
            var rowsByRunName = bundle.Rows
                .GroupBy(row => row.RunName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var experimentName = string.IsNullOrWhiteSpace(settings.ExperimentName)
                ? DeriveExperimentName(bundle)
                : settings.ExperimentName.Trim();
            var runResults = new List<object>();

            PreparedExperimentSupport.ReportProgress(
                $"Publishing {bundle.Runs.Count} experiment run alias(es) from '{settings.InputPath}' to Langfuse Experiments Beta metadata.");

            foreach (var run in bundle.Runs.OrderBy(run => run.RunName, StringComparer.Ordinal))
            {
                if (!rowsByRunName.TryGetValue(run.RunName, out var rows) || rows.Count == 0)
                {
                    throw new InvalidOperationException($"Analysis bundle run '{run.RunName}' has no rows to publish.");
                }

                var targetRunName = run.RunName + settings.RunNameSuffix;
                var runDescription = string.IsNullOrWhiteSpace(settings.Description)
                    ? $"Published from analysis bundle '{Path.GetFileName(settings.InputPath)}' for Langfuse Experiments Beta."
                    : settings.Description.Trim();

                if (settings.DryRun)
                {
                    runResults.Add(new
                    {
                        sourceRunName = run.RunName,
                        targetRunName,
                        rowCount = rows.Count,
                        experimentName
                    });
                    continue;
                }

                var existingRun = await _langfuseClient.GetDatasetRunAsync(
                    bundle.DatasetName,
                    targetRunName,
                    cancellationToken);

                if (existingRun is not null)
                {
                    if (!settings.ReplaceRuns)
                    {
                        throw new InvalidOperationException(
                            $"Published run alias '{targetRunName}' already exists in dataset '{bundle.DatasetName}'. Use --replace-runs to recreate aliases.");
                    }

                    await _langfuseClient.DeleteDatasetRunAsync(bundle.DatasetName, targetRunName, cancellationToken);
                }

                var publishedAtUtc = ExperimentArtifactSupport.FormatStartedAtUtc(DateTimeOffset.UtcNow);
                var runMetadata = BuildRunMetadata(bundle, run, rows);
                var metadata = PreparedExperimentSupport.BuildLangfuseExperimentMetadata(
                    runMetadata,
                    experimentName,
                    targetRunName,
                    new Dictionary<string, string?>
                    {
                        ["sourceRunName"] = run.RunName,
                        ["sourceDatasetRunId"] = run.DatasetRunId,
                        ["publishedFromAnalysisBundle"] = Path.GetFileName(settings.InputPath),
                        ["publishedAtUtc"] = publishedAtUtc
                    });
                var createdAt = ParseTimestampOrNull(run.StartedAtUtc);
                string? targetDatasetRunId = null;

                PreparedExperimentSupport.ReportProgress(
                    $"Publishing alias '{targetRunName}' with {rows.Count} item(s).");

                foreach (var row in rows.OrderBy(row => row.DatasetItemId, StringComparer.Ordinal))
                {
                    var datasetRunItem = await _langfuseClient.CreateDatasetRunItemAsync(
                        new LangfuseCreateDatasetRunItemRequest(
                            targetRunName,
                            row.DatasetItemId,
                            row.TraceId,
                            runDescription,
                            metadata,
                            row.ObservationId,
                            createdAt),
                        cancellationToken);

                    targetDatasetRunId ??= datasetRunItem.DatasetRunId;
                }

                if (string.IsNullOrWhiteSpace(targetDatasetRunId))
                {
                    throw new InvalidOperationException($"Publishing run alias '{targetRunName}' did not return a datasetRunId.");
                }

                await PostRunScoresAsync(targetDatasetRunId, run, metadata, cancellationToken);

                runResults.Add(new
                {
                    sourceRunName = run.RunName,
                    targetRunName,
                    datasetRunId = targetDatasetRunId,
                    rowCount = rows.Count,
                    experimentName
                });
            }

            var summary = new
            {
                bundle.DatasetName,
                bundle.TaskType,
                experimentName,
                dryRun = settings.DryRun,
                runCount = runResults.Count,
                runs = runResults
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing experiment analysis bundle");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task PostRunScoresAsync(
        string datasetRunId,
        PreparedExperimentAnalysisRun run,
        JsonElement metadata,
        CancellationToken cancellationToken)
    {
        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "total_kicktipp_points",
                run.AggregateScores.TotalKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Published aggregate score for {run.RowCount} item(s)",
                Id: PreparedExperimentSupport.CreateScoreId("total_kicktipp_points", datasetRunId),
                Metadata: metadata),
            cancellationToken);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "avg_kicktipp_points",
                run.AggregateScores.AvgKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Published aggregate score for {run.RowCount} item(s)",
                Id: PreparedExperimentSupport.CreateScoreId("avg_kicktipp_points", datasetRunId),
                Metadata: metadata),
            cancellationToken);
    }

    private static PreparedExperimentRunMetadata BuildRunMetadata(
        PreparedExperimentAnalysisBundle bundle,
        PreparedExperimentAnalysisRun run,
        IReadOnlyList<PreparedExperimentAnalysisRow> rows)
    {
        var datasetItemIdMap = rows
            .GroupBy(row => row.SourceDatasetItemId, StringComparer.Ordinal)
            .Where(group => group.Select(row => row.DatasetItemId).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().DatasetItemId, StringComparer.Ordinal);

        return new PreparedExperimentRunMetadata
        {
            Runner = "experiment-analysis-publisher",
            TaskType = run.TaskType,
            CommunityContext = TryResolveCommunityContext(bundle.DatasetName),
            Competition = TryResolveCompetition(bundle.DatasetName),
            DatasetName = bundle.DatasetName,
            PromptKey = run.PromptKey,
            ReasoningEffort = run.ReasoningEffort,
            SliceKind = run.SliceKind,
            SliceKey = run.SliceKey,
            SourcePoolKey = run.SourcePoolKey,
            SelectedItemIdsHash = run.SelectedItemIdsHash,
            SelectedItemIdsCount = run.SelectedItemIdsCount,
            SampleSize = run.SampleSize,
            EvaluationTimestampPolicyKey = run.EvaluationTimestampPolicyKey,
            EvaluationTime = run.EvaluationTime,
            StartedAtUtc = run.StartedAtUtc,
            IncludeJustification = false,
            PromptVersion = run.PromptKey,
            SourceDatasetKind = run.TaskType,
            DatasetItemIdMap = datasetItemIdMap,
            Model = run.Model,
            RunSubjectKind = run.RunSubjectKind,
            RunSubjectId = run.RunSubjectId,
            RunSubjectDisplayName = run.RunSubjectDisplayName,
            BatchStrategy = "published-analysis"
        };
    }

    private static string DeriveExperimentName(PreparedExperimentAnalysisBundle bundle)
    {
        var datasetSegments = bundle.DatasetName
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var community = datasetSegments.Length >= 3
            ? datasetSegments[2]
            : "unknown-community";
        var datasetTail = datasetSegments.Length > 0
            ? datasetSegments[^1]
            : "analysis";

        return string.Join(
            "__",
            new[]
            {
                bundle.TaskType,
                community,
                datasetTail
            }.Select(ExperimentArtifactSupport.Slugify));
    }

    private static string? TryResolveCompetition(string datasetName)
    {
        var datasetSegments = datasetName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return datasetSegments.Length >= 2 && string.Equals(datasetSegments[0], "match-predictions", StringComparison.Ordinal)
            ? datasetSegments[1]
            : null;
    }

    private static string? TryResolveCommunityContext(string datasetName)
    {
        var datasetSegments = datasetName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return datasetSegments.Length >= 3 && string.Equals(datasetSegments[0], "match-predictions", StringComparison.Ordinal)
            ? datasetSegments[2]
            : null;
    }

    private static DateTimeOffset? ParseTimestampOrNull(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
