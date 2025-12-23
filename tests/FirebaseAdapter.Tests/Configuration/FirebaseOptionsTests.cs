using FirebaseAdapter.Configuration;
using TUnit.Core;

namespace FirebaseAdapter.Tests.Configuration;

/// <summary>
/// Tests for FirebaseOptions.Validate method.
/// </summary>
public class FirebaseOptionsTests
{
    [Test]
    public async Task Validate_throws_when_ProjectId_is_null()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = null!,
            ServiceAccountJson = "valid-json"
        };

        // Act & Assert
        await Assert.That(() => options.Validate())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ProjectId");
    }

    [Test]
    public async Task Validate_throws_when_ProjectId_is_empty()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = "",
            ServiceAccountJson = "valid-json"
        };

        // Act & Assert
        await Assert.That(() => options.Validate())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ProjectId");
    }

    [Test]
    public async Task Validate_throws_when_ProjectId_is_whitespace()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = "   ",
            ServiceAccountJson = "valid-json"
        };

        // Act & Assert
        await Assert.That(() => options.Validate())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ProjectId");
    }

    [Test]
    public async Task Validate_throws_when_both_ServiceAccountJson_and_ServiceAccountPath_are_missing()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = "valid-project-id",
            ServiceAccountJson = "",
            ServiceAccountPath = null
        };

        // Act & Assert
        await Assert.That(() => options.Validate())
            .Throws<InvalidOperationException>()
            .WithMessageContaining("ServiceAccountJson")
            .Or.WithMessageContaining("ServiceAccountPath");
    }

    [Test]
    public void Validate_succeeds_when_ProjectId_and_ServiceAccountJson_are_provided()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = "valid-project-id",
            ServiceAccountJson = "valid-json"
        };

        // Act & Assert - no exception thrown
        options.Validate();
    }

    [Test]
    public void Validate_succeeds_when_ProjectId_and_ServiceAccountPath_are_provided()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ProjectId = "valid-project-id",
            ServiceAccountPath = "/path/to/service-account.json"
        };

        // Act & Assert - no exception thrown
        options.Validate();
    }

    [Test]
    public void Validate_succeeds_when_both_ServiceAccountJson_and_ServiceAccountPath_are_provided()
    {
        // Arrange - ServiceAccountPath takes precedence
        var options = new FirebaseOptions
        {
            ProjectId = "valid-project-id",
            ServiceAccountJson = "valid-json",
            ServiceAccountPath = "/path/to/service-account.json"
        };

        // Act & Assert - no exception thrown
        options.Validate();
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "TUnitAssertions",
        "TUnitAssertions0005:Assert.That(...) should not be used with a constant value",
        Justification = "Intentionally testing the constant value of SectionName.")]
    public async Task SectionName_is_Firebase()
    {
        // Assert
        await Assert.That(FirebaseOptions.SectionName).IsEqualTo("Firebase");
    }

    [Test]
    public async Task Default_ProjectId_is_empty_string()
    {
        // Arrange
        var options = new FirebaseOptions();

        // Assert
        await Assert.That(options.ProjectId).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Default_ServiceAccountJson_is_empty_string()
    {
        // Arrange
        var options = new FirebaseOptions();

        // Assert
        await Assert.That(options.ServiceAccountJson).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Default_ServiceAccountPath_is_null()
    {
        // Arrange
        var options = new FirebaseOptions();

        // Assert
        await Assert.That(options.ServiceAccountPath).IsNull();
    }
}
