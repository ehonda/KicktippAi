using System.Globalization;
using CsvHelper;

namespace EHonda.KicktippAi.Core;

public sealed record HistoryDateMapEntry(
    string DocumentName,
    string Competition,
    string HomeTeam,
    string AwayTeam,
    string Score,
    string Annotation,
    string PlayedAt,
    string SourceName,
    string SourceUrl,
    string VerifiedAt,
    string Notes);

public sealed record HistoryDateMapApplyResult(
    string Content,
    int RowCount,
    int UpdatedRowCount,
    IReadOnlyList<HistoryDateMapEntry> MissingEntries);

/// <summary>
/// Utility class for handling Data_Collected_At column in history CSV documents.
/// </summary>
public static class HistoryCsvUtility
{
    public const string DataCollectedAtColumnName = "Data_Collected_At";

    private static readonly string[] DateMapHeaders =
    [
        "DocumentName",
        "Competition",
        "Home_Team",
        "Away_Team",
        "Score",
        "Annotation",
        "Played_At",
        "Source_Name",
        "Source_Url",
        "Verified_At",
        "Notes"
    ];

    /// <summary>
    /// Adds or updates the Data_Collected_At column in a history CSV document.
    /// </summary>
    /// <param name="csvContent">The original CSV content.</param>
    /// <param name="previousCsvContent">The previous version of the CSV content (null if this is the first version).</param>
    /// <param name="collectedDate">The date when the data was collected (e.g., "2025-08-30").</param>
    /// <returns>The updated CSV content with Data_Collected_At column.</returns>
    public static string AddDataCollectedAtColumn(string csvContent, string? previousCsvContent, string collectedDate)
    {
        // Check if the CSV already has Data_Collected_At column
        if (HasDataCollectedAtColumn(csvContent))
        {
            return csvContent; // Already has the column
        }
        
        // Extract matches from previous version to get their collection dates
        var previousMatches = previousCsvContent != null 
            ? ExtractMatchesWithCollectionDates(previousCsvContent) 
            : new Dictionary<string, string>();
        
        // Extract current matches
        var currentMatches = ExtractMatches(csvContent);
        
        // Build the new CSV with Data_Collected_At column
        return BuildCsvWithDataCollectedAt(csvContent, currentMatches, previousMatches, collectedDate);
    }

    public static IReadOnlyList<HistoryDateMapEntry> ReadDateMapEntries(string csvContent)
    {
        var entries = new List<HistoryDateMapEntry>();
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return entries.AsReadOnly();
        }

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        try
        {
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                entries.Add(new HistoryDateMapEntry(
                    GetOptionalField(csv, "DocumentName"),
                    GetOptionalField(csv, "Competition"),
                    GetOptionalField(csv, "Home_Team"),
                    GetOptionalField(csv, "Away_Team"),
                    GetOptionalField(csv, "Score"),
                    GetOptionalField(csv, "Annotation"),
                    GetOptionalField(csv, "Played_At"),
                    GetOptionalField(csv, "Source_Name"),
                    GetOptionalField(csv, "Source_Url"),
                    GetOptionalField(csv, "Verified_At"),
                    GetOptionalField(csv, "Notes")));
            }
        }
        catch (Exception)
        {
            return Array.Empty<HistoryDateMapEntry>();
        }

        return entries.AsReadOnly();
    }

    public static string WriteDateMapEntries(IEnumerable<HistoryDateMapEntry> entries)
    {
        using var writer = new StringWriter();
        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in DateMapHeaders)
        {
            csvWriter.WriteField(header);
        }

        csvWriter.NextRecord();

        foreach (var entry in entries)
        {
            csvWriter.WriteField(entry.DocumentName);
            csvWriter.WriteField(entry.Competition);
            csvWriter.WriteField(entry.HomeTeam);
            csvWriter.WriteField(entry.AwayTeam);
            csvWriter.WriteField(entry.Score);
            csvWriter.WriteField(entry.Annotation);
            csvWriter.WriteField(entry.PlayedAt);
            csvWriter.WriteField(entry.SourceName);
            csvWriter.WriteField(entry.SourceUrl);
            csvWriter.WriteField(entry.VerifiedAt);
            csvWriter.WriteField(entry.Notes);
            csvWriter.NextRecord();
        }

        return writer.ToString();
    }

    public static IReadOnlyList<HistoryDateMapEntry> ExtractDateMapEntries(
        string documentName,
        string csvContent,
        bool includeExistingDataCollectedAt = false)
    {
        var entries = new List<HistoryDateMapEntry>();
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return entries.AsReadOnly();
        }

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        try
        {
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var playedAt = includeExistingDataCollectedAt
                    ? GetOptionalField(csv, DataCollectedAtColumnName)
                    : "";

                entries.Add(new HistoryDateMapEntry(
                    documentName,
                    GetOptionalField(csv, "Competition"),
                    GetOptionalField(csv, "Home_Team"),
                    GetOptionalField(csv, "Away_Team"),
                    GetOptionalField(csv, "Score"),
                    GetOptionalField(csv, "Annotation"),
                    playedAt,
                    SourceName: "",
                    SourceUrl: "",
                    VerifiedAt: "",
                    Notes: ""));
            }
        }
        catch (Exception)
        {
            return Array.Empty<HistoryDateMapEntry>();
        }

        return entries.AsReadOnly();
    }

    public static HistoryDateMapApplyResult ApplyDateMap(
        string documentName,
        string csvContent,
        IReadOnlyList<HistoryDateMapEntry> dateMapEntries)
    {
        var dateMap = dateMapEntries
            .Where(entry => string.Equals(entry.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))
            .GroupBy(CreateDateMapKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var missingEntries = new List<HistoryDateMapEntry>();
        var rows = new List<HistoryDateMapEntry>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new HistoryDateMapApplyResult(csvContent, RowCount: 0, UpdatedRowCount: 0, missingEntries);
        }

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        try
        {
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var row = new HistoryDateMapEntry(
                    documentName,
                    GetOptionalField(csv, "Competition"),
                    GetOptionalField(csv, "Home_Team"),
                    GetOptionalField(csv, "Away_Team"),
                    GetOptionalField(csv, "Score"),
                    GetOptionalField(csv, "Annotation"),
                    PlayedAt: "",
                    SourceName: "",
                    SourceUrl: "",
                    VerifiedAt: "",
                    Notes: "");

                if (!dateMap.TryGetValue(CreateDateMapKey(row), out var dateMapEntry) ||
                    !IsExactDate(dateMapEntry.PlayedAt))
                {
                    missingEntries.Add(dateMapEntry ?? row);
                    rows.Add(row);
                    continue;
                }

                rows.Add(row with { PlayedAt = dateMapEntry.PlayedAt.Trim() });
            }
        }
        catch (Exception)
        {
            return new HistoryDateMapApplyResult(csvContent, RowCount: 0, UpdatedRowCount: 0, missingEntries);
        }

        if (missingEntries.Count > 0)
        {
            return new HistoryDateMapApplyResult(csvContent, rows.Count, UpdatedRowCount: 0, missingEntries);
        }

        using var writer = new StringWriter();
        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csvWriter.WriteField("Competition");
        csvWriter.WriteField(DataCollectedAtColumnName);
        csvWriter.WriteField("Home_Team");
        csvWriter.WriteField("Away_Team");
        csvWriter.WriteField("Score");
        csvWriter.WriteField("Annotation");
        csvWriter.NextRecord();

        foreach (var row in rows)
        {
            csvWriter.WriteField(row.Competition);
            csvWriter.WriteField(row.PlayedAt);
            csvWriter.WriteField(row.HomeTeam);
            csvWriter.WriteField(row.AwayTeam);
            csvWriter.WriteField(row.Score);
            csvWriter.WriteField(row.Annotation);
            csvWriter.NextRecord();
        }

        return new HistoryDateMapApplyResult(writer.ToString(), rows.Count, rows.Count, missingEntries);
    }

    /// <summary>
    /// Checks if the CSV content already has a Data_Collected_At column.
    /// </summary>
    private static bool HasDataCollectedAtColumn(string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }
        
        var header = lines[0];
        return header.Contains(DataCollectedAtColumnName, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Extracts matches from CSV content without Data_Collected_At.
    /// </summary>
    private static HashSet<string> ExtractMatches(string csvContent)
    {
        var matches = new HashSet<string>();
        
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        try
        {
            csv.Read();
            csv.ReadHeader();
            
            while (csv.Read())
            {
                var competition = csv.GetField("Competition") ?? "";
                var homeTeam = csv.GetField("Home_Team") ?? "";
                var awayTeam = csv.GetField("Away_Team") ?? "";
                var score = csv.GetField("Score") ?? "";
                var annotation = (csv.TryGetField<string>("Annotation", out var ann) ? ann : null) ?? "";
                
                var matchKey = CreateMatchKey(competition, homeTeam, awayTeam, score, annotation);
                matches.Add(matchKey);
            }
        }
        catch (Exception)
        {
            // If CSV parsing fails, return empty set
        }
        
        return matches;
    }
    
    /// <summary>
    /// Extracts matches with their collection dates from CSV content that has Data_Collected_At.
    /// </summary>
    private static Dictionary<string, string> ExtractMatchesWithCollectionDates(string csvContent)
    {
        var matches = new Dictionary<string, string>();
        
        if (!HasDataCollectedAtColumn(csvContent))
        {
            return matches;
        }
        
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        try
        {
            csv.Read();
            csv.ReadHeader();
            
            while (csv.Read())
            {
                var competition = csv.GetField("Competition") ?? "";
                var dataCollectedAt = csv.GetField(DataCollectedAtColumnName) ?? "";
                var homeTeam = csv.GetField("Home_Team") ?? "";
                var awayTeam = csv.GetField("Away_Team") ?? "";
                var score = csv.GetField("Score") ?? "";
                var annotation = (csv.TryGetField<string>("Annotation", out var ann) ? ann : null) ?? "";
                
                var matchKey = CreateMatchKey(competition, homeTeam, awayTeam, score, annotation);
                matches[matchKey] = dataCollectedAt;
            }
        }
        catch (Exception)
        {
            // If CSV parsing fails, return empty dictionary
        }
        
        return matches;
    }
    
    /// <summary>
    /// Builds a new CSV with the Data_Collected_At column.
    /// </summary>
    private static string BuildCsvWithDataCollectedAt(
        string originalCsvContent, 
        HashSet<string> currentMatches, 
        Dictionary<string, string> previousMatches, 
        string collectedDate)
    {
        using var reader = new StringReader(originalCsvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        using var writer = new StringWriter();
        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        try
        {
            csv.Read();
            csv.ReadHeader();
            
            // Write new header with Data_Collected_At after Competition
            csvWriter.WriteField("Competition");
            csvWriter.WriteField(DataCollectedAtColumnName);
            csvWriter.WriteField("Home_Team");
            csvWriter.WriteField("Away_Team");
            csvWriter.WriteField("Score");
            csvWriter.WriteField("Annotation");
            csvWriter.NextRecord();
            
            while (csv.Read())
            {
                var competition = csv.GetField("Competition") ?? "";
                var homeTeam = csv.GetField("Home_Team") ?? "";
                var awayTeam = csv.GetField("Away_Team") ?? "";
                var score = csv.GetField("Score") ?? "";
                var annotation = (csv.TryGetField<string>("Annotation", out var ann) ? ann : null) ?? "";
                
                var matchKey = CreateMatchKey(competition, homeTeam, awayTeam, score, annotation);
                
                // Determine the collection date for this match
                string dataCollectedAt;
                if (previousMatches.TryGetValue(matchKey, out var existingDate))
                {
                    // Match existed in previous version, use its existing date
                    dataCollectedAt = existingDate;
                }
                else
                {
                    // New match, use current collection date
                    dataCollectedAt = collectedDate;
                }
                
                csvWriter.WriteField(competition);
                csvWriter.WriteField(dataCollectedAt);
                csvWriter.WriteField(homeTeam);
                csvWriter.WriteField(awayTeam);
                csvWriter.WriteField(score);
                csvWriter.WriteField(annotation);
                csvWriter.NextRecord();
            }
        }
        catch (Exception)
        {
            // If parsing fails, return original content
            return originalCsvContent;
        }
        
        return writer.ToString();
    }
    
    /// <summary>
    /// Creates a unique key for a match.
    /// </summary>
    private static string CreateMatchKey(string competition, string homeTeam, string awayTeam, string score, string annotation)
    {
        return $"{competition}|{homeTeam}|{awayTeam}|{score}|{annotation}";
    }

    private static string CreateDateMapKey(HistoryDateMapEntry entry)
    {
        return CreateMatchKey(
            NormalizeKeyPart(entry.Competition),
            NormalizeKeyPart(entry.HomeTeam),
            NormalizeKeyPart(entry.AwayTeam),
            NormalizeKeyPart(entry.Score),
            NormalizeKeyPart(entry.Annotation));
    }

    private static string NormalizeKeyPart(string? value)
    {
        return value?.Trim() ?? "";
    }

    private static bool IsExactDate(string value)
    {
        return DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string GetOptionalField(CsvReader csv, string fieldName)
    {
        return (csv.TryGetField<string>(fieldName, out var value) ? value : null) ?? "";
    }
}
