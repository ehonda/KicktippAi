using System.Net;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class GitHubContextSourceIssueProjectorTests
{
    [Test]
    public async Task Fresh_arm_precedes_POST_and_binding_precedes_success()
    {
        var health = Health(); var fences = new MemoryFences(health);
        var handler = new QueueHandler(Json("[]"), Json(Issue(health, 5), HttpStatusCode.Created));
        handler.BeforeRequest = method => { if (method == HttpMethod.Post && fences.Fence.State != BundesligaContextSourceIssueFenceState.CreateUncertain) throw new InvalidOperationException("POST before arm"); };
        using var client = Client(handler); var result = await Projector(client, fences).ProjectAsync(health);
        await Assert.That(result.ErrorCode).IsNull(); await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Bound);
        await Assert.That(fences.Fence.IssueNumber).IsEqualTo(5);
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Post]);
    }
    [Test]
    public async Task Concurrent_projectors_share_one_fresh_arm_and_issue_at_most_one_POST()
    {
        var health = Health(); var fences = new MemoryFences(health); var barrier = new ListingBarrier();
        var first = new ConcurrentHandler(barrier, health); var second = new ConcurrentHandler(barrier, health);
        using var firstClient = Client(first); using var secondClient = Client(second);
        await Task.WhenAll(Projector(firstClient, fences).ProjectAsync(health), Projector(secondClient, fences).ProjectAsync(health));
        await Assert.That(first.Posts + second.Posts).IsEqualTo(1);
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Bound);
    }
    [Test]
    public async Task Bound_identity_survives_missing_health_persistence_and_recovers_without_POST()
    {
        var health = Health(); var fences = new MemoryFences(health);
        var first = new QueueHandler(Json("[]"), Json(Issue(health, 5), HttpStatusCode.Created)); using var client = Client(first);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNull();
        // Discard synchronization result: process loss after Bind, before coordinator health CAS.
        var recovery = new QueueHandler(Json("[]"), Json(Issue(health, 5))); using var freshClient = Client(recovery);
        await Assert.That((await Projector(freshClient, fences).ProjectAsync(health)).ErrorCode).IsNull();
        await Assert.That(fences.Fence.IssueNumber).IsEqualTo(5);
        await Assert.That(recovery.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Get]);
        await Assert.That(first.Methods.Concat(recovery.Methods).Count(method => method == HttpMethod.Post)).IsEqualTo(1);
    }
    [Test]
    public async Task Admitted_older_POST_binds_once_and_newer_health_reconciles_that_issue()
    {
        var older = Health(); var newer = Health(run: 457); var fences = new MemoryFences(older);
        var first = new QueueHandler(Json("[]"), Json(Issue(older, 5), HttpStatusCode.Created))
            { BeforeRequest = method => { if (method == HttpMethod.Post) fences.Health = newer; } };
        using var oldClient = Client(first);
        await Assert.That((await Projector(oldClient, fences).ProjectAsync(older)).AppliedBodySha256).IsEqualTo(older.DesiredIssueProjection!.BodySha256);
        var recovery = new QueueHandler(Json("[" + Issue(older, 5) + "]"), Json(Issue(newer, 5))); using var newClient = Client(recovery);
        await Assert.That((await Projector(newClient, fences).ProjectAsync(newer)).AppliedBodySha256).IsEqualTo(newer.DesiredIssueProjection!.BodySha256);
        await Assert.That(fences.Fence.IssueNumber).IsEqualTo(5);
        await Assert.That(recovery.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Patch]);
        await Assert.That(first.Methods.Concat(recovery.Methods).Count(method => method == HttpMethod.Post)).IsEqualTo(1);
    }
    private sealed class ListingBarrier
    {
        private int _arrived;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ArriveAsync(CancellationToken token)
        { if (Interlocked.Increment(ref _arrived) == 2) _release.SetResult(); await _release.Task.WaitAsync(token); }
    }
    private sealed class ConcurrentHandler(ListingBarrier barrier, BundesligaContextSourceHealth health) : HttpMessageHandler
    {
        public int Posts { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get) { await barrier.ArriveAsync(cancellationToken); return Json("[]"); }
            if (request.Method != HttpMethod.Post) throw new InvalidOperationException();
            Posts++; return Json(Issue(health, 5), HttpStatusCode.Created);
        }
    }
    [Test]
    [Arguments("lost-reply")]
    [Arguments("http-rejection")]
    [Arguments("malformed")]
    [Arguments("wrong-marker")]
    [Arguments("bind-loss")]
    public async Task Crash_after_POST_and_delayed_visibility_across_instances_never_recreate(string outcome)
    {
        var health = Health(); var fences = new MemoryFences(health) { FailBind = outcome == "bind-loss" };
        object response = outcome switch { "lost-reply" => new HttpRequestException(), "http-rejection" => Json("{}", HttpStatusCode.BadRequest),
            "malformed" => Json("{", HttpStatusCode.Created), "wrong-marker" => Json(Issue(health, 5, body: "foreign"), HttpStatusCode.Created),
            _ => Json(Issue(health, 5), HttpStatusCode.Created) };
        var first = new QueueHandler(Json("[]"), response); using var client = Client(first);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.CreateUncertain);
        var newer = Health(run: 457); fences.Health = newer; fences.FailBind = false;
        var second = new QueueHandler(Json("[]")); using var freshClient = Client(second);
        await Assert.That((await Projector(freshClient, fences).ProjectAsync(newer)).ErrorCode).IsNotNull();
        await Assert.That(second.Methods).IsEquivalentTo([HttpMethod.Get]);
        var recovery = new QueueHandler(Json("[" + Issue(newer, 5) + "]")); using var recoveryClient = Client(recovery);
        await Assert.That((await Projector(recoveryClient, fences).ProjectAsync(newer)).ErrorCode).IsNull();
        await Assert.That(fences.Fence.IssueNumber).IsEqualTo(5);
    }
    [Test]
    public async Task Committed_arm_with_lost_reply_or_crash_before_send_consumes_create_permission()
    {
        var health = Health(); var fences = new MemoryFences(health) { LoseArmReply = true };
        var first = new QueueHandler(Json("[]")); using var client = Client(first);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(first.Methods).IsEquivalentTo([HttpMethod.Get]);
        fences.LoseArmReply = false;
        var second = new QueueHandler(Json("[]")); using var fresh = Client(second);
        await Assert.That((await Projector(fresh, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(second.Methods).IsEquivalentTo([HttpMethod.Get]);
    }
    [Test]
    [Arguments(BundesligaContextSourceIssueFenceState.Ready, true)]
    [Arguments(BundesligaContextSourceIssueFenceState.LegacyUncertain, false)]
    [Arguments(BundesligaContextSourceIssueFenceState.CreateUncertain, false)]
    public async Task Closed_absence_only_synchronizes_proven_fresh_lineage(BundesligaContextSourceIssueFenceState state, bool synchronized)
    {
        var health = Health(BundesligaContextSourceIssueState.Closed); var fences = new MemoryFences(health, state);
        var handler = new QueueHandler(Json("[]")); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode is null).IsEqualTo(synchronized);
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get]);
    }
    [Test]
    public async Task Bound_invisible_issue_uses_one_targeted_GET_and_never_creates()
    {
        var health = Health(); var fences = new MemoryFences(health, BundesligaContextSourceIssueFenceState.Bound);
        var handler = new QueueHandler(Json("[]"), Json(Issue(health, 5))); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNull();
        await Assert.That(handler.Paths.Last()).IsEqualTo("/repos/ehonda/KicktippAi/issues/5");
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Get]);
    }
    [Test]
    [Arguments("deleted")]
    [Arguments("foreign")]
    [Arguments("wrong-number")]
    [Arguments("oversized")]
    public async Task Bound_missing_or_foreign_issue_is_never_edited_or_replaced(string outcome)
    {
        var health = Health(); var fences = new MemoryFences(health, BundesligaContextSourceIssueFenceState.Bound);
        var response = outcome == "deleted" ? Json("{}", HttpStatusCode.NotFound)
            : Json(Issue(health, outcome == "wrong-number" ? 6 : 5, body: outcome == "foreign" ? "foreign" : Body(health)));
        if (outcome == "oversized") response.Content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
        var handler = new QueueHandler(Json("[]"), response); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Get]);
    }
    [Test]
    public async Task Unique_exact_marker_binds_legacy_and_PATCH_requires_current_health()
    {
        var health = Health(); var fences = new MemoryFences(health, BundesligaContextSourceIssueFenceState.LegacyUncertain);
        var handler = new QueueHandler(Json("[" + Issue(health, 5, body: health.DesiredIssueProjection!.Marker) + "]"), Json(Issue(health, 5)));
        using var client = Client(handler); await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNull();
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Patch]);
        await Assert.That(fences.Reads).IsEqualTo(2);
        var staleFences = new MemoryFences(health) { AfterBind = () => { } };
        staleFences.AfterBind = () => staleFences.Health = Health(run: 457);
        var staleHandler = new QueueHandler(Json("[" + Issue(health, 5, body: health.DesiredIssueProjection.Marker) + "]")); using var staleClient = Client(staleHandler);
        await Assert.That((await Projector(staleClient, staleFences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(staleHandler.Methods).IsEquivalentTo([HttpMethod.Get]);
    }
    [Test]
    public async Task Stale_POST_admission_and_synchronized_input_do_not_mutate()
    {
        var health = Health(); var fences = new MemoryFences(health);
        var handler = new QueueHandler(Json("[]")) { BeforeRequest = _ => fences.Health = Health(run: 457) };
        using var client = Client(handler); await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get]);
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Ready);
        var synchronized = health with { DesiredIssueProjection = health.DesiredIssueProjection! with { SynchronizationStatus = BundesligaContextSourceIssueSynchronization.Synchronized,
            AppliedBodySha256 = health.DesiredIssueProjection.BodySha256, LastAttemptedAtUtc = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero) } };
        var empty = new QueueHandler(); using var syncClient = Client(empty);
        await Projector(syncClient, new MemoryFences(synchronized)).ProjectAsync(synchronized);
        await Assert.That(empty.Methods).IsEmpty();
    }
    [Test]
    [Arguments("duplicate")]
    [Arguments("partial-marker")]
    [Arguments("PR")]
    [Arguments("malformed")]
    [Arguments("next-short-page")]
    public async Task Marker_listing_is_complete_exact_and_excludes_pull_requests(string scenario)
    {
        var health = Health(); var fences = new MemoryFences(health, BundesligaContextSourceIssueFenceState.LegacyUncertain);
        var content = scenario switch {
            "duplicate" => "[" + Issue(health, 5) + "," + Issue(health, 6) + "]",
            "partial-marker" => "[" + Issue(health, 5, body: "prefix" + health.DesiredIssueProjection!.Marker) + "]",
            "PR" => "[{\"pull_request\":{}}]", "malformed" => "{}", _ => "[]" };
        var response = Json(content); if (scenario == "next-short-page") response.Headers.TryAddWithoutValidation("Link", "<https://api.github.com/x>; rel=\"next\"");
        var handler = new QueueHandler(response); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get]);
    }
    [Test]
    public async Task Cancellation_after_arm_retains_uncertainty_and_performs_no_cleanup_HTTP()
    {
        var health = Health(); using var cancellation = new CancellationTokenSource();
        var fences = new MemoryFences(health) { AfterArm = cancellation.Cancel };
        var handler = new QueueHandler(Json("[]")); using var client = Client(handler);
        await Assert.That(() => Projector(client, fences).ProjectAsync(health, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.CreateUncertain);
        await Assert.That(handler.Methods).IsEquivalentTo([HttpMethod.Get]);
    }
    [Test]
    public async Task Wrong_repository_disabled_development_and_roster_inputs_have_zero_effects()
    {
        var health = Health(); var fences = new MemoryFences(health); var handler = new QueueHandler(); using var client = Client(handler);
        await Assert.That(() => new GitHubContextSourceIssueProjector(client, fences, "foreign/repo", true).ProjectAsync(health)).Throws<InvalidOperationException>();
        await Assert.That(() => new GitHubContextSourceIssueProjector(client, fences, "ehonda/KicktippAi", true, enabled: false).ProjectAsync(health)).Throws<InvalidOperationException>();
        var devIdentity = BundesligaContextSourceCycleIdentity.Development(health.Competition, "0198f865-1467-7000-8000-000000000000");
        var dev = health with { Scope = BundesligaContextSourceScope.Development, Watermark = new(devIdentity.Sequence, devIdentity.CycleId), DesiredIssueProjection = null };
        await Assert.That(() => Projector(client, fences).ProjectAsync(dev)).Throws<InvalidOperationException>();
        var rosterMarker = "<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->";
        var rosterBody = BundesligaContextSourceHealth.CreateIssueBody(rosterMarker, health.Competition, BundesligaContextSource.Rosters, health.Watermark, []);
        var roster = health with { Source = BundesligaContextSource.Rosters, RosterRevisionState = new(null, null), ActiveConditions = [],
            DesiredIssueProjection = health.DesiredIssueProjection! with { Marker = rosterMarker, Title = "[KicktippAi] Bundesliga 2026/27 rosters context-source health",
                BodySha256 = BundesligaContextSourceHealth.HashIssueBody(rosterBody), DesiredState = BundesligaContextSourceIssueState.Closed } };
        await Assert.That(() => Projector(client, fences).ProjectAsync(roster)).Throws<InvalidOperationException>();
        await Assert.That(fences.Reads).IsEqualTo(0); await Assert.That(handler.Methods).IsEmpty();
    }
    [Test]
    public async Task Infinite_request_timeout_is_rejected()
    {
        var health = Health(); using var client = Client(new QueueHandler()); client.Timeout = Timeout.InfiniteTimeSpan;
        await Assert.That(() => Projector(client, new MemoryFences(health))).Throws<InvalidOperationException>();
    }
    [Test]
    [Arguments("list")]
    [Arguments("POST")]
    [Arguments("PATCH")]
    public async Task Every_response_has_a_finite_byte_bound(string effect)
    {
        var health = Health(); var fences = new MemoryFences(health);
        var oversized = Json(Issue(health, 5), effect == "POST" ? HttpStatusCode.Created : HttpStatusCode.OK);
        oversized.Content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
        object[] responses = effect switch { "POST" => [Json("[]"), oversized],
            "PATCH" => [Json("[" + Issue(health, 5, body: health.DesiredIssueProjection!.Marker) + "]"), oversized], _ => [oversized] };
        var handler = new QueueHandler(responses); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(handler.Methods.Count(method => method == HttpMethod.Post)).IsEqualTo(effect == "POST" ? 1 : 0);
        if (effect == "POST") await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.CreateUncertain);
    }
    [Test]
    public async Task Listing_page_ceiling_and_repeated_page_identity_never_authorize_creation()
    {
        var health = Health();
        var pages = Enumerable.Range(0, 100).Select(page => (object)Json("[" + string.Join(",", Enumerable.Range(1, 100)
            .Select(offset => Issue(health, page * 100 + offset, body: "unrelated"))) + "]")).ToArray();
        var fences = new MemoryFences(health); var handler = new QueueHandler(pages); using var client = Client(handler);
        await Assert.That((await Projector(client, fences).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(handler.Methods).Count().IsEqualTo(100);
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Ready);
        var firstPage = "[" + string.Join(",", Enumerable.Range(1, 100).Select(number => Issue(health, number, body: "unrelated"))) + "]";
        var repeated = new QueueHandler(Json(firstPage), Json(firstPage)); using var repeatedClient = Client(repeated);
        await Assert.That((await Projector(repeatedClient, new MemoryFences(health)).ProjectAsync(health)).ErrorCode).IsNotNull();
        await Assert.That(repeated.Methods).IsEquivalentTo([HttpMethod.Get, HttpMethod.Get]);
    }
    [Test]
    public async Task Overall_budget_expires_across_bounded_pages_without_any_create()
    {
        var health = Health(); var fences = new MemoryFences(health); var time = new ManualTimeProvider();
        var pages = Enumerable.Range(0, 5).Select(page => (object)Json("[" + string.Join(",", Enumerable.Range(1, 100)
            .Select(offset => Issue(health, page * 100 + offset, body: "unrelated"))) + "]")).ToArray();
        var handler = new QueueHandler(pages) { BeforeRequest = _ => time.Advance(TimeSpan.FromSeconds(29)) };
        using var client = Client(handler);
        var result = await new GitHubContextSourceIssueProjector(client, fences, "ehonda/KicktippAi", true, time).ProjectAsync(health);
        await Assert.That(result.ErrorCode).IsNotNull(); await Assert.That(handler.Methods).Count().IsEqualTo(5);
        await Assert.That(fences.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Ready);
    }
    private sealed class ManualTimeProvider : TimeProvider
    {
        private TimeSpan _elapsed;
        private readonly List<ManualTimer> _timers = [];
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero) + _elapsed;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period); return timer;
        }
        public void Advance(TimeSpan amount)
        {
            _elapsed += amount;
            foreach (var timer in _timers.ToArray()) timer.Fire(_elapsed);
        }
        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? _due;
            public bool Change(TimeSpan dueTime, TimeSpan period) { _due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._elapsed + dueTime; return true; }
            public void Fire(TimeSpan elapsed) { if (_due is { } due && elapsed >= due) { _due = null; callback(state); } }
            public void Dispose() => _due = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private static GitHubContextSourceIssueProjector Projector(HttpClient client, MemoryFences fences) => new(client, fences, "ehonda/KicktippAi", true);
    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new("https://api.github.test/"), Timeout = TimeSpan.FromSeconds(30) };
    private static BundesligaContextSourceHealth Health(BundesligaContextSourceIssueState state = BundesligaContextSourceIssueState.Open, long run = 456)
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, run); var watermark = new BundesligaContextSourceWatermark(run, identity.CycleId);
        var conditions = state == BundesligaContextSourceIssueState.Open ? new[] { BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days } : [];
        var marker = BundesligaContextSourceIssueFenceIdentity.CanonicalMarker;
        var body = BundesligaContextSourceHealth.CreateIssueBody(marker, identity.Competition, BundesligaContextSource.ClubElo, watermark, conditions);
        var desired = new BundesligaContextSourceIssueProjection(marker, "[KicktippAi] Bundesliga 2026/27 club-elo context-source health", BundesligaContextSourceHealth.HashIssueBody(body), state, null, BundesligaContextSourceIssueSynchronization.Pending, null, null);
        return new(identity.Competition, identity.Scope, BundesligaContextSource.ClubElo, watermark, null, new(0, 0, 0, 0), new(null, null, null), null, [], conditions, desired);
    }
    private static string Body(BundesligaContextSourceHealth health) => BundesligaContextSourceHealth.CreateIssueBody(health.DesiredIssueProjection!.Marker, health.Competition, health.Source, health.Watermark, health.ActiveConditions);
    private static string Issue(BundesligaContextSourceHealth health, long number, string? body = null) => JsonSerializer.Serialize(new { number, title = health.DesiredIssueProjection!.Title, body = body ?? Body(health), state = health.DesiredIssueProjection.DesiredState == BundesligaContextSourceIssueState.Open ? "open" : "closed" });
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class QueueHandler(params object[] responses) : HttpMessageHandler
    {
        private readonly Queue<object> _responses = new(responses);
        public List<HttpMethod> Methods { get; } = []; public List<string> Paths { get; } = [];
        public Action<HttpMethod>? BeforeRequest { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method); Paths.Add(request.RequestUri!.AbsolutePath); BeforeRequest?.Invoke(request.Method);
            var next = _responses.Dequeue(); if (next is Exception exception) throw exception; return Task.FromResult((HttpResponseMessage)next);
        }
    }
    // Deterministic transport tests share one store across fresh projector instances. Actual CAS tests use Firestore separately.
    private sealed class MemoryFences : IBundesligaContextSourceIssueFenceRepository
    {
        private readonly object _gate = new();
        public BundesligaContextSourceHealth Health { get; set; }
        public BundesligaContextSourceIssueFence Fence { get; private set; }
        public int Reads { get; private set; }
        public bool LoseArmReply { get; set; } public bool FailBind { get; set; }
        public Action? AfterArm { get; init; } public Action? AfterBind { get; set; }
        public MemoryFences(BundesligaContextSourceHealth health, BundesligaContextSourceIssueFenceState state = BundesligaContextSourceIssueFenceState.Ready)
        {
            Health = health; var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
            Fence = new(BundesligaContextSourceIssueFenceIdentity.FromHealth(health), 0, state,
                state == BundesligaContextSourceIssueFenceState.CreateUncertain ? new(BundesligaContextSourceContract.NewClaimToken(), health.DesiredIssueProjection!.BodySha256, health.Watermark, health.DesiredIssueProjection.DesiredState, now) : null,
                state == BundesligaContextSourceIssueFenceState.Bound ? 5 : null, now);
        }
        private bool Current(BundesligaContextSourceHealth expected) => expected.Watermark == Health.Watermark && expected.LastCompletedCycleId == Health.LastCompletedCycleId
            && expected.DesiredIssueProjection == Health.DesiredIssueProjection && Health.DesiredIssueProjection!.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Pending;
        public Task<BundesligaContextSourceIssueFenceRead> ReadOrInitializeAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(new BundesligaContextSourceIssueFenceRead(Current(health), Fence)); }
        public Task<BundesligaContextSourceIssueCreateGrant?> TryArmCreateAsync(BundesligaContextSourceHealth health, long revision, BundesligaContextSourceIssueFenceAttempt attempt, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
            if (!Current(health) || Fence.Revision != revision || Fence.State != BundesligaContextSourceIssueFenceState.Ready) return Task.FromResult<BundesligaContextSourceIssueCreateGrant?>(null);
            Fence = Fence with { Revision = revision + 1, State = BundesligaContextSourceIssueFenceState.CreateUncertain, Attempt = attempt, UpdatedAtUtc = attempt.StartedAtUtc };
            AfterArm?.Invoke(); if (LoseArmReply) throw new IOException(); return Task.FromResult<BundesligaContextSourceIssueCreateGrant?>(new(Fence));
            }
        }
        public Task<BundesligaContextSourceIssueFenceBindResult> BindObservedIssueAsync(BundesligaContextSourceIssueFence expected, long number, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
            if (FailBind) throw new IOException();
            if (Fence.Revision != expected.Revision || Fence.IssueNumber is not null && Fence.IssueNumber != number)
                return Task.FromResult(new BundesligaContextSourceIssueFenceBindResult(BundesligaContextSourceIssueFenceBindDisposition.Conflict, Fence));
            Fence = Fence with { Revision = Fence.Revision + 1, State = BundesligaContextSourceIssueFenceState.Bound, IssueNumber = number };
            AfterBind?.Invoke(); return Task.FromResult(new BundesligaContextSourceIssueFenceBindResult(BundesligaContextSourceIssueFenceBindDisposition.Bound, Fence));
            }
        }
    }
}
