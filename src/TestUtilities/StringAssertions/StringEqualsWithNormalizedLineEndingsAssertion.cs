using TUnit.Assertions.Core;

namespace TestUtilities.StringAssertions;

public class StringEqualsWithNormalizedLineEndingsAssertion : Assertion<string>
{
    private readonly string _expected;

    public StringEqualsWithNormalizedLineEndingsAssertion(AssertionContext<string> context, string expected)
        : base(context)
    {
        _expected = expected;
    }

    protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<string> metadata)
    {
        var actual = metadata.Value;
        
        if (actual is null)
        {
            return Task.FromResult(AssertionResult.Failed("Actual string was null"));
        }

        var normalizedActual = Normalize(actual);
        var normalizedExpected = Normalize(_expected);

        if (normalizedActual == normalizedExpected)
        {
            return Task.FromResult(AssertionResult.Passed);
        }

        return Task.FromResult(AssertionResult.Failed(
            $"""
             Strings do not match (ignoring line endings).
             Expected:
             {_expected}
             
             Actual:
             {actual}
             """));
    }

    protected override string GetExpectation()
        => $"to be equal to \"{_expected}\" (ignoring line endings)";

    private static string Normalize(string input)
    {
        return input.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
