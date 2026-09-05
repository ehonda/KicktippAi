using System.Net;

namespace KicktippIntegration.Transport;

/// <summary>
/// Single-attempt transport for the frozen Schadensfresse Champions-League bonus mutation.
/// </summary>
public sealed class ChampionsLeagueBonusStrictTransport : IDisposable
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

    private readonly HttpClient _httpClient;

    public ChampionsLeagueBonusStrictTransport(
        Uri origin,
        CookieContainer cookieContainer,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(cookieContainer);
        Origin = ValidateOrigin(origin);
        var route = ChampionsLeagueBonusRoute.CreateExactUrisForValidatedOrigin(Origin);
        ActionUri = route.Action;
        PageUri = route.Page;

        var primaryHandler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = false
        };
        _httpClient = new HttpClient(primaryHandler)
        {
            BaseAddress = Origin,
            Timeout = timeout ?? TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", BrowserUserAgent);
    }

    public Uri Origin { get; }

    public Uri ActionUri { get; }

    public Uri PageUri { get; }

    public async Task<HttpResponseMessage> PostAndResolveResponseOnceAsync(
        IReadOnlyList<KeyValuePair<string, string>> formValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(formValues);
        using var request = new HttpRequestMessage(HttpMethod.Post, ActionUri)
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The strict Champions-League POST outcome is unknown; the mutation will not be retried automatically.",
                exception);
        }

        try
        {
            ValidateResponseRequestUri(response, ActionUri, "POST");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response;
            }

            if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
            {
                var location = response.Headers.Location;
                if (location is null
                    || !Uri.TryCreate(ActionUri, location, out var resolved)
                    || !HasExactComponents(resolved, PageUri))
                {
                    throw new InvalidDataException(
                        "The strict Champions-League POST redirect does not target the exact bonus page.");
                }

                response.Dispose();
                return await SendGetOnceAsync(PageUri, "POST response validation", cancellationToken);
            }

            throw new HttpRequestException(
                $"The strict Champions-League POST returned forbidden status {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public Task<HttpResponseMessage> GetOnceAsync(CancellationToken cancellationToken = default) =>
        SendGetOnceAsync(PageUri, "final verification", cancellationToken);

    public void Dispose() => _httpClient.Dispose();

    private async Task<HttpResponseMessage> SendGetOnceAsync(
        Uri target,
        string purpose,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        try
        {
            ValidateResponseRequestUri(response, target, "GET");
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException(
                    $"The strict Champions-League {purpose} GET returned forbidden status {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
            }

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static Uri ValidateOrigin(Uri origin)
    {
        if (!origin.IsAbsoluteUri
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
               && !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.IsNullOrEmpty(origin.Host)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new ArgumentException("The strict Champions-League transport origin must be an absolute HTTP(S) authority only.", nameof(origin));
        }

        return new Uri(origin.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
    }

    private static void ValidateResponseRequestUri(HttpResponseMessage response, Uri expected, string method)
    {
        if (response.RequestMessage?.Method.Method != method
            || response.RequestMessage.RequestUri is not { } actual
            || !HasExactComponents(actual, expected))
        {
            throw new InvalidDataException(
                $"The strict Champions-League {method} response is not bound to its exact request URI.");
        }
    }

    private static bool HasExactComponents(Uri actual, Uri expected) =>
        string.Equals(actual.Scheme, expected.Scheme, StringComparison.Ordinal)
        && string.Equals(actual.Host, expected.Host, StringComparison.Ordinal)
        && actual.Port == expected.Port
        && string.Equals(actual.UserInfo, expected.UserInfo, StringComparison.Ordinal)
        && string.Equals(actual.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal)
        && string.Equals(actual.Query, expected.Query, StringComparison.Ordinal)
        && string.Equals(actual.Fragment, expected.Fragment, StringComparison.Ordinal);
}
