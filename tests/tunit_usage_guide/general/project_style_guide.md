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

### Key Moq Patterns

- **Creating mocks**: `var mock = new Mock<IInterface>();`
- **Getting the object**: `mock.Object`
- **Verifying calls**: `mock.Verify(x => x.Method(...), Times.Once);`
- **Verifying no calls**: `mock.Verify(x => x.Method(...), Times.Never);`
- **Setup return values**: `mock.Setup(x => x.Method()).Returns(value);`

## Test Utilities Library

This project provides a shared `TestUtilities` library (located at `src/TestUtilities`) with common test helper methods. Include this using directive in your test files when you need to use types from this library:

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

Always use `FakeLogger` for testing logging behavior:

- Provides clearer, more readable assertions
- Better failure messages that show all captured logs
- Simpler API than Moq's `Verify` method
- Specifically designed for testing logging

**Migrating from Mock<ILogger>:**

If you encounter existing tests using `Mock<ILogger<T>>`, replace them with `FakeLogger<T>`:

```csharp
// ❌ Old pattern - replace this
var logger = new Mock<ILogger<MyService>>();
var service = new MyService(logger.Object);
logger.Verify(x => x.Log(...), Times.Once);

// ✅ New pattern - use this instead
var logger = new FakeLogger<MyService>();
var service = new MyService(logger);
logger.AssertLogContains(LogLevel.Information, "Expected message");
```

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
    protected FakeLogger<CostCalculationService> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new FakeLogger<CostCalculationService>();
        Service = new CostCalculationService(Logger);
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
    public void CalculateCost_logs_information()
    {
        // Arrange
        var data = CreateTestData();
        
        // Act
        Service.CalculateCost("model", data);
        
        // Assert - Using FakeLogger assertion extension
        Logger.AssertLogContains(LogLevel.Information, "Expected message");
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
    protected FakeLogger<MyService> Logger = null!;
    protected Mock<IDependency> Dependency = null!;
    protected MyService Service = null!;

    [Before(Test)]
    public void SetupCommonDependencies()
    {
        Logger = new FakeLogger<MyService>();
        Dependency = new Mock<IDependency>();
        Service = new MyService(Logger, Dependency.Object);
    }
}
```

**Benefits:**

- Each test automatically gets fresh instances
- No need to repeat mock creation in every test
- Changes to constructor signatures only require updating one place

**For more flexibility**, use a factory method pattern instead of creating the service directly:

```csharp
public abstract class MyServiceTests_Base
{
    protected FakeLogger<MyService> Logger = null!;
    protected Mock<IDependency> Dependency = null!;
    protected string ConfigValue = null!;

    [Before(Test)]
    public void SetupCommonDependencies()
    {
        // Set up default dependencies
        Logger = new FakeLogger<MyService>();
        Dependency = new Mock<IDependency>();
        ConfigValue = "default-value";
    }

    /// <summary>
    /// Factory method to create service with configured dependencies
    /// </summary>
    protected MyService CreateService()
    {
        return new MyService(Logger, Dependency.Object, ConfigValue);
    }
}
```

**Using the factory method in tests:**

```csharp
[Test]
public async Task Test_with_default_dependencies()
{
    // Arrange - use defaults from setup
    var service = CreateService();
    
    // Act & Assert
    var result = service.DoSomething();
    await Assert.That(result).IsNotNull();
}

[Test]
public async Task Test_with_custom_dependency()
{
    // Arrange - modify only what's needed
    Dependency.Setup(d => d.GetValue()).Returns(42);
    var service = CreateService();
    
    // Act & Assert
    var result = service.DoSomething();
    await Assert.That(result).IsEqualTo(42);
}

[Test]
public async Task Test_with_custom_config()
{
    // Arrange - change config before creating service
    ConfigValue = "custom-value";
    var service = CreateService();
    
    // Act & Assert
    var result = service.GetConfig();
    await Assert.That(result).IsEqualTo("custom-value");
}
```

This pattern:

- Eliminates repetitive dependency setup across all tests
- Allows easy customization of individual dependencies per test
- Keeps tests focused on what varies, not boilerplate
- Makes it trivial to add new constructor parameters (update setup and factory once)


#### 2. Helper Methods for Repetitive Operations

Create helper methods to simplify common test patterns. When testing logging, use FakeLogger's built-in assertion extensions rather than creating custom verification helpers:

```csharp
public abstract class MyServiceTests_Base
{
    protected FakeLogger<MyService> Logger = null!;

    [Before(Test)]
    public void Setup()
    {
        Logger = new FakeLogger<MyService>();
    }
}
```

**Usage in tests:**

```csharp
[Test]
public void Method_logs_information()
{
    Service.DoSomething();
    
    // Use FakeLogger's built-in assertion extensions
    Logger.AssertLogContains(LogLevel.Information, "Expected message");
}
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

#### 5. Factory Methods and Flexible Defaults

Use factory methods in your test base class or derived test classes to create instances of the system under test (SUT) or complex dependencies. This centralizes object creation and makes tests more maintainable.

**Pattern: Factory Methods with `NullableOption<T>`**

Use `NullableOption<T>` (from `EHonda.Optional.Core`) for factory method parameters. This allows tests to:

1. Override specific dependencies when relevant to the test.
2. Fall back to sensible defaults (mocks or fakes) for dependencies that don't matter for the specific test.
3. Explicitly pass `null` to test null guards (using `NullableOption.Some(null)`).

**Base Class Implementation:**

```csharp
using EHonda.Optional.Core;

public abstract class MyServiceTests_Base
{
    protected static MyService CreateService(
        NullableOption<IDependency> dependency = default,
        NullableOption<ILogger<MyService>> logger = default)
    {
        // Use provided dependency or create a default mock
        var actualDependency = dependency.Or(() => new Mock<IDependency>().Object);
        
        // Use provided logger or create a default fake
        var actualLogger = logger.Or(() => new FakeLogger<MyService>());

        return new MyService(actualDependency!, actualLogger!);
    }
}
```

**Usage Guidelines:**

1. **Only specify what is relevant**: When calling factory methods, only provide arguments for the dependencies that are specific to the test scenario. Rely on the defaults for everything else.
2. **Minimize named arguments**: Do not use named arguments (e.g., `dependency: mock.Object`) unless necessary (i.e., when skipping preceding optional parameters). If you are passing the first parameter, pass it positionally.
3. **Use `NullableOption.Some(null)` for null testing**: When you need to verify that a constructor throws on null, pass `NullableOption.Some<T>(null)`.
4. **Use `Option.Some(value)` for non-null values**: When passing a non-null value, use `Option.Some(value)` instead of `NullableOption.Some(value)`. It will be implicitly converted to `NullableOption<T>`.
5. **Use `Option<T>` for infrastructure parameters**: If a parameter is used directly by the test helper (e.g., the SUT instance, or a required context object) and must not be null for the helper to function, use `Option<T>` instead of `NullableOption<T>`. This prevents passing `null` where it would cause the test helper to crash.

**Examples:**

```csharp
[Test]
public async Task Test_with_default_dependencies()
{
    // ✅ Good: Use defaults for all dependencies
    var service = CreateService();
    
    // ...
}

[Test]
public async Task Test_with_specific_dependency()
{
    var mockDependency = new Mock<IDependency>();
    
    // ✅ Good: Override only the dependency. 
    // No need to name the argument since it's the first parameter.
    // Use Option.Some (implicit conversion) for non-null values.
    var service = CreateService(Option.Some(mockDependency.Object));
    
    // ...
}

[Test]
public async Task Test_with_specific_logger()
{
    var logger = new FakeLogger<MyService>();
    
    // ✅ Good: Override only the logger.
    // Named argument is required here to skip the 'dependency' parameter.
    var service = CreateService(logger: Option.Some(logger));
    
    // ...
}

[Test]
public async Task Test_with_null_dependency()
{
    // ✅ Good: Explicitly pass null to test validation using NullableOption.Some(null)
    await Assert.That(() => CreateService(dependency: NullableOption.Some<IDependency>(null)))
        .Throws<ArgumentNullException>();
}

[Test]
public async Task Test_with_unnecessary_verbosity()
{
    var mockDependency = new Mock<IDependency>();

    // ❌ Avoid: Specifying argument name when not required
    var service = CreateService(dependency: Option.Some(mockDependency.Object));
    
    // ...
}

// Helper method demonstrating Option<T> vs NullableOption<T> distinction
private async Task CallMethod(
    Option<MyService> service = default,       // Infrastructure: Must not be null
    NullableOption<InputData> input = default) // Data: Can be null (if SUT allows it / to test null handling)
{
    // 'service' is infrastructure: we need it to invoke the method.
    // Using Option<T> ensures we can't accidentally pass null here.
    var actualService = service.Or(CreateService);
    
    // 'input' is data: we pass it through.
    // Using NullableOption<T> allows testing with null input if needed.
    var actualInput = input.Or(CreateDefaultInput);
    
    await actualService.ProcessAsync(actualInput!);
}
```

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

### Async Test Methods

**TUnit supports both synchronous and asynchronous test methods**, but the choice depends on what your test does:

#### Use `async Task` when

- Your test uses `await Assert.That(...)` - TUnit's assertion library returns awaitable objects
- Your test awaits any async operations
- You need to test async code

```csharp
[Test]
public async Task Adding_numbers_returns_sum()
{
    var result = calculator.Add(2, 3);
    // Assert.That(...) must be awaited
    await Assert.That(result).IsEqualTo(5);
}

[Test]
public async Task Async_operation_completes_successfully()
{
    // Testing async code
    var result = await someService.ProcessAsync();
    await Assert.That(result).IsNotNull();
}
```

#### Use `void` (synchronous) when

- Your test uses `Moq.Verify(...)` (returns void, not awaitable)
- Your test uses `FakeLogger.AssertLogContains(...)` (returns void, not awaitable)
- Your test has no async operations and no TUnit assertions

```csharp
[Test]
public void Method_calls_dependency()
{
    var mock = new Mock<IDependency>();
    var service = new MyService(mock.Object);
    
    service.DoSomething();
    
    // Moq.Verify returns void - no await needed
    mock.Verify(x => x.Method(), Times.Once);
}

[Test]
public void Method_logs_message()
{
    var logger = new FakeLogger<MyService>();
    var service = new MyService(logger);
    
    service.DoSomething();
    
    // FakeLogger.AssertLogContains returns void - no await needed
    logger.AssertLogContains(LogLevel.Information, "Message");
}
```

#### Never use `async void`

```csharp
// ❌ Invalid - async void is not supported
[Test]
public async void Invalid_test() { }
```

**Source:** [TUnit Troubleshooting - Why do I have to await all assertions?](https://tunit.dev/docs/troubleshooting/#why-do-i-have-to-await-all-assertions-can-i-use-synchronous-assertions)

**Compiler Warning CS1998:** If you see "This async method lacks 'await' operators" warnings, your test method should probably be `void` instead of `async Task`.

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

### Naming System Under Test Variables

When naming variables for the class being tested, use **concise but meaningful** names:

**Do:** Use shortened but clear names that are contextually obvious

```csharp
// ✅ Good - "Service" is clear in context
protected CostCalculationService Service = null!;

// ✅ Good - "Provider" is clear in context
protected InstructionsTemplateProvider Provider = null!;

// ✅ Good - Full name when it's already short
protected Calculator Calculator = null!;

// ✅ Good - Full name when abbreviation would be unclear
protected InstructionsTemplateProvider InstructionsTemplateProvider = null!;
```

**Don't:** Use generic abbreviations like `Sut` (System Under Test)

```csharp
// ❌ Avoid - Too generic, doesn't convey what's being tested
protected CostCalculationService Sut = null!;

// ❌ Avoid - Unnecessarily verbose when context is clear
protected CostCalculationService CostCalculationService = null!;
```

**Guidelines:**

1. **Prefer concise names** when the class type is clear from context (e.g., `Service`, `Provider`, `Repository`)
2. **Use full names** when the class name is already short (e.g., `Calculator`, `Validator`)
3. **Use full names** when abbreviation would be ambiguous or unclear
4. **Never use generic names** like `Sut`, `Instance`, or `Target`
5. **Be consistent** - use the same pattern throughout the test class

**Examples:**

```csharp
// Services - use "Service"
protected UserService Service = null!;
protected PaymentService Service = null!;
protected CostCalculationService Service = null!;

// Providers - use "Provider"
protected IFileProvider Provider = null!;
protected InstructionsTemplateProvider Provider = null!;

// Short names - use full name
protected Calculator Calculator = null!;
protected Validator Validator = null!;

// When clarity requires it - use full name
protected InstructionsTemplateProvider InstructionsTemplateProvider = null!;
protected UserProfileManager UserProfileManager = null!;
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
5. **Async when needed** - Use `async Task` for assertions and async operations, `void` for synchronous verifications
6. **Performance awareness** - Share expensive setup when appropriate
7. **Factory Methods and Flexible Defaults** - Use factory methods with `Option<T>` and minimal arguments for maintainable test setup
