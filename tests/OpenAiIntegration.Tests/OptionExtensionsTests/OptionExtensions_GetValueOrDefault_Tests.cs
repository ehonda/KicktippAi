using OneOf.Types;
using TestUtilities.Options;

namespace OpenAiIntegration.Tests.OptionExtensionsTests;

/// <summary>
/// Tests for the OptionExtensions GetValueOrDefault method
/// </summary>
public class OptionExtensions_GetValueOrDefault_Tests
{
    [Test]
    public async Task GetValueOrDefault_with_some_value_returns_value()
    {
        // Arrange
        Option<string> option = "test value";

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        await Assert.That(result).IsEqualTo("test value");
    }

    [Test]
    public async Task GetValueOrDefault_with_none_returns_null()
    {
        // Arrange
        Option<string> option = new None();

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetValueOrDefault_with_null_option_returns_null()
    {
        // Arrange
        Option<string>? option = null;

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetValueOrDefault_with_default_bang_returns_null()
    {
        // Arrange
        Option<string> option = default!;

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetValueOrDefault_with_value_type_returns_default_when_none()
    {
        // Arrange
        Option<int> option = new None();

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }
}
