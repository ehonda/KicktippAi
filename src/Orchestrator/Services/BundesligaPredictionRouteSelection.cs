using EHonda.KicktippAi.Core;
using OpenAiIntegration;

namespace Orchestrator.Services;

public sealed class BundesligaPredictionRouteSelection
{
    private BundesligaPredictionRouteSelection(
        string selectionId,
        BundesligaPredictionRouteContract route,
        string communityContext,
        string profileId,
        BundesligaGenerationInputContractReference generationInputContract,
        PredictionPromptExecutionRequirement promptRequirement,
        PredictionCopyCompatibilityContractV2? copyCompatibility)
    {
        SelectionId = selectionId;
        Route = route;
        CommunityContext = communityContext;
        ProfileId = profileId;
        GenerationInputContract = generationInputContract;
        PromptRequirement = promptRequirement;
        CopyCompatibility = copyCompatibility;
    }

    public string SelectionId { get; }
    public BundesligaPredictionRouteContract Route { get; }
    public string CommunityContext { get; }
    public string ProfileId { get; }
    public BundesligaGenerationInputContractReference GenerationInputContract { get; }
    public PredictionPromptExecutionRequirement PromptRequirement { get; }
    public PredictionModelConfig ModelConfig => PromptRequirement.ModelConfig;
    public PredictionCopyCompatibilityContractV2? CopyCompatibility { get; }

    public static BundesligaPredictionRouteSelection Create(
        string selectionId,
        BundesligaPredictionRouteContract route,
        string communityContext,
        string profileId,
        BundesligaGenerationInputContractReference generationInputContract,
        PredictionPromptExecutionRequirement promptRequirement,
        PredictionCopyCompatibilityContractV2? copyCompatibility = null)
    {
        RequireIdentifier(selectionId, nameof(selectionId));
        ArgumentNullException.ThrowIfNull(route);
        RequireCommunity(communityContext, nameof(communityContext));
        RequireIdentifier(profileId, nameof(profileId));
        ArgumentNullException.ThrowIfNull(generationInputContract);
        ArgumentNullException.ThrowIfNull(promptRequirement);

        if (copyCompatibility is not null
            && (!string.Equals(copyCompatibility.RouteId, route.RouteId, StringComparison.Ordinal)
                || copyCompatibility.ItemKind != route.ItemKind
                || copyCompatibility.Subcompetition != route.Subcompetition
                || !string.Equals(copyCompatibility.CommunityContext, communityContext, StringComparison.Ordinal)
                || copyCompatibility.Model != promptRequirement.ModelConfig))
        {
            throw new InvalidDataException(
                "Registered copy compatibility contract conflicts with its route selection.");
        }
        if (copyCompatibility is not null)
        {
            promptRequirement.RequireProvenance(copyCompatibility.Prompt);
        }

        return new BundesligaPredictionRouteSelection(
            selectionId,
            route,
            communityContext,
            profileId,
            generationInputContract,
            promptRequirement,
            copyCompatibility);
    }

    private static void RequireIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Length > 256
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("Identifier is not exact canonical text.", parameterName);
        }
    }

    private static void RequireCommunity(string value, string parameterName)
    {
        RequireIdentifier(value, parameterName);
        if (value.Any(character => !((character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9') || character == '-')))
        {
            throw new ArgumentException(
                "Community Context must be an exact lowercase path-safe slug.",
                parameterName);
        }
    }

    internal BundesligaTypedCurrentIdentity CreateCurrentIdentity() =>
        BundesligaTypedCurrentIdentity.Create(Route.RouteId, ProfileId, GenerationInputContract);
}
