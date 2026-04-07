using EHonda.KicktippAi.Core;

namespace KicktippIntegration;

public enum KicktippCommunityPredictionStatus
{
    Placed,
    Missed
}

public sealed record KicktippCommunityMatchdaySnapshot(
    int Matchday,
    IReadOnlyList<CollectedMatchOutcome> Outcomes,
    IReadOnlyList<KicktippCommunityParticipantSnapshot> Participants);

public sealed record KicktippCommunityParticipantSnapshot(
    string ParticipantId,
    string DisplayName,
    IReadOnlyList<KicktippCommunityMatchPrediction> Predictions,
    int MatchdayPoints,
    int TotalPoints);

public sealed record KicktippCommunityMatchPrediction(
    int EventIndex,
    string SourceMatchId,
    string? TippSpielId,
    KicktippCommunityPredictionStatus Status,
    BetPrediction? Prediction,
    int AwardedPoints);
