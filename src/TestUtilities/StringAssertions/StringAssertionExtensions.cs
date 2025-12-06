using System.Runtime.CompilerServices;
using TUnit.Assertions.Core;

namespace TestUtilities.StringAssertions;

public static class StringAssertionExtensions
{
    public static StringEqualsWithNormalizedLineEndingsAssertion IsEqualToWithNormalizedLineEndings(
        this IAssertionSource<string> source,
        string expected,
        [CallerArgumentExpression(nameof(expected))] string? expectedExpression = null)
    {
        source.Context.ExpressionBuilder.Append($".IsEqualToWithNormalizedLineEndings({expectedExpression})");
        return new StringEqualsWithNormalizedLineEndingsAssertion(source.Context, expected);
    }
}
