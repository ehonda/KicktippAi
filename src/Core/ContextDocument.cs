namespace Core;

/// <summary>
/// Represents a versioned context document.
/// </summary>
public class ContextDocument
{
    public string DocumentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }

    public ContextDocument() { }

    public ContextDocument(string documentName, string content, int version, DateTime createdAt)
    {
        DocumentName = documentName;
        Content = content;
        Version = version;
        CreatedAt = createdAt;
    }
}
