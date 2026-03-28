using System.Text.Json;

namespace Orchestrator.Commands.Observability.ExportExperimentItem;

public sealed record ExportedExperimentItem(
    MatchExperimentDatasetItem DatasetItem,
    MatchExperimentRunnerPayload RunnerPayload);

public sealed record MatchExperimentDatasetItem(
    string Id,
    JsonElement Input,
    MatchExperimentExpectedOutput ExpectedOutput,
    MatchExperimentMetadata Metadata);

public sealed record MatchExperimentExpectedOutput(
    int HomeGoals,
    int AwayGoals,
    string Availability);

public sealed record MatchExperimentMetadata(
    string CommunityContext,
    string Competition,
    int Matchday,
    string HomeTeam,
    string AwayTeam,
    string TippSpielId,
    string Model,
    bool IncludeJustification,
    DateTimeOffset PredictionCreatedAt,
    string PromptTemplatePath,
    IReadOnlyList<string> ContextDocumentNames,
    IReadOnlyList<MatchExperimentResolvedContextDocument> ResolvedContextDocuments,
    MatchExperimentOutcome Outcome);

public sealed record MatchExperimentResolvedContextDocument(
    string DocumentName,
    int Version,
    DateTimeOffset CreatedAt);

public sealed record MatchExperimentOutcome(
    int HomeGoals,
    int AwayGoals);

public sealed record MatchExperimentRunnerPayload(
    string SystemPrompt,
    string MatchJson);
