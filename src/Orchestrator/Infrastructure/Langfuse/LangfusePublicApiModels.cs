using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchestrator.Infrastructure.Langfuse;

public sealed record LangfuseCreateDatasetRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("metadata")] object? Metadata = null,
    [property: JsonPropertyName("inputSchema")] JsonElement? InputSchema = null,
    [property: JsonPropertyName("expectedOutputSchema")] JsonElement? ExpectedOutputSchema = null);

public sealed record LangfuseCreateDatasetItemRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("datasetName")] string? DatasetName = null,
    [property: JsonPropertyName("input")] object? Input = null,
    [property: JsonPropertyName("expectedOutput")] object? ExpectedOutput = null,
    [property: JsonPropertyName("metadata")] object? Metadata = null,
    [property: JsonPropertyName("status")] string? Status = null);

public sealed record LangfuseCreateDatasetRunItemRequest(
    [property: JsonPropertyName("runName")] string RunName,
    [property: JsonPropertyName("datasetItemId")] string DatasetItemId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("runDescription")] string? RunDescription = null,
    [property: JsonPropertyName("metadata")] object? Metadata = null,
    [property: JsonPropertyName("observationId")] string? ObservationId = null);

public sealed record LangfuseCreateScoreRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("traceId")] string? TraceId = null,
    [property: JsonPropertyName("observationId")] string? ObservationId = null,
    [property: JsonPropertyName("datasetRunId")] string? DatasetRunId = null,
    [property: JsonPropertyName("dataType")] string? DataType = null,
    [property: JsonPropertyName("comment")] string? Comment = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("metadata")] object? Metadata = null,
    [property: JsonPropertyName("configId")] string? ConfigId = null,
    [property: JsonPropertyName("environment")] string? Environment = null);

public sealed record LangfuseListScoresRequest(
    string? Name = null,
    string? DatasetRunId = null,
    string? SessionId = null,
    string? Filter = null,
    string? TraceId = null,
    int Page = 1,
    int Limit = 100,
    string? Fields = "score");

public sealed record LangfuseListDatasetItemsRequest(
    string? DatasetName = null,
    string? Version = null,
    int Page = 1,
    int Limit = 100);

public sealed record LangfuseListTracesRequest(
    string? SessionId = null,
    int Page = 1,
    int Limit = 100,
    string? Fields = null);

public sealed record LangfuseListObservationsRequest(
    string? SessionId = null,
    int Limit = 1000,
    string? Cursor = null,
    string? Fields = null);

public sealed record LangfuseCreateScoreResponse(
    [property: JsonPropertyName("id")] string Id);

public sealed record LangfuseDataset(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("metadata")] JsonElement Metadata,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema,
    [property: JsonPropertyName("expectedOutputSchema")] JsonElement ExpectedOutputSchema);

public sealed record LangfuseDatasetItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("datasetId")] string DatasetId,
    [property: JsonPropertyName("datasetName")] string? DatasetName,
    [property: JsonPropertyName("input")] JsonElement Input,
    [property: JsonPropertyName("expectedOutput")] JsonElement ExpectedOutput,
    [property: JsonPropertyName("metadata")] JsonElement Metadata,
    [property: JsonPropertyName("status")] string? Status);

public sealed record LangfuseDatasetRunItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("datasetRunId")] string DatasetRunId,
    [property: JsonPropertyName("datasetRunName")] string DatasetRunName,
    [property: JsonPropertyName("datasetItemId")] string DatasetItemId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("observationId")] string? ObservationId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);

public sealed record LangfuseDatasetRunWithItems(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("datasetId")] string DatasetId,
    [property: JsonPropertyName("datasetName")] string DatasetName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("metadata")] JsonElement Metadata,
    [property: JsonPropertyName("datasetRunItems")] IReadOnlyList<LangfuseDatasetRunItem> DatasetRunItems);

public sealed record LangfuseScore(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] double? Value,
    [property: JsonPropertyName("traceId")] string? TraceId,
    [property: JsonPropertyName("observationId")] string? ObservationId,
    [property: JsonPropertyName("datasetRunId")] string? DatasetRunId,
    [property: JsonPropertyName("dataType")] string? DataType,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("metadata")] JsonElement Metadata,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt);

public sealed record LangfuseObservationDetail(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("output")] JsonElement Output,
    [property: JsonPropertyName("metadata")] JsonElement Metadata);

public sealed record LangfuseTraceWithDetails(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("metadata")] JsonElement Metadata,
    [property: JsonPropertyName("output")] JsonElement Output,
    [property: JsonPropertyName("scores")] IReadOnlyList<LangfuseScore>? Scores,
    [property: JsonPropertyName("observations")] IReadOnlyList<LangfuseObservationDetail>? Observations,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags);

public sealed record LangfusePaginationMeta(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("totalItems")] int TotalItems,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record LangfusePaginatedResponse<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data,
    [property: JsonPropertyName("meta")] LangfusePaginationMeta Meta);

public sealed record LangfuseCursorPaginationMeta(
    [property: JsonPropertyName("cursor")] string? Cursor);

public sealed record LangfuseCursorPaginatedResponse<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data,
    [property: JsonPropertyName("meta")] LangfuseCursorPaginationMeta Meta);
