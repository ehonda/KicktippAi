using TestUtilities.StringAssertions;
using TUnit.Core;

namespace TestUtilities.Tests.StringAssertionTests;

public class StringEqualsWithNormalizedLineEndingsAssertion_Tests
{
    [Test]
    public async Task Matching_strings_pass()
    {
        var actual = "Line1\nLine2";
        var expected = "Line1\nLine2";

        await Assert.That(actual).IsEqualToWithNormalizedLineEndings(expected);
    }

    [Test]
    public async Task Matching_strings_with_different_line_endings_pass()
    {
        var actual = "Line1\r\nLine2";
        var expected = "Line1\nLine2";

        await Assert.That(actual).IsEqualToWithNormalizedLineEndings(expected);
    }

    [Test]
    public async Task Matching_strings_with_mixed_line_endings_pass()
    {
        var actual = "Line1\r\nLine2\nLine3";
        var expected = "Line1\nLine2\r\nLine3";

        await Assert.That(actual).IsEqualToWithNormalizedLineEndings(expected);
    }

    [Test]
    public async Task Non_matching_strings_fail()
    {
        var actual = "Line1\nLine2";
        var expected = "Line1\nLine3";

        await Assert.That(async () => await Assert.That(actual).IsEqualToWithNormalizedLineEndings(expected))
            .Throws<TUnit.Assertions.Exceptions.AssertionException>();
    }

    [Test]
    public async Task Null_actual_fails()
    {
        string? actual = null;
        var expected = "Line1";

        await Assert.That(async () => await Assert.That(actual!).IsEqualToWithNormalizedLineEndings(expected))
            .Throws<TUnit.Assertions.Exceptions.AssertionException>();
    }
}
