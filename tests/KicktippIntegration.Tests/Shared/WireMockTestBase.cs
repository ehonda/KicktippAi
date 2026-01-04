using KicktippIntegration.Tests.Infrastructure;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace KicktippIntegration.Tests.Shared;

/// <summary>
/// Base class for tests using WireMock, providing server lifecycle management,
/// fixture loading, and common stub methods.
/// </summary>
public abstract class WireMockTestBase : IAsyncDisposable
{
    protected WireMockServer Server { get; private set; } = null!;

    [Before(Test)]
    public void StartServer()
    {
        Server = WireMockServer.Start();
    }

    [After(Test)]
    public void StopServer()
    {
        Server?.Stop();
    }

    public ValueTask DisposeAsync()
    {
        Server?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the base URL of the WireMock server.
    /// </summary>
    protected string ServerUrl => Server.Urls[0];

    /// <summary>
    /// Stubs a GET request to return HTML content.
    /// Only matches requests without query parameters.
    /// </summary>
    /// <param name="path">The URL path (e.g., "/test-community/tabellen").</param>
    /// <param name="htmlContent">The HTML content to return.</param>
    protected void StubHtmlResponse(string path, string htmlContent)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(htmlContent));
    }

    /// <summary>
    /// Stubs a GET request with specific query parameters to return HTML content.
    /// Only matches requests that have exactly these query parameters.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="htmlContent">The HTML content to return.</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubHtmlResponseWithParams(string path, string htmlContent, params (string key, string value)[] queryParams)
    {
        var request = Request.Create()
            .WithPath(path)
            .UsingGet();
        
        foreach (var (key, value) in queryParams)
        {
            request = request.WithParam(key, new ExactMatcher(value));
        }
        
        Server
            .Given(request)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(htmlContent));
    }

    /// <summary>
    /// Stubs a GET request with specific query parameters (dictionary) to return HTML content.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="htmlContent">The HTML content to return.</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubHtmlResponseWithParams(string path, string htmlContent, Dictionary<string, string> queryParams)
    {
        var paramTuples = queryParams.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
        StubHtmlResponseWithParams(path, htmlContent, paramTuples);
    }

    /// <summary>
    /// Stubs a POST request and captures the form data for verification.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="responseStatusCode">The HTTP status code to return.</param>
    /// <param name="responseBody">Optional HTML body for the response.</param>
    protected void StubPostResponse(string path, int responseStatusCode = 200, string? responseBody = null)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(responseStatusCode)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(responseBody ?? "<html><body>Success</body></html>"));
    }

    /// <summary>
    /// Stubs a POST request with query parameters.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    /// <param name="responseStatusCode">The HTTP status code to return.</param>
    protected void StubPostResponseWithParams(string path, Dictionary<string, string> queryParams, int responseStatusCode = 200)
    {
        var request = Request.Create()
            .WithPath(path)
            .UsingPost();
        
        foreach (var (key, value) in queryParams)
        {
            request = request.WithParam(key, new ExactMatcher(value));
        }
        
        Server
            .Given(request)
            .RespondWith(Response.Create()
                .WithStatusCode(responseStatusCode)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body>Success</body></html>"));
    }

    /// <summary>
    /// Stubs a GET request to return a specific HTTP status code.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    protected void StubStatusCode(string path, int statusCode)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode));
    }

    /// <summary>
    /// Stubs a GET request to return a 404 Not Found.
    /// </summary>
    /// <param name="path">The URL path.</param>
    protected void StubNotFound(string path)
    {
        StubStatusCode(path, 404);
    }

    /// <summary>
    /// Stubs a GET request with query parameters to return a 404 Not Found.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubNotFoundWithParams(string path, params (string key, string value)[] queryParams)
    {
        var request = Request.Create()
            .WithPath(path)
            .UsingGet();
        
        foreach (var (key, value) in queryParams)
        {
            request = request.WithParam(key, new ExactMatcher(value));
        }
        
        Server
            .Given(request)
            .RespondWith(Response.Create()
                .WithStatusCode(404));
    }

    /// <summary>
    /// Stubs a GET request with query parameters (dictionary) to return a 404 Not Found.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubNotFoundWithParams(string path, Dictionary<string, string> queryParams)
    {
        var paramTuples = queryParams.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
        StubNotFoundWithParams(path, paramTuples);
    }

    /// <summary>
    /// Stubs a GET request using a synthetic fixture file from the Synthetic directory.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="community">The community folder containing the fixture (e.g., "test-community").</param>
    /// <param name="syntheticFixtureName">Name of the synthetic fixture file (without .html extension).</param>
    protected void StubWithSyntheticFixture(string path, string community, string syntheticFixtureName)
    {
        var htmlContent = FixtureLoader.LoadSyntheticFixture(community, syntheticFixtureName);
        StubHtmlResponse(path, htmlContent);
    }

    /// <summary>
    /// Stubs a GET request with query parameters using a synthetic fixture file.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="community">The community folder containing the fixture (e.g., "test-community").</param>
    /// <param name="syntheticFixtureName">Name of the synthetic fixture file (without .html extension).</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubWithSyntheticFixtureAndParams(string path, string community, string syntheticFixtureName, params (string key, string value)[] queryParams)
    {
        var htmlContent = FixtureLoader.LoadSyntheticFixture(community, syntheticFixtureName);
        StubHtmlResponseWithParams(path, htmlContent, queryParams);
    }

    /// <summary>
    /// Stubs a GET request with query parameters (dictionary) using a synthetic fixture file.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="community">The community folder containing the fixture (e.g., "test-community").</param>
    /// <param name="syntheticFixtureName">Name of the synthetic fixture file (without .html extension).</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubWithSyntheticFixtureAndParams(string path, string community, string syntheticFixtureName, Dictionary<string, string> queryParams)
    {
        var paramTuples = queryParams.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
        StubWithSyntheticFixtureAndParams(path, community, syntheticFixtureName, paramTuples);
    }

    /// <summary>
    /// Loads a synthetic fixture file content (public for use in test classes when direct access is needed).
    /// </summary>
    /// <param name="community">The community folder containing the fixture (e.g., "test-community").</param>
    /// <param name="name">Name of the synthetic fixture file (without .html extension).</param>
    /// <returns>The HTML content.</returns>
    protected static string LoadSyntheticFixtureContent(string community, string name)
    {
        return FixtureLoader.LoadSyntheticFixture(community, name);
    }

    /// <summary>
    /// Stubs a GET request using an encrypted real fixture file for a specific community.
    /// Uses the default path pattern "/{community}/{fixtureName}".
    /// Real fixtures contain actual data from Kicktipp pages and should be tested for invariants only.
    /// </summary>
    /// <param name="community">The Kicktipp community (e.g., "ehonda-test-buli").</param>
    /// <param name="fixtureName">Name of the fixture file (without .html.enc extension).</param>
    protected void StubWithRealFixture(string community, string fixtureName)
    {
        StubWithRealFixture($"/{community}/{fixtureName}", community, fixtureName);
    }

    /// <summary>
    /// Stubs a GET request using an encrypted real fixture file for a specific community.
    /// Real fixtures contain actual data from Kicktipp pages and should be tested for invariants only.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="community">The Kicktipp community (e.g., "ehonda-test-buli").</param>
    /// <param name="fixtureName">Name of the fixture file (without .html.enc extension).</param>
    protected void StubWithRealFixture(string path, string community, string fixtureName)
    {
        var htmlContent = FixtureLoader.LoadRealFixture(community, fixtureName);
        StubHtmlResponse(path, htmlContent);
    }

    /// <summary>
    /// Stubs a GET request with query parameters using an encrypted real fixture file for a specific community.
    /// Real fixtures contain actual data from Kicktipp pages and should be tested for invariants only.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="community">The Kicktipp community (e.g., "ehonda-test-buli").</param>
    /// <param name="fixtureName">Name of the fixture file (without .html.enc extension).</param>
    /// <param name="queryParams">Query parameters that must be present.</param>
    protected void StubWithRealFixtureAndParams(
        string path,
        string community,
        string fixtureName,
        params (string key, string value)[] queryParams)
    {
        var htmlContent = FixtureLoader.LoadRealFixture(community, fixtureName);
        StubHtmlResponseWithParams(path, htmlContent, queryParams);
    }

    /// <summary>
    /// Loads a real (encrypted) fixture file content directly.
    /// Useful when you need to access the content without stubbing a request.
    /// </summary>
    /// <param name="community">The Kicktipp community (e.g., "ehonda-test-buli").</param>
    /// <param name="fixtureName">Name of the fixture file (without .html.enc extension).</param>
    /// <returns>The decrypted HTML content.</returns>
    protected static string LoadRealFixtureContent(string community, string fixtureName)
    {
        return FixtureLoader.LoadRealFixture(community, fixtureName);
    }

    /// <summary>
    /// Gets the requests received by the WireMock server for a specific path.
    /// Useful for verifying POST form data.
    /// </summary>
    /// <param name="path">The URL path to filter by.</param>
    /// <returns>The log entries for matching requests.</returns>
    protected IEnumerable<WireMock.Logging.ILogEntry> GetRequestsForPath(string path)
    {
        return Server.LogEntries.Where(e => e.RequestMessage.Path == path);
    }

    /// <summary>
    /// Gets the form data from a POST request body.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <returns>Dictionary of form field names to values.</returns>
    protected static Dictionary<string, string> ParseFormData(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return new Dictionary<string, string>();
        }

        return body.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1]));
    }

    /// <summary>
    /// Gets the form data from a POST request body, supporting multiple values for the same key.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <returns>Dictionary of form field names to list of values.</returns>
    protected static Dictionary<string, List<string>> ParseFormDataMultiValue(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return new Dictionary<string, List<string>>();
        }

        return body.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => Uri.UnescapeDataString(parts[0]))
            .ToDictionary(
                g => g.Key,
                g => g.Select(parts => Uri.UnescapeDataString(parts[1])).ToList());
    }
}
