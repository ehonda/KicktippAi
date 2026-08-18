using DuckDB.NET.Data;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>Small, mutable multi-club DuckDB artifact for ADR-0017/0018 source tests.</summary>
internal sealed class BundesligaRosterDuckDbFixture : IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly Dictionary<string, int[]> _playerIds = new(StringComparer.Ordinal);

    public BundesligaRosterDuckDbFixture(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"buli-roster-{Guid.NewGuid():N}.duckdb");
        var builder = new System.Data.Common.DbConnectionStringBuilder { ["Data Source"] = Path };
        _connection = new DuckDBConnection(builder.ConnectionString);
        _connection.Open();
        Execute("create table clubs(club_id integer, domestic_competition_id varchar, last_season integer, squad_size integer, coach_name varchar)");
        Execute("create table players(player_id integer, name varchar, current_club_id integer, last_season integer, date_of_birth date, position varchar)");
        Execute("create table player_valuations(player_id integer, date date, market_value_in_eur bigint)");
    }

    public string Path { get; }

    public int FirstPlayerId(string slug) => _playerIds[slug][0];

    public void AddEligibleClub(string slug)
    {
        var team = BundesligaTeamManifest.Default.GetByTeamSlug(slug);
        var players = BundesligaRosterSeed.Default.Entries
            .Where(entry => entry.TeamSlug == slug && entry.Role == BundesligaRosterRole.Player)
            .ToArray();
        var ids = new List<int>();
        Execute($"insert into clubs values ({team.TransfermarktClubId}, 'L1', 2026, {players.Length}, 'Coach {slug}')");
        foreach (var (entry, index) in players.Select((entry, index) => (entry, index)))
        {
            var id = entry.TransfermarktPlayerId ?? 8_000_000 + index;
            ids.Add(id);
            Execute($"insert into players values ({id}, {Quote(entry.Name)}, {team.TransfermarktClubId}, 2026, '2000-01-01', 'Midfield')");
            Execute($"insert into player_valuations values ({id}, '2026-08-18', 1000000)");
        }
        _playerIds.Add(slug, ids.ToArray());
    }

    public void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (File.Exists(Path)) File.Delete(Path);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
