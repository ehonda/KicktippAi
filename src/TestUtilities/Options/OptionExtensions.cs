using OneOf.Types;

namespace TestUtilities.Options;

/// <summary>
/// Extension methods for working with Option&lt;T&gt; types
/// </summary>
public static class OptionExtensions
{
    /// <summary>
    /// Gets the value from the option if it exists, otherwise returns the provided default value
    /// </summary>
    public static T GetValueOr<T>(this Option<T> option, T defaultValue)
        => option.Match(
            value => value,
            none => defaultValue);

    /// <summary>
    /// Gets the value from the option if it exists, otherwise returns the result of calling the factory function
    /// </summary>
    public static T GetValueOrCreate<T>(this Option<T> option, Func<T> defaultFactory)
        => option.Match(
            value => value,
            none => defaultFactory());

    /// <summary>
    /// Gets the value from the option if it exists, otherwise returns default(T)
    /// </summary>
    public static T? GetValueOrDefault<T>(this Option<T> option)
        => option.Match(
            value => value,
            none => default(T));
}
