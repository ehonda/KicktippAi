using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Tests;

public class SchadensfresseLiveRulesExtractorTests
{
    private static readonly string Fixture = File.ReadAllText(Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        "tests",
        "ContextProviders.Kicktipp.Tests",
        "Fixtures",
        "schadensfresse-live-rules-sanitized.html"))
        .ReplaceLineEndings("\n");

    [Test]
    public async Task Sanitized_captured_live_semantic_page_reproduces_all_three_ADR_hashes()
    {
        var document = await new HtmlParser().ParseDocumentAsync(Fixture);
        var result = SchadensfresseLiveRulesExtractor.ExtractDocument(document);

        await Assert.That(result.Rules).IsEqualTo(SchadensfresseRulesCanonicalJson.Expected);
        await Assert.That(result.TableSha256).IsEqualTo(SchadensfresseRulesCanonicalJson.ScoringTableSha256);
        await Assert.That(SchadensfresseRulesCanonicalJson.ComputeSha256(result.Rules))
            .IsEqualTo(SchadensfresseRulesCanonicalJson.CanonicalSha256);
        await Assert.That(SchadensfresseLiveRulesExtractor.ComputeLegacyNormalizedSha256(document))
            .IsEqualTo(SchadensfresseRulesCanonicalJson.LegacyNormalizedSha256);
    }

    [Test]
    public async Task Redirect_chain_that_finishes_on_exact_target_is_accepted()
    {
        var transport = new ScriptedRedirectTransport(
            [
                new Uri("https://www.kicktipp.de/community/redirect", UriKind.Absolute),
                SchadensfresseLiveRulesExtractor.RulesUri
            ],
            Fixture);
        var extractor = new SchadensfresseLiveRulesExtractor(new HttpClient(new RedirectFollowingHandler(transport)));

        var observation = await extractor.ExtractAsync(DateTimeOffset.UnixEpoch);

        await Assert.That(transport.Visited).IsEquivalentTo([
            SchadensfresseLiveRulesExtractor.RulesUri,
            new Uri("https://www.kicktipp.de/community/redirect", UriKind.Absolute),
            SchadensfresseLiveRulesExtractor.RulesUri
        ]);
        await Assert.That(observation.Rules).IsEqualTo(SchadensfresseRulesCanonicalJson.Expected);
    }

    [Test]
    [Arguments("http://www.kicktipp.de/schadensfresse/spielregeln")]
    [Arguments("ftp://www.kicktipp.de/schadensfresse/spielregeln")]
    [Arguments("https://kicktipp.de/schadensfresse/spielregeln")]
    [Arguments("https://www.kicktipp.de.evil.example/schadensfresse/spielregeln")]
    [Arguments("https://www.kicktipp.de/schadensfresse/spielregeln/extra")]
    [Arguments("https://www.kicktipp.de/Schadensfresse/spielregeln")]
    [Arguments("https://www.kicktipp.de/schadensfresse/spielregeln//")]
    [Arguments("https://www.kicktipp.de/schadensfresse/spielregeln?login=1")]
    [Arguments("https://www.kicktipp.de/schadensfresse/spielregeln#login")]
    [Arguments("https://user@www.kicktipp.de/schadensfresse/spielregeln")]
    [Arguments("https://www.kicktipp.de:444/schadensfresse/spielregeln")]
    public async Task Every_invalid_final_target_is_rejected(string uri)
    {
        var finalUri = new Uri(uri, UriKind.Absolute);
        await Assert.That(SchadensfresseLiveRulesExtractor.IsAllowedFinalUri(finalUri)).IsFalse();
        var extractor = new SchadensfresseLiveRulesExtractor(new HttpClient(new FinalResponseHandler(finalUri, Fixture)));
        await AssertExtractFailsAsync(extractor);
    }

    [Test]
    public async Task Exact_final_target_allows_one_trailing_slash_and_case_insensitive_host_only()
    {
        await Assert.That(SchadensfresseLiveRulesExtractor.IsAllowedFinalUri(
            new Uri("https://WWW.KICKTIPP.DE/schadensfresse/spielregeln/"))).IsTrue();
    }

    [Test]
    public async Task Non_200_and_missing_final_uri_fail_independently()
    {
        var non200 = new HttpResponseMessage(HttpStatusCode.Found)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, SchadensfresseLiveRulesExtractor.RulesUri)
        };
        await Assert.That(() => SchadensfresseLiveRulesExtractor.ValidateResponse(non200)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseLiveRulesExtractor.ValidateResponse(new HttpResponseMessage(HttpStatusCode.OK)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Login_form_and_login_title_each_fail_independently()
    {
        var parser = new HtmlParser();
        var formOnly = await parser.ParseDocumentAsync(Fixture.Replace(
            "<script>",
            "<form id='loginFormular'></form><script>",
            StringComparison.Ordinal));
        var titleOnly = await parser.ParseDocumentAsync(Fixture.Replace(
            "<title>Schadensfresse</title>",
            "<title>Schadensfresse Login</title>",
            StringComparison.Ordinal));

        await Assert.That(() => SchadensfresseLiveRulesExtractor.ExtractDocument(formOnly)).Throws<InvalidDataException>();
        await Assert.That(() => SchadensfresseLiveRulesExtractor.ExtractDocument(titleOnly)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Nfc_and_whitespace_normalization_are_the_only_text_tolerance()
    {
        await Assert.That(SchadensfresseLiveRulesExtractor.Normalize("  Elfmeterschie\u00DFen\t\n"))
            .IsEqualTo("Elfmeterschießen");
        await Assert.That(SchadensfresseLiveRulesExtractor.Normalize("Cafe\u0301")).IsEqualTo("Café");
        var normalizedFixture = Fixture.Replace(
            "Sichtbarkeit der Tipps",
            " Sichtbarkeit\n der Tipps ",
            StringComparison.Ordinal);
        await Assert.That(() => SchadensfresseLiveRulesExtractor.ExtractDocument(
            new HtmlParser().ParseDocument(normalizedFixture))).ThrowsNothing();
    }

    [Test]
    public async Task Systematic_structural_mutations_are_all_rejected()
    {
        var mutations = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
        {
            ["missing root"] = html => html.Replace("class=\"pagecontent\"", "class=\"page-content\"", StringComparison.Ordinal),
            ["duplicate root"] = html => html.Replace("</body>", "<div class='pagecontent'></div></body>", StringComparison.Ordinal),
            ["missing heading"] = html => html.Replace("<h2>Sichtbarkeit der Tipps</h2>", "", StringComparison.Ordinal),
            ["duplicate heading"] = html => html.Replace("<h2>Tippmodus</h2>", "<h2>Tippmodus</h2><h2>Tippmodus</h2>", StringComparison.Ordinal),
            ["reordered headings"] = html => Swap(html, "<h2>Sichtbarkeit der Tipps</h2>", "<h2>Tippmodus</h2>"),
            ["nested heading"] = html => html.Replace("<h2>Tippmodus</h2>", "<section><h2>Tippmodus</h2></section>", StringComparison.Ordinal),
            ["wrong heading tag"] = html => html.Replace("<h2>Tippmodus</h2>", "<h3>Tippmodus</h3>", StringComparison.Ordinal),
            ["missing paragraph"] = html => html.Replace("<p>Es wird das genaue Ergebnis getippt.</p>", "", StringComparison.Ordinal),
            ["duplicate paragraph"] = html => html.Replace("<p>Es wird das genaue Ergebnis getippt.</p>", "<p>Es wird das genaue Ergebnis getippt.</p><p>Es wird das genaue Ergebnis getippt.</p>", StringComparison.Ordinal),
            ["reordered paragraphs"] = html => Swap(html, "<p>Es wird das genaue Ergebnis getippt.</p>", "<p>Es wird das jeweils folgende Ergebnis gewertet:</p>"),
            ["nested paragraph"] = html => html.Replace("<p>Es wird das genaue Ergebnis getippt.</p>", "<div><p>Es wird das genaue Ergebnis getippt.</p></div>", StringComparison.Ordinal),
            ["wrong paragraph tag"] = html => html.Replace("<p>Es wird das genaue Ergebnis getippt.</p>", "<span>Es wird das genaue Ergebnis getippt.</span>", StringComparison.Ordinal),
            ["missing list"] = html => RemoveBetween(html, "<ul>", "</ul>"),
            ["duplicate list"] = html => html.Replace("</ul>", "</ul><ul></ul>", StringComparison.Ordinal),
            ["nested list"] = html => html.Replace("<ul>", "<div><ul>", StringComparison.Ordinal).Replace("</ul>", "</ul></div>", StringComparison.Ordinal),
            ["wrong list tag"] = html => html.Replace("<ul>", "<ol>", StringComparison.Ordinal).Replace("</ul>", "</ol>", StringComparison.Ordinal),
            ["missing list item"] = html => html.Replace("<li>DFB-Pokal 2026/27: nach Elfmeterschießen</li>", "", StringComparison.Ordinal),
            ["duplicate list item"] = html => html.Replace("</ul>", "<li>1. Bundesliga 2026/27: 90 Minuten</li></ul>", StringComparison.Ordinal),
            ["reordered list items"] = html => Swap(html, "<li>DFB-Pokal 2026/27: nach Elfmeterschießen</li>", "<li>Champions League 2026/27: nach Elfmeterschießen</li>"),
            ["nested list item"] = html => html.Replace("<li>DFB-Pokal 2026/27: nach Elfmeterschießen</li>", "<li><span>DFB-Pokal 2026/27: nach Elfmeterschießen</span></li>", StringComparison.Ordinal),
            ["wrong list item tag"] = html => html.Replace("<li>DFB-Pokal 2026/27: nach Elfmeterschießen</li>", "<p>DFB-Pokal 2026/27: nach Elfmeterschießen</p>", StringComparison.Ordinal),
            ["classified scoring wrapper"] = html => html.Replace("<div>\n<table class=\"ktable\">", "<div class='rules'>\n<table class=\"ktable\">", StringComparison.Ordinal),
            ["nested scoring wrapper"] = html => html.Replace("<table class=\"ktable\">", "<section><table class=\"ktable\">", StringComparison.Ordinal).Replace("</table>\n</div>", "</table></section>\n</div>", StringComparison.Ordinal),
            ["missing table"] = html => RemoveBetween(html, "<table class=\"ktable\">", "</table>"),
            ["duplicate table"] = html => html.Replace("</table>", "</table><table class='ktable'></table>", StringComparison.Ordinal),
            ["wrong table tag"] = html => html.Replace("<table class=\"ktable\">", "<div class=\"ktable\">", StringComparison.Ordinal).Replace("</table>", "</div>", StringComparison.Ordinal),
            ["missing thead"] = html => html.Replace("<thead><tr><th></th><th>Tendenz</th><th>Tordifferenz</th><th>Ergebnis</th></tr></thead>", "", StringComparison.Ordinal),
            ["duplicate thead"] = html => html.Replace("</thead>", "</thead><thead></thead>", StringComparison.Ordinal),
            ["reordered table groups"] = html => SwapBlocks(html, "<thead>", "</thead>", "<tbody>", "</tbody>"),
            ["wrong table group tag"] = html => html.Replace("<thead>", "<tbody>", StringComparison.Ordinal).Replace("</thead>", "</tbody>", StringComparison.Ordinal),
            ["missing row"] = html => html.Replace("<tr><td>Sieg</td><td>2</td><td>3</td><td>5</td></tr>", "", StringComparison.Ordinal),
            ["duplicate row"] = html => html.Replace("</tbody>", "<tr><td>Sieg</td><td>2</td><td>3</td><td>5</td></tr></tbody>", StringComparison.Ordinal),
            ["nested row"] = html => html.Replace("<tbody>", "<tbody><div>", StringComparison.Ordinal).Replace("</tbody>", "</div></tbody>", StringComparison.Ordinal),
            ["wrong row tag"] = html => html.Replace("<tr><td>Sieg</td>", "<div><td>Sieg</td>", StringComparison.Ordinal),
            ["missing cell"] = html => html.Replace("<td>Sieg</td>", "", StringComparison.Ordinal),
            ["duplicate cell"] = html => html.Replace("<td>Sieg</td>", "<td>Sieg</td><td>Sieg</td>", StringComparison.Ordinal),
            ["nested cell"] = html => html.Replace("<td>Sieg</td>", "<td><span>Sieg</span></td>", StringComparison.Ordinal),
            ["wrong cell tag"] = html => html.Replace("<td>Sieg</td>", "<div>Sieg</div>", StringComparison.Ordinal),
            ["classified bonus wrapper"] = html => html.Replace("<div>\n<p>Punkte pro richtiger Antwort", "<div class='bonus'>\n<p>Punkte pro richtiger Antwort", StringComparison.Ordinal),
            ["reordered bonus paragraphs"] = html => Swap(html, "<p>Punkte pro richtiger Antwort: 9</p>", "<p>Punkte gibt es für jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung.</p>"),
            ["unconsumed before first heading"] = html => html.Replace("<h2>Sichtbarkeit", "<p>unconsumed</p><h2>Sichtbarkeit", StringComparison.Ordinal),
            ["unconsumed after last section"] = html => html.Replace("</div>\n</div>\n<script>", "</div><p>unconsumed</p>\n</div>\n<script>", StringComparison.Ordinal),
            ["unconsumed nested rule node"] = html => html.Replace("<p>Die Tipps sind erst sichtbar", "<section><p>unconsumed</p></section><p>Die Tipps sind erst sichtbar", StringComparison.Ordinal)
        };

        await AssertMutationsRejected(mutations);
    }

    [Test]
    public async Task Exhaustive_label_case_punctuation_season_and_semantic_text_mutations_are_rejected()
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sichtbarkeit der Tipps"] = "sichtbarkeit der Tipps",
            ["Tippmodus"] = "Tippmodus!",
            ["Punktegleichstand"] = "Punkte-Gleichstand",
            ["Tippabgaberegel: 0 Minuten Vorlaufzeit"] = "Tippabgaberegel: 1 Minuten Vorlaufzeit",
            ["Punkteregel: 2 - 5 Punkte"] = "Punkteregel: 2–5 Punkte",
            ["Punkteregel: 9 Punkte"] = "Punkteregel: neun Punkte",
            ["Die Tipps sind erst sichtbar, wenn die Tippzeit abgelaufen ist."] = "Die Tipps sind sichtbar, bevor die Tippzeit abgelaufen ist.",
            ["Es wird das genaue Ergebnis getippt."] = "Es wird die Tendenz getippt.",
            ["Es wird das jeweils folgende Ergebnis gewertet:"] = "Es wird das folgende Ergebnis gewertet:",
            ["DFB-Pokal 2026/27: nach Elfmeterschießen"] = "DFB-Pokal 2025/26: nach Elfmeterschießen",
            ["Champions League 2026/27: nach Elfmeterschießen"] = "UEFA Champions League 2026/27: nach Elfmeterschießen",
            ["1. Bundesliga 2026/27: 90 Minuten"] = "1. Bundesliga 2026/27: 120 Minuten",
            ["Soweit nicht etwas anderes vereinbart wurde, entscheidet bei Gleichstand in der Gesamtpunktzahl die Anzahl der Spieltagssiege (\"Siege\") über die Platzierung der Tipper."] = "Bei Gleichstand entscheidet die Tordifferenz.",
            ["Die Tippzeit endet 0 Minuten vor dem Termin des jeweiligen Ereignisses."] = "Die Tippzeit endet 1 Minute vor dem Termin des jeweiligen Ereignisses.",
            ["Tendenz"] = "tendenz",
            ["Tordifferenz"] = "Tor-Differenz",
            ["Ergebnis"] = "Exaktes Ergebnis",
            ["Sieg"] = "Gewinn",
            ["Unentschieden"] = "Remis",
            ["Punkte pro richtiger Antwort: 9"] = "Punkte pro richtiger Antwort: 8",
            ["Punkte gibt es für jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung."] = "Punkte gibt es für jeden richtigen Tipp. Die Reihenfolge ist wichtig."
        };

        await AssertMutationsRejected(replacements.ToDictionary(
            pair => $"replace {pair.Key}",
            pair => new Func<string, string>(html => html.Replace(pair.Key, pair.Value, StringComparison.Ordinal)),
            StringComparer.Ordinal));
    }

    [Test]
    public async Task Numeric_format_and_sentinel_mutations_are_rejected()
    {
        var mutations = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
        {
            ["plus sign"] = html => ReplaceFirst(html, "<td>2</td>", "<td>+2</td>"),
            ["leading zero"] = html => ReplaceFirst(html, "<td>2</td>", "<td>02</td>"),
            ["separator"] = html => ReplaceFirst(html, "<td>2</td>", "<td>2,000</td>"),
            ["decimal"] = html => ReplaceFirst(html, "<td>2</td>", "<td>2.0</td>"),
            ["surrounding text"] = html => ReplaceFirst(html, "<td>2</td>", "<td>2 points</td>"),
            ["negative number"] = html => ReplaceFirst(html, "<td>2</td>", "<td>-2</td>"),
            ["empty draw sentinel"] = html => html.Replace("<td>-</td>", "<td></td>", StringComparison.Ordinal),
            ["unicode dash sentinel"] = html => html.Replace("<td>-</td>", "<td>–</td>", StringComparison.Ordinal),
            ["zero draw sentinel"] = html => html.Replace("<td>-</td>", "<td>0</td>", StringComparison.Ordinal),
            ["sentinel in numeric cell"] = html => ReplaceFirst(html, "<td>2</td>", "<td>-</td>")
        };

        await AssertMutationsRejected(mutations);
    }

    [Test]
    public async Task Numeric_drift_leaves_legacy_digest_unchanged_but_changes_table_and_structured_hashes()
    {
        var parser = new HtmlParser();
        var original = await parser.ParseDocumentAsync(Fixture);
        var driftedRules = SchadensfresseRulesCanonicalJson.Expected with
        {
            MatchScoring = SchadensfresseRulesCanonicalJson.Expected.MatchScoring with
            {
                Win = SchadensfresseRulesCanonicalJson.Expected.MatchScoring.Win with { ExactResultPoints = 4 }
            }
        };
        var driftedHtml = ReplaceFirst(Fixture, "<td>5</td>", "<td>4</td>");
        var drifted = await parser.ParseDocumentAsync(driftedHtml);
        var driftedMatrix = drifted.QuerySelectorAll("table.ktable tr")
            .Select(row => (IReadOnlyList<string>)row.Children.Select(cell => SchadensfresseLiveRulesExtractor.Normalize(cell.TextContent)).ToArray())
            .ToArray();
        var driftedTableHash = SchadensfresseRulesCanonicalJson.ComputeScoringTableSha256(driftedMatrix);
        var driftedStructuredBytes = JsonSerializer.SerializeToUtf8Bytes(driftedRules, SchadensfresseRulesCanonicalJson.Options);
        var driftedStructuredHash = Convert.ToHexStringLower(SHA256.HashData(driftedStructuredBytes));

        await Assert.That(SchadensfresseLiveRulesExtractor.ComputeLegacyNormalizedSha256(drifted))
            .IsEqualTo(SchadensfresseLiveRulesExtractor.ComputeLegacyNormalizedSha256(original));
        await Assert.That(driftedTableHash).IsNotEqualTo(SchadensfresseRulesCanonicalJson.ScoringTableSha256);
        await Assert.That(driftedStructuredHash).IsNotEqualTo(SchadensfresseRulesCanonicalJson.CanonicalSha256);
        await Assert.That(() => SchadensfresseLiveRulesExtractor.ExtractDocument(drifted)).Throws<InvalidDataException>();
    }

    private static async Task AssertMutationsRejected(IReadOnlyDictionary<string, Func<string, string>> mutations)
    {
        var parser = new HtmlParser();
        foreach (var (name, mutation) in mutations)
        {
            var mutated = mutation(Fixture);
            if (string.Equals(mutated, Fixture, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Mutation '{name}' did not alter the fixture.");
            }

            var document = await parser.ParseDocumentAsync(mutated);
            try
            {
                SchadensfresseLiveRulesExtractor.ExtractDocument(document);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            throw new InvalidOperationException($"Mutation '{name}' was unexpectedly accepted.");
        }
    }

    private static async Task AssertExtractFailsAsync(SchadensfresseLiveRulesExtractor extractor)
    {
        try
        {
            await extractor.ExtractAsync(DateTimeOffset.UnixEpoch);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException("Invalid final target was unexpectedly accepted.");
    }

    private static string ReplaceFirst(string value, string oldValue, string newValue)
    {
        var index = value.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException($"Fixture does not contain '{oldValue}'.");
        return string.Concat(value.AsSpan(0, index), newValue, value.AsSpan(index + oldValue.Length));
    }

    private static string Swap(string value, string first, string second)
    {
        const string marker = "__SCHADENSFRESSE_SWAP__";
        return value.Replace(first, marker, StringComparison.Ordinal)
            .Replace(second, first, StringComparison.Ordinal)
            .Replace(marker, second, StringComparison.Ordinal);
    }

    private static string RemoveBetween(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        return value.Remove(startIndex, endIndex + end.Length - startIndex);
    }

    private static string SwapBlocks(string value, string firstStart, string firstEnd, string secondStart, string secondEnd)
    {
        var firstStartIndex = value.IndexOf(firstStart, StringComparison.Ordinal);
        var firstEndIndex = value.IndexOf(firstEnd, firstStartIndex, StringComparison.Ordinal) + firstEnd.Length;
        var secondStartIndex = value.IndexOf(secondStart, firstEndIndex, StringComparison.Ordinal);
        var secondEndIndex = value.IndexOf(secondEnd, secondStartIndex, StringComparison.Ordinal) + secondEnd.Length;
        var first = value[firstStartIndex..firstEndIndex];
        var between = value[firstEndIndex..secondStartIndex];
        var second = value[secondStartIndex..secondEndIndex];
        return value[..firstStartIndex] + second + between + first + value[secondEndIndex..];
    }

    private sealed class FinalResponseHandler(Uri finalUri, string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri),
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
    }

    private sealed class RedirectFollowingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var current = request;
            for (var redirects = 0; redirects <= 5; redirects++)
            {
                var response = await base.SendAsync(current, cancellationToken);
                if (response.StatusCode is not (HttpStatusCode.MovedPermanently
                    or HttpStatusCode.Redirect
                    or HttpStatusCode.TemporaryRedirect
                    or HttpStatusCode.PermanentRedirect))
                {
                    return response;
                }

                var location = response.Headers.Location
                    ?? throw new InvalidOperationException("Redirect response did not contain Location.");
                var nextUri = location.IsAbsoluteUri ? location : new Uri(current.RequestUri!, location);
                response.Dispose();
                current = new HttpRequestMessage(HttpMethod.Get, nextUri);
            }

            throw new InvalidOperationException("Redirect chain exceeded the test limit.");
        }
    }

    private sealed class ScriptedRedirectTransport(IReadOnlyList<Uri> redirects, string html) : HttpMessageHandler
    {
        public List<Uri> Visited { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Visited.Add(request.RequestUri!);
            var hop = Visited.Count - 1;
            if (hop < redirects.Count)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    RequestMessage = request
                };
                redirect.Headers.Location = redirects[hop];
                return Task.FromResult(redirect);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
        }
    }
}
