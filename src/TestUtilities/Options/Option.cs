using OneOf;
using OneOf.Types;

namespace TestUtilities.Options;

/// <summary>
/// Represents an optional value - either <c>Some(T)</c> or <c>None</c>.
/// This is a strongly-typed wrapper around <c>OneOf&lt;T, None&gt;</c> for cleaner syntax.
/// Source-generated using OneOf.SourceGenerator.
/// </summary>
/// <typeparam name="T">The type of the value when present</typeparam>
[GenerateOneOf]
public partial class Option<T> : OneOfBase<T, None>
{
}

// Signature like `CancellationToken`:
//  ` public readonly struct CancellationToken : IEquatable<CancellationToken`
public readonly struct OptionStruct<T> : IEquatable<OptionStruct<T>>
{
    public static OptionStruct<T> None => default;
    
    private readonly OneOf<T, None> _implementation;

    public OptionStruct(OneOf<T, None>? implementation)
    {
        _implementation = implementation ?? new None();
    }
    
    // --------------------------------------------------------------------------
    // Get Value Or
    
    public T GetValueOr(T defaultValue)
    {
        return _implementation.Match(
            value => value,
            _ => defaultValue);
    }
    
    public T GetValueOr(Func<T> defaultFactory)
    {
        return _implementation.Match(
            value => value,
            _ => defaultFactory());
    }
    
    public T? GetValueOrDefault()
    {
        return _implementation.Match(
            value => value,
            _ => default(T));
    }
    
    // --------------------------------------------------------------------------
    // Match / Switch

    public TResult Match<TResult>(Func<T, TResult> f0, Func<None, TResult> f1)
        => _implementation.Match(f0, f1);

    public void Switch(Action<T> f0, Action<None> f1)
        => _implementation.Switch(f0, f1);
    
    // --------------------------------------------------------------------------
    // Conversions
    
    public static implicit operator OptionStruct<T>(T t) => new(t);
    public static explicit operator T(OptionStruct<T> option) => option._implementation.AsT0;

    public static implicit operator OptionStruct<T>(None none) => new(none);
    public static explicit operator None(OptionStruct<T> option) => option._implementation.AsT1;
    
    // --------------------------------------------------------------------------
    // Generated stuff for equality
    // TODO: Are these consistent?

    public bool Equals(OptionStruct<T> other) => _implementation.Equals(other._implementation);

    public override bool Equals(object? obj) => obj is OptionStruct<T> other && Equals(other);

    public override int GetHashCode() => _implementation.GetHashCode();

    public static bool operator ==(OptionStruct<T> left, OptionStruct<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(OptionStruct<T> left, OptionStruct<T> right)
    {
        return !(left == right);
    }
}
