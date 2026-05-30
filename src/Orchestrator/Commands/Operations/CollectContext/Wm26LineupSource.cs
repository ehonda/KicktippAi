using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using DuckDB.NET.Data;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Commands.Operations.CollectContext;

public interface IWm26LineupSource
{
    Task<Wm26LineupCollection> CollectAsync(
        Wm26LineupSourceRequest request,
        CancellationToken cancellationToken = default);
}

internal interface IWm26TransfermarktDuckDbProvider
{
    Task<string> GetDatabasePathAsync(
        string? configuredPath,
        CancellationToken cancellationToken = default);
}

internal sealed class Wm26LineupSource : IWm26LineupSource
{
    private const string MissingValue = "N/A";

    private static readonly IReadOnlyList<string> RequiredSeedColumns =
    [
        "Team_Slug",
        "Team",
        "Data_Collected_At",
        "Role",
        "Name",
        "Transfermarkt_National_Team_Id",
        "Transfermarkt_Player_Id"
    ];

    private static readonly IReadOnlyList<string> OutputColumns =
    [
        "Team",
        "Data_Collected_At",
        "Role",
        "Name",
        "Age",
        "Position",
        "Market_Value_EUR"
    ];

    private static readonly Regex NonAlphanumericRegex = new("[^a-z0-9]+", RegexOptions.Compiled);

    private readonly IWm26TransfermarktDuckDbProvider _duckDbProvider;

    public Wm26LineupSource(IWm26TransfermarktDuckDbProvider duckDbProvider)
    {
        _duckDbProvider = duckDbProvider;
    }

    public async Task<Wm26LineupCollection> CollectAsync(
        Wm26LineupSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var seedPath = ResolvePath(request.SeedPath);
        var teamsPath = ResolvePath(request.TeamsPath);
        var teams = ReadTeamManifest(teamsPath);
        var seedRows = ReadSeedRows(seedPath);
        var databasePath = await _duckDbProvider.GetDatabasePathAsync(request.DuckDbPath, cancellationToken);

        var enrichedRows = EnrichRows(seedRows, databasePath);
        var groupedRows = GroupRowsByManifest(teams, enrichedRows);
        ValidateCoaches(groupedRows);

        var contextDocuments = groupedRows
            .Select(entry => new Wm26LineupDocument(
                $"lineup-{entry.Team.Slug}.csv",
                RenderCsv(entry.Rows),
                entry.Team.Name,
                entry.Rows.Count(row => string.Equals(row.Role, "Player", StringComparison.Ordinal)),
                entry.Rows.Count == 0))
            .ToList();

        var aggregateRows = groupedRows.SelectMany(entry => entry.Rows);
        var kpiContent = RenderCsv(aggregateRows);
        var headerOnlyTeams = contextDocuments
            .Where(document => document.IsHeaderOnly)
            .Select(document => new Wm26LineupTeam(GetSlugFromDocumentName(document.DocumentName), document.TeamName))
            .ToList();
        var missingSourceData = BuildMissingSourceData(enrichedRows);

        return new Wm26LineupCollection(
            seedPath,
            teamsPath,
            databasePath,
            seedRows.Count,
            enrichedRows.Count,
            contextDocuments,
            kpiContent,
            headerOnlyTeams,
            missingSourceData);
    }

    private static string ResolvePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Path.GetFullPath(value);
    }

    private static string GetSlugFromDocumentName(string documentName)
    {
        return documentName["lineup-".Length..^4];
    }

    private static IReadOnlyList<Wm26LineupTeam> ReadTeamManifest(string teamsPath)
    {
        if (!File.Exists(teamsPath))
        {
            throw new FileNotFoundException($"Team manifest CSV not found: {teamsPath}", teamsPath);
        }

        using var reader = new StreamReader(teamsPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = CreateReader(reader);
        csv.Read();
        csv.ReadHeader();
        ValidateColumns(csv, ["Team_Slug", "Team"], "Team manifest CSV");

        var teams = new List<Wm26LineupTeam>();
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (csv.Read())
        {
            var lineNumber = csv.Context?.Parser?.Row ?? 0;
            var slug = GetTrimmedField(csv, "Team_Slug");
            var team = GetTrimmedField(csv, "Team");

            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new InvalidOperationException($"Team manifest line {lineNumber}: missing Team_Slug");
            }

            if (string.IsNullOrWhiteSpace(team))
            {
                throw new InvalidOperationException($"Team manifest line {lineNumber}: missing Team");
            }

            if (!slugs.Add(slug))
            {
                throw new InvalidOperationException($"Team manifest line {lineNumber}: duplicate Team_Slug {slug}");
            }

            teams.Add(new Wm26LineupTeam(slug, team));
        }

        if (teams.Count == 0)
        {
            throw new InvalidOperationException("Team manifest CSV has no team rows");
        }

        return teams;
    }

    private static List<Wm26LineupSeedRow> ReadSeedRows(string seedPath)
    {
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Lineup seed CSV not found: {seedPath}", seedPath);
        }

        using var reader = new StreamReader(seedPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = CreateReader(reader);
        csv.Read();
        csv.ReadHeader();
        ValidateColumns(csv, RequiredSeedColumns, "Lineup seed CSV");

        var rows = new List<Wm26LineupSeedRow>();
        while (csv.Read())
        {
            var lineNumber = csv.Context?.Parser?.Row ?? 0;
            var row = new Wm26LineupSeedRow(
                GetTrimmedField(csv, "Team_Slug"),
                GetTrimmedField(csv, "Team"),
                GetTrimmedField(csv, "Data_Collected_At"),
                GetTrimmedField(csv, "Role"),
                GetTrimmedField(csv, "Name"),
                GetTrimmedField(csv, "Transfermarkt_National_Team_Id"),
                GetTrimmedField(csv, "Transfermarkt_Player_Id"),
                GetOptionalField(csv, "Age"),
                GetOptionalField(csv, "Position"),
                GetOptionalField(csv, "Market_Value_EUR"));

            ValidateSeedRow(row, lineNumber);
            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Lineup seed CSV has no lineup rows");
        }

        return rows;
    }

    private static CsvReader CreateReader(TextReader reader)
    {
        return new CsvReader(
            reader,
            new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                BadDataFound = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim
            });
    }

    private static void ValidateColumns(CsvReader csv, IReadOnlyList<string> requiredColumns, string label)
    {
        var headers = csv.HeaderRecord ?? [];
        var missing = requiredColumns
            .Where(column => !headers.Contains(column, StringComparer.Ordinal))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"{label} is missing required column(s): {string.Join(", ", missing)}");
        }
    }

    private static string GetTrimmedField(CsvReader csv, string column)
    {
        return (csv.GetField(column) ?? string.Empty).Trim();
    }

    private static string GetOptionalField(CsvReader csv, string column)
    {
        var headers = csv.HeaderRecord ?? [];
        return headers.Contains(column, StringComparer.Ordinal)
            ? GetTrimmedField(csv, column)
            : string.Empty;
    }

    private static void ValidateSeedRow(Wm26LineupSeedRow row, int lineNumber)
    {
        foreach (var (column, value) in new[]
                 {
                     ("Team_Slug", row.TeamSlug),
                     ("Team", row.Team),
                     ("Data_Collected_At", row.DataCollectedAt),
                     ("Role", row.Role)
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Line {lineNumber}: missing {column}");
            }
        }

        if (!IsValidRole(row.Role))
        {
            throw new InvalidOperationException($"Line {lineNumber}: unsupported Role {row.Role}");
        }

        if (string.Equals(row.Role, "Player", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(row.Name)
            && string.IsNullOrWhiteSpace(row.TransfermarktPlayerId))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Player row needs Name or Transfermarkt_Player_Id");
        }

        ValidateAge(row.Age, lineNumber);
        ValidateMarketValue(row.MarketValueEur, row.Role, lineNumber);

        if (!DateOnly.TryParseExact(
                row.DataCollectedAt,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new InvalidOperationException(
                $"Line {lineNumber}: Data_Collected_At must use YYYY-MM-DD, got {row.DataCollectedAt}");
        }
    }

    private static void ValidateOutputRow(Wm26LineupOutputRow row, int lineNumber)
    {
        foreach (var (column, value) in new[]
                 {
                     ("Team", row.Team),
                     ("Data_Collected_At", row.DataCollectedAt),
                     ("Role", row.Role),
                     ("Name", row.Name),
                     ("Position", row.Position)
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Line {lineNumber}: missing {column}");
            }
        }

        if (string.Equals(row.Role, "Player", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(row.Age))
        {
            throw new InvalidOperationException($"Line {lineNumber}: missing Age");
        }

        if (!IsValidRole(row.Role))
        {
            throw new InvalidOperationException($"Line {lineNumber}: unsupported Role {row.Role}");
        }

        ValidateAge(row.Age, lineNumber);
        ValidateMarketValue(row.MarketValueEur, row.Role, lineNumber);
    }

    private static bool IsValidRole(string role)
    {
        return string.Equals(role, "Player", StringComparison.Ordinal)
               || string.Equals(role, "Coach", StringComparison.Ordinal);
    }

    private static void ValidateAge(string age, int lineNumber)
    {
        if (!string.IsNullOrWhiteSpace(age)
            && !string.Equals(age, MissingValue, StringComparison.OrdinalIgnoreCase)
            && !age.All(char.IsDigit))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Age must be numeric or N/A when provided");
        }
    }

    private static void ValidateMarketValue(string marketValue, string role, int lineNumber)
    {
        var normalized = marketValue.Replace(".", string.Empty, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(marketValue)
            && !string.Equals(marketValue, MissingValue, StringComparison.OrdinalIgnoreCase)
            && !normalized.All(char.IsDigit))
        {
            throw new InvalidOperationException(
                $"Line {lineNumber}: Market_Value_EUR must be numeric, N/A, or empty");
        }

        if (string.Equals(role, "Player", StringComparison.Ordinal) && normalized == "0")
        {
            throw new InvalidOperationException(
                $"Line {lineNumber}: Market_Value_EUR must use N/A instead of 0 when unavailable");
        }
    }

    private static List<Wm26LineupOutputRow> EnrichRows(
        IReadOnlyList<Wm26LineupSeedRow> seedRows,
        string databasePath)
    {
        using var connection = new DuckDBConnection($"Data Source={databasePath}");
        connection.Open();

        var enrichedRows = new List<Wm26LineupOutputRow>();
        var errors = new List<string>();
        for (var index = 0; index < seedRows.Count; index++)
        {
            var lineNumber = index + 2;
            var seedRow = seedRows[index];
            try
            {
                var row = string.Equals(seedRow.Role, "Coach", StringComparison.Ordinal)
                    ? EnrichCoachRow(connection, seedRow)
                    : EnrichPlayerRow(connection, seedRow);
                ValidateOutputRow(row, lineNumber);
                enrichedRows.Add(row);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                errors.Add($"Line {lineNumber}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Lineup enrichment failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
        }

        return enrichedRows;
    }

    private static Wm26LineupOutputRow EnrichCoachRow(
        DuckDBConnection connection,
        Wm26LineupSeedRow row)
    {
        var coachName = row.Name;
        if (string.IsNullOrWhiteSpace(coachName) && !string.IsNullOrWhiteSpace(row.TransfermarktNationalTeamId))
        {
            coachName = GetCoachName(connection, row.TransfermarktNationalTeamId);
        }

        if (string.IsNullOrWhiteSpace(coachName))
        {
            throw new InvalidOperationException(
                "Coach row needs Name or Transfermarkt_National_Team_Id with national_teams.coach_name");
        }

        return new Wm26LineupOutputRow(
            row.TeamSlug,
            row.Team,
            row.DataCollectedAt,
            "Coach",
            coachName,
            row.Age,
            string.IsNullOrWhiteSpace(row.Position) ? "Coach" : row.Position,
            string.Empty);
    }

    private static Wm26LineupOutputRow EnrichPlayerRow(
        DuckDBConnection connection,
        Wm26LineupSeedRow row)
    {
        var player = ResolvePlayer(connection, row);
        if (player is null)
        {
            return new Wm26LineupOutputRow(
                row.TeamSlug,
                row.Team,
                row.DataCollectedAt,
                "Player",
                row.Name,
                ProvidedValue(row.Age) ?? MissingValue,
                ProvidedValue(row.Position) ?? MissingValue,
                ProvidedValue(row.MarketValueEur) ?? MissingValue);
        }

        var collectedAt = DateOnly.ParseExact(row.DataCollectedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Wm26LineupOutputRow(
            row.TeamSlug,
            row.Team,
            row.DataCollectedAt,
            "Player",
            string.IsNullOrWhiteSpace(row.Name) ? player.Name : row.Name,
            ProvidedValue(row.Age) ?? CalculateAgeOrMissing(player.DateOfBirth, collectedAt),
            string.IsNullOrWhiteSpace(player.Position) ? ProvidedValue(row.Position) ?? MissingValue : player.Position,
            ProvidedValue(row.MarketValueEur) ?? FormatMarketValueOrMissing(player.MarketValueInEur));
    }

    private static Wm26LineupPlayerRecord? ResolvePlayer(
        DuckDBConnection connection,
        Wm26LineupSeedRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.TransfermarktPlayerId))
        {
            return GetPlayerById(connection, row.TransfermarktPlayerId);
        }

        if (string.IsNullOrWhiteSpace(row.TransfermarktNationalTeamId))
        {
            return null;
        }

        var candidates = GetPlayersByNationalTeamId(connection, row.TransfermarktNationalTeamId);
        var normalizedName = NormalizeName(row.Name);
        var matches = candidates
            .Where(candidate => NormalizeName(candidate.Name) == normalizedName)
            .ToList();

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Player {row.Name} matched multiple Transfermarkt players: {string.Join(", ", matches.Select(match => match.PlayerId))}")
        };
    }

    private static Wm26LineupPlayerRecord? GetPlayerById(DuckDBConnection connection, string playerId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                player_id,
                name,
                date_of_birth,
                position,
                market_value_in_eur,
                current_national_team_id
            from players
            where cast(player_id as varchar) = $player_id
            """;
        command.Parameters.Add(new DuckDBParameter("player_id", playerId));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPlayer(reader) : null;
    }

    private static IReadOnlyList<Wm26LineupPlayerRecord> GetPlayersByNationalTeamId(
        DuckDBConnection connection,
        string nationalTeamId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                player_id,
                name,
                date_of_birth,
                position,
                market_value_in_eur,
                current_national_team_id
            from players
            where cast(current_national_team_id as varchar) = $national_team_id
            """;
        command.Parameters.Add(new DuckDBParameter("national_team_id", nationalTeamId));

        using var reader = command.ExecuteReader();
        var players = new List<Wm26LineupPlayerRecord>();
        while (reader.Read())
        {
            players.Add(ReadPlayer(reader));
        }

        return players;
    }

    private static string GetCoachName(DuckDBConnection connection, string nationalTeamId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            select coach_name
            from national_teams
            where cast(national_team_id as varchar) = $national_team_id
            """;
        command.Parameters.Add(new DuckDBParameter("national_team_id", nationalTeamId));

        var value = command.ExecuteScalar();
        return value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static Wm26LineupPlayerRecord ReadPlayer(System.Data.Common.DbDataReader reader)
    {
        return new Wm26LineupPlayerRecord(
            Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            reader.IsDBNull(2) ? null : reader.GetValue(2),
            reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            reader.IsDBNull(4) ? null : reader.GetValue(4),
            reader.IsDBNull(5) ? string.Empty : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty);
    }

    private static string? ProvidedValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, MissingValue, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static string CalculateAgeOrMissing(object? dateOfBirth, DateOnly collectedAt)
    {
        if (dateOfBirth is null or DBNull)
        {
            return MissingValue;
        }

        DateOnly born;
        if (dateOfBirth is DateTime dateTime)
        {
            born = DateOnly.FromDateTime(dateTime);
        }
        else if (dateOfBirth is DateOnly dateOnly)
        {
            born = dateOnly;
        }
        else
        {
            var text = Convert.ToString(dateOfBirth, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(text)
                || !DateOnly.TryParseExact(
                    text[..Math.Min(text.Length, 10)],
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out born))
            {
                return MissingValue;
            }
        }

        var age = collectedAt.Year - born.Year;
        if (collectedAt.Month < born.Month || (collectedAt.Month == born.Month && collectedAt.Day < born.Day))
        {
            age--;
        }

        return age < 0 ? MissingValue : age.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatMarketValueOrMissing(object? value)
    {
        if (value is null or DBNull)
        {
            return MissingValue;
        }

        if (!long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var marketValue))
        {
            return MissingValue;
        }

        return marketValue == 0 ? MissingValue : marketValue.ToString(CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<Wm26GroupedLineupRows> GroupRowsByManifest(
        IReadOnlyList<Wm26LineupTeam> teams,
        IReadOnlyList<Wm26LineupOutputRow> rows)
    {
        var grouped = teams
            .Select(team => new Wm26GroupedLineupRows(team, []))
            .ToDictionary(entry => entry.Team.Slug, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!grouped.TryGetValue(row.TeamSlug, out var group))
            {
                group = new Wm26GroupedLineupRows(new Wm26LineupTeam(row.TeamSlug, row.Team), []);
                grouped[row.TeamSlug] = group;
            }

            group.Rows.Add(row);
        }

        return grouped.Values.ToList();
    }

    private static void ValidateCoaches(IReadOnlyList<Wm26GroupedLineupRows> groupedRows)
    {
        var teamsWithoutCoach = groupedRows
            .Where(group => group.Rows.Count > 0
                            && group.Rows.All(row => !string.Equals(row.Role, "Coach", StringComparison.Ordinal)))
            .Select(group => group.Team.Slug)
            .ToList();

        if (teamsWithoutCoach.Count > 0)
        {
            throw new InvalidOperationException(
                $"Lineup source has teams without Coach rows: {string.Join(", ", teamsWithoutCoach)}");
        }
    }

    private static IReadOnlyList<Wm26LineupMissingData> BuildMissingSourceData(
        IReadOnlyList<Wm26LineupOutputRow> rows)
    {
        return rows
            .Where(row => string.Equals(row.Role, "Player", StringComparison.Ordinal))
            .Select(row => new
            {
                Row = row,
                Fields = new[] { ("Age", row.Age), ("Position", row.Position), ("Market_Value_EUR", row.MarketValueEur) }
                    .Where(field => string.Equals(field.Item2, MissingValue, StringComparison.OrdinalIgnoreCase))
                    .Select(field => field.Item1)
                    .ToList()
            })
            .Where(entry => entry.Fields.Count > 0)
            .Select(entry => new Wm26LineupMissingData(
                entry.Row.TeamSlug,
                entry.Row.Team,
                entry.Row.Name,
                entry.Fields))
            .ToList();
    }

    private static string RenderCsv(IEnumerable<Wm26LineupOutputRow> rows)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(
            writer,
            new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                NewLine = "\r\n"
            });

        foreach (var column in OutputColumns)
        {
            csv.WriteField(column);
        }

        csv.NextRecord();

        foreach (var row in rows)
        {
            csv.WriteField(row.Team);
            csv.WriteField(row.DataCollectedAt);
            csv.WriteField(row.Role);
            csv.WriteField(row.Name);
            csv.WriteField(row.Age);
            csv.WriteField(row.Position);
            csv.WriteField(FormatMarketValueForOutput(row.MarketValueEur));
            csv.NextRecord();
        }

        return writer.ToString();
    }

    private static string FormatMarketValueForOutput(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, MissingValue, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var digits = value.Replace(".", string.Empty, StringComparison.Ordinal);
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var marketValue)
            ? marketValue.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".", StringComparison.Ordinal)
            : value;
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return NonAlphanumericRegex.Replace(builder.ToString(), " ").Trim();
    }

    private sealed record Wm26LineupSeedRow(
        string TeamSlug,
        string Team,
        string DataCollectedAt,
        string Role,
        string Name,
        string TransfermarktNationalTeamId,
        string TransfermarktPlayerId,
        string Age,
        string Position,
        string MarketValueEur);

    private sealed record Wm26LineupOutputRow(
        string TeamSlug,
        string Team,
        string DataCollectedAt,
        string Role,
        string Name,
        string Age,
        string Position,
        string MarketValueEur);

    private sealed record Wm26LineupPlayerRecord(
        string PlayerId,
        string Name,
        object? DateOfBirth,
        string Position,
        object? MarketValueInEur,
        string CurrentNationalTeamId);

    private sealed record Wm26GroupedLineupRows(Wm26LineupTeam Team, List<Wm26LineupOutputRow> Rows);
}

internal sealed class Wm26TransfermarktDuckDbProvider : IWm26TransfermarktDuckDbProvider
{
    public const string DefaultDuckDbUrl =
        "https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb";

    public const string DefaultCachePath = "data/wm26/lineups/private/data/transfermarkt-datasets.duckdb";

    private readonly HttpClient _httpClient;
    private readonly ILogger<Wm26TransfermarktDuckDbProvider> _logger;

    public Wm26TransfermarktDuckDbProvider(
        HttpClient httpClient,
        ILogger<Wm26TransfermarktDuckDbProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetDatabasePathAsync(
        string? configuredPath,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var path = Path.GetFullPath(configuredPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"DuckDB database not found: {path}", path);
            }

            return path;
        }

        var cachePath = Path.GetFullPath(DefaultCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var tempPath = $"{cachePath}.download";

        _logger.LogInformation("Refreshing Transfermarkt DuckDB snapshot from {Url}", DefaultDuckDbUrl);
        using var response = await _httpClient.GetAsync(DefaultDuckDbUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        File.Move(tempPath, cachePath, overwrite: true);
        return cachePath;
    }
}

public sealed record Wm26LineupSourceRequest(
    string SeedPath,
    string TeamsPath,
    string? DuckDbPath);

public sealed record Wm26LineupCollection(
    string SeedPath,
    string TeamsPath,
    string DuckDbPath,
    int SeedRowCount,
    int EnrichedRowCount,
    IReadOnlyList<Wm26LineupDocument> ContextDocuments,
    string KpiContent,
    IReadOnlyList<Wm26LineupTeam> HeaderOnlyTeams,
    IReadOnlyList<Wm26LineupMissingData> MissingSourceData);

public sealed record Wm26LineupDocument(
    string DocumentName,
    string Content,
    string TeamName,
    int PlayerCount,
    bool IsHeaderOnly);

public sealed record Wm26LineupTeam(string Slug, string Name);

public sealed record Wm26LineupMissingData(
    string TeamSlug,
    string TeamName,
    string PlayerName,
    IReadOnlyList<string> Fields);
