using OneOf.Types;

namespace TestUtilities.Options;

/// <summary>
/// Extension methods for working with <see cref="Option{T}"/> types.
/// </summary>
public static class OptionExtensions
{
    extension<T>(Option<T>? option)
    {
        /// <summary>
        /// Gets the value from the option if it exists, otherwise returns the provided default value.
        /// If the option is <see langword="null"/>, it is treated as <see cref="None"/>.
        /// </summary>
        public T GetValueOr(T defaultValue)
        {
            option ??= new None();
            return option.Match(
                value => value,
                _ => defaultValue);
        }

        /// <summary>
        /// Gets the value from the option if it exists, otherwise returns the result of calling the factory function.
        /// If the option is <see langword="null"/>, it is treated as <see cref="None"/>.
        /// </summary>
        public T GetValueOr(Func<T> defaultFactory)
        {
            option ??= new None();
            return option.Match(
                value => value,
                _ => defaultFactory());
        }

        /// <summary>
        /// Gets the value from the option if it exists, otherwise returns default(T).
        /// If the option is <see langword="null"/>, it is treated as <see cref="None"/>.
        /// </summary>
        public T? GetValueOrDefault()
        {
            option ??= new None();
            return option.Match(
                value => value,
                _ => default(T));
        }
    }
}
