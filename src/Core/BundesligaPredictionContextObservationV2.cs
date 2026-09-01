namespace EHonda.KicktippAi.Core;

/// <summary>Immutable proof that one exact community/profile context was observed.</summary>
public sealed class BundesligaPredictionContextObservationV2
{
    private BundesligaPredictionContextObservationV2(
        string communityContext,
        string profileId,
        PredictionContextProvenanceV2 provenance) =>
        (CommunityContext, ProfileId, Provenance) = (communityContext, profileId, provenance);

    public string CommunityContext { get; }
    public string ProfileId { get; }
    public PredictionContextProvenanceV2 Provenance { get; }

    public static BundesligaPredictionContextObservationV2 Create(
        string communityContext,
        string profileId,
        PredictionContextProvenanceV2 provenance)
    {
        BundesligaPredictionContractValidation.Community(communityContext, nameof(communityContext));
        BundesligaPredictionContractValidation.Identifier(profileId, nameof(profileId));
        ArgumentNullException.ThrowIfNull(provenance);
        return new BundesligaPredictionContextObservationV2(communityContext, profileId, provenance);
    }

    public void Require(BundesligaPredictionAuthority authority, BundesligaTypedCurrentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(CommunityContext, authority.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(ProfileId, identity.ProfileId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Context observation does not match the exact Community Context and profile.");
        }
    }
}
