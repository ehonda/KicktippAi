# Project Style Guide

This document defines conventions and best practices for writing tests in this project using TUnit.

## Quick Reference

- **Test names**: Fluent, sentence-like (`Adding_positive_numbers_returns_sum`)
- **Mocking**: Use **Moq**
- **Logging**: Use **FakeLogger**
- **Utilities**: Check `src/TestUtilities` before writing custom helpers
- **Structure**: Arrange-Act-Assert pattern
- **Async**: Use `async Task` for TUnit assertions; `void` for Moq.Verify/FakeLogger assertions
- **SUT naming**: `Service`, `Provider`, etc. (not `Sut`)
- **Factory methods**: Use `Option<T>` by default; `NullableOption<T>` only for null-testing
- **File organization**:
  - Simple: Single `{ClassName}Tests.cs`
  - Complex: Folder `{ClassName}Tests/` with `{ClassName}Tests_Base.cs` and `{ClassName}_{Method}_Tests.cs`

## Test Naming Conventions

### Fluent Test Names

**✅ Do:** Use fluent, descriptive names that read like sentences

```csharp
[Test]
public async Task Adding_positive_numbers_returns_sum()
{
    // Test implementation
}
```

**❌ Avoid:** Using the traditional `[MethodName]_[Scenario]_[Expected]` format with abbreviations

```csharp
[Test]
public async Task Add_PosNums_ReturnsSum() { }
```

### Naming Guidelines

1. **Start with the action** being tested (e.g., `Adding`, `Dividing`, `Validating`)
2. **Describe the scenario** clearly (e.g., `positive_numbers`, `by_zero`, `empty_string`)
3. **State the expected outcome** (e.g., `returns_sum`, `throws_exception`, `is_invalid`)
4. **Use underscores** to separate words for readability
5. **Be specific** - avoid vague names like `Test1` or `BasicTest`

## Mocking Library

This project uses **Moq** (version 4.20.72 or later) as the mocking library for creating test doubles. The source code and documentation reside on [GitHub](https://github.com/devlooped/moq).

## Test Utilities Library

This project provides a shared `TestUtilities` library (located at `src/TestUtilities`) with common test helper methods.

- When writing new tests utilizing common logic, make sure to check if a suitable helper method already exists in `TestUtilities` before implementing custom logic.

## Testing Logging

Always use `FakeLogger` for testing logging behavior, or when needing to pass a logger instance to entities involved in tests.

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

**Naming Conventions:**

- **Folder name**: `{ClassName}Tests` (e.g., `CostCalculationServiceTests`)
- **Base class**: `{ClassName}Tests_Base` (e.g., `CostCalculationServiceTests_Base`)
- **Test classes**: `{ClassName}_{MethodName}_Tests` (e.g., `CostCalculationService_CalculateCost_Tests`)

### Factory Methods and Flexible Defaults

Use factory methods in your test base class or derived test classes to create instances of the system under test (SUT) or complex dependencies. This centralizes object creation and makes tests more maintainable.

**Pattern: Factory Methods with `Option<T>` and `NullableOption<T>`**

Use `Option<T>` (from `EHonda.Optional.Core`) for factory method parameters by default. This allows tests to:

1. Override specific dependencies when relevant to the test.
2. Fall back to sensible defaults (mocks or fakes) for dependencies that don't matter for the specific test.

Use `NullableOption<T>` ONLY when you need to explicitly pass `null` (e.g., to test null guards).

**Base Class Implementation:**

```csharp
using EHonda.Optional.Core;

public abstract class MyServiceTests_Base
{
    protected static MyService CreateService(
        // Use NullableOption<T> here because we want to test passing null to the constructor
        NullableOption<IDependency> dependency = default,
        NullableOption<ILogger<MyService>> logger = default,
        // Use Option<T> here because this parameter is required by the SUT and we don't test nulls for it
        Option<string> config = default)
    {
        // Use provided dependency or create a default mock
        var actualDependency = dependency.Or(() => new Mock<IDependency>().Object);
        
        // Use provided logger or create a default fake
        var actualLogger = logger.Or(() => new FakeLogger<MyService>());

        // Use provided config or default value
        var actualConfig = config.Or("default-config");

        return new MyService(actualDependency!, actualLogger!, actualConfig);
    }
}
```

**Usage Guidelines:**

1. **Only specify what is relevant**: When calling factory methods, only provide arguments for the dependencies that are specific to the test scenario. Rely on the defaults for everything else.
2. **Minimize named arguments**: Do not use named arguments (e.g., `dependency: mock.Object`) unless necessary (i.e., when skipping preceding optional parameters). If you are passing the first parameter, pass it positionally.
3. **Use `Option<T>` by default**: Start with `Option<T>` for all parameters. Only change to `NullableOption<T>` if you specifically need to pass `null` in a test (e.g. for null guard tests).
4. **Argument passing preferences**: Implicit conversions are preferred, but might not always work (note that explicit `NullableOption.Some(value)` is never needed, because either the implicit conversion from `null` works, or the implicit conversion from explicit `Option.Some(value)` to `NullableOption.Some(value)`):
    1. Implicit conversion by passing a value of `T` as `Option<T>` or a value / `null` of `T?` as `NullableOption<T>`.
    2. Explicitly using `Option.Some(value)` for `Option<T>` or `NullableOption<T>`.

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
    // Use implicit conversion (pass object directly).
    var service = CreateService(mockDependency.Object);
    
    // ...
}

[Test]
public async Task Test_with_specific_logger()
{
    var logger = new FakeLogger<MyService>();
    
    // ✅ Good: Override only the logger.
    // Named argument is required here to skip the 'dependency' parameter.
    var service = CreateService(logger: logger);
    
    // ...
}

[Test]
public async Task Test_with_specific_config()
{
    // ✅ Good: Override only the config (Option<T> parameter).
    // Use implicit conversion (pass string directly).
    var service = CreateService(config: "custom-config");
    
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

    // ❌ Avoid: Explicitly wrapping in Option.Some when not needed
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

## Code Style

### Async Test Methods

**TUnit supports both synchronous and asynchronous test methods**, but the choice depends on what your test does:

#### Use `async Task` when

- Your test uses `await Assert.That(...)` - TUnit's assertion library returns awaitable objects
- Your test awaits any async operations
- You need to test async code

#### Use `void` (synchronous) when

- Your test uses `Moq.Verify(...)` (returns void, not awaitable)
- Your test has no async operations and no TUnit assertions

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
