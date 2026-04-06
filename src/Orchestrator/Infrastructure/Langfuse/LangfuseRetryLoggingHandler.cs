using System.Net;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Langfuse;

internal sealed class LangfuseRetryLoggingHandler : DelegatingHandler
{
    private readonly ILogger<LangfuseRetryLoggingHandler> _logger;

    public LangfuseRetryLoggingHandler(ILogger<LangfuseRetryLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (ShouldLogPotentialRetry(response.StatusCode))
        {
            var retryMetadata = LangfuseRetryAfterUtility.GetRetryAfterMetadata(response.Headers);
            _logger.LogWarning(
                "Langfuse request {Method} {Path} returned {StatusCode} ({ReasonPhrase}) and will be handled by the standard resilience pipeline if retryable. Retry-After: {RetryAfterHeaderValue}; RetryDelay: {RetryAfterDelay}; RetryAtUtc: {RetryAfterAtUtc}",
                request.Method.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                (int)response.StatusCode,
                response.ReasonPhrase,
                retryMetadata.RetryAfterHeaderValue,
                retryMetadata.RetryAfterDelay,
                retryMetadata.RetryAfterAtUtc);
        }

        return response;
    }

    private static bool ShouldLogPotentialRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }
}
