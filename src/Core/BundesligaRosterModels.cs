namespace EHonda.KicktippAi.Core;

public enum BundesligaRosterRole
{
    Coach,
    Player
}

public enum BundesligaRosterPosition
{
    Goalkeeper,
    Defender,
    Midfield,
    Attack
}

public enum BundesligaRosterMembershipSource
{
    DuckDb,
    FallbackSeed,
    LastKnownGood
}

public enum BundesligaRosterDuckDbGateResult
{
    Pass,
    Rejected,
    NotAvailable,
    NotEvaluated
}

public sealed record BundesligaRosterSeedEntry(
    string TeamSlug,
    BundesligaRosterRole Role,
    string Name,
    int? TransfermarktClubId,
    int? TransfermarktPlayerId,
    Uri MembershipSourceUrl,
    DateOnly MembershipAsOf);

public sealed record BundesligaRosterMember(
    BundesligaRosterRole Role,
    string Name,
    int? TransfermarktPlayerId = null,
    int? Age = null,
    BundesligaRosterPosition? Position = null,
    long? MarketValueEur = null);

public sealed record BundesligaRosterClubSnapshot(
    BundesligaTeamManifestEntry Team,
    DateOnly MembershipAsOf,
    BundesligaRosterMembershipSource MembershipSource,
    IReadOnlyList<BundesligaRosterMember> Members);

public sealed record BundesligaRosterQualityReportRow(
    BundesligaTeamManifestEntry Team,
    BundesligaRosterMembershipSource SelectedSource,
    DateOnly MembershipAsOf,
    IReadOnlyList<Uri> SourceReferences,
    string? SourceRevision,
    string? LastKnownGoodSnapshotId,
    DateOnly? DuckDbSnapshotAsOf,
    int PlayerCount,
    int CoachCount,
    int StablePlayerIdCount,
    int KnownAgeCount,
    int KnownPositionCount,
    int ValuedPlayerCount,
    BundesligaRosterDuckDbGateResult DuckDbGateResult,
    string SelectionReason,
    IReadOnlyList<string> Diagnostics);

public sealed record BundesligaRosterIdentity(int? TransfermarktPlayerId, string Name);

public sealed record BundesligaRosterDuckDbPlayer(
    int TransfermarktPlayerId,
    string Name,
    int CurrentClubId,
    int LastSeason);

public sealed record BundesligaRosterDuckDbCandidate(
    string TeamSlug,
    int? ManifestClubId,
    int MatchingClubRowCount,
    string CompetitionId,
    int LastSeason,
    DateOnly SnapshotAsOf,
    string SourceRevision,
    int? DeclaredSquadSize,
    string? HeadCoach,
    IReadOnlyList<BundesligaRosterDuckDbPlayer> Players);

public sealed record BundesligaRosterDuckDbEvaluation(
    BundesligaRosterDuckDbGateResult Result,
    IReadOnlyList<string> Diagnostics)
{
    public bool Passed => Result == BundesligaRosterDuckDbGateResult.Pass;
}

public sealed record BundesligaRosterMembershipCandidate(
    string TeamSlug,
    BundesligaRosterMembershipSource Source,
    DateOnly MembershipAsOf,
    bool StructurallyValid,
    string? SnapshotId = null);

public sealed record BundesligaRosterSelection(
    BundesligaRosterMembershipCandidate Selected,
    BundesligaRosterDuckDbEvaluation DuckDbEvaluation,
    string SelectionReason);

public enum BundesligaRosterPublicationDocumentKind
{
    Context,
    Kpi
}

public sealed record BundesligaRosterPublicationDocument(
    BundesligaRosterPublicationDocumentKind Kind,
    string Name,
    byte[] Content);
