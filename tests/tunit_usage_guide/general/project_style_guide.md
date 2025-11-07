# Project Style Guide

This document defines conventions and best practices for writing tests in this project using TUnit.

## Test Naming Conventions

### Fluent Test Names

**Do:** Use fluent, descriptive names that read like sentences

```csharp
[Test]
public async Task Adding_positive_numbers_returns_sum()
{
    // Test implementation
}

[Test]
public async Task Dividing_by_zero_throws_DivideByZeroException()
{
    // Test implementation
}

[Test]
public async Task Empty_string_is_considered_invalid()
{
    // Test implementation
}
```

**Don't:** Use the traditional `[MethodName]_[Scenario]_[Expected]` format with abbreviations

```csharp
// ❌ Avoid
[Test]
public async Task Add_PosNums_ReturnsSum() { }

// ❌ Avoid
[Test]
public async Task Div_ZeroDiv_Throws() { }

// ❌ Avoid
[Test]
public async Task ValidateStr_EmptyStr_RetsFalse() { }
```

### Naming Guidelines

1. **Start with the action** being tested (e.g., `Adding`, `Dividing`, `Validating`)
2. **Describe the scenario** clearly (e.g., `positive_numbers`, `by_zero`, `empty_string`)
3. **State the expected outcome** (e.g., `returns_sum`, `throws_exception`, `is_invalid`)
4. **Use underscores** to separate words for readability
5. **Be specific** - avoid vague names like `Test1` or `BasicTest`

## Mocking Library

This project uses **Moq** (version 4.20.72 or later) as the mocking library for creating test doubles.

### Basic Moq Usage

```csharp
using Moq;
using Microsoft.Extensions.Logging;

public class ServiceTests
{
    [Test]
    public async Task Example_test_with_mocked_dependency()
    {
        // Arrange - Create a mock
        var logger = new Mock<ILogger<MyService>>();
        var service = new MyService(logger.Object);
        
        // Act
        var result = service.DoSomething();
        
        // Assert
        await Assert.That(result).IsNotNull();
    }
}
```

### Verifying Logger Calls

When testing logging behavior, use Moq's `Verify` method with `It.IsAnyType` for the state parameter:

```csharp
[Test]
public async Task Method_logs_information_message()
{
    // Arrange
    var logger = new Mock<ILogger<MyService>>();
    var service = new MyService(logger.Object);
    
    // Act
    service.DoSomething();
    
    // Assert - Verify log was called
    logger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Expected message")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}

[Test]
public async Task Method_does_not_log_warning()
{
    // Arrange
    var logger = new Mock<ILogger<MyService>>();
    var service = new MyService(logger.Object);
    
    // Act
    service.DoSomething();
    
    // Assert - Verify log was NOT called
    logger.Verify(
        x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Warning message")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Never);
}
```

### Key Moq Patterns

- **Creating mocks**: `var mock = new Mock<IInterface>();`
- **Getting the object**: `mock.Object`
- **Verifying calls**: `mock.Verify(x => x.Method(...), Times.Once);`
- **Verifying no calls**: `mock.Verify(x => x.Method(...), Times.Never);`
- **Setup return values**: `mock.Setup(x => x.Method()).Returns(value);`

## Test Utilities Library

This project provides a shared `TestUtilities` library (located at `src/TestUtilities`) with common test helper methods. Always include this using directive in your test files:

```csharp
using TestUtilities;
```

### OpenAI Test Helpers

When testing code that uses OpenAI's `ChatTokenUsage`, use `OpenAITestHelpers.CreateChatTokenUsage()` instead of manually creating instances:

```csharp
[Test]
public async Task Calculating_cost_with_token_usage_returns_correct_amount()
{
    // Arrange
    var logger = new FakeLogger<CostCalculationService>();
    var service = new CostCalculationService(logger);
    
    // Use OpenAITestHelpers to create test ChatTokenUsage
    var usage = OpenAITestHelpers.CreateChatTokenUsage(
        inputTokens: 1_000_000,
        outputTokens: 500_000,
        cachedInputTokens: 100_000,
        outputReasoningTokens: 50_000);
    
    // Act
    var cost = service.CalculateCost("gpt-4o", usage);
    
    // Assert
    await Assert.That(cost).IsNotNull();
}
```

### FakeLogger Assertion Extensions

When testing logging behavior with `FakeLogger<T>`, use the assertion extension methods instead of manually inspecting the log collector:

```csharp
[Test]
public async Task Service_logs_information_when_processing()
{
    // Arrange
    var logger = new FakeLogger<MyService>();
    var service = new MyService(logger);
    
    // Act
    service.ProcessData();
    
    // Assert - Use the extension method
    logger.AssertLogContains(LogLevel.Information, "Processing started");
}

[Test]
public async Task Service_does_not_log_error_on_success()
{
    // Arrange
    var logger = new FakeLogger<MyService>();
    var service = new MyService(logger);
    
    // Act
    service.ProcessData();
    
    // Assert - Use the extension method
    logger.AssertLogDoesNotContain(LogLevel.Error, "Error occurred");
}
```

**When to Use:**

Use `FakeLogger` assertion extensions instead of Moq's `Verify` when:

- You want clearer, more readable assertions
- You need to verify log messages in tests where `FakeLogger` is already being used
- You want better failure messages that show all captured logs

Use Moq's `Verify` when:

- You're already using mocked logger dependencies
- You need to verify exact call counts or specific method overloads

**Tip:** Check the source files in `src/TestUtilities/` for all available helper methods, extension methods, and their parameters.

## Examples by Category

### Testing Methods

```csharp
public class CalculatorTests
{
    [Test]
    public async Task Adding_two_positive_numbers_returns_their_sum()
    {
        var calculator = new Calculator();
        var result = calculator.Add(2, 3);
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    public async Task Subtracting_larger_from_smaller_returns_negative()
    {
        var calculator = new Calculator();
        var result = calculator.Subtract(3, 7);
        await Assert.That(result).IsEqualTo(-4);
    }

    [Test]
    public async Task Dividing_by_zero_throws_DivideByZeroException()
    {
        var calculator = new Calculator();
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>();
    }
}
```

### Testing Validation

```csharp
public class UserValidatorTests
{
    [Test]
    public async Task Validating_valid_user_returns_true()
    {
        var validator = new UserValidator();
        var user = new User { Name = "John", Email = "john@example.com" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validating_user_with_empty_name_returns_false()
    {
        var validator = new UserValidator();
        var user = new User { Name = "", Email = "john@example.com" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Validating_user_with_invalid_email_returns_false()
    {
        var validator = new UserValidator();
        var user = new User { Name = "John", Email = "invalid-email" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsFalse();
    }
}
```

### Testing State Changes

```csharp
public class ShoppingCartTests
{
    [Test]
    public async Task Adding_item_increases_cart_count()
    {
        var cart = new ShoppingCart();
        var item = new CartItem { ProductId = 1, Quantity = 1 };
        
        cart.AddItem(item);
        
        await Assert.That(cart.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Removing_last_item_empties_cart()
    {
        var cart = new ShoppingCart();
        var item = new CartItem { ProductId = 1, Quantity = 1 };
        cart.AddItem(item);
        
        cart.RemoveItem(item.ProductId);
        
        await Assert.That(cart.Items).IsEmpty();
    }

    [Test]
    public async Task Clearing_cart_removes_all_items()
    {
        var cart = new ShoppingCart();
        cart.AddItem(new CartItem { ProductId = 1, Quantity = 1 });
        cart.AddItem(new CartItem { ProductId = 2, Quantity = 2 });
        
        cart.Clear();
        
        await Assert.That(cart.Items).IsEmpty();
    }
}
```

## Test Organization

### File and Folder Structure

The organization of test files depends on the size and complexity of the class being tested:

#### Simple Test Fixtures

For small classes with few methods, use a single test file named after the class being tested:

```text
tests/
  MyProject.Tests/
    CalculatorTests.cs
    ValidatorTests.cs
```

**Example:**

```csharp
namespace MyProject.Tests;

public class CalculatorTests
{
    [Test]
    public async Task Adding_numbers_returns_sum() { }
    
    [Test]
    public async Task Dividing_by_zero_throws_exception() { }
}
```

#### Complex Test Fixtures

For larger classes with multiple methods that require many tests, organize tests in a dedicated folder with:

- A base class for shared test functionality
- Separate test files for each method or logical grouping

**Folder Structure:**

```text
tests/
  MyProject.Tests/
    CostCalculationServiceTests/
      CostCalculationServiceTests_Base.cs
      CostCalculationService_CalculateCost_Tests.cs
      CostCalculationService_LogCostBreakdown_Tests.cs
```

**Base Class Example:**

```csharp
namespace MyProject.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
    protected Mock<ILogger<CostCalculationService>> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new Mock<ILogger<CostCalculationService>>();
        Service = new CostCalculationService(Logger.Object);
    }

    /// <summary>
    /// Verifies that a log message containing the specified text was logged at the specified level
    /// </summary>
    protected void VerifyLogContains(LogLevel logLevel, string messageContent, Func<Times> times)
    {
        Logger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(messageContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    protected static SomeTestData CreateTestData()
    {
        // Shared test data creation logic
        return new SomeTestData();
    }
}
```

**Test Class Example:**

```csharp
namespace MyProject.Tests.CostCalculationServiceTests;

/// <summary>
/// Tests for the CostCalculationService CalculateCost method
/// </summary>
public class CostCalculationService_CalculateCost_Tests : CostCalculationServiceTests_Base
{
    [Test]
    public async Task CalculateCost_with_valid_input_returns_result()
    {
        // Arrange
        var data = CreateTestData(); // Using inherited helper
        
        // Act
        var result = Service.CalculateCost("model", data);
        
        // Assert
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task CalculateCost_logs_information()
    {
        // Arrange
        var data = CreateTestData();
        
        // Act
        Service.CalculateCost("model", data);
        
        // Assert - Using helper method for verification
        VerifyLogContains(LogLevel.Information, "Expected message", Times.Once);
    }
}
```

**Naming Conventions:**

- **Folder name**: `{ClassName}Tests` (e.g., `CostCalculationServiceTests`)
- **Base class**: `{ClassName}Tests_Base` (e.g., `CostCalculationServiceTests_Base`)
- **Test classes**: `{ClassName}_{MethodName}_Tests` (e.g., `CostCalculationService_CalculateCost_Tests`)

This structure:

- Keeps test files focused on a single method or concern
- Reduces code duplication through the base class
- Makes it easy to locate tests for specific methods
- Improves readability with clear, underscored naming

### Base Class Best Practices

When using a base class for complex test fixtures, follow these patterns to maximize maintainability:

#### 1. Shared Setup with `[Before(Test)]`

Use protected fields and setup methods to eliminate repetitive instantiation:

```csharp
public abstract class MyServiceTests_Base
{
    protected Mock<ILogger<MyService>> Logger = null!;
    protected Mock<IDependency> Dependency = null!;
    protected MyService Service = null!;

    [Before(Test)]
    public void SetupCommonDependencies()
    {
        Logger = new Mock<ILogger<MyService>>();
        Dependency = new Mock<IDependency>();
        Service = new MyService(Logger.Object, Dependency.Object);
    }
}
```

**Benefits:**

- Each test automatically gets fresh instances
- No need to repeat mock creation in every test
- Changes to constructor signatures only require updating one place

#### 2. Helper Methods for Repetitive Verifications

Create helper methods to simplify common verification patterns:

```csharp
public abstract class MyServiceTests_Base
{
    protected Mock<ILogger<MyService>> Logger = null!;

    /// <summary>
    /// Verifies that a log message containing the specified text was logged
    /// </summary>
    protected void VerifyLogContains(LogLevel logLevel, string messageContent, Func<Times> times)
    {
        Logger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(messageContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
```

**Usage in tests:**

```csharp
// Instead of 8 lines of logger.Verify(...)
VerifyLogContains(LogLevel.Information, "Expected message", Times.Once);
```

#### 3. Factory Methods for Test Data

Provide methods to create common test data configurations:

```csharp
public abstract class MyServiceTests_Base
{
    protected static User CreateValidUser(string name = "John", string email = "john@example.com")
    {
        return new User { Name = name, Email = email };
    }

    protected static User CreateInvalidUser()
    {
        return new User { Name = "", Email = "invalid" };
    }
}
```

#### 4. When to Use Base Classes

**Use a base class when:**

- Multiple test files test the same class
- Tests share common setup (mocks, dependencies)
- You have repetitive verification patterns
- You need shared test data factory methods

**Keep tests in a single file when:**

- The class being tested is simple with few methods
- Tests don't share significant setup
- Total number of tests is small (< 10-15)

### Class Naming

Test classes should be named after the class being tested with a `Tests` suffix:

```csharp
// Testing Calculator class
public class CalculatorTests { }

// Testing UserService class
public class UserServiceTests { }

// Testing OrderValidator class
public class OrderValidatorTests { }
```

### Namespace Organization

Match the namespace structure of the code being tested, but add `.Tests`:

```csharp
// Production code: KicktippAi.Core.Services
namespace KicktippAi.Core.Services.Tests;

// Production code: KicktippAi.Orchestrator.Commands
namespace KicktippAi.Orchestrator.Commands.Tests;
```

### File Organization

- One test class per file
- File name matches class name: `CalculatorTests.cs`
- Group related tests in the same class
- Use nested classes for grouping when appropriate

## Code Style

### Always Use Arrange-Act-Assert

Structure tests with clear sections:

```csharp
[Test]
public async Task Example_test()
{
    // Arrange - Set up test data and dependencies
    var service = new MyService();
    var input = new InputData { Value = 42 };

    // Act - Execute the method being tested
    var result = await service.ProcessAsync(input);

    // Assert - Verify the outcome
    await Assert.That(result).IsNotNull();
    await Assert.That(result.Value).IsEqualTo(42);
}
```

### Use Clear Variable Names

Prefer descriptive names over abbreviations:

```csharp
// ✅ Good
var expectedResult = 42;
var actualResult = calculator.Add(20, 22);
await Assert.That(actualResult).IsEqualTo(expectedResult);

// ❌ Avoid
var exp = 42;
var act = calc.Add(20, 22);
await Assert.That(act).IsEqualTo(exp);
```

### One Logical Assert Per Test

Focus each test on one specific behavior:

```csharp
// ✅ Good - Single concern
[Test]
public async Task User_registration_creates_new_user()
{
    var result = await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(result.Success).IsTrue();
}

[Test]
public async Task User_registration_sends_confirmation_email()
{
    await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(emailService.SentEmails).Contains(e => e.To == "user@example.com");
}

// ❌ Avoid - Multiple concerns
[Test]
public async Task User_registration_works()
{
    var result = await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(result.Success).IsTrue();
    await Assert.That(emailService.SentEmails).Contains(e => e.To == "user@example.com");
}
```

**Exception:** Use `Assert.Multiple()` when testing multiple properties of the same object:

```csharp
[Test]
public async Task Created_user_has_correct_properties()
{
    var user = await userService.CreateAsync("John", "john@example.com");

    using (Assert.Multiple())
    {
        await Assert.That(user.Name).IsEqualTo("John");
        await Assert.That(user.Email).IsEqualTo("john@example.com");
        await Assert.That(user.CreatedDate).IsGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }
}
```

## Test Attributes

### Use Descriptive Categories

```csharp
[Test]
[Category("Integration")]
[Category("Database")]
public async Task Database_query_returns_users()
{
    // Test implementation
}

[Test]
[Category("Unit")]
[Category("Validation")]
public async Task Validator_rejects_invalid_input()
{
    // Test implementation
}
```

### Add Display Names for Complex Scenarios

```csharp
[Test]
[DisplayName("User registration with existing email should fail")]
public async Task Register_User_ExistingEmail()
{
    // Test implementation
}
```

## Performance Considerations

### Share Expensive Setup

Use class-level setup for expensive operations:

```csharp
public class DatabaseIntegrationTests
{
    private static DatabaseConnection? _connection;

    [Before(Class)]
    public static async Task SetupDatabase()
    {
        _connection = new DatabaseConnection();
        await _connection.OpenAsync();
        await _connection.MigrateAsync();
    }

    [After(Class)]
    public static async Task CleanupDatabase()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
    }

    [Test]
    public async Task Query_returns_results()
    {
        var results = await _connection!.QueryAsync("SELECT * FROM Users");
        await Assert.That(results).IsNotEmpty();
    }
}
```

### Use Parallel Execution Wisely

Group tests that share resources:

```csharp
[ParallelGroup("DatabaseTests")]
public class UserRepositoryTests
{
    // These tests share database resources and run sequentially
}

[ParallelGroup("DatabaseTests")]
public class OrderRepositoryTests
{
    // These also share database resources
}

[ParallelGroup("ApiTests")]
public class ApiIntegrationTests
{
    // These can run in parallel with database tests
}
```

## Common Anti-Patterns to Avoid

### ❌ Don't Use Logic in Tests

```csharp
// ❌ Avoid
[Test]
public async Task Bad_test_with_logic()
{
    var items = GetItems();
    var sum = 0;
    foreach (var item in items)
    {
        sum += item.Value;
    }
    await Assert.That(sum).IsGreaterThan(0);
}

// ✅ Good
[Test]
public async Task Total_of_items_is_greater_than_zero()
{
    var items = GetItems();
    var total = items.Sum(i => i.Value);
    await Assert.That(total).IsGreaterThan(0);
}
```

### ❌ Don't Use Magic Numbers

```csharp
// ❌ Avoid
[Test]
public async Task Bad_test_with_magic_numbers()
{
    var result = calculator.Calculate(42, 7);
    await Assert.That(result).IsEqualTo(49);
}

// ✅ Good
[Test]
public async Task Adding_two_numbers_returns_sum()
{
    const int firstNumber = 42;
    const int secondNumber = 7;
    const int expectedSum = 49;

    var result = calculator.Add(firstNumber, secondNumber);

    await Assert.That(result).IsEqualTo(expectedSum);
}
```

### ❌ Don't Test Implementation Details

```csharp
// ❌ Avoid - Testing private implementation
[Test]
public async Task Bad_test_checking_internals()
{
    var service = new UserService();
    service.ProcessUser(user);
    await Assert.That(service.InternalCache.Count).IsEqualTo(1);
}

// ✅ Good - Testing public behavior
[Test]
public async Task Processing_user_stores_in_cache()
{
    var service = new UserService();
    service.ProcessUser(user);
    var cachedUser = service.GetCachedUser(user.Id);
    await Assert.That(cachedUser).IsEqualTo(user);
}
```

## Summary

Follow these key principles:

1. **Fluent test names** - Use descriptive, readable names with underscores
2. **Clear structure** - Use Arrange-Act-Assert pattern
3. **Single responsibility** - One logical assertion per test
4. **Descriptive code** - No abbreviations or magic numbers
5. **Async throughout** - All tests and assertions are async
6. **Performance awareness** - Share expensive setup when appropriate
