using EHonda.KicktippAi.Core;
using NodaTime;

namespace Core.Tests;

public class BundesligaHistoryPlayedDateCollectorTests
{
    private const string DocumentName = "recent-history-b04.csv";
    private static readonly BundesligaHistoryPlayedDateCollector Collector = new();

    [Test]
    public async Task Embedded_preseason_map_covers_every_selected_inventory_row_with_frozen_source_counts()
    {
        var map = BundesligaHistoryPlayedDateMap.Default.Entries;
        var documents = map.GroupBy(entry => entry.DocumentName, StringComparer.Ordinal)
            .Select(group => new BundesligaHistoryDocument(group.Key,
                "Competition,Home_Team,Away_Team,Score,Annotation\n" +
                string.Join('\n', group.OrderBy(entry => entry.RowOrdinal).Select(entry =>
                    $"{entry.HistoryCompetition},{entry.HomeTeam},{entry.AwayTeam},{entry.Score},{entry.Annotation}"))))
            .ToArray();

        var result = Collector.Collect(CompetitionIds.Bundesliga2026_27, documents, map, []);

        await Assert.That(map.Count).IsEqualTo(400);
        await Assert.That(map.Select(entry => (entry.SourceName, entry.SourceMatchId)).Distinct().Count()).IsEqualTo(197);
        await Assert.That(map.Count(entry => entry.SourceName == BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceName)).IsEqualTo(326);
        await Assert.That(map.Count(entry => entry.SourceName == BundesligaHistoryPlayedDateMap.OpenLigaDbSourceName)).IsEqualTo(72);
        await Assert.That(map.Count(entry => entry.SourceName == BundesligaHistoryPlayedDateMap.UefaSourceName)).IsEqualTo(2);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.FixedMapCount).IsEqualTo(400);
        await Assert.That(result.Documents.Count).IsEqualTo(54);
        await Assert.That(map.Select(entry => entry.DocumentName).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(54);
    }

    [Test]
    public async Task Tracked_preseason_map_is_utf8_without_bom_and_canonical_crlf()
    {
        var path = Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "history-played-dates.csv");
        var bytes = await File.ReadAllBytesAsync(path);
        var content = System.Text.Encoding.UTF8.GetString(bytes);

        await Assert.That(bytes[0]).IsEqualTo((byte)'D');
        await Assert.That(content.EndsWith("\r\n", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Replace("\r\n", string.Empty, StringComparison.Ordinal)).DoesNotContain("\n");
    }

    [Test]
    public async Task Fixed_map_reconstructs_exact_date_with_canonical_crlf()
    {
        var result = Collect(Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,"),
            [Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09")]);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.FixedMapCount).IsEqualTo(1);
        await Assert.That(result.Documents[0].Content).IsEqualTo(
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\n" +
            "1.BL,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,\r\n");
    }

    [Test]
    public async Task Non_league_row_uses_fixed_map_even_when_kicktipp_outcome_matches_identity()
    {
        var content = Undated("DFB,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var outcome = new PersistedMatchOutcome("community", CompetitionIds.Bundesliga2026_27,
            "Bayer 04 Leverkusen", "VfB Stuttgart", Instant.FromUtc(2026, 5, 10, 13, 30).InUtc(), 33,
            3, 1, MatchOutcomeAvailability.Completed, "9", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = Collect(content,
            [Map(1, "DFB", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09")], [outcome]);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.FixedMapCount).IsEqualTo(1);
        await Assert.That(result.KicktippCount).IsEqualTo(0);
        await Assert.That(result.Documents[0].Content).Contains("DFB,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
    }

    [Test]
    public async Task Non_league_row_without_fixed_map_fails_instead_of_using_kicktipp_outcome()
    {
        var content = Undated("DFB,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var outcome = new PersistedMatchOutcome("community", CompetitionIds.Bundesliga2026_27,
            "Bayer 04 Leverkusen", "VfB Stuttgart", Instant.FromUtc(2026, 5, 10, 13, 30).InUtc(), 33,
            3, 1, MatchOutcomeAvailability.Completed, "9", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = Collect(content, [], [outcome]);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.KicktippCount).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Any(value => value.Message.Contains("No exact played-date source", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.Documents[0].Content).IsEqualTo(content);
    }

    [Test]
    public async Task Completed_current_season_outcome_uses_exact_manifest_names_and_berlin_date()
    {
        var outcome = new PersistedMatchOutcome("community", CompetitionIds.Bundesliga2026_27,
            "Bayer 04 Leverkusen", "VfB Stuttgart", Instant.FromUtc(2026, 8, 28, 18, 30).InUtc(), 1,
            2, 1, MatchOutcomeAvailability.Completed, "4711", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = Collect(Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,2:1,"), [], [outcome]);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.KicktippCount).IsEqualTo(1);
        await Assert.That(result.Documents[0].Content).Contains("1.BL,2026-08-28,Bayer 04 Leverkusen,VfB Stuttgart,2:1,");
    }

    [Test]
    public async Task Prepended_current_outcome_and_shifted_fixed_suffix_resolve_by_exact_identity()
    {
        var content = Undated(
            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,2:1,\n" +
            "DFB,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var frozenSuffix = Map(1, "DFB", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");
        var outcome = new PersistedMatchOutcome("community", CompetitionIds.Bundesliga2026_27,
            "Bayer 04 Leverkusen", "VfB Stuttgart", Instant.FromUtc(2026, 8, 28, 18, 30).InUtc(), 1,
            2, 1, MatchOutcomeAvailability.Completed, "4711", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = Collect(content, [frozenSuffix], [outcome]);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.KicktippCount).IsEqualTo(1);
        await Assert.That(result.FixedMapCount).IsEqualTo(1);
        await Assert.That(result.Documents[0].Content)
            .Contains("1.BL,2026-08-28,Bayer 04 Leverkusen,VfB Stuttgart,2:1,")
            .And.Contains("DFB,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
    }

    [Test]
    public async Task Rolled_out_ledger_entries_are_not_required_but_new_uncaptured_rows_still_fail()
    {
        var rolledOut = Map(1, "DFB", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");
        var selected = Map(2, "DFB", "VfB Stuttgart", "Bayer 04 Leverkusen", "1:2", "2026-06-01",
            sourceMatchId: "4634535",
            sourceUrl: "https://www.transfermarkt.co.uk/example/index/spielbericht/4634535");

        var rolledWindow = Collect(Undated("DFB,VfB Stuttgart,Bayer 04 Leverkusen,1:2,"), [rolledOut, selected]);
        var uncaptured = Collect(Undated(
            "DFB,SC St. Tönis,Eintracht Frankfurt,0:11,\n" +
            "DFB,VfB Stuttgart,Bayer 04 Leverkusen,1:2,"), [rolledOut, selected]);

        await Assert.That(rolledWindow.Succeeded).IsTrue();
        await Assert.That(rolledWindow.FixedMapCount).IsEqualTo(1);
        await Assert.That(uncaptured.Succeeded).IsFalse();
        await Assert.That(uncaptured.Diagnostics.Any(value =>
            value.Message.Contains("No exact played-date source", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Multiple_fixed_map_candidates_for_one_exact_identity_fail_closed()
    {
        var first = Map(1, "DFB", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");
        var second = first with
        {
            RowOrdinal = 2,
            PlayedAt = "2026-05-10",
            SourceMatchId = "4634535",
            SourceUrl = "https://www.transfermarkt.co.uk/example/index/spielbericht/4634535"
        };

        var result = Collect(Undated("DFB,Bayer 04 Leverkusen,VfB Stuttgart,3:1,"), [first, second]);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Diagnostics.Any(value =>
            value.Message.Contains("Multiple fixed-map candidates", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Incomplete_rows_are_excluded_before_completed_ordinals_are_assigned()
    {
        var content = Undated(
            "DFB,SV Wehen Wiesbaden,Bayer 04 Leverkusen,,\n" +
            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var map = Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");

        var result = Collect(content, [map]);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.ExcludedIncompleteRowCount).IsEqualTo(1);
        await Assert.That(result.Documents[0].Content).DoesNotContain("SV Wehen Wiesbaden");
        await Assert.That(result.Documents[0].Content).Contains("2026-05-09");
    }

    [Test]
    public async Task Inventory_export_rejects_header_only_and_incomplete_only_selected_documents()
    {
        var headerOnly = new BundesligaHistoryDocument(DocumentName,
            "Competition,Home_Team,Away_Team,Score,Annotation\r\n");
        var incompleteOnly = new BundesligaHistoryDocument(DocumentName,
            "Competition,Home_Team,Away_Team,Score,Annotation\r\n" +
            "DFB,Bayer 04 Leverkusen,VfB Stuttgart,,\r\n");

        await Assert.That(() => BundesligaHistoryPlayedDateCollector.ExportInventory([headerOnly]))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaHistoryPlayedDateCollector.ExportInventory([incompleteOnly]))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Repeated_application_is_byte_stable()
    {
        var map = new[] { Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09") };
        var first = Collect(Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,"), map);
        var second = Collect(first.Documents[0].Content, map);

        await Assert.That(first.Succeeded).IsTrue();
        await Assert.That(second.Succeeded).IsTrue();
        await Assert.That(second.Documents[0].Content).IsEqualTo(first.Documents[0].Content);
        await Assert.That(second.PreservedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Head_to_head_and_other_documents_are_byte_unchanged()
    {
        const string headToHead = "League,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation\n1.BL,1,2025-08-01T20:30:00+02:00,A,B,1:0,";
        var result = Collector.Collect(CompetitionIds.Bundesliga2026_27,
            [new("head-to-head-b04-vs-vfb.csv", headToHead)], [], []);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Documents[0].Content).IsEqualTo(headToHead);
    }

    [Test]
    public async Task Duplicate_indistinguishable_rows_fail_and_return_last_known_good_bytes()
    {
        var content = Undated(
            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,\n" +
            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var result = Collect(content,
            [Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09"),
             Map(2, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2025-05-09", "other")]);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Diagnostics.Any(value => value.Message.Contains("Ambiguous duplicate", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.Documents[0].Content).IsEqualTo(content);
    }

    [Test]
    public async Task Missing_and_conflicting_sources_fail_closed()
    {
        var content = Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var missing = Collect(content, []);
        var conflictOutcome = new PersistedMatchOutcome("community", CompetitionIds.Bundesliga2026_27,
            "Bayer 04 Leverkusen", "VfB Stuttgart", Instant.FromUtc(2026, 5, 10, 13, 30).InUtc(), 33,
            3, 1, MatchOutcomeAvailability.Completed, "9", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var conflict = Collect(content,
            [Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09")], [conflictOutcome]);

        await Assert.That(missing.Succeeded).IsFalse();
        await Assert.That(conflict.Succeeded).IsFalse();
        await Assert.That(conflict.Diagnostics.Any(value => value.Message.Contains("Conflicting", StringComparison.Ordinal))).IsTrue();
        await Assert.That(conflict.Documents[0].Content).IsEqualTo(content);
    }

    [Test]
    public async Task Competition_and_manifest_document_identity_are_strict()
    {
        var wrongCompetition = Collector.Collect(CompetitionIds.FifaWorldCup2026,
            [new(DocumentName, Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,"))], [], []);
        var unknownSlug = Collector.Collect(CompetitionIds.Bundesliga2026_27,
            [new("recent-history-unknown.csv", Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,"))], [], []);

        await Assert.That(wrongCompetition.Succeeded).IsFalse();
        await Assert.That(unknownSlug.Succeeded).IsFalse();
    }

    [Test]
    public async Task Explicit_expected_selected_document_set_must_match_exactly()
    {
        var content = Undated("1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,");
        var documents = new[] { new BundesligaHistoryDocument(DocumentName, content) };
        var map = new[] { Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09") };

        var missing = Collector.Collect(CompetitionIds.Bundesliga2026_27, documents, map, [],
            new HashSet<string>([DocumentName, "home-history-b04.csv"], StringComparer.Ordinal));
        var unexpected = Collector.Collect(CompetitionIds.Bundesliga2026_27, documents, map, [],
            new HashSet<string>(["home-history-b04.csv"], StringComparer.Ordinal));

        await Assert.That(missing.Succeeded).IsFalse();
        await Assert.That(missing.Diagnostics[0].Message).Contains("missing=[home-history-b04.csv]");
        await Assert.That(unexpected.Succeeded).IsFalse();
        await Assert.That(unexpected.Diagnostics[0].Message).Contains("unexpected=[recent-history-b04.csv]");
    }

    [Test]
    public async Task Explicit_expected_set_rejects_header_only_and_incomplete_only_selected_documents()
    {
        var contents = new[]
        {
            "Competition,Home_Team,Away_Team,Score,Annotation\r\n",
            "Competition,Home_Team,Away_Team,Score,Annotation\r\n" +
            "DFB,SV Wehen Wiesbaden,Bayer 04 Leverkusen,,\r\n"
        };
        foreach (var content in contents)
        {
            var document = new BundesligaHistoryDocument(DocumentName, content);

            var result = Collector.Collect(
                CompetitionIds.Bundesliga2026_27,
                [document],
                [],
                [],
                new HashSet<string>([DocumentName], StringComparer.Ordinal));

            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Documents).IsEquivalentTo([document]);
            await Assert.That(result.Diagnostics.Any(value => value.Message.Contains(
                "must contain at least one completed result row",
                StringComparison.Ordinal))).IsTrue();
        }
    }

    [Test]
    public async Task Existing_played_at_is_preserved_but_source_conflicts_fail()
    {
        var content = "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\n" +
                      "1.BL,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,\r\n";
        var matching = Collect(content,
            [Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09")]);
        var conflict = Collect(content,
            [Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-10")]);

        await Assert.That(matching.Succeeded).IsTrue();
        await Assert.That(matching.Documents[0].Content).IsEqualTo(content);
        await Assert.That(conflict.Succeeded).IsFalse();
    }

    [Test]
    public async Task Map_parser_enforces_fixed_source_scope_and_provenance()
    {
        var valid = BundesligaHistoryPlayedDateMap.Write([
            Map(1, "2.BL", "FC Schalke 04", "SC Paderborn 07", "2:1", "2025-11-28",
                sourceMatchId: "2025-26-bl2-2025-11-28-fc-schalke-04-sc-paderborn-07",
                sourceName: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceName,
                sourceUrl: BundesligaHistoryPlayedDateMap.OpenLigaDbLeagueUrl,
                sourceRevision: BundesligaHistoryPlayedDateMap.OpenLigaDbLeagueRevision,
                sourceClass: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceClass)
        ]);
        using var reader = new StringReader(valid);
        var parsed = BundesligaHistoryPlayedDateMap.ParseFragment(reader, "test");

        await Assert.That(parsed.Entries.Count).IsEqualTo(1);
        await Assert.That(() =>
        {
            using var invalidReader = new StringReader(valid.Replace("2.BL", "1.BL", StringComparison.Ordinal));
            return BundesligaHistoryPlayedDateMap.ParseFragment(invalidReader, "test");
        }).Throws<InvalidDataException>();

        var future = valid.Replace(
            "2026-08-21T12:00:00+02:00",
            DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"),
            StringComparison.Ordinal);
        await Assert.That(() =>
        {
            using var futureReader = new StringReader(future);
            return BundesligaHistoryPlayedDateMap.ParseFragment(futureReader, "test");
        }).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Map_parser_binds_transfermarkt_competition_score_host_path_and_numeric_match_id()
    {
        var valid = Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");
        var invalidEntries = new[]
        {
            valid with { HistoryCompetition = "Friendly" },
            valid with { Score = "3-1" },
            valid with { SourceUrl = "https://example.com/example/index/spielbericht/4634534" },
            valid with { SourceUrl = "https://www.transfermarkt.co.uk/example/index/spielbericht/9999999" },
            valid with
            {
                SourceMatchId = "not-numeric",
                SourceUrl = "https://www.transfermarkt.co.uk/example/index/spielbericht/not-numeric"
            }
        };

        foreach (var entry in invalidEntries)
        {
            var content = BundesligaHistoryPlayedDateMap.Write([entry]);
            await Assert.That(() =>
            {
                using var reader = new StringReader(content);
                return BundesligaHistoryPlayedDateMap.ParseFragment(reader, "invalid-transfermarkt");
            }).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Map_parser_accepts_only_the_frozen_DFB_fallback_capture()
    {
        var final = Map(1, "DFB", "FC Bayern München", "VfB Stuttgart", "3:0", "2026-05-23",
                sourceMatchId: "81581",
                sourceName: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceName,
                sourceUrl: BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalUrl,
                sourceRevision: BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalRevision,
                sourceClass: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceClass);
        var valid = BundesligaHistoryPlayedDateMap.Write([
            final with { DocumentName = "away-history-vfb.csv" },
            final with { DocumentName = "home-history-fcb.csv" },
            final with { DocumentName = "recent-history-fcb.csv" },
            final with { DocumentName = "recent-history-vfb.csv" }
        ]);

        using var reader = new StringReader(valid);
        var parsed = BundesligaHistoryPlayedDateMap.ParseFragment(reader, "test");

        await Assert.That(parsed.Entries.Count).IsEqualTo(4);
        await Assert.That(() =>
        {
            using var invalidReader = new StringReader(valid.Replace(
                BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalRevision,
                BundesligaHistoryPlayedDateMap.OpenLigaDbLeagueRevision,
                StringComparison.Ordinal));
            return BundesligaHistoryPlayedDateMap.ParseFragment(invalidReader, "test");
        }).Throws<InvalidDataException>();
        await Assert.That(() =>
        {
            using var missingCoverageReader = new StringReader(BundesligaHistoryPlayedDateMap.Write(parsed.Entries.Take(3)));
            return BundesligaHistoryPlayedDateMap.ParseFragment(missingCoverageReader, "missing-2025-final-coverage");
        }).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Map_parser_accepts_only_the_frozen_2026_DFB_live_completion_capture()
    {
        var match = Map(1, "DFB", "SC St. Tönis", "Eintracht Frankfurt", "0:11", "2026-08-21",
            sourceMatchId: BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokal2026MatchId,
            sourceName: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceName,
            sourceUrl: BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokal2026Url,
            sourceRevision: BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokal2026Revision,
            sourceClass: BundesligaHistoryPlayedDateMap.OpenLigaDbSourceClass) with
        {
            VerifiedAt = "2026-08-21T20:14:00+02:00"
        };
        var entries = new[]
        {
            match with { DocumentName = "away-history-sge.csv" },
            match with { DocumentName = "recent-history-sge.csv" }
        };
        var valid = BundesligaHistoryPlayedDateMap.Write(entries);

        using var reader = new StringReader(valid);
        var parsed = BundesligaHistoryPlayedDateMap.ParseFragment(reader, "dfb-2026-live-completion");

        await Assert.That(parsed.Entries.Count).IsEqualTo(2);

        var invalidMaps = new[]
        {
            BundesligaHistoryPlayedDateMap.Write(entries.Take(1)),
            BundesligaHistoryPlayedDateMap.Write([entries[0] with { HomeTeam = "Wrong Team" }, entries[1]]),
            BundesligaHistoryPlayedDateMap.Write([entries[0] with { SourceRevision = BundesligaHistoryPlayedDateMap.OpenLigaDbDfbPokalRevision }, entries[1]]),
            BundesligaHistoryPlayedDateMap.Write([entries[0] with { VerifiedAt = "2026-08-21T19:30:00+02:00" }, entries[1]])
        };
        foreach (var invalid in invalidMaps)
        {
            await Assert.That(() =>
            {
                using var invalidReader = new StringReader(invalid);
                return BundesligaHistoryPlayedDateMap.ParseFragment(invalidReader, "invalid-dfb-2026-live-completion");
            }).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Map_parser_rejects_duplicate_exact_fixed_identity_even_when_ordinals_and_sources_differ()
    {
        var first = Map(1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", "2026-05-09");
        var duplicate = first with
        {
            RowOrdinal = 2,
            SourceMatchId = "4634535",
            SourceUrl = "https://www.transfermarkt.co.uk/example/index/spielbericht/4634535"
        };
        var content = BundesligaHistoryPlayedDateMap.Write([first, duplicate]);

        await Assert.That(() =>
        {
            using var duplicateReader = new StringReader(content);
            return BundesligaHistoryPlayedDateMap.ParseFragment(duplicateReader, "duplicate-exact-identity");
        }).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Map_parser_limits_UEFA_final_to_the_two_exact_inventory_rows()
    {
        var uefa = Map(1, "EL", "SC Freiburg", "Aston Villa", "0:3", "2026-05-20",
            sourceMatchId: BundesligaHistoryPlayedDateMap.UefaFinalMatchId,
            sourceName: BundesligaHistoryPlayedDateMap.UefaSourceName,
            sourceUrl: BundesligaHistoryPlayedDateMap.UefaFinalUrl,
            sourceRevision: BundesligaHistoryPlayedDateMap.UefaFinalRevision,
            sourceClass: BundesligaHistoryPlayedDateMap.UefaSourceClass);
        var valid = BundesligaHistoryPlayedDateMap.Write([
            uefa with { DocumentName = "home-history-scf.csv" },
            uefa with { DocumentName = "recent-history-scf.csv" }
        ]);

        using var reader = new StringReader(valid);
        var parsed = BundesligaHistoryPlayedDateMap.ParseFragment(reader, "test");

        await Assert.That(parsed.Entries.Count).IsEqualTo(2);
        await Assert.That(() =>
        {
            using var invalidReader = new StringReader(valid.Replace("recent-history-scf.csv", "recent-history-b04.csv", StringComparison.Ordinal));
            return BundesligaHistoryPlayedDateMap.ParseFragment(invalidReader, "test");
        }).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Production_map_parser_requires_all_manifest_documents_and_contiguous_completed_ordinals()
    {
        var complete = BundesligaHistoryPlayedDateMap.Default.Entries;
        var missingDocument = BundesligaHistoryPlayedDateMap.Write(
            complete.Where(entry => entry.DocumentName != "away-history-bvb.csv"));
        var missingFirstOrdinal = BundesligaHistoryPlayedDateMap.Write(
            complete.Where(entry => entry.DocumentName != "home-history-b04.csv" || entry.RowOrdinal != 1));

        await Assert.That(() =>
        {
            using var reader = new StringReader(missingDocument);
            return BundesligaHistoryPlayedDateMap.Parse(reader, "missing-document");
        }).Throws<InvalidDataException>();
        await Assert.That(() =>
        {
            using var reader = new StringReader(missingFirstOrdinal);
            return BundesligaHistoryPlayedDateMap.Parse(reader, "missing-ordinal");
        }).Throws<InvalidDataException>();
    }

    private static BundesligaHistoryPlayedDateCollectionResult Collect(string content,
        IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> map,
        IReadOnlyList<PersistedMatchOutcome>? outcomes = null) =>
        Collector.Collect(CompetitionIds.Bundesliga2026_27, [new(DocumentName, content)], map, outcomes ?? []);

    private static string Undated(string rows) =>
        "Competition,Home_Team,Away_Team,Score,Annotation\n" + rows;

    private static BundesligaHistoryPlayedDateMapEntry Map(int ordinal, string competition, string home, string away,
        string score, string playedAt, string sourceMatchId = "4634534",
        string sourceName = BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceName,
        string sourceUrl = "https://www.transfermarkt.co.uk/example/index/spielbericht/4634534",
        string sourceRevision = BundesligaHistoryPlayedDateMap.TransfermarktDatasetRevision,
        string sourceClass = BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceClass) =>
        new(DocumentName, ordinal, competition, home, away, score, string.Empty, playedAt,
            sourceClass, sourceName, sourceUrl, sourceRevision,
            sourceMatchId, "2026-08-21T12:00:00+02:00");
}
