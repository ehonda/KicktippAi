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
}
