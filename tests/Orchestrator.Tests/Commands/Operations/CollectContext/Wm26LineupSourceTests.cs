using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class Wm26LineupSourceTests
{
    [Test]
    public async Task CollectAsync_enriches_player_by_transfermarkt_player_id()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Official Name", nationalTeamId: "100", playerId: "10")
                ]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Official Name,26,Defender,15.000.000");
            await Assert.That(content).DoesNotContain("Transfermarkt_Player_Id");
        });
    }

    [Test]
    public async Task CollectAsync_enriches_player_by_unambiguous_name_and_national_team_id()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Ana Example", nationalTeamId: "100")
                ]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Ana Example,24,Midfield,N/A");
        });
    }

    [Test]
    public async Task CollectAsync_keeps_missing_player_with_na_supplemental_values()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Official Roster Player", nationalTeamId: "100", position: "Attack")
                ],
                includeOptional: true);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Official Roster Player,N/A,Attack,N/A");
            await Assert.That(collection.MissingSourceData.Count).IsEqualTo(1);
        });
    }

    [Test]
    public async Task CollectAsync_keeps_missing_player_without_optional_seed_values()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Official Roster Player", nationalTeamId: "100")
                ]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Official Roster Player,N/A,N/A,N/A");
            await Assert.That(collection.MissingSourceData.Single().Fields)
                .IsEquivalentTo(["Age", "Position", "Market_Value_EUR"]);
        });
    }

    [Test]
    public async Task CollectAsync_keeps_player_when_explicit_transfermarkt_id_is_not_found()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Official Roster Player", playerId: "999999")
                ]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Official Roster Player,N/A,N/A,N/A");
            await Assert.That(collection.EnrichedRowCount).IsEqualTo(2);
        });
    }

    [Test]
    public async Task CollectAsync_fails_when_name_match_is_ambiguous()
    {
        await WithFixtureAsync(async fixture =>
        {
            AddDuplicateAnaExample(fixture.Database);
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Ana Example", nationalTeamId: "100")
                ]);

            await Assert.That(async () => await fixture.Source.CollectAsync(fixture.Request))
                .Throws<InvalidOperationException>()
                .WithMessageContaining("matched multiple Transfermarkt players");
        });
    }

    [Test]
    public async Task CollectAsync_fills_coach_name_from_national_teams_when_mapped()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(fixture.Seed, [SeedRow(role: "Coach", name: "", nationalTeamId: "100")]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var content = GetDocument(collection, "lineup-exampleland.csv").Content;

            await Assert.That(content).Contains("Coach,Casey Coach,,Coach,");
        });
    }

    [Test]
    public async Task CollectAsync_rejects_zero_player_market_value()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(
                fixture.Seed,
                [
                    SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100"),
                    SeedRow(name: "Player One", nationalTeamId: "100", marketValue: "0")
                ],
                includeOptional: true);

            await Assert.That(async () => await fixture.Source.CollectAsync(fixture.Request))
                .Throws<InvalidOperationException>()
                .WithMessageContaining("Market_Value_EUR must use N/A instead of 0");
        });
    }

    [Test]
    public async Task CollectAsync_generates_header_only_documents_for_manifest_teams_without_source_rows()
    {
        await WithFixtureAsync(async fixture =>
        {
            WriteSeed(fixture.Seed, [SeedRow(role: "Coach", name: "Coach One", nationalTeamId: "100")]);

            var collection = await fixture.Source.CollectAsync(fixture.Request);
            var missingland = GetDocument(collection, "lineup-missingland.csv");

            await Assert.That(missingland.Content).IsEqualTo("Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n");
            await Assert.That(collection.HeaderOnlyTeams.Select(team => team.Slug)).Contains("missingland");
        });
    }

    private static async Task WithFixtureAsync(Func<Fixture, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), $"wm26-lineups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var fixture = new Fixture(
                Path.Combine(root, "seed.csv"),
                Path.Combine(root, "teams.csv"),
                Path.Combine(root, "transfermarkt-datasets.duckdb"));
            CreateDuckDb(fixture.Database);
            WriteTeams(fixture.Teams);
            await test(fixture);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Wm26LineupDocument GetDocument(Wm26LineupCollection collection, string documentName)
    {
        return collection.ContextDocuments.Single(document => document.DocumentName == documentName);
    }

    private static void CreateDuckDb(string path)
    {
        using var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            create table players (
                player_id integer,
                name varchar,
                date_of_birth date,
                position varchar,
                market_value_in_eur integer,
                current_national_team_id integer
            );
            insert into players values
                (10, 'Player Ten', '2000-05-25', 'Defender', 15000000, 100),
                (11, 'Ana Example', '2001-05-26', 'Midfield', null, 100);
            create table national_teams (
                national_team_id integer,
                coach_name varchar
            );
            insert into national_teams values (100, 'Casey Coach');
            """;
        command.ExecuteNonQuery();
    }

    private static void AddDuplicateAnaExample(string path)
    {
        using var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "insert into players values (12, 'Ana Example', '2002-01-01', 'Attack', 1000000, 100)";
        command.ExecuteNonQuery();
    }

    private static void WriteTeams(string path)
    {
        File.WriteAllText(
            path,
            "Team_Slug,Team\r\nexampleland,Exampleland\r\nmissingland,Missingland\r\n");
    }

    private static void WriteSeed(
        string path,
        IReadOnlyList<Dictionary<string, string>> rows,
        bool includeOptional = false)
    {
        var columns = new List<string>
        {
            "Team_Slug",
            "Team",
            "Data_Collected_At",
            "Role",
            "Name",
            "Transfermarkt_National_Team_Id",
            "Transfermarkt_Player_Id"
        };

        if (includeOptional)
        {
            columns.AddRange(["Age", "Position", "Market_Value_EUR"]);
        }

        var lines = new List<string> { string.Join(",", columns) };
        lines.AddRange(rows.Select(row => string.Join(",", columns.Select(column => row.GetValueOrDefault(column, string.Empty)))));
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
    }

    private static Dictionary<string, string> SeedRow(
        string role = "Player",
        string name = "Player Ten",
        string nationalTeamId = "",
        string playerId = "",
        string age = "",
        string position = "",
        string marketValue = "")
    {
        return new Dictionary<string, string>
        {
            ["Team_Slug"] = "exampleland",
            ["Team"] = "Exampleland",
            ["Data_Collected_At"] = "2026-05-25",
            ["Role"] = role,
            ["Name"] = name,
            ["Transfermarkt_National_Team_Id"] = nationalTeamId,
            ["Transfermarkt_Player_Id"] = playerId,
            ["Age"] = age,
            ["Position"] = position,
            ["Market_Value_EUR"] = marketValue
        };
    }

    private sealed record Fixture(string Seed, string Teams, string Database)
    {
        public Wm26LineupSource Source { get; } = new(
            new StaticDuckDbProvider(Database));

        public Wm26LineupSourceRequest Request { get; } = new(Seed, Teams, DuckDbPath: null);
    }

    private sealed class StaticDuckDbProvider : IWm26TransfermarktDuckDbProvider
    {
        private readonly string _path;

        public StaticDuckDbProvider(string path)
        {
            _path = path;
        }

        public Task<string> GetDatabasePathAsync(
            string? configuredPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_path);
        }
    }
}
