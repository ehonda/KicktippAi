namespace FirebaseAdapter.Configuration;

/// <summary>
/// Configuration options for Firebase database connection.
/// </summary>
public class FirebaseOptions
{
    /// <summary>
    /// The section name for configuration binding.
    /// </summary>
    public const string SectionName = "Firebase";

    /// <summary>
    /// Firebase project ID.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Firebase service account private key JSON content.
    /// This should contain the complete JSON service account key.
    /// </summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Path to the service account JSON file.
    /// If specified, this will be used instead of ServiceAccountJson.
    /// </summary>
    public string? ServiceAccountPath { get; set; }

    /// <summary>
    /// Validates that the required configuration is present.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException("Firebase ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(ServiceAccountJson) && string.IsNullOrWhiteSpace(ServiceAccountPath))
        {
            throw new InvalidOperationException("Either Firebase ServiceAccountJson or ServiceAccountPath must be provided.");
        }
    }
}
