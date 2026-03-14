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

        var resolvedContextDocuments = new List<ResolvedContextDocumentVersion>();
        foreach (var documentName in predictionMetadata.ContextDocumentNames)
        {
            var contextDocument = await _contextRepository.GetContextDocumentByTimestampAsync(
                documentName,
                predictionMetadata.CreatedAt,
                communityContext,
                cancellationToken);

            if (contextDocument is null)
            {
                throw new InvalidOperationException(
                    $"Failed to reconstruct prompt for {match.HomeTeam} vs {match.AwayTeam}: " +
                    $"document '{documentName}' had no version at or before {predictionMetadata.CreatedAt:O}.");
            }

            resolvedContextDocuments.Add(new ResolvedContextDocumentVersion(
                contextDocument.DocumentName,
                contextDocument.Version,
                contextDocument.CreatedAt,
                contextDocument.Content));
        }

        var (template, templatePath) = _templateProvider.LoadMatchTemplate(model, includeJustification);
        var systemPrompt = PredictionPromptComposer.BuildSystemPrompt(
            template,
            resolvedContextDocuments.Select(document => new DocumentContext(document.DocumentName, document.Content)));

        return new ReconstructedMatchPredictionPrompt(
            match,
            model,
            communityContext,
            includeJustification,
            predictionMetadata.CreatedAt,
            templatePath,
            systemPrompt,
            PredictionPromptComposer.CreateMatchJson(match),
            predictionMetadata.ContextDocumentNames,
            resolvedContextDocuments);
    }
}
