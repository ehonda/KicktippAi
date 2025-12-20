using CsvHelper.Configuration;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Csv;

/// <summary>
/// CsvHelper ClassMap for <see cref="MatchResult"/> defining the CSV schema for match history.
/// Uses empty string for score when goals are not available (pending matches).
/// </summary>
public sealed class MatchResultCsvMap : ClassMap<MatchResult>
{
    public MatchResultCsvMap()
    {
        Map(m => m.Competition).Index(0).Name("Competition");
        Map(m => m.HomeTeam).Index(1).Name("Home_Team");
        Map(m => m.AwayTeam).Index(2).Name("Away_Team");
        Map(m => m.HomeGoals).Index(3).Name("Score")
            .Convert(args => args.Value.HomeGoals.HasValue && args.Value.AwayGoals.HasValue
                ? $"{args.Value.HomeGoals}:{args.Value.AwayGoals}"
                : "");
        Map(m => m.Annotation).Index(4).Name("Annotation")
            .Convert(args => args.Value.Annotation ?? "");
    }
}
