using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public enum OpenLigaDbHistorySnapshotKind
{
    SecondBundesliga,
    Relegation,
    DfbPokal,
    DfbPokal2026LiveCompletion
}

public sealed record OpenLigaDbHistorySnapshotValidation(
    OpenLigaDbHistorySnapshotKind Kind,
    string Sha256,
    int MatchCount,
    int CompletedMatchCount,
    IReadOnlySet<long> MatchIds);

public static class OpenLigaDbHistorySnapshotValidator
{
    public static OpenLigaDbHistorySnapshotValidation Validate(
        ReadOnlySpan<byte> content,
        OpenLigaDbHistorySnapshotKind kind,
        string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        var contract = Contract.For(kind);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(hash, contract.Revision, StringComparison.Ordinal))
        {
            throw Invalid(sourceName, $"SHA-256 '{hash}' does not match frozen revision '{contract.Revision}'");
        }

        using var document = JsonDocument.Parse(content.ToArray());
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(sourceName, "root must be a JSON array");
        }

        var matches = document.RootElement.EnumerateArray().ToArray();
        if (matches.Length != contract.MatchCount)
        {
            throw Invalid(sourceName, $"expected exactly {contract.MatchCount} matches but found {matches.Length}");
        }

        var ids = new HashSet<long>();
        var completedIds = new HashSet<long>();
        foreach (var match in matches)
        {
            var matchId = RequiredInt64(match, "matchID", sourceName);
            if (matchId <= 0 || !ids.Add(matchId))
            {
                throw Invalid(sourceName, $"matchID '{matchId}' must be positive and unique");
            }

            var isCompleted = RequiredBoolean(match, "matchIsFinished", sourceName);
            if (isCompleted)
            {
                completedIds.Add(matchId);
            }
            if (contract.RequireAllCompleted && !isCompleted)
            {
                throw Invalid(sourceName, $"matchID '{matchId}' is not completed");
            }
            if (RequiredInt32(match, "leagueSeason", sourceName) != contract.LeagueSeason
                || !string.Equals(RequiredString(match, "leagueShortcut", sourceName), contract.LeagueShortcut, StringComparison.Ordinal))
            {
                throw Invalid(sourceName, $"matchID '{matchId}' is outside {contract.LeagueShortcut}/{contract.LeagueSeason}");
            }

            ValidateExactDateTime(match, matchId, sourceName);
            _ = RequiredTeamName(match, "team1", matchId, sourceName);
            _ = RequiredTeamName(match, "team2", matchId, sourceName);

            var results = RequiredProperty(match, "matchResults", sourceName);
            if (results.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(sourceName, $"matchID '{matchId}' matchResults must be an array");
            }
            var fullTimeResults = results.EnumerateArray()
                .Where(result => RequiredInt32(result, "resultTypeID", sourceName) == 2)
                .ToArray();
            if (isCompleted && fullTimeResults.Length != 1)
            {
                throw Invalid(sourceName, $"matchID '{matchId}' must have exactly one full-time resultTypeID 2");
            }
            if (isCompleted)
            {
                _ = RequiredInt32(fullTimeResults[0], "pointsTeam1", sourceName);
                _ = RequiredInt32(fullTimeResults[0], "pointsTeam2", sourceName);
            }
        }

        if (contract.ExactCompletedMatchIds is not null && !completedIds.SetEquals(contract.ExactCompletedMatchIds))
        {
            throw Invalid(sourceName,
                $"completed match IDs must be exactly [{string.Join(',', contract.ExactCompletedMatchIds.Order())}]");
        }
        contract.ValidateIdentities(matches, sourceName);
        return new(kind, hash, matches.Length, completedIds.Count, ids);
    }

    private static void ValidateExactDateTime(JsonElement match, long matchId, string sourceName)
    {
        var localText = RequiredString(match, "matchDateTime", sourceName);
        var utcText = RequiredString(match, "matchDateTimeUTC", sourceName);
        if (!DateTime.TryParseExact(localText, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            throw Invalid(sourceName, $"matchID '{matchId}' has no exact local datetime");
        }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (!DateTimeOffset.TryParseExact(utcText, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            throw Invalid(sourceName, $"matchID '{matchId}' has no exact UTC datetime");
        }

        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var expectedLocal = TimeZoneInfo.ConvertTime(utc, berlin).DateTime;
        if (expectedLocal != local)
        {
            throw Invalid(sourceName, $"matchID '{matchId}' local and UTC datetimes disagree");
        }
    }

    private static string RequiredTeamName(JsonElement match, string propertyName, long matchId, string sourceName)
    {
        var team = RequiredProperty(match, propertyName, sourceName);
        if (team.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(sourceName, $"matchID '{matchId}' {propertyName} must be an object");
        }
        return RequiredString(team, "teamName", sourceName);
    }

    private static JsonElement RequiredProperty(JsonElement element, string propertyName, string sourceName) =>
        element.TryGetProperty(propertyName, out var value)
            ? value
            : throw Invalid(sourceName, $"requires property '{propertyName}'");

    private static string RequiredString(JsonElement element, string propertyName, string sourceName)
    {
        var property = RequiredProperty(element, propertyName, sourceName);
        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(sourceName, $"requires non-blank string '{propertyName}'")
            : value;
    }

    private static int RequiredInt32(JsonElement element, string propertyName, string sourceName)
    {
        var property = RequiredProperty(element, propertyName, sourceName);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : throw Invalid(sourceName, $"requires integer '{propertyName}'");
    }

    private static long RequiredInt64(JsonElement element, string propertyName, string sourceName)
    {
        var property = RequiredProperty(element, propertyName, sourceName);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
            ? value
            : throw Invalid(sourceName, $"requires integer '{propertyName}'");
    }

    private static bool RequiredBoolean(JsonElement element, string propertyName, string sourceName)
    {
        var property = RequiredProperty(element, propertyName, sourceName);
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Invalid(sourceName, $"requires boolean '{propertyName}'")
        };
    }

    private static InvalidDataException Invalid(string sourceName, string message) =>
        new($"Invalid frozen OpenLigaDB history snapshot '{sourceName}': {message}.");

    private sealed record Contract(
        int MatchCount,
        int LeagueSeason,
        string LeagueShortcut,
        string Revision,
        bool RequireAllCompleted,
        IReadOnlySet<long>? ExactCompletedMatchIds,
        Action<JsonElement[], string> ValidateIdentities)
    {
        public static Contract For(OpenLigaDbHistorySnapshotKind kind) => kind switch
        {
            OpenLigaDbHistorySnapshotKind.SecondBundesliga => new(
                306, 2025, "bl2", BundesligaHistoryPlayedDateMap.OpenLigaDbLeagueRevision, true, null, (_, _) => { }),
            OpenLigaDbHistorySnapshotKind.Relegation => new(
                2, 2025, "rel", BundesligaHistoryPlayedDateMap.OpenLigaDbRelegationRevision, true, null, ValidateRelegation),
            OpenLigaDbHistorySnapshotKind.DfbPokal => new(
                63, 2025, "dfb", BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalRevision, true, null, ValidateDfbPokalFinal),
            OpenLigaDbHistorySnapshotKind.DfbPokal2026LiveCompletion => new(
                32, 2026, "dfb", BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokal2026Revision, true,
                new HashSet<long>
                {
                    81832, 81833, 81834, 81835, 81836, 81837, 81838, 81839, 81840, 81841,
                    81842, 81843, 81844, 81845, 81846, 81847, 81848, 81849, 81850, 81851,
                    81852, 81853, 81854, 81855, 81856, 81857, 81858, 81859, 81860, 81861,
                    81862, 81863
                }, ValidateDfbPokal2026LiveCompletion),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        private static void ValidateRelegation(JsonElement[] matches, string sourceName)
        {
            var identities = matches.Select(match => (
                Id: RequiredInt64(match, "matchID", sourceName),
                Home: RequiredTeamName(match, "team1", RequiredInt64(match, "matchID", sourceName), sourceName),
                Away: RequiredTeamName(match, "team2", RequiredInt64(match, "matchID", sourceName), sourceName),
                Score: FullTimeScore(match, sourceName))).ToHashSet();
            var expected = new HashSet<(long, string, string, string)>
            {
                (81658, "VfL Wolfsburg", "SC Paderborn 07", "0:0"),
                (81659, "SC Paderborn 07", "VfL Wolfsburg", "2:1")
            };
            if (!identities.SetEquals(expected))
            {
                throw Invalid(sourceName, "does not contain the exact two accepted relegation legs");
            }
        }

        private static void ValidateDfbPokalFinal(JsonElement[] matches, string sourceName)
        {
            var final = matches.SingleOrDefault(match => RequiredInt64(match, "matchID", sourceName) == 81581);
            if (final.ValueKind == JsonValueKind.Undefined
                || !string.Equals(RequiredTeamName(final, "team1", 81581, sourceName), "FC Bayern München", StringComparison.Ordinal)
                || !string.Equals(RequiredTeamName(final, "team2", 81581, sourceName), "VfB Stuttgart", StringComparison.Ordinal)
                || !string.Equals(FullTimeScore(final, sourceName), "3:0", StringComparison.Ordinal)
                || !string.Equals(RequiredString(final, "matchDateTime", sourceName), "2026-05-23T20:00:00", StringComparison.Ordinal))
            {
                throw Invalid(sourceName, "does not contain the accepted exact DFB-Pokal final identity");
            }
        }

        private static void ValidateDfbPokal2026LiveCompletion(JsonElement[] matches, string sourceName)
        {
            var accepted = new[]
            {
                new Dfb2026SourceIdentity(81832, "SC St. Tönis", "Eintracht Frankfurt", "2026-08-21T18:00:00", 2, "0:11"),
                new Dfb2026SourceIdentity(81833, "Erzgebirge Aue", "TSG Hoffenheim", "2026-08-22T15:30:00", 2, "0:4"),
                new Dfb2026SourceIdentity(81834, "Eintracht Braunschweig", "1. FC Union Berlin", "2026-08-23T15:30:00", 2, "2:4"),
                new Dfb2026SourceIdentity(81835, "Eintracht Trier", "RB Leipzig", "2026-08-22T18:00:00", 2, "0:6"),
                new Dfb2026SourceIdentity(81836, "Hamburg Eimsbütteler BC", "Borussia Dortmund", "2026-09-01T20:45:00", 2, "0:5"),
                new Dfb2026SourceIdentity(81837, "TSV Schott Mainz", "Borussia Mönchengladbach", "2026-08-23T15:30:00", 2, "0:5"),
                new Dfb2026SourceIdentity(81838, "Fortuna Düsseldorf", "SC Freiburg", "2026-08-23T18:00:00", 2, "1:5"),
                new Dfb2026SourceIdentity(81842, "SV Wehen Wiesbaden", "Bayer 04 Leverkusen", "2026-08-22T13:00:00", 2, "0:4"),
                new Dfb2026SourceIdentity(81843, "Hallescher FC", "FC Schalke 04", "2026-08-24T20:45:00", 4, "2:5"),
                new Dfb2026SourceIdentity(81844, "Energie Cottbus", "FC Augsburg", "2026-08-22T13:00:00", 2, "0:2"),
                new Dfb2026SourceIdentity(81845, "VfB 1921 Krieschow ", "1. FSV Mainz 05", "2026-08-23T15:30:00", 2, "0:9"),
                new Dfb2026SourceIdentity(81851, "MSV Duisburg", "SV 07 Elversberg", "2026-08-22T15:30:00", 2, "1:3"),
                new Dfb2026SourceIdentity(81852, "VfL Osnabrück", "Bayern München", "2026-09-02T20:45:00", 2, "1:4"),
                new Dfb2026SourceIdentity(81853, "Lüneburger SK Hansa", "SV Werder Bremen", "2026-08-22T15:30:00", 2, "0:3"),
                new Dfb2026SourceIdentity(81854, "SC Verl", "Hamburger SV", "2026-08-24T18:00:00", 2, "0:3"),
                new Dfb2026SourceIdentity(81855, "Hansa Rostock", "VfB Stuttgart", "2026-08-21T20:45:00", 2, "0:4"),
                new Dfb2026SourceIdentity(81861, "1. FC Phönix Lübeck", "SC Paderborn 07", "2026-08-23T18:00:00", 2, "2:4"),
                new Dfb2026SourceIdentity(81863, "Würzburger Kickers", "1. FC Köln", "2026-08-24T18:00:00", 2, "1:2")
            };
            foreach (var identity in accepted)
            {
                var match = matches.SingleOrDefault(value => RequiredInt64(value, "matchID", sourceName) == identity.MatchId);
                if (match.ValueKind == JsonValueKind.Undefined
                    || !string.Equals(RequiredTeamName(match, "team1", identity.MatchId, sourceName), identity.HomeTeam, StringComparison.Ordinal)
                    || !string.Equals(RequiredTeamName(match, "team2", identity.MatchId, sourceName), identity.AwayTeam, StringComparison.Ordinal)
                    || !string.Equals(ResultScore(match, identity.ResultTypeId, sourceName), identity.Score, StringComparison.Ordinal)
                    || !string.Equals(RequiredString(match, "matchDateTime", sourceName), identity.LocalDateTime, StringComparison.Ordinal))
                {
                    throw Invalid(sourceName, $"does not contain accepted exact DFB-Pokal completion identity {identity.MatchId}");
                }
            }

            var firstCompletion = matches.Single(value => RequiredInt64(value, "matchID", sourceName) == 81832);
            if (!string.Equals(ResultScore(firstCompletion, 1, sourceName), "0:10", StringComparison.Ordinal))
            {
                throw Invalid(sourceName, "does not retain the accepted match 81832 halftime identity");
            }

            var extraTimeCompletion = matches.Single(value => RequiredInt64(value, "matchID", sourceName) == 81843);
            if (!string.Equals(ResultScore(extraTimeCompletion, 2, sourceName), "2:2", StringComparison.Ordinal))
            {
                throw Invalid(sourceName, "does not retain the accepted match 81843 full-time identity");
            }
        }

        private sealed record Dfb2026SourceIdentity(
            long MatchId,
            string HomeTeam,
            string AwayTeam,
            string LocalDateTime,
            int ResultTypeId,
            string Score);

        private static string FullTimeScore(JsonElement match, string sourceName)
            => ResultScore(match, 2, sourceName);

        private static string ResultScore(JsonElement match, int resultTypeId, string sourceName)
        {
            var result = RequiredProperty(match, "matchResults", sourceName).EnumerateArray()
                .Single(value => RequiredInt32(value, "resultTypeID", sourceName) == resultTypeId);
            return $"{RequiredInt32(result, "pointsTeam1", sourceName)}:{RequiredInt32(result, "pointsTeam2", sourceName)}";
        }
    }
}
