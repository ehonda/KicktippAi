using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class GitHubContextSourceArtifactStoreTests
{
    private static readonly GitHubArtifactRuntime Runtime = new("ehonda/KicktippAi", 123, 456);
    private static readonly string Name = Runtime.ExpectedArtifactName;
    [Test]
    public async Task Probe_paginates_exact_run_and_never_forwards_bearer_to_signed_archive()
    {
        var apiHandler = new PagedApiHandler(Name); var archiveHandler = new ArchiveHandler(Archive(("manifest.json", "{}"), ("bundle.sha256", new string('a', 64) + "\n")));
        using var api = Client(apiHandler, "https://api.github.test/"); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-secret"); using var archive = Client(archiveHandler, "https://signed.example/");
        var probe = await Store(api, archive).ProbeAsync(Name);
        await Assert.That(probe.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present); await Assert.That(apiHandler.Pages).IsEquivalentTo([1, 2]); await Assert.That(archiveHandler.Authorization).IsNull();
    }
    [Test]
    public async Task Probe_fails_closed_for_expired_ambiguous_and_hostile_archives()
    {
        using var archive = Client(new ArchiveHandler([]), "https://signed.example/"); using var expired = Client(new ListedHandler(1, "[{\"id\":4,\"name\":\"" + Name + "\",\"expired\":true,\"workflow_run\":{\"id\":456}}]"), "https://api.github.test/");
        await Assert.That((await Store(expired, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
        using var ambiguous = Client(new ListedHandler(2, "[{\"id\":4,\"name\":\"" + Name + "\",\"expired\":false,\"workflow_run\":{\"id\":456}},{\"id\":5,\"name\":\"" + Name + "\",\"expired\":false,\"workflow_run\":{\"id\":456}}]"), "https://api.github.test/");
        await Assert.That((await Store(ambiguous, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
        using var hostileApi = Client(new SingleArtifactApi(Name), "https://api.github.test/"); using var hostile = Client(new ArchiveHandler(Archive(("../manifest.json", "{}"), ("bundle.sha256", "x"))), "https://signed.example/");
        await Assert.That((await Store(hostileApi, hostile).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
    }
    [Test]
    public async Task Probe_returns_indeterminate_for_failed_or_partial_pagination()
    {
        using var archive = Client(new ArchiveHandler([]), "https://signed.example/"); using var failed = Client(new StatusHandler(HttpStatusCode.Forbidden), "https://api.github.test/");
        await Assert.That((await Store(failed, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        using var partial = Client(new ListedHandler(2, "[{\"id\":4,\"name\":\"other\",\"expired\":false,\"workflow_run\":{\"id\":456}}]"), "https://api.github.test/");
        await Assert.That((await Store(partial, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
    }
    [Test]
    public async Task Oversized_listing_is_indeterminate_and_caller_cancellation_propagates()
    {
        using var archive = Client(new ArchiveHandler([]), "https://signed.example/"); using var oversized = Client(new OversizedListingHandler(), "https://api.github.test/");
        await Assert.That((await Store(oversized, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        using var cancelling = Client(new CancellationHandler(), "https://api.github.test/"); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.That(() => Store(cancelling, archive).ProbeAsync(Name, source.Token)).Throws<OperationCanceledException>();
    }    [Test]
    public async Task Signed_rate_limit_and_server_failure_are_indeterminate()
    {
        foreach (var status in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
        {
            using var api = Client(new SingleArtifactApi(Name), "https://api.github.test/"); using var archive = Client(new ArchiveStatusHandler(status), "https://signed.example/");
            await Assert.That((await Store(api, archive).ProbeAsync(Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        }
    }    [Test]
    public async Task Upload_binds_exact_runtime_authority_and_frozen_options_before_tool_execution()
    {
        var tool = new RecordingTool(); using var api = Client(new ListedHandler(0, "[]"), "https://api.github.test/"); using var archive = Client(new ArchiveHandler([]), "https://signed.example/"); var store = new GitHubContextSourceArtifactStore(tool, api, archive, Runtime, true);
        await Assert.That(() => store.UploadAsync("bundesliga-context-source-bundle-" + new string('a', 64), [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7)).Throws<InvalidDataException>();
        await store.UploadAsync(Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7); await Assert.That(tool.Invocation!.ArtifactName).IsEqualTo(Name); await Assert.That(tool.Invocation.CompressionLevel).IsEqualTo(0); await Assert.That(tool.Invocation.RetentionDays).IsEqualTo(7);
        await Assert.That(() => store.UploadAsync(Name, [new("manifest.json", [1], false, "link"), new("bundle.sha256", [2])], false, 0, 7)).Throws<InvalidDataException>();
    }
    private static GitHubContextSourceArtifactStore Store(HttpClient api, HttpClient archive) => new(new RecordingTool(), api, archive, Runtime, true);
    private static HttpClient Client(HttpMessageHandler handler, string baseAddress) => new(handler) { BaseAddress = new Uri(baseAddress) };
    private static byte[] Archive(params (string Path, string Body)[] entries) { using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) foreach (var (path, body) in entries) { var entry = zip.CreateEntry(path, CompressionLevel.NoCompression); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false), 1024, false); writer.Write(body); } return memory.ToArray(); }
    private sealed class RecordingTool : IGitHubArtifactTool { public GitHubArtifactToolInvocation? Invocation { get; private set; } public Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default) { Invocation = invocation; return Task.CompletedTask; } }
    private sealed class PagedApiHandler(string target) : HttpMessageHandler { public List<int> Pages { get; } = []; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { if (request.RequestUri!.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://signed.example/archive") } }); var page = int.Parse(request.RequestUri.Query.Split('&').Last().Split('=').Last()); Pages.Add(page); var items = page == 1 ? string.Join(',', Enumerable.Range(1, 100).Select(id => "{\"id\":" + id + ",\"name\":\"other\",\"expired\":false,\"workflow_run\":{\"id\":456}}")) : "{\"id\":999,\"name\":\"" + target + "\",\"expired\":false,\"workflow_run\":{\"id\":456}}"; var response = Json("{\"total_count\":101,\"artifacts\":[" + items + "]}"); if (page == 1) response.Headers.TryAddWithoutValidation("Link", "<https://api.github.com/repos/ehonda/KicktippAi/actions/runs/456/artifacts?per_page=100&page=2>; rel=\"next\""); return Task.FromResult(response); } }
    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(status)); }
    private sealed class OversizedListingHandler : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(Json(new string(' ', 1024 * 1024 + 1))); }
    private sealed class CancellationHandler : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromCanceled<HttpResponseMessage>(token); }
    private sealed class ListedHandler(int total, string artifacts) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(Json("{\"total_count\":" + total + ",\"artifacts\":" + artifacts + "}")); }
    private sealed class SingleArtifactApi(string target) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal) ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://signed.example/archive") } } : Json("{\"total_count\":1,\"artifacts\":[{\"id\":1,\"name\":\"" + target + "\",\"expired\":false,\"workflow_run\":{\"id\":456}}]}")); }
    private sealed class ArchiveStatusHandler(HttpStatusCode status) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(status)); }
    private sealed class ArchiveHandler(byte[] bytes) : HttpMessageHandler { public AuthenticationHeaderValue? Authorization { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Authorization = request.Headers.Authorization; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }); } }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

// W2R Group H. Stream counters distinguish transport admission from an eventual enum
// returned after HttpClient has already buffered an unbounded body.
public class GitHubArtifactW2RHTests
{
    [Test]
    [Arguments(false)] [Arguments(true)]
    public Task H_listing_stops_at_cap_plus_one_without_requesting_eof(bool falseLength)
        => AssertByteCap(false, falseLength, 1024 * 1024);

    [Test]
    [Arguments(false)] [Arguments(true)]
    public Task H_archive_stops_at_cap_plus_one_without_requesting_eof(bool falseLength)
        => AssertByteCap(true, falseLength, 4 * 1024 * 1024);

    private static async Task AssertByteCap(bool archiveBody, bool falseLength, int cap)
    {
        // The source exposes cap+1 bytes, then STALLS. It never supplies an EOF that
        // could make an unbounded buffering implementation accidentally finish.
        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var body = new W2RObservedStream(cap + 1, stalled);
        using var content = new W2RStreamingContent(body, falseLength ? 1 : null);
        using var cancellation = new CancellationTokenSource();
        var bodyHandler = new W2RHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var api = W2RSupport.Client(archiveBody ? W2RSupport.ArtifactApi() : bodyHandler);
        using var archive = W2RSupport.Client(archiveBody ? bodyHandler : new W2RHandler((_, _) => throw new InvalidOperationException("ARCHIVE_MUST_NOT_BE_REQUESTED")));
        using var store = W2RSupport.Store(api, archive);
        var operation = store.ProbeAsync(W2RSupport.Name, cancellation.Token);
        try
        {
            await Task.WhenAny(operation, stalled.Task).WaitAsync(TimeSpan.FromSeconds(5));
            W2RSupport.Require(operation.IsCompleted, $"Limiter requested beyond cap+1 and waited for EOF: bytes={body.BytesRead}, post-cap reads={body.ReadsAfterPayload}, serialized={content.Serialized}");
            var result = await operation;
            await Assert.That(result.Disposition).IsEqualTo(archiveBody ? ContextSourceArtifactProbeDisposition.Conflict : ContextSourceArtifactProbeDisposition.Indeterminate);
            W2RSupport.Require(!content.Serialized && body.BytesRead == cap + 1 && body.ReadsAfterPayload == 0 && body.EofReads == 0 && body.Disposed,
                $"Required streamed cap+1 disposal: serialized={content.Serialized}, bytes={body.BytesRead}, post-cap reads={body.ReadsAfterPayload}, EOF={body.EofReads}, disposed={body.Disposed}");
            foreach (var read in body.Reads)
                W2RSupport.Require(read.Requested <= cap - read.Before + 1,
                    $"Read requested {read.Requested} bytes with only {cap - read.Before} allowance plus exactly one detection byte remaining");
        }
        finally { cancellation.Cancel(); await W2RProcessFixture.Observe(operation); }
    }
    [Test]
    [Arguments(0, "exact")]
    [Arguments(1, "exact")]
    [Arguments(0, "foreign")]
    [Arguments(1, "duplicate")]
    public async Task H_short_terminal_page_with_next_link_is_indeterminate(int count, string linkKind)
    {
        var requests = 0;
        using var api = W2RSupport.Client(new W2RHandler((request, _) =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal)) return Task.FromResult(W2RSupport.Redirect());
            var response = W2RSupport.Listing(count);
            var link = "<https://api.github.com/repos/ehonda/KicktippAi/actions/runs/456/artifacts?per_page=100&page=2>; rel=\"next\"";
            if (linkKind == "foreign") link = link.Replace("api.github.com", "attacker.invalid", StringComparison.Ordinal);
            if (linkKind == "duplicate") link += ", " + link;
            response.Headers.TryAddWithoutValidation("Link", link);
            return Task.FromResult(response);
        }));
        var archiveRequests = 0;
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => { archiveRequests++; return Task.FromResult(W2RSupport.Bytes(new W2RZip().Build().Bytes)); }));
        var result = await W2RSupport.Store(api, archive).ProbeAsync(W2RSupport.Name);
        await Assert.That(result.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        W2RSupport.Require(requests == 1 && archiveRequests == 0, "Contradictory listing must not authorize archive access");
    }

    // Operational statuses cannot prove an immutable identity/content conflict.
    // 200 malformed ZIP => Conflict; listed expired/wrong run/duplicates => Conflict;
    // complete zero-match listing => Absent; signed 401/403/404/408/429/5xx => Indeterminate.
    [Test]
    [Arguments(401)] [Arguments(403)] [Arguments(404)] [Arguments(408)] [Arguments(429)] [Arguments(500)] [Arguments(503)]
    public async Task H_signed_operational_status_is_indeterminate(int status)
    {
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        await Assert.That((await W2RSupport.Store(api, archive).ProbeAsync(W2RSupport.Name)).Disposition)
            .IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
    }

    [Test]
    [Arguments("listing-headers", false)] [Arguments("listing-body", false)]
    [Arguments("redirect-headers", false)] [Arguments("signed-headers", false)] [Arguments("signed-body", false)]
    [Arguments("listing-headers", true)] [Arguments("listing-body", true)]
    [Arguments("redirect-headers", true)] [Arguments("signed-headers", true)] [Arguments("signed-body", true)]
    public async Task H_one_linked_budget_covers_every_phase_and_caller_cancellation(string phase, bool callerCancellation)
    {
        using var caller = new CancellationTokenSource();
        using var budget = new CancellationTokenSource();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var body = new W2RObservedStream(7, reached);
        var requests = new List<string>();
        async Task<HttpResponseMessage> Gate(string current, CancellationToken token, Func<HttpResponseMessage> next)
        {
            requests.Add(current);
            if (phase == current) { reached.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            if (phase == current.Replace("headers", "body", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new W2RStreamingContent(body) };
            return next();
        }
        using var api = W2RSupport.Client(new W2RHandler((request, token) =>
            request.RequestUri!.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal)
                ? Gate("redirect-headers", token, W2RSupport.Redirect)
                : Gate("listing-headers", token, () => W2RSupport.Listing(1))));
        using var archive = W2RSupport.Client(new W2RHandler((_, token) => Gate("signed-headers", token, () => W2RSupport.Bytes([]))));
        // Infinite injected client timeouts must not disable the adapter's own operation budget.
        using var store = W2RSupport.Store(api, archive, budget.Token);
        var operation = store.ProbeAsync(W2RSupport.Name, caller.Token);
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10)); // harness watchdog, never operation clock
        var requestCount = requests.Count;
        if (callerCancellation) caller.Cancel(); else budget.Cancel();
        if (callerCancellation)
            await Assert.That(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
        else
            await Assert.That((await operation.WaitAsync(TimeSpan.FromSeconds(5))).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        W2RSupport.Require(requests.Count == requestCount, "No follow-up after cancellation/deadline");
        if (phase.EndsWith("body", StringComparison.Ordinal)) W2RSupport.Require(body.Disposed, "Canceled body must be disposed");
    }
    [Test]
    public async Task H_cumulative_deadline_is_not_renewed_between_listing_pages()
    {
        var clock = new W2RManualTimeProvider(); var pages = new List<int>();
        using var caller = new CancellationTokenSource();
        using var api = W2RSupport.Client(new W2RHandler(async (request, token) =>
        {
            var page = W2RSupport.PageNumber(request); pages.Add(page);
            // Each individual phase consumes less than 30 seconds, but their sum
            // crosses the original operation deadline in page three's headers.
            clock.Advance(TimeSpan.FromSeconds(page < 3 ? 12 : 7));
            if (page == 3) await Task.Delay(Timeout.Infinite, token);
            return W2RSupport.ListingPage(page, 301, 100, true);
        }));
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => throw new InvalidOperationException("NO_ARCHIVE_AFTER_DEADLINE")));
        using var store = W2RSupport.Store(api, archive, timeProvider: clock);
        var operation = store.ProbeAsync(W2RSupport.Name, caller.Token);
        try
        {
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(result.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
            W2RSupport.Require(pages.SequenceEqual(new[] { 1, 2, 3 }) && clock.GetTimestamp() == TimeSpan.FromSeconds(31).Ticks,
                "One 30-second operation budget must expire at cumulative second 31 before page four");
            W2RSupport.Require(clock.CreatedTimers == 1, "Page/phase transitions must not create fresh deadline timers");
        }
        finally { caller.Cancel(); await W2RProcessFixture.Observe(operation); }
    }

    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task H_simultaneous_caller_and_internal_cancellation_prefers_caller(bool internalFirst)
    {
        using var caller = new CancellationTokenSource(); using var budget = new CancellationTokenSource();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var api = W2RSupport.Client(new W2RHandler(async (_, token) =>
        {
            requests++; reached.SetResult();
            // Release only after BOTH sources are canceled: no scheduler race can
            // let the operation observe a one-source intermediate state.
            await release.Task; token.ThrowIfCancellationRequested(); return W2RSupport.Listing(0);
        }));
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => throw new InvalidOperationException("NO_ARCHIVE_AFTER_CANCELLATION")));
        using var store = W2RSupport.Store(api, archive, budget.Token);
        var operation = store.ProbeAsync(W2RSupport.Name, caller.Token);
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (internalFirst) { budget.Cancel(); caller.Cancel(); } else { caller.Cancel(); budget.Cancel(); }
            release.SetResult();
            await Assert.That(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
            W2RSupport.Require(requests == 1, "Simultaneous cancellation must not permit another request");
        }
        finally { caller.Cancel(); budget.Cancel(); release.TrySetResult(); await W2RProcessFixture.Observe(operation); }
    }

    [Test]
    [Arguments("unstable-count")]
    [Arguments("wrong-run-next-link")]
    [Arguments("missing-next-link")]
    public async Task H_pagination_authority_failures_are_independent(string violation)
    {
        var pages = new List<int>();
        using var api = W2RSupport.Client(new W2RHandler((request, _) =>
        {
            var page = W2RSupport.PageNumber(request); pages.Add(page);
            return Task.FromResult(violation switch
            {
                "unstable-count" => W2RSupport.ListingPage(page, page == 1 ? 101 : 102, page == 1 ? 100 : 2, page == 1),
                "wrong-run-next-link" => W2RSupport.ListingPage(page, 101, 100, true, 999),
                "missing-next-link" => W2RSupport.ListingPage(page, 101, 100, false),
                _ => throw new ArgumentOutOfRangeException(nameof(violation))
            });
        }));
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => throw new InvalidOperationException("INCOMPLETE_LISTING_CANNOT_AUTHORIZE_ARCHIVE")));
        using var store = W2RSupport.Store(api, archive);
        await Assert.That((await store.ProbeAsync(W2RSupport.Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Indeterminate);
        W2RSupport.Require(pages.SequenceEqual(violation == "unstable-count" ? new[] { 1, 2 } : new[] { 1 }), "Stop at first independent pagination contradiction");
    }

    [Test]
    public async Task H_matching_artifact_with_wrong_workflow_run_is_conflict()
    {
        using var api = W2RSupport.Client(new W2RHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(new { total_count = 1, artifacts = new[] { new { id = 1, name = W2RSupport.Name, expired = false, workflow_run = new { id = 999 } } } })) })));
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => throw new InvalidOperationException("WRONG_RUN_CANNOT_AUTHORIZE_ARCHIVE")));
        using var store = W2RSupport.Store(api, archive);
        await Assert.That((await store.ProbeAsync(W2RSupport.Name)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
    }

    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task H_maximum_page_budget_requires_complete_listing(bool morePagesRemain)
    {
        var pages = new List<int>();
        using var api = W2RSupport.Client(new W2RHandler((request, _) =>
        {
            var page = W2RSupport.PageNumber(request); pages.Add(page);
            W2RSupport.Require(page <= 100, "The bounded listing state machine must never request page 101");
            return Task.FromResult(W2RSupport.ListingPage(page, morePagesRemain ? 10001 : 10000, 100, page < 100 || morePagesRemain));
        }));
        using var archive = W2RSupport.Client(new W2RHandler((_, _) => throw new InvalidOperationException("ZERO_MATCH_LISTING_CANNOT_REQUEST_ARCHIVE")));
        using var store = W2RSupport.Store(api, archive);
        await Assert.That((await store.ProbeAsync(W2RSupport.Name)).Disposition)
            .IsEqualTo(morePagesRemain ? ContextSourceArtifactProbeDisposition.Indeterminate : ContextSourceArtifactProbeDisposition.Absent);
        W2RSupport.Require(pages.SequenceEqual(Enumerable.Range(1, 100)), "Exactly 100 sequential authoritative pages");
    }

    [Test]
    [Arguments("api-200")]
    [Arguments("api-307")]
    [Arguments("missing-location")]
    [Arguments("relative-location")]
    [Arguments("http-location")]
    [Arguments("userinfo-location")]
    [Arguments("signed-302")]
    [Arguments("signed-307")]
    [Arguments("signed-response-uri-change")]
    public async Task H_redirect_topology_never_authorizes_followup_or_credential_forwarding(string violation)
    {
        var apiRequests = 0; var signedRequests = 0;
        using var api = W2RSupport.Client(new W2RHandler((request, _) =>
        {
            apiRequests++;
            W2RSupport.Require(request.RequestUri!.Host == "api.github.com", "Authenticated API authority is fixed");
            if (!request.RequestUri.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal)) return Task.FromResult(W2RSupport.Listing(1));
            var response = W2RSupport.Redirect();
            switch (violation)
            {
                case "api-200": response.StatusCode = HttpStatusCode.OK; break;
                case "api-307": response.StatusCode = HttpStatusCode.TemporaryRedirect; break;
                case "missing-location": response.Headers.Location = null; break;
                case "relative-location": response.Headers.Location = new Uri("/archive", UriKind.Relative); break;
                case "http-location": response.Headers.Location = new Uri("http://signed.example/archive"); break;
                case "userinfo-location": response.Headers.Location = new Uri("https://user:synthetic-secret@signed.example/archive"); break;
            }
            return Task.FromResult(response);
        }));
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-api-token");
        using var archive = W2RSupport.Client(new W2RHandler((request, _) =>
        {
            signedRequests++;
            W2RSupport.Require(request.Headers.Authorization is null && !request.Headers.Contains("Cookie"), "Signed origin receives no credentials");
            W2RSupport.Require(request.RequestUri!.Host == "signed.example" && signedRequests == 1, "No signed redirect follow-up is permitted");
            var response = W2RSupport.Bytes(new W2RZip().Build().Bytes);
            if (violation is "signed-302" or "signed-307")
            {
                response.StatusCode = violation == "signed-302" ? HttpStatusCode.Found : HttpStatusCode.TemporaryRedirect;
                response.Headers.Location = new Uri("https://foreign.example/next");
            }
            else response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://foreign.example/changed");
            return Task.FromResult(response);
        }));
        using var store = W2RSupport.Store(api, archive);
        await Assert.That((await store.ProbeAsync(W2RSupport.Name)).Disposition).IsEqualTo(
            violation is "api-200" or "api-307" ? ContextSourceArtifactProbeDisposition.Indeterminate : ContextSourceArtifactProbeDisposition.Conflict);
        W2RSupport.Require(apiRequests == 2 && signedRequests == (violation.StartsWith("signed-", StringComparison.Ordinal) ? 1 : 0),
            "Reject topology at the first untrusted transition");
    }

}

internal static class W2RSupport
{
    internal static readonly GitHubArtifactRuntime Runtime = new("ehonda/KicktippAi", 123, 456);
    internal static string Name => Runtime.ExpectedArtifactName;
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("W2R_ASSERT: " + message); }
    internal static void ThrowIndependentFailures(params Exception?[] failures)
    {
        var present = failures.OfType<Exception>().ToArray();
        if (present.Length == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(present[0]).Throw();
        if (present.Length > 1) throw new AggregateException("W2R body, harness and fixture failures", present);
    }
    internal static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://api.github.com/"), Timeout = Timeout.InfiniteTimeSpan };
    internal static GitHubContextSourceArtifactStore Store(HttpClient api, HttpClient archive, CancellationToken budget = default, TimeProvider? timeProvider = null)
        => new(new NoUpload(), api, archive, Runtime, true, budget, timeProvider);
    internal static HttpResponseMessage Listing(int count) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { total_count = count, artifacts = count == 0 ? Array.Empty<object>() : [new { id = 1, name = Name, expired = false, workflow_run = new { id = 456 } }] }))
    };
    internal static int PageNumber(HttpRequestMessage request)
        => int.Parse(request.RequestUri!.Query.Split('&').Last().Split('=').Last(), System.Globalization.CultureInfo.InvariantCulture);
    internal static HttpResponseMessage ListingPage(int page, int total, int count, bool next, int nextRun = 456)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { total_count = total, artifacts = Enumerable.Range(1, count)
                .Select(index => new { id = (page - 1) * 100 + index, name = "other", expired = false, workflow_run = new { id = 456 } }).ToArray() }))
        };
        if (next) response.Headers.TryAddWithoutValidation("Link", $"<https://api.github.com/repos/ehonda/KicktippAi/actions/runs/{nextRun}/artifacts?per_page=100&page={page + 1}>; rel=\"next\"");
        return response;
    }
    internal static HttpResponseMessage Redirect() => new(HttpStatusCode.Found) { Headers = { Location = new Uri("https://signed.example/archive?secret=synthetic-signed-secret") } };
    internal static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    internal static W2RHandler ArtifactApi() => new((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/zip", StringComparison.Ordinal) ? Redirect() : Listing(1)));
    internal static async Task<ContextSourceArtifactProbe> Probe(byte[] bytes)
    {
        using var api = Client(ArtifactApi());
        using var archive = Client(new W2RHandler((_, _) => Task.FromResult(Bytes(bytes))));
        using var store = Store(api, archive);
        return await store.ProbeAsync(Name);
    }
    private sealed class NoUpload : IGitHubArtifactTool
    { public Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException("UPLOAD_NOT_PERMITTED_IN_HTTP_TEST"); }
}

internal sealed class W2RHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }

internal sealed class W2RStreamingContent(W2RObservedStream source, long? declaredLength = null) : HttpContent
{
    internal bool Serialized { get; private set; }
    protected override bool TryComputeLength(out long length) { length = declaredLength ?? 0; return declaredLength.HasValue; }
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(source);
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult<Stream>(source);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    { Serialized = true; await source.CopyToAsync(stream, cancellationToken); }
    protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
}

internal sealed class W2RObservedStream(int length, TaskCompletionSource? stall = null) : Stream
{
    internal long BytesRead { get; private set; }
    internal List<(long Before, int Requested, int Returned)> Reads { get; } = [];
    internal int ReadsAfterPayload { get; private set; }
    internal int EofReads { get; private set; }
    internal bool Disposed { get; private set; }
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("SYNCHRONOUS_STREAM_READ_FORBIDDEN");
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BytesRead == length && stall is not null) { ReadsAfterPayload++; stall.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); }
        var count = (int)Math.Min(buffer.Length, length - BytesRead);
        Reads.Add((BytesRead, buffer.Length, count)); if (count == 0) EofReads++;
        buffer.Span[..count].Fill((byte)' '); BytesRead += count; return count;
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

// W2R Group P uses the real Node process. No fake scope can earn containment credit.
// All independently retained Process handles are killed/awaited in finally, including red runs.
public class GitHubArtifactW2RPTests
{
    [Test]
    [Arguments("helper")]
    [Arguments(".github")]
    [Arguments("scripts")]
    [Arguments("context-source-artifact")]
    [Arguments("workspace-ancestor")]
    public async Task P_helper_and_workspace_ancestry_are_rejected_before_launch(string component)
    {
        using var fixture = new W2RProcessFixture();
        await fixture.WriteHelper($"import fs from 'node:fs'; fs.writeFileSync({JsonSerializer.Serialize(fixture.Sentinel)}, 'executed');");
        fixture.Indirect(component); // capability failure is a failing test, never a silent skip
        var launches = 0;
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, (launch, token) => { launches++; return fixture.Start(launch, token); });
        Exception? failure = null;
        try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (Exception exception) { failure = exception; }
        finally { await fixture.StopProcesses(); }
        var expected = component == "workspace-ancestor" ? "GITHUB_ARTIFACT_WORKSPACE_INVALID" : "GITHUB_ARTIFACT_HELPER_INVALID";
        W2RSupport.Require(failure is IOException io && io.Message == expected && io.InnerException is null,
            "Indirection must fail admission with its exact fixed ancestry diagnostic");
        W2RSupport.Require(launches == 0 && !File.Exists(fixture.Sentinel), "No helper code may run before canonical ancestry admission");
    }

    [Test]
    public async Task P_precanceled_caller_does_not_launch_helper()
    {
        using var fixture = new W2RProcessFixture();
        await fixture.WriteHelper("setInterval(() => {}, 1000);");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var launches = 0;
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, (launch, token) => { launches++; return fixture.Start(launch, token); });
        try { await Assert.That(() => tool.ExecuteUploadAsync(fixture.Invocation, cancellation.Token)).Throws<OperationCanceledException>(); }
        finally { await fixture.StopProcesses(); }
        W2RSupport.Require(launches == 0, "Pre-cancellation must be checked before Process.Start");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task P_scope_survives_direct_child_exit_and_kills_inherited_pipe_descendant(bool deadline)
    {
        var fixture = new W2RProcessFixture();
        fixture.RequireDescendantWitness = true;
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        fixture.ScopeStage = stages.Enqueue;
        var observation = new W2ROutputObservation();
        Exception? bodyFailure = null, operationFailure = null, harnessFailure = null, fixtureFailure = null;
        var grandchild = "setInterval(() => {}, 1000);";
        using var caller = new CancellationTokenSource(); using var budget = new CancellationTokenSource();
        Task? operation = null;
        try
        {
            try
            {
                await fixture.WriteHelper($$"""
                    import {spawn} from 'node:child_process';
                    import fs from 'node:fs';
                    const child = spawn(process.execPath, ['-e', {{JsonSerializer.Serialize(grandchild)}}], {stdio: ['ignore', 'inherit', 'inherit']});
                    fs.writeFileSync({{JsonSerializer.Serialize(fixture.PidFile)}}, String(child.pid));
                    child.unref();
                    process.exit(0);
                    """);
                var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start, budget.Token,
                    observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
                operation = tool.ExecuteUploadAsync(fixture.Invocation, caller.Token);
                await fixture.CaptureGrandchild();
                await fixture.DirectExit!.WaitAsync(TimeSpan.FromSeconds(10));
                W2RSupport.Require(!operation.IsCompleted, "Inherited pipes must keep readers pending after direct exit");
                if (deadline) budget.Cancel(); else caller.Cancel();
                Exception? failure = null;
                try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; operationFailure = exception; }
                var descendant = fixture.DescendantExitWitness(); // first terminal observation, before any fallback
                W2RSupport.Require(deadline ? failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_TIMEOUT" } : failure is OperationCanceledException,
                    "Fixed deadline/cancellation result must settle within cleanup budget");
                var descendantExited = descendant.Passed;
                if (!descendantExited && OperatingSystem.IsLinux())
                    Console.WriteLine($"W2R_P_SCOPE_FIRST_FAILED_WITNESS term=descendantHasExited " +
                        $"operation={operation.Status} outcome={W2RProcessFixture.FixedOutcome(operation, operationFailure)} " +
                        $"{descendant.Diagnostic} observer={fixture.DirectExit?.Status.ToString() ?? "absent"} " +
                        $"readers={observation.ReaderCount} readersSettled={observation.ReadersSettled} pipesDisposed={observation.PipesDisposed} " +
                        $"stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}");
                W2RSupport.Require(descendantExited && fixture.HarnessKills == 0 && fixture.Direct!.HasExited && fixture.DirectExit.IsCompleted &&
                    observation.ReaderCount == 2 && observation.ReadersSettled && observation.PipesDisposed,
                    "Retained process scope must terminate the same descendant and settle direct child, observer and both pipes before harness fallback: " + descendant.Diagnostic);
            }
            catch (Exception exception) { bodyFailure = exception; }
            finally
            {
                // Snapshot before fallback termination. The fixed stage names and states contain no helper output.
                Console.WriteLine($"W2R_P_SCOPE_DIAGNOSTIC operation={operation?.Status.ToString() ?? "absent"} " +
                    $"outcome={W2RProcessFixture.FixedOutcome(operation, operationFailure)} " +
                    $"direct={W2RProcessFixture.ExitState(fixture.Direct)} descendant={W2RProcessFixture.ExitState(fixture.Grandchild)} " +
                    $"observer={fixture.DirectExit?.Status.ToString() ?? "absent"} readers={observation.ReaderCount} " +
                    $"readersSettled={observation.ReadersSettled} pipesDisposed={observation.PipesDisposed} " +
                    $"stages={string.Join(',', stages.ToArray())}");
                caller.Cancel(); budget.Cancel();
                try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = exception; }
                if (operation is not null) await W2RProcessFixture.Observe(operation);
                Console.WriteLine($"W2R_P_SCOPE_HARNESS_KILLS {fixture.HarnessKills}");
            }
        }
        finally { try { fixture.Dispose(); } catch (Exception exception) { fixtureFailure = exception; } }
        W2RSupport.ThrowIndependentFailures(bodyFailure, harnessFailure, fixtureFailure);
    }

    [Test]
    [Arguments("caller")]
    [Arguments("deadline")]
    [Arguments("reader-fault")]
    public async Task P_abnormal_live_child_exit_terminates_scope_and_sanitizes_output(string cause)
    {
        using var fixture = new W2RProcessFixture();
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        fixture.LaunchTransition = stage => stages.Enqueue("launch:" + stage);
        fixture.ScopeStage = stage => stages.Enqueue("scope:" + stage);
        await fixture.WriteHelper($$"""
            import fs from 'node:fs';
            process.stdout.write('synthetic-token-secret'); process.stderr.write('https://signed.invalid/?secret=synthetic-token-secret');
            fs.writeFileSync({{JsonSerializer.Serialize(fixture.PidFile)}}, String(process.pid));
            setInterval(() => {}, 1000);
            """);
        using var caller = new CancellationTokenSource(); using var budget = new CancellationTokenSource();
        var releaseFault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> FaultReader(StreamReader reader, CancellationToken token)
        { await releaseFault.Task.WaitAsync(token); throw new IOException("synthetic-token-secret"); }
        var observation = new W2ROutputObservation();
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start, budget.Token, cause == "reader-fault" ? FaultReader : null,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        var operation = tool.ExecuteUploadAsync(fixture.Invocation, caller.Token);
        // Retain a separate handle because production disposes its direct Process object.
        Process? witness = null;
        Exception? operationFailure = null, bodyFailure = null, fallbackFailure = null, fixtureFailure = null;
        try
        {
            await fixture.WaitForPidFile(); witness = Process.GetProcessById(fixture.Direct!.Id);
            if (cause == "caller") caller.Cancel(); else if (cause == "deadline") budget.Cancel(); else releaseFault.SetResult();
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { operationFailure = exception; }
            W2RSupport.Require(operationFailure is not null && operationFailure is not TimeoutException, "Abnormal exit must settle within bounded cleanup");
            W2RSupport.Require(!operationFailure!.ToString().Contains("synthetic-token-secret", StringComparison.Ordinal), "Diagnostics must not retain helper output or reader secrets");
            W2RSupport.Require(witness.HasExited, "Every abnormal path must terminate and await owned process scope");
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            // Capture the original result before fallback signals or fixture kills can
            // change the process state or replace the first failing assertion.
            var beforeHarness = $"cause={cause} operation={operation.Status} outcome={W2RProcessFixture.FixedOutcome(operation, operationFailure)} " +
                $"{fixture.LinuxDirectWitnessDiagnostic()} witnessPid={witness?.Id.ToString() ?? "absent"} witnessHasExited={W2RProcessFixture.ExitState(witness)} " +
                $"directExit={fixture.DirectExit?.Status.ToString() ?? "absent"} readers={observation.ReaderCount} readersSettled={observation.ReadersSettled} " +
                $"pipesDisposed={observation.PipesDisposed} stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}";
            if (bodyFailure is not null) Console.WriteLine("W2R_P_ORIGINAL_FIRST_FAILURE " + beforeHarness + $" bodyFailure={bodyFailure.GetType().Name}");
            caller.Cancel(); budget.Cancel();
            try
            {
                if (witness is not null)
                {
                    if (!witness.HasExited) witness.Kill(true);
                    await witness.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception exception) { fallbackFailure = exception; }
            finally { witness?.Dispose(); }
            try { await fixture.StopProcesses(); }
            catch (Exception exception) { fixtureFailure = exception; }
            await W2RProcessFixture.Observe(operation);
            if (fallbackFailure is not null || fixtureFailure is not null)
                Console.WriteLine("W2R_P_ORIGINAL_CLEANUP_FAILURE " + beforeHarness +
                    $" fallbackFailure={fallbackFailure?.GetType().Name ?? "absent"} fixtureFailure={fixtureFailure?.GetType().Name ?? "absent"} harnessKillsAfter={fixture.HarnessKills}");
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, fallbackFailure, fixtureFailure);
    }

    [Test]
    [Arguments("stdout")] [Arguments("stderr")]
    public async Task P_real_output_flood_bounds_reads_settles_readers_kills_scope_and_removes_scratch(string pipe)
    {
        const int outputCap = 1024 * 1024;
        using var fixture = new W2RProcessFixture { RequireDescendantWitness = true };
        var childSource = $$"""
            const fs = require('node:fs');
            const timer = setInterval(() => {
              if (!fs.existsSync({{JsonSerializer.Serialize(fixture.Sentinel)}})) return;
              clearInterval(timer);
              process[{{JsonSerializer.Serialize(pipe)}}].write(Buffer.alloc({{2 * outputCap + 1}}, 120));
              setInterval(() => {}, 1000);
            }, 10);
            """;
        await fixture.WriteHelper($$"""
            import {spawn} from 'node:child_process'; import fs from 'node:fs';
            const child = spawn(process.execPath, ['-e', {{JsonSerializer.Serialize(childSource)}}], {stdio: ['ignore', 'inherit', 'inherit']});
            fs.writeFileSync({{JsonSerializer.Serialize(fixture.PidFile)}}, String(child.pid));
            setInterval(() => {}, 1000);
            """);
        var observation = new W2ROutputObservation();
        var node = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        var bridge = new W2RRecordingBridge(node);
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi()); using var archive = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var store = new GitHubContextSourceArtifactStore(bridge, api, archive, W2RSupport.Runtime, true);
        using var caller = new CancellationTokenSource();
        var operation = store.UploadAsync(W2RSupport.Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7, caller.Token);
        Process? directWitness = null;
        Exception? bodyFailure = null, harnessFailure = null, fallbackFailure = null;
        try
        {
            await fixture.CaptureGrandchild(); directWitness = Process.GetProcessById(fixture.Direct!.Id);
            W2RSupport.Require(bridge.Content is not null && Directory.Exists(bridge.Content), "Real Store.UploadAsync must own scratch before the helper floods");
            W2RSupport.Require(observation.ReaderCount == 2, "Both real pipe readers must be observable before flood release");
            await File.WriteAllTextAsync(fixture.Sentinel, "release-flood");
            Exception? failure = null;
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            var descendant = fixture.DescendantExitWitness(); // first terminal observation, before assertions or fallback
            // Snapshot every invariant BEFORE caller cancellation, harness kill, pipe
            // disposal, or fallback scratch deletion can make a broken bridge look safe.
            var bytes = observation.BytesRead;
            var boundedRequests = observation.Reads.All(read => read.Before <= outputCap && read.Requested <= outputCap - read.Before + 1);
            var readersSettled = observation.ReadersSettled;
            var pipesDisposed = observation.PipesDisposed;
            var directExited = directWitness.HasExited; var descendantExited = descendant.Passed;
            var scratchRemoved = !Directory.Exists(Path.GetDirectoryName(bridge.Content!));
            var fixedFailure = failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_OUTPUT_LIMIT" };
            W2RSupport.Require(fixedFailure && bytes == outputCap + 1 && boundedRequests && readersSettled && pipesDisposed && directExited &&
                fixture.DirectExit!.IsCompleted && descendantExited && scratchRemoved && fixture.HarnessKills == 0,
                $"Flood before harness cleanup: failure={failure?.GetType().Name}/{failure?.Message}, bytes={bytes}, boundedRequests={boundedRequests}, readersSettled={readersSettled}, pipesDisposed={pipesDisposed}, directExited={directExited}, descendantExited={descendantExited}, scratchRemoved={scratchRemoved}, {descendant.Diagnostic}, harnessKills={fixture.HarnessKills}");
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            caller.Cancel();
            try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = exception; }
            try
            {
                if (directWitness is not null) { if (!directWitness.HasExited) directWitness.Kill(true); await directWitness.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            }
            catch (Exception exception) { fallbackFailure = exception; }
            finally { directWitness?.Dispose(); }
            await W2RProcessFixture.Observe(operation);
            bridge.CleanupLeftoverScratch(); // never counted as product cleanup evidence
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, harnessFailure, fallbackFailure);
    }

    [Test]
    public async Task P_real_helper_success_baselines_observed_readers_and_store_scratch_cleanup()
    {
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.stdout.write('ok'); process.stderr.write('ok');");
        var observation = new W2ROutputObservation();
        var bridge = new W2RRecordingBridge(new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted));
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi()); using var archive = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var store = new GitHubContextSourceArtifactStore(bridge, api, archive, W2RSupport.Runtime, true);
        try
        {
            await store.UploadAsync(W2RSupport.Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7).WaitAsync(TimeSpan.FromSeconds(10));
            W2RSupport.Require(observation.BytesRead == 4 && observation.ReadersSettled && observation.PipesDisposed,
                "Observation seams must preserve a successful real helper baseline");
            W2RSupport.Require(bridge.Content is not null && !Directory.Exists(Path.GetDirectoryName(bridge.Content)), "Real helper success removes store-owned scratch before harness cleanup");
        }
        finally { await fixture.StopProcesses(); bridge.CleanupLeftoverScratch(); }
    }
    [Test]
    public async Task P_startup_failure_has_fixed_secret_free_outcome()
    {
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.exit(0);");
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, (_, _) => throw new System.ComponentModel.Win32Exception("synthetic-start-secret"));
        Exception? failure = null;
        try { await tool.ExecuteUploadAsync(fixture.Invocation); } catch (Exception exception) { failure = exception; }
        W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_START_FAILED" }, "Startup failure must be normalized before diagnostics");
        W2RSupport.Require(!failure!.ToString().Contains("synthetic-start-secret", StringComparison.Ordinal), "Startup diagnostic must not retain injected OS details");
    }
    [Test]
    public async Task P_scratch_is_removed_after_bridge_failure()
    {
        var tool = new W2RFailingUpload();
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi()); using var archive = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var store = new GitHubContextSourceArtifactStore(tool, api, archive, W2RSupport.Runtime, true);
        await Assert.That(() => store.UploadAsync(W2RSupport.Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7)).Throws<IOException>();
        W2RSupport.Require(tool.Content is not null && !Directory.Exists(Path.GetDirectoryName(tool.Content)), "Store owns and removes its unique scratch tree on failure");
    }
    [Test]
    public async Task P_partial_staging_rolls_back_first_owned_file_without_launch()
    {
        var tool = new W2RCountingUpload(); string? root = null; var firstFilePresent = false;
        void Stage(string transition, string path)
        {
            if (transition != "first-file-staged") return;
            root = path; firstFilePresent = File.Exists(Path.Combine(path, "content", "manifest.json"));
            throw new IOException("synthetic-staging-secret");
        }
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi()); using var archive = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var store = new GitHubContextSourceArtifactStore(tool, api, archive, W2RSupport.Runtime, true,
            stagingTransitionForTests: Stage);
        Exception? failure = null;
        try { await store.UploadAsync(W2RSupport.Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7); }
        catch (Exception exception) { failure = exception; }
        W2RSupport.Require(firstFilePresent && root is not null && !Directory.Exists(root) && tool.Launches == 0 &&
            failure is IOException { Message: "GITHUB_ARTIFACT_SCRATCH_INVALID", InnerException: null } &&
            !failure.ToString().Contains("synthetic-staging-secret", StringComparison.Ordinal),
            "A real first-file staging failure rolls back the owned root before any bridge launch");
    }
    [Test]
    [Arguments("content")]
    [Arguments("manifest.json")]
    public async Task P_deletion_rejects_indirect_staged_component_without_following_external_sentinel(string component)
    {
        var external = Path.Combine(Path.GetTempPath(), "kicktippai-w2r-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(external);
        var sentinel = Path.Combine(external, "sentinel"); await File.WriteAllTextAsync(sentinel, "external-intact");
        var tool = new W2RCountingUpload(); var bridge = new W2RRecordingBridge(tool);
        string? root = null, link = null;
        Exception? stageFailure = null, bodyFailure = null, fixtureFailure = null;
        void Stage(string transition, string path)
        {
            if (transition != "before-delete") return;
            try
            {
                root = path;
                link = component == "content" ? Path.Combine(path, "content") : Path.Combine(path, "content", component);
                if (component == "content")
                {
                    // Move the owned directory aside before placing the indirect path.
                    var displaced = Path.Combine(external, "owned-content"); Directory.Move(link, displaced);
                    Directory.CreateSymbolicLink(link, external);
                }
                else
                {
                    File.Delete(link);
                    File.CreateSymbolicLink(link, sentinel);
                }
            }
            catch (Exception exception) { stageFailure = exception; throw; }
        }
        using var api = W2RSupport.Client(W2RSupport.ArtifactApi()); using var archive = W2RSupport.Client(W2RSupport.ArtifactApi());
        using var store = new GitHubContextSourceArtifactStore(bridge, api, archive, W2RSupport.Runtime, true,
            stagingTransitionForTests: Stage);
        try
        {
            try
            {
                Exception? failure = null;
                try { await store.UploadAsync(W2RSupport.Name, [new("manifest.json", [1]), new("bundle.sha256", [2])], false, 0, 7); }
                catch (Exception exception) { failure = exception; }
                W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_SCRATCH_INVALID", InnerException: null } &&
                    tool.Launches == 1 && root is not null && Directory.Exists(root) &&
                    File.Exists(sentinel) && await File.ReadAllTextAsync(sentinel) == "external-intact",
                    "Finite product deletion rejects an indirect staged directory or leaf with a fixed code and preserves the external sentinel");
            }
            catch (Exception exception) { bodyFailure = exception; }
        }
        finally
        {
            try
            {
                if (root is not null && link is not null && Guid.TryParseExact(Path.GetFileName(root), "N", out _) &&
                    Path.GetDirectoryName(root) == Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kicktippai-github-artifact")))
                {
                    if (component == "content" && Directory.Exists(link)) Directory.Delete(link);
                    if (component == "manifest.json" && File.Exists(link)) File.Delete(link);
                    bridge.CleanupLeftoverScratch();
                }
                if (Directory.Exists(external)) Directory.Delete(external, true);
            }
            catch (Exception exception) { fixtureFailure = exception; }
        }
        W2RSupport.ThrowIndependentFailures(stageFailure, bodyFailure, fixtureFailure);
    }
    private sealed class W2RCountingUpload : IGitHubArtifactTool
    {
        internal int Launches;
        public Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default)
        { Launches++; return Task.CompletedTask; }
    }
    private sealed class W2RFailingUpload : IGitHubArtifactTool
    {
        internal string? Content;
        public Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default)
        { Content = invocation.ContentDirectory; throw new IOException("GITHUB_ARTIFACT_HELPER_TIMEOUT"); }
    }
}

public class GitHubArtifactW2RPBridgeTests
{
    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task P_real_dual_pipe_exact_caps_require_eof_and_overflow_is_per_pipe(bool overflow)
    {
        using var fixture = new W2RProcessFixture(); var size = ArtifactPipeDrain.ByteCap + (overflow ? 1 : 0);
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        fixture.LaunchTransition = stage => stages.Enqueue("native:" + stage);
        fixture.ScopeStage = stage => stages.Enqueue("scope:" + stage);
        await fixture.WriteHelper($"process.stdout.write(Buffer.alloc({size}, 120)); process.stderr.write(Buffer.alloc({size}, 121)); " + (overflow ? "setInterval(() => {}, 1000);" : ""));
        var observation = new W2ROutputObservation();
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start, observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        Exception? failure = null, bodyFailure = null, cleanupFailure = null;
        Task? operation = null;
        try
        {
            operation = tool.ExecuteUploadAsync(fixture.Invocation);
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            W2RSupport.Require(overflow ? failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_OUTPUT_LIMIT" } : failure is null, "Both exact per-pipe caps with EOF succeed; overflow fails with its fixed code");
            W2RSupport.Require(observation.ReadersSettled && observation.PipesDisposed && fixture.Direct!.HasExited, "Real dual-pipe completion must settle both readers, endpoints and native child before harness cleanup");
            W2RSupport.Require(observation.Reads.All(read => read.Requested <= 4096) && observation.Reads.GroupBy(read => read.Pipe).All(group => group.Sum(read => read.Returned) <= ArtifactPipeDrain.ByteCap + 1), "Requests and consumption obey independent per-pipe limits");
            W2RSupport.Require(overflow ? observation.BytesRead >= ArtifactPipeDrain.ByteCap + 1 && observation.BytesRead <= 2L * (ArtifactPipeDrain.ByteCap + 1) : observation.BytesRead == 2L * ArtifactPipeDrain.ByteCap, "The policy permits two exact caps and bounds simultaneous overflow without retaining output");
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            var pipeReads = string.Join(',', observation.Reads.GroupBy(read => read.Pipe).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:bytes={group.Sum(read => read.Returned)},zeroReads={group.Count(read => read.Returned == 0)}"));
            var exitCode = fixture.DirectExit is { IsCompletedSuccessfully: true } directExit
                ? directExit.Result.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable";
            var beforeHarness = $"overflow={overflow} operation={operation?.Status.ToString() ?? "absent"} " +
                $"outcome={W2RProcessFixture.FixedOutcome(operation, failure)} failure={failure?.GetType().Name ?? "absent"} " +
                $"{fixture.LinuxDirectIdentityDiagnostic()} directExit={fixture.DirectExit?.Status.ToString() ?? "absent"} directExitCode={exitCode} " +
                $"readers={observation.ReaderCount} readersSettled={observation.ReadersSettled} pipesDisposed={observation.PipesDisposed} " +
                $"bytes={observation.BytesRead} pipeReads={pipeReads} stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}";
            if (bodyFailure is not null) Console.WriteLine("W2R_P_BRIDGE_CAP_FIRST_FAILURE " + beforeHarness + $" bodyFailure={bodyFailure.GetType().Name}");
            try { await fixture.StopProcesses(); } catch (Exception exception) { cleanupFailure = exception; }
            if (cleanupFailure is not null)
                Console.WriteLine("W2R_P_BRIDGE_CAP_CLEANUP_FAILURE " + beforeHarness +
                    $" cleanupFailure={cleanupFailure.GetType().Name} harnessKillsAfter={fixture.HarnessKills}");
            if (operation is not null) await W2RProcessFixture.Observe(operation);
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, cleanupFailure);
    }

    [Test]
    [Arguments("deadline")] [Arguments("reader-fault")] [Arguments("helper-failure")] [Arguments("output-limit")]
    public async Task P_caller_signal_during_confirmed_native_cleanup_has_deterministic_precedence(string cause)
    {
        using var fixture = new W2RProcessFixture();
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        fixture.LaunchTransition = stage => stages.Enqueue("launch:" + stage);
        fixture.ScopeStage = stage => stages.Enqueue("scope:" + stage);
        var ending = cause == "helper-failure" ? "process.exit(7);" : cause == "output-limit" ? $"process.stdout.write(Buffer.alloc({ArtifactPipeDrain.ByteCap + 1})); setInterval(() => {{}}, 1000);" : "setInterval(() => {}, 1000);";
        await fixture.WriteHelper($"import fs from 'node:fs'; fs.writeFileSync({JsonSerializer.Serialize(fixture.PidFile)}, String(process.pid)); {ending}");
        using var caller = new CancellationTokenSource(); using var deadline = new CancellationTokenSource();
        var fault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> FaultReader(StreamReader reader, CancellationToken token) { await fault.Task.WaitAsync(token); throw new IOException("synthetic-reader-secret"); }
        var observation = new W2ROutputObservation();
        var tool = new NodeGitHubArtifactTool(fixture.Workspace,
            (launch, token) => new ConfirmationGate(fixture.Start(launch, token), entered, release, stage => stages.Enqueue("callback:" + stage)),
            deadline.Token, cause == "reader-fault" ? FaultReader : null,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        var operation = tool.ExecuteUploadAsync(fixture.Invocation, caller.Token);
        Exception? operationFailure = null, bodyFailure = null, fixtureFailure = null;
        try
        {
            await fixture.WaitForPidFile();
            if (cause == "deadline") deadline.Cancel();
            if (cause == "reader-fault") fault.TrySetResult();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel(); deadline.Cancel(); release.TrySetResult();
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { operationFailure = exception; }
            W2RSupport.Require(operationFailure is OperationCanceledException canceled && canceled.CancellationToken == caller.Token && canceled.InnerException is null && fixture.Direct!.HasExited,
                "A caller signal before outcome selection outranks deadline, reader, helper and output-limit results after actual native confirmation");
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            var beforeHarness = $"cause={cause} operation={operation.Status} outcome={W2RProcessFixture.FixedOutcome(operation, operationFailure)} " +
                $"{fixture.LinuxDirectWitnessDiagnostic()} directExit={fixture.DirectExit?.Status.ToString() ?? "absent"} " +
                $"readers={observation.ReaderCount} readersSettled={observation.ReadersSettled} pipesDisposed={observation.PipesDisposed} " +
                $"entered={entered.Task.Status} released={release.Task.Status} callerCanceled={caller.IsCancellationRequested} " +
                $"deadlineCanceled={deadline.IsCancellationRequested} stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}";
            if (bodyFailure is not null) Console.WriteLine("W2R_P_BRIDGE_FIRST_FAILURE " + beforeHarness + $" bodyFailure={bodyFailure.GetType().Name}");
            caller.Cancel(); release.TrySetResult(); fault.TrySetResult();
            try { await fixture.StopProcesses(); }
            catch (Exception exception) { fixtureFailure = exception; }
            await W2RProcessFixture.Observe(operation);
            if (fixtureFailure is not null)
                Console.WriteLine("W2R_P_BRIDGE_CLEANUP_FAILURE " + beforeHarness + $" fixtureFailure={fixtureFailure.GetType().Name} harnessKillsAfter={fixture.HarnessKills}");
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, fixtureFailure);
    }

    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task P_wrapper_start_and_disposal_faults_are_fixed_and_settle_native_scope(bool disposal)
    {
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.stdout.write('x'); process.stderr.write('y');");
        using var caller = new CancellationTokenSource(); using var deadline = new CancellationTokenSource();
        var observation = new W2ROutputObservation();
        Stream Wrap(string pipe, Stream stream)
        {
            if (pipe == "stdout" && !disposal) throw new IOException("synthetic-wrapper-secret");
            var counted = observation.Wrap(pipe, stream);
            return pipe == "stdout" ? new DisposeFaultStream(counted, () => { caller.Cancel(); deadline.Cancel(); }) : counted;
        }
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start, deadline.Token, observeOutputStream: Wrap, observeOutputTask: observation.ReaderStarted);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation, caller.Token).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            W2RSupport.Require(failure is IOException io && io.Message == (disposal ? "GITHUB_ARTIFACT_HELPER_CLEANUP_FAILED" : "GITHUB_ARTIFACT_HELPER_IO_FAILED") && io.InnerException is null,
                "Wrapper-start faults are IO failures; uncertain wrapper disposal outranks simultaneous caller/deadline signals");
            W2RSupport.Require(!failure!.ToString().Contains("synthetic-wrapper-secret", StringComparison.Ordinal) && observation.ReadersSettled && fixture.Direct!.HasExited,
                "Wrapper faults must not expose diagnostics or leave real readers/native children running");
        }
        finally { caller.Cancel(); await fixture.StopProcesses(); }
    }

    [Test]
    [Arguments(false)] [Arguments(true)]
    public void P_monotonic_work_and_cleanup_deadlines_never_extend_the_total_budget(bool earlyFailure)
    {
        var clock = new W2RManualTimeProvider(); using var budget = new ArtifactBridgeBudget(clock);
        clock.Advance(TimeSpan.FromSeconds(earlyFailure ? 5 : 117));
        W2RSupport.Require(budget.WorkExpired == !earlyFailure, "Production work cutoff is exactly 117 monotonic seconds");
        using var first = budget.CleanupDeadline(); clock.Advance(TimeSpan.FromSeconds(2)); using var second = budget.CleanupDeadline();
        W2RSupport.Require(!first.IsCancellationRequested && !second.IsCancellationRequested, "Cleanup retains the remaining part of its original reservation");
        clock.Advance(TimeSpan.FromSeconds(1));
        W2RSupport.Require(first.IsCancellationRequested && second.IsCancellationRequested, "Repeated cleanup phases share one maximum three-second deadline and cannot exceed 120 seconds");
    }

    // A gate around real native confirmation, never an alternative process owner.
    private sealed class ConfirmationGate(IArtifactProcessScope actual, TaskCompletionSource entered, TaskCompletionSource release,
        Action<string> stage) : IArtifactProcessScope
    {
        public int ProcessId => actual.ProcessId; public Task<int> DirectExit => actual.DirectExit;
        public Stream StandardOutput => actual.StandardOutput; public Stream StandardError => actual.StandardError;
        public void TerminateScope()
        {
            stage("terminate-enter");
            try { actual.TerminateScope(); stage("terminate-return"); }
            catch { stage("terminate-throw"); throw; }
        }
        public async Task<bool> ConfirmTerminatedAsync(CancellationToken token)
        {
            stage($"confirm-enter tokenCanceled={token.IsCancellationRequested}");
            bool confirmed;
            try { confirmed = await actual.ConfirmTerminatedAsync(token); }
            catch { stage($"confirm-throw tokenCanceled={token.IsCancellationRequested}"); throw; }
            stage($"confirm-{(confirmed ? "true" : "false")} tokenCanceled={token.IsCancellationRequested}");
            entered.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            catch { stage($"release-wait-throw tokenCanceled={token.IsCancellationRequested}"); throw; }
            stage($"confirm-released tokenCanceled={token.IsCancellationRequested}");
            return confirmed;
        }
        public Task<bool> ReleaseAsync(CancellationToken token) => actual.ReleaseAsync(token);
        public Task<bool> SettleObserverAsync(CancellationToken token) => actual.SettleObserverAsync(token);
        public void Dispose() => actual.Dispose();
    }
    private sealed class DisposeFaultStream(Stream actual, Action beforeFailure) : Stream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => actual.ReadAsync(buffer, token);
        protected override void Dispose(bool disposing) { if (disposing) { actual.Dispose(); beforeFailure(); throw new IOException("synthetic-wrapper-secret"); } base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => actual.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public class GitHubArtifactW2RPNativeTests
{
    [Test]
    public async Task P_native_launch_description_seals_canonical_paths_and_collections()
    {
        var fixture = new W2RProcessFixture();
        using var caller = new CancellationTokenSource();
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var callbackFaults = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var output = new W2RNativeTimeoutOutputProbe();
        Task? operation = null;
        Exception? bodyFailure = null, diagnosticFailure = null, harnessActionFailure = null, stopFailure = null,
            residualFailure = null, fixtureFailure = null;
        var firstVerdictRecorded = false;
        void RecordStage(string source, string stage)
        {
            try { stages.Enqueue(source + ":" + stage); }
            catch (Exception exception) { callbackFaults.Enqueue(source + "-" + exception.GetType().Name); }
        }
        try
        {
            try
            {
                await fixture.WriteHelper($"import fs from 'node:fs'; fs.writeFileSync({JsonSerializer.Serialize(fixture.Sentinel)}, process.cwd());");
                var workspace = Path.Combine(fixture.Workspace, "unused", "..", ".");
                var invocation = fixture.Invocation with { ContentDirectory = Path.Combine(fixture.Invocation.ContentDirectory, "unused", "..") };
                var admitted = ValidatedArtifactLaunch.Admit(workspace, invocation);
                W2RSupport.Require(admitted.Workspace == Path.GetFullPath(fixture.Workspace) && admitted.Arguments[1] == fixture.Helper && admitted.Arguments[6] == fixture.Invocation.ContentDirectory,
                    "Admission and native cwd/argv must share the same canonical path values");
                W2RSupport.Require(admitted.Arguments is not string[] && admitted.Environment is not string[], "Backing launch arrays must not escape admission");
                foreach (var values in new[] { admitted.Arguments, admitted.Environment })
                {
                    var rejected = false;
                    try { ((IList<string>)values)[0] = "unadmitted-replacement"; } catch (NotSupportedException) { rejected = true; }
                    W2RSupport.Require(rejected, "Launch argument and environment views must reject mutation");
                }
                var copy = admitted.Arguments.ToArray(); copy[1] = "unadmitted-replacement";
                W2RSupport.Require(admitted.Arguments[1] == fixture.Helper, "Consumer copies must not mutate the admitted helper");
                fixture.LaunchTransition = stage => RecordStage("launch", stage);
                fixture.ScopeStage = stage => RecordStage("scope", stage);
                fixture.CaptureLinuxSpawnedChild = OperatingSystem.IsLinux();
                fixture.DecorateScope = output.DecorateScope;
                var tool = new NodeGitHubArtifactTool(workspace, fixture.Start,
                    observeOutputStream: output.Wrap, observeOutputTask: output.ReaderStarted);
                operation = tool.ExecuteUploadAsync(invocation, caller.Token);
                Exception? firstFailure = null;
                try { await operation.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception exception) { firstFailure = exception; }
                // The first result is fixed before cancellation, fixture recovery,
                // another wait, or any attempt to improve a pending observation.
                var firstState = operation.Status;
                var snapshotFaults = new List<string>();
                string Capture(string name, Func<string> sample)
                {
                    try { return sample(); }
                    catch (Exception exception)
                    {
                        snapshotFaults.Add(name + "-" + exception.GetType().Name);
                        return "unavailable";
                    }
                }
                var firstStages = Capture("stages", () => string.Join(',', stages.ToArray()));
                W2RProcessFixture.TimeoutIdentitySnapshot? identitySample = null;
                var identity = Capture("identity", () =>
                {
                    identitySample = fixture.LinuxDirectIdentitySnapshotForTimeout();
                    return identitySample.Diagnostic;
                });
                var directExit = Capture("direct-exit", () => fixture.DirectExit is { } exit
                    ? exit.Status + (exit.IsCompletedSuccessfully ? ":" + exit.Result.ToString(System.Globalization.CultureInfo.InvariantCulture) : "")
                    : "unavailable-prepublication");
                var pipes = Capture("pipes", output.Snapshot);
                var sentinel = Capture("sentinel", () => BoundedSentinelWitness(fixture));
                var callbackErrors = Capture("callbacks", () => string.Join(',', callbackFaults.ToArray().Concat(output.CallbackFaults)));
                var probeErrors = Capture("probe", () => string.Join(',', output.ProbeFaults));
                if (identitySample?.Complete != true) snapshotFaults.Add("identity-incomplete");
                var complete = callbackErrors.Length == 0 && probeErrors.Length == 0 && snapshotFaults.Count == 0;
                firstVerdictRecorded = true;
                try
                {
                    Console.WriteLine($"W2R_P_NATIVE_DESCRIPTION_FIRST_VERDICT result={W2RProcessFixture.FixedOutcome(operation, firstFailure)} " +
                        $"waitFailure={firstFailure?.GetType().Name ?? "absent"} operationState={firstState} " +
                        $"stages={firstStages} identity={identity} directExit={directExit} " +
                        $"pipes={pipes} sentinel={sentinel} callbackFaults={callbackErrors} probeFaults={probeErrors} " +
                        $"snapshotFaults={string.Join(',', snapshotFaults)} diagnosticComplete={complete} harnessKills={fixture.HarnessKills}");
                }
                catch (Exception exception) { diagnosticFailure = exception; }
                if (!complete) diagnosticFailure = new InvalidOperationException("W2R_ASSERT: Native timeout diagnostic capture incomplete");
                if (firstFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
                W2RSupport.Require(await File.ReadAllTextAsync(fixture.Sentinel) == fixture.Workspace, "The actual native helper must execute in the admitted canonical cwd");
            }
            catch (Exception exception) { bodyFailure = exception; }
        }
        finally
        {
            if (operation is { IsCompleted: false } && firstVerdictRecorded)
            {
                try { caller.Cancel(); }
                catch (Exception exception) { harnessActionFailure = exception; }
                try { Console.WriteLine("W2R_P_NATIVE_DESCRIPTION_HARNESS_ACTION caller-cancel-after-first-verdict"); }
                catch (Exception exception) { diagnosticFailure ??= exception; }
            }
            try { await fixture.StopProcesses(); } catch (Exception exception) { stopFailure = exception; }
            if (operation is not null)
            {
                await W2RProcessFixture.Observe(operation);
                if (!operation.IsCompleted) residualFailure = new InvalidOperationException("W2R_ASSERT: Native description operation pending after bounded recovery");
            }
            var terminalIdentity = "unavailable";
            var terminalPipes = "unavailable";
            var terminalDirectExit = "unavailable-prepublication";
            try
            {
                var terminal = fixture.LinuxDirectIdentitySnapshotForTimeout();
                terminalIdentity = terminal.Diagnostic;
                terminalPipes = output.Snapshot();
                if (fixture.DirectExit is { } exit)
                    terminalDirectExit = exit.Status + (exit.IsCompletedSuccessfully
                        ? ":" + exit.Result.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
                if (terminal.Residual || fixture.DirectExit is { IsCompleted: false } || !output.RecoverySettled ||
                    output.CallbackFaults.Count != 0 || output.ProbeFaults.Count != 0)
                    residualFailure = residualFailure is null
                        ? new InvalidOperationException("W2R_ASSERT: Native description identity or pipe residual after bounded recovery")
                        : new AggregateException(residualFailure, new InvalidOperationException("W2R_ASSERT: Native description identity or pipe residual after bounded recovery"));
            }
            catch (Exception exception)
            {
                residualFailure = residualFailure is null ? exception : new AggregateException(residualFailure, exception);
            }
            try { fixture.Dispose(); } catch (Exception exception) { fixtureFailure = exception; }
            try
            {
                Console.WriteLine($"W2R_P_NATIVE_DESCRIPTION_RECOVERY operation={operation?.Status.ToString() ?? "absent"} " +
                    $"eventualOutcome={W2RProcessFixture.FixedOutcome(operation, null)} " +
                    $"harnessActionFailure={harnessActionFailure?.GetType().Name ?? "absent"} " +
                    $"stopFailure={stopFailure?.GetType().Name ?? "absent"} " +
                    $"fixtureFailure={fixtureFailure?.GetType().Name ?? "absent"} residual={residualFailure is not null} " +
                    $"terminalIdentity={terminalIdentity} terminalPipes={terminalPipes} terminalDirectExit={terminalDirectExit} " +
                    $"observedProductReaps={stages.Count(stage => stage == "launch:waitpid-reaped")} " +
                    $"harnessKills={fixture.HarnessKills} stages={string.Join(',', stages.ToArray())}");
            }
            catch (Exception exception) { diagnosticFailure ??= exception; }
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, diagnosticFailure, harnessActionFailure, stopFailure, residualFailure, fixtureFailure);
    }

    private static string BoundedSentinelWitness(W2RProcessFixture fixture)
    {
        try
        {
            using var file = new FileStream(fixture.Sentinel, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[4097];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = file.Read(buffer.AsSpan(count));
                if (read == 0) break;
                count += read;
            }
            if (count == buffer.Length) return "present-over-4096-bytes";
            return "present-cwd-match-" +
                buffer.AsSpan(0, count).SequenceEqual(Encoding.UTF8.GetBytes(fixture.Workspace));
        }
        catch (FileNotFoundException) { return "absent"; }
        catch (DirectoryNotFoundException) { return "absent"; }
    }

    [Test]
    public async Task P_native_special_file_helper_is_rejected_before_launch()
    {
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.exit(0);");
        File.Delete(fixture.Helper);
        if (OperatingSystem.IsLinux())
            W2RSupport.Require(NativeFixture.mkfifo(fixture.Helper, 0x180) == 0, "Linux FIFO capability is required for the regular-file admission gate");
        else Directory.CreateDirectory(fixture.Helper);
        var launches = 0;
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, (launch, token) => { launches++; return fixture.Start(launch, token); });
        Exception? failure = null;
        try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
        finally { await fixture.StopProcesses(); }
        W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_INVALID" } && launches == 0 && !File.Exists(fixture.Sentinel),
            "A FIFO on Linux or directory on Windows must fail regular-file admission with no launch/effect");
    }

    [Test]
    public async Task P_native_unpublished_scope_rolls_back_after_observer_start()
    {
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("setInterval(() => {}, 1000);");
        var transitions = new List<string>();
        fixture.LaunchTransition = stage =>
        {
            transitions.Add(stage);
            if (stage == "before-publication") throw new IOException("synthetic-start-secret");
        };
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
        Exception? failure = null;
        try
        {
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_START_FAILED" } && !failure.ToString().Contains("synthetic-start-secret", StringComparison.Ordinal),
                "Post-owner startup failure must settle and normalize without leaking injected diagnostics");
            W2RSupport.Require(fixture.Direct is not null && fixture.Direct.HasExited, "Unpublished native scope must terminate before harness cleanup");
            W2RSupport.Require(transitions.IndexOf("parent-child-endpoints-closed") < transitions.IndexOf("scope-contained") && transitions.Contains("before-publication"),
                "Native endpoint closure must precede scope publication and rollback injection");
            if (OperatingSystem.IsWindows())
                W2RSupport.Require(transitions.IndexOf("scope-contained") < transitions.IndexOf("before-resume") && transitions.Contains("after-resume"),
                    "Windows Job must be assigned before resume; this rollback must cover a resumed child");
        }
        finally { await fixture.StopProcesses(); }
    }

    [Test]
    [Arguments("assignment-failure")]
    [Arguments("pre-resume-fault")]
    [Arguments("pre-resume-cancel")]
    public async Task P_windows_suspended_child_rolls_back_before_first_instruction(string cause)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new W2RProcessFixture();
        await fixture.WriteHelper($"import fs from 'node:fs'; fs.writeFileSync({JsonSerializer.Serialize(fixture.Sentinel)}, 'executed'); setInterval(() => {{}}, 1000);");
        fixture.CaptureWindowsSuspendedChild = true;
        using var caller = new CancellationTokenSource();
        var transitions = new List<string>();
        fixture.LaunchTransition = stage =>
        {
            transitions.Add(stage);
            if (stage == "before-resume" && cause == "pre-resume-fault") throw new IOException("synthetic-pre-resume-secret");
            if (stage == "before-resume" && cause == "pre-resume-cancel") caller.Cancel();
        };
        if (cause == "assignment-failure") fixture.WindowsAssignmentGateForTests = () => false;
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation, caller.Token).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) { failure = exception; }
            W2RSupport.Require(cause == "pre-resume-cancel"
                    ? failure is OperationCanceledException canceled && canceled.CancellationToken == caller.Token
                    : failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_START_FAILED", InnerException: null },
                "Suspended-child rollback must preserve fixed start or caller-cancellation precedence");
            W2RSupport.Require(fixture.Direct is not null && fixture.Direct.HasExited && !File.Exists(fixture.Sentinel),
                "The independently retained suspended child must exit without running its first helper instruction");
            W2RSupport.Require(transitions.Contains("parent-child-endpoints-closed") && transitions.Contains("before-assignment") &&
                transitions.Contains("rollback-endpoints-closed") && !transitions.Contains("after-resume") &&
                (cause == "assignment-failure" ? !transitions.Contains("scope-contained") && !transitions.Contains("before-resume")
                    : transitions.Contains("scope-contained") && transitions.Contains("before-resume")),
                "Rollback closes native endpoints; assignment failure exercises the unassigned branch before resume");
            W2RSupport.Require(failure is null || !failure.ToString().Contains("synthetic-pre-resume-secret", StringComparison.Ordinal),
                "The public failure must not expose the injected native-stage diagnostic");
        }
        finally { caller.Cancel(); await fixture.StopProcesses(); }
    }

    [Test]
    public async Task P_native_first_action_and_ordinary_descendant_have_original_scope_membership()
    {
        using var fixture = new W2RProcessFixture { RequireDescendantWitness = true };
        var descendant = $"require('node:fs').writeFileSync({JsonSerializer.Serialize(fixture.DescendantReady)}, String(process.pid)); setInterval(() => {{}}, 1000);";
        await fixture.WriteHelper($$"""
            import fs from 'node:fs';
            import {spawn} from 'node:child_process';
            fs.writeFileSync({{JsonSerializer.Serialize(fixture.LeaderReady)}}, String(process.pid));
            const child = spawn(process.execPath, ['-e', {{JsonSerializer.Serialize(descendant)}}], {stdio: ['ignore', 'inherit', 'inherit']});
            fs.writeFileSync({{JsonSerializer.Serialize(fixture.PidFile)}}, String(child.pid));
            child.unref(); setInterval(() => {}, 1000);
            """);
        using var caller = new CancellationTokenSource();
        var observation = new W2ROutputObservation();
        Microsoft.Win32.SafeHandles.SafeFileHandle? windowsJob = null;
        fixture.ObserveWindowsAssignment = (_, process, job) =>
        {
            W2RSupport.Require(NativeFixture.InJob(process, job), "Windows direct child is in its assigned Job while still suspended");
            windowsJob = job;
        };
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        var operation = tool.ExecuteUploadAsync(fixture.Invocation, caller.Token);
        Exception? bodyFailure = null, harnessFailure = null;
        try
        {
            await fixture.WaitForFile(fixture.LeaderReady);
            await fixture.CaptureGrandchild();
            await fixture.WaitForFile(fixture.DescendantReady);
            W2RSupport.Require(fixture.Direct is not null && fixture.Grandchild is not null,
                "Independent leader and descendant handles are retained at their first script-level readiness gates");
            if (OperatingSystem.IsWindows())
                W2RSupport.Require(windowsJob is not null && NativeFixture.InJob(fixture.Grandchild.SafeHandle, windowsJob),
                    "The ordinary child inherits the actual assigned Job at its first gated action");
            else
            {
                W2RSupport.Require(OperatingSystem.IsLinux() && NativeFixture.LinuxProcessGroup(fixture.Direct.Id) == fixture.Direct.Id &&
                    NativeFixture.LinuxProcessGroup(fixture.Grandchild.Id) == fixture.Direct.Id &&
                    NativeFixture.ProcGroup(fixture.Direct.Id) == fixture.Direct.Id &&
                    NativeFixture.ProcGroup(fixture.Grandchild.Id) == fixture.Direct.Id,
                    "The glibc-spawned retained leader and earliest gated ordinary child share their real group in the visible /proc namespace");
            }
            caller.Cancel();
            Exception? failure = null;
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            var descendantExit = fixture.DescendantExitWitness();
            W2RSupport.Require(failure is OperationCanceledException && fixture.Direct.HasExited && descendantExit.Passed &&
                observation.ReaderCount == 2 && observation.ReadersSettled && observation.PipesDisposed && fixture.DirectExit!.IsCompleted &&
                fixture.HarnessKills == 0,
                "Native scope, same-identity descendant, both pipes and observer settle before harness fallback: " + descendantExit.Diagnostic);
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            caller.Cancel();
            try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = exception; }
            await W2RProcessFixture.Observe(operation);
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, harnessFailure);
    }

    [Test]
    [Arguments("confirm-false")]
    [Arguments("confirm-throw")]
    [Arguments("release-false")]
    [Arguments("release-throw")]
    [Arguments("observer-false")]
    [Arguments("observer-throw")]
    [Arguments("direct-exit-fault")]
    public async Task P_native_cleanup_outcomes_are_fixed_and_preserve_real_owner_settlement(string mode)
    {
        using var fixture = new W2RProcessFixture();
        await fixture.WriteHelper("process.exit(7);");
        var observation = new W2ROutputObservation(); CleanupOutcomeScope? scope = null;
        fixture.DecorateScope = actual => scope = new CleanupOutcomeScope(actual, mode);
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            var expected = mode == "direct-exit-fault" ? "GITHUB_ARTIFACT_HELPER_IO_FAILED" : "GITHUB_ARTIFACT_HELPER_CLEANUP_FAILED";
            W2RSupport.Require(failure is IOException io && io.Message == expected && io.InnerException is null &&
                !io.ToString().Contains("synthetic-cleanup-secret", StringComparison.Ordinal),
                "Native confirmation, release and observer faults normalize to their exact secret-free public result");
            W2RSupport.Require(scope is not null && scope.RealConfirmed && scope.RealReleaseCompleted && scope.RealObserverSettled &&
                fixture.Direct!.HasExited && fixture.DirectExit!.IsCompleted &&
                observation.ReadersSettled && observation.PipesDisposed,
                "Actual native confirmation, release, observer, both drains and direct witness settle before harness cleanup");
            W2RSupport.Require(mode.StartsWith("confirm-", StringComparison.Ordinal) ? scope!.ProductReleaseCalls == 0 : scope!.ProductReleaseCalls == 1,
                "A failed confirmation must not be credited with a product release call");
        }
        finally { await fixture.StopProcesses(); }
    }

    private sealed class CleanupOutcomeScope(IArtifactProcessScope actual, string mode) : IArtifactProcessScope
    {
        private readonly Task<int>? _faultedExit = mode == "direct-exit-fault" ? Task.FromException<int>(new IOException("synthetic-cleanup-secret")) : null;
        public int ProcessId => actual.ProcessId;
        public Task<int> DirectExit => _faultedExit ?? actual.DirectExit;
        public Stream StandardOutput => actual.StandardOutput; public Stream StandardError => actual.StandardError;
        public bool RealConfirmed { get; private set; }
        public bool RealReleaseCompleted { get; private set; }
        public bool RealObserverSettled { get; private set; }
        public int ProductReleaseCalls { get; private set; }
        public void TerminateScope() => actual.TerminateScope();
        public async Task<bool> ConfirmTerminatedAsync(CancellationToken token)
        {
            RealConfirmed = await actual.ConfirmTerminatedAsync(token);
            if (mode == "confirm-throw") throw new IOException("synthetic-cleanup-secret");
            return mode == "confirm-false" ? false : RealConfirmed;
        }
        public async Task<bool> ReleaseAsync(CancellationToken token)
        {
            ProductReleaseCalls++;
            RealReleaseCompleted = await actual.ReleaseAsync(token);
            if (mode == "release-throw") throw new IOException("synthetic-cleanup-secret");
            return mode == "release-false" ? false : RealReleaseCompleted;
        }
        public async Task<bool> SettleObserverAsync(CancellationToken token)
        {
            RealObserverSettled = await actual.SettleObserverAsync(token);
            if (mode == "observer-throw") throw new IOException("synthetic-cleanup-secret");
            return mode == "observer-false" ? false : RealObserverSettled;
        }
        public void Dispose()
        {
            try
            {
                // The product correctly skips release after a failed confirmation.
                // This test-only owner finishes the real retained native scope.
                if (RealConfirmed && !RealReleaseCompleted)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    RealReleaseCompleted = actual.ReleaseAsync(cleanup.Token).GetAwaiter().GetResult();
                }
            }
            finally { actual.Dispose(); }
        }
    }

    [Test]
    public async Task P_linux_deployed_abi_retains_waitid_identity_until_one_native_reap()
    {
        if (!OperatingSystem.IsLinux()) return;
        NativeFixture.RequireLinuxExports();
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.exit(7);");
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var retainedBeforeReap = false;
        fixture.LaunchTransition = stage =>
        {
            stages.Enqueue(stage);
            if (stage == "before-final-reap") retainedBeforeReap = NativeFixture.WaitidRetainsExitedChild(fixture.Direct!.Id);
        };
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            var recorded = stages.ToArray();
            W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_FAILED", InnerException: null } &&
                retainedBeforeReap && fixture.Direct!.HasExited && fixture.DirectExit!.IsCompleted &&
                recorded.Contains("waitid-wnowait-retained") && recorded.Count(stage => stage == "waitpid-reaped") == 1 &&
                Array.IndexOf(recorded, "waitid-wnowait-retained") < Array.IndexOf(recorded, "waitpid-reaped") &&
                NativeFixture.WaitidSeesEchildAfterReap(fixture.Direct.Id),
                "The deployed glibc waitid ABI retains the exited child until the sole native owner consumes it once");
        }
        finally { await fixture.StopProcesses(); }
    }

    [Test]
    [Arguments("stdout-transferred")]
    [Arguments("stderr-transferred")]
    public async Task P_linux_partial_pipe_transfer_rolls_back_all_native_owners(string faultStage)
    {
        if (!OperatingSystem.IsLinux()) return;
        var fixture = new W2RProcessFixture();
        var transitions = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var closed = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Exception? setupFailure = null, bodyFailure = null, harnessFailure = null, fixtureFailure = null;
        try
        {
            try { await fixture.WriteHelper("setInterval(() => {}, 1000);"); }
            catch (Exception exception)
            {
                setupFailure = exception;
                Console.WriteLine($"W2R_P_PARTIAL_TRANSFER_SETUP faultStage={faultStage} failureType={exception.GetType().Name}");
            }
            if (setupFailure is null)
            {
                fixture.CaptureLinuxSpawnedChild = true;
                fixture.ObserveLinuxPipeClose = closed.Enqueue;
                fixture.LaunchTransition = stage =>
                {
                    transitions.Enqueue(stage);
                    if (stage == faultStage) throw new IOException("synthetic-transfer-secret");
                };
                try
                {
                    var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
                    Exception? operationFailure = null;
                    try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (Exception exception) { operationFailure = exception; }
                    // Freeze the first body and native evidence before fixture recovery.
                    // The procfs snapshot never waits or reaps; waitid WNOWAIT then
                    // independently confirms that the product consumed its child.
                    var recorded = transitions.ToArray();
                    var closedPipes = closed.ToArray();
                    var terminal = fixture.CaptureDirectTerminalWitness();
                    var nativeReaped = fixture.Direct is { } direct && NativeFixture.WaitidSeesEchildAfterReap(direct.Id);
                    // Do not ask Process to inspect exit while the native status
                    // might still be retained; its terminal state is secondary.
                    var directExited = nativeReaped && fixture.Direct is { } retained && retained.HasExited;
                    Console.WriteLine($"W2R_P_PARTIAL_TRANSFER_FIRST_WITNESS faultStage={faultStage} " +
                        $"operation={W2RProcessFixture.FixedOutcome(null, operationFailure)} " +
                        $"operationType={operationFailure?.GetType().Name ?? "none"} " +
                        $"stages={string.Join(',', recorded)} closedPipes={string.Join(',', closedPipes)} " +
                        $"directExit={fixture.DirectExit?.Status.ToString() ?? "unavailable-prepublication"} " +
                        $"scopeStages=unavailable-prepublication nativeReaped={nativeReaped} " +
                        $"directHasExited={directExited} harnessKills={fixture.HarnessKills} {terminal.Diagnostic}");
                    W2RSupport.Require(operationFailure is IOException { Message: "GITHUB_ARTIFACT_HELPER_START_FAILED", InnerException: null } &&
                        !operationFailure.ToString().Contains("synthetic-transfer-secret", StringComparison.Ordinal) &&
                        terminal.ProductReapCompatible && nativeReaped && directExited && fixture.HarnessKills == 0 &&
                        recorded.Contains("parent-child-endpoints-closed") && recorded.Contains(faultStage) &&
                        recorded.Count(stage => stage == "waitpid-reaped") == 1 &&
                        closedPipes.Contains("stdout") && (faultStage == "stdout-transferred" || closedPipes.Contains("stderr")),
                        "Fault after each owned fd transfer closes native pipes and exclusively reaps the independently retained child: " + terminal.Diagnostic);
                    Console.WriteLine($"W2R_P_PARTIAL_TRANSFER_FIRST_ASSERTION faultStage={faultStage} result=passed");
                }
                catch (Exception exception)
                {
                    bodyFailure = exception;
                    Console.WriteLine($"W2R_P_PARTIAL_TRANSFER_FIRST_ASSERTION faultStage={faultStage} " +
                        $"result=failed failureType={exception.GetType().Name} harnessKills={fixture.HarnessKills}");
                }
            }
        }
        finally
        {
            try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = exception; }
            try { fixture.Dispose(); } catch (Exception exception) { fixtureFailure = exception; }
            Console.WriteLine($"W2R_P_PARTIAL_TRANSFER_CLEANUP faultStage={faultStage} " +
                $"setupFailure={setupFailure?.GetType().Name ?? "absent"} " +
                $"bodyFailure={bodyFailure?.GetType().Name ?? "absent"} " +
                $"harnessFailure={harnessFailure?.GetType().Name ?? "absent"} " +
                $"fixtureFailure={fixtureFailure?.GetType().Name ?? "absent"} harnessKills={fixture.HarnessKills}");
        }
        W2RSupport.ThrowIndependentFailures(setupFailure, bodyFailure, harnessFailure, fixtureFailure);
    }

    [Test]
    public async Task P_linux_injected_eintr_retries_both_observer_and_exclusive_reap()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.exit(7);");
        var observerFaults = 0; var reapFaults = 0;
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>(); fixture.LaunchTransition = stages.Enqueue;
        fixture.LinuxWaitErrorForTests = phase => phase switch
        {
            "waitid-observer" when Interlocked.Increment(ref observerFaults) == 1 => 4,
            "waitpid-release" when Interlocked.Increment(ref reapFaults) == 1 => 4,
            _ => null
        };
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_FAILED", InnerException: null } &&
                observerFaults >= 1 && reapFaults >= 1 && stages.Count(stage => stage == "waitpid-reaped") == 1 &&
                fixture.Direct!.HasExited && NativeFixture.WaitidSeesEchildAfterReap(fixture.Direct.Id),
                "Injected EINTR at both lifecycle calls retries without surrendering actual native ownership or changing the helper result");
        }
        finally { await fixture.StopProcesses(); }
    }

    [Test]
    public async Task P_linux_injected_echild_refuses_group_signal_and_requires_explicit_test_reap()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new W2RProcessFixture(); await fixture.WriteHelper("process.exit(7);");
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>(); fixture.LaunchTransition = stages.Enqueue;
        fixture.LinuxWaitErrorForTests = phase => phase == "waitid-terminate" ? 10 : null;
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start);
        var directPid = 0;
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            directPid = fixture.Direct!.Id;
            var cleanupFailure = failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_CLEANUP_FAILED", InnerException: null };
            var directExited = fixture.Direct.HasExited;
            var identityLost = stages.Contains("waitid-echild-lost");
            var productDidNotReap = !stages.Contains("waitpid-reaped");
            var retainedForTest = NativeFixture.WaitidRetainsExitedChild(directPid);
            if (!cleanupFailure || !directExited || !identityLost || !productDidNotReap || !retainedForTest)
                Console.WriteLine($"W2R_P_NATIVE_ECHILD_FIRST_FAILED_WITNESS cleanupFailure={cleanupFailure} directPid={directPid} directHasExited={directExited} " +
                    $"identityLost={identityLost} productDidNotReap={productDidNotReap} retainedForTest={retainedForTest} stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}");
            // WNOWAIT retention is the direct, native proof that this exact child
            // has exited but remains unconsumed. Process.HasExited is expected to
            // remain false until this test's targeted consuming reap below.
            var directIsNonExecutingRetainedZombie = retainedForTest;
            W2RSupport.Require(cleanupFailure && identityLost && productDidNotReap && directIsNonExecutingRetainedZombie,
                "Injected ECHILD marks the identity lost, refuses PGID use and does not claim a successful native release");
        }
        finally
        {
            // The product deliberately lost ownership after injected ECHILD. Preserve
            // that proof, then let this test consume precisely its retained zombie
            // before generic fixture cleanup can wait on it.
            if (directPid != 0 && NativeFixture.WaitidRetainsExitedChild(directPid))
            {
                W2RSupport.Require(NativeFixture.TargetedTestReap(directPid) && NativeFixture.WaitidSeesEchildAfterReap(directPid),
                    "Only this test's exact retained child consumes the injected-ECHILD zombie after product no-reap proof");
                await fixture.StopProcesses();
            }
            else
            {
                // Setup/body failure cleanup uses the same identity-bound direct
                // fallback; it earns no product-release credit.
                await fixture.StopProcesses();
                if (directPid != 0 && NativeFixture.WaitidRetainsExitedChild(directPid))
                    W2RSupport.Require(NativeFixture.TargetedTestReap(directPid) && NativeFixture.WaitidSeesEchildAfterReap(directPid),
                        "Only the fixture-failure branch reaps its exact retained direct child after it is no longer executing");
            }
        }
    }

    [Test]
    public async Task P_windows_concurrent_launch_handle_lists_do_not_cross_inherit_parent_pipe_writers()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var first = new W2RProcessFixture(); using var second = new W2RProcessFixture();
        await first.WriteHelper("process.exit(0);"); await second.WriteHelper("setInterval(() => {}, 1000);");
        using var releaseFirst = new ManualResetEventSlim();
        var firstWritersReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.LaunchTransition = stage =>
        {
            if (stage != "before-create-process") return;
            firstWritersReady.TrySetResult();
            W2RSupport.Require(releaseFirst.Wait(TimeSpan.FromSeconds(10)), "The concurrent launch gate must be released by the test harness");
        };
        var firstOutput = new W2ROutputObservation(); var secondOutput = new W2ROutputObservation();
        var firstTool = new NodeGitHubArtifactTool(first.Workspace, first.Start,
            observeOutputStream: firstOutput.Wrap, observeOutputTask: firstOutput.ReaderStarted);
        var secondTool = new NodeGitHubArtifactTool(second.Workspace, second.Start,
            observeOutputStream: secondOutput.Wrap, observeOutputTask: secondOutput.ReaderStarted);
        using var secondCancel = new CancellationTokenSource();
        var firstOperation = Task.Run(() => firstTool.ExecuteUploadAsync(first.Invocation));
        Task? secondOperation = null;
        try
        {
            await firstWritersReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            secondOperation = secondTool.ExecuteUploadAsync(second.Invocation, secondCancel.Token);
            W2RSupport.Require(second.Direct is not null && !second.Direct.HasExited,
                "The second real helper is live while the first parent's inheritable writers are open");
            releaseFirst.Set();
            await firstOperation.WaitAsync(TimeSpan.FromSeconds(5));
            W2RSupport.Require(first.Direct!.HasExited && firstOutput.ReadersSettled && firstOutput.PipesDisposed &&
                !second.Direct.HasExited,
                "The first helper reaches EOF and releases its Job while the concurrent ordinary helper remains live");
            secondCancel.Cancel();
            Exception? canceled = null;
            try { await secondOperation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { canceled = exception; }
            W2RSupport.Require(canceled is OperationCanceledException && second.Direct.HasExited &&
                secondOutput.ReadersSettled && secondOutput.PipesDisposed,
                "The second real Job and both pipe readers also settle before harness fallback");
        }
        finally
        {
            releaseFirst.Set(); secondCancel.Cancel();
            await first.StopProcesses(); await second.StopProcesses();
            await W2RProcessFixture.Observe(firstOperation);
            if (secondOperation is not null) await W2RProcessFixture.Observe(secondOperation);
        }
    }

    [Test]
    public async Task P_linux_owned_pipe_close_preserves_nonzero_helper_result()
    {
        if (!OperatingSystem.IsLinux()) return; // The gate targets the Linux fd adapter.
        using var fixture = new W2RProcessFixture { RequireDescendantWitness = true };
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>(); fixture.ScopeStage = stages.Enqueue;
        await fixture.WriteHelper($$"""
            import {spawn} from 'node:child_process';
            import fs from 'node:fs';
            const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], {stdio: ['ignore', 'inherit', 'inherit']});
            fs.writeFileSync({{JsonSerializer.Serialize(fixture.PidFile)}}, String(child.pid));
            child.unref();
            const timer = setInterval(() => {
              if (fs.existsSync({{JsonSerializer.Serialize(fixture.GateFile)}})) { clearInterval(timer); process.exit(23); }
            }, 5);
            """);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var parked = 0;
        fixture.BeforeLinuxPipeLock = (pipe, _) =>
        {
            if (pipe != "stdout" || Interlocked.Exchange(ref parked, 1) != 0) return ValueTask.CompletedTask;
            entered.TrySetResult(); return new ValueTask(releaseRead.Task);
        };
        fixture.ObserveLinuxPipeClose = pipe => { if (pipe == "stdout") closed.TrySetResult(); };
        var observation = new W2ROutputObservation();
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
        var operation = tool.ExecuteUploadAsync(fixture.Invocation);
        Exception? bodyFailure = null, harnessFailure = null;
        try
        {
            await fixture.CaptureGrandchild();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await File.WriteAllTextAsync(fixture.GateFile, "release");
            W2RSupport.Require(await fixture.DirectExit!.WaitAsync(TimeSpan.FromSeconds(5)) == 23,
                "The actual helper must exit nonzero while a descendant retains both native pipes");
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            W2RSupport.Require(!operation.IsCompleted, "The native read remains parked after owned fd closure");
            releaseRead.TrySetResult();
            Exception? failure = null;
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            var descendant = fixture.DescendantExitWitness(); // preserve the first snapshot even on unexpected outcome
            W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_FAILED", InnerException: null },
                "Owned close after nonzero direct exit must not manufacture a competing IO result");
            var readersSettled = observation.ReadersSettled;
            var pipesDisposed = observation.PipesDisposed;
            var directExited = fixture.Direct!.HasExited;
            var descendantExited = descendant.Passed;
            var observerSettled = fixture.DirectExit.IsCompleted;
            if ((!readersSettled || !pipesDisposed || !directExited || !descendantExited || !observerSettled) && OperatingSystem.IsLinux())
                Console.WriteLine($"W2R_P_NATIVE_FIRST_FAILED_WITNESS terms=readersSettled:{readersSettled},pipesDisposed:{pipesDisposed},directHasExited:{directExited},descendantHasExited:{descendantExited},observerSettled:{observerSettled} " +
                    $"operation={operation.Status} outcome={W2RProcessFixture.FixedOutcome(operation, failure)} {descendant.Diagnostic} " +
                    $"readers={observation.ReaderCount} stages={string.Join(',', stages.ToArray())} harnessKills={fixture.HarnessKills}");
            W2RSupport.Require(readersSettled && pipesDisposed && directExited && descendantExited && observerSettled &&
                observation.ReaderCount == 2 && fixture.HarnessKills == 0,
                "Native scope, same-identity descendant, both drains, both endpoints and observer settle before harness cleanup: " + descendant.Diagnostic);
        }
        catch (Exception exception) { bodyFailure = exception; }
        finally
        {
            releaseRead.TrySetResult();
            await File.WriteAllTextAsync(fixture.GateFile, "release");
            try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = exception; }
            await W2RProcessFixture.Observe(operation);
        }
        W2RSupport.ThrowIndependentFailures(bodyFailure, harnessFailure);
    }

    [Test]
    public async Task P_native_read_fault_before_owned_close_keeps_io_precedence()
    {
        using var fixture = new W2RProcessFixture();
        await fixture.WriteHelper("setInterval(() => {}, 1000);");
        var observation = new W2ROutputObservation();
        Stream Wrap(string pipe, Stream actual)
        {
            var counted = observation.Wrap(pipe, actual);
            return pipe == "stdout" ? new ReadFaultStream(counted) : counted;
        }
        var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
            observeOutputStream: Wrap, observeOutputTask: observation.ReaderStarted);
        try
        {
            Exception? failure = null;
            try { await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { failure = exception; }
            W2RSupport.Require(failure is IOException { Message: "GITHUB_ARTIFACT_HELPER_IO_FAILED", InnerException: null } &&
                observation.ReadersSettled && observation.PipesDisposed && fixture.Direct!.HasExited,
                "A genuine read fault before cleanup remains an IO failure after real native settlement");
        }
        finally { await fixture.StopProcesses(); }
    }

    private sealed class ReadFaultStream(Stream actual) : Stream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) =>
            throw new IOException("synthetic-read-secret");
        protected override void Dispose(bool disposing) { if (disposing) actual.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Test]
    public async Task P_windows_fixture_directory_handle_blocks_deletion_after_confirmed_scope_exit()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows directory sharing is the controlled variable.
        var fixture = new W2RProcessFixture();
        var stages = new System.Collections.Concurrent.ConcurrentQueue<string>(); fixture.ScopeStage = stages.Enqueue;
        var observation = new W2ROutputObservation();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? holder = null;
        Exception? bodyFailure = null, harnessFailure = null, fixtureFailure = null;
        async Task HoldWorkspace()
        {
            try
            {
                using var handle = NativeFixture.OpenDirectoryWithoutDeleteSharing(fixture.Workspace);
                W2RSupport.Require(!handle.IsInvalid, "The Windows control must retain a real directory handle");
                acquired.TrySetResult();
                await release.Task;
            }
            catch (Exception exception) { acquired.TrySetException(exception); throw; }
        }
        try
        {
            try
            {
                await fixture.WriteHelper("process.exit(0);");
                holder = HoldWorkspace(); await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var tool = new NodeGitHubArtifactTool(fixture.Workspace, fixture.Start,
                    observeOutputStream: observation.Wrap, observeOutputTask: observation.ReaderStarted);
                await tool.ExecuteUploadAsync(fixture.Invocation).WaitAsync(TimeSpan.FromSeconds(5));
                W2RSupport.Require(fixture.Direct!.HasExited && fixture.DirectExit!.IsCompleted &&
                    observation.ReadersSettled && observation.PipesDisposed &&
                    stages.Contains("confirm-true") && stages.Contains("release-true") && stages.Contains("observer-true"),
                    "Real Job, observer and pipe cleanup must finish before the retained-handle deletion control");
                Exception? blocked = null;
                try { fixture.DeleteOwnedRootForControl(); } catch (Exception exception) { blocked = exception; }
                W2RSupport.Require(blocked is IOException && Directory.Exists(fixture.Workspace),
                    "A test-owned directory handle without delete sharing must reproduce the fixture deletion shape");
                release.TrySetResult(); await holder.WaitAsync(TimeSpan.FromSeconds(5));
                fixture.DeleteOwnedRootForControl();
                W2RSupport.Require(!Directory.Exists(fixture.Workspace), "Fixture deletion succeeds immediately after the explicit handle-release gate");
            }
            catch (Exception exception) { bodyFailure = exception; }
            finally
            {
                release.TrySetResult();
                if (holder is not null) try { await holder.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception exception) { harnessFailure = exception; }
                try { await fixture.StopProcesses(); } catch (Exception exception) { harnessFailure = harnessFailure is null ? exception : new AggregateException(harnessFailure, exception); }
            }
        }
        finally { try { fixture.Dispose(); } catch (Exception exception) { fixtureFailure = exception; } }
        W2RSupport.ThrowIndependentFailures(bodyFailure, harnessFailure, fixtureFailure);
    }

    private static class NativeFixture
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 128)]
        private struct SignalInfo
        {
            [System.Runtime.InteropServices.FieldOffset(16)] internal int Pid;
        }
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int waitid(int kind, uint pid, ref SignalInfo info, int options);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int waitpid(int pid, out int status, int options);
        internal static bool TargetedTestReap(int pid) => waitpid(pid, out _, 1) == pid;
        internal static bool WaitidRetainsExitedChild(int pid)
        {
            var info = new SignalInfo();
            return waitid(1, (uint)pid, ref info, 4 | 1 | 0x01000000) == 0 && info.Pid == pid &&
                File.Exists($"/proc/{pid}/stat");
        }
        internal static bool WaitidSeesEchildAfterReap(int pid)
        {
            var info = new SignalInfo();
            return waitid(1, (uint)pid, ref info, 4 | 1 | 0x01000000) == -1 &&
                System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == 10;
        }
        internal static void RequireLinuxExports()
        {
            W2RSupport.Require(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                System.Runtime.InteropServices.Architecture.X64 &&
                System.Runtime.InteropServices.Marshal.SizeOf<SignalInfo>() == 128,
                "The native scope requires the reviewed glibc x86-64 signal-info ABI");
            W2RSupport.Require(System.Runtime.InteropServices.NativeLibrary.TryLoad("libc.so.6", out var library),
                "The deployed glibc library must be loadable");
            try
            {
                foreach (var symbol in new[] { "gnu_get_libc_version", "posix_spawn", "posix_spawnattr_init", "posix_spawnattr_destroy",
                    "posix_spawnattr_setpgroup", "posix_spawnattr_setflags", "posix_spawnattr_setsigmask", "posix_spawnattr_setsigdefault",
                    "posix_spawn_file_actions_init", "posix_spawn_file_actions_destroy", "posix_spawn_file_actions_addchdir_np",
                    "posix_spawn_file_actions_adddup2", "posix_spawn_file_actions_addclose", "pipe2", "fcntl", "read", "close",
                    "open", "kill", "waitid", "waitpid" })
                    W2RSupport.Require(System.Runtime.InteropServices.NativeLibrary.TryGetExport(library, symbol, out _),
                        "The deployed glibc library must expose every native-scope symbol: " + symbol);
            }
            finally { System.Runtime.InteropServices.NativeLibrary.Free(library); }
        }
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int mkfifo([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, uint mode);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int getpgid(int pid);
        internal static int LinuxProcessGroup(int pid) => getpgid(pid);
        internal static int ProcGroup(int pid)
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture);
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool IsProcessInJob(System.Runtime.InteropServices.SafeHandle process,
            System.Runtime.InteropServices.SafeHandle job,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] out bool result);
        internal static bool InJob(System.Runtime.InteropServices.SafeHandle process,
            System.Runtime.InteropServices.SafeHandle job)
        {
            W2RSupport.Require(IsProcessInJob(process, job, out var result), "The native Job membership query must succeed");
            return result;
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr template);
        internal static Microsoft.Win32.SafeHandles.SafeFileHandle OpenDirectoryWithoutDeleteSharing(string path) =>
            CreateFileW(path, 0x80000000, 0x1 | 0x2, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
    }
}

internal sealed class W2RProcessFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kicktippai-w2r-" + Guid.NewGuid().ToString("N"));
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "kicktippai-github-artifact", Guid.NewGuid().ToString("N"));
    private readonly List<string> _links = [];
    internal string Workspace { get; private set; }
    internal string Sentinel => Path.Combine(_root, "executed");
    internal string PidFile => Path.Combine(_root, "pid");
    internal string GateFile => Path.Combine(_root, "release");
    internal string LeaderReady => Path.Combine(_root, "leader-ready");
    internal string DescendantReady => Path.Combine(_root, "descendant-ready");
    internal string Helper => Path.Combine(Workspace, ".github", "scripts", "context-source-artifact", "context-source-artifact.mjs");
    internal Action<string>? LaunchTransition { get; set; }
    internal Action<string>? ScopeStage { get; set; }
    internal Func<IArtifactProcessScope, IArtifactProcessScope>? DecorateScope { get; set; }
    internal Func<string, CancellationToken, ValueTask>? BeforeLinuxPipeLock { get; set; }
    internal Action<string>? ObserveLinuxPipeClose { get; set; }
    internal bool CaptureWindowsSuspendedChild { get; set; }
    internal bool CaptureLinuxSpawnedChild { get; set; }
    internal Func<string, int?>? LinuxWaitErrorForTests { get; set; }
    internal Action<int, Microsoft.Win32.SafeHandles.SafeFileHandle, Microsoft.Win32.SafeHandles.SafeFileHandle>? ObserveWindowsAssignment { get; set; }
    internal Func<bool>? WindowsAssignmentGateForTests { get; set; }
    internal Process? Direct { get; private set; }
    internal Task<int>? DirectExit { get; private set; }
    internal Process? Grandchild { get; private set; }
    internal bool RequireDescendantWitness { get; set; }
    private string? _retainedLeaderPgid;
    private LinuxIdentity? _leaderIdentity;
    private LinuxIdentity? _descendantIdentity;
    private string? _pidNamespace;
    private bool _earlyIdentityAttempted;
    private string? _earlyIdentityFailure;
    private string? _directAcquisitionFailure;
    private bool _identityReconciliationAttempted;
    private string? _identityReconciliationFailure;
    private DescendantWitness? _firstDescendantWitness;
    internal int HarnessKills { get; private set; }
    internal GitHubArtifactToolInvocation Invocation => new(W2RSupport.Name, Path.Combine(_scratch, "content"), 0, 7);
    internal W2RProcessFixture()
    { Workspace = Path.Combine(_root, "real", "workspace"); Directory.CreateDirectory(Workspace); Directory.CreateDirectory(Invocation.ContentDirectory); }
    internal async Task WriteHelper(string source)
    {
        var helper = Path.Combine(Workspace, ".github", "scripts", "context-source-artifact", "context-source-artifact.mjs");
        Directory.CreateDirectory(Path.GetDirectoryName(helper)!); await File.WriteAllTextAsync(helper, source);
    }
    internal void Indirect(string component)
    {
        if (component == "workspace-ancestor")
        { var link = Path.Combine(_root, "alias"); Directory.CreateSymbolicLink(link, Path.Combine(_root, "real")); _links.Add(link); Workspace = Path.Combine(link, "workspace"); return; }
        var parts = new[] { ".github", "scripts", "context-source-artifact", "context-source-artifact.mjs" };
        var index = component == "helper" ? 3 : Array.IndexOf(parts, component);
        var source = parts.Take(index + 1).Aggregate(Workspace, Path.Combine);
        var target = Path.Combine(_root, "external-" + index + (index == 3 ? ".mjs" : ""));
        if (index == 3) { File.Move(source, target); File.CreateSymbolicLink(source, target); }
        else { Directory.Move(source, target); Directory.CreateSymbolicLink(source, target); }
        _links.Add(source);
    }
    internal IArtifactProcessScope Start(ValidatedArtifactLaunch launch, CancellationToken token)
    {
        // This decorates the production native launcher. The independent Process is
        // retained before Windows resumes and while Linux owns the unreaped leader.
        var launcher = new ArtifactProcessScopeLauncher
        {
            ObserveTransition = LaunchTransition,
            BeforeLinuxPipeLock = BeforeLinuxPipeLock,
            ObserveLinuxPipeClose = ObserveLinuxPipeClose,
            ObserveWindowsSuspendedChild = CaptureWindowsSuspendedChild ? pid =>
            {
                Direct = Process.GetProcessById(pid);
                _ = Direct.SafeHandle;
            } : null,
            ObserveLinuxSpawnedChild = CaptureLinuxSpawnedChild ? pid =>
            {
                // This callback precedes all pipe-transfer fault hooks and native
                // rollback still owns the unreaped child. Procfs and GetProcessById
                // are observations only; neither waits for the child.
                _earlyIdentityAttempted = true;
                try
                {
                    var identity = ReadLinuxIdentity(pid);
                    var pidNamespace = ReadPidNamespace();
                    if (identity.Pgid != pid || pidNamespace.Length == 0) _earlyIdentityFailure = "unexpected-pgid-or-namespace";
                    else
                    {
                        _leaderIdentity = identity; _pidNamespace = pidNamespace;
                        _retainedLeaderPgid = identity.Pgid.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception exception)
                { _earlyIdentityFailure = "capture-" + exception.GetType().Name; }
                try { Direct = Process.GetProcessById(pid); }
                catch (Exception exception)
                { _directAcquisitionFailure = "process-" + exception.GetType().Name; }
            } : null,
            LinuxWaitErrorForTests = LinuxWaitErrorForTests,
            ObserveWindowsAssignment = ObserveWindowsAssignment,
            WindowsAssignmentGateForTests = WindowsAssignmentGateForTests,
            ObserveIdentity = pid =>
            {
                if (!CaptureLinuxSpawnedChild) Direct ??= Process.GetProcessById(pid);
                if (OperatingSystem.IsWindows()) _ = Direct.SafeHandle;
                if (OperatingSystem.IsLinux())
                {
                    if (CaptureLinuxSpawnedChild)
                    {
                        _identityReconciliationAttempted = true;
                        try
                        {
                            var later = ReadLinuxIdentity(pid);
                            var laterNamespace = ReadPidNamespace();
                            if (_leaderIdentity is not { } early || _pidNamespace is null ||
                                later.Pid != early.Pid || later.StartTime != early.StartTime ||
                                later.Pgid != early.Pgid || laterNamespace != _pidNamespace)
                                _identityReconciliationFailure = "identity-mismatch-or-unavailable-baseline";
                        }
                        catch (Exception exception)
                        { _identityReconciliationFailure = "reconcile-" + exception.GetType().Name; }
                    }
                    else
                    {
                        _leaderIdentity = ReadLinuxIdentity(pid);
                        _pidNamespace = ReadPidNamespace();
                    }
                    _retainedLeaderPgid = _leaderIdentity?.Pgid.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        };
        var scope = launcher.Start(launch, token);
        DirectExit = scope.DirectExit;
        var staged = ScopeStage is null ? scope : new StageScope(scope, ScopeStage);
        return DecorateScope?.Invoke(staged) ?? staged;
    }
    internal async Task WaitForPidFile()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(PidFile)) await Task.Delay(10, watchdog.Token);
        // File readiness is synchronization only; deadline assertions use explicit cancellation gates.
        while (new FileInfo(PidFile).Length == 0) await Task.Delay(10, watchdog.Token);
    }
    internal async Task WaitForFile(string path)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(path)) await Task.Delay(10, watchdog.Token);
    }
    internal async Task CaptureGrandchild()
    {
        await WaitForPidFile();
        var pid = int.Parse(await File.ReadAllTextAsync(PidFile), System.Globalization.CultureInfo.InvariantCulture);
        if (OperatingSystem.IsLinux())
        {
            var leader = _leaderIdentity ?? throw new InvalidDataException("Missing retained leader identity");
            W2RSupport.Require(leader.Pid == Direct?.Id && leader.Pgid == leader.Pid &&
                leader.State is not ('Z' or 'X') && _pidNamespace is not null,
                "Valid retained leader PID/start-time/PGID and PID namespace must precede descendant capture");
            var descendant = ReadLinuxIdentity(pid);
            W2RSupport.Require(descendant.Pgid == leader.Pgid && descendant.State is not ('Z' or 'X'),
                "The first descendant PID/start-time/PGID baseline must be live in the retained leader group");
            _descendantIdentity = descendant;
        }
        Grandchild = Process.GetProcessById(pid);
    }
    internal sealed record DescendantWitness(bool Passed, string Diagnostic);
    private sealed record LinuxIdentity(int Pid, char State, int Ppid, int Pgid, ulong StartTime)
    {
        internal string Diagnostic => $"pid={Pid},start={StartTime},pgid={Pgid},ppid={Ppid},state={State}";
    }
    internal sealed record DirectTerminalWitness(bool ProductReapCompatible, string Diagnostic);
    internal DirectTerminalWitness CaptureDirectTerminalWitness()
    {
        var baseline = _leaderIdentity;
        var status = "unavailable-baseline";
        LinuxIdentity? terminal = null;
        if (baseline is not null && _pidNamespace is not null)
        {
            try
            {
                if (ReadPidNamespace() != _pidNamespace) status = "pid-namespace-changed";
                else
                {
                    try { terminal = ReadLinuxIdentity(baseline.Pid); status = "present"; }
                    catch (FileNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                    catch (DirectoryNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            { status = "unavailable-" + exception.GetType().Name; }
        }
        var verdict = terminal is null ? status :
            terminal.Pid != baseline!.Pid || terminal.StartTime != baseline.StartTime ? "identity-replaced" :
            terminal.Pgid != baseline.Pgid ? "escaped-group" :
            terminal.State is 'Z' or 'X' ? "same-identity-nonexecuting-unreaped" : "same-identity-live";
        return new(verdict == "disappeared" && _earlyIdentityAttempted && _earlyIdentityFailure is null &&
            _directAcquisitionFailure is null && _identityReconciliationFailure is null,
            $"directIdentity={verdict} baseline={baseline?.Diagnostic ?? "absent"} terminal={terminal?.Diagnostic ?? status} " +
            $"earlyCapture={(_earlyIdentityAttempted ? _earlyIdentityFailure ?? "ok" : "not-reached")} " +
            $"independentProcess={(_earlyIdentityAttempted ? _directAcquisitionFailure ?? "ok" : "not-reached")} " +
            $"laterReconciliation={(_identityReconciliationAttempted ? _identityReconciliationFailure ?? "ok" : "not-reached")} " +
            $"pidNamespace={_pidNamespace ?? "absent"}");
    }
    internal DescendantWitness DescendantExitWitness()
    {
        if (_firstDescendantWitness is not null) return _firstDescendantWitness;
        if (!OperatingSystem.IsLinux())
            return _firstDescendantWitness = new(Grandchild?.HasExited == true,
                $"windowsDescendantHasExited={ExitState(Grandchild)} harnessKills={HarnessKills}");
        var baseline = _descendantIdentity;
        var leader = _leaderIdentity;
        var status = "unavailable-baseline";
        LinuxIdentity? terminal = null;
        if (baseline is not null && leader is not null)
        {
            try
            {
                if (ReadPidNamespace() != _pidNamespace) status = "pid-namespace-changed";
                else
                {
                    try { terminal = ReadLinuxIdentity(baseline.Pid); status = "present"; }
                    catch (FileNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                    catch (DirectoryNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            { status = "unavailable-" + exception.GetType().Name; }
        }
        var verdict = status == "disappeared" ? "disappeared" : terminal is null ? status :
            terminal.StartTime != baseline!.StartTime || terminal.Pid != baseline.Pid ? "identity-replaced" :
            terminal.Pgid != baseline.Pgid ? "escaped-group" :
            terminal.State is 'Z' or 'X' ? "same-identity-nonexecuting" : "same-identity-live";
        var passed = verdict is "disappeared" or "same-identity-nonexecuting";
        var handle = ExitState(Grandchild); // diagnostic only, after the decisive procfs snapshot
        return _firstDescendantWitness = new(passed,
            $"descendantVerdict={verdict} baseline={baseline?.Diagnostic ?? "absent"} terminal={terminal?.Diagnostic ?? status} " +
            $"leader={leader?.Diagnostic ?? "absent"} pidNamespace={_pidNamespace ?? "absent"} descendantHasExited={handle} harnessKills={HarnessKills}");
    }
    private static string ReadPidNamespace() => new FileInfo("/proc/self/ns/pid").LinkTarget ?? throw new InvalidDataException("Missing procfs PID namespace");
    private static bool ProcfsAvailable()
    {
        try { return ReadLinuxIdentity(Environment.ProcessId).Pid == Environment.ProcessId && ReadPidNamespace().Length > 0; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    }
    private static LinuxIdentity ReadLinuxIdentity(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        var open = stat.IndexOf('('); var close = stat.LastIndexOf(')');
        if (open <= 1 || close <= open || close + 2 >= stat.Length || stat[open - 1] != ' ' || stat[close + 1] != ' ' ||
            !int.TryParse(stat.AsSpan(0, open - 1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var actualPid) || actualPid != pid)
            throw new InvalidDataException("Malformed procfs PID/command");
        var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || fields[0].Length != 1 || !"RSDTtZXxKWPI".Contains(fields[0][0]) ||
            !int.TryParse(fields[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ppid) ||
            !int.TryParse(fields[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pgid) ||
            !ulong.TryParse(fields[19], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var start) ||
            ppid < 0 || pgid <= 0 || start == 0)
            throw new InvalidDataException("Malformed procfs identity fields");
        return new(actualPid, fields[0][0], ppid, pgid, start);
    }
    internal async Task StopProcesses()
    {
        Exception? descendantCleanupFailure = null;
        try
        {
            if (OperatingSystem.IsLinux()) await StopLinuxDescendant();
            else
            {
                // Windows still owns the independent Process handle and Job evidence.
                if (Grandchild is null && File.Exists(PidFile) && int.TryParse(await File.ReadAllTextAsync(PidFile), out var pid))
                {
                    try { Grandchild = Process.GetProcessById(pid); } catch (ArgumentException) { }
                }
                if (Grandchild is not null)
                {
                    try { if (!Grandchild.HasExited) { HarnessKills++; Grandchild.Kill(true); } await Grandchild.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) when (Grandchild.HasExited) { }
                }
            }
        }
        catch (Exception exception) { descendantCleanupFailure = exception; }
        Exception? directCleanupFailure = null;
        if (Direct is { } process)
        {
            try
            {
                if (OperatingSystem.IsLinux()) await StopLinuxDirect(process);
                else
                {
                    if (!process.HasExited) { HarnessKills++; process.Kill(true); }
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (InvalidOperationException) when (!OperatingSystem.IsLinux()) { /* Windows bridge disposed its retained handle */ }
            catch (System.ComponentModel.Win32Exception) when (!OperatingSystem.IsLinux() && process.HasExited) { /* Windows process exited between HasExited and Kill */ }
            catch (Exception exception) { directCleanupFailure = exception; }
        }
        W2RSupport.ThrowIndependentFailures(descendantCleanupFailure, directCleanupFailure);
    }
    private async Task StopLinuxDirect(Process process)
    {
        var baseline = _leaderIdentity ?? throw new InvalidDataException("Missing retained direct-child identity at cleanup");
        if (_pidNamespace is null || ReadPidNamespace() != _pidNamespace)
            throw new InvalidDataException("PID namespace unavailable or changed at direct-child cleanup");
        LinuxIdentity current;
        try { current = ReadLinuxIdentity(baseline.Pid); }
        catch (FileNotFoundException) when (ProcfsAvailable()) { return; }
        catch (DirectoryNotFoundException) when (ProcfsAvailable()) { return; }
        if (current.Pid != baseline.Pid || current.StartTime != baseline.StartTime || current.Pgid != baseline.Pgid)
            throw new InvalidDataException("Direct-child identity changed at cleanup; refusing stale PID kill");
        if (current.State is 'Z' or 'X') return; // exited, retained zombie; only the native owner may reap
        var beforeKill = ReadLinuxIdentity(baseline.Pid);
        if (beforeKill.StartTime != baseline.StartTime || beforeKill.Pgid != baseline.Pgid)
            throw new InvalidDataException("Direct-child identity changed before cleanup kill");
        if (beforeKill.State is 'Z' or 'X') return;
        HarnessKills++;
        process.Kill(true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            try
            {
                var after = ReadLinuxIdentity(baseline.Pid);
                if (after.StartTime != baseline.StartTime || after.Pgid != baseline.Pgid)
                    throw new InvalidDataException("Direct-child identity changed after cleanup kill");
                if (after.State is 'Z' or 'X') return; // nonexecuting; do not claim a fixture or product reap
            }
            catch (FileNotFoundException) when (ProcfsAvailable()) { return; }
            catch (DirectoryNotFoundException) when (ProcfsAvailable()) { return; }
            await Task.Delay(10, deadline.Token);
        }
    }
    private async Task StopLinuxDescendant()
    {
        if (!RequireDescendantWitness) return;
        if (_pidNamespace is null || ReadPidNamespace() != _pidNamespace)
            throw new InvalidDataException("PID namespace unavailable or changed at descendant cleanup");
        var baseline = _descendantIdentity;
        if (baseline is null && File.Exists(PidFile))
        {
            // On setup failure, recover only a child still attached to the exact live leader.
            if (!int.TryParse(await File.ReadAllTextAsync(PidFile), out var pid) || _leaderIdentity is not { } leader)
                throw new InvalidDataException("Cannot identify descendant for bounded setup cleanup");
            var currentLeader = ReadLinuxIdentity(leader.Pid);
            var candidate = ReadLinuxIdentity(pid);
            if (currentLeader.StartTime != leader.StartTime || candidate.Ppid != leader.Pid || candidate.Pgid != leader.Pgid)
                throw new InvalidDataException("Unknown descendant identity at setup cleanup; refusing stale PID kill");
            baseline = candidate;
            _descendantIdentity = candidate;
        }
        if (baseline is null) return;
        // A decisive first verdict cannot be changed by fallback cleanup. A witnessed
        // zombie is retained by its parent; this fixture does not reap it.
        if (_firstDescendantWitness is { Passed: true }) return;
        LinuxIdentity current;
        try { current = ReadLinuxIdentity(baseline.Pid); }
        catch (FileNotFoundException) when (ProcfsAvailable()) { return; }
        catch (DirectoryNotFoundException) when (ProcfsAvailable()) { return; }
        if (current.Pid != baseline.Pid || current.StartTime != baseline.StartTime || current.Pgid != baseline.Pgid)
            throw new InvalidDataException("Descendant identity changed at cleanup; refusing stale PID kill");
        if (current.State is 'Z' or 'X') return;
        // Check identity immediately again before using the PID. The bounded recovery
        // never uses an unknown or replaced PID as its target.
        var beforeKill = ReadLinuxIdentity(baseline.Pid);
        if (beforeKill.StartTime != baseline.StartTime || beforeKill.Pgid != baseline.Pgid)
            throw new InvalidDataException("Descendant identity changed before cleanup kill");
        if (beforeKill.State is 'Z' or 'X') return;
        var process = Grandchild ?? Process.GetProcessById(baseline.Pid);
        HarnessKills++;
        process.Kill(true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            try
            {
                var after = ReadLinuxIdentity(baseline.Pid);
                if (after.StartTime != baseline.StartTime || after.Pgid != baseline.Pgid)
                    throw new InvalidDataException("Descendant identity changed after cleanup kill");
                if (after.State is 'Z' or 'X') return;
            }
            catch (FileNotFoundException) when (ProcfsAvailable()) { return; }
            catch (DirectoryNotFoundException) when (ProcfsAvailable()) { return; }
            await Task.Delay(10, deadline.Token);
        }
    }
    internal static async Task Observe(Task operation) { try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch { } }
    internal void DeleteOwnedRootForControl() => DeleteOwned(_root, Path.GetTempPath(), "kicktippai-w2r-");
    internal static string ExitState(Process? process)
    {
        if (process is null) return "absent";
        try { return process.HasExited ? "exited" : "live"; }
        catch (InvalidOperationException) { return "unavailable"; }
        catch (System.ComponentModel.Win32Exception) { return "unavailable"; }
    }
    internal string LinuxDescendantWitnessDiagnostic()
    {
        var leaderPid = Direct?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent";
        var descendantPid = Grandchild?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent";
        var descendant = Grandchild is null ? ("absent", "absent", "absent", "absent", "absent") : TryReadProcStat(Grandchild.Id);
        return $"leaderPid={leaderPid} leaderPgid={_retainedLeaderPgid ?? "unavailable"} " +
            $"leaderHasExited={ExitState(Direct)} descendantPid={descendantPid} descendantHasExited={ExitState(Grandchild)} " +
            $"descendantProcState={descendant.Item1} descendantPpid={descendant.Item2} descendantPgid={descendant.Item3} descendantStartTime={descendant.Item4} descendantProc={descendant.Item5}";
    }
    internal string LinuxDirectWitnessDiagnostic()
    {
        var pid = Direct?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent";
        var stat = Direct is null ? ("absent", "absent", "absent", "absent", "absent") : TryReadProcStat(Direct.Id);
        return $"directPid={pid} retainedLeaderPgid={_retainedLeaderPgid ?? "unavailable"} directHasExited={ExitState(Direct)} " +
            $"directProcState={stat.Item1} directPpid={stat.Item2} directPgid={stat.Item3} directStartTime={stat.Item4} directProc={stat.Item5}";
    }
    internal string LinuxDirectIdentityDiagnostic()
    {
        if (!OperatingSystem.IsLinux()) return $"directHasExited={ExitState(Direct)}";
        var baseline = _leaderIdentity;
        if (baseline is null) return $"directIdentity=unavailable-baseline directHasExited={ExitState(Direct)}";
        LinuxIdentity? terminal = null;
        var status = "unavailable";
        try
        {
            if (ReadPidNamespace() != _pidNamespace) status = "pid-namespace-changed";
            else
            {
                try { terminal = ReadLinuxIdentity(baseline.Pid); status = "present"; }
                catch (FileNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                catch (DirectoryNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        { status = "unavailable-" + exception.GetType().Name; }
        var verdict = terminal is null ? status :
            terminal.Pid != baseline.Pid || terminal.StartTime != baseline.StartTime ? "identity-replaced" :
            terminal.Pgid != baseline.Pgid ? "escaped-group" :
            terminal.State is 'Z' or 'X' ? "same-identity-nonexecuting-unreaped" : "same-identity-live";
        return $"directIdentity={verdict} leaderBaseline={baseline.Diagnostic} leaderTerminal={terminal?.Diagnostic ?? status} " +
            $"directHasExited={ExitState(Direct)}";
    }
    internal sealed record TimeoutIdentitySnapshot(bool Complete, bool Residual, string Verdict, string Diagnostic);
    internal TimeoutIdentitySnapshot LinuxDirectIdentitySnapshotForTimeout()
    {
        if (!OperatingSystem.IsLinux())
        {
            var state = ExitState(Direct);
            return new(state is "exited" or "live", state != "exited", state,
                $"directPid={Direct?.Id.ToString() ?? "absent"},directHandle={state}");
        }
        var baseline = _leaderIdentity;
        LinuxIdentity? terminal = null;
        var status = baseline is null ? (_earlyIdentityAttempted ? "baseline-unavailable" : "spawn-callback-unreached") : "unavailable";
        try
        {
            if (baseline is null) { /* No retrospective baseline may replace the early capture. */ }
            else if (_pidNamespace is null || ReadPidNamespace() != _pidNamespace) status = "pid-namespace-unavailable-or-changed";
            else
            {
                try { terminal = ReadLinuxIdentity(baseline.Pid); status = "present"; }
                catch (FileNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
                catch (DirectoryNotFoundException) { status = ProcfsAvailable() ? "disappeared" : "procfs-unavailable"; }
            }
        }
        catch (Exception exception) { status = "unavailable-" + exception.GetType().Name; }
        var verdict = terminal is null ? status :
            terminal.Pid != baseline!.Pid || terminal.StartTime != baseline.StartTime ? "identity-replaced" :
            terminal.Pgid != baseline.Pgid ? "escaped-group" :
            terminal.State is 'Z' or 'X' ? "same-identity-nonexecuting-unreaped" : "same-identity-live";
        // This snapshot intentionally does not query Process.HasExited, waitid or
        // waitpid while the native owner may still hold the child's exit status.
        var acquisition = _earlyIdentityAttempted ? _directAcquisitionFailure ?? (Direct is null ? "missing" : "ok") : "not-reached";
        var capture = _earlyIdentityAttempted ? _earlyIdentityFailure ?? (baseline is null ? "missing" : "ok") : "not-reached";
        var reconciliation = _identityReconciliationAttempted ? _identityReconciliationFailure ?? "ok" : "not-reached";
        var complete = _earlyIdentityAttempted && baseline is not null && capture == "ok" && acquisition == "ok" &&
            _identityReconciliationFailure is null && (_identityReconciliationAttempted || DirectExit is null) &&
            verdict is ("disappeared" or "same-identity-live" or "same-identity-nonexecuting-unreaped");
        return new(complete, !complete || verdict != "disappeared", verdict,
            $"baseline={baseline?.Diagnostic ?? "unavailable"},terminal={terminal?.Diagnostic ?? status},verdict={verdict}," +
            $"capture={capture},acquisition={acquisition},reconciliation={reconciliation},identityComplete={complete}," +
            $"pidNamespace={_pidNamespace ?? "absent"},retainedPid={Direct?.Id.ToString() ?? "absent"}");
    }
    private static (string State, string Ppid, string Pgid, string StartTime, string Presence) TryReadProcStat(int pid)
    {
        try
        {
            var path = $"/proc/{pid}/stat";
            if (!File.Exists(path)) return ("unavailable", "unavailable", "unavailable", "unavailable", "missing");
            var stat = File.ReadAllText(path); var close = stat.LastIndexOf(')');
            if (close < 0 || close + 2 >= stat.Length) return ("invalid", "invalid", "invalid", "invalid", "malformed");
            var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 19 ? (fields[0], fields[1], fields[2], fields[19], "present") : ("invalid", "invalid", "invalid", "invalid", "malformed");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return ("unavailable", "unavailable", "unavailable", "unavailable", "unreadable"); }
    }
    internal static string FixedOutcome(Task? operation, Exception? observedFailure)
    {
        var failure = observedFailure ?? operation?.Exception?.GetBaseException();
        return failure switch
        {
            IOException { Message: "GITHUB_ARTIFACT_HELPER_TIMEOUT" } => "helper-timeout",
            IOException { Message: "GITHUB_ARTIFACT_HELPER_CLEANUP_FAILED" } => "cleanup-failed",
            IOException { Message: "GITHUB_ARTIFACT_HELPER_IO_FAILED" } => "io-failed",
            IOException { Message: "GITHUB_ARTIFACT_HELPER_FAILED" } => "helper-failed",
            IOException { Message: "GITHUB_ARTIFACT_HELPER_OUTPUT_LIMIT" } => "output-limit",
            OperationCanceledException => "canceled",
            null => operation?.IsCompletedSuccessfully == true ? "success" : operation?.IsCanceled == true ? "canceled" : "pending",
            _ => "fault-" + failure.GetType().Name
        };
    }
    public void Dispose()
    {
        Grandchild?.Dispose(); Direct?.Dispose();
        foreach (var link in _links) { if (Directory.Exists(link)) Directory.Delete(link); else if (File.Exists(link)) File.Delete(link); }
        DeleteOwned(_root, Path.GetTempPath(), "kicktippai-w2r-");
        DeleteOwned(_scratch, Path.Combine(Path.GetTempPath(), "kicktippai-github-artifact"), "");
    }
    private static void DeleteOwned(string target, string parent, string prefix)
    {
        var full = Path.GetFullPath(target); var basePath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
        W2RSupport.Require(Path.GetDirectoryName(full) == basePath && Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal), "Fixture deletion must stay in its unique owned directory");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
    private sealed class StageScope(IArtifactProcessScope actual, Action<string> stage) : IArtifactProcessScope
    {
        public int ProcessId => actual.ProcessId; public Task<int> DirectExit => actual.DirectExit;
        public Stream StandardOutput => actual.StandardOutput; public Stream StandardError => actual.StandardError;
        public void TerminateScope()
        {
            stage("terminate-enter");
            try { actual.TerminateScope(); stage("terminate-return"); }
            catch { stage("terminate-throw"); throw; }
        }
        public async Task<bool> ConfirmTerminatedAsync(CancellationToken token)
        {
            stage("confirm-enter");
            try { var result = await actual.ConfirmTerminatedAsync(token); stage(result ? "confirm-true" : "confirm-false"); return result; }
            catch { stage("confirm-throw"); throw; }
        }
        public async Task<bool> ReleaseAsync(CancellationToken token)
        {
            stage("release-enter");
            try { var result = await actual.ReleaseAsync(token); stage(result ? "release-true" : "release-false"); return result; }
            catch { stage("release-throw"); throw; }
        }
        public async Task<bool> SettleObserverAsync(CancellationToken token)
        {
            stage("observer-enter");
            try { var result = await actual.SettleObserverAsync(token); stage(result ? "observer-true" : "observer-false"); return result; }
            catch { stage("observer-throw"); throw; }
        }
        public void Dispose()
        {
            stage("dispose-enter");
            try { actual.Dispose(); stage("dispose-return"); }
            catch { stage("dispose-throw"); throw; }
        }
    }
}

// W2R Group Z: named structural attacks. Every case first proves the independent
// baseline, then changes only its modeled field/layout. No signature-scanning XOR.
public class GitHubArtifactW2RZTests
{
    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task Z_structured_store_baseline_preserves_exact_entries(bool html)
    {
        var model = new W2RZip(); if (html) model.Entries.Add(new("club-elo/source.html", "<html>fixture</html>"u8.ToArray()));
        var result = await W2RSupport.Probe(model.Build().Bytes);
        await Assert.That(result.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        foreach (var entry in model.Entries)
            W2RSupport.Require(result.Entries.Single(actual => actual.Path == entry.Name).Bytes.SequenceEqual(entry.Payload), "Byte-exact baseline payload " + entry.Name);
    }

    [Test]
    [Arguments("local-version")] [Arguments("central-version")] [Arguments("unsupported-version")]
    [Arguments("local-time")] [Arguments("local-date")] [Arguments("central-time")] [Arguments("central-date")]
    [Arguments("creator-version")] [Arguments("creator-platform")] [Arguments("internal-attributes")]
    [Arguments("unix-symlink")] [Arguments("unix-directory")] [Arguments("unix-fifo")] [Arguments("unix-socket")]
    [Arguments("unix-block")] [Arguments("unix-character")]
    [Arguments("dos-directory")] [Arguments("dos-volume")] [Arguments("dos-device")] [Arguments("dos-reparse-only")]
    [Arguments("encrypted")] [Arguments("reserved-flags")] [Arguments("local-flags")] [Arguments("local-method")]
    [Arguments("local-crc")] [Arguments("local-compressed")] [Arguments("local-expanded")]
    [Arguments("unsupported-method")] [Arguments("central-crc-and-local-crc")]
    [Arguments("local-extra")] [Arguments("central-extra")] [Arguments("zip64-extra")]
    [Arguments("malformed-local-extra")] [Arguments("malformed-central-extra")]
    [Arguments("zip64-compressed")] [Arguments("zip64-expanded")] [Arguments("zip64-offset")]
    [Arguments("entry-comment")] [Arguments("local-name")] [Arguments("utf8-name")]
    [Arguments("stored-size-inequality")] [Arguments("aggregate-declared-bomb")]
    public async Task Z_named_metadata_violation_is_conflict(string attack)
    {
        await AssertBaseline();
        var zip = new W2RZip(); var entry = zip.Entries[0];
        switch (attack)
        {
            case "local-version": entry.LocalVersion = 10; break;
            case "central-version": entry.Version = 10; entry.LocalVersion = 20; break;
            case "unsupported-version": entry.Version = 63; entry.LocalVersion = 63; break;
            case "local-time": entry.LocalTime = 1; break;
            case "local-date": entry.LocalDate = 0x5822; break;
            case "central-time": entry.Time = 1; entry.LocalTime = 0; break;
            case "central-date": entry.Date = 0x5822; entry.LocalDate = 0x5821; break;
            case "creator-version": entry.Creator = 0x00ff; break;
            case "creator-platform": entry.Creator = 0x1314; break;
            case "internal-attributes": entry.InternalAttributes = 0x8000; break;
            case "unix-symlink": entry.Creator = 0x0314; entry.Attributes = 0xa1a40000; break;
            case "unix-directory": entry.Creator = 0x0314; entry.Attributes = 0x41a40000; break;
            case "unix-fifo": entry.Creator = 0x0314; entry.Attributes = 0x11a40000; break;
            case "unix-socket": entry.Creator = 0x0314; entry.Attributes = 0xc1a40000; break;
            case "unix-block": entry.Creator = 0x0314; entry.Attributes = 0x61a40000; break;
            case "unix-character": entry.Creator = 0x0314; entry.Attributes = 0x21a40000; break;
            case "dos-directory": entry.Attributes = 0x10; break;
            case "dos-volume": entry.Attributes = 0x08; break;
            case "dos-device": entry.Attributes = 0x40; break;
            case "dos-reparse-only": entry.Attributes = 0x400; break;
            case "encrypted": entry.Flags = 1; break;
            case "reserved-flags": entry.Flags = 0x4000; break;
            case "local-flags": entry.LocalFlags = 0x800; break;
            case "local-method": entry.LocalMethod = 8; break;
            case "local-crc": entry.LocalCrc = W2RZip.Crc(entry.Payload) ^ 1; break;
            case "local-compressed": entry.LocalCompressed = 3; break;
            case "local-expanded": entry.LocalExpanded = 3; break;
            case "unsupported-method": entry.Method = 99; break;
            case "central-crc-and-local-crc": entry.CrcOverride = W2RZip.Crc(entry.Payload) ^ 1; break;
            case "local-extra": entry.LocalExtra = [0xfe, 0xca, 0, 0]; break;
            case "central-extra": entry.CentralExtra = [0xfe, 0xca, 0, 0]; break;
            case "malformed-local-extra": entry.LocalExtra = [0xfe, 0xca, 4, 0, 0x42]; break; // TLV claims four bytes, contains one
            case "malformed-central-extra": entry.CentralExtra = [0xfe, 0xca, 4, 0, 0x42]; break;
            case "zip64-extra": entry.CentralExtra = [1, 0, 8, 0, 2, 0, 0, 0, 0, 0, 0, 0]; break;
            case "zip64-compressed": entry.Compressed = uint.MaxValue; break;
            case "zip64-expanded": entry.Expanded = uint.MaxValue; break;
            case "zip64-offset": entry.OffsetOverride = uint.MaxValue; break;
            case "entry-comment": entry.Comment = [120]; break;
            case "local-name": entry.LocalName = "manifest.jsox"u8.ToArray(); break;
            case "utf8-name": entry.NameBytes = [0xff]; break;
            case "stored-size-inequality": entry.Expanded = 3; break;
            case "aggregate-declared-bomb": entry.Expanded = 3 * 1024 * 1024 + 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(attack));
        }
        await AssertConflict(zip.Build().Bytes);
    }

    [Test]
    [Arguments("prefix")] [Arguments("local-gap")] [Arguments("aliased-local-name")] [Arguments("overlapping-local-range")]
    [Arguments("central-gap")] [Arguments("central-size")] [Arguments("central-to-eocd-gap")]
    [Arguments("trailing-byte")] [Arguments("eocd-comment-length")] [Arguments("disk")] [Arguments("count")]
    [Arguments("payload-crosses-central")]
    public async Task Z_named_layout_violation_is_conflict(string attack)
    {
        await AssertBaseline();
        var zip = new W2RZip();
        switch (attack)
        {
            case "prefix": zip.Prefix = [0x7f]; break;
            case "local-gap": zip.GapBeforeSecond = [0x7f]; break;
            case "aliased-local-name": zip.Entries[1].OffsetOverride = 0; break; // alias cannot bind two distinct allowed names
            case "overlapping-local-range":
                // Embed a COMPLETE valid second local record inside the first payload.
                // Both names/CRCs/sizes bind correctly; only the local interval union overlaps.
                var source = zip.Build();
                zip.Entries[0].Payload = new byte[] { 7, 7 }.Concat(source.Bytes[source.LocalOffsets[1]..source.CentralOffsets[0]]).ToArray();
                zip.Entries[1].OffsetOverride = checked((uint)(source.PayloadOffsets[0] + 2));
                zip.Entries[1].SkipLocal = true;
                break;
            case "central-gap": zip.CentralGap = [0x7f]; break;
            case "central-size": zip.CentralSizeAdjustment = -1; break;
            case "central-to-eocd-gap": zip.EocdGap = [0x7f]; break;
            case "trailing-byte": zip.Trailing = [0x7f]; break;
            case "eocd-comment-length": zip.EocdCommentLength = 1; break;
            case "disk": zip.Disk = 1; break;
            case "count": zip.EntryCount = 3; break;
            case "payload-crosses-central":
                var crossing = zip.Entries[1];
                crossing.Payload = Encoding.ASCII.GetBytes(new string('a', 64));
                crossing.Compressed = 65; crossing.Expanded = 65; // within the hash entry's 65-byte limit
                // The declared 65th byte is the first central-signature byte (0x50).
                // CRC covers all declared bytes, including that deliberately overlapped byte.
                crossing.CrcOverride = W2RZip.Crc(crossing.Payload.Concat(new byte[] { 0x50 }).ToArray());
                break;
            default: throw new ArgumentOutOfRangeException(nameof(attack));
        }
        await AssertConflict(zip.Build().Bytes);
    }

    [Test]
    [Arguments("manifest.json", 512 * 1024)] [Arguments("bundle.sha256", 65)] [Arguments("club-elo/source.html", 2 * 1024 * 1024)]
    public async Task Z_per_entry_exact_limit_passes_and_plus_one_is_conflict(string name, int limit)
    {
        var zip = new W2RZip(); var entry = zip.Entries.SingleOrDefault(value => value.Name == name);
        if (entry is null) { entry = new(name, []); zip.Entries.Add(entry); }
        entry.Payload = new byte[limit];
        await Assert.That((await W2RSupport.Probe(zip.Build().Bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        entry.Payload = new byte[limit + 1]; await AssertConflict(zip.Build().Bytes);
        // The sum of allowed per-entry maxima is <3 MiB, so no honest aggregate-limit positive exists.
    }

    [Test]
    [Arguments("missing-manifest")] [Arguments("missing-hash")] [Arguments("duplicate")]
    [Arguments("case-collision")] [Arguments("directory")] [Arguments("unknown-file")]
    [Arguments("rooted")] [Arguments("drive")] [Arguments("backslash")] [Arguments("empty")]
    [Arguments("dot")] [Arguments("dotdot")]
    public async Task Z_named_entry_set_violation_is_conflict(string attack)
    {
        await AssertBaseline(); var zip = new W2RZip();
        switch (attack)
        {
            case "missing-manifest": zip.Entries.RemoveAt(0); break;
            case "missing-hash": zip.Entries.RemoveAt(1); break;
            case "duplicate": zip.Entries.Add(new("manifest.json", "{}"u8.ToArray())); break;
            case "case-collision": zip.Entries.Add(new("Manifest.json", "{}"u8.ToArray())); break;
            default:
                zip.Entries.Add(new(attack switch { "directory" => "unexpected/", "unknown-file" => "unknown.txt", "rooted" => "/manifest.json", "drive" => "C:/manifest.json", "backslash" => "club-elo\\source.html", "empty" => "", "dot" => "./manifest.json", "dotdot" => "../manifest.json", _ => throw new ArgumentOutOfRangeException(nameof(attack)) }, []));
                break;
        }
        await AssertConflict(zip.Build().Bytes);
    }

    [Test]
    [Arguments("local-fixed")] [Arguments("local-name")] [Arguments("local-payload")]
    [Arguments("central-fixed")] [Arguments("central-name")]
    public async Task Z_record_boundary_truncation_preserves_terminal_envelope_and_unrelated_records(string boundary)
    {
        await AssertBaseline();
        var baseline = new W2RZip().Build();
        var malformed = W2RZip.CutRecordBoundary(baseline, boundary);
        // The builder verifies a complete 22-byte terminal EOCD, the exact adjusted
        // central offset/size, and byte-identical unaffected first local/central records.
        // These cannot fail merely because the whole archive lost its terminal EOCD.
        await AssertConflict(malformed);
    }
    [Test]
    [Arguments(false)] [Arguments(true)]
    public async Task Z_supported_creator_and_utf8_flag_baselines_pass(bool unix)
    {
        var zip = new W2RZip(); foreach (var entry in zip.Entries) { entry.Flags = 0x800; entry.Creator = unix ? (ushort)0x0314 : (ushort)0x0014; entry.Attributes = unix ? 0x81a40000u : 0u; }
        await Assert.That((await W2RSupport.Probe(zip.Build().Bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
    }

    [Test]
    [Arguments((ushort)0)] [Arguments((ushort)0x800)]
    [Arguments((ushort)8)] [Arguments((ushort)0x808)]
    public async Task Z_reviewed_flag_combinations_and_matching_timestamps_pass(ushort flags)
    {
        var zip = new W2RZip();
        foreach (var entry in zip.Entries)
        {
            entry.Flags = flags; entry.Descriptor = (flags & 8) != 0;
            entry.Time = 0x1234; entry.Date = 0x5822;
        }
        var probe = await W2RSupport.Probe(zip.Build().Bytes);
        await Assert.That(probe.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        foreach (var entry in zip.Entries)
            W2RSupport.Require(probe.Entries.Single(actual => actual.Path == entry.Name).Bytes.SequenceEqual(entry.Payload), "Exact allowed flags payload");
    }

    [Test]
    [Arguments(false)] [Arguments(true)]
    public Task Z_raw_pinned_producer_single_local_timestamp_violation_is_conflict(bool includeHtml)
        => W2RReviewedLinuxProducer.VerifyRawTimestampConflict(includeHtml);
    // Descriptor profile is intentionally provisional until the exact-package fixture emitted
    // by JT has been independently reviewed. These tests establish a precise grammar candidate;
    // they are not a substitute for embedding the root-generated Linux package bytes below.
    [Test]
    public async Task Z_supplemental_synthetic_classic_descriptor_profile_preserves_payload()
    {
        await AssertBaseline(); var zip = new W2RZip(); foreach (var entry in zip.Entries) entry.Descriptor = true;
        var result = await W2RSupport.Probe(zip.Build().Bytes);
        await Assert.That(result.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        foreach (var entry in zip.Entries) W2RSupport.Require(result.Entries.Single(value => value.Path == entry.Name).Bytes.SequenceEqual(entry.Payload), "Signed descriptor payload");
    }

    [Test]
    [Arguments("missing")] [Arguments("truncated")] [Arguments("wrong-signature")] [Arguments("crc")]
    [Arguments("compressed")] [Arguments("expanded")] [Arguments("duplicate")] [Arguments("unsigned")]
    [Arguments("zip64")] [Arguments("nonzero-local")]
    public async Task Z_classic_descriptor_named_violation_is_conflict(string attack)
    {
        // Raw Linux captures, exact profile/payloads, and every supplemental order
        // must pass the actual consumer before ANY descriptor negative earns credit.
        await W2RReviewedLinuxProducer.RequireAndVerify();
        var baseline = new W2RZip(); foreach (var value in baseline.Entries) value.Descriptor = true;
        // Synthetic baseline is supplemental, after the exact-producer prerequisite.
        await Assert.That((await W2RSupport.Probe(baseline.Build().Bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        baseline.Entries[0].DescriptorAttack = attack; await AssertConflict(baseline.Build().Bytes);
    }

    [Test]
    [Arguments("valid")]
    [Arguments("truncated-stream")]
    [Arguments("actual-crc")]
    [Arguments("declared-short")]
    [Arguments("declared-long")]
    public async Task Z_deflate_actual_decode_length_and_crc_are_bound(string attack)
    {
        var zip = new W2RZip(); var entry = zip.Entries[0];
        entry.Payload = Enumerable.Range(0, 4096).Select(index => (byte)(index * 73)).ToArray();
        entry.Method = 8;
        using (var encoded = new MemoryStream())
        {
            using (var compressor = new DeflateStream(encoded, CompressionLevel.SmallestSize, true)) compressor.Write(entry.Payload);
            entry.EncodedPayload = encoded.ToArray();
        }
        await Assert.That((await W2RSupport.Probe(zip.Build().Bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        switch (attack)
        {
            case "valid": return;
            case "truncated-stream": entry.EncodedPayload = entry.EncodedPayload[..(entry.EncodedPayload.Length / 2)]; break;
            case "actual-crc": entry.CrcOverride = W2RZip.Crc(entry.Payload) ^ 1; break;
            case "declared-short": entry.Expanded = (uint)entry.Payload.Length - 1; break;
            case "declared-long": entry.Expanded = (uint)entry.Payload.Length + 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(attack));
        }
        await AssertConflict(zip.Build().Bytes);
    }
    [Test]
    public Task Z_reviewed_linux_node24_producer_prerequisite_is_mandatory()
        => W2RReviewedLinuxProducer.RequireAndVerify();

    [Test]
    [Arguments(4 * 1024 * 1024)]
    [Arguments(4 * 1024 * 1024 + 1)]
    public async Task Z_compressed_response_boundary_has_consistent_decodable_zip_metadata(int compressedBytes)
    {
        var bytes = W2RZip.BuildExactArchiveLength(compressedBytes);
        W2RSupport.Require(bytes.Length == compressedBytes, "Exact compressed transport size");
        // Legal empty DEFLATE blocks increase encoded length without inflating the
        // payload. This independently proves both fixtures are otherwise valid;
        // the +1 rejection cannot be caused by CRC, STORE inequality, or layout.
        var decoded = GitHubContextSourceArtifactStore.InspectArchiveForTests(bytes);
        W2RSupport.Require(decoded.Count == 2 && decoded.Sum(entry => entry.Bytes.Length) < 100,
            "Both boundary archives must decode successfully with tiny valid payloads before transport admission");
        await Assert.That((await W2RSupport.Probe(bytes)).Disposition).IsEqualTo(
            compressedBytes == 4 * 1024 * 1024 ? ContextSourceArtifactProbeDisposition.Present : ContextSourceArtifactProbeDisposition.Conflict);
    }

    [Test]
    public async Task Z_zip64_end_record_and_locator_are_rejected_without_sentinel_or_extra_field_mutations()
    {
        await AssertBaseline();
        var ordinary = new W2RZip().Build();
        var bytes = W2RZip.AddZip64EndRecords(ordinary);
        // Normal local/central records, their sizes, and all classic EOCD values
        // remain unchanged. Only complete, internally consistent ZIP64 end records
        // and locator are inserted; no ZIP64 sentinel/extra-field test stands in.
        await AssertConflict(bytes);
    }
    [Test]
    [Arguments(false)] [Arguments(true)]
    public Task Z_immutable_raw_linux_producer_exemplar_is_accepted_byte_exactly(bool includeHtml)
        => W2RReviewedLinuxProducer.VerifyRaw(includeHtml);

    [Test]
    [Arguments(false, 0)] [Arguments(false, 1)]
    [Arguments(true, 0)] [Arguments(true, 1)] [Arguments(true, 2)]
    [Arguments(true, 3)] [Arguments(true, 4)] [Arguments(true, 5)]
    public Task Z_synthetic_reviewed_metadata_and_payloads_accept_every_local_entry_order(bool includeHtml, int permutation)
        => W2RReviewedLinuxProducer.VerifySyntheticPermutation(includeHtml, permutation);
    private static async Task AssertBaseline() => await Assert.That((await W2RSupport.Probe(new W2RZip().Build().Bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
    private static async Task AssertConflict(byte[] bytes) => await Assert.That((await W2RSupport.Probe(bytes)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
}

internal sealed class W2RZipEntry(string name, byte[] payload)
{
    internal string Name = name;
    internal byte[] Payload = payload;
    internal byte[]? EncodedPayload;
    internal ushort Creator = 20, Version = 20, Flags, Method, Time, Date = 0x5821, InternalAttributes;
    internal uint Attributes;
    internal ushort? LocalVersion, LocalFlags, LocalMethod, LocalTime, LocalDate;
    internal uint? CrcOverride, Compressed, Expanded, LocalCrc, LocalCompressed, LocalExpanded, OffsetOverride;
    internal byte[]? NameBytes, LocalName;
    internal byte[] LocalExtra = [], CentralExtra = [], Comment = [];
    internal bool Descriptor, SkipLocal;
    internal string? DescriptorAttack;
}

internal sealed record W2RZipBytes(byte[] Bytes, int[] LocalOffsets, int[] PayloadOffsets, int[] CentralOffsets, int EocdOffset);
internal sealed class W2RZip
{
    internal List<W2RZipEntry> Entries = [new("manifest.json", "{}"u8.ToArray()), new("bundle.sha256", Encoding.ASCII.GetBytes(new string('a', 64) + "\n"))];
    internal byte[] Prefix = [], GapBeforeSecond = [], CentralGap = [], EocdGap = [], Trailing = [];
    internal int CentralSizeAdjustment;
    internal ushort Disk, EocdCommentLength;
    internal ushort? EntryCount;
    internal W2RZipBytes Build()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        var local = new List<int>(); var payloads = new List<int>(); var central = new List<int>();
        writer.Write(Prefix);
        foreach (var entry in Entries)
        {
            if (entry.SkipLocal)
            {
                local.Add(checked((int)entry.OffsetOverride!.Value));
                payloads.Add(local[^1] + 30 + (entry.LocalName ?? entry.NameBytes ?? Encoding.UTF8.GetBytes(entry.Name)).Length + entry.LocalExtra.Length);
                continue;
            }
            if (local.Count == 1) writer.Write(GapBeforeSecond);
            local.Add(checked((int)stream.Position));
            var name = entry.LocalName ?? entry.NameBytes ?? Encoding.UTF8.GetBytes(entry.Name);
            var crc = entry.CrcOverride ?? Crc(entry.Payload); var compressed = entry.Compressed ?? checked((uint)(entry.EncodedPayload ?? entry.Payload).Length); var expanded = entry.Expanded ?? checked((uint)entry.Payload.Length);
            var flags = (ushort)(entry.Flags | (entry.Descriptor ? 8 : 0));
            writer.Write(0x04034b50u); writer.Write(entry.LocalVersion ?? entry.Version); writer.Write(entry.LocalFlags ?? flags); writer.Write(entry.LocalMethod ?? entry.Method);
            writer.Write(entry.LocalTime ?? entry.Time); writer.Write(entry.LocalDate ?? entry.Date);
            var nonzero = entry.DescriptorAttack == "nonzero-local";
            writer.Write(entry.LocalCrc ?? (entry.Descriptor && !nonzero ? 0 : crc));
            writer.Write(entry.LocalCompressed ?? (entry.Descriptor && !nonzero ? 0 : compressed)); writer.Write(entry.LocalExpanded ?? (entry.Descriptor && !nonzero ? 0 : expanded));
            writer.Write(checked((ushort)name.Length)); writer.Write(checked((ushort)entry.LocalExtra.Length)); writer.Write(name); writer.Write(entry.LocalExtra);
            payloads.Add(checked((int)stream.Position)); writer.Write(entry.EncodedPayload ?? entry.Payload);
            if (entry.Descriptor)
            {
                using var descriptorStream = new MemoryStream(); using var descriptor = new BinaryWriter(descriptorStream);
                if (entry.DescriptorAttack != "unsigned") descriptor.Write(entry.DescriptorAttack == "wrong-signature" ? 0x08074b51u : 0x08074b50u);
                descriptor.Write(entry.DescriptorAttack == "crc" ? crc ^ 1 : crc);
                if (entry.DescriptorAttack == "zip64") { descriptor.Write((ulong)compressed); descriptor.Write((ulong)expanded); }
                else { descriptor.Write(entry.DescriptorAttack == "compressed" ? compressed + 1 : compressed); descriptor.Write(entry.DescriptorAttack == "expanded" ? expanded + 1 : expanded); }
                var bytes = descriptorStream.ToArray();
                if (entry.DescriptorAttack == "truncated") bytes = bytes[..^1];
                if (entry.DescriptorAttack != "missing") writer.Write(bytes);
                if (entry.DescriptorAttack == "duplicate") writer.Write(bytes);
            }
        }
        var centralStart = checked((uint)stream.Position);
        for (var i = 0; i < Entries.Count; i++)
        {
            if (i == 1) writer.Write(CentralGap);
            var entry = Entries[i]; var name = entry.NameBytes ?? Encoding.UTF8.GetBytes(entry.Name); central.Add(checked((int)stream.Position));
            writer.Write(0x02014b50u); writer.Write(entry.Creator); writer.Write(entry.Version); writer.Write((ushort)(entry.Flags | (entry.Descriptor ? 8 : 0))); writer.Write(entry.Method);
            writer.Write(entry.Time); writer.Write(entry.Date); writer.Write(entry.CrcOverride ?? Crc(entry.Payload));
            writer.Write(entry.Compressed ?? checked((uint)(entry.EncodedPayload ?? entry.Payload).Length)); writer.Write(entry.Expanded ?? checked((uint)entry.Payload.Length));
            writer.Write(checked((ushort)name.Length)); writer.Write(checked((ushort)entry.CentralExtra.Length)); writer.Write(checked((ushort)entry.Comment.Length));
            writer.Write((ushort)0); writer.Write(entry.InternalAttributes); writer.Write(entry.Attributes); writer.Write(entry.OffsetOverride ?? checked((uint)local[i]));
            writer.Write(name); writer.Write(entry.CentralExtra); writer.Write(entry.Comment);
        }
        var centralSize = checked((uint)(stream.Position - centralStart + CentralSizeAdjustment)); writer.Write(EocdGap);
        var eocd = checked((int)stream.Position); writer.Write(0x06054b50u); writer.Write(Disk); writer.Write((ushort)0);
        writer.Write(EntryCount ?? checked((ushort)Entries.Count)); writer.Write(EntryCount ?? checked((ushort)Entries.Count));
        writer.Write(centralSize); writer.Write(centralStart); writer.Write(EocdCommentLength); writer.Write(Trailing);
        var result = stream.ToArray();
        W2RSupport.Require(local.Count == Entries.Count && central.Count == Entries.Count && eocd + 22 + Trailing.Length == result.Length, "Builder envelope arithmetic");
        for (var i = 0; i < Entries.Count; i++)
            W2RSupport.Require(payloads[i] == local[i] + 30 + (Entries[i].LocalName ?? Entries[i].NameBytes ?? Encoding.UTF8.GetBytes(Entries[i].Name)).Length + Entries[i].LocalExtra.Length, "Builder local offset arithmetic");
        return new(result, local.ToArray(), payloads.ToArray(), central.ToArray(), eocd);
    }
    internal static byte[] CutRecordBoundary(W2RZipBytes baseline, string boundary)
    {
        var localBoundary = boundary.StartsWith("local-", StringComparison.Ordinal);
        var recordEnd = localBoundary ? baseline.CentralOffsets[0] : baseline.EocdOffset;
        var cut = boundary switch
        {
            "local-fixed" => baseline.LocalOffsets[1] + 29,
            "local-name" => baseline.PayloadOffsets[1] - 1,
            "local-payload" => baseline.CentralOffsets[0] - 1,
            "central-fixed" => baseline.CentralOffsets[1] + 45,
            "central-name" => baseline.EocdOffset - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(boundary))
        };
        var removed = recordEnd - cut;
        W2RSupport.Require(removed > 0, "Boundary cut must remove only the final modeled record tail");
        var bytes = new byte[baseline.Bytes.Length - removed];
        baseline.Bytes.AsSpan(0, cut).CopyTo(bytes);
        baseline.Bytes.AsSpan(recordEnd).CopyTo(bytes.AsSpan(cut));
        var eocd = baseline.EocdOffset - removed;
        var central = baseline.CentralOffsets[0] - (localBoundary ? removed : 0);
        var centralSize = baseline.EocdOffset - baseline.CentralOffsets[0] - (localBoundary ? 0 : removed);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(eocd + 12), checked((uint)centralSize));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(eocd + 16), checked((uint)central));
        W2RSupport.Require(eocd == bytes.Length - 22 && BitConverter.ToUInt32(bytes, eocd) == 0x06054b50 &&
            BitConverter.ToUInt16(bytes, eocd + 8) == 2 && BitConverter.ToUInt16(bytes, eocd + 10) == 2 &&
            BitConverter.ToUInt16(bytes, eocd + 20) == 0 && central + centralSize == eocd,
            "Malformed inner boundary must preserve valid terminal EOCD and exact central envelope");
        W2RSupport.Require(bytes.AsSpan(0, baseline.LocalOffsets[1]).SequenceEqual(baseline.Bytes.AsSpan(0, baseline.LocalOffsets[1])),
            "Unrelated first local record/payload must remain byte-identical");
        var firstCentralLength = baseline.CentralOffsets[1] - baseline.CentralOffsets[0];
        W2RSupport.Require(bytes.AsSpan(central, firstCentralLength).SequenceEqual(baseline.Bytes.AsSpan(baseline.CentralOffsets[0], firstCentralLength)),
            "Unrelated first central record must remain byte-identical");
        if (localBoundary)
            W2RSupport.Require(bytes.AsSpan(central, centralSize).SequenceEqual(baseline.Bytes.AsSpan(baseline.CentralOffsets[0], centralSize)),
                "Local truncation must preserve EVERY central record and all local/central declarations");
        else
            W2RSupport.Require(bytes.AsSpan(0, central).SequenceEqual(baseline.Bytes.AsSpan(0, central)),
                "Central truncation must preserve EVERY complete local record and payload");
        return bytes;
    }

    internal static byte[] BuildExactArchiveLength(int totalBytes)
    {
        var zip = new W2RZip(); var entry = zip.Entries[0]; entry.Method = 8;
        entry.EncodedPayload = new byte[5 + entry.Payload.Length];
        var envelopeBytes = zip.Build().Bytes.Length - entry.EncodedPayload.Length;
        var payloadLength = 2 + ((totalBytes - envelopeBytes - 5 - 2) % 5 + 5) % 5;
        entry.Payload = Encoding.UTF8.GetBytes("{}" + new string(' ', payloadLength - 2));
        var paddingBlocks = (totalBytes - envelopeBytes - 5 - payloadLength) / 5;
        W2RSupport.Require(paddingBlocks >= 0, "Requested archive length must fit legal DEFLATE framing");
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        for (var index = 0; index < paddingBlocks; index++)
        {
            writer.Write((byte)0); // BFINAL=0, BTYPE=00, zero alignment bits
            writer.Write((ushort)0); writer.Write(ushort.MaxValue); // LEN=0, NLEN=0xffff
        }
        writer.Write((byte)1); // final stored DEFLATE block
        writer.Write(checked((ushort)payloadLength)); writer.Write(unchecked((ushort)~payloadLength)); writer.Write(entry.Payload);
        entry.EncodedPayload = memory.ToArray();
        var bytes = zip.Build().Bytes;
        W2RSupport.Require(bytes.Length == totalBytes, "Exact-length builder arithmetic");
        return bytes;
    }

    internal static byte[] AddZip64EndRecords(W2RZipBytes ordinary)
    {
        var centralOffset = ordinary.CentralOffsets[0]; var centralSize = ordinary.EocdOffset - centralOffset;
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        writer.Write(ordinary.Bytes.AsSpan(0, ordinary.EocdOffset));
        var zip64Offset = memory.Position;
        writer.Write(0x06064b50u); writer.Write(44ul); writer.Write((ushort)45); writer.Write((ushort)45);
        writer.Write(0u); writer.Write(0u); writer.Write(2ul); writer.Write(2ul); writer.Write((ulong)centralSize); writer.Write((ulong)centralOffset);
        writer.Write(0x07064b50u); writer.Write(0u); writer.Write((ulong)zip64Offset); writer.Write(1u);
        writer.Write(ordinary.Bytes.AsSpan(ordinary.EocdOffset));
        var bytes = memory.ToArray();
        W2RSupport.Require(bytes.Length == ordinary.Bytes.Length + 76 && BitConverter.ToUInt32(bytes, bytes.Length - 22) == 0x06054b50 &&
            bytes.AsSpan(bytes.Length - 22).SequenceEqual(ordinary.Bytes.AsSpan(ordinary.EocdOffset)) &&
            bytes.AsSpan(0, ordinary.EocdOffset).SequenceEqual(ordinary.Bytes.AsSpan(0, ordinary.EocdOffset)),
            "ZIP64 record fixture preserves every ordinary byte and the classic terminal EOCD");
        return bytes;
    }
    internal static uint Crc(ReadOnlySpan<byte> bytes)
    { uint crc = 0xffffffff; foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); } return ~crc; }
}

// Deterministic BCL TimeProvider seam: no wall-clock sleeps and no added package.
internal sealed class W2RManualTimeProvider : TimeProvider
{
    private long _ticks;
    private readonly List<ManualTimer> _timers = [];
    internal int CreatedTimers { get; private set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        CreatedTimers++; var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer); timer.Change(dueTime, period); return timer;
    }
    internal void Advance(TimeSpan elapsed)
    {
        _ticks = checked(_ticks + elapsed.Ticks);
        foreach (var timer in _timers.ToArray()) timer.FireIfDue();
    }
    private sealed class ManualTimer(W2RManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue; private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed) return false;
            W2RSupport.Require(period == Timeout.InfiniteTimeSpan, "Cancellation deadlines use one-shot timers");
            _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(owner._ticks + dueTime.Ticks);
            return true;
        }
        internal void FireIfDue() { if (!_disposed && _due <= owner._ticks) { _due = long.MaxValue; callback(state); } }
        public void Dispose() { _disposed = true; _due = long.MaxValue; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

internal sealed class W2RRecordingBridge(IGitHubArtifactTool actual) : IGitHubArtifactTool
{
    internal string? Content { get; private set; }
    public Task ExecuteUploadAsync(GitHubArtifactToolInvocation invocation, CancellationToken cancellationToken = default)
    { Content = invocation.ContentDirectory; return actual.ExecuteUploadAsync(invocation, cancellationToken); }
    internal void CleanupLeftoverScratch()
    {
        if (Content is null) return;
        var root = Path.GetFullPath(Path.GetDirectoryName(Content)!);
        var fixedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kicktippai-github-artifact"));
        W2RSupport.Require(Path.GetDirectoryName(root) == fixedRoot && Guid.TryParseExact(Path.GetFileName(root), "N", out _) && Path.GetFileName(Content) == "content",
            "Fallback cleanup is confined to this invocation's GUID scratch directory");
        if (Directory.Exists(root))
        {
            W2RSupport.Require((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0, "Never recursively delete an indirect scratch root");
            Directory.Delete(root, true);
        }
    }
}

internal sealed class W2ROutputObservation
{
    private long _bytes;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _readers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CountingStream> _streams = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Pipe, long Before, int Requested, int Returned)> _reads = new();
    internal long BytesRead => Interlocked.Read(ref _bytes);
    internal int ReaderCount => _readers.Count;
    internal bool ReadersSettled => _readers.Count == 2 && _readers.Values.All(task => task.IsCompleted);
    internal bool PipesDisposed => _streams.Count == 2 && _streams.Values.All(stream => stream.Disposed);
    internal IReadOnlyCollection<(string Pipe, long Before, int Requested, int Returned)> Reads => _reads.ToArray();
    internal Stream Wrap(string pipe, Stream actual)
    { var stream = new CountingStream(this, pipe, actual); W2RSupport.Require(_streams.TryAdd(pipe, stream), "One observed stream per real pipe"); return stream; }
    internal void ReaderStarted(string pipe, Task task) => W2RSupport.Require(_readers.TryAdd(pipe, task), "One observed task per real pipe");
    private sealed class CountingStream(W2ROutputObservation owner, string pipe, Stream actual) : Stream
    {
        internal bool Disposed { get; private set; }
        public override bool CanRead => actual.CanRead; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var before = owner.BytesRead; var read = actual.Read(buffer, offset, count);
            owner._reads.Enqueue((pipe, before, count, read)); Interlocked.Add(ref owner._bytes, read); return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var before = owner.BytesRead;
            var read = await actual.ReadAsync(buffer, cancellationToken);
            owner._reads.Enqueue((pipe, before, buffer.Length, read)); Interlocked.Add(ref owner._bytes, read); return read;
        }
        protected override void Dispose(bool disposing) { if (disposing) actual.Dispose(); Disposed = true; base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

// The launch-description diagnostic observes the same streams and completion
// tasks that the bridge uses. It never initiates a read or closes a pipe.
internal sealed class W2RNativeTimeoutOutputProbe
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Pipe> _pipes = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _callbackFaults = new();
    internal IReadOnlyCollection<string> CallbackFaults => _callbackFaults.ToArray();
    internal IReadOnlyCollection<string> ProbeFaults => _pipes.Select(pair => pair.Value.ProbeFault is { } fault
        ? pair.Key + "-" + fault : null).OfType<string>().ToArray();
    internal bool RecoverySettled => _pipes.Count == 2 && _pipes.Values.All(pipe =>
        Volatile.Read(ref pipe.Reader)?.IsCompleted == true && Volatile.Read(ref pipe.Disposed) != 0 &&
        Volatile.Read(ref pipe.EndpointAnyDisposed) != 0 && Volatile.Read(ref pipe.EndpointObserved) != 0);

    internal IArtifactProcessScope DecorateScope(IArtifactProcessScope actual)
    {
        try
        {
            var stdout = _pipes.GetOrAdd("stdout", _ => new Pipe());
            var stderr = _pipes.GetOrAdd("stderr", _ => new Pipe());
            var output = new EndpointStream(actual.StandardOutput, stdout);
            var error = new EndpointStream(actual.StandardError, stderr);
            Interlocked.Exchange(ref stdout.EndpointObserved, 1);
            Interlocked.Exchange(ref stderr.EndpointObserved, 1);
            return new EndpointScope(actual, output, error);
        }
        catch (Exception exception)
        {
            _callbackFaults.Enqueue("endpoint-decoration-" + exception.GetType().Name);
            return actual;
        }
    }

    internal Stream Wrap(string name, Stream actual)
    {
        try
        {
            if (name is not ("stdout" or "stderr"))
            {
                _callbackFaults.Enqueue("stream-name");
                return actual;
            }
            var pipe = _pipes.GetOrAdd(name, _ => new Pipe());
            if (Interlocked.CompareExchange(ref pipe.ReaderWrapped, 1, 0) != 0)
            {
                _callbackFaults.Enqueue("stream-duplicate");
                return actual;
            }
            return new ProbeStream(actual, pipe);
        }
        catch (Exception exception)
        {
            _callbackFaults.Enqueue("stream-" + exception.GetType().Name);
            return actual;
        }
    }

    internal void ReaderStarted(string name, Task reader)
    {
        try
        {
            if (!_pipes.TryGetValue(name, out var pipe) || Interlocked.CompareExchange(ref pipe.Reader, reader, null) is not null)
                _callbackFaults.Enqueue("reader-missing-or-duplicate");
        }
        catch (Exception exception) { _callbackFaults.Enqueue("reader-" + exception.GetType().Name); }
    }

    internal string Snapshot()
    {
        string One(string name)
        {
            if (!_pipes.TryGetValue(name, out var pipe)) return name + ":unavailable-before-drain";
            var reader = Volatile.Read(ref pipe.Reader);
            var result = reader is Task<ArtifactDrainResult> { IsCompletedSuccessfully: true } completed
                ? completed.Result.ToString() : "unavailable";
            return $"{name}:bytes={Interlocked.Read(ref pipe.Bytes)},reads={Volatile.Read(ref pipe.Reads)}," +
                $"eof={Volatile.Read(ref pipe.Eof) != 0},reader={reader?.Status.ToString() ?? "unavailable"}," +
                $"readerResult={result},readerDisposeEntered={Volatile.Read(ref pipe.DisposeEntered) != 0}," +
                $"readerDisposed={Volatile.Read(ref pipe.Disposed) != 0},readFault={Volatile.Read(ref pipe.ReadFault) ?? "absent"}," +
                $"readerDisposeFault={Volatile.Read(ref pipe.DisposeFault) ?? "absent"}," +
                $"endpointObserved={Volatile.Read(ref pipe.EndpointObserved) != 0}," +
                $"directEndpointDisposeEntered={Volatile.Read(ref pipe.EndpointDisposeEntered) != 0}," +
                $"directEndpointDisposed={Volatile.Read(ref pipe.EndpointDisposed) != 0}," +
                $"directEndpointDisposeFault={Volatile.Read(ref pipe.EndpointDisposeFault) ?? "absent"}," +
                $"endpointAnyDisposed={Volatile.Read(ref pipe.EndpointAnyDisposed) != 0}";
        }
        return One("stdout") + ";" + One("stderr");
    }

    private sealed class Pipe
    {
        internal Task? Reader;
        internal int ReaderWrapped;
        internal long Bytes;
        internal int Reads;
        internal int Eof;
        internal int DisposeEntered;
        internal int Disposed;
        internal string? ReadFault;
        internal string? DisposeFault;
        internal string? ProbeFault;
        internal int EndpointObserved;
        internal int EndpointDisposeEntered;
        internal int EndpointDisposed;
        internal int EndpointAnyDisposed;
        internal string? EndpointDisposeFault;
    }

    private sealed class ProbeStream(Stream actual, Pipe pipe) : Stream
    {
        private void Record(int requested, int count)
        {
            try
            {
                Interlocked.Increment(ref pipe.Reads);
                Interlocked.Add(ref pipe.Bytes, count);
                if (requested != 0 && count == 0) Interlocked.Exchange(ref pipe.Eof, 1);
            }
            catch (Exception exception) { pipe.ProbeFault = exception.GetType().Name; }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            try { var read = actual.Read(buffer, offset, count); Record(count, read); return read; }
            catch (Exception exception) { pipe.ReadFault = exception.GetType().Name; throw; }
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try { var read = await actual.ReadAsync(buffer, cancellationToken); Record(buffer.Length, read); return read; }
            catch (Exception exception) { pipe.ReadFault = exception.GetType().Name; throw; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Exchange(ref pipe.DisposeEntered, 1);
                try
                {
                    if (actual is EndpointStream endpoint) endpoint.DisposeFromReader();
                    else actual.Dispose();
                    Interlocked.Exchange(ref pipe.Disposed, 1);
                }
                catch (Exception exception) { pipe.DisposeFault = exception.GetType().Name; throw; }
            }
            base.Dispose(disposing);
        }
        public override bool CanRead => actual.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class EndpointScope(IArtifactProcessScope actual, Stream stdout, Stream stderr) : IArtifactProcessScope
    {
        public int ProcessId => actual.ProcessId;
        public Task<int> DirectExit => actual.DirectExit;
        public Stream StandardOutput => stdout;
        public Stream StandardError => stderr;
        public void TerminateScope() => actual.TerminateScope();
        public Task<bool> ConfirmTerminatedAsync(CancellationToken token) => actual.ConfirmTerminatedAsync(token);
        public Task<bool> ReleaseAsync(CancellationToken token) => actual.ReleaseAsync(token);
        public Task<bool> SettleObserverAsync(CancellationToken token) => actual.SettleObserverAsync(token);
        public void Dispose() => actual.Dispose();
    }

    private sealed class EndpointStream(Stream actual, Pipe pipe) : Stream
    {
        internal void DisposeFromReader() { Close(false); base.Dispose(true); }
        private void Close(bool direct)
        {
            if (direct) Interlocked.Exchange(ref pipe.EndpointDisposeEntered, 1);
            try
            {
                actual.Dispose();
                Interlocked.Exchange(ref pipe.EndpointAnyDisposed, 1);
                if (direct) Interlocked.Exchange(ref pipe.EndpointDisposed, 1);
            }
            catch (Exception exception)
            {
                if (direct) pipe.EndpointDisposeFault = exception.GetType().Name;
                throw;
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) Close(true); base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => actual.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => actual.ReadAsync(buffer, offset, count, token);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => actual.ReadAsync(buffer, token);
        public override bool CanRead => actual.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

// Immutable ORIGINAL Linux producer captures. Their generator identity is separate
// from this later test revision. Image/npm are root-attested execution provenance;
// all bytes/metadata/payloads below are checked offline without reading scratch logs.
internal static class W2RReviewedLinuxProducer
{
    internal const string DiagnosisSha256 = "1105295401b422d13659c971f7550d2f2aeb3b365662549d13e7c97dda07811f";
    internal const string ReviewArtifactSha256 = "88b1485818713b8c175a1fc4b198b8b111dea62b289d7c6356ce2de08859fcca";
    internal const string OriginalGeneratorSha256 = "e18dc8370e751a30d84e1307b531c2ab38cb5cbce0a61ba44c1a1d37c469ad52";
    private const string TwoFileBase64 = "UEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAYnVuZGxlLnNoYTI1NmFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWEKUEsHCI0XXylBAAAAQQAAAFBLAwQUAAgAAAAAACFYAAAAAAAAAAAAAAAADQAAAG1hbmlmZXN0Lmpzb257ImNvbXBldGl0aW9uIjoiYnVuZGVzbGlnYS0yMDI2LTI3Iiwic2NvcGUiOiJwcm9kdWN0aW9uLWxpdmUiLCJjeWNsZUlkIjoiZ2hhOjEyMzo0NTYiLCJjeWNsZVN0b3JhZ2VJZCI6ImQyMWM0OGY0Y2U3YjNhYmIwMDg1OWY3MGIwZjJhMjE2OTcxYTc3ODdiZmU3YjI5ZDkyMmQ5Y2ZmY2E2YTY4YjYifVBLBwh8aEhyqgAAAKoAAABQSwECLQMUAAgAAAAAACFYjRdfKUEAAABBAAAADQAAAAAAAAAAACAApIEAAAAAYnVuZGxlLnNoYTI1NlBLAQItAxQACAAAAAAAIVh8aEhyqgAAAKoAAAANAAAAAAAAAAAAIACkgXwAAABtYW5pZmVzdC5qc29uUEsFBgAAAAACAAIAdgAAAGEBAAAAAA==";
    private const string ThreeFileBase64 = "UEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAYnVuZGxlLnNoYTI1NmFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWEKUEsHCI0XXylBAAAAQQAAAFBLAwQUAAgAAAAAACFYAAAAAAAAAAAAAAAAFAAAAGNsdWItZWxvL3NvdXJjZS5odG1sPGh0bWw+Zml4dHVyZTwvaHRtbD5QSwcIu1EiAxQAAAAUAAAAUEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAbWFuaWZlc3QuanNvbnsiY29tcGV0aXRpb24iOiJidW5kZXNsaWdhLTIwMjYtMjciLCJzY29wZSI6InByb2R1Y3Rpb24tbGl2ZSIsImN5Y2xlSWQiOiJnaGE6MTIzOjQ1NiIsImN5Y2xlU3RvcmFnZUlkIjoiZDIxYzQ4ZjRjZTdiM2FiYjAwODU5ZjcwYjBmMmEyMTY5NzFhNzc4N2JmZTdiMjlkOTIyZDljZmZjYTZhNjhiNiJ9UEsHCHxoSHKqAAAAqgAAAFBLAQItAxQACAAAAAAAIViNF18pQQAAAEEAAAANAAAAAAAAAAAAIACkgQAAAABidW5kbGUuc2hhMjU2UEsBAi0DFAAIAAAAAAAhWLtRIgMUAAAAFAAAABQAAAAAAAAAAAAgAKSBfAAAAGNsdWItZWxvL3NvdXJjZS5odG1sUEsBAi0DFAAIAAAAAAAhWHxoSHKqAAAAqgAAAA0AAAAAAAAAAAAgAKSB0gAAAG1hbmlmZXN0Lmpzb25QSwUGAAAAAAMAAwC4AAAAtwEAAAAA";
    private const string CaptureProvenanceJson = """
        {
          "diagnosisSha256": "1105295401b422d13659c971f7550d2f2aeb3b365662549d13e7c97dda07811f",
          "reviewSha256": "88b1485818713b8c175a1fc4b198b8b111dea62b289d7c6356ce2de08859fcca",
          "originalGeneratorSha256": "e18dc8370e751a30d84e1307b531c2ab38cb5cbce0a61ba44c1a1d37c469ad52",
          "image": "node:24-bookworm-slim",
          "imageDigest": "sha256:2fe369e969550cde8e867afc3fe370b260140cab4a23d467074295b42163d553",
          "npm": "11.19.0",
          "command": "node context-source-artifact.test.mjs --w2r-producer-fixture",
          "rawCaptures": {
            "producer-fixtures-run1.txt": {
              "length": 9514,
              "sha256": "a511ac371ca225de777158d621c6104d23eedf8f93c527afacd86fbeb442eed8"
            },
            "producer-fixtures-run2.txt": {
              "length": 9514,
              "sha256": "a511ac371ca225de777158d621c6104d23eedf8f93c527afacd86fbeb442eed8"
            }
          },
          "producer": {
            "package": "@actions/artifact",
            "version": "6.2.1",
            "tarball": "https://registry.npmjs.org/@actions/artifact/-/artifact-6.2.1.tgz",
            "integrity": "sha512-sJGH0mhEbEjBCw7o6SaLhUU66u27aFW8HTfkIb5Tk2/Wy0caUDc+oYQEgnuFN7a0HCpAbQyK0U6U7XUJDgDWrw==",
            "lockSha256": "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd",
            "lockHashNormalization": "UTF-8 text with CRLF converted to LF; raw file hash also reported",
            "sourceSha256": {
              "package.json": "e21bb31fa8424754cd03c72278d78a76e50429895a9cb2babf69b4a7ba8f533a",
              "lib/internal/upload/zip.js": "8a8708fd49b2d6474a67fea2aa5e0ff56bae90401f5b6c95db0a600eb91e5a23",
              "lib/internal/upload/upload-zip-specification.js": "770ddc02798dd42627653f48aada2168d3d3af4f5c0f9f7e4b655f0ba914fe94"
            },
            "transitive": {
              "archiver": "7.0.1",
              "zip-stream": "6.0.1",
              "compress-commons": "6.0.2"
            },
            "transitiveWriterSha256": {
              "archiver/lib/core.js": "a8b28e116fef412d7503f7cc4a64b01d3d2f747a493b3d83dd97bd732ffc8b92",
              "zip-stream/index.js": "29ea55ff9cf0007853d4ac51936278547299a601a00980bb373c002f21a85186",
              "compress-commons/lib/archivers/zip/zip-archive-output-stream.js": "262cce586a8d182efb086e5be6b617958366851b53c1c95298377eec085274c4"
            },
            "actualPipeline": "getUploadZipSpecification(files, root) -> createZipUploadStream(specification, 0)",
            "expectedProfileRequiresReview": "classic signed descriptors; STORE; local zero CRC/sizes; central and descriptor bindings",
            "node": "v24.21.0",
            "platform": "linux",
            "arch": "x64",
            "transitivePackageSha256": {
              "archiver": "71f9d2abd62fc121c3f5c7ccb75a148cf1e8ce0b58ae6b86ea150cf719b86133",
              "zip-stream": "83ad75d717b4403a74b909e05363f6f7696eefdec04b49e783bf4546e84dab7f",
              "compress-commons": "d405726826d0c72939d487c8b81b1a278741e825f7a7e2ea7748c9bd9cb5c57c"
            },
            "rawLockSha256": "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd",
            "inputTimestamp": "2024-01-01T00:00:00.000Z",
            "inputMode": "0644",
            "compressionLevel": 0
          }
        }
        """;
    private static readonly IReadOnlyDictionary<string, string> PayloadBase64 = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bundle.sha256"] = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYQo=",
        ["manifest.json"] = "eyJjb21wZXRpdGlvbiI6ImJ1bmRlc2xpZ2EtMjAyNi0yNyIsInNjb3BlIjoicHJvZHVjdGlvbi1saXZlIiwiY3ljbGVJZCI6ImdoYToxMjM6NDU2IiwiY3ljbGVTdG9yYWdlSWQiOiJkMjFjNDhmNGNlN2IzYWJiMDA4NTlmNzBiMGYyYTIxNjk3MWE3Nzg3YmZlN2IyOWQ5MjJkOWNmZmNhNmE2OGI2In0=",
        ["club-elo/source.html"] = "PGh0bWw+Zml4dHVyZTwvaHRtbD4="
    };
    private sealed record Exemplar(int Length, string Sha256, string Base64, string[] Names);
    private static readonly Exemplar[] Exemplars =
    [
        new(493, "2fcaf91b679e82239bb94c15ef35a90690c88b566010e6aef9fec94f144282b5", TwoFileBase64, ["bundle.sha256", "manifest.json"]),
        new(645, "36f81428b5f589cd8c1fa5f3cb626d6d3316eb75efc4c751b9291204abb74c7c", ThreeFileBase64, ["bundle.sha256", "club-elo/source.html", "manifest.json"])
    ];
    private sealed record EntryProfile(int Length, uint Crc, string Sha256);
    private static readonly IReadOnlyDictionary<string, EntryProfile> EntryProfiles = new Dictionary<string, EntryProfile>(StringComparer.Ordinal)
    {
        ["bundle.sha256"] = new(65, 0x295f178d, "44c2336fedab8ff6a85c74c2b94165377b0981f526adb9487895ca6314165e86"),
        ["manifest.json"] = new(170, 0x7248687c, "38ebe2bd8fe5e8d122861f17d7c76f66b1acd3f652343f9cddc1eab858989734"),
        ["club-elo/source.html"] = new(20, 0x032251bb, "3ec85d118c49f07362673b7837c60410d3f39e6fa73414a7c6fcd58094f87eb4")
    };

    // This prerequisite opens only when the ACTUAL CONSUMER accepts both immutable
    // captures AND all eight supplemental synthetic order cases. A fixture hash or
    // metadata-only check, by itself, never earns descriptor-negative credit.
    internal static async Task RequireAndVerify()
    {
        AssertProvenance();
        await VerifyRaw(false); await VerifyRaw(true);
        foreach (var includeHtml in new[] { false, true })
            for (var index = 0; index < Orders(includeHtml).Length; index++) await VerifySyntheticPermutation(includeHtml, index);
    }
    internal static async Task VerifyRaw(bool includeHtml)
    {
        AssertProvenance();
        var exemplar = Exemplars[includeHtml ? 1 : 0]; var bytes = Convert.FromBase64String(exemplar.Base64);
        W2RSupport.Require(bytes.Length == exemplar.Length && Sha(bytes) == exemplar.Sha256, "Immutable raw producer exemplar length/SHA");
        var order = ValidateProfile(bytes, includeHtml);
        W2RSupport.Require(order.SequenceEqual(exemplar.Names), "Original capture order is preserved by its raw bytes");
        await VerifyConsumer(bytes, includeHtml);
    }
    internal static async Task VerifyRawTimestampConflict(bool includeHtml)
    {
        // First accept the untouched exact package-produced archive and all its
        // payloads. Then change one known local field; every layout byte stays fixed.
        await VerifyRaw(includeHtml);
        var original = Convert.FromBase64String(Exemplars[includeHtml ? 1 : 0].Base64);
        var hostile = (byte[])original.Clone();
        W2RSupport.Require(U32(hostile, 0) == 0x04034b50 && U16(hostile, 10) == 0, "Reviewed first local timestamp");
        hostile[10] = 1;
        W2RSupport.Require(original.Where((value, index) => value != hostile[index]).Count() == 1, "Only one local timestamp byte changed");
        await Assert.That((await W2RSupport.Probe(hostile)).Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Conflict);
    }
    internal static async Task VerifySyntheticPermutation(bool includeHtml, int index)
    {
        AssertProvenance();
        var order = Orders(includeHtml)[index];
        var zip = new W2RZip
        {
            Entries = order.Select(name => new W2RZipEntry(name, Convert.FromBase64String(PayloadBase64[name]))
            {
                Creator = 0x032d, Version = 20, Flags = 0, Method = 0, Time = 0, Date = 0x5821,
                Attributes = 0x81a40020, InternalAttributes = 0, Descriptor = true
            }).ToList()
        };
        // Rebuilt offsets/headers are SYNTHETIC grammar positives, never captures.
        var bytes = zip.Build().Bytes;
        W2RSupport.Require(ValidateProfile(bytes, includeHtml).SequenceEqual(order), "Synthetic local/central order must equal the requested permutation");
        await VerifyConsumer(bytes, includeHtml);
    }
    private static string[][] Orders(bool includeHtml) => includeHtml ?
    [
        ["bundle.sha256", "club-elo/source.html", "manifest.json"],
        ["bundle.sha256", "manifest.json", "club-elo/source.html"],
        ["club-elo/source.html", "bundle.sha256", "manifest.json"],
        ["club-elo/source.html", "manifest.json", "bundle.sha256"],
        ["manifest.json", "bundle.sha256", "club-elo/source.html"],
        ["manifest.json", "club-elo/source.html", "bundle.sha256"]
    ] : [["bundle.sha256", "manifest.json"], ["manifest.json", "bundle.sha256"]];

    private static async Task VerifyConsumer(byte[] bytes, bool includeHtml)
    {
        var expected = Exemplars[includeHtml ? 1 : 0].Names;
        var probe = await W2RSupport.Probe(bytes);
        await Assert.That(probe.Disposition).IsEqualTo(ContextSourceArtifactProbeDisposition.Present);
        W2RSupport.Require(probe.Entries.Select(entry => entry.Path).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)), "Exact consumer entry set");
        foreach (var entry in probe.Entries)
            W2RSupport.Require(entry.Bytes.SequenceEqual(Convert.FromBase64String(PayloadBase64[entry.Path])), "Byte-exact consumer payload " + entry.Path);
    }
    private static IReadOnlyList<string> ValidateProfile(byte[] bytes, bool includeHtml)
    {
        var expectedNames = Exemplars[includeHtml ? 1 : 0].Names;
        W2RSupport.Require(bytes.Length == (includeHtml ? 645 : 493), "Reviewed fixture envelope length");
        var eocd = bytes.Length - 22;
        W2RSupport.Require(U32(bytes, eocd) == 0x06054b50 && U16(bytes, eocd + 4) == 0 && U16(bytes, eocd + 6) == 0 &&
            U16(bytes, eocd + 8) == expectedNames.Length && U16(bytes, eocd + 10) == expectedNames.Length && U16(bytes, eocd + 20) == 0,
            "Complete single-disk terminal EOCD with exact counts and no comment");
        var central = checked((int)U32(bytes, eocd + 16)); var centralSize = checked((int)U32(bytes, eocd + 12));
        Range(central, centralSize, eocd);
        W2RSupport.Require(central + centralSize == eocd && central == (includeHtml ? 439 : 353) && centralSize == (includeHtml ? 184 : 118), "Exact local/central/EOCD envelope");
        var cursor = central; var nextLocal = 0; var order = new List<string>(); var locals = new HashSet<int>();
        for (var index = 0; index < expectedNames.Length; index++)
        {
            Range(cursor, 46, eocd);
            W2RSupport.Require(U32(bytes, cursor) == 0x02014b50, "Central record signature");
            var nameLength = U16(bytes, cursor + 28); var extraLength = U16(bytes, cursor + 30); var commentLength = U16(bytes, cursor + 32);
            Range(cursor, 46 + nameLength + extraLength + commentLength, eocd);
            var nameBytes = bytes.AsSpan(cursor + 46, nameLength).ToArray(); var name = Encoding.UTF8.GetString(nameBytes);
            W2RSupport.Require(expectedNames.Contains(name, StringComparer.Ordinal) && !order.Contains(name, StringComparer.Ordinal) &&
                nameBytes.SequenceEqual(Encoding.ASCII.GetBytes(name)), "Exact unique raw ASCII entry names");
            var expected = EntryProfiles[name]; var local = checked((int)U32(bytes, cursor + 42));
            W2RSupport.Require(local == nextLocal && locals.Add(local), "Producer local/central sequence and unique contiguous intervals");
            W2RSupport.Require(U16(bytes, cursor + 4) == 0x032d && U16(bytes, cursor + 6) == 20 && U16(bytes, cursor + 8) == 8 &&
                U16(bytes, cursor + 10) == 0 && U16(bytes, cursor + 12) == 0 && U16(bytes, cursor + 14) == 0x5821 &&
                U32(bytes, cursor + 16) == expected.Crc && U32(bytes, cursor + 20) == expected.Length && U32(bytes, cursor + 24) == expected.Length &&
                extraLength == 0 && commentLength == 0 && U16(bytes, cursor + 34) == 0 && U16(bytes, cursor + 36) == 0 && U32(bytes, cursor + 38) == 0x81a40020,
                "Every central metadata field matches reviewed creator/version/flags/STORE/date/CRC/sizes/regular-file profile");
            Range(local, 30, central);
            var localNameLength = U16(bytes, local + 26); var localExtraLength = U16(bytes, local + 28);
            W2RSupport.Require(U32(bytes, local) == 0x04034b50 && U16(bytes, local + 4) == 20 && U16(bytes, local + 6) == 8 && U16(bytes, local + 8) == 0 &&
                U16(bytes, local + 10) == 0 && U16(bytes, local + 12) == 0x5821 && U32(bytes, local + 14) == 0 && U32(bytes, local + 18) == 0 && U32(bytes, local + 22) == 0 &&
                localNameLength == nameLength && localExtraLength == 0, "Every local metadata field and classic-descriptor zero field binds");
            Range(local, 30 + localNameLength + localExtraLength, central);
            W2RSupport.Require(bytes.AsSpan(local + 30, localNameLength).SequenceEqual(nameBytes), "Local/central raw name bytes agree");
            var payloadStart = local + 30 + localNameLength; var descriptor = payloadStart + expected.Length;
            Range(payloadStart, expected.Length + 16, central);
            var payload = bytes.AsSpan(payloadStart, expected.Length).ToArray();
            W2RSupport.Require(U32(bytes, descriptor) == 0x08074b50 && U32(bytes, descriptor + 4) == expected.Crc &&
                U32(bytes, descriptor + 8) == expected.Length && U32(bytes, descriptor + 12) == expected.Length,
                "Exactly signed classic 16-byte descriptor; signature/CRC/32-bit sizes agree");
            W2RSupport.Require(W2RZip.Crc(payload) == expected.Crc && Sha(payload) == expected.Sha256 && payload.SequenceEqual(Convert.FromBase64String(PayloadBase64[name])),
                "Independent CRC32/SHA256 and byte-exact input payload");
            nextLocal = descriptor + 16; cursor += 46 + nameLength; order.Add(name);
        }
        W2RSupport.Require(nextLocal == central && cursor == eocd && order.Order(StringComparer.Ordinal).SequenceEqual(expectedNames.Order(StringComparer.Ordinal)),
            "Complete disjoint local interval union, central coverage and exact entry set");
        return order;
    }
    private static void AssertProvenance()
    {
        using var document = JsonDocument.Parse(CaptureProvenanceJson); var capture = document.RootElement; var producer = capture.GetProperty("producer");
        W2RSupport.Require(capture.GetProperty("diagnosisSha256").GetString() == DiagnosisSha256 && capture.GetProperty("reviewSha256").GetString() == ReviewArtifactSha256 &&
            capture.GetProperty("originalGeneratorSha256").GetString() == OriginalGeneratorSha256, "Exact accepted diagnosis/review and ORIGINAL generator identities");
        W2RSupport.Require(capture.GetProperty("image").GetString() == "node:24-bookworm-slim" &&
            capture.GetProperty("imageDigest").GetString() == "sha256:2fe369e969550cde8e867afc3fe370b260140cab4a23d467074295b42163d553" &&
            capture.GetProperty("npm").GetString() == "11.19.0" && capture.GetProperty("command").GetString() == "node context-source-artifact.test.mjs --w2r-producer-fixture", "Root-attested image/npm/generation-command provenance");
        foreach (var name in new[] { "producer-fixtures-run1.txt", "producer-fixtures-run2.txt" })
        {
            var raw = capture.GetProperty("rawCaptures").GetProperty(name);
            W2RSupport.Require(raw.GetProperty("length").GetInt32() == 9514 && raw.GetProperty("sha256").GetString() == "a511ac371ca225de777158d621c6104d23eedf8f93c527afacd86fbeb442eed8", "Original immutable raw capture identity");
        }
        W2RSupport.Require(producer.GetProperty("platform").GetString() == "linux" && producer.GetProperty("arch").GetString() == "x64" && producer.GetProperty("node").GetString() == "v24.21.0" &&
            producer.GetProperty("package").GetString() == "@actions/artifact" && producer.GetProperty("version").GetString() == "6.2.1" &&
            producer.GetProperty("lockSha256").GetString() == "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd" &&
            producer.GetProperty("rawLockSha256").GetString() == "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd" &&
            producer.GetProperty("integrity").GetString() == "sha512-sJGH0mhEbEjBCw7o6SaLhUU66u27aFW8HTfkIb5Tk2/Wy0caUDc+oYQEgnuFN7a0HCpAbQyK0U6U7XUJDgDWrw==",
            "Exact captured Linux runtime and locked package identity");
        foreach (var (group, name, hash) in new[]
        {
            ("sourceSha256", "package.json", "e21bb31fa8424754cd03c72278d78a76e50429895a9cb2babf69b4a7ba8f533a"),
            ("sourceSha256", "lib/internal/upload/zip.js", "8a8708fd49b2d6474a67fea2aa5e0ff56bae90401f5b6c95db0a600eb91e5a23"),
            ("sourceSha256", "lib/internal/upload/upload-zip-specification.js", "770ddc02798dd42627653f48aada2168d3d3af4f5c0f9f7e4b655f0ba914fe94"),
            ("transitiveWriterSha256", "archiver/lib/core.js", "a8b28e116fef412d7503f7cc4a64b01d3d2f747a493b3d83dd97bd732ffc8b92"),
            ("transitiveWriterSha256", "zip-stream/index.js", "29ea55ff9cf0007853d4ac51936278547299a601a00980bb373c002f21a85186"),
            ("transitiveWriterSha256", "compress-commons/lib/archivers/zip/zip-archive-output-stream.js", "262cce586a8d182efb086e5be6b617958366851b53c1c95298377eec085274c4"),
            ("transitivePackageSha256", "archiver", "71f9d2abd62fc121c3f5c7ccb75a148cf1e8ce0b58ae6b86ea150cf719b86133"),
            ("transitivePackageSha256", "zip-stream", "83ad75d717b4403a74b909e05363f6f7696eefdec04b49e783bf4546e84dab7f"),
            ("transitivePackageSha256", "compress-commons", "d405726826d0c72939d487c8b81b1a278741e825f7a7e2ea7748c9bd9cb5c57c")
        }) W2RSupport.Require(producer.GetProperty(group).GetProperty(name).GetString() == hash, "Reviewed installed source/package SHA " + name);
        W2RSupport.Require(producer.GetProperty("transitive").GetProperty("archiver").GetString() == "7.0.1" &&
            producer.GetProperty("transitive").GetProperty("zip-stream").GetString() == "6.0.1" &&
            producer.GetProperty("transitive").GetProperty("compress-commons").GetString() == "6.0.2", "Locked transitive producer versions");
        W2RSupport.Require(producer.GetProperty("inputTimestamp").GetString() == "2024-01-01T00:00:00.000Z" && producer.GetProperty("inputMode").GetString() == "0644" &&
            producer.GetProperty("compressionLevel").GetInt32() == 0 && producer.GetProperty("actualPipeline").GetString() == "getUploadZipSpecification(files, root) -> createZipUploadStream(specification, 0)", "Exact offline producer input controls and routine");
    }
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Range(int offset, int length, int limit) => W2RSupport.Require(offset >= 0 && length >= 0 && offset <= limit && length <= limit - offset, "Bounded reviewed ZIP record");
    private static ushort U16(byte[] bytes, int offset) { Range(offset, 2, bytes.Length); return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)); }
    private static uint U32(byte[] bytes, int offset) { Range(offset, 4, bytes.Length); return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)); }
}
