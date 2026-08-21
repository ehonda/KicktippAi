using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public enum OpenLigaDbHistorySnapshotKind
{
    SecondBundesliga,
    Relegation,
    DfbPokal
}

public sealed record OpenLigaDbHistorySnapshotValidation(
    OpenLigaDbHistorySnapshotKind Kind,
    string Sha256,
    int MatchCount,
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
        foreach (var match in matches)
        {
            var matchId = RequiredInt64(match, "matchID", sourceName);
            if (matchId <= 0 || !ids.Add(matchId))
            {
                throw Invalid(sourceName, $"matchID '{matchId}' must be positive and unique");
            }

            if (!RequiredBoolean(match, "matchIsFinished", sourceName))
            {
                throw Invalid(sourceName, $"matchID '{matchId}' is not completed");
            }
            if (RequiredInt32(match, "leagueSeason", sourceName) != 2025
                || !string.Equals(RequiredString(match, "leagueShortcut", sourceName), contract.LeagueShortcut, StringComparison.Ordinal))
            {
                throw Invalid(sourceName, $"matchID '{matchId}' is outside {contract.LeagueShortcut}/2025");
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
            if (fullTimeResults.Length != 1)
            {
                throw Invalid(sourceName, $"matchID '{matchId}' must have exactly one full-time resultTypeID 2");
            }
            _ = RequiredInt32(fullTimeResults[0], "pointsTeam1", sourceName);
            _ = RequiredInt32(fullTimeResults[0], "pointsTeam2", sourceName);
        }

        contract.ValidateIdentities(matches, sourceName);
        return new(kind, hash, matches.Length, ids);
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
        string LeagueShortcut,
        string Revision,
        Action<JsonElement[], string> ValidateIdentities)
    {
        public static Contract For(OpenLigaDbHistorySnapshotKind kind) => kind switch
        {
            OpenLigaDbHistorySnapshotKind.SecondBundesliga => new(
                306, "bl2", BundesligaHistoryPlayedDateMap.OpenLigaDbLeagueRevision, (_, _) => { }),
            OpenLigaDbHistorySnapshotKind.Relegation => new(
                2, "rel", BundesligaHistoryPlayedDateMap.OpenLigaDbRelegationRevision, ValidateRelegation),
            OpenLigaDbHistorySnapshotKind.DfbPokal => new(
                63, "dfb", BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalRevision, ValidateDfbPokalFinal),
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

        private static string FullTimeScore(JsonElement match, string sourceName)
        {
            var result = RequiredProperty(match, "matchResults", sourceName).EnumerateArray()
                .Single(value => RequiredInt32(value, "resultTypeID", sourceName) == 2);
            return $"{RequiredInt32(result, "pointsTeam1", sourceName)}:{RequiredInt32(result, "pointsTeam2", sourceName)}";
        }
    }
}
