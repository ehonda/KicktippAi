using System.Text.Json;

namespace Orchestrator.Commands.Observability.ExportExperimentDataset;

public sealed record ExportedExperimentDataset(
    string DatasetName,
    IReadOnlyList<HostedMatchExperimentDatasetItem> Items);

public sealed record HostedMatchExperimentDatasetItem(
    string Id,
    JsonElement Input,
    HostedMatchExperimentExpectedOutput ExpectedOutput,
    HostedMatchExperimentMetadata Metadata);

public sealed record HostedMatchExperimentExpectedOutput(
    int HomeGoals,
    int AwayGoals);

public sealed record HostedMatchExperimentMetadata(
    string Competition,
    string Season,
    string CommunityContext,
    int Matchday,
    string MatchdayLabel,
    string HomeTeam,
    string AwayTeam,
    string TippSpielId);
