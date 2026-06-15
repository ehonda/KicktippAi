using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class FifaRankingSourceTests
{
    private static readonly string[] RequiredCountryCodes =
    [
        "EGY", "ALG", "ARG", "AUS", "BEL", "BIH", "BRA", "CUW",
        "GER", "COD", "ECU", "CIV", "ENG", "FRA", "GHA", "HAI",
        "IRQ", "IRN", "JPN", "JOR", "CAN", "CPV", "QAT", "COL",
        "CRO", "MAR", "MEX", "NZL", "NED", "NOR", "AUT", "PAN",
        "PAR", "POR", "KSA", "SCO", "SWE", "SUI", "SEN", "ESP",
        "RSA", "KOR", "CZE", "TUN", "TUR", "URU", "USA", "UZB"
    ];

    [Test]
    public async Task CollectLatestAsync_selects_newest_approved_schedule_and_formats_ranking_csvs()
    {
        var apiClient = new StubFifaRankingApiClient(
            [
                new()
                {
                    IdRankingSchedule = "FRS_Male_Football_20260401",
                    RankingApproved = false,
                    PublicationDateUTC = null
                },
                new()
                {
                    IdRankingSchedule = "FRS_Male_Football_20251219",
                    RankingApproved = true,
                    PublicationDateUTC = "2026-01-19T17:11:57.976Z"
                },
                new()
                {
                    IdRankingSchedule = "FRS_Male_Football_20260119",
                    RankingApproved = true,
                    PublicationDateUTC = "2026-04-01T11:55:29.435Z"
                }
            ],
            CreateRankingRows());
        var sut = new FifaRankingSource(apiClient);

        var result = await sut.CollectLatestAsync(new DateOnly(2026, 5, 25));

        await Assert.That(apiClient.RequestedScheduleId).IsEqualTo("FRS_Male_Football_20260119");
        await Assert.That(result.ScheduleId).IsEqualTo("FRS_Male_Football_20260119");
        await Assert.That(result.PublicationDateUtc).IsEqualTo(new DateTimeOffset(2026, 4, 1, 11, 55, 29, 435, TimeSpan.Zero));
        await Assert.That(result.SourceRowCount).IsEqualTo(200);
        await Assert.That(result.MappedTeamCount).IsEqualTo(48);
        await Assert.That(result.KpiContent).StartsWith("Rank,Team,ELO,Published_At");
        await Assert.That(result.KpiContent).Contains("15,Mexiko,1681.03,2026-04-01T11:55:29.4350000+00:00");
        await Assert.That(result.KpiContent).Contains("60,Südafrika,1429.73,2026-04-01T11:55:29.4350000+00:00");

        var mexicoDocument = result.ContextDocuments.Single(document => document.DocumentName == "fifa-ranking-mexiko.csv");
        await Assert.That(mexicoDocument.Content)
            .IsEqualTo($"Rank,Team,ELO,Published_At{Environment.NewLine}15,Mexiko,1681.03,2026-04-01T11:55:29.4350000+00:00{Environment.NewLine}");
    }

    [Test]
    public async Task CollectLatestAsync_fails_when_any_mapped_wm26_team_is_missing()
    {
        var apiClient = new StubFifaRankingApiClient(
            [
                new()
                {
                    IdRankingSchedule = "FRS_Male_Football_20260119",
                    RankingApproved = true,
                    PublicationDateUTC = "2026-04-01T11:55:29.435Z"
                }
            ],
            CreateRankingRows(omitCountryCode: "MEX"));
        var sut = new FifaRankingSource(apiClient);

        InvalidOperationException? exception = null;
        try
        {
            await sut.CollectLatestAsync(new DateOnly(2026, 5, 25));
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("MEX (Mexiko)");
    }

    [Test]
    public async Task CollectLatestAsync_fails_when_latest_approved_schedule_is_missing()
    {
        var apiClient = new StubFifaRankingApiClient(
            [
                new()
                {
                    IdRankingSchedule = "FRS_Male_Football_20260401",
                    RankingApproved = false,
                    PublicationDateUTC = null
                }
            ],
            CreateRankingRows());
        var sut = new FifaRankingSource(apiClient);

        InvalidOperationException? exception = null;
        try
        {
            await sut.CollectLatestAsync(new DateOnly(2026, 5, 25));
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("No approved FIFA ranking schedule");
    }

    [Test]
    public async Task PreserveExistingContentWhenRankingUnchanged_reuses_existing_published_at_payload()
    {
        const string existingContent = "Rank,Team,ELO,Published_At\n15,Mexiko,1681.03,2026-04-01T11:55:29.4350000+00:00\n";
        const string newContent = "Rank,Team,ELO,Published_At\n15,Mexiko,1681.03,2026-06-11T10:00:59.6360000+00:00\n";

        var result = FifaRankingCsvUtility.PreserveExistingContentWhenRankingUnchanged(newContent, existingContent);

        await Assert.That(result).IsEqualTo(existingContent);
    }

    [Test]
    public async Task PreserveExistingContentWhenRankingUnchanged_does_not_reuse_legacy_data_collected_at_payload()
    {
        const string existingContent = "Rank,Team,ELO,Data_Collected_At\n15,Mexiko,1681.03,2026-05-25\n";
        const string newContent = "Rank,Team,ELO,Published_At\n15,Mexiko,1681.03,2026-06-11T10:00:59.6360000+00:00\n";

        var result = FifaRankingCsvUtility.PreserveExistingContentWhenRankingUnchanged(newContent, existingContent);

        await Assert.That(result).IsEqualTo(newContent);
    }

    [Test]
    public async Task PreserveExistingContentWhenRankingUnchanged_does_not_reuse_payload_when_elo_changes()
    {
        const string existingContent = "Rank,Team,ELO,Published_At\n15,Mexiko,1681.03,2026-04-01T11:55:29.4350000+00:00\n";
        const string newContent = "Rank,Team,ELO,Published_At\n15,Mexiko,1681.04,2026-06-11T10:00:59.6360000+00:00\n";

        var result = FifaRankingCsvUtility.PreserveExistingContentWhenRankingUnchanged(newContent, existingContent);

        await Assert.That(result).IsEqualTo(newContent);
    }

    private static IReadOnlyList<FifaRankingRowDto> CreateRankingRows(string? omitCountryCode = null)
    {
        var rows = new List<FifaRankingRowDto>();
        var fallbackRank = 100;

        foreach (var countryCode in RequiredCountryCodes.Where(code => code != omitCountryCode))
        {
            var (rank, points) = countryCode switch
            {
                "FRA" => (1, 1877.322731m),
                "MEX" => (15, 1681.034m),
                "RSA" => (60, 1429.725m),
                _ => (fallbackRank++, 1300m + fallbackRank)
            };

            rows.Add(new FifaRankingRowDto
            {
                IdCountry = countryCode,
                Rank = rank,
                TotalPoints = points
            });
        }

        var fillerIndex = 0;
        while (rows.Count < 200)
        {
            rows.Add(new FifaRankingRowDto
            {
                IdCountry = $"ZZ{fillerIndex:000}",
                Rank = 500 + fillerIndex,
                TotalPoints = 900m + fillerIndex
            });
            fillerIndex++;
        }

        return rows;
    }

    private sealed class StubFifaRankingApiClient : IFifaRankingApiClient
    {
        private readonly IReadOnlyList<FifaRankingScheduleDto> _schedules;
        private readonly IReadOnlyList<FifaRankingRowDto> _rows;

        public StubFifaRankingApiClient(
            IReadOnlyList<FifaRankingScheduleDto> schedules,
            IReadOnlyList<FifaRankingRowDto> rows)
        {
            _schedules = schedules;
            _rows = rows;
        }

        public string? RequestedScheduleId { get; private set; }

        public Task<IReadOnlyList<FifaRankingScheduleDto>> GetRankingSchedulesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_schedules);
        }

        public Task<IReadOnlyList<FifaRankingRowDto>> GetRankingRowsAsync(
            string rankingScheduleId,
            CancellationToken cancellationToken = default)
        {
            RequestedScheduleId = rankingScheduleId;
            return Task.FromResult(_rows);
        }
    }
}
