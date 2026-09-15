using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>Dormant, bounded official-HTML source. Registration and activation belong to W1.</summary>
public sealed class BundesligaClubEloRefreshSource : IBundesligaContextSourceObservationProvider
{
    private readonly HttpClient _client;
    private readonly IFirebaseServiceFactory _firebase;
    private readonly IBundesligaClubEloSource _seed;
    private readonly Func<CancellationToken, Task<byte[]>> _mapping;
    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public BundesligaClubEloRefreshSource(HttpClient client, IFirebaseServiceFactory firebase,
        IBundesligaClubEloSource seed, Func<CancellationToken, Task<byte[]>> mapping,
        TimeProvider? timeProvider = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _client = client;
        _firebase = firebase;
        _seed = seed;
        _mapping = mapping;
        _time = timeProvider ?? TimeProvider.System;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, _time, token));
    }

    public BundesligaContextSource Source => BundesligaContextSource.ClubElo;

    // W1 must use this handler; an injected test handler cannot make a live request.
    public static HttpClientHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    };

    public async Task<BundesligaContextSourceObservationResult> ObserveAsync(
        BundesligaContextSourceCycleIdentity cycle, CancellationToken cancellationToken = default)
    {
        var retained = await LoadRetainedSelectionsAsync(cycle, cancellationToken);
        var mapping = await _mapping(cancellationToken);
        var started = _time.GetTimestamp();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = TimeSpan.FromSeconds(25) - _time.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                return Reject("CLUB_ELO_TIMEOUT");
            using var timeout = new CancellationTokenSource(remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10), _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            string? terminalCause = null;
            (BundesligaClubEloRefresh.Response Facts, byte[]? Bytes)? captured = null;
            try
            {
                captured = await AcquireAsync(linked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) { terminalCause = "CLUB_ELO_TIMEOUT"; }
            catch (HttpRequestException) { terminalCause = "CLUB_ELO_CONNECTION_FAILED"; }
            catch (IOException) { terminalCause = "CLUB_ELO_CONNECTION_FAILED"; }

            // Parsing and contract validation are outside the transport catch: an invalid artifact
            // or programming fault must never masquerade as a connection failure or trigger HTTP.
            if (captured is { } completed)
            {
                var (facts, bytes) = completed;
                if (bytes is null || bytes.Length == 0 || facts.DeclaredContentLength is { } declared && declared != bytes.LongLength)
                    return Result("SizeRejected", facts, bytes);

                if (facts.StatusCode is 408 or 429 or >= 500 and <= 599)
                {
                    terminalCause = "CLUB_ELO_HTTP_REJECTED";
                }
                else
                {
                    if (!facts.IsAccepting) return Result("ResponseRejected", facts, bytes);
                    // Capture the complete byte identity before any decoding or parsing. The
                    // resulting descriptor must still bind exactly those same bytes.
                    var rawSha256 = BundesligaContextSourceHashing.Sha256(bytes);
                    var observed = ObservedAt();
                    var parsed = Parse(bytes);
                    var result = parsed.Evaluation is { } rejected
                        ? BundesligaClubEloRefresh.CreateObservation(cycle, observed, rejected, facts, bytes)
                        : BundesligaClubEloRefresh.Evaluate(cycle, observed, facts, bytes, parsed.DisplayedDate, parsed.Rows!, mapping, retained);
                    using var descriptor = JsonDocument.Parse(result.Observation.DescriptorJson);
                    if (descriptor.RootElement.GetProperty("rawSha256").GetString() != rawSha256)
                        throw new InvalidDataException("Parsed Club Elo evidence changed the captured raw identity.");
                    return result;
                }
            }

            if (attempt == 1 || _time.GetElapsedTime(started) + TimeSpan.FromSeconds(5) >= TimeSpan.FromSeconds(25))
                return Reject(terminalCause);
            await _delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        throw new InvalidOperationException("Bounded Club Elo attempt loop did not terminate.");

        BundesligaContextSourceObservationResult Reject(string? cause) => BundesligaClubEloRefresh.CreateObservation(
            cycle, ObservedAt(), "TransportRejected", terminalCause: cause);
        BundesligaContextSourceObservationResult Result(string evaluation, BundesligaClubEloRefresh.Response response, byte[]? bytes = null) =>
            BundesligaClubEloRefresh.CreateObservation(cycle, ObservedAt(), evaluation, response, bytes);
    }

    private async Task<(BundesligaClubEloRefresh.Response Facts, byte[]? Bytes)> AcquireAsync(CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BundesligaClubEloRefresh.SourceUrl);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        var facts = ReadResponse(response);
        // Null bytes mean an advertised or observed size proof, never a partial hash.
        if (facts.DeclaredContentLength > BundesligaClubEloRefresh.MaximumBytes) return (facts, null);
        await using var body = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        while (buffer.Length <= BundesligaClubEloRefresh.MaximumBytes)
        {
            var count = Math.Min(chunk.Length, BundesligaClubEloRefresh.MaximumBytes + 1 - (int)buffer.Length);
            var read = await body.ReadAsync(chunk.AsMemory(0, count), token);
            if (read == 0) break;
            buffer.Write(chunk, 0, read);
        }
        return (facts, buffer.Length > BundesligaClubEloRefresh.MaximumBytes ? null : buffer.ToArray());
    }

    private DateTimeOffset ObservedAt()
    {
        var now = _time.GetUtcNow();
        return new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    internal async Task<IReadOnlyDictionary<string, BundesligaClubEloSnapshot>> LoadRetainedSelectionsAsync(
        BundesligaContextSourceCycleIdentity cycle, CancellationToken token)
    {
        _ = BundesligaContextSourceCycleIdentity.Create(cycle.Competition, cycle.Scope, cycle.CycleId, cycle.Sequence);
        var seed = await _seed.GetLatestAsync(token);
        if (!seed.IsComplete || seed.Snapshot?.Origin != BundesligaClubEloSnapshotOrigin.LaunchSeed)
            throw new InvalidDataException("Club Elo shared evaluation requires a valid complete launch seed.");
        var repository = _firebase.CreateDocumentPublicationRepository(cycle.Competition);
        var consumers = cycle.Scope == BundesligaContextSourceScope.ProductionLive
            ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        var retained = new Dictionary<string, BundesligaClubEloSnapshot>(StringComparer.Ordinal);
        // Read every consumer independently, including arena lanes. Health dates and the producer
        // alone cannot authorize a shared NotNewer result.
        foreach (var lane in consumers)
        {
            var community = Community(cycle.Scope, lane);
            var loaded = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community, token);
            if (loaded is not null)
                DocumentPublicationContract.ValidateLoaded(cycle.Competition, community, BundesligaDocumentPublication.ClubElo, loaded.Snapshot, loaded.Documents);
            retained.Add(lane, loaded is null ? seed.Snapshot : BundesligaClubEloPublication.ReconstructLastKnownGood(loaded));
        }
        BundesligaClubEloRefresh.ValidateRetainedSelections(cycle, retained);
        return retained;
    }

    internal static string Community(BundesligaContextSourceScope scope, string lane)
    {
        var community = scope == BundesligaContextSourceScope.Development ? BundesligaContextSourceContract.DevelopmentCommunity
            : lane switch
            {
                "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse",
                "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena"
            };
        BundesligaContextSourceContract.ValidateConsumerAuthority(scope, lane, community);
        return community;
    }

    private static BundesligaClubEloRefresh.Response ReadResponse(HttpResponseMessage response)
    {
        var contentTypes = response.Content.Headers.TryGetValues("Content-Type", out var types) ? types.ToArray() : [];
        string? media = null;
        string? charset = null;
        if (contentTypes.Length == 1 && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentTypes[0], out var type))
        {
            media = type.MediaType?.ToLowerInvariant();
            var charsets = type.Parameters.Where(parameter => parameter.Name.Equals("charset", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (charsets.Length == 1 && charsets[0].Value?.Trim('"').Equals("utf-8", StringComparison.OrdinalIgnoreCase) == true)
                charset = "utf-8";
            else if (charsets.Length == 1) charset = charsets[0].Value;
        }
        var locations = response.Headers.TryGetValues("Location", out var values) ? values.ToArray() : [];
        long? length = null;
        if (response.Content.Headers.TryGetValues("Content-Length", out var lengths))
        {
            var all = lengths.ToArray();
            if (all.Length == 1 && long.TryParse(all[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                length = parsed;
        }
        return new((int)response.StatusCode, response.RequestMessage?.RequestUri?.AbsoluteUri ?? "",
            0, locations.Length == 0 ? null : string.Join(",", locations), media, charset,
            response.Content.Headers.ContentEncoding.ToArray(), length);
    }

    private sealed record ParseResult(string? Evaluation, string? DisplayedDate = null, IReadOnlyList<BundesligaClubEloRefresh.Row>? Rows = null);

    private static ParseResult Parse(byte[] bytes)
    {
        string html;
        try { html = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return new("DomRejected"); }
        // Parsing an isolated document has no browsing context, loaders, script engine or navigation.
        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = false });
        using var document = parser.ParseDocument(html);
        var sheets = document.QuerySelectorAll("div.blatt");
        var tables = document.QuerySelectorAll("table#eloTable");
        if (sheets.Length != 1 || tables.Length != 1) return new("DomRejected");
        var sheet = sheets[0];
        var headings = sheet.Children.Where(child => child.LocalName == "h1").ToArray();
        if (headings.Length != 1 || headings[0].Children.Length != 1 || headings[0].Children[0].LocalName != "a"
            || headings[0].Children[0].TextContent != "Germany" || headings[0].Children[0].Children.Length != 0)
            return new("DomRejected");
        var table = tables[0];
        var wrapper = table.ParentElement;
        if (wrapper?.ParentElement != sheet || wrapper.Children.Length != 1
            || !table.Children.Select(element => element.LocalName).SequenceEqual(new[] { "thead", "tbody" }))
            return new("DomRejected");
        var head = table.Children[0];
        var body = table.Children[1];
        if (head.Children.Length != 1 || head.Children[0].LocalName != "tr" || body.Children.Length != 0
            || body.ChildNodes.Any(node => node is not IComment && (node is not IText text || !string.IsNullOrWhiteSpace(text.Data))))
            return new("DomRejected");
        var headers = head.Children[0].Children;
        if (headers.Length != 4 || headers.Any(element => element.LocalName != "th" || element.Children.Length != 0)
            || !headers.Select(element => element.TextContent).SequenceEqual(new[] { "Club", "Elo", "+/-", "Golo" }))
            return new("DomRejected");
        var script = wrapper.NextElementSibling;
        if (script?.LocalName != "script" || script.Attributes.Length != 0) return new("DomRejected");
        IReadOnlyList<string[]> cells;
        try { cells = new LiteralLexer(script.TextContent).Read(); }
        catch (InvalidDataException) { return new("LexerRejected"); }
        var rows = new List<BundesligaClubEloRefresh.Row>();
        var routes = new HashSet<string>(StringComparer.Ordinal);
        var previousRank = 0;
        foreach (var row in cells)
        {
            var parsed = ParseFragment(row[0], row[1]);
            if (parsed is null || parsed.GlobalRank <= previousRank || !routes.Add(parsed.ProviderRoute)) return new("FragmentRejected");
            previousRank = parsed.GlobalRank;
            rows.Add(parsed);
        }
        var href = headings[0].Children[0].GetAttribute("href");
        var date = href is not null && Regex.IsMatch(href, "\\A/[0-9]{4}-[0-9]{2}-[0-9]{2}/GER\\z", RegexOptions.CultureInvariant)
            ? href.Substring(1, 10) : null;
        return new(null, date, rows);
    }

    private static BundesligaClubEloRefresh.Row? ParseFragment(string fragment, string elo)
    {
        // A lexical envelope rejects repairs (omitted close tags, duplicate/event attributes,
        // unknown descendants, comments) before the fresh inert tr-context DOM is inspected.
        const string pattern = "\\A[ \\t\\r\\n]*<td>[ \\t\\r\\n]*<a[ \\t\\r\\n]+href=(?:\"[^\"]*\"|'[^']*')>[^<]+</a>[ \\t\\r\\n]*<small>[^<]+</small>[ \\t\\r\\n]*<a[ \\t\\r\\n]+href=(?:\"[^\"]*\"|'[^']*')>[^<]+</a>[ \\t\\r\\n]*</td>[ \\t\\r\\n]*\\z";
        if (!Regex.IsMatch(fragment, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) return null;
        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = false, IsStrictMode = true });
        try
        {
            using var document = parser.ParseDocument("<!doctype html><html><head></head><body></body></html>");
            var nodes = parser.ParseFragment(fragment, document.CreateElement("tr"));
            var roots = nodes.Where(node => node is not IText text || !string.IsNullOrWhiteSpace(text.Data)).ToArray();
            if (roots.Length != 1 || roots[0] is not IElement { LocalName: "td" } td || td.Attributes.Length != 0
                || !td.Children.Select(child => child.LocalName).SequenceEqual(new[] { "a", "small", "a" })) return null;
            var children = td.Children;
            if (td.ChildNodes.Any(node => node is not IElement && (node is not IText text || !string.IsNullOrWhiteSpace(text.Data)))
                || children.Any(child => child.ChildNodes.Length != 1 || child.ChildNodes[0] is not IText
                    || string.IsNullOrWhiteSpace(child.TextContent) || !ValidNfc(child.TextContent))
                || children[0].Attributes.Length != 1 || children[0].GetAttribute("href") != "/GER"
                || children[1].Attributes.Length != 0 || children[2].Attributes.Length != 1 || !children[2].HasAttribute("href")) return null;
            var route = children[2].GetAttribute("href")!;
            if (route == "/GER" || !Regex.IsMatch(route, "\\A/[A-Za-z0-9][A-Za-z0-9-]{0,127}\\z", RegexOptions.CultureInvariant)
                || !PositiveInt(children[0].TextContent, out var rank) || !PositiveInt(elo, out var rating)) return null;
            return new(route, children[2].TextContent, rank, rating);
        }
        catch (HtmlParseException) { return null; }
    }

    private static bool PositiveInt(string value, out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0
        && value.All(char.IsAsciiDigit);

    private static bool ValidNfc(string value)
    {
        try { return value.IsNormalized(NormalizationForm.FormC) && !value.Contains('\0'); }
        catch (ArgumentException) { return false; } // Unpaired UTF-16 surrogate from a literal escape.
    }

    /// <summary>Only the two frozen statements and their literal data; never JavaScript execution.</summary>
    private sealed class LiteralLexer(string script)
    {
        private int _position;
        public IReadOnlyList<string[]> Read()
        {
            if (script.Length > 262144) throw Invalid();
            Token("const"); RequiredWhitespace(); Token("eloData"); Token("="); Token("[");
            var rows = new List<string[]>();
            if (!Peek(']'))
            {
                do
                {
                    if (rows.Count == 512) throw Invalid();
                    Token("[");
                    var row = new string[4];
                    for (var i = 0; i < 4; i++) { if (i != 0) Token(","); row[i] = String(); }
                    Token("]"); rows.Add(row);
                    if (!Peek(',')) break;
                    Token(",");
                } while (true);
            }
            Token("]"); Token(";"); Token("JSSortableEloTable"); Token("("); Token("eloData"); Token(")"); Token(";");
            Whitespace(); if (_position != script.Length) throw Invalid();
            return rows;
        }
        private string String()
        {
            Token("'"); var value = new StringBuilder();
            var closed = false;
            while (_position < script.Length)
            {
                var character = script[_position++];
                if (character == '\'') { closed = true; break; }
                if (character is '\r' or '\n' || character < ' ') throw Invalid();
                if (character == '\\')
                {
                    if (_position == script.Length) throw Invalid();
                    character = script[_position++] switch
                    {
                        '\\' => '\\', '\'' => '\'', 'n' => '\n', 'r' => '\r', 't' => '\t',
                        'x' => Hex(2), 'u' => Hex(4), _ => throw Invalid()
                    };
                }
                value.Append(character);
                if (value.Length > 8192) throw Invalid();
            }
            if (!closed || !ValidNfc(value.ToString())) throw Invalid();
            return value.ToString();
        }
        private char Hex(int count)
        {
            if (_position + count > script.Length) throw Invalid();
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                var character = script[_position++];
                if (!char.IsAsciiHexDigit(character)) throw Invalid();
                value = value * 16 + (character <= '9' ? character - '0' : char.ToUpperInvariant(character) - 'A' + 10);
            }
            return (char)value;
        }
        private bool Peek(char character) { Whitespace(); return _position < script.Length && script[_position] == character; }
        private void Token(string token)
        {
            Whitespace();
            if (!script.AsSpan(_position).StartsWith(token, StringComparison.Ordinal)) throw Invalid();
            _position += token.Length;
        }
        private void RequiredWhitespace()
        {
            var before = _position; Whitespace(); if (before == _position) throw Invalid();
        }
        private void Whitespace() { while (_position < script.Length && script[_position] is ' ' or '\t' or '\r' or '\n') _position++; }
        private static InvalidDataException Invalid() => new("Club Elo script is not the bounded literal grammar.");
    }
}
