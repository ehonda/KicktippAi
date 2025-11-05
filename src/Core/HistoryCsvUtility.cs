using System.Globalization;
using CsvHelper;

namespace Core;

/// <summary>
/// Utility class for handling Data_Collected_At column in history CSV documents.
/// </summary>
public static class HistoryCsvUtility
{
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
        return header.Contains("Data_Collected_At", StringComparison.OrdinalIgnoreCase);
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
                var dataCollectedAt = csv.GetField("Data_Collected_At") ?? "";
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
            csvWriter.WriteField("Data_Collected_At");
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
}
