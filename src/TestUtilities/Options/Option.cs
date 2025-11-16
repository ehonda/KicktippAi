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
