using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

/// <summary>The dormant official-HTML evaluation contract (ADR-0077/0081).</summary>
public static class BundesligaClubEloRefresh
{
    public const string SourceUrl = "https://clubelo.com/GER";
    public const string MappingPath = "data/bundesliga-2026-27/club-elo-name-map.csv";
    public const int MaximumBytes = 2097152;

    public sealed record Response(int StatusCode, string FinalUrl, int RedirectCount, string? RedirectLocation,
        string? MediaType, string? Charset, IReadOnlyList<string> ContentEncodings, long? DeclaredContentLength)
    {
        public bool IsAccepting => StatusCode == 200 && FinalUrl == SourceUrl && RedirectCount == 0
            && RedirectLocation is null && MediaType == "text/html" && Charset == "utf-8" && ContentEncodings.Count == 0;
    }

    public sealed record Row(string ProviderRoute, string ProviderDisplayName, int GlobalRank, int Elo);

    // The exact accepted bytes are duplicated intentionally: the tracked artifact is checked against
    // these bytes and the C3 descriptor hash, never treated as permission to infer aliases.
    public static byte[] CanonicalMappingBytes() => new UTF8Encoding(false, true).GetBytes(string.Join("\r\n",
    [
        "teamSlug,providerRoute,providerDisplayName",
        "b04,/Leverkusen,Leverkusen", "bmg,/Gladbach,Gladbach", "bvb,/Dortmund,Dortmund",
        "fca,/Augsburg,Augsburg", "fcb,/Bayern,Bayern München", "fck,/Koeln,Köln",
        "fcu,/UnionBerlin,Union Berlin", "hsv,/Hamburg,Hamburg", "m05,/Mainz,Mainz",
        "rbl,/RBLeipzig,RB Leipzig", "s04,/Schalke,Schalke", "scf,/Freiburg,Freiburg",
        "scp,/Paderborn,Paderborn", "sge,/Frankfurt,Frankfurt", "sve,/Elversberg,Elversberg",
        "svw,/Werder,Werder", "tsg,/Hoffenheim,Hoffenheim", "vfb,/Stuttgart,Stuttgart"
    ]));

    /// <summary>Runs the date, mapping, coverage, age and shared-retained gates after inert parsing.</summary>
    public static BundesligaContextSourceObservationResult Evaluate(
        BundesligaContextSourceCycleIdentity cycle, DateTimeOffset observedAtUtc, Response response,
        byte[] rawBytes, string? displayedDate, IReadOnlyList<Row> parsedRows, byte[] mappingBytes,
        IReadOnlyDictionary<string, BundesligaClubEloSnapshot> retainedSelections)
    {
        ValidateRetainedSelections(cycle, retainedSelections);
        if (displayedDate is null || displayedDate.Length != 10
            || !DateOnly.TryParseExact(displayedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != displayedDate
            || date > DateOnly.FromDateTime(observedAtUtc.UtcDateTime))
            return CreateObservation(cycle, observedAtUtc, "DateRejected", response, rawBytes);

        var canonical = CanonicalMappingBytes();
        var mapping = Encoding.UTF8.GetString(canonical).Split("\r\n").Skip(1).Select(line => line.Split(',')).ToArray();
        if (!mappingBytes.AsSpan().SequenceEqual(canonical)
            || BundesligaContextSourceHashing.Sha256(mappingBytes) != BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256
            || !mapping.Select(row => row[0]).SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(team => team.TeamSlug), StringComparer.Ordinal))
            return CreateObservation(cycle, observedAtUtc, "MappingRejected", response, rawBytes, displayedDate);

        var projected = new List<(string Slug, Row Row)>();
        foreach (var row in parsedRows)
        {
            var routeMatch = mapping.SingleOrDefault(item => item[1] == row.ProviderRoute);
            var nameMatch = mapping.SingleOrDefault(item => item[2] == row.ProviderDisplayName);
            if (routeMatch is null && nameMatch is null) continue; // Extra German clubs are allowed.
            if (routeMatch is null || nameMatch is null || routeMatch[0] != nameMatch[0]
                || projected.Any(item => item.Slug == routeMatch[0]))
                return CreateObservation(cycle, observedAtUtc, "MappingRejected", response, rawBytes, displayedDate);
            projected.Add((routeMatch[0], row));
        }
        var rows = projected.OrderBy(item => item.Slug, StringComparer.Ordinal).ToArray();
        var evaluation = rows.Length != BundesligaTeamManifest.ExpectedTeamCount ? "CoverageRejected"
            : DateOnly.FromDateTime(observedAtUtc.UtcDateTime).DayNumber - date.DayNumber > 7 ? "StaleRejected"
            : retainedSelections.Values.All(retained => date <= retained.RatedAt) ? "NotNewer"
            : "Eligible";
        return CreateObservation(cycle, observedAtUtc, evaluation, response, rawBytes, displayedDate, rows);
    }

    public static void ValidateRetainedSelections(BundesligaContextSourceCycleIdentity cycle,
        IReadOnlyDictionary<string, BundesligaClubEloSnapshot> retainedSelections)
    {
        _ = BundesligaContextSourceCycleIdentity.Create(cycle.Competition, cycle.Scope, cycle.CycleId, cycle.Sequence);
        var consumers = cycle.Scope == BundesligaContextSourceScope.ProductionLive
            ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        if (!retainedSelections.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(consumers)
            || retainedSelections.Values.Any(snapshot => snapshot.Origin is not (BundesligaClubEloSnapshotOrigin.LaunchSeed or BundesligaClubEloSnapshotOrigin.LastKnownGood)))
            throw new InvalidDataException("Shared Club Elo evaluation requires every independently verified retained consumer selection.");
    }

    public static BundesligaContextSourceObservationResult CreateObservation(
        BundesligaContextSourceCycleIdentity cycle, DateTimeOffset observedAtUtc, string evaluation,
        Response? response = null, byte[]? rawBytes = null, string? displayedDate = null,
        IReadOnlyList<(string Slug, Row Row)>? rows = null, string? terminalCause = null)
    {
        var descriptor = JsonSerializer.Serialize(new
        {
            contract = "club-elo-official-html-descriptor/v1",
            sourceUrl = SourceUrl,
            response = response is null ? null : new
            {
                statusCode = response.StatusCode, finalUrl = response.FinalUrl, redirectCount = response.RedirectCount,
                redirectLocation = response.RedirectLocation, mediaType = response.MediaType, charset = response.Charset,
                contentEncodings = response.ContentEncodings, declaredContentLength = response.DeclaredContentLength
            },
            rawSha256 = rawBytes is null ? null : BundesligaContextSourceHashing.Sha256(rawBytes),
            rawByteLength = rawBytes?.LongLength,
            parserContract = "club-elo-official-html-parser/v1",
            displayedDate,
            providerDateEvidence = displayedDate is null ? null : new
            {
                kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1",
                field = "h1>a[href]", rawValue = displayedDate, ratedAt = displayedDate
            },
            tableContract = "club-elo-official-html-table/v1",
            tableHeader = new[] { "Club", "Elo", "+/-", "Golo" },
            nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1",
            nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256,
            sourceRows = rows?.Select(item => new
            {
                teamSlug = item.Slug, providerRoute = item.Row.ProviderRoute, providerDisplayName = item.Row.ProviderDisplayName,
                globalRank = item.Row.GlobalRank, elo = item.Row.Elo
            }).ToArray(),
            evaluation
        });
        var diagnostic = evaluation switch
        {
            "Eligible" => null,
            "TransportRejected" => "CLUB_ELO_TRANSPORT_REJECTED", "SizeRejected" => "CLUB_ELO_SIZE_REJECTED",
            "ResponseRejected" => "CLUB_ELO_RESPONSE_REJECTED", "DomRejected" => "CLUB_ELO_DOM_REJECTED",
            "LexerRejected" => "CLUB_ELO_LEXER_REJECTED", "FragmentRejected" => "CLUB_ELO_FRAGMENT_REJECTED",
            "DateRejected" => "CLUB_ELO_DISPLAYED_DATE_REJECTED", "MappingRejected" => "CLUB_ELO_MAPPING_REJECTED",
            "CoverageRejected" => "CLUB_ELO_COVERAGE_REJECTED", "StaleRejected" => "CLUB_ELO_STALE_GT_7_DAYS",
            "NotNewer" => "CLUB_ELO_NOT_NEWER", _ => throw new InvalidDataException("Unknown HTML evaluation.")
        };
        var eligible = evaluation == "Eligible";
        var payload = eligible ? new BundesligaContextSourcePayload("club-elo/source.html", rawBytes!.LongLength,
            BundesligaContextSourceHashing.Sha256(rawBytes)) : null;
        var result = new BundesligaContextSourceObservationResult(new BundesligaContextSourceObservation(
            BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo),
            observedAtUtc, eligible ? BundesligaContextSourceDisposition.ArtifactCaptured : BundesligaContextSourceDisposition.Rejected,
            descriptor, payload, new[] { diagnostic, terminalCause }.OfType<string>().Order(StringComparer.Ordinal).ToArray()),
            eligible ? rawBytes!.ToArray() : null);
        result.Validate();
        return result;
    }

    /// <summary>Consumes a validated immutable observation against the actual current retained selection.</summary>
    public static BundesligaClubEloSelection Select(BundesligaContextSourceObservation observation, BundesligaClubEloSnapshot retained)
    {
        observation.Validate();
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        var descriptor = document.RootElement;
        if (observation.Source != BundesligaContextSource.ClubElo
            || descriptor.GetProperty("contract").GetString() != "club-elo-official-html-descriptor/v1"
            || retained.Origin is not (BundesligaClubEloSnapshotOrigin.LaunchSeed or BundesligaClubEloSnapshotOrigin.LastKnownGood))
            throw new InvalidDataException("Club Elo consumption requires HTML and a verified retained seed/LKG.");
        var evaluation = descriptor.GetProperty("evaluation").GetString();
        var candidateDate = BundesligaContextSourceDescriptorContract.ClubEloRatedAt(observation.DescriptorJson);
        if (evaluation == "NotNewer" && candidateDate > retained.RatedAt)
            throw new InvalidDataException("Shared NotNewer contradicts the actual retained selection.");
        if (evaluation == "Eligible" && candidateDate > retained.RatedAt)
        {
            var teams = BundesligaTeamManifest.Default.Entries.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
            var entries = descriptor.GetProperty("sourceRows").EnumerateArray().Select(row => new BundesligaClubEloEntry(
                teams[row.GetProperty("teamSlug").GetString()!], row.GetProperty("globalRank").GetInt32(), row.GetProperty("elo").GetInt32())).ToArray();
            var candidate = BundesligaClubEloSnapshot.Create(entries, candidateDate!.Value, observation.ObservedAtUtc,
                new Uri(SourceUrl), BundesligaClubEloSnapshotOrigin.NetworkCandidate);
            return new(candidate, BundesligaClubEloSelectionDisposition.NetworkAccepted, []);
        }
        // Retained publications keep the historical diagnostic vocabulary. The observation retains
        // the exact HTML rejection and never acquires a synthetic payload or HTML-v2 provenance.
        return evaluation switch
        {
            "Eligible" or "NotNewer" => new(retained, BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer,
                [$"NETWORK_RATED_AT_NOT_NEWER:{candidateDate:yyyy-MM-dd}"]),
            "StaleRejected" => new(retained, BundesligaClubEloSelectionDisposition.NetworkCandidateStale,
                [$"NETWORK_CANDIDATE_STALE:AGE_DAYS={DateOnly.FromDateTime(observation.ObservedAtUtc.UtcDateTime).DayNumber - candidateDate!.Value.DayNumber}:MAX_DAYS=7"]),
            _ => new(retained, BundesligaClubEloSelectionDisposition.NetworkCandidateRejected, observation.Diagnostics)
        };
    }
}
