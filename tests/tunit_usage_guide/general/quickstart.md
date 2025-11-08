# TUnit Quickstart

This quickstart provides the essential information you need to start writing tests immediately.

## Basic Test Structure

```csharp
using TUnit.Core;

public class MyTests
{
    [Test]
    public async Task Test_description_in_fluent_format()
    {
        // Arrange - Set up test data and dependencies
        var sut = new SystemUnderTest();

        // Act - Execute the method being tested
        var result = sut.DoSomething();

        // Assert - Verify the outcome
        await Assert.That(result).IsNotNull();
    }
}
```

## Key Principles

### 1. All Tests Are Async

Every test method should return `Task` and use `await` with assertions:

```csharp
[Test]
public async Task My_test()
{
    var value = 42;
    await Assert.That(value).IsEqualTo(42);  // Always await assertions
}
```

### 2. Use the `[Test]` Attribute

Mark test methods with `[Test]`:

```csharp
[Test]  // This makes the method discoverable
public async Task My_test()
{
    // Test code
}
```

### 3. Fluent Test Names

Follow the pattern: `[Action]_[Scenario]_[Expected]` in a fluent, readable format:

```csharp
[Test]
public async Task Dividing_by_zero_throws_DivideByZeroException()
{
    var calculator = new Calculator();
    
    await Assert.That(() => calculator.Divide(10, 0))
        .Throws<DivideByZeroException>();
}
```

See the [Project Style Guide](project_style_guide.md) for more naming conventions.

## Common Assertions

```csharp
// Equality
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(actual).IsNotEqualTo(unexpected);

// Null checks
await Assert.That(value).IsNull();
await Assert.That(value).IsNotNull();

// Boolean
await Assert.That(condition).IsTrue();
await Assert.That(condition).IsFalse();

// Numeric comparisons
await Assert.That(value).IsGreaterThan(10);
await Assert.That(value).IsLessThan(100);
await Assert.That(value).IsGreaterThanOrEqualTo(0);

// Collections
await Assert.That(collection).IsEmpty();
await Assert.That(collection).IsNotEmpty();
await Assert.That(collection).Contains(expectedItem);

// Strings
await Assert.That(text).Contains("substring");
await Assert.That(text).StartsWith("prefix");
await Assert.That(text).EndsWith("suffix");

// Exceptions
await Assert.That(() => method()).Throws<InvalidOperationException>();
```

## Parameterized Tests

Use `[Arguments]` for multiple test cases:

```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(5, 7, 12)]
[Arguments(-1, 1, 0)]
public async Task Adding_two_numbers_returns_their_sum(int a, int b, int expected)
{
    var calculator = new Calculator();
    
    var result = calculator.Add(a, b);
    
    await Assert.That(result).IsEqualTo(expected);
}
```

## Setup and Teardown

```csharp
public class DatabaseTests
{
    private DatabaseConnection? _connection;

    // Runs before each test
    [Before(Test)]
    public async Task Setup()
    {
        _connection = new DatabaseConnection();
        await _connection.OpenAsync();
    }

    // Runs after each test
    [After(Test)]
    public async Task Teardown()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
    }

    [Test]
    public async Task Database_query_returns_results()
    {
        var results = await _connection!.QueryAsync("SELECT * FROM Users");
        await Assert.That(results).IsNotEmpty();
    }
}
```

## Testing Async Code

TUnit is async-first, making async testing natural:

```csharp
[Test]
public async Task Fetching_user_data_returns_valid_user()
{
    var service = new UserService();
    
    var user = await service.GetUserAsync(userId: 123);
    
    await Assert.That(user).IsNotNull();
    await Assert.That(user.Id).IsEqualTo(123);
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTest"
```

## Common Patterns

### Testing Exceptions

```csharp
[Test]
public async Task Invalid_input_throws_ArgumentException()
{
    var validator = new InputValidator();
    
    await Assert.That(() => validator.Validate(null))
        .Throws<ArgumentException>();
}
```

### Multiple Assertions

Use `Assert.Multiple()` to ensure all assertions run:

```csharp
[Test]
public async Task User_has_valid_properties()
{
    var user = new User { Name = "John", Email = "john@example.com", Age = 30 };

    using (Assert.Multiple())
    {
        await Assert.That(user.Name).IsEqualTo("John");
        await Assert.That(user.Email).Contains("@");
        await Assert.That(user.Age).IsGreaterThan(0);
    }
}
```

### Member Assertions

Assert on object properties directly:

```csharp
[Test]
public async Task User_has_correct_email_and_name()
{
    var user = await GetUserAsync();

    await Assert.That(user)
        .Member(u => u.Email, email => email.IsEqualTo("user@example.com"))
        .And.Member(u => u.Name, name => name.IsNotNull());
}
```

## Test Utilities

This project provides a `TestUtilities` library with helpful test helper methods. Include it in your test files when you need to use types from this library:

```csharp
using TestUtilities;
```

### OpenAI Test Helpers

For tests involving OpenAI's ChatTokenUsage, use `OpenAITestHelpers.CreateChatTokenUsage()`:

```csharp
[Test]
public async Task Token_usage_is_calculated_correctly()
{
    // Create a ChatTokenUsage instance for testing
    var usage = OpenAITestHelpers.CreateChatTokenUsage(
        inputTokens: 1000,
        outputTokens: 500,
        cachedInputTokens: 100,
        outputReasoningTokens: 50);
    
    var service = new TokenUsageService();
    var result = service.Calculate(usage);
    
    await Assert.That(result).IsNotNull();
}
```

### FakeLogger Assertions

For tests using `FakeLogger<T>`, use the assertion extension methods:

```csharp
[Test]
public async Task Service_logs_information_message()
{
    var logger = new FakeLogger<MyService>();
    var service = new MyService(logger);
    
    service.DoSomething();
    
    // Assert that a log message was logged
    logger.AssertLogContains(LogLevel.Information, "Operation completed");
}

[Test]
public async Task Service_does_not_log_warning()
{
    var logger = new FakeLogger<MyService>();
    var service = new MyService(logger);
    
    service.DoSomething();
    
    // Assert that NO log message was logged
    logger.AssertLogDoesNotContain(LogLevel.Warning, "Warning message");
}
```

**Tip:** Check the source files in `src/TestUtilities/` for all available helper methods and their parameters.

## What's Next?

- **More complex scenarios?** See the [Usage Patterns](../usage_patterns/) section
- **Project conventions?** Check the [Project Style Guide](project_style_guide.md)
- **Specific topics:**
  - [Parameterized Tests](../usage_patterns/parameterized_tests.md)
  - [Setup and Teardown](../usage_patterns/setup_teardown.md)
  - [Assertions](../usage_patterns/assertions.md)
