using OneOf.Types;
using TestUtilities.Options;

namespace OpenAiIntegration.Tests.OptionExtensionsTests;

/// <summary>
/// Tests for the OptionExtensions GetValueOr method
/// </summary>
public class OptionExtensions_GetValueOr_Tests
{
    [Test]
    public async Task GetValueOr_with_some_value_returns_value()
    {
        // Arrange
        Option<string> option = "test value";

        // Act
        var result = option.GetValueOr("default");

        // Assert
        await Assert.That(result).IsEqualTo("test value");
    }

    [Test]
    public async Task GetValueOr_with_none_returns_default()
    {
        // Arrange
        Option<string> option = new None();

        // Act
        var result = option.GetValueOr("default");

        // Assert
        await Assert.That(result).IsEqualTo("default");
    }

    [Test]
    public async Task GetValueOr_with_null_option_returns_default()
    {
        // Arrange
        Option<string>? option = null;

        // Act
        var result = option.GetValueOr("default");

        // Assert
        await Assert.That(result).IsEqualTo("default");
    }

    [Test]
    public async Task GetValueOr_with_default_bang_returns_default()
    {
        // Arrange
        Option<string> option = default!;

        // Act
        var result = option.GetValueOr("default");

        // Assert
        await Assert.That(result).IsEqualTo("default");
    }
}
