using CsvHelper.Configuration;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Csv;

/// <summary>
/// CsvHelper ClassMap for <see cref="TeamStanding"/> defining the CSV schema for league standings.
/// </summary>
public sealed class TeamStandingCsvMap : ClassMap<TeamStanding>
{
    public TeamStandingCsvMap()
    {
        Map(m => m.Position).Index(0).Name("Position");
        Map(m => m.TeamName).Index(1).Name("Team");
        Map(m => m.GamesPlayed).Index(2).Name("Games");
        Map(m => m.Points).Index(3).Name("Points");
        Map(m => m.GoalsFormatted).Index(4).Name("Goal_Ratio");
        Map(m => m.GoalsFor).Index(5).Name("Goals_For");
        Map(m => m.GoalsAgainst).Index(6).Name("Goals_Against");
        Map(m => m.Wins).Index(7).Name("Wins");
        Map(m => m.Draws).Index(8).Name("Draws");
        Map(m => m.Losses).Index(9).Name("Losses");
    }
}
