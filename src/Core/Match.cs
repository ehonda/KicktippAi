using NodaTime;

namespace EHonda.KicktippAi.Core;

public record Match(
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt,
    int Matchday);
