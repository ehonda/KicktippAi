# Parameterized Tests

Parameterized tests allow you to run the same test logic with different inputs, making your tests more comprehensive and reducing code duplication.

## Basic Parameterized Tests

Use `[Arguments]` to provide inline test data:

```csharp
using TUnit.Core;

public class CalculatorTests
{
    [Test]
    [Arguments(2, 3, 5)]
    [Arguments(10, 20, 30)]
    [Arguments(-1, 1, 0)]
    [Arguments(0, 0, 0)]
    public async Task Adding_numbers_returns_correct_sum(int a, int b, int expected)
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(a, b);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }
}
```

Each `[Arguments]` attribute creates a separate test case that appears in the test runner.

## Method Data Sources

For more complex data or when you need to generate test cases dynamically, use `[MethodDataSource]`:

```csharp
public class UserValidatorTests
{
    [Test]
    [MethodDataSource(nameof(GetValidationTestCases))]
    public async Task Validating_user_returns_expected_result(
        string name, 
        string email, 
        int age, 
        bool expectedValid)
    {
        // Arrange
        var user = new User { Name = name, Email = email, Age = age };
        var validator = new UserValidator();

        // Act
        var isValid = validator.Validate(user);

        // Assert
        await Assert.That(isValid).IsEqualTo(expectedValid);
    }

    // Data source method returns IEnumerable of test cases
    public static IEnumerable<(string Name, string Email, int Age, bool Expected)> GetValidationTestCases()
    {
        yield return ("John Doe", "john@example.com", 25, true);
        yield return ("", "john@example.com", 25, false);  // Empty name
        yield return ("John Doe", "invalid-email", 25, false);  // Invalid email
        yield return ("John Doe", "john@example.com", -1, false);  // Negative age
        yield return ("John Doe", "john@example.com", 150, false);  // Age too high
    }
}
```

## Using Object Arrays

When you need more flexibility in return types:

```csharp
public class StringExtensionsTests
{
    [Test]
    [MethodDataSource(nameof(GetTruncateTestCases))]
    public async Task Truncating_string_produces_expected_result(
        string input, 
        int maxLength, 
        string expected)
    {
        var result = input.Truncate(maxLength);
        await Assert.That(result).IsEqualTo(expected);
    }

    public static IEnumerable<object[]> GetTruncateTestCases()
    {
        yield return new object[] { "Hello, World!", 5, "Hello..." };
        yield return new object[] { "Short", 10, "Short" };
        yield return new object[] { "Exact", 5, "Exact" };
        yield return new object[] { "", 5, "" };
    }
}
```

## Async Data Sources

For test data that requires async operations:

```csharp
public class UserServiceTests
{
    [Test]
    [MethodDataSource(nameof(GetUsersFromDatabaseAsync))]
    public async Task Processing_user_succeeds(User user)
    {
        var service = new UserService();
        
        var result = await service.ProcessAsync(user);
        
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Processed).IsTrue();
    }

    public static async IAsyncEnumerable<User> GetUsersFromDatabaseAsync()
    {
        // Simulate loading users from database
        await Task.Delay(10);
        
        yield return new User { Id = 1, Name = "Alice", Age = 30 };
        yield return new User { Id = 2, Name = "Bob", Age = 25 };
        yield return new User { Id = 3, Name = "Charlie", Age = 35 };
    }
}
```

## Complex Test Data

### Using Custom Types

```csharp
public class OrderProcessorTests
{
    [Test]
    [MethodDataSource(nameof(GetOrderTestCases))]
    public async Task Processing_order_calculates_correct_total(OrderTestCase testCase)
    {
        // Arrange
        var processor = new OrderProcessor();

        // Act
        var result = await processor.ProcessAsync(testCase.Order);

        // Assert
        await Assert.That(result.Total).IsEqualTo(testCase.ExpectedTotal);
    }

    public static IEnumerable<OrderTestCase> GetOrderTestCases()
    {
        yield return new OrderTestCase
        {
            Order = new Order
            {
                Items = new[] { new OrderItem { Price = 10, Quantity = 2 } },
                DiscountPercentage = 0
            },
            ExpectedTotal = 20
        };

        yield return new OrderTestCase
        {
            Order = new Order
            {
                Items = new[] { new OrderItem { Price = 100, Quantity = 1 } },
                DiscountPercentage = 10
            },
            ExpectedTotal = 90
        };

        yield return new OrderTestCase
        {
            Order = new Order
            {
                Items = new[]
                {
                    new OrderItem { Price = 50, Quantity = 2 },
                    new OrderItem { Price = 30, Quantity = 1 }
                },
                DiscountPercentage = 0
            },
            ExpectedTotal = 130
        };
    }

    public record OrderTestCase
    {
        public Order Order { get; init; } = null!;
        public decimal ExpectedTotal { get; init; }
    }
}
```

### Combining Multiple Data Sources

```csharp
public class PaymentProcessorTests
{
    [Test]
    [Arguments(PaymentMethod.CreditCard)]
    [Arguments(PaymentMethod.DebitCard)]
    [Arguments(PaymentMethod.PayPal)]
    [MethodDataSource(nameof(GetPaymentAmounts))]
    public async Task Processing_payment_with_valid_method_succeeds(
        PaymentMethod method, 
        decimal amount)
    {
        var processor = new PaymentProcessor();
        
        var result = await processor.ProcessAsync(method, amount);
        
        await Assert.That(result.Success).IsTrue();
    }

    public static IEnumerable<decimal> GetPaymentAmounts()
    {
        yield return 10.00m;
        yield return 100.00m;
        yield return 1000.00m;
    }
}
```

## Naming Test Cases

### Using Display Names

```csharp
public class ValidationTests
{
    [Test]
    [Arguments("valid@email.com", true)]
    [Arguments("invalid-email", false)]
    [Arguments("", false)]
    [DisplayName("Email validation: {0} should return {1}")]
    public async Task Email_validation(string email, bool expectedValid)
    {
        var validator = new EmailValidator();
        var isValid = validator.Validate(email);
        await Assert.That(isValid).IsEqualTo(expectedValid);
    }
}
```

## Edge Cases and Boundary Testing

Use parameterized tests to systematically test edge cases:

```csharp
public class RangeValidatorTests
{
    [Test]
    [Arguments(-1, false)]  // Below minimum
    [Arguments(0, true)]    // Minimum value
    [Arguments(50, true)]   // Middle value
    [Arguments(100, true)]  // Maximum value
    [Arguments(101, false)] // Above maximum
    public async Task Validating_value_in_range_0_to_100(int value, bool expectedValid)
    {
        var validator = new RangeValidator(min: 0, max: 100);
        
        var isValid = validator.IsInRange(value);
        
        await Assert.That(isValid).IsEqualTo(expectedValid);
    }
}
```

## Testing String Operations

```csharp
public class StringManipulationTests
{
    [Test]
    [Arguments("hello", "HELLO")]
    [Arguments("WORLD", "WORLD")]
    [Arguments("MiXeD", "MIXED")]
    [Arguments("", "")]
    [Arguments("123", "123")]
    public async Task Converting_to_uppercase(string input, string expected)
    {
        var result = input.ToUpperInvariant();
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(GetPalindromeTestCases))]
    public async Task Checking_if_string_is_palindrome(string input, bool expectedResult)
    {
        var checker = new PalindromeChecker();
        
        var isPalindrome = checker.IsPalindrome(input);
        
        await Assert.That(isPalindrome).IsEqualTo(expectedResult);
    }

    public static IEnumerable<(string Input, bool Expected)> GetPalindromeTestCases()
    {
        yield return ("racecar", true);
        yield return ("hello", false);
        yield return ("A man a plan a canal Panama", true);  // Ignores spaces and case
        yield return ("", true);  // Empty string is palindrome
        yield return ("a", true);  // Single character is palindrome
    }
}
```

## Testing with Null and Empty Values

```csharp
public class NullHandlingTests
{
    [Test]
    [Arguments(null, "")]
    [Arguments("", "")]
    [Arguments("  ", "")]
    [Arguments("hello", "hello")]
    [Arguments(" hello ", "hello")]
    public async Task Normalizing_string(string? input, string expected)
    {
        var normalizer = new StringNormalizer();
        
        var result = normalizer.Normalize(input);
        
        await Assert.That(result).IsEqualTo(expected);
    }
}
```

## Testing Collections

```csharp
public class CollectionTests
{
    [Test]
    [MethodDataSource(nameof(GetCollectionTestCases))]
    public async Task Filtering_collection(int[] input, int threshold, int[] expected)
    {
        var filter = new CollectionFilter();
        
        var result = filter.FilterGreaterThan(input, threshold);
        
        await Assert.That(result).IsEquivalentTo(expected);
    }

    public static IEnumerable<(int[] Input, int Threshold, int[] Expected)> GetCollectionTestCases()
    {
        yield return (
            new[] { 1, 2, 3, 4, 5 },
            3,
            new[] { 4, 5 }
        );

        yield return (
            new[] { 10, 20, 30 },
            25,
            new[] { 30 }
        );

        yield return (
            Array.Empty<int>(),
            0,
            Array.Empty<int>()
        );

        yield return (
            new[] { 1, 2, 3 },
            10,
            Array.Empty<int>()
        );
    }
}
```

## Performance Testing with Parameters

```csharp
public class PerformanceTests
{
    [Test]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task Processing_items_completes_within_timeout(int itemCount)
    {
        var processor = new BatchProcessor();
        var items = Enumerable.Range(1, itemCount).ToArray();

        var startTime = DateTime.UtcNow;
        await processor.ProcessAsync(items);
        var duration = DateTime.UtcNow - startTime;

        // Assert processing time scales reasonably
        var expectedMaxDuration = TimeSpan.FromMilliseconds(itemCount * 0.1);
        await Assert.That(duration).IsLessThan(expectedMaxDuration);
    }
}
```

## Best Practices

### 1. Keep Data Sources Readable

```csharp
// ✅ Good - Clear and readable
public static IEnumerable<(string Description, int Input, int Expected)> GetTestCases()
{
    yield return ("Zero", 0, 0);
    yield return ("Positive", 5, 25);
    yield return ("Negative", -3, 9);
}

// ❌ Avoid - Unclear what the values represent
public static IEnumerable<object[]> GetTestCases()
{
    yield return new object[] { 0, 0 };
    yield return new object[] { 5, 25 };
    yield return new object[] { -3, 9 };
}
```

### 2. Use Descriptive Test Names

Even with parameters, test names should be clear:

```csharp
// ✅ Good
[Test]
[Arguments(1, 2, 3)]
public async Task Adding_positive_numbers_returns_sum(int a, int b, int expected)

// ❌ Avoid
[Test]
[Arguments(1, 2, 3)]
public async Task Test1(int a, int b, int c)
```

### 3. Group Related Test Cases

```csharp
public class StringValidatorTests
{
    // Test valid cases
    [Test]
    [Arguments("hello@example.com")]
    [Arguments("user.name@domain.co.uk")]
    [Arguments("test+tag@example.com")]
    public async Task Valid_email_formats_pass_validation(string email)
    {
        var validator = new EmailValidator();
        await Assert.That(validator.IsValid(email)).IsTrue();
    }

    // Test invalid cases
    [Test]
    [Arguments("invalid")]
    [Arguments("@example.com")]
    [Arguments("user@")]
    [Arguments("")]
    public async Task Invalid_email_formats_fail_validation(string email)
    {
        var validator = new EmailValidator();
        await Assert.That(validator.IsValid(email)).IsFalse();
    }
}
```

### 4. Avoid Over-Parameterization

```csharp
// ❌ Too many parameters - hard to read
[Test]
[Arguments(1, 2, 3, true, "success", 100, DateTime.Now, null)]
public async Task Complex_test(int a, int b, int c, bool d, string e, int f, DateTime g, object? h)

// ✅ Better - use a test case object
public record TestCase(int A, int B, int C, bool D, string E, int F, DateTime G, object? H);

[Test]
[MethodDataSource(nameof(GetTestCases))]
public async Task Better_test(TestCase testCase)
{
    // More readable
}
```

## See Also

- [Basic Tests](basic_tests.md) - Simple test structure
- [Assertions](assertions.md) - Assertion techniques
- [Test Organization](test_organization.md) - Organizing and filtering tests
