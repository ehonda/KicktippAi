namespace Core;

/// <summary>
/// Represents a KPI context document.
/// </summary>
public class KpiDocument
{
    public string DocumentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();

    public KpiDocument() { }

    public KpiDocument(string documentId, string name, string content, string description, string documentType, string[] tags)
    {
        DocumentId = documentId;
        Name = name;
        Content = content;
        Description = description;
        DocumentType = documentType;
        Tags = tags;
    }
}
