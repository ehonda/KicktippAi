namespace EHonda.KicktippAi.Core;

public enum BundesligaContextSourceIssueFenceState { Ready, CreateUncertain, Bound, LegacyUncertain }

public sealed record BundesligaContextSourceIssueFenceIdentity(string Repository, string Competition,
    BundesligaContextSourceScope Scope, BundesligaContextSource Source, string Marker)
{
    public const string CanonicalRepository = "ehonda/KicktippAi";
    public const string CanonicalMarker = "<!-- kicktippai:context-source-health:bundesliga-2026-27:club-elo -->";
    public string StorageId => BundesligaContextSourceHashing.HashFields("context-source-issue-fence-storage/v1",
        Repository, Competition, BundesligaContextSourceContract.ScopeValue(Scope), BundesligaContextSourceContract.SourceValue(Source), Marker);
    public void Validate()
    {
        if (Repository != CanonicalRepository || Competition != BundesligaContextSourceContract.Competition
            || Scope != BundesligaContextSourceScope.ProductionLive || Source != BundesligaContextSource.ClubElo || Marker != CanonicalMarker)
            throw new InvalidDataException("ISSUE_FENCE_IDENTITY_INVALID");
    }
    public static BundesligaContextSourceIssueFenceIdentity FromHealth(BundesligaContextSourceHealth health)
    {
        health.Validate();
        var identity = new BundesligaContextSourceIssueFenceIdentity(CanonicalRepository, health.Competition, health.Scope,
            health.Source, health.DesiredIssueProjection?.Marker ?? CanonicalMarker);
        identity.Validate(); return identity;
    }
}

public sealed record BundesligaContextSourceIssueFenceAttempt(string Token, string BodySha256,
    BundesligaContextSourceWatermark Watermark, BundesligaContextSourceIssueState DesiredState, DateTimeOffset StartedAtUtc)
{
    public void Validate()
    {
        BundesligaContextSourceContract.ValidateClaimToken(Token); BundesligaContextSourceHashing.ValidateSha(BodySha256);
        _ = BundesligaContextSourceCycleIdentity.Create(BundesligaContextSourceContract.Competition,
            BundesligaContextSourceScope.ProductionLive, Watermark.CycleId, Watermark.Sequence);
        if (!Enum.IsDefined(DesiredState)) throw new InvalidDataException("ISSUE_FENCE_ATTEMPT_INVALID");
        BundesligaContextSourceContract.FormatUtc(StartedAtUtc);
    }
}

public sealed record BundesligaContextSourceIssueFence(BundesligaContextSourceIssueFenceIdentity Identity, long Revision,
    BundesligaContextSourceIssueFenceState State, BundesligaContextSourceIssueFenceAttempt? Attempt,
    long? IssueNumber, DateTimeOffset UpdatedAtUtc)
{
    public const string Contract = "context-source-issue-fence/v1";
    public void Validate()
    {
        Identity.Validate(); BundesligaContextSourceContract.FormatUtc(UpdatedAtUtc); Attempt?.Validate();
        if (Revision < 0 || !Enum.IsDefined(State) || IssueNumber is <= 0
            || (State == BundesligaContextSourceIssueFenceState.Bound) != (IssueNumber is not null)
            || (State == BundesligaContextSourceIssueFenceState.CreateUncertain && Attempt is null)
            || (State is BundesligaContextSourceIssueFenceState.Ready or BundesligaContextSourceIssueFenceState.LegacyUncertain && Attempt is not null)
            || (Attempt is not null && Attempt.StartedAtUtc > UpdatedAtUtc))
            throw new InvalidDataException("ISSUE_FENCE_STATE_INVALID");
    }
}

/// <summary>Returned only after a fresh Ready transaction commits successfully. Replays never return a grant.</summary>
public sealed class BundesligaContextSourceIssueCreateGrant(BundesligaContextSourceIssueFence fence)
{
    private int _consumed;
    public BundesligaContextSourceIssueFence Fence { get; } = fence;
    public bool TryConsume() => Interlocked.Exchange(ref _consumed, 1) == 0;
}

public sealed record BundesligaContextSourceIssueFenceRead(bool CurrentPending, BundesligaContextSourceIssueFence? Fence);
public enum BundesligaContextSourceIssueFenceBindDisposition { Bound, Conflict }
public sealed record BundesligaContextSourceIssueFenceBindResult(BundesligaContextSourceIssueFenceBindDisposition Disposition,
    BundesligaContextSourceIssueFence Fence);

public interface IBundesligaContextSourceIssueFenceRepository
{
    Task<BundesligaContextSourceIssueFenceRead> ReadOrInitializeAsync(BundesligaContextSourceHealth expectedHealth, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceIssueCreateGrant?> TryArmCreateAsync(BundesligaContextSourceHealth expectedHealth, long expectedRevision,
        BundesligaContextSourceIssueFenceAttempt attempt, CancellationToken cancellationToken = default);
    Task<BundesligaContextSourceIssueFenceBindResult> BindObservedIssueAsync(BundesligaContextSourceIssueFence expectedFence,
        long verifiedIssueNumber, CancellationToken cancellationToken = default);
}
