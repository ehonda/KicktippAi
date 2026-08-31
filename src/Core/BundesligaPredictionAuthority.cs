namespace EHonda.KicktippAi.Core;

public enum BundesligaPredictionAuthorityMode
{
    Direct,
    Copy
}

public enum BundesligaPredictionItemKind
{
    Match,
    Bonus
}

public sealed record BundesligaIdentitySeedReference
{
    private BundesligaIdentitySeedReference(int generation, string sha256) =>
        (Generation, Sha256) = (generation, sha256);

    public int Generation { get; }
    public string Sha256 { get; }

    public static BundesligaIdentitySeedReference Create(int generation, string sha256)
    {
        BundesligaPredictionContractValidation.Generation(generation, nameof(generation));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new BundesligaIdentitySeedReference(generation, sha256);
    }
}

public sealed record BundesligaCopyBindingReference
{
    private BundesligaCopyBindingReference(int generation, string sha256) =>
        (Generation, Sha256) = (generation, sha256);

    public int Generation { get; }
    public string Sha256 { get; }

    public static BundesligaCopyBindingReference Create(int generation, string sha256)
    {
        BundesligaPredictionContractValidation.Generation(generation, nameof(generation));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new BundesligaCopyBindingReference(generation, sha256);
    }
}

/// <summary>
/// Complete fixed authority for one Bundesliga 2026/27 direct-generation or
/// accepted-copy operation.
/// </summary>
public sealed record BundesligaPredictionAuthority
{
    public const string SeasonPartitionValue = CompetitionIds.Bundesliga2026_27;
    public const string AuthorityEpochValue = "bundesliga-2026-27-typed-v1";

    private BundesligaPredictionAuthority(
        BundesligaPredictionAuthorityMode mode,
        string postingCommunity,
        string predictionSourceCommunity,
        string communityContext,
        BundesligaIdentitySeedReference postingSeed,
        BundesligaIdentitySeedReference sourceSeed,
        BundesligaCopyBindingReference? copyBinding)
    {
        Mode = mode;
        PostingCommunity = postingCommunity;
        PredictionSourceCommunity = predictionSourceCommunity;
        CommunityContext = communityContext;
        PostingSeed = postingSeed;
        SourceSeed = sourceSeed;
        CopyBinding = copyBinding;
    }

    public string SeasonPartition => SeasonPartitionValue;
    public string AuthorityEpoch => AuthorityEpochValue;
    public BundesligaPredictionAuthorityMode Mode { get; }
    public string PostingCommunity { get; }
    public string PredictionSourceCommunity { get; }
    public string CommunityContext { get; }
    public BundesligaIdentitySeedReference PostingSeed { get; }
    public BundesligaIdentitySeedReference SourceSeed { get; }
    public BundesligaCopyBindingReference? CopyBinding { get; }

    public static BundesligaPredictionAuthority CreateDirect(
        string seasonPartition,
        string authorityEpoch,
        string postingCommunity,
        string predictionSourceCommunity,
        string communityContext,
        BundesligaIdentitySeedReference postingSeed,
        BundesligaIdentitySeedReference sourceSeed)
    {
        ValidateFixedScope(seasonPartition, authorityEpoch);
        ValidateCommunities(postingCommunity, predictionSourceCommunity, communityContext);
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(sourceSeed);

        if (!string.Equals(postingCommunity, predictionSourceCommunity, StringComparison.Ordinal)
            || postingSeed != sourceSeed)
        {
            throw new InvalidDataException(
                "Direct generation requires the Prediction-source Community and seed to equal the Posting Community and seed.");
        }

        return new BundesligaPredictionAuthority(
            BundesligaPredictionAuthorityMode.Direct,
            postingCommunity,
            predictionSourceCommunity,
            communityContext,
            postingSeed,
            sourceSeed,
            null);
    }

    public static BundesligaPredictionAuthority CreateCopy(
        string seasonPartition,
        string authorityEpoch,
        string postingCommunity,
        string predictionSourceCommunity,
        string communityContext,
        BundesligaIdentitySeedReference postingSeed,
        BundesligaIdentitySeedReference sourceSeed,
        BundesligaCopyBindingReference copyBinding)
    {
        ValidateFixedScope(seasonPartition, authorityEpoch);
        ValidateCommunities(postingCommunity, predictionSourceCommunity, communityContext);
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(sourceSeed);
        ArgumentNullException.ThrowIfNull(copyBinding);

        return new BundesligaPredictionAuthority(
            BundesligaPredictionAuthorityMode.Copy,
            postingCommunity,
            predictionSourceCommunity,
            communityContext,
            postingSeed,
            sourceSeed,
            copyBinding);
    }

    private static void ValidateFixedScope(string seasonPartition, string authorityEpoch)
    {
        if (!string.Equals(seasonPartition, SeasonPartitionValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Prediction authority season must be exactly '{SeasonPartitionValue}'.");
        }

        if (!string.Equals(authorityEpoch, AuthorityEpochValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Prediction authority epoch must be exactly '{AuthorityEpochValue}'.");
        }
    }

    private static void ValidateCommunities(
        string postingCommunity,
        string predictionSourceCommunity,
        string communityContext)
    {
        BundesligaPredictionContractValidation.Community(postingCommunity, nameof(postingCommunity));
        BundesligaPredictionContractValidation.Community(predictionSourceCommunity, nameof(predictionSourceCommunity));
        BundesligaPredictionContractValidation.Community(communityContext, nameof(communityContext));
    }
}

public sealed record BundesligaPredictionRouteContract
{
    public BundesligaPredictionRouteContract(
        string routeId,
        BundesligaPredictionItemKind itemKind,
        BundesligaSeasonSubcompetition subcompetition)
    {
        BundesligaPredictionContractValidation.Identifier(routeId, nameof(routeId));
        RouteId = routeId;
        ItemKind = itemKind;
        Subcompetition = subcompetition;
    }

    public string RouteId { get; }
    public BundesligaPredictionItemKind ItemKind { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }
}

public sealed class BundesligaPredictionRouteCatalog
{
    private readonly IReadOnlyDictionary<string, BundesligaPredictionRouteContract> _contracts;

    public BundesligaPredictionRouteCatalog(IEnumerable<BundesligaPredictionRouteContract> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        var materialized = contracts.ToArray();
        if (materialized.Length == 0)
        {
            throw new InvalidDataException("At least one explicit prediction route contract is required.");
        }

        var duplicate = materialized
            .GroupBy(contract => contract.RouteId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate prediction route '{duplicate.Key}'.");
        }

        _contracts = materialized.ToDictionary(contract => contract.RouteId, StringComparer.Ordinal);
    }

    public BundesligaPredictionRouteContract Require(
        string routeId,
        BundesligaPredictionItemKind itemKind,
        BundesligaSeasonSubcompetition subcompetition)
    {
        if (!_contracts.TryGetValue(routeId, out var contract)
            || contract.ItemKind != itemKind
            || contract.Subcompetition != subcompetition)
        {
            throw new InvalidDataException(
                $"Prediction route '{routeId}' is not registered for {itemKind}/{subcompetition}.");
        }

        return contract;
    }
}

internal static class BundesligaPredictionContractValidation
{
    public static void Generation(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Generation must be positive.");
        }
    }

    public static void Sha256(string value, string parameterName)
    {
        if (!TypedContextCanonicalJson.IsLowercaseSha256(value))
        {
            throw new ArgumentException("Value must be a lowercase SHA-256.", parameterName);
        }
    }

    public static void Community(string value, string parameterName)
    {
        Identifier(value, parameterName);
        if (value.Any(character => !((character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9') || character == '-')))
        {
            throw new ArgumentException(
                "Community must be an exact lowercase path-safe slug.",
                parameterName);
        }
    }

    public static void Identifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Length > 256
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("Identifier is not exact canonical text.", parameterName);
        }
    }

    public static void ExactText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => character is '\r' or '\n' or '\0'))
        {
            throw new ArgumentException("Text is not exact single-line canonical text.", parameterName);
        }
    }
}
