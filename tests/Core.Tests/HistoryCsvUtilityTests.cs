using EHonda.KicktippAi.Core;
using TestUtilities.StringAssertions;

namespace Core.Tests;

/// <summary>
/// Tests for <see cref="HistoryCsvUtility.AddDataCollectedAtColumn"/> method.
/// </summary>
public class HistoryCsvUtilityTests
{
    [Test]
    public async Task Adding_data_collected_at_to_new_csv_adds_column_with_current_date()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).IsEqualToWithNormalizedLineEndings(
            "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\r\nBundesliga,2025-01-10,Bayern,Leipzig,2-1,\r\n");
    }

    [Test]
    public async Task Adding_data_collected_at_returns_original_if_column_already_exists()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).IsEqualTo(csvContent);
    }

    [Test]
    public async Task Adding_data_collected_at_preserves_existing_match_dates_from_previous_version()
    {
        var previousCsvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,\nBundesliga,Bayern,Mainz,3-0,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent, collectedDate);

        // Existing match keeps its original date (2025-01-01), new match gets current date (2025-01-10)
        await Assert.That(result).Contains("2025-01-01"); // Preserved date
        await Assert.That(result).Contains("2025-01-10"); // New match date
    }

    [Test]
    public async Task Adding_data_collected_at_preserves_existing_played_at_dates_from_previous_version()
    {
        var previousCsvContent = "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,\nBundesliga,Bayern,Mainz,3-0,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent, collectedDate);

        await Assert.That(result).Contains("2025-01-01");
        await Assert.That(result).Contains("2025-01-10");
    }

    [Test]
    public async Task Adding_data_collected_at_handles_multiple_rows()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,\nBundesliga,Dortmund,Mainz,3-0,\nBundesliga,Bremen,Hamburg,1-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).Contains("Data_Collected_At");
        // All rows should have the same date since there's no previous version
        var dateCount = result.Split("2025-01-10").Length - 1;
        await Assert.That(dateCount).IsEqualTo(3);
    }

    [Test]
    public async Task Adding_data_collected_at_handles_empty_annotation_field()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).Contains("Data_Collected_At");
        await Assert.That(result).Contains("2025-01-10");
    }

    [Test]
    public async Task Adding_data_collected_at_handles_annotation_with_value()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,Derby";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).Contains("Data_Collected_At");
        await Assert.That(result).Contains("Derby");
    }

    [Test]
    public async Task Adding_data_collected_at_handles_case_insensitive_column_detection()
    {
        var csvContent = "Competition,data_collected_at,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        // Should return original since column exists (case-insensitive check)
        await Assert.That(result).IsEqualTo(csvContent);
    }

    [Test]
    public async Task Adding_data_collected_at_with_previous_version_without_column_treats_as_new()
    {
        var previousCsvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,";
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,\nBundesliga,Bayern,Mainz,3-0,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent, collectedDate);

        // Since previous version doesn't have Data_Collected_At, all rows get current date
        await Assert.That(result).Contains("Data_Collected_At");
        var dateCount = result.Split("2025-01-10").Length - 1;
        await Assert.That(dateCount).IsEqualTo(2);
    }

    [Test]
    public async Task Adding_data_collected_at_handles_empty_csv_content()
    {
        var csvContent = "";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).IsEqualTo(csvContent);
    }

    [Test]
    public async Task Adding_data_collected_at_handles_header_only_csv()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent: null, collectedDate);

        await Assert.That(result).Contains("Data_Collected_At");
    }

    [Test]
    public async Task Adding_data_collected_at_uses_match_key_for_deduplication()
    {
        // Same match (same competition, teams, score, annotation) should preserve date
        var previousCsvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent, collectedDate);

        await Assert.That(result).Contains("2025-01-01"); // Preserved from previous
        await Assert.That(result).DoesNotContain("2025-01-10"); // Not the new date
    }

    [Test]
    public async Task Adding_data_collected_at_treats_different_scores_as_different_matches()
    {
        // Same teams but different score = different match (gets new date)
        var previousCsvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nBundesliga,2025-01-01,Bayern,Leipzig,2-1,";
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,3-0,";
        var collectedDate = "2025-01-10";

        var result = HistoryCsvUtility.AddDataCollectedAtColumn(csvContent, previousCsvContent, collectedDate);

        await Assert.That(result).Contains("2025-01-10"); // New date for different score
        await Assert.That(result).DoesNotContain("2025-01-01"); // Old date not present
    }

    [Test]
    public async Task Applying_date_map_writes_played_at_with_played_dates()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-germany.csv",
                "KL-WM",
                "Germany",
                "Slovakia",
                "6:0",
                "",
                "2025-11-17",
                "DFB",
                "https://example.test/match",
                "2026-05-23",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap("recent-history-germany.csv", csvContent, dateMap);

        await Assert.That(result.MissingEntries).IsEmpty();
        await Assert.That(result.Content).IsEqualToWithNormalizedLineEndings(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\nKL-WM,2025-11-17,Germany,Slovakia,6:0,\r\n");
    }

    [Test]
    public async Task Applying_date_map_migrates_existing_data_collected_at_values_to_played_at()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nKL-WM,2026-05-23,Germany,Slovakia,6:0,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-germany.csv",
                "KL-WM",
                "Germany",
                "Slovakia",
                "6:0",
                "",
                "2025-11-17",
                "DFB",
                "https://example.test/match",
                "2026-05-23",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap("recent-history-germany.csv", csvContent, dateMap);

        await Assert.That(result.MissingEntries).IsEmpty();
        await Assert.That(result.Content).Contains("Played_At");
        await Assert.That(result.Content).DoesNotContain("Data_Collected_At");
        await Assert.That(result.Content).Contains("2025-11-17");
        await Assert.That(result.Content).DoesNotContain("2026-05-23");
    }

    [Test]
    public async Task Applying_date_map_uses_duplicate_entries_in_row_order()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nCopAm,Argentina,Canada,2:0,\nCopAm,Argentina,Canada,2:0,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-canada.csv",
                "CopAm",
                "Argentina",
                "Canada",
                "2:0",
                "",
                "2024-07-09",
                "CONMEBOL",
                "https://example.test/semifinal",
                "2026-05-24",
                ""),
            new HistoryDateMapEntry(
                "recent-history-canada.csv",
                "CopAm",
                "Argentina",
                "Canada",
                "2:0",
                "",
                "2024-06-20",
                "CONMEBOL",
                "https://example.test/group",
                "2026-05-24",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap("recent-history-canada.csv", csvContent, dateMap);

        await Assert.That(result.MissingEntries).IsEmpty();
        await Assert.That(result.Content).IsEqualToWithNormalizedLineEndings(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\nCopAm,2024-07-09,Argentina,Canada,2:0,\r\nCopAm,2024-06-20,Argentina,Canada,2:0,\r\n");
    }

    [Test]
    public async Task Applying_date_map_replaces_cutoff_collection_date_with_prediction_timestamp()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nWM,2026-06-11,Mexiko,Südafrika,1:1,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-mexiko.csv",
                "WM",
                "Mexiko",
                "Südafrika",
                "1:1",
                "",
                "2010-06-11",
                "FIFA",
                "https://example.test/match",
                "2026-05-23",
                "")
        };
        var predictionEntries = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-mexiko.csv",
                "WM",
                "Mexiko",
                "Südafrika",
                "1:1",
                "",
                "2026-06-11T21:00:00+02:00",
                "",
                "",
                "",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap(
            "recent-history-mexiko.csv",
            csvContent,
            dateMap,
            new HistoryDateMapApplyOptions(
                ApplyKnownOnly: true,
                PreserveCollectedOnOrAfter: new DateOnly(2026, 6, 11),
                PredictionDateEntries: predictionEntries));

        await Assert.That(result.MissingPredictionEntries).IsEmpty();
        await Assert.That(result.UpdatedRowCount).IsEqualTo(1);
        await Assert.That(result.Content).IsEqualToWithNormalizedLineEndings(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\nWM,2026-06-11T21:00:00+02:00,Mexiko,Südafrika,1:1,\r\n");
    }

    [Test]
    public async Task Applying_date_map_reports_missing_prediction_for_cutoff_collection_date()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nWM,2026-06-11,Mexiko,Südafrika,1:1,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-mexiko.csv",
                "WM",
                "Mexiko",
                "Südafrika",
                "1:1",
                "",
                "2010-06-11",
                "FIFA",
                "https://example.test/match",
                "2026-05-23",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap(
            "recent-history-mexiko.csv",
            csvContent,
            dateMap,
            new HistoryDateMapApplyOptions(
                ApplyKnownOnly: true,
                PreserveCollectedOnOrAfter: new DateOnly(2026, 6, 11)));

        await Assert.That(result.MissingPredictionEntries.Count).IsEqualTo(1);
        await Assert.That(result.Content).IsEqualTo(csvContent);
    }

    [Test]
    public async Task Applying_date_map_preserves_existing_cutoff_timestamp()
    {
        var csvContent = "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\nWM,2026-06-11T21:00:00+02:00,Mexiko,Südafrika,1:1,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-mexiko.csv",
                "WM",
                "Mexiko",
                "Südafrika",
                "1:1",
                "",
                "2010-06-11",
                "FIFA",
                "https://example.test/match",
                "2026-05-23",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap(
            "recent-history-mexiko.csv",
            csvContent,
            dateMap,
            new HistoryDateMapApplyOptions(
                ApplyKnownOnly: true,
                PreserveCollectedOnOrAfter: new DateOnly(2026, 6, 11)));

        await Assert.That(result.MissingPredictionEntries).IsEmpty();
        await Assert.That(result.PreservedRowCount).IsEqualTo(1);
        await Assert.That(result.Content).IsEqualToWithNormalizedLineEndings(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\nWM,2026-06-11T21:00:00+02:00,Mexiko,Südafrika,1:1,\r\n");
    }

    [Test]
    public async Task Applying_date_map_uses_canonical_map_for_pre_cutoff_rows()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nWM,2026-06-10,Mexiko,Südafrika,1:1,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-mexiko.csv",
                "WM",
                "Mexiko",
                "Südafrika",
                "1:1",
                "",
                "2010-06-11",
                "FIFA",
                "https://example.test/match",
                "2026-05-23",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap(
            "recent-history-mexiko.csv",
            csvContent,
            dateMap,
            new HistoryDateMapApplyOptions(
                ApplyKnownOnly: true,
                PreserveCollectedOnOrAfter: new DateOnly(2026, 6, 11)));

        await Assert.That(result.MissingPredictionEntries).IsEmpty();
        await Assert.That(result.Content).Contains("WM,2010-06-11,Mexiko,Südafrika,1:1,");
    }

    [Test]
    public async Task Applying_date_map_uses_canonical_map_for_post_cutoff_non_wm_rows()
    {
        var csvContent = "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\nAfCup,2026-06-14,Ägypten,Nigeria,2:4,nach Elfmeterschießen";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-agypten.csv",
                "AfCup",
                "Ägypten",
                "Nigeria",
                "2:4",
                "nach Elfmeterschießen",
                "2026-01-17",
                "Sportsgambler",
                "https://example.test/match",
                "2026-05-24",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap(
            "recent-history-agypten.csv",
            csvContent,
            dateMap,
            new HistoryDateMapApplyOptions(
                ApplyKnownOnly: true,
                PreserveCollectedOnOrAfter: new DateOnly(2026, 6, 11)));

        await Assert.That(result.MissingPredictionEntries).IsEmpty();
        await Assert.That(result.UpdatedRowCount).IsEqualTo(1);
        await Assert.That(result.Content).IsEqualToWithNormalizedLineEndings(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\nAfCup,2026-01-17,Ägypten,Nigeria,2:4,nach Elfmeterschießen\r\n");
    }

    [Test]
    public async Task Applying_date_map_reports_missing_entries_when_played_at_is_absent()
    {
        var csvContent = "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,";
        var dateMap = new[]
        {
            new HistoryDateMapEntry(
                "recent-history-germany.csv",
                "KL-WM",
                "Germany",
                "Slovakia",
                "6:0",
                "",
                "",
                "",
                "",
                "",
                "")
        };

        var result = HistoryCsvUtility.ApplyDateMap("recent-history-germany.csv", csvContent, dateMap);

        await Assert.That(result.MissingEntries.Count).IsEqualTo(1);
        await Assert.That(result.Content).IsEqualTo(csvContent);
    }
}
