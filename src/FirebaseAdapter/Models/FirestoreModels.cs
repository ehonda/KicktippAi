using Google.Cloud.Firestore;
using NodaTime;
using EHonda.KicktippAi.Core;

namespace FirebaseAdapter.Models;

/// <summary>
/// Firestore document model for storing match predictions.
/// </summary>
[FirestoreData]
public class FirestoreMatchPrediction
{
    /// <summary>
    /// Document ID constructed from match details for uniqueness.
    /// Format: "{homeTeam}_{awayTeam}_{startsAtTicks}_{matchday}"
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// Home team name.
    /// </summary>
    [FirestoreProperty("homeTeam")]
    public string HomeTeam { get; set; } = string.Empty;

    /// <summary>
    /// Away team name.
    /// </summary>
    [FirestoreProperty("awayTeam")]
    public string AwayTeam { get; set; } = string.Empty;

    /// <summary>
    /// Match start time as UTC timestamp.
    /// </summary>
    [FirestoreProperty("startsAt")]
    public Timestamp StartsAt { get; set; }

    /// <summary>
    /// Match day number (1-34 for Bundesliga).
    /// </summary>
    [FirestoreProperty("matchday")]
    public int Matchday { get; set; }

    [FirestoreProperty("competitionSpecificData")]
    public FirestoreCompetitionSpecificMatchData? CompetitionSpecificData { get; set; }

    /// <summary>
    /// Predicted home team goals.
    /// </summary>
    [FirestoreProperty("homeGoals")]
    public int HomeGoals { get; set; }

    /// <summary>
    /// Predicted away team goals.
    /// </summary>
    [FirestoreProperty("awayGoals")]
    public int AwayGoals { get; set; }

    /// <summary>
    /// Optional structured justification payload (stored as JSON string) explaining the predicted outcome.
    /// </summary>
    [FirestoreProperty("justification")]
    public string? Justification { get; set; }

    /// <summary>
    /// When the prediction was created (UTC timestamp).
    /// </summary>
    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    /// <summary>
    /// When the prediction was last updated (UTC timestamp).
    /// </summary>
    [FirestoreProperty("updatedAt")]
    public Timestamp UpdatedAt { get; set; }

    /// <summary>
    /// Competition/season identifier (e.g., "bundesliga-2026-27").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    /// <summary>
    /// The AI model used to generate this prediction (e.g., "gpt-4o", "o1-mini").
    /// </summary>
    [FirestoreProperty("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Stable identity key for the model configuration used to generate this prediction.
    /// </summary>
    [FirestoreProperty("modelConfigKey")]
    public string? ModelConfigKey { get; set; }

    /// <summary>
    /// Optional OpenAI reasoning effort used to generate this prediction.
    /// </summary>
    [FirestoreProperty("reasoningEffort")]
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Explicit maximum output-token cap used to generate this prediction.
    /// </summary>
    [FirestoreProperty("maxOutputTokens")]
    public int? MaxOutputTokenCount { get; set; }

    /// <summary>
    /// Hosted prompt name used to generate this prediction.
    /// </summary>
    [FirestoreProperty("promptName")]
    public string? PromptName { get; set; }

    /// <summary>
    /// Exact hosted prompt version used to generate this prediction.
    /// </summary>
    [FirestoreProperty("promptVersion")]
    public int? PromptVersion { get; set; }

    /// <summary>
    /// JSON string containing the token usage object from the API (e.g., completion_tokens, prompt_tokens, total_tokens).
    /// </summary>
    [FirestoreProperty("tokenUsage")]
    public string TokenUsage { get; set; } = string.Empty;

    /// <summary>
    /// Cost in USD to generate this prediction.
    /// </summary>
    [FirestoreProperty("cost")]
    public double Cost { get; set; }

    /// <summary>
    /// The community context (community rules) used to generate this prediction.
    /// </summary>
    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    /// <summary>
    /// Names of context documents that were used as input for generating this prediction.
    /// Used to check if prediction is outdated compared to context changes.
    /// </summary>
    [FirestoreProperty("contextDocumentNames")]
    public string[] ContextDocumentNames { get; set; } = [];

    /// <summary>Canonical JSON resolved-context provenance for Bundesliga snapshot-backed prompts.</summary>
    [FirestoreProperty("resolvedContextManifest")]
    public string? ResolvedContextManifest { get; set; }

    /// <summary>
    /// Reprediction index for tracking prediction versions.
    /// Starts at 0 for the first prediction, increments for each reprediction.
    /// </summary>
    [FirestoreProperty("repredictionIndex")]
    public int RepredictionIndex { get; set; } = 0;
}

/// <summary>
/// Firestore document model for storing match information without predictions.
/// Used for managing match days and match schedules.
/// </summary>
[FirestoreData]
public class FirestoreMatch
{
    /// <summary>
    /// Document ID constructed from match details.
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// Home team name.
    /// </summary>
    [FirestoreProperty("homeTeam")]
    public string HomeTeam { get; set; } = string.Empty;

    /// <summary>
    /// Away team name.
    /// </summary>
    [FirestoreProperty("awayTeam")]
    public string AwayTeam { get; set; } = string.Empty;

    /// <summary>
    /// Match start time as UTC timestamp.
    /// </summary>
    [FirestoreProperty("startsAt")]
    public Timestamp StartsAt { get; set; }

    /// <summary>
    /// Match day number (1-34 for Bundesliga).
    /// </summary>
    [FirestoreProperty("matchday")]
    public int Matchday { get; set; }

    /// <summary>
    /// Competition/season identifier.
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the match has been cancelled.
    /// Cancelled matches show "Abgesagt" on Kicktipp instead of a scheduled time.
    /// See docs/features/cancelled-matches.md for design rationale.
    /// </summary>
    [FirestoreProperty("isCancelled")]
    public bool IsCancelled { get; set; } = false;

    [FirestoreProperty("competitionSpecificData")]
    public FirestoreCompetitionSpecificMatchData? CompetitionSpecificData { get; set; }
}

[FirestoreData]
public class FirestoreCompetitionSpecificMatchData
{
    [FirestoreProperty("type")]
    public string Type { get; set; } = string.Empty;

    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    [FirestoreProperty("kicktippRoundName")]
    public string? KicktippRoundName { get; set; }

    [FirestoreProperty("stage")]
    public string Stage { get; set; } = string.Empty;

    [FirestoreProperty("resultBasis")]
    public string ResultBasis { get; set; } = string.Empty;
}

/// <summary>
/// Firestore document model for storing bonus predictions.
/// </summary>
[FirestoreData]
public class FirestoreBonusPrediction
{
    /// <summary>
    /// Document ID - unique identifier for the prediction.
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// The bonus question text (for observability).
    /// </summary>
    [FirestoreProperty("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Optional exact Kicktipp question ID for specialized profiles.</summary>
    [FirestoreProperty("questionId")]
    public string? QuestionId { get; set; }

    /// <summary>Optional exact UTC deadline token for specialized profiles.</summary>
    [FirestoreProperty("questionDeadline")]
    public string? QuestionDeadline { get; set; }

    /// <summary>
    /// Selected option IDs for the bonus question.
    /// </summary>
    [FirestoreProperty("selectedOptionIds")]
    public string[] SelectedOptionIds { get; set; } = [];

    /// <summary>
    /// Selected option texts (for observability).
    /// </summary>
    [FirestoreProperty("selectedOptionTexts")]
    public string[] SelectedOptionTexts { get; set; } = [];

    /// <summary>
    /// When the bonus prediction was created (UTC timestamp).
    /// </summary>
    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    /// <summary>
    /// When the bonus prediction was last updated (UTC timestamp).
    /// </summary>
    [FirestoreProperty("updatedAt")]
    public Timestamp UpdatedAt { get; set; }

    /// <summary>
    /// Competition/season identifier (e.g., "bundesliga-2026-27").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    /// <summary>
    /// The AI model used to generate this prediction (e.g., "gpt-4o", "o1-mini").
    /// </summary>
    [FirestoreProperty("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Stable identity key for the model configuration used to generate this prediction.
    /// </summary>
    [FirestoreProperty("modelConfigKey")]
    public string? ModelConfigKey { get; set; }

    /// <summary>
    /// Optional OpenAI reasoning effort used to generate this prediction.
    /// </summary>
    [FirestoreProperty("reasoningEffort")]
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Explicit maximum output-token cap used to generate this prediction.
    /// </summary>
    [FirestoreProperty("maxOutputTokens")]
    public int? MaxOutputTokenCount { get; set; }

    /// <summary>
    /// Hosted prompt name used to generate this prediction.
    /// </summary>
    [FirestoreProperty("promptName")]
    public string? PromptName { get; set; }

    /// <summary>
    /// Exact hosted prompt version used to generate this prediction.
    /// </summary>
    [FirestoreProperty("promptVersion")]
    public int? PromptVersion { get; set; }

    /// <summary>
    /// JSON string containing the token usage object from the API (e.g., completion_tokens, prompt_tokens, total_tokens).
    /// </summary>
    [FirestoreProperty("tokenUsage")]
    public string TokenUsage { get; set; } = string.Empty;

    /// <summary>
    /// Cost in USD to generate this prediction.
    /// </summary>
    [FirestoreProperty("cost")]
    public double Cost { get; set; }

    /// <summary>
    /// The community context (community rules) used to generate this prediction.
    /// </summary>
    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    /// <summary>
    /// Names of context documents that were used as input for generating this prediction.
    /// Used to check if prediction is outdated compared to context changes.
    /// </summary>
    [FirestoreProperty("contextDocumentNames")]
    public string[] ContextDocumentNames { get; set; } = [];

    /// <summary>Canonical JSON resolved-context provenance for Bundesliga bonus prompts.</summary>
    [FirestoreProperty("resolvedBonusContextManifest")]
    public string? ResolvedBonusContextManifest { get; set; }

    /// <summary>Canonical empty-context provenance for the narrow CL exception.</summary>
    [FirestoreProperty("schadensfresseChampionsLeagueBonusManifest")]
    public string? SchadensfresseChampionsLeagueBonusManifest { get; set; }

    /// <summary>
    /// Canonical JSON provenance for the complete normalized question and source option set.
    /// Legacy documents legitimately omit this field and cannot be reused across communities.
    /// </summary>
    [FirestoreProperty("bonusQuestionCompatibilityManifest")]
    public string? BonusQuestionCompatibilityManifest { get; set; }

    /// <summary>
    /// Reprediction index for tracking prediction versions.
    /// Starts at 0 for the first prediction, increments for each reprediction.
    /// </summary>
    [FirestoreProperty("repredictionIndex")]
    public int RepredictionIndex { get; set; } = 0;
}

/// <summary>
/// Firestore document model for storing KPI context documents.
/// Used for storing contextual data for bonus predictions.
/// </summary>
[FirestoreData]
public class FirestoreKpiDocument
{
    /// <summary>
    /// Document ID - constructed from document name, community context, and version.
    /// Format: "{documentName}_{communityContext}_{version}"
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// The document name (for observability and context lookup consistency).
    /// </summary>
    [FirestoreProperty("documentName")]
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// The document content (TSV format).
    /// </summary>
    [FirestoreProperty("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document description.
    /// </summary>
    [FirestoreProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Version number for this document (starts at 0).
    /// </summary>
    [FirestoreProperty("version")]
    public int Version { get; set; }

    /// <summary>
    /// When the document was created (UTC timestamp).
    /// </summary>
    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    /// <summary>
    /// Competition/season identifier (e.g., "bundesliga-2026-27").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    /// <summary>
    /// Community context for filtering KPI documents.
    /// </summary>
    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    [FirestoreProperty("publicationSet")]
    public string PublicationSet { get; set; } = string.Empty;
}

/// <summary>
/// Firestore document model for storing versioned context documents.
/// Used for storing contextual data retrieved from Kicktipp for matchday predictions.
/// </summary>
[FirestoreData]
public class FirestoreContextDocument
{
    /// <summary>
    /// Document ID - constructed from document name, community context, and version.
    /// Format: "{documentName}_{communityContext}_{version}"
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// The context document name (e.g., "bundesliga-standings.csv", "recent-history-fcb.csv").
    /// </summary>
    [FirestoreProperty("documentName")]
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// The document content (CSV format).
    /// </summary>
    [FirestoreProperty("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Version number for this document (starts at 0).
    /// </summary>
    [FirestoreProperty("version")]
    public int Version { get; set; }

    /// <summary>
    /// When the document was created (UTC timestamp).
    /// </summary>
    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    /// <summary>
    /// Competition/season identifier (e.g., "bundesliga-2026-27").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    /// <summary>
    /// Community context for filtering context documents.
    /// </summary>
    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    [FirestoreProperty("publicationSet")]
    public string PublicationSet { get; set; } = string.Empty;
}

[FirestoreData]
public class FirestoreDocumentPublicationHead
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    [FirestoreProperty("publicationSet")]
    public string PublicationSet { get; set; } = string.Empty;

    [FirestoreProperty("snapshotId")]
    public string SnapshotId { get; set; } = string.Empty;
}

[FirestoreData]
public class FirestoreDocumentPublicationSnapshot
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;

    [FirestoreProperty("publicationSet")]
    public string PublicationSet { get; set; } = string.Empty;

    [FirestoreProperty("snapshotId")]
    public string SnapshotId { get; set; } = string.Empty;

    [FirestoreProperty("previousSnapshotId")]
    public string? PreviousSnapshotId { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("metadataJson")]
    public string MetadataJson { get; set; } = string.Empty;

    [FirestoreProperty("documents")]
    public List<FirestoreDocumentPublicationEntry> Documents { get; set; } = [];
}

[FirestoreData]
public class FirestoreDocumentPublicationEntry
{
    [FirestoreProperty("kind")]
    public string Kind { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("version")]
    public int Version { get; set; }

    [FirestoreProperty("contentSha256")]
    public string ContentSha256 { get; set; } = string.Empty;
}

/// <summary>
/// Firestore document model for storing persisted match outcomes collected from Kicktipp.
/// </summary>
[FirestoreData]
public class FirestoreMatchOutcome
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty("homeTeam")]
    public string HomeTeam { get; set; } = string.Empty;

    [FirestoreProperty("awayTeam")]
    public string AwayTeam { get; set; } = string.Empty;

    [FirestoreProperty("startsAt")]
    public Timestamp StartsAt { get; set; }

    [FirestoreProperty("matchday")]
    public int Matchday { get; set; }

    [FirestoreProperty("homeGoals")]
    public int? HomeGoals { get; set; }

    [FirestoreProperty("awayGoals")]
    public int? AwayGoals { get; set; }

    [FirestoreProperty("availability")]
    public string Availability { get; set; } = nameof(MatchOutcomeAvailability.Pending);

    [FirestoreProperty("tippspielId")]
    public string? TippSpielId { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public Timestamp UpdatedAt { get; set; }

    [FirestoreProperty("competition")]
    public string Competition { get; set; } = string.Empty;

    [FirestoreProperty("communityContext")]
    public string CommunityContext { get; set; } = string.Empty;
}
