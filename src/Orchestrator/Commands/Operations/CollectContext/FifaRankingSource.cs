using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CsvHelper;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Commands.Operations.CollectContext;

public interface IFifaRankingSource
{
    Task<FifaRankingCollection> CollectLatestAsync(
        DateOnly collectionDate,
        CancellationToken cancellationToken = default);
}

internal interface IFifaRankingApiClient
{
    Task<IReadOnlyList<FifaRankingScheduleDto>> GetRankingSchedulesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FifaRankingRowDto>> GetRankingRowsAsync(
        string rankingScheduleId,
        CancellationToken cancellationToken = default);
}

internal sealed class FifaRankingSource : IFifaRankingSource
{
    private const int MinimumExpectedRankingRows = 200;

    private static readonly IReadOnlyList<Wm26FifaTeam> Wm26Teams =
    [
        new("EGY", "Ägypten", "agypten"),
        new("ALG", "Algerien", "algerien"),
        new("ARG", "Argentinien", "argentinien"),
        new("AUS", "Australien", "australien"),
        new("BEL", "Belgien", "belgien"),
        new("BIH", "Bosnien-Herzegowina", "bosnien-herzegowina"),
        new("BRA", "Brasilien", "brasilien"),
        new("CUW", "Curaçao", "curacao"),
        new("GER", "Deutschland", "deutschland"),
        new("COD", "DR Kongo", "dr-kongo"),
        new("ECU", "Ecuador", "ecuador"),
        new("CIV", "Elfenbeinküste", "elfenbeinkuste"),
        new("ENG", "England", "england"),
        new("FRA", "Frankreich", "frankreich"),
        new("GHA", "Ghana", "ghana"),
        new("HAI", "Haiti", "haiti"),
        new("IRQ", "Irak", "irak"),
        new("IRN", "Iran", "iran"),
        new("JPN", "Japan", "japan"),
        new("JOR", "Jordanien", "jordanien"),
        new("CAN", "Kanada", "kanada"),
        new("CPV", "Kap Verde", "kap-verde"),
        new("QAT", "Katar", "katar"),
        new("COL", "Kolumbien", "kolumbien"),
        new("CRO", "Kroatien", "kroatien"),
        new("MAR", "Marokko", "marokko"),
        new("MEX", "Mexiko", "mexiko"),
        new("NZL", "Neuseeland", "neuseeland"),
        new("NED", "Niederlande", "niederlande"),
        new("NOR", "Norwegen", "norwegen"),
        new("AUT", "Österreich", "osterreich"),
        new("PAN", "Panama", "panama"),
        new("PAR", "Paraguay", "paraguay"),
        new("POR", "Portugal", "portugal"),
        new("KSA", "Saudi-Arabien", "saudi-arabien"),
        new("SCO", "Schottland", "schottland"),
        new("SWE", "Schweden", "schweden"),
        new("SUI", "Schweiz", "schweiz"),
        new("SEN", "Senegal", "senegal"),
        new("ESP", "Spanien", "spanien"),
        new("RSA", "Südafrika", "sudafrika"),
        new("KOR", "Südkorea", "sudkorea"),
        new("CZE", "Tschechien", "tschechien"),
        new("TUN", "Tunesien", "tunesien"),
        new("TUR", "Türkei", "turkei"),
        new("URU", "Uruguay", "uruguay"),
        new("USA", "USA", "usa"),
        new("UZB", "Usbekistan", "usbekistan")
    ];

    private readonly IFifaRankingApiClient _apiClient;

    public FifaRankingSource(IFifaRankingApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<FifaRankingCollection> CollectLatestAsync(
        DateOnly collectionDate,
        CancellationToken cancellationToken = default)
    {
        var schedules = await _apiClient.GetRankingSchedulesAsync(cancellationToken);
        var latestSchedule = SelectLatestApprovedSchedule(schedules);
        var rows = await _apiClient.GetRankingRowsAsync(latestSchedule.Id, cancellationToken);

        if (rows.Count < MinimumExpectedRankingRows)
        {
            throw new InvalidOperationException(
                $"FIFA ranking response returned {rows.Count} rows; expected at least {MinimumExpectedRankingRows}.");
        }

        var rowsByCountry = BuildRankingRowLookup(rows);
        var rankingEntries = BuildRankingEntries(rowsByCountry, latestSchedule.PublicationDateUtc);
        var contextDocuments = rankingEntries
            .OrderBy(entry => entry.Team.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new FifaRankingDocument(
                $"fifa-ranking-{entry.Team.Slug}.csv",
                WriteRankingCsv([entry]),
                entry.Team.DisplayName,
                entry.Rank,
                entry.Points))
            .ToList();

        var kpiContent = WriteRankingCsv(
            rankingEntries
                .OrderBy(entry => entry.Rank)
                .ThenBy(entry => entry.Team.DisplayName, StringComparer.Ordinal));

        return new FifaRankingCollection(
            latestSchedule.Id,
            latestSchedule.PublicationDateUtc,
            collectionDate,
            rows.Count,
            contextDocuments,
            kpiContent);
    }

    private static SelectedFifaRankingSchedule SelectLatestApprovedSchedule(
        IReadOnlyList<FifaRankingScheduleDto> schedules)
    {
        var candidates = schedules
            .Where(schedule => schedule.RankingApproved == true)
            .Select(schedule => new
            {
                Schedule = schedule,
                ParsedPublicationDate = TryParsePublicationDate(schedule.PublicationDateUTC)
            })
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Schedule.IdRankingSchedule)
                                && candidate.ParsedPublicationDate.HasValue)
            .OrderByDescending(candidate => candidate.ParsedPublicationDate!.Value)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "No approved FIFA ranking schedule with a publication date was found.");
        }

        var latest = candidates[0];
        return new SelectedFifaRankingSchedule(
            latest.Schedule.IdRankingSchedule!.Trim(),
            latest.ParsedPublicationDate!.Value);
    }

    private static DateTimeOffset? TryParsePublicationDate(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyDictionary<string, FifaRankingRowDto> BuildRankingRowLookup(
        IReadOnlyList<FifaRankingRowDto> rows)
    {
        var duplicateCountryCodes = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.IdCountry))
            .GroupBy(row => row.IdCountry!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateCountryCodes.Count > 0)
        {
            throw new InvalidOperationException(
                $"FIFA ranking response contains duplicate country codes: {string.Join(", ", duplicateCountryCodes)}.");
        }

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.IdCountry))
            .ToDictionary(row => row.IdCountry!.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FifaRankingEntry> BuildRankingEntries(
        IReadOnlyDictionary<string, FifaRankingRowDto> rowsByCountry,
        DateTimeOffset publicationDateUtc)
    {
        var entries = new List<FifaRankingEntry>();
        var missingTeamCodes = new List<string>();
        var invalidRows = new List<string>();

        foreach (var team in Wm26Teams)
        {
            if (!rowsByCountry.TryGetValue(team.IdCountry, out var row))
            {
                missingTeamCodes.Add($"{team.IdCountry} ({team.DisplayName})");
                continue;
            }

            if (row.Rank is null or <= 0 || row.TotalPoints is null)
            {
                invalidRows.Add($"{team.IdCountry} ({team.DisplayName})");
                continue;
            }

            entries.Add(new FifaRankingEntry(
                team,
                row.Rank.Value,
                decimal.Round(row.TotalPoints.Value, 2, MidpointRounding.AwayFromZero),
                publicationDateUtc));
        }

        if (missingTeamCodes.Count > 0)
        {
            throw new InvalidOperationException(
                $"FIFA ranking response is missing WM26 teams: {string.Join(", ", missingTeamCodes)}.");
        }

        if (invalidRows.Count > 0)
        {
            throw new InvalidOperationException(
                $"FIFA ranking response has invalid rank or points for WM26 teams: {string.Join(", ", invalidRows)}.");
        }

        return entries;
    }

    private static string WriteRankingCsv(IEnumerable<FifaRankingEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Rank,Team,ELO,{FifaRankingCsvUtility.PublishedAtColumnName}");

        foreach (var entry in entries)
        {
            AppendCsvField(builder, entry.Rank.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendCsvField(builder, entry.Team.DisplayName);
            builder.Append(',');
            AppendCsvField(builder, entry.Points.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendCsvField(builder, entry.PublishedAt.ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendCsvField(StringBuilder builder, string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\"", StringComparison.Ordinal));
        builder.Append('"');
    }

    private sealed record Wm26FifaTeam(string IdCountry, string DisplayName, string Slug);

    private sealed record SelectedFifaRankingSchedule(string Id, DateTimeOffset PublicationDateUtc);

    private sealed record FifaRankingEntry(
        Wm26FifaTeam Team,
        int Rank,
        decimal Points,
        DateTimeOffset PublishedAt);
}

internal static class FifaRankingCsvUtility
{
    internal const string PublishedAtColumnName = "Published_At";

    internal static string PreserveExistingContentWhenRankingUnchanged(string newContent, string? existingContent)
    {
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return newContent;
        }

        if (!TryParseRows(newContent, requirePublishedAtHeader: true, out var newRows) ||
            !TryParseRows(existingContent, requirePublishedAtHeader: true, out var existingRows))
        {
            return newContent;
        }

        if (newRows.Count != existingRows.Count)
        {
            return newContent;
        }

        var existingByTeam = existingRows.ToDictionary(row => row.Team, StringComparer.Ordinal);
        foreach (var newRow in newRows)
        {
            if (!existingByTeam.TryGetValue(newRow.Team, out var existingRow) ||
                existingRow.Rank != newRow.Rank ||
                existingRow.Elo != newRow.Elo)
            {
                return newContent;
            }
        }

        return existingContent;
    }

    private static bool TryParseRows(
        string content,
        bool requirePublishedAtHeader,
        out IReadOnlyList<FifaRankingCsvRow> rows)
    {
        rows = [];

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var reader = new StringReader(content);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            if (!csv.Read())
            {
                return false;
            }

            csv.ReadHeader();
            var header = csv.HeaderRecord ?? [];

            if (!header.Contains("Rank", StringComparer.Ordinal) ||
                !header.Contains("Team", StringComparer.Ordinal) ||
                !header.Contains("ELO", StringComparer.Ordinal) ||
                (requirePublishedAtHeader && !header.Contains(PublishedAtColumnName, StringComparer.Ordinal)))
            {
                return false;
            }

            var parsedRows = new List<FifaRankingCsvRow>();
            while (csv.Read())
            {
                var team = csv.GetField("Team");
                var rankText = csv.GetField("Rank");
                var eloText = csv.GetField("ELO");

                if (string.IsNullOrWhiteSpace(team) &&
                    string.IsNullOrWhiteSpace(rankText) &&
                    string.IsNullOrWhiteSpace(eloText))
                {
                    continue;
                }

                if (!int.TryParse(rankText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ||
                    !decimal.TryParse(eloText, NumberStyles.Number, CultureInfo.InvariantCulture, out var elo))
                {
                    return false;
                }

                parsedRows.Add(new FifaRankingCsvRow(team ?? string.Empty, rank, elo));
            }

            rows = parsedRows;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record FifaRankingCsvRow(string Team, int Rank, decimal Elo);
}

internal sealed class FifaRankingApiClient : IFifaRankingApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<FifaRankingApiClient> _logger;

    public FifaRankingApiClient(HttpClient httpClient, ILogger<FifaRankingApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FifaRankingScheduleDto>> GetRankingSchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetResultsAsync<FifaRankingScheduleDto>(
            "fifarankings/rankingschedules/all?type=0&gender=1&sportType=0&language=de",
            cancellationToken);
    }

    public async Task<IReadOnlyList<FifaRankingRowDto>> GetRankingRowsAsync(
        string rankingScheduleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rankingScheduleId);

        var path =
            $"fifarankings/rankings/rankingsbyschedule?rankingScheduleId={Uri.EscapeDataString(rankingScheduleId)}&count=300&language=de";
        return await GetResultsAsync<FifaRankingRowDto>(path, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetResultsAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(relativePath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "FIFA ranking API request failed with {StatusCode}: {ResponseBody}",
                response.StatusCode,
                responseBody);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<FifaApiResponse<T>>(
            SerializerOptions,
            cancellationToken);

        return payload?.Results ?? [];
    }
}

public sealed record FifaRankingCollection(
    string ScheduleId,
    DateTimeOffset PublicationDateUtc,
    DateOnly CollectionDate,
    int SourceRowCount,
    IReadOnlyList<FifaRankingDocument> ContextDocuments,
    string KpiContent)
{
    public int MappedTeamCount => ContextDocuments.Count;
}

public sealed record FifaRankingDocument(
    string DocumentName,
    string Content,
    string TeamName,
    int Rank,
    decimal Points);

internal sealed record FifaApiResponse<T>
{
    public List<T>? Results { get; init; }
}

internal sealed record FifaRankingScheduleDto
{
    public string? IdRankingSchedule { get; init; }

    public bool? RankingApproved { get; init; }

    public string? PublicationDateUTC { get; init; }
}

internal sealed record FifaRankingRowDto
{
    public string? IdCountry { get; init; }

    public int? Rank { get; init; }

    public decimal? TotalPoints { get; init; }
}
