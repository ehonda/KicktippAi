using OneOf;
using OneOf.Types;

namespace TestUtilities.Options;

/// <summary>
/// Represents an optional value - either Some(T) or None.
/// This is a strongly-typed wrapper around OneOf&lt;T, None&gt; for cleaner syntax.
/// Source-generated using OneOf.SourceGenerator.
/// </summary>
/// <typeparam name="T">The type of the value when present</typeparam>
[GenerateOneOf]
public partial class Option<T> : OneOfBase<T, None>
{
}
