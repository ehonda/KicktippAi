using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp;

public sealed record SchadensfresseLiveRulesObservation(
    SchadensfresseLiveRulesV1 Rules,
    DateTimeOffset ObservedAt,
    string ScoringTableSha256,
    string LegacyNormalizedSha256);

/// <summary>Authenticated, exact-DOM source boundary for the Schadensfresse live rule contract.</summary>
public sealed class SchadensfresseLiveRulesExtractor
{
    public static readonly Uri RulesUri = new("https://www.kicktipp.de/schadensfresse/spielregeln", UriKind.Absolute);
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.CultureInvariant);
    private static readonly Regex Number = new("^(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant);
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _parser = new();

    public SchadensfresseLiveRulesExtractor(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<SchadensfresseLiveRulesObservation> ExtractAsync(DateTimeOffset observedAt, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(RulesUri, cancellationToken);
        ValidateResponse(response);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _parser.ParseDocumentAsync(html, cancellationToken);
        var result = ExtractDocument(document);
        return new SchadensfresseLiveRulesObservation(result.Rules, observedAt, result.TableSha256, ComputeLegacyNormalizedSha256(document));
    }

    public static void ValidateResponse(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode != System.Net.HttpStatusCode.OK || response.RequestMessage?.RequestUri is not { } finalUri || !IsAllowedFinalUri(finalUri))
            throw new InvalidDataException("Authenticated rules source did not resolve to the exact final HTTPS rules URI with HTTP 200.");
    }

    public static bool IsAllowedFinalUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var path = uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal) ? uri.AbsolutePath[..^1] : uri.AbsolutePath;
        return uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "www.kicktipp.de", StringComparison.OrdinalIgnoreCase)
            && uri.Port == 443
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && string.Equals(path, "/schadensfresse/spielregeln", StringComparison.Ordinal);
    }

    public static (SchadensfresseLiveRulesV1 Rules, string TableSha256) ExtractDocument(IDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.QuerySelector("form#loginFormular") is not null || Normalize(document.Title).Contains("Login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Authenticated rules source resolved to a login page.");
        var roots = document.QuerySelectorAll("div.pagecontent");
        if (roots.Length != 1) throw Invalid("expected exactly one div.pagecontent root");
        var root = roots[0];
        var children = root.Children.ToArray();
        var headings = children.Where(item => item.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase)).ToArray();
        var labels = new[] { "Sichtbarkeit der Tipps", "Tippmodus", "Punktegleichstand", "Tippabgaberegel: 0 Minuten Vorlaufzeit", "Punkteregel: 2 - 5 Punkte", "Punkteregel: 9 Punkte" };
        if (headings.Length != labels.Length || headings.Any(item => item.Children.Length != 0) || !headings.Select(item => Normalize(item.TextContent)).SequenceEqual(labels, StringComparer.Ordinal)) throw Invalid("headings are missing, reordered, or drifted");
        var sections = headings.Select((heading, index) => children.Skip(Array.IndexOf(children, heading) + 1).TakeWhile(item => !item.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase)).ToArray()).ToArray();
        RequireParagraphSection(sections[0], "Die Tipps sind erst sichtbar, wenn die Tippzeit abgelaufen ist.");
        RequireModeSection(sections[1]);
        RequireParagraphSection(sections[2], "Soweit nicht etwas anderes vereinbart wurde, entscheidet bei Gleichstand in der Gesamtpunktzahl die Anzahl der Spieltagssiege (\"Siege\") über die Platzierung der Tipper.");
        RequireParagraphSection(sections[3], "Die Tippzeit endet 0 Minuten vor dem Termin des jeweiligen Ereignisses.");
        var matrix = RequireScoringSection(root, sections[4]);
        RequireBonusSection(sections[5]);
        // Direct-section validation above consumes the expected nodes. Count every rule-like
        // descendant as a second guard so chrome-like wrappers cannot hide a pre-heading or
        // nested unconsumed semantic claim.
        if (root.QuerySelectorAll("h2").Length != 6
            || root.QuerySelectorAll("p").Length != 7
            || root.QuerySelectorAll("ul").Length != 1
            || root.QuerySelectorAll("li").Length != 3
            || root.QuerySelectorAll("table").Length != 1)
            throw Invalid("an unconsumed rule-like node is present under pagecontent");
        return (SchadensfresseRulesCanonicalJson.Expected, SchadensfresseRulesCanonicalJson.ComputeScoringTableSha256(matrix));
    }

    public static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : Whitespace.Replace(value.Normalize(NormalizationForm.FormC).Trim(), " ");

    private static void RequireParagraphSection(IReadOnlyList<IElement> section, string text)
    {
        if (section.Count != 1 || !IsTag(section[0], "P") || section[0].Children.Length != 0 || !string.Equals(Normalize(section[0].TextContent), text, StringComparison.Ordinal)) throw Invalid("paragraph section drifted");
    }

    private static void RequireModeSection(IReadOnlyList<IElement> section)
    {
        var expected = new[] { "Es wird das genaue Ergebnis getippt.", "Es wird das jeweils folgende Ergebnis gewertet:" };
        if (section.Count != 3 || !IsTag(section[0], "P") || !IsTag(section[1], "P") || !IsTag(section[2], "UL") || !section.Take(2).Select(item => Normalize(item.TextContent)).SequenceEqual(expected, StringComparer.Ordinal)) throw Invalid("prediction mode shape drifted");
        var items = section[2].Children.ToArray();
        var labels = new[] { "DFB-Pokal 2026/27: nach Elfmeterschießen", "Champions League 2026/27: nach Elfmeterschießen", "1. Bundesliga 2026/27: 90 Minuten" };
        if (items.Length != 3 || items.Any(item => !IsTag(item, "LI") || item.Children.Length != 0) || !items.Select(item => Normalize(item.TextContent)).SequenceEqual(labels, StringComparer.Ordinal)) throw Invalid("result basis list drifted");
    }

    private static IReadOnlyList<IReadOnlyList<string>> RequireScoringSection(IElement root, IReadOnlyList<IElement> section)
    {
        if (section.Count != 1 || !IsTag(section[0], "DIV") || !string.IsNullOrWhiteSpace(section[0].GetAttribute("class"))) throw Invalid("scoring wrapper drifted");
        var tables = root.QuerySelectorAll("table.ktable");
        if (tables.Length != 1 || section[0].Children.Length != 1 || !ReferenceEquals(section[0].Children[0], tables[0])) throw Invalid("scoring table is ambiguous or misplaced");
        var table = tables[0];
        var groups = table.Children.ToArray();
        if (groups.Length != 2 || !IsTag(groups[0], "THEAD") || !IsTag(groups[1], "TBODY") || groups[0].Children.Length != 1 || groups[1].Children.Length != 2) throw Invalid("scoring table groups drifted");
        var rows = new[] { groups[0].Children[0], groups[1].Children[0], groups[1].Children[1] };
        if (rows.Any(row => !IsTag(row, "TR"))) throw Invalid("scoring table rows drifted");
        var matrix = rows.Select(row => row.Children.Select(cell =>
        {
            if (!IsTag(cell, "TH") && !IsTag(cell, "TD") || cell.Children.Length != 0) throw Invalid("scoring table cells drifted");
            return Normalize(cell.TextContent);
        }).ToArray()).ToArray();
        var expected = new[] { new[] { "", "Tendenz", "Tordifferenz", "Ergebnis" }, new[] { "Sieg", "2", "3", "5" }, new[] { "Unentschieden", "3", "-", "5" } };
        if (matrix.Any(row => row.Length != 4) || !matrix.SelectMany(row => row).SequenceEqual(expected.SelectMany(row => row), StringComparer.Ordinal)) throw Invalid("scoring matrix drifted");
        _ = ParseNumber(matrix[1][1]); _ = ParseNumber(matrix[1][2]); _ = ParseNumber(matrix[1][3]); _ = ParseNumber(matrix[2][1]); _ = ParseNullableDrawGoalDifference(matrix[2][2]); _ = ParseNumber(matrix[2][3]);
        return matrix;
    }

    private static void RequireBonusSection(IReadOnlyList<IElement> section)
    {
        var expected = new[] { "Punkte pro richtiger Antwort: 9", "Punkte gibt es für jeden richtigen Tipp. Bei dieser Regel hat die Reihenfolge keine Bedeutung." };
        if (section.Count != 1 || !IsTag(section[0], "DIV") || !string.IsNullOrWhiteSpace(section[0].GetAttribute("class"))) throw Invalid("bonus wrapper drifted");
        var paragraphs = section[0].Children.ToArray();
        if (paragraphs.Length != 2 || paragraphs.Any(item => !IsTag(item, "P") || item.Children.Length != 0) || !paragraphs.Select(item => Normalize(item.TextContent)).SequenceEqual(expected, StringComparer.Ordinal)) throw Invalid("bonus section drifted");
    }

    private static int ParseNumber(string value) => Number.IsMatch(value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : throw Invalid("numeric cell is not canonical");
    private static int? ParseNullableDrawGoalDifference(string value) => value == "-" ? null : throw Invalid("draw goal difference sentinel drifted");
    private static bool IsTag(IElement element, string tag) => element.TagName.Equals(tag, StringComparison.OrdinalIgnoreCase);
    private static InvalidDataException Invalid(string message) => new($"Schadensfresse live rules DOM contract failed: {message}.");

    public static string ComputeLegacyNormalizedSha256(IDocument document)
    {
        var keywords = new[] { "sichtbar", "tippabgabe", "tippzeit", "vorlauf", "tendenz", "tordifferenz", "exakt", "ergebnis", "bonus", "bundesliga", "dfb", "champions", "elfmeter", "90 minuten", "spieltagssieg", "punkte" };
        var values = document.QuerySelectorAll("tr, li, dt, dd, p, h1, h2, h3, h4").Select(element => string.IsNullOrWhiteSpace(element.TextContent) ? string.Empty : Whitespace.Replace(element.TextContent.Trim(), " ")).Where(value => value.Length is > 0 and <= 1000 && keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.Ordinal).ToArray();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
