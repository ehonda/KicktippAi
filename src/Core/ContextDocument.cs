namespace EHonda.KicktippAi.Core;

/// <summary>
/// Represents a versioned context document.
/// </summary>
public class ContextDocument
{
    public string DocumentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ContextDocument() { }

    public ContextDocument(string documentName, string content, int version, DateTimeOffset createdAt)
    {
        DocumentName = documentName;
        Content = content;
        Version = version;
        CreatedAt = createdAt;
    }
}
