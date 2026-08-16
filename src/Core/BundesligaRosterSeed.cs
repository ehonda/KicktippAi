using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

public sealed class BundesligaRosterSeed
{
    public const string RelativePath = "data/bundesliga-2026-27/rosters/roster-membership-seed.csv";

    public static readonly IReadOnlyList<string> Headers =
    [
        "Team_Slug",
        "Role",
        "Name",
        "Transfermarkt_Club_Id",
        "Transfermarkt_Player_Id",
        "Membership_Source_Url",
        "Membership_As_Of"
    ];

    private static readonly Regex CollapsibleWhitespace = new(
        "\\s+",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private BundesligaRosterSeed(
        IReadOnlyList<BundesligaRosterSeedEntry> entries,
        IReadOnlyList<string> diagnostics)
    {
        Entries = entries;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<BundesligaRosterSeedEntry> Entries { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public static BundesligaRosterSeed Parse(
        byte[] content,
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
        return Parse(reader, expectedTeams, sourceName);
    }

    public static BundesligaRosterSeed Parse(
        TextReader reader,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null,
        string sourceName = RelativePath)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        expectedTeams ??= BundesligaTeamManifest.Default.Entries;

        if (expectedTeams.Count == 0)
        {
            throw new ArgumentException("At least one expected team is required.", nameof(expectedTeams));
        }

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

            var entries = new List<BundesligaRosterSeedEntry>();
            while (csv.Read())
            {
                if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) == true)
                {
                    throw Invalid(sourceName, $"row {csv.Parser.Row} must not be blank");
                }

                entries.Add(ParseEntry(csv, sourceName));
            }

            var diagnostics = Validate(entries, expectedTeams, sourceName);
            return new BundesligaRosterSeed(entries.AsReadOnly(), diagnostics);
        }
        catch (CsvHelperException exception)
        {
            throw Invalid(
                sourceName,
                $"CSV parsing failed at row {exception.Context?.Parser?.Row ?? -1}: {exception.Message}",
                exception);
        }
    }

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return CollapsibleWhitespace.Replace(name.Trim().Normalize(NormalizationForm.FormKC), " ");
    }

    private static BundesligaRosterSeedEntry ParseEntry(CsvReader csv, string sourceName)
    {
        var row = csv.Parser.Row;
        var teamSlug = Required(csv, "Team_Slug", sourceName, row);
        var roleValue = Required(csv, "Role", sourceName, row);
        var name = NormalizeName(Required(csv, "Name", sourceName, row));
        var clubId = OptionalPositiveInt(csv, "Transfermarkt_Club_Id", sourceName, row);
        var playerId = OptionalPositiveInt(csv, "Transfermarkt_Player_Id", sourceName, row);
        var sourceUrlValue = Required(csv, "Membership_Source_Url", sourceName, row);
        var asOfValue = Required(csv, "Membership_As_Of", sourceName, row);

        if (!Enum.TryParse<BundesligaRosterRole>(roleValue, ignoreCase: false, out var role))
        {
            throw Invalid(sourceName, $"row {row} has invalid Role '{roleValue}'");
        }

        if (role == BundesligaRosterRole.Coach && playerId is not null)
        {
            throw Invalid(sourceName, $"row {row} must leave Transfermarkt_Player_Id empty for the coach");
        }

        if (!Uri.TryCreate(sourceUrlValue, UriKind.Absolute, out var sourceUrl)
            || !string.Equals(sourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(sourceName, $"row {row} requires an absolute HTTPS Membership_Source_Url");
        }

        if (!DateOnly.TryParseExact(
                asOfValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var membershipAsOf))
        {
            throw Invalid(sourceName, $"row {row} has invalid Membership_As_Of '{asOfValue}'");
        }

        return new BundesligaRosterSeedEntry(
            teamSlug,
            role,
            name,
            clubId,
            playerId,
            sourceUrl,
            membershipAsOf);
    }

    private static IReadOnlyList<string> Validate(
        IReadOnlyList<BundesligaRosterSeedEntry> entries,
        IReadOnlyList<BundesligaTeamManifestEntry> expectedTeams,
        string sourceName)
    {
        var expectedBySlug = expectedTeams.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
        var groups = entries.GroupBy(entry => entry.TeamSlug, StringComparer.Ordinal).ToArray();

        var actualSlugs = groups.Select(group => group.Key).Order(StringComparer.Ordinal).ToArray();
        var expectedSlugs = expectedBySlug.Keys.Order(StringComparer.Ordinal).ToArray();
        if (!actualSlugs.SequenceEqual(expectedSlugs, StringComparer.Ordinal))
        {
            throw Invalid(
                sourceName,
                $"team coverage must be exactly [{string.Join(',', expectedSlugs)}] but was [{string.Join(',', actualSlugs)}]");
        }

        foreach (var group in groups)
        {
            var team = expectedBySlug[group.Key];
            var rows = group.ToArray();
            var coaches = rows.Count(entry => entry.Role == BundesligaRosterRole.Coach);
            var players = rows.Where(entry => entry.Role == BundesligaRosterRole.Player).ToArray();

            if (coaches != 1)
            {
                throw Invalid(sourceName, $"team '{group.Key}' requires exactly one Coach but found {coaches}");
            }

            if (players.Length is < BundesligaRosterPolicy.MinimumPlayerCount or > BundesligaRosterPolicy.MaximumPlayerCount)
            {
                throw Invalid(
                    sourceName,
                    $"team '{group.Key}' requires {BundesligaRosterPolicy.MinimumPlayerCount}-{BundesligaRosterPolicy.MaximumPlayerCount} Players but found {players.Length}");
            }

            if (rows.Select(entry => entry.MembershipAsOf).Distinct().Count() != 1)
            {
                throw Invalid(sourceName, $"team '{group.Key}' must use one Membership_As_Of date");
            }

            foreach (var row in rows)
            {
                if (row.TransfermarktClubId is not null
                    && row.TransfermarktClubId != team.TransfermarktClubId)
                {
                    throw Invalid(
                        sourceName,
                        $"team '{group.Key}' has Transfermarkt_Club_Id {row.TransfermarktClubId} but the manifest has {team.TransfermarktClubId?.ToString(CultureInfo.InvariantCulture) ?? "no ID"}");
                }
            }

            EnsureUnique(
                rows,
                entry => NormalizeName(entry.Name),
                $"team '{group.Key}' member Name",
                sourceName);
            EnsureUnique(
                players.Where(entry => entry.TransfermarktPlayerId is not null),
                entry => entry.TransfermarktPlayerId!.Value,
                $"team '{group.Key}' Transfermarkt_Player_Id",
                sourceName);
        }

        EnsureUnique(
            entries.Where(entry => entry.Role == BundesligaRosterRole.Player && entry.TransfermarktPlayerId is not null),
            entry => entry.TransfermarktPlayerId!.Value,
            "cross-team Transfermarkt_Player_Id",
            sourceName);

        var ordered = entries
            .OrderBy(entry => entry.TeamSlug, StringComparer.Ordinal)
            .ThenBy(entry => entry.Role == BundesligaRosterRole.Coach ? 0 : 1)
            .ThenBy(entry => NormalizeName(entry.Name), StringComparer.Ordinal)
            .ThenBy(entry => entry.TransfermarktPlayerId ?? 0)
            .ToArray();
        if (!entries.SequenceEqual(ordered))
        {
            throw Invalid(sourceName, "rows must be ordered by Team_Slug, Coach before Player, normalized Name, then Transfermarkt_Player_Id");
        }

        return entries
            .Where(entry => entry.Role == BundesligaRosterRole.Player && entry.TransfermarktPlayerId is null)
            .GroupBy(entry => NormalizeName(entry.Name), StringComparer.Ordinal)
            .Where(group => group.Select(entry => entry.TeamSlug).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => $"CROSS_TEAM_NAME_REVIEW:{group.Key}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Required(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = csv.GetField(fieldName)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(sourceName, $"row {row} requires non-empty {fieldName}")
            : value;
    }

    private static int? OptionalPositiveInt(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = csv.GetField(fieldName)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw Invalid(sourceName, $"row {row} has invalid {fieldName} '{value}'");
    }

    private static void EnsureUnique<TEntry, TKey>(
        IEnumerable<TEntry> entries,
        Func<TEntry, TKey> selector,
        string fieldName,
        string sourceName)
        where TKey : notnull
    {
        var duplicate = entries.GroupBy(selector).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw Invalid(sourceName, $"{fieldName} value '{duplicate.Key}' must be unique");
        }
    }

    private static InvalidDataException Invalid(string sourceName, string message, Exception? innerException = null)
    {
        return new InvalidDataException($"Invalid Bundesliga roster seed '{sourceName}': {message}.", innerException);
    }
}
