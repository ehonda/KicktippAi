using OneOf.Types;
using TestUtilities.Options;

namespace OpenAiIntegration.Tests.OptionExtensionsTests;

/// <summary>
/// Tests for the OptionExtensions GetValueOr method with factory function
/// </summary>
public class OptionExtensions_GetValueOr_WithFactory_Tests
{
    string SetupValue(Option<string> v = null!)
    {
        return v.GetValueOr("empty");
    }

    void UseSetup()
    {
        var x = SetupValue("s");
        var z = SetupValue(new None());
        var y = SetupValue();
    }
    
    void Conversions()
    {
        // Construct from string
        var x = new Option<string>("s");
        // Construct from None
        var z = new Option<string>(new None());
        
        // Explicit conversion Option<string> -> string
        var a = (string)x;

        // Implicit conversion string -> Option<string>
        Option<string> y = "t";
        
        // Implicit conversion None -> Option<string>
        Option<string> w = new None();
        
        // Explicit conversion Option<string> -> None
        var b = (None)y;
    }

    void Match_Switch()
    {
        var x = new Option<string>("s");

        var z = x.Match(
            s => s,
            _ => "s");
        
        x.Switch(
            s =>
            {
                var c = s[0]; 
            },
            _ => {});
    }
    
    [Test]
    public async Task equals()
    {
        // Arrange
        Option<string> a = "a";
        Option<string> a2 = "a";
        Option<string> b = "b";

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(a2);
        await Assert.That(a.Equals(a2)).IsTrue();
        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a.Equals(b)).IsFalse();
    }
    
    [Test]
    public async Task get_none_from_default()
    {
        // Arrange
        Option<string> a = null!;
        var b = (None) a;

        // Act

        // Assert
        await Assert.That(a).IsAssignableTo<None>();
    }
    
    [Test]
    public async Task match_switch_from_default()
    {
        // Arrange
        Option<string> a = null!;
        var b = a.Match(
            s => s,
            _ => "s");

        var x = 0;
        a.Switch(
            _ => x = 1,
            _ => x = 2);

        // Act

        // Assert
        await Assert.That(b).IsEqualTo("s");
        await Assert.That(x).IsEqualTo(2);
    }
    
    [Test]
    public async Task default_value_from_default()
    {
        // Arrange
        Option<string> a = null!;
        var b = a.GetValueOr("b");
        var c = a.GetValueOr(() => "c");
        var d = a.GetValueOrDefault();

        // Act

        // Assert
        await Assert.That(b).IsEqualTo("b");
        await Assert.That(c).IsEqualTo("c");
        await Assert.That(d).IsNull();
    }
    
    // ------------------------- Option Struct -------------------------------------

    string SetupValueStruct(OptionStruct<string> v = default)
    {
        return "empty";
    }

    // `CancellationToken ctx = CancellationToken.None` does NOT ACTUALLY WORK! We have to use `default` as well
    void SetupCancellation(CancellationToken ctx = default)
    {
    }

    void UseSetupStruct()
    {
        var x = SetupValueStruct("s");
        var z = SetupValueStruct(new None());
        var y = SetupValueStruct();
    }

    void ConversionsStruct()
    {
        // Construct from string
        var x = new OptionStruct<string>("s");
        // Construct from None
        var z = new OptionStruct<string>(new None());

        // Explicit conversion Option<string> -> string
        var a = (string) x;

        // Implicit conversion string -> Option<string>
        OptionStruct<string> y = "t";

        // Implicit conversion None -> Option<string>
        OptionStruct<string> w = new None();

        // Explicit conversion Option<string> -> None
        var b = (None) y;
    }
    
    void Match_Switch_struct()
    {
        var x = new OptionStruct<string>("s");

        var z = x.Match(
            s => s,
            _ => "s");
        
        x.Switch(
            s =>
            {
                var c = s[0]; 
            },
            _ => {});
    }

    [Test]
    public async Task equalsStruct()
    {
        // Arrange
        OptionStruct<string> a = "a";
        OptionStruct<string> a2 = "a";
        OptionStruct<string> b = "b";

        // Act

        // Assert
        await Assert.That(a).IsEqualTo(a2);
        await Assert.That(a.Equals(a2)).IsTrue();
        await Assert.That(a).IsNotEqualTo(b);
        await Assert.That(a.Equals(b)).IsFalse();
    }
    
    [Test]
    public async Task get_none_from_default_struct()
    {
        // Arrange
        OptionStruct<string> a = default;
        var b = (None) a;

        // Act

        // Assert
        await Assert.That(a).IsAssignableTo<None>();
    }
    
    [Test]
    public async Task match_switch_from_default_struct()
    {
        // Arrange
        OptionStruct<string> a = default;
        var b = a.Match(
            s => s,
            _ => "s");

        var x = 0;
        a.Switch(
            _ => x = 1,
            _ => x = 2);

        // Act

        // Assert
        await Assert.That(b).IsEqualTo("s");
        await Assert.That(x).IsEqualTo(2);
    }

    [Test]
    public async Task default_value_from_default_struct()
    {
        // Arrange
        OptionStruct<string> a = default;
        var b = a.GetValueOr("b");
        var c = a.GetValueOr(() => "c");
        var d = a.GetValueOrDefault();

        // Act

        // Assert
        await Assert.That(b).IsEqualTo("b");
        await Assert.That(c).IsEqualTo("c");
        await Assert.That(d).IsNull();
    }

    // ------------------------- Option Struct -------------------------------------
        
    
    [Test]
    public async Task GetValueOr_with_some_value_returns_value()
    {
        // Arrange
        Option<string> option = "test value";

        // Act
        var result = option.GetValueOr(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("test value");
    }

    [Test]
    public async Task GetValueOr_with_none_calls_factory()
    {
        // Arrange
        Option<string> option = new None();

        // Act
        var result = option.GetValueOr(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOr_with_null_option_calls_factory()
    {
        // Arrange
        Option<string>? option = null;

        // Act
        var result = option.GetValueOr(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOr_with_default_bang_calls_factory()
    {
        // Arrange
        Option<string> option = default!;

        // Act
        var result = option.GetValueOr(() => "created");

        // Assert
        await Assert.That(result).IsEqualTo("created");
    }

    [Test]
    public async Task GetValueOr_with_none_only_calls_factory_once()
    {
        // Arrange
        Option<string> option = new None();
        var callCount = 0;

        // Act
        var result = option.GetValueOr(() =>
        {
            callCount++;
            return "created";
        });

        // Assert
        await Assert.That(result).IsEqualTo("created");
        await Assert.That(callCount).IsEqualTo(1);
    }
}
