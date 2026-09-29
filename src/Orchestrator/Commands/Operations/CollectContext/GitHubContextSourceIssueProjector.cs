using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>Projects committed health through the durable marker-wide create fence.</summary>
public sealed class GitHubContextSourceIssueProjector : IBundesligaContextSourceIssueProjector
{
    private const string Repository = BundesligaContextSourceIssueFenceIdentity.CanonicalRepository;
    private static readonly Uri ApiOrigin = new("https://api.github.com/");
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly string? _repository;
    private readonly IBundesligaContextSourceIssueFenceRepository _fences;
    private readonly TimeProvider _time;
    private readonly bool _enabled;

    public GitHubContextSourceIssueProjector(HttpClient client, IBundesligaContextSourceIssueFenceRepository fences)
        : this(CreateProductionClient(client), fences, Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")) { }
    internal GitHubContextSourceIssueProjector(HttpClient client, IBundesligaContextSourceIssueFenceRepository fences,
        string? repository, bool testSeam = false, TimeProvider? timeProvider = null, bool enabled = true)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fences = fences ?? throw new ArgumentNullException(nameof(fences)); _repository = repository;
        _time = timeProvider ?? TimeProvider.System; _enabled = enabled;
        if (_client.Timeout <= TimeSpan.Zero || _client.Timeout > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("GITHUB_ISSUE_CLIENT_INVALID");
        if (!testSeam && (_client.BaseAddress != ApiOrigin || _client.DefaultRequestHeaders.Authorization?.Scheme != "Bearer"
            || string.IsNullOrWhiteSpace(_client.DefaultRequestHeaders.Authorization.Parameter)
            || !_client.DefaultRequestHeaders.Accept.Any(value => value.MediaType == "application/vnd.github+json")
            || !_client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version") || _client.DefaultRequestHeaders.UserAgent.Count == 0))
            throw new InvalidOperationException("GITHUB_ISSUE_CLIENT_INVALID");
    }

    public async Task<BundesligaContextSourceIssueProjectionAttempt> ProjectAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(health); health.Validate();
        if (!_enabled || health.Scope != BundesligaContextSourceScope.ProductionLive || health.Source != BundesligaContextSource.ClubElo
            || health.DesiredIssueProjection is not { } desired || _repository != Repository)
            throw new InvalidOperationException("GITHUB_ISSUE_SCOPE_OR_REPOSITORY_INVALID");
        _ = BundesligaContextSourceIssueFenceIdentity.FromHealth(health);
        var body = BundesligaContextSourceHealth.CreateIssueBody(desired.Marker, health.Competition, health.Source, health.Watermark, health.ActiveConditions);
        if (BundesligaContextSourceHealth.HashIssueBody(body) != desired.BodySha256) throw new InvalidDataException("GITHUB_ISSUE_BODY_INVALID");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2), _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = linked.Token;
        var failure = BundesligaContextSourceIssueError.GithubIssueListFailed;
        try
        {
            token.ThrowIfCancellationRequested();
            var admission = await _fences.ReadOrInitializeAsync(health, token);
            if (!admission.CurrentPending || admission.Fence is null) return Failed(failure);
            var fence = admission.Fence;
            var matches = await FindIssuesAsync(desired.Marker, token);
            if (matches.Count > 1) return Failed(failure);
            if (matches.Count == 1) return await BindAndReconcileAsync(health, fence, matches[0], body, token);
            if (fence.State == BundesligaContextSourceIssueFenceState.Bound)
            {
                // Delayed visibility never permits replacement creation. Verify the stored number directly.
                using var requestDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), _time);
                using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, requestDeadline.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{Repository}/issues/{fence.IssueNumber}");
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token);
                if (response.StatusCode != HttpStatusCode.OK) return Failed(failure);
                using var issueJson = await ReadJsonAsync(response, requestCancellation.Token);
                var issue = ParseIssue(issueJson.RootElement);
                if (issue.Number != fence.IssueNumber || !HasExactMarkerLine(issue.Body, desired.Marker)) return Failed(failure);
                return await ReconcileAsync(health, issue, body, token);
            }
            if (desired.DesiredState == BundesligaContextSourceIssueState.Closed)
                return fence.State == BundesligaContextSourceIssueFenceState.Ready
                    ? BundesligaContextSourceIssueProjectionAttempt.Synchronized(desired.BodySha256) : Failed(failure);
            failure = BundesligaContextSourceIssueError.GithubIssueCreateFailed;
            if (fence.State != BundesligaContextSourceIssueFenceState.Ready) return Failed(failure);
            var now = _time.GetUtcNow(); now = new(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
            var attempt = new BundesligaContextSourceIssueFenceAttempt(BundesligaContextSourceContract.NewClaimToken(), desired.BodySha256,
                health.Watermark, desired.DesiredState, now);
            var grant = await _fences.TryArmCreateAsync(health, fence.Revision, attempt, token);
            if (grant is null || !grant.TryConsume()) return Failed(failure);
            // No handler retries POST: production uses a private redirect-disabled HttpClientHandler.
            token.ThrowIfCancellationRequested();
            using var createDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), _time);
            using var createCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, createDeadline.Token);
            using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"repos/{Repository}/issues") { Content = JsonContent.Create(new { title = desired.Title, body }) };
            using var createResponse = await _client.SendAsync(createRequest, HttpCompletionOption.ResponseHeadersRead, createCancellation.Token);
            if (createResponse.StatusCode != HttpStatusCode.Created) return Failed(failure);
            using var createdJson = await ReadJsonAsync(createResponse, createCancellation.Token);
            var created = ParseIssue(createdJson.RootElement);
            if (!HasExactMarkerLine(created.Body, desired.Marker) || !IsDesired(created, desired, body)) return Failed(failure);
            var bound = await _fences.BindObservedIssueAsync(grant.Fence, created.Number, token);
            return bound.Disposition == BundesligaContextSourceIssueFenceBindDisposition.Bound
                ? BundesligaContextSourceIssueProjectionAttempt.Synchronized(desired.BodySha256) : Failed(failure);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or IOException or JsonException or InvalidDataException)
        { return Failed(failure); }
    }

    private async Task<BundesligaContextSourceIssueProjectionAttempt> BindAndReconcileAsync(BundesligaContextSourceHealth health,
        BundesligaContextSourceIssueFence fence, GitHubIssue issue, string body, CancellationToken token)
    {
        var bound = await _fences.BindObservedIssueAsync(fence, issue.Number, token);
        if (bound.Disposition != BundesligaContextSourceIssueFenceBindDisposition.Bound) return Failed(BundesligaContextSourceIssueError.GithubIssueListFailed);
        return await ReconcileAsync(health, issue, body, token);
    }
    private async Task<BundesligaContextSourceIssueProjectionAttempt> ReconcileAsync(BundesligaContextSourceHealth health, GitHubIssue issue, string body, CancellationToken token)
    {
        var desired = health.DesiredIssueProjection!;
        if (IsDesired(issue, desired, body)) return BundesligaContextSourceIssueProjectionAttempt.Synchronized(desired.BodySha256);
        var failure = desired.DesiredState == BundesligaContextSourceIssueState.Closed
            ? BundesligaContextSourceIssueError.GithubIssueCloseFailed : BundesligaContextSourceIssueError.GithubIssueUpdateFailed;
        try
        {
            // This transaction is PATCH admission; a later watermark cannot revoke an admitted request.
            var admission = await _fences.ReadOrInitializeAsync(health, token);
            if (!admission.CurrentPending || admission.Fence?.State != BundesligaContextSourceIssueFenceState.Bound
                || admission.Fence.IssueNumber != issue.Number || !HasExactMarkerLine(issue.Body, desired.Marker)) return Failed(failure);
            using var requestDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), _time);
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, requestDeadline.Token);
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"repos/{Repository}/issues/{issue.Number}")
                { Content = JsonContent.Create(new { title = desired.Title, body, state = desired.DesiredState == BundesligaContextSourceIssueState.Open ? "open" : "closed" }) };
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token);
            if (response.StatusCode != HttpStatusCode.OK) return Failed(failure, Hash(issue.Body));
            using var json = await ReadJsonAsync(response, requestCancellation.Token); var actual = ParseIssue(json.RootElement);
            return actual.Number == issue.Number && IsDesired(actual, desired, body)
                ? BundesligaContextSourceIssueProjectionAttempt.Synchronized(desired.BodySha256) : Failed(failure, Hash(issue.Body));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException or OperationCanceledException)
        { return Failed(failure, Hash(issue.Body)); }
    }
    private async Task<IReadOnlyList<GitHubIssue>> FindIssuesAsync(string marker, CancellationToken token)
    {
        var result = new List<GitHubIssue>(); var seen = new HashSet<long>();
        for (var page = 1; page <= 100; page++)
        {
            using var requestDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), _time);
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, requestDeadline.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{Repository}/issues?state=all&per_page=100&page={page}");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token);
            if (response.StatusCode != HttpStatusCode.OK) throw new IOException("GITHUB_ISSUE_LIST_FAILED");
            using var json = await ReadJsonAsync(response, requestCancellation.Token);
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > 100) throw new InvalidDataException("GITHUB_ISSUE_LIST_INVALID");
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("GITHUB_ISSUE_LIST_INVALID");
                if (item.TryGetProperty("pull_request", out _)) continue;
                var issue = ParseIssue(item); if (!seen.Add(issue.Number)) throw new InvalidDataException("GITHUB_ISSUE_LIST_INCOMPLETE");
                if (HasExactMarkerLine(issue.Body, marker)) result.Add(issue);
            }
            if (json.RootElement.GetArrayLength() < 100)
            {
                // A short page declaring a successor is incomplete evidence, never authoritative absence.
                if (response.Headers.TryGetValues("Link", out var links) && links.Any(link => link.Contains("rel=\"next\"", StringComparison.Ordinal)))
                    throw new InvalidDataException("GITHUB_ISSUE_LIST_INCOMPLETE");
                return result;
            }
        }
        throw new IOException("GITHUB_ISSUE_LIST_UNBOUNDED");
    }
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes) throw new InvalidDataException("GITHUB_ISSUE_RESPONSE_UNBOUNDED");
        await using var stream = await response.Content.ReadAsStreamAsync(token); using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, token); if (count == 0) break;
            if (bytes.Length + count > MaxResponseBytes) throw new InvalidDataException("GITHUB_ISSUE_RESPONSE_UNBOUNDED");
            bytes.Write(buffer, 0, count);
        }
        token.ThrowIfCancellationRequested(); return JsonDocument.Parse(bytes.ToArray());
    }
    private static HttpClient CreateProductionClient(HttpClient configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        var bearer = configured.DefaultRequestHeaders.Authorization;
        if (configured.BaseAddress != ApiOrigin || bearer?.Scheme != "Bearer" || string.IsNullOrWhiteSpace(bearer.Parameter))
            throw new InvalidOperationException("GITHUB_ISSUE_CLIENT_INVALID");
        var timeout = configured.Timeout <= TimeSpan.Zero || configured.Timeout > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : configured.Timeout;
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = ApiOrigin, Timeout = timeout };
        client.DefaultRequestHeaders.Authorization = bearer;
        foreach (var accept in configured.DefaultRequestHeaders.Accept) client.DefaultRequestHeaders.Accept.Add(accept);
        foreach (var agent in configured.DefaultRequestHeaders.UserAgent) client.DefaultRequestHeaders.UserAgent.Add(agent);
        if (configured.DefaultRequestHeaders.TryGetValues("X-GitHub-Api-Version", out var versions)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", versions);
        return client;
    }
    private static BundesligaContextSourceIssueProjectionAttempt Failed(BundesligaContextSourceIssueError error, string? hash = null) => BundesligaContextSourceIssueProjectionAttempt.Failed(error, hash);
    private static bool IsDesired(GitHubIssue issue, BundesligaContextSourceIssueProjection desired, string body) => issue.Title == desired.Title && issue.Body == body && issue.State == (desired.DesiredState == BundesligaContextSourceIssueState.Open ? "open" : "closed");
    private static bool HasExactMarkerLine(string? body, string marker) => body is not null && body.Split('\n').Any(line => string.Equals(line.TrimEnd('\r'), marker, StringComparison.Ordinal));
    private static string? Hash(string? body) => body is null ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    private static GitHubIssue ParseIssue(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || item.TryGetProperty("pull_request", out _) || !item.TryGetProperty("number", out var number)
            || !number.TryGetInt64(out var parsedNumber) || parsedNumber <= 0 || !item.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String
            || !item.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String || state.GetString() is not ("open" or "closed")
            || !item.TryGetProperty("body", out var body) || body.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new InvalidDataException("GITHUB_ISSUE_RESPONSE_INVALID");
        return new(parsedNumber, title.GetString()!, body.GetString(), state.GetString()!);
    }
    private sealed record GitHubIssue(long Number, string Title, string? Body, string State);
}
