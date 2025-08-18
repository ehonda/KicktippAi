namespace Core;

/// <summary>
/// Represents a KPI context document.
/// </summary>
public class KpiDocument
{
    public string DocumentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Version number for this document (starts at 0).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the document was created (UTC timestamp).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    public KpiDocument() { }

    public KpiDocument(string documentName, string content, string description, int version = 0, DateTimeOffset createdAt = default)
    {
        DocumentName = documentName;
        Content = content;
        Description = description;
        Version = version;
        CreatedAt = createdAt == default ? DateTimeOffset.UtcNow : createdAt;
    }
}
