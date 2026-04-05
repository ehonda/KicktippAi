using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Langfuse;

public sealed class LangfusePublicApiClient : ILangfusePublicApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LangfusePublicApiClient> _logger;

    public LangfusePublicApiClient(HttpClient httpClient, ILogger<LangfusePublicApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<LangfuseDataset?> GetDatasetAsync(string datasetName, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseDataset>(HttpMethod.Get, $"datasets/{EncodePathSegment(datasetName)}", null, true, cancellationToken);
    }

    public Task<LangfuseDataset> CreateDatasetAsync(LangfuseCreateDatasetRequest request, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseDataset>(HttpMethod.Post, "datasets", request, false, cancellationToken)!;
    }

    public Task<LangfuseDatasetItem?> GetDatasetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseDatasetItem>(HttpMethod.Get, $"dataset-items/{EncodePathSegment(id)}", null, true, cancellationToken);
    }

    public Task<LangfuseDatasetItem> CreateDatasetItemAsync(LangfuseCreateDatasetItemRequest request, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseDatasetItem>(HttpMethod.Post, "dataset-items", request, false, cancellationToken)!;
    }

    public Task<LangfuseDatasetRunItem> CreateDatasetRunItemAsync(LangfuseCreateDatasetRunItemRequest request, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseDatasetRunItem>(HttpMethod.Post, "dataset-run-items", request, false, cancellationToken)!;
    }

    public Task<LangfuseDatasetRunWithItems?> GetDatasetRunAsync(string datasetName, string runName, CancellationToken cancellationToken = default)
    {
        var path = $"datasets/{EncodePathSegment(datasetName)}/runs/{EncodePathSegment(runName)}";
        return SendForJsonAsync<LangfuseDatasetRunWithItems>(HttpMethod.Get, path, null, true, cancellationToken);
    }

    public Task<LangfuseTraceWithDetails?> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseTraceWithDetails>(
            HttpMethod.Get,
            $"traces/{EncodePathSegment(traceId)}",
            null,
            true,
            cancellationToken);
    }

    public async Task<bool> DeleteDatasetRunAsync(string datasetName, string runName, CancellationToken cancellationToken = default)
    {
        var path = $"datasets/{EncodePathSegment(datasetName)}/runs/{EncodePathSegment(runName)}";
        using var response = await _httpClient.DeleteAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public Task<LangfusePaginatedResponse<LangfuseDatasetRunItem>> ListDatasetRunItemsAsync(
        string datasetId,
        string runName,
        int page = 1,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var path = $"dataset-run-items?datasetId={Uri.EscapeDataString(datasetId)}&runName={Uri.EscapeDataString(runName)}&page={page}&limit={limit}";
        return SendForJsonAsync<LangfusePaginatedResponse<LangfuseDatasetRunItem>>(HttpMethod.Get, path, null, false, cancellationToken)!;
    }

    public Task<LangfusePaginatedResponse<LangfuseScore>> ListScoresAsync(
        LangfuseListScoresRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queryParameters = new List<KeyValuePair<string, string?>>
        {
            new("page", request.Page.ToString()),
            new("limit", request.Limit.ToString()),
            new("fields", request.Fields),
            new("name", request.Name),
            new("datasetRunId", request.DatasetRunId),
            new("traceId", request.TraceId)
        };

        var path = BuildQueryString("v2/scores", queryParameters);
        return SendForJsonAsync<LangfusePaginatedResponse<LangfuseScore>>(HttpMethod.Get, path, null, false, cancellationToken)!;
    }

    public Task<LangfuseCreateScoreResponse> CreateScoreAsync(LangfuseCreateScoreRequest request, CancellationToken cancellationToken = default)
    {
        return SendForJsonAsync<LangfuseCreateScoreResponse>(HttpMethod.Post, "scores", request, false, cancellationToken)!;
    }

    private async Task<T?> SendForJsonAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        bool returnNullOnNotFound,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (returnNullOnNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            if (result is null)
            {
                throw new LangfusePublicApiException(response.StatusCode, relativePath, "Langfuse returned an empty JSON body.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(ex, "Failed to deserialize Langfuse response from {Path}: {Body}", relativePath, responseBody);
            throw new LangfusePublicApiException(response.StatusCode, relativePath, responseBody, ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new LangfusePublicApiException(response.StatusCode, response.RequestMessage?.RequestUri?.ToString() ?? string.Empty, body);
    }

    private static string EncodePathSegment(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string BuildQueryString(string relativePath, IEnumerable<KeyValuePair<string, string?>> queryParameters)
    {
        var parts = queryParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}")
            .ToArray();

        return parts.Length == 0
            ? relativePath
            : $"{relativePath}?{string.Join("&", parts)}";
    }
}

public sealed class LangfusePublicApiException : Exception
{
    public LangfusePublicApiException(HttpStatusCode statusCode, string endpoint, string responseBody, Exception? innerException = null)
        : base($"Langfuse API request failed with status {(int)statusCode} ({statusCode}) for '{endpoint}'. Response: {responseBody}", innerException)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string Endpoint { get; }

    public string ResponseBody { get; }
}