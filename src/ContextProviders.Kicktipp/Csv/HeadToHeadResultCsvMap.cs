using CsvHelper.Configuration;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Csv;

/// <summary>
/// CsvHelper ClassMap for <see cref="HeadToHeadResult"/> defining the CSV schema for head-to-head history.
/// </summary>
public sealed class HeadToHeadResultCsvMap : ClassMap<HeadToHeadResult>
{
    public HeadToHeadResultCsvMap()
    {
        Map(m => m.League).Index(0).Name("Competition");
        Map(m => m.Matchday).Index(1).Name("Matchday");
        Map(m => m.PlayedAt).Index(2).Name("Played_At");
        Map(m => m.HomeTeam).Index(3).Name("Home_Team");
        Map(m => m.AwayTeam).Index(4).Name("Away_Team");
        Map(m => m.Score).Index(5).Name("Score");
        Map(m => m.Annotation).Index(6).Name("Annotation")
            .Convert(args => args.Value.Annotation ?? "");
    }
}
