using EHonda.KicktippAi.Core;

namespace OpenAiIntegration;

/// <summary>
/// Reconstructs historical match prompt inputs from stored prediction metadata and versioned context documents.
/// </summary>
public sealed class MatchPromptReconstructionService : IMatchPromptReconstructionService
{
    private readonly IPredictionRepository _predictionRepository;
    private readonly IContextRepository _contextRepository;
    private readonly IInstructionsTemplateProvider _templateProvider;

    public MatchPromptReconstructionService(
        IPredictionRepository predictionRepository,
        IContextRepository contextRepository,
        IInstructionsTemplateProvider templateProvider)
    {
        _predictionRepository = predictionRepository ?? throw new ArgumentNullException(nameof(predictionRepository));
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
    }

    public async Task<ReconstructedMatchPredictionPrompt?> ReconstructMatchPredictionPromptAsync(
        Match match,
        string model,
        string communityContext,
        bool includeJustification = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var predictionMetadata = await _predictionRepository.GetPredictionMetadataAsync(
            match,
            model,
            communityContext,
            cancellationToken);

        if (predictionMetadata is null)
        {
            return null;
        }

        return await ReconstructMatchPredictionPromptAtTimestampAsync(
            match,
            model,
            communityContext,
            predictionMetadata.CreatedAt,
            predictionMetadata.ContextDocumentNames,
            includeJustification: includeJustification,
            cancellationToken: cancellationToken);
    }

    public async Task<ReconstructedMatchPredictionPrompt> ReconstructMatchPredictionPromptAtTimestampAsync(
        Match match,
        string model,
        string communityContext,
        DateTimeOffset promptTimestamp,
        IReadOnlyList<string> requiredContextDocumentNames,
        IReadOnlyList<string>? optionalContextDocumentNames = null,
        bool includeJustification = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        ArgumentNullException.ThrowIfNull(requiredContextDocumentNames);

        var resolvedContextDocuments = await ResolveContextDocumentsAsync(
            match,
            communityContext,
            promptTimestamp,
            requiredContextDocumentNames,
            optionalContextDocumentNames ?? [],
            cancellationToken);

        var (template, templatePath) = _templateProvider.LoadMatchTemplate(model, includeJustification);
        var systemPrompt = PredictionPromptComposer.BuildSystemPrompt(
            template,
            resolvedContextDocuments.Select(document => new DocumentContext(document.DocumentName, document.Content)));

        return new ReconstructedMatchPredictionPrompt(
            match,
            model,
            communityContext,
            includeJustification,
            promptTimestamp,
            templatePath,
            systemPrompt,
            PredictionPromptComposer.CreateMatchJson(match),
            resolvedContextDocuments.Select(document => document.DocumentName).ToArray(),
            resolvedContextDocuments);
    }

    private async Task<IReadOnlyList<ResolvedContextDocumentVersion>> ResolveContextDocumentsAsync(
        Match match,
        string communityContext,
        DateTimeOffset promptTimestamp,
        IReadOnlyList<string> requiredContextDocumentNames,
        IReadOnlyList<string> optionalContextDocumentNames,
        CancellationToken cancellationToken)
    {
        var resolvedContextDocuments = new List<ResolvedContextDocumentVersion>();

        foreach (var documentName in requiredContextDocumentNames)
        {
            var contextDocument = await _contextRepository.GetContextDocumentByTimestampAsync(
                documentName,
                promptTimestamp,
                communityContext,
                cancellationToken);

            if (contextDocument is null)
            {
                throw new InvalidOperationException(
                    $"Failed to reconstruct prompt for {match.HomeTeam} vs {match.AwayTeam}: " +
                    $"document '{documentName}' had no version at or before {promptTimestamp:O}.");
            }

            resolvedContextDocuments.Add(new ResolvedContextDocumentVersion(
                contextDocument.DocumentName,
                contextDocument.Version,
                contextDocument.CreatedAt,
                contextDocument.Content));
        }

        foreach (var documentName in optionalContextDocumentNames)
        {
            var contextDocument = await _contextRepository.GetContextDocumentByTimestampAsync(
                documentName,
                promptTimestamp,
                communityContext,
                cancellationToken);

            if (contextDocument is null)
            {
                continue;
            }

            resolvedContextDocuments.Add(new ResolvedContextDocumentVersion(
                contextDocument.DocumentName,
                contextDocument.Version,
                contextDocument.CreatedAt,
                contextDocument.Content));
        }

        return resolvedContextDocuments;
    }
}
