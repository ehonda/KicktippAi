using System.Threading;

namespace Orchestrator.Infrastructure.Langfuse;

/// <summary>
/// Narrow wrapper over the Langfuse public API endpoints used by the experiment runner.
/// </summary>
public interface ILangfusePublicApiClient
{
    Task<LangfuseDataset?> GetDatasetAsync(string datasetName, CancellationToken cancellationToken = default);

    Task<LangfuseDataset> CreateDatasetAsync(LangfuseCreateDatasetRequest request, CancellationToken cancellationToken = default);

    Task<LangfuseDatasetItem?> GetDatasetItemAsync(string id, CancellationToken cancellationToken = default);

    Task<LangfuseDatasetItem> CreateDatasetItemAsync(LangfuseCreateDatasetItemRequest request, CancellationToken cancellationToken = default);

    Task<LangfuseDatasetRunItem> CreateDatasetRunItemAsync(LangfuseCreateDatasetRunItemRequest request, CancellationToken cancellationToken = default);

    Task<LangfuseDatasetRunWithItems?> GetDatasetRunAsync(string datasetName, string runName, CancellationToken cancellationToken = default);

    Task<LangfuseTraceWithDetails?> GetTraceAsync(string traceId, CancellationToken cancellationToken = default);

    Task<bool> DeleteDatasetRunAsync(string datasetName, string runName, CancellationToken cancellationToken = default);

    Task<LangfusePaginatedResponse<LangfuseDatasetRunItem>> ListDatasetRunItemsAsync(
        string datasetId,
        string runName,
        int page = 1,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<LangfusePaginatedResponse<LangfuseScore>> ListScoresAsync(
        LangfuseListScoresRequest request,
        CancellationToken cancellationToken = default);

    Task<LangfuseCreateScoreResponse> CreateScoreAsync(LangfuseCreateScoreRequest request, CancellationToken cancellationToken = default);
}