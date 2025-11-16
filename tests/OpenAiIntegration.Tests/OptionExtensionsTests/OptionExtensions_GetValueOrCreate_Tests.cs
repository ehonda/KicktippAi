using OneOf.Types;
using TestUtilities.Options;

namespace OpenAiIntegration.Tests.OptionExtensionsTests;

/// <summary>
/// Tests for the OptionExtensions GetValueOrCreate method
/// </summary>
public class OptionExtensions_GetValueOrCreate_Tests
{
    [Test]
    public async Task GetValueOrCreate_with_some_value_returns_value()
    {
        // Arrange
        Option<string> option = "test value";

        // Act
        var result = option.GetValueOrCreate(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("test value");
    }

    [Test]
    public async Task GetValueOrCreate_with_none_calls_factory()
    {
        // Arrange
        Option<string> option = new None();

        // Act
        var result = option.GetValueOrCreate(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOrCreate_with_null_option_calls_factory()
    {
        // Arrange
        Option<string>? option = null;

        // Act
        var result = option.GetValueOrCreate(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOrCreate_with_default_bang_calls_factory()
    {
        // Arrange
        Option<string> option = default!;

        // Act
        var result = option.GetValueOrCreate(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOrCreate_with_none_only_calls_factory_once()
    {
        // Arrange
        Option<string> option = new None();
        var callCount = 0;

        // Act
        var result = option.GetValueOrCreate(() =>
        {
            callCount++;
            return "created";
        });

        // Assert
        await Assert.That(result).IsEqualTo("created");
        await Assert.That(callCount).IsEqualTo(1);
    }
}
