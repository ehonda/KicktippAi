using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

public static class BundesligaClubEloSeed
{
    public const string RelativePath = "data/bundesliga-2026-27/club-elo-launch-seed.csv";

    public static readonly IReadOnlyList<string> Headers =
    [
        "Team_Slug",
        "Club_Elo_Name",
        "Global_Rank",
        "ELO",
        "Rated_At",
        "Collected_At",
        "Source_Url"
    ];

    private const string ResourceName = "EHonda.KicktippAi.Core.Data.Bundesliga2026_27ClubEloLaunchSeed.csv";
    private const string CollectedAtFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private static readonly Lazy<BundesligaClubEloSnapshot> DefaultSeed = new(LoadEmbedded);

    public static BundesligaClubEloSnapshot Default => DefaultSeed.Value;

    public static BundesligaClubEloSnapshot Parse(
        byte[] content,
        BundesligaClubEloSnapshotOrigin origin = BundesligaClubEloSnapshotOrigin.LaunchSeed,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null,
        string sourceName = RelativePath)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw Invalid(sourceName, "content must not be empty");
        }

        if (content.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            throw Invalid(sourceName, "UTF-8 content must not contain a byte-order mark");
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content);
        }
        catch (DecoderFallbackException exception)
        {
            throw Invalid(sourceName, "content must be valid UTF-8", exception);
        }

        if (!text.EndsWith("\r\n", StringComparison.Ordinal))
        {
            throw Invalid(sourceName, "content must end with a final CRLF");
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n'))
            {
                throw Invalid(sourceName, "content must not contain a bare carriage return");
            }

            if (text[index] == '\n' && (index == 0 || text[index - 1] != '\r'))
            {
                throw Invalid(sourceName, "content must not contain a bare line feed");
            }
        }

        using var reader = new StringReader(text);
        return Parse(reader, origin, expectedTeams, sourceName);
    }

    public static BundesligaClubEloSnapshot Parse(
        TextReader reader,
        BundesligaClubEloSnapshotOrigin origin = BundesligaClubEloSnapshotOrigin.LaunchSeed,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null,
        string sourceName = RelativePath)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        expectedTeams ??= BundesligaTeamManifest.Default.Entries;

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        try
        {
            using var csv = new CsvReader(reader, configuration);
            if (!csv.Read() || !csv.ReadHeader())
            {
                throw Invalid(sourceName, "a header row is required");
            }

            if (!(csv.HeaderRecord ?? []).SequenceEqual(Headers, StringComparer.Ordinal))
            {
                throw Invalid(sourceName, $"headers must be exactly: {string.Join(',', Headers)}");
            }

            var rows = new List<SeedRow>();
            while (csv.Read())
            {
                if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) == true)
                {
                    throw Invalid(sourceName, $"row {csv.Parser.Row} must not be blank");
                }

                rows.Add(ParseRow(csv, sourceName));
            }

            return BuildSnapshot(rows, origin, expectedTeams, sourceName);
        }
        catch (CsvHelperException exception)
        {
            throw Invalid(
                sourceName,
                $"CSV parsing failed at row {exception.Context?.Parser?.Row ?? -1}: {exception.Message}",
                exception);
        }
    }

    private static SeedRow ParseRow(CsvReader csv, string sourceName)
    {
        var row = csv.Parser.Row;
        var teamSlug = Required(csv, "Team_Slug", sourceName, row);
        var clubEloName = Required(csv, "Club_Elo_Name", sourceName, row);
        var globalRank = PositiveInt(csv, "Global_Rank", sourceName, row);
        var elo = PositiveInt(csv, "ELO", sourceName, row);
        var ratedAtValue = Required(csv, "Rated_At", sourceName, row);
        var collectedAtValue = Required(csv, "Collected_At", sourceName, row);
        var sourceUrlValue = Required(csv, "Source_Url", sourceName, row);

        if (!DateOnly.TryParseExact(
                ratedAtValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ratedAt))
        {
            throw Invalid(sourceName, $"row {row} has invalid Rated_At '{ratedAtValue}'");
        }

        if (!DateTimeOffset.TryParseExact(
                collectedAtValue,
                CollectedAtFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var collectedAt))
        {
            throw Invalid(sourceName, $"row {row} has invalid Collected_At '{collectedAtValue}'");
        }

        if (!Uri.TryCreate(sourceUrlValue, UriKind.Absolute, out var sourceUrl)
            || !string.Equals(sourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(sourceName, $"row {row} requires an absolute HTTPS Source_Url");
        }

        return new SeedRow(teamSlug, clubEloName, globalRank, elo, ratedAt, collectedAt, sourceUrl);
    }

    private static BundesligaClubEloSnapshot BuildSnapshot(
        IReadOnlyList<SeedRow> rows,
        BundesligaClubEloSnapshotOrigin origin,
        IReadOnlyList<BundesligaTeamManifestEntry> expectedTeams,
        string sourceName)
    {
        if (rows.Count != expectedTeams.Count)
        {
            throw Invalid(sourceName, $"expected {expectedTeams.Count} teams but found {rows.Count}");
        }

        var expectedBySlug = expectedTeams.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
        var entries = new List<BundesligaClubEloEntry>(rows.Count);
        foreach (var row in rows)
        {
            if (!expectedBySlug.TryGetValue(row.TeamSlug, out var team))
            {
                throw Invalid(sourceName, $"unknown Team_Slug '{row.TeamSlug}'");
            }

            if (!string.Equals(row.ClubEloName, team.ClubEloName, StringComparison.Ordinal))
            {
                throw Invalid(
                    sourceName,
                    $"Team_Slug '{row.TeamSlug}' requires Club_Elo_Name '{team.ClubEloName}', not '{row.ClubEloName}'");
            }

            entries.Add(new BundesligaClubEloEntry(team, row.GlobalRank, row.Elo));
        }

        var ratedAt = SingleValue(rows.Select(row => row.RatedAt), "Rated_At", sourceName);
        var collectedAt = SingleValue(rows.Select(row => row.CollectedAt), "Collected_At", sourceName);
        var sourceUrl = SingleValue(rows.Select(row => row.SourceUrl), "Source_Url", sourceName);

        try
        {
            return BundesligaClubEloSnapshot.Create(
                entries,
                ratedAt,
                collectedAt,
                sourceUrl,
                origin,
                expectedTeams);
        }
        catch (InvalidDataException exception)
        {
            throw Invalid(sourceName, exception.Message, exception);
        }
    }

    private static T SingleValue<T>(IEnumerable<T> values, string fieldName, string sourceName)
        where T : notnull
    {
        var distinct = values.Distinct().ToArray();
        return distinct.Length == 1
            ? distinct[0]
            : throw Invalid(sourceName, $"all rows must use one {fieldName} value");
    }

    private static string Required(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = csv.GetField(fieldName)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(sourceName, $"row {row} requires non-empty {fieldName}")
            : value;
    }

    private static int PositiveInt(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = Required(csv, fieldName, sourceName, row);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw Invalid(sourceName, $"row {row} has invalid {fieldName} '{value}'");
    }

    private static BundesligaClubEloSnapshot LoadEmbedded()
    {
        using var stream = typeof(BundesligaClubEloSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Bundesliga Club Elo seed resource '{ResourceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Parse(memory.ToArray());
    }

    private static InvalidDataException Invalid(string sourceName, string message, Exception? innerException = null)
    {
        return new InvalidDataException($"Invalid Bundesliga Club Elo snapshot '{sourceName}': {message}.", innerException);
    }

    private sealed record SeedRow(
        string TeamSlug,
        string ClubEloName,
        int GlobalRank,
        int Elo,
        DateOnly RatedAt,
        DateTimeOffset CollectedAt,
        Uri SourceUrl);
}
