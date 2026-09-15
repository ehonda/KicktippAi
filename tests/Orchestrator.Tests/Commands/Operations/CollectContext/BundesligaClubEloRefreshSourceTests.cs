using System.Net;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class BundesligaClubEloRefreshSourceTests
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    internal static readonly BundesligaContextSourceCycleIdentity Cycle = BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27, 1, 100);
    internal static string Fixture(string name = "eligible.html") => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Commands", "Operations", "CollectContext", "Fixtures", "ClubElo", name));

    [Test]
    [Arguments("eligible.html", "Eligible")]
    [Arguments("unknown-source-date.html", "DateRejected")]
    [Arguments("partial.html", "CoverageRejected")]
    [Arguments("hostile-dom.html", "DomRejected")]
    [Arguments("hostile-lexer.html", "LexerRejected")]
    [Arguments("hostile-fragment.html", "FragmentRejected")]
    public async Task Six_fixtures_prove_first_failed_parser_gate(string fixture, string expected)
    {
        var result = await Observe(Fixture(fixture));
        await Assert.That(Evaluation(result)).IsEqualTo(expected);
        result.Validate();
        await Assert.That(result.PayloadBytes is not null).IsEqualTo(expected == "Eligible");
        using var descriptor = JsonDocument.Parse(result.Observation.DescriptorJson);
        if (expected == "Eligible")
        {
            var rows = descriptor.RootElement.GetProperty("sourceRows").EnumerateArray().ToArray();
            await Assert.That(rows[0].GetProperty("globalRank").GetInt32()).IsEqualTo(18);
            await Assert.That(rows[^1].GetProperty("globalRank").GetInt32()).IsEqualTo(1);
            await Assert.That(rows.Select(row => row.GetProperty("elo").GetInt32()).Distinct().Count()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Exact_get_uri_has_no_discretionary_headers_and_handler_disables_redirects_decoding_and_cookies()
    {
        using var handler = BundesligaClubEloRefreshSource.CreateHandler();
        await Assert.That(handler.AllowAutoRedirect).IsFalse();
        await Assert.That(handler.AutomaticDecompression).IsEqualTo(DecompressionMethods.None);
        await Assert.That(handler.UseCookies).IsFalse();
        var calls = 0;
        using var stub = new Handler((request, _) =>
        {
            calls++;
            if (request.Method != HttpMethod.Get || request.RequestUri?.AbsoluteUri != "https://clubelo.com/GER"
                || request.Headers.Any() || request.Content is not null) throw new InvalidDataException("Unexpected request.");
            return Task.FromResult(Response(Fixture().Replace("</head>", "<script src=\"https://evil.invalid/script.js\"></script><link rel=\"stylesheet\" href=\"https://evil.invalid/style.css\"></head>")));
        });
        var source = Create(stub);
        await Assert.That(Evaluation(await source.ObserveAsync(Cycle))).IsEqualTo("Eligible");
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("status")]
    [Arguments("redirect")]
    [Arguments("final-url")]
    [Arguments("media")]
    [Arguments("no-charset")]
    [Arguments("wrong-charset")]
    [Arguments("two-charsets")]
    [Arguments("encoding")]
    [Arguments("two-content-types")]
    public async Task Each_response_predicate_fails_before_invalid_dom(string defect)
    {
        var response = Response("not HTML");
        switch (defect)
        {
            case "status": response.StatusCode = HttpStatusCode.NotFound; break;
            case "redirect": response.StatusCode = HttpStatusCode.Found; response.Headers.Location = new Uri("/elsewhere", UriKind.Relative); break;
            case "final-url": response.RequestMessage = new(HttpMethod.Get, "https://clubelo.com/other"); break;
            case "media": SetType(response, "application/json; charset=utf-8"); break;
            case "no-charset": SetType(response, "text/html"); break;
            case "wrong-charset": SetType(response, "text/html; charset=windows-1252"); break;
            case "two-charsets": SetType(response, "text/html; charset=utf-8; charset=utf-8"); break;
            case "encoding": response.Content.Headers.ContentEncoding.Add("gzip"); break;
            case "two-content-types": SetType(response, "text/html; charset=utf-8", "text/html; charset=utf-8"); break;
        }
        await Assert.That(Evaluation(await Observe(response))).IsEqualTo("ResponseRejected");
    }

    [Test]
    public async Task Utf8_case_and_quotes_are_canonicalized_and_same_response_heading_is_the_only_date()
    {
        var response = Response(Fixture());
        SetType(response, "text/html; charset=\"UTF-8\"");
        var result = await Observe(response);
        await Assert.That(Evaluation(result)).IsEqualTo("Eligible");
        using var descriptor = JsonDocument.Parse(result.Observation.DescriptorJson);
        await Assert.That(descriptor.RootElement.GetProperty("response").GetProperty("charset").GetString()).IsEqualTo("utf-8");
        var undated = Response(Fixture("unknown-source-date.html").Replace("</body>", "<a href=\"/2026-09-06/GER\">Germany</a></body>"));
        undated.Headers.Date = Now;
        undated.Content.Headers.LastModified = Now;
        await Assert.That(Evaluation(await Observe(undated))).IsEqualTo("DateRejected");
    }

    [Test]
    [Arguments("/2026-02-30/GER")]
    [Arguments("/2026-09-07/GER")]
    [Arguments("/2026-9-06/GER")]
    [Arguments("/2026-09-06/GER?x=1")]
    [Arguments("/2026-09-06/GER\n")]
    [Arguments("https://clubelo.com/2026-09-06/GER")]
    public async Task Invalid_untrimmed_heading_date_is_rejected(string href)
        => await Assert.That(Evaluation(await Observe(Fixture().Replace("/2026-09-06/GER", href)))).IsEqualTo("DateRejected");

    [Test]
    public async Task Size_gates_keep_exact_order_and_only_complete_hashes()
    {
        var advertised = new ReadProbeStream([1, 2, 3]);
        var response = Response(advertised); response.Content.Headers.ContentLength = BundesligaClubEloRefresh.MaximumBytes + 1;
        response.StatusCode = HttpStatusCode.InternalServerError;
        var result = await Observe(response);
        await Assert.That(Evaluation(result)).IsEqualTo("SizeRejected");
        await Assert.That(advertised.BytesRead).IsEqualTo(0);
        await RawPair(result, null);

        var over = new ReadProbeStream(new byte[BundesligaClubEloRefresh.MaximumBytes + 100]);
        result = await Observe(Response(over));
        await Assert.That(Evaluation(result)).IsEqualTo("SizeRejected");
        await Assert.That(over.BytesRead).IsEqualTo(BundesligaClubEloRefresh.MaximumBytes + 1);
        await RawPair(result, null);

        result = await Observe(Response(new ReadProbeStream([])));
        await Assert.That(Evaluation(result)).IsEqualTo("SizeRejected");
        await RawPair(result, []);

        var mismatch = Response(new ReadProbeStream([1, 2, 3])); mismatch.Content.Headers.ContentLength = 2;
        result = await Observe(mismatch);
        await Assert.That(Evaluation(result)).IsEqualTo("SizeRejected");
        await RawPair(result, [1, 2, 3]);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2097152)]
    public async Task Inclusive_body_boundaries_pass_size_before_dom(int length)
    {
        var bytes = Enumerable.Repeat((byte)' ', length).ToArray();
        var response = Response(new ReadProbeStream(bytes)); response.Content.Headers.ContentLength = length;
        var result = await Observe(response);
        await Assert.That(Evaluation(result)).IsEqualTo("DomRejected");
        await RawPair(result, bytes);
    }

    [Test]
    public async Task Failed_read_has_no_partial_raw_facts_and_uses_terminal_attempt_only()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        using var handler = new Handler((_, _) =>
        {
            attempts++;
            return attempts == 1 ? Task.FromResult(Response(new ReadProbeStream([1, 2, 3], failAt: 1)))
                : throw new TaskCanceledException("terminal timeout");
        });
        var result = await Create(handler, delays: delays).ObserveAsync(Cycle);
        await Assert.That(Evaluation(result)).IsEqualTo("TransportRejected");
        await Assert.That(result.Observation.Diagnostics.SequenceEqual(new[] { "CLUB_ELO_TIMEOUT", "CLUB_ELO_TRANSPORT_REJECTED" })).IsTrue();
        await RawPair(result, null);
        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(delays.Single()).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    [Arguments(408)]
    [Arguments(429)]
    [Arguments(500)]
    [Arguments(599)]
    public async Task Only_retryable_http_statuses_retry_with_one_five_second_delay(int status)
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        using var handler = new Handler((_, _) => { attempts++; return Task.FromResult(Response("failed", status)); });
        var result = await Create(handler, delays: delays).ObserveAsync(Cycle);
        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(delays.Single()).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(result.Observation.Diagnostics.SequenceEqual(new[] { "CLUB_ELO_HTTP_REJECTED", "CLUB_ELO_TRANSPORT_REJECTED" })).IsTrue();
    }

    [Test]
    [Arguments(301)]
    [Arguments(403)]
    [Arguments(404)]
    [Arguments(600)]
    public async Task Redirects_and_nonretryable_statuses_never_retry(int status)
    {
        var attempts = 0;
        using var handler = new Handler((_, _) => { attempts++; return Task.FromResult(Response("failed", status)); });
        await Assert.That(Evaluation(await Create(handler).ObserveAsync(Cycle))).IsEqualTo("ResponseRejected");
        await Assert.That(attempts).IsEqualTo(1);
    }

    [Test]
    public async Task Connection_retry_can_succeed_and_monotonic_budget_stops_at_25_seconds()
    {
        var attempts = 0;
        using var recovery = new Handler((_, _) => ++attempts == 1 ? throw new HttpRequestException("connection") : Task.FromResult(Response(Fixture())));
        await Assert.That(Evaluation(await Create(recovery).ObserveAsync(Cycle))).IsEqualTo("Eligible");
        var clock = new Clock(); attempts = 0;
        using var timeout = new Handler((_, token) => { attempts++; clock.Advance(TimeSpan.FromSeconds(10)); token.ThrowIfCancellationRequested(); throw new InvalidOperationException("Attempt timeout did not cancel transport."); });
        var result = await Create(timeout, clock: clock).ObserveAsync(Cycle);
        await Assert.That(Evaluation(result)).IsEqualTo("TransportRejected");
        await Assert.That(attempts).IsEqualTo(2);
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.FromSeconds(25));
        await Assert.That(clock.DueTimes.SequenceEqual(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10) })).IsTrue();
    }

    [Test]
    public async Task Caller_cancellation_rethrows_and_mapping_io_is_not_transport_evidence()
    {
        using var cancel = new CancellationTokenSource();
        var attempts = 0;
        using var handler = new Handler((_, token) => { attempts++; cancel.Cancel(); token.ThrowIfCancellationRequested(); throw new Exception(); });
        await Assert.That(() => Create(handler).ObserveAsync(Cycle, cancel.Token)).Throws<OperationCanceledException>();
        await Assert.That(attempts).IsEqualTo(1);
        attempts = 0;
        await Assert.That(() => Create(handler, mapping: _ => throw new IOException("mapping unavailable")).ObserveAsync(Cycle)).Throws<IOException>();
        await Assert.That(attempts).IsEqualTo(0);
    }

    [Test]
    public async Task Unrepresentable_response_evidence_is_fatal_and_does_not_retry_as_transport()
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        {
            calls++;
            var response = Response(Fixture());
            response.RequestMessage = new(HttpMethod.Get, "http://clubelo.com/GER");
            return Task.FromResult(response);
        });
        await Assert.That(() => Create(handler).ObserveAsync(Cycle)).Throws<InvalidDataException>();
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("<div class=\"blatt\">", "<div class=\"blatt\"><div class=\"blatt\"></div>")]
    [Arguments("<tbody></tbody>", "<tbody><tr><td>bad</td></tr></tbody>")]
    [Arguments("<h1>", "<div><h1>")]
    [Arguments("<th>Club</th>", "<th><span>Club</span></th>")]
    [Arguments("<script>", "<script src=\"https://evil.invalid/payload.js\">")]
    [Arguments("</table></div><script>", "</table></div><div></div><script>")]
    public async Task Hostile_dom_never_reaches_the_lexer(string find, string replacement)
        => await Assert.That(Evaluation(await Observe(Fixture("hostile-lexer.html").Replace(find, replacement)))).IsEqualTo("DomRejected");

    [Test]
    [Arguments("const eloData", "let eloData")]
    [Arguments("const eloData", "consteloData")]
    [Arguments("'1500'", "1500")]
    [Arguments("'1500'", "'15' + '00'")]
    [Arguments("'1500'", "'\\v1500'")]
    [Arguments("'1500'", "'\\uD800'")]
    [Arguments("'1500'", "'\\xGG'")]
    [Arguments("'1500'", "'\\u0065\\u0301'")]
    [Arguments("JSSortableEloTable(eloData);", "JSSortableEloTable(eloData); eloData;")]
    [Arguments("JSSortableEloTable(eloData);", "JSSortableEloTable(eloData); JSSortableEloTable(eloData);")]
    [Arguments("JSSortableEloTable(eloData);", "")]
    [Arguments("const eloData = [", "const eloData = /*comment*/ [")]
    public async Task Hostile_literals_are_not_executed_or_coerced(string find, string replacement)
        => await Assert.That(Evaluation(await Observe(Fixture().Replace(find, replacement)))).IsEqualTo("LexerRejected");

    [Test]
    [Arguments("<td>", "<td onclick=\"alert(1)\">")]
    [Arguments("<td>", "<td x=\"1\">")]
    [Arguments("href=\"/GER\"", "href=\"/GER\" href=\"/GER\"")]
    [Arguments("<small>GER</small>", "<small><span>GER</span></small>")]
    [Arguments("<small>GER</small>", "<small>GER")]
    [Arguments("<small>GER</small>", "<small>&invalid;</small>")]
    [Arguments("<small>GER</small>", "<!--x--><small>GER</small>")]
    [Arguments("href=\"/Stuttgart\"", "href=\"/GER\"")]
    [Arguments("href=\"/Stuttgart\"", "href=\"//Stuttgart\"")]
    [Arguments("href=\"/Stuttgart\"", "href=\"/Stutt%67art\"")]
    [Arguments("href=\"/Stuttgart\"", "href=\"/Stuttgart?q=1\"")]
    [Arguments("href=\"/Stuttgart\"", "href=\"/Stuttgart#fragment\"")]
    [Arguments("href=\"/Stuttgart\"", "href=\"javascript:alert(1)\"")]
    [Arguments(">1</a>", ">0</a>")]
    [Arguments(">1</a>", ">2147483648</a>")]
    [Arguments(">1</a>", ">2</a>")]
    [Arguments("'1500'", "'1500.25'")]
    [Arguments("'1500'", "'1e3'")]
    [Arguments("'1500'", "'0'")]
    [Arguments("'1500'", "'2147483648'")]
    public async Task Hostile_fragments_and_nonpositive_or_fractional_numbers_fail(string find, string replacement)
        => await Assert.That(Evaluation(await Observe(Fixture().Replace(find, replacement)))).IsEqualTo("FragmentRejected");

    [Test]
    public async Task Literal_bounds_are_inclusive_for_rows_strings_and_script_and_minimal_escapes_decode()
    {
        var fragment = "<td><a href=\"/GER\">{0}</a><small>GER</small><a href=\"/Extra{0}\">Extra{0}</a></td>";
        string Row(int index, string last = "0") => "['" + string.Format(fragment, index) + "', '1500', '0', '" + last + "']";
        string Html(IEnumerable<string> rows) => Fixture().Split("const eloData = [")[0] + "const eloData = [" + string.Join(',', rows) + "]; JSSortableEloTable(eloData);</script></div></body></html>";
        await Assert.That(Evaluation(await Observe(Html(Enumerable.Range(1, 512).Select(index => Row(index)))))).IsEqualTo("CoverageRejected");
        await Assert.That(Evaluation(await Observe(Html(Enumerable.Range(1, 513).Select(index => Row(index)))))).IsEqualTo("LexerRejected");
        await Assert.That(Evaluation(await Observe(Html([Row(1, new string('x', 8192))])))).IsEqualTo("CoverageRejected");
        await Assert.That(Evaluation(await Observe(Html([Row(1, new string('x', 8193))])))).IsEqualTo("LexerRejected");
        var minimal = "const eloData = []; JSSortableEloTable(eloData);";
        var empty = Html([]);
        await Assert.That(Evaluation(await Observe(empty.Replace(minimal, minimal.PadRight(262144))))).IsEqualTo("CoverageRejected");
        await Assert.That(Evaluation(await Observe(empty.Replace(minimal, minimal.PadRight(262145))))).IsEqualTo("LexerRejected");
        var escaped = Fixture().Replace("'1500'", "'\\x31\\u003500'").Replace("'0', '0'", "'\\\\\\\'\\n\\r\\t', '0'");
        await Assert.That(Evaluation(await Observe(escaped))).IsEqualTo("Eligible");
    }

    [Test]
    public async Task Freeze_reads_every_lane_and_a_lagging_last_lane_keeps_the_payload()
    {
        var repository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        var reads = new List<string>();
        var index = 0;
        repository.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((DocumentPublicationDefinition _, string community, CancellationToken _) =>
            {
                reads.Add(community); index++;
                return Task.FromResult(index == 8 ? null : Loaded(community, new DateOnly(2026, 9, 6)));
            });
        using var handler = new Handler((_, _) => Task.FromResult(Response(Fixture())));
        var result = await Create(handler, repository: repository.Object).ObserveAsync(Cycle);
        await Assert.That(reads.Count).IsEqualTo(8);
        await Assert.That(reads.SequenceEqual(new[] { "pes-squad", "schadensfresse", "relaxdays-tippt", "ehonda-ai-arena", "ehonda-ai-arena", "ehonda-ai-arena", "ehonda-ai-arena", "ehonda-ai-arena" })).IsTrue();
        await Assert.That(Evaluation(result)).IsEqualTo("Eligible");
    }

    [Test]
    public async Task Corrupt_or_crossed_retained_evidence_fails_before_http()
    {
        var repository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Loaded("wrong-community", new DateOnly(2026, 9, 6)));
        var calls = 0;
        using var handler = new Handler((_, _) => { calls++; throw new Exception("Must not acquire."); });
        await Assert.That(() => Create(handler, repository: repository.Object).ObserveAsync(Cycle)).Throws<InvalidDataException>();
        await Assert.That(calls).IsEqualTo(0);
    }

    internal static LoadedDocumentPublication Loaded(string community, DateOnly date)
    {
        var snapshot = BundesligaClubEloSnapshot.Create(BundesligaClubEloSeed.Default.Entries, date, Now,
            new Uri(BundesligaClubEloRefresh.SourceUrl), BundesligaClubEloSnapshotOrigin.NetworkCandidate);
        var publication = BundesligaClubEloPublication.Build(new(snapshot, BundesligaClubEloSelectionDisposition.NetworkAccepted, []));
        var id = DocumentPublicationContract.ComputeSnapshotId(publication.Documents);
        var entries = publication.Documents.Select(payload => new DocumentPublicationEntry(payload.Kind, payload.Name, 1,
            DocumentPublicationContract.ComputeContentSha256(payload.Content))).ToArray();
        var stored = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, community, "club-elo", id, null, Now, publication.MetadataJson, entries);
        return new(stored, publication.Documents.Select(payload => new PublishedDocument(CompetitionIds.Bundesliga2026_27,
            community, "club-elo", payload.Kind, payload.Name, 1, payload.Content, payload.Description, Now)));
    }

    internal static string Evaluation(BundesligaContextSourceObservationResult result)
    {
        using var document = JsonDocument.Parse(result.Observation.DescriptorJson);
        return document.RootElement.GetProperty("evaluation").GetString()!;
    }
    private static async Task RawPair(BundesligaContextSourceObservationResult result, byte[]? bytes)
    {
        using var document = JsonDocument.Parse(result.Observation.DescriptorJson);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("rawSha256").GetString()).IsEqualTo(bytes is null ? null : BundesligaContextSourceHashing.Sha256(bytes));
        await Assert.That(root.GetProperty("rawByteLength").ValueKind == JsonValueKind.Null ? (long?)null : root.GetProperty("rawByteLength").GetInt64()).IsEqualTo(bytes?.LongLength);
        await Assert.That(root.GetProperty("sourceRows").ValueKind).IsEqualTo(JsonValueKind.Null);
    }
    private static Task<BundesligaContextSourceObservationResult> Observe(string html) => Observe(Response(html));
    private static async Task<BundesligaContextSourceObservationResult> Observe(HttpResponseMessage response)
    {
        using var handler = new Handler((_, _) => Task.FromResult(response));
        return await Create(handler).ObserveAsync(Cycle);
    }
    private static BundesligaClubEloRefreshSource Create(Handler handler, List<TimeSpan>? delays = null, Clock? clock = null,
        IDocumentPublicationRepository? repository = null, Func<CancellationToken, Task<byte[]>>? mapping = null)
    {
        var seed = new Mock<IBundesligaClubEloSource>();
        seed.Setup(value => value.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Default));
        if (repository is null)
        {
            var mock = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
            mock.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LoadedDocumentPublication?)null);
            repository = mock.Object;
        }
        var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(repository);
        clock ??= new Clock();
        return new(new HttpClient(handler), factory.Object, seed.Object, mapping ?? (_ => Task.FromResult(BundesligaClubEloRefresh.CanonicalMappingBytes())), clock,
            (duration, token) => { token.ThrowIfCancellationRequested(); delays?.Add(duration); clock.Advance(duration); return Task.CompletedTask; });
    }
    private static HttpResponseMessage Response(string text, int status = 200) => Response(new ReadProbeStream(Encoding.UTF8.GetBytes(text)), status);
    private static HttpResponseMessage Response(Stream stream, int status = 200)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StreamContent(stream), RequestMessage = new(HttpMethod.Get, BundesligaClubEloRefresh.SourceUrl) };
        SetType(response, "text/html; charset=utf-8"); return response;
    }
    private static void SetType(HttpResponseMessage response, params string[] types)
    {
        response.Content.Headers.Remove("Content-Type");
        response.Content.Headers.TryAddWithoutValidation("Content-Type", types);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class Clock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        public List<TimeSpan> DueTimes { get; } = [];
        public TimeSpan Elapsed { get; private set; }
        public override DateTimeOffset GetUtcNow() => Now + Elapsed;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DueTimes.Add(dueTime);
            var timer = new ManualTimer(callback, state, Elapsed + dueTime);
            _timers.Add(timer); return timer;
        }
        public void Advance(TimeSpan duration)
        {
            Elapsed += duration;
            foreach (var timer in _timers.ToArray()) timer.Fire(Elapsed);
        }
        private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
        {
            private bool _disposed;
            public void Fire(TimeSpan elapsed) { if (!_disposed && elapsed >= due) { _disposed = true; callback(state); } }
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class ReadProbeStream(byte[] bytes, int? failAt = null) : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (failAt is { } limit && BytesRead >= limit) throw new IOException("partial body");
            var available = Math.Min(count, bytes.Length - BytesRead);
            if (failAt is { } stop) available = Math.Min(available, stop - BytesRead);
            bytes.AsSpan(BytesRead, available).CopyTo(buffer.AsSpan(offset, available)); BytesRead += available; return available;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); var temporary = new byte[buffer.Length]; var count = Read(temporary, 0, temporary.Length);
            temporary.AsSpan(0, count).CopyTo(buffer.Span); return ValueTask.FromResult(count);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
