using Google.Cloud.Firestore;
using NodaTime;

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
    /// Competition/season identifier (e.g., "bundesliga-2025-26").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = "bundesliga-2025-26";
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
    public string Competition { get; set; } = "bundesliga-2025-26";
}

/// <summary>
/// Firestore document model for storing bonus predictions.
/// </summary>
[FirestoreData]
public class FirestoreBonusPrediction
{
    /// <summary>
    /// Document ID - the bonus question ID.
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// The bonus question ID.
    /// </summary>
    [FirestoreProperty("questionId")]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>
    /// The bonus question text (for observability).
    /// </summary>
    [FirestoreProperty("questionText")]
    public string QuestionText { get; set; } = string.Empty;

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
    /// Competition/season identifier (e.g., "bundesliga-2025-26").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = "bundesliga-2025-26";
}

/// <summary>
/// Firestore document model for storing KPI context documents.
/// Used for storing contextual data for bonus predictions.
/// </summary>
[FirestoreData]
public class FirestoreKpiDocument
{
    /// <summary>
    /// Document ID - the KPI document identifier.
    /// </summary>
    [FirestoreDocumentId]
    public string? Id { get; set; }

    /// <summary>
    /// The document identifier.
    /// </summary>
    [FirestoreProperty("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// The document name (for observability).
    /// </summary>
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

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
    /// Document type identifier.
    /// </summary>
    [FirestoreProperty("documentType")]
    public string DocumentType { get; set; } = "kpi-context";

    /// <summary>
    /// Tags for categorizing the document.
    /// </summary>
    [FirestoreProperty("tags")]
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// When the document was created (UTC timestamp).
    /// </summary>
    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    /// <summary>
    /// When the document was last updated (UTC timestamp).
    /// </summary>
    [FirestoreProperty("updatedAt")]
    public Timestamp UpdatedAt { get; set; }

    /// <summary>
    /// Competition/season identifier (e.g., "bundesliga-2025-26").
    /// </summary>
    [FirestoreProperty("competition")]
    public string Competition { get; set; } = "bundesliga-2025-26";
}
