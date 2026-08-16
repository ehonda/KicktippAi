using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaTeamManifestEntry(
    string KicktippName,
    string TeamSlug,
    string OfficialName,
    Uri OfficialRosterSourceUrl,
    string ClubEloName,
    int? TransfermarktClubId);

public sealed class BundesligaTeamManifest
{
    public const int ExpectedTeamCount = 18;
    public const string RelativePath = "data/bundesliga-2026-27/team-manifest.csv";

    private const string ResourceName = "EHonda.KicktippAi.Core.Data.Bundesliga2026_27TeamManifest.csv";
    private static readonly string[] ExpectedHeaders =
    [
        "Kicktipp_Name",
        "Team_Slug",
        "Official_Name",
        "Official_Roster_Source_Url",
        "Club_Elo_Name",
        "Transfermarkt_Club_Id"
    ];

    private static readonly Regex TeamSlugPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Lazy<BundesligaTeamManifest> DefaultManifest = new(LoadEmbedded);

    private readonly IReadOnlyDictionary<string, BundesligaTeamManifestEntry> _byKicktippName;
    private readonly IReadOnlyDictionary<string, BundesligaTeamManifestEntry> _byTeamSlug;
    private readonly IReadOnlyDictionary<string, BundesligaTeamManifestEntry> _byClubEloName;

    private BundesligaTeamManifest(IReadOnlyList<BundesligaTeamManifestEntry> entries)
    {
        Entries = entries;
        _byKicktippName = entries.ToDictionary(entry => entry.KicktippName, StringComparer.Ordinal);
        _byTeamSlug = entries.ToDictionary(entry => entry.TeamSlug, StringComparer.Ordinal);
        _byClubEloName = entries.ToDictionary(entry => entry.ClubEloName, StringComparer.Ordinal);
    }

    public static BundesligaTeamManifest Default => DefaultManifest.Value;

    public IReadOnlyList<BundesligaTeamManifestEntry> Entries { get; }

    public BundesligaTeamManifestEntry GetByKicktippName(string kicktippName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kicktippName);

        if (_byKicktippName.TryGetValue(kicktippName, out var entry))
        {
            return entry;
        }

        throw new KeyNotFoundException(
            $"Unknown {CompetitionIds.Bundesliga2026_27} Kicktipp team '{kicktippName}'. " +
            $"Add its exact Kicktipp name to {RelativePath}; automatic slug fallback is disabled for this competition.");
    }

    public BundesligaTeamManifestEntry GetByTeamSlug(string teamSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamSlug);

        return _byTeamSlug.TryGetValue(teamSlug, out var entry)
            ? entry
            : throw new KeyNotFoundException(
                $"Unknown {CompetitionIds.Bundesliga2026_27} team slug '{teamSlug}' in {RelativePath}.");
    }

    public BundesligaTeamManifestEntry GetByClubEloName(string clubEloName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clubEloName);

        return _byClubEloName.TryGetValue(clubEloName, out var entry)
            ? entry
            : throw new KeyNotFoundException(
                $"Unknown Club Elo alias '{clubEloName}' in {RelativePath}.");
    }

    public static BundesligaTeamManifest Parse(TextReader reader, string sourceName = RelativePath)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

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

            var actualHeaders = csv.HeaderRecord ?? [];
            if (!actualHeaders.SequenceEqual(ExpectedHeaders, StringComparer.Ordinal))
            {
                throw Invalid(
                    sourceName,
                    $"headers must be exactly: {string.Join(',', ExpectedHeaders)}");
            }

            var entries = new List<BundesligaTeamManifestEntry>();
            while (csv.Read())
            {
                if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) == true)
                {
                    continue;
                }

                entries.Add(ParseEntry(csv, sourceName));
            }

            Validate(entries, sourceName);
            return new BundesligaTeamManifest(entries.AsReadOnly());
        }
        catch (CsvHelperException exception)
        {
            throw Invalid(
                sourceName,
                $"CSV parsing failed at row {exception.Context?.Parser?.Row ?? -1}: {exception.Message}",
                exception);
        }
    }

    private static BundesligaTeamManifestEntry ParseEntry(CsvReader csv, string sourceName)
    {
        var row = csv.Parser.Row;
        var kicktippName = Required(csv, "Kicktipp_Name", sourceName, row);
        var teamSlug = Required(csv, "Team_Slug", sourceName, row);
        var officialName = Required(csv, "Official_Name", sourceName, row);
        var officialRosterSource = Required(csv, "Official_Roster_Source_Url", sourceName, row);
        var clubEloName = Required(csv, "Club_Elo_Name", sourceName, row);
        var transfermarktIdValue = csv.GetField("Transfermarkt_Club_Id")?.Trim();

        if (!TeamSlugPattern.IsMatch(teamSlug))
        {
            throw Invalid(sourceName, $"row {row} has invalid Team_Slug '{teamSlug}'");
        }

        if (!Uri.TryCreate(officialRosterSource, UriKind.Absolute, out var officialRosterSourceUrl)
            || !string.Equals(officialRosterSourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(sourceName, $"row {row} requires an absolute HTTPS Official_Roster_Source_Url");
        }

        int? transfermarktClubId = null;
        if (!string.IsNullOrEmpty(transfermarktIdValue))
        {
            if (!int.TryParse(transfermarktIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId)
                || parsedId <= 0)
            {
                throw Invalid(sourceName, $"row {row} has invalid Transfermarkt_Club_Id '{transfermarktIdValue}'");
            }

            transfermarktClubId = parsedId;
        }

        return new BundesligaTeamManifestEntry(
            kicktippName,
            teamSlug,
            officialName,
            officialRosterSourceUrl,
            clubEloName,
            transfermarktClubId);
    }

    private static string Required(CsvReader csv, string fieldName, string sourceName, int row)
    {
        var value = csv.GetField(fieldName)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(sourceName, $"row {row} requires non-empty {fieldName}")
            : value;
    }

    private static void Validate(IReadOnlyList<BundesligaTeamManifestEntry> entries, string sourceName)
    {
        if (entries.Count != ExpectedTeamCount)
        {
            throw Invalid(sourceName, $"expected {ExpectedTeamCount} teams but found {entries.Count}");
        }

        EnsureUnique(entries, entry => entry.KicktippName, "Kicktipp_Name", sourceName);
        EnsureUnique(entries, entry => entry.TeamSlug, "Team_Slug", sourceName);
        EnsureUnique(entries, entry => entry.OfficialName, "Official_Name", sourceName);
        EnsureUnique(entries, entry => entry.ClubEloName, "Club_Elo_Name", sourceName);

        var sortedSlugs = entries.Select(entry => entry.TeamSlug).Order(StringComparer.Ordinal);
        if (!entries.Select(entry => entry.TeamSlug).SequenceEqual(sortedSlugs, StringComparer.Ordinal))
        {
            throw Invalid(sourceName, "rows must be sorted by Team_Slug using ordinal comparison");
        }
    }

    private static void EnsureUnique(
        IEnumerable<BundesligaTeamManifestEntry> entries,
        Func<BundesligaTeamManifestEntry, string> selector,
        string fieldName,
        string sourceName)
    {
        var duplicate = entries
            .GroupBy(selector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw Invalid(sourceName, $"{fieldName} value '{duplicate.Key}' must be unique");
        }
    }

    private static BundesligaTeamManifest LoadEmbedded()
    {
        using var stream = typeof(BundesligaTeamManifest).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Bundesliga team manifest resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    private static InvalidDataException Invalid(string sourceName, string message, Exception? innerException = null)
    {
        return new InvalidDataException($"Invalid Bundesliga team manifest '{sourceName}': {message}.", innerException);
    }
}
