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
    private readonly ChampionsLeagueBonusRoute.ExactRouteUris _route;

    public ChampionsLeagueBonusStrictTransport(
        Uri origin,
        CookieContainer cookieContainer,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(cookieContainer);
        Origin = ValidateOrigin(origin);
        _route = ChampionsLeagueBonusRoute.CreateExactUrisForValidatedOrigin(Origin);
        PageUri = _route.Page;
        TippabgabeActionUri = _route.TippabgabeAction;
        TippabgabeFormActionUri = _route.TippabgabeFormAction;

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

    public Uri TippabgabeActionUri { get; }

    public Uri TippabgabeFormActionUri { get; }

    public Uri PageUri { get; }

    internal ChampionsLeagueBonusRoute.ExactRouteUris Route => _route;

    public async Task<HttpResponseMessage> PostAndResolveResponseOnceAsync(
        Uri selectedAction,
        IReadOnlyList<KeyValuePair<string, string>> formValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedAction);
        ArgumentNullException.ThrowIfNull(formValues);
        var canonicalAction = ChampionsLeagueBonusRoute.CanonicalizeAction(selectedAction, _route);
        var exactSelectedAction = ChampionsLeagueBonusRoute.MapCanonicalActionToRoute(canonicalAction, _route);
        using var request = new HttpRequestMessage(HttpMethod.Post, exactSelectedAction)
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
            ValidateResponseRequestUri(response, exactSelectedAction, "POST");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response;
            }

            if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
            {
                var location = response.Headers.Location;
                if (location is null
                    || !Uri.TryCreate(exactSelectedAction, location, out var resolved)
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

    internal Uri GetActionForCanonicalMember(Uri canonicalAction) =>
        ChampionsLeagueBonusRoute.MapCanonicalActionToRoute(canonicalAction, _route);

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
        ChampionsLeagueBonusRoute.HasExactUriComponents(actual, expected);
}
