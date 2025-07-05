using NodaTime;

namespace Core;

public record Match(
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt);
