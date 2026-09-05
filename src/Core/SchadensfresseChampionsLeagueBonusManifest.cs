using System.Text.Json.Serialization;

namespace EHonda.KicktippAi.Core;

/// <summary>Canonical empty-context provenance for the frozen CL exception.</summary>
public sealed record SchadensfresseChampionsLeagueBonusManifest
{
    [JsonPropertyOrder(1)] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyOrder(2)] public string ProfileId { get; init; } = SchadensfresseChampionsLeagueBonusProfile.ProfileId;
    [JsonPropertyOrder(3)] public string Competition { get; init; } = SchadensfresseChampionsLeagueBonusProfile.Competition;
    [JsonPropertyOrder(4)] public string CommunityContext { get; init; } = SchadensfresseChampionsLeagueBonusProfile.Community;
    [JsonPropertyOrder(5)] public string KicktippQuestionId { get; init; } = string.Empty;
    [JsonPropertyOrder(6)] public string Deadline { get; init; } = SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc;
    [JsonPropertyOrder(7)] public string QuestionSetSha256 { get; init; } = SchadensfresseChampionsLeagueBonusProfile.QuestionSetSha256;
    [JsonPropertyOrder(8)] public string QuestionDefinitionSha256 { get; init; } = string.Empty;
    [JsonPropertyOrder(9)] public string SourceSnapshotSha256 { get; init; } = SchadensfresseChampionsLeagueBonusProfile.SourceSnapshotSha256;
    [JsonPropertyOrder(10)] public string HistoricalEvidenceQuestionSetSha256 { get; init; } = SchadensfresseChampionsLeagueBonusProfile.HistoricalEvidenceQuestionSetSha256;
    [JsonPropertyOrder(11)] public string PromptName { get; init; } = SchadensfresseChampionsLeagueBonusProfile.PromptName;
    [JsonPropertyOrder(12)] public int PromptVersion { get; init; } = SchadensfresseChampionsLeagueBonusProfile.PromptVersion;
    [JsonPropertyOrder(13)] public string PromptLabel { get; init; } = SchadensfresseChampionsLeagueBonusProfile.PromptLabel;
    [JsonPropertyOrder(14)] public string PromptNormalizedSha256 { get; init; } = SchadensfresseChampionsLeagueBonusProfile.PromptNormalizedSha256;
    [JsonPropertyOrder(15)] public string PromptProvider { get; init; } = "langfuse";
    [JsonPropertyOrder(16)] public string Model { get; init; } = SchadensfresseChampionsLeagueBonusProfile.Model;
    [JsonPropertyOrder(17)] public string ReasoningEffort { get; init; } = SchadensfresseChampionsLeagueBonusProfile.ReasoningEffort;
    [JsonPropertyOrder(18)] public int MaxOutputTokens { get; init; } = SchadensfresseChampionsLeagueBonusProfile.MaxOutputTokens;
    [JsonPropertyOrder(19)] public string ModelConfigKey { get; init; } = string.Empty;
    [JsonPropertyOrder(20)] public string ServicePolicyId { get; init; } = SchadensfresseChampionsLeagueBonusProfile.ServicePolicyId;
    [JsonPropertyOrder(21)] public string[] Documents { get; init; } = [];

    public void Validate(PredictionModelConfig modelConfig)
    {
        ArgumentNullException.ThrowIfNull(modelConfig);
        if (SchemaVersion != 1
            || !string.Equals(ProfileId, SchadensfresseChampionsLeagueBonusProfile.ProfileId, StringComparison.Ordinal)
            || !string.Equals(Competition, SchadensfresseChampionsLeagueBonusProfile.Competition, StringComparison.Ordinal)
            || !string.Equals(CommunityContext, SchadensfresseChampionsLeagueBonusProfile.Community, StringComparison.Ordinal)
            || !string.Equals(Deadline, SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc, StringComparison.Ordinal)
            || !string.Equals(QuestionSetSha256, SchadensfresseChampionsLeagueBonusProfile.QuestionSetSha256, StringComparison.Ordinal)
            || !string.Equals(SourceSnapshotSha256, SchadensfresseChampionsLeagueBonusProfile.SourceSnapshotSha256, StringComparison.Ordinal)
            || !string.Equals(HistoricalEvidenceQuestionSetSha256, SchadensfresseChampionsLeagueBonusProfile.HistoricalEvidenceQuestionSetSha256, StringComparison.Ordinal)
            || !string.Equals(PromptName, SchadensfresseChampionsLeagueBonusProfile.PromptName, StringComparison.Ordinal)
            || PromptVersion != SchadensfresseChampionsLeagueBonusProfile.PromptVersion
            || !string.Equals(PromptLabel, SchadensfresseChampionsLeagueBonusProfile.PromptLabel, StringComparison.Ordinal)
            || !string.Equals(PromptNormalizedSha256, SchadensfresseChampionsLeagueBonusProfile.PromptNormalizedSha256, StringComparison.Ordinal)
            || (PromptProvider is not "langfuse" and not "dedicated-cl-mirror")
            || !string.Equals(Model, SchadensfresseChampionsLeagueBonusProfile.Model, StringComparison.Ordinal)
            || !string.Equals(ReasoningEffort, SchadensfresseChampionsLeagueBonusProfile.ReasoningEffort, StringComparison.Ordinal)
            || MaxOutputTokens != SchadensfresseChampionsLeagueBonusProfile.MaxOutputTokens
            || !string.Equals(ServicePolicyId, SchadensfresseChampionsLeagueBonusProfile.ServicePolicyId, StringComparison.Ordinal)
            || Documents is null || Documents.Length != 0
            || string.IsNullOrWhiteSpace(KicktippQuestionId)
            || string.IsNullOrWhiteSpace(QuestionDefinitionSha256)
            || !string.Equals(ModelConfigKey, modelConfig.IdentityKey, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Schadensfresse Champions-League bonus manifest is not the frozen empty-context lineage.");
        }
    }
}
