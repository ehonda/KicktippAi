# Project Style Guide

This document defines conventions and best practices for writing tests in this project using TUnit.

## Quick Reference

- **Test names**: Fluent, sentence-like (`Adding_positive_numbers_returns_sum`)
- **Mocking**: Use **Moq**
- **Logging**: Use **FakeLogger**
- **Utilities**: Check `src/TestUtilities` before writing custom helpers
- **CSV Assertions**: Use `IsEqualToWithNormalizedLineEndings`
- **Structure**: Arrange-Act-Assert pattern
- **Async**: Use `async Task` for TUnit assertions; `void` for Moq.Verify/FakeLogger assertions
- **SUT naming**: `Service`, `Provider`, etc. (not `Sut`)
- **Factory methods**: Use `Option<T>` by default; `NullableOption<T>` only for null-testing
- **File organization**:
  - Simple: Single `{ClassName}Tests.cs`
  - Complex: Folder `{ClassName}Tests/` with `{ClassName}Tests_Base.cs` and `{ClassName}_{Method}_Tests.cs`
- **No regions**: Do not use `#region`/`#endregion` in test files
- **Temp directories**: Use `TempDirectoryTestBase` (or `TempDirectoryWithEncryptionKeyTestBase` for encryption tests)
- **Cleanup**: Use `[After(Test)]` hooks, not `IAsyncDisposable`

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

### Core Test Factories

The `CoreTestFactories` class provides factory methods for creating Core domain objects (such as `Match`, `Prediction`, `BonusQuestion`, etc.) with sensible defaults. Use these in your tests to:

1. Reduce boilerplate code
2. Ensure consistency across test files
3. Override only the properties relevant to each test

**Using CoreTestFactories:**

```csharp
// ✅ Good: Import the factory class for cleaner syntax
using static TestUtilities.CoreTestFactories;

[Test]
public async Task Match_can_be_stored_and_retrieved()
{
    var match = CreateMatch(matchday: 10);  // Only override what matters for this test
    await repository.StoreMatchAsync(match);
    // ...
}

// ✅ Good: Override only relevant parameters
var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
var bonusQuestion = CreateBonusQuestion(text: "Who will win?");
```

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

- **IMPORTANT:** If you need to pass `null` for a dependency in a test, you MUST USE `NullableOption<T>` for that parameter in the factory method, as `Option<T>` does not support `null` values.

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

**⚠️ Collection literals with `Option<List<T>>`:**

Collection literals (e.g., `[]`, `["a", "b"]`) cannot be implicitly converted to `Option<List<T>>`. This causes build errors when passing collection literals to factory methods with `Option<List<T>>` parameters.

```csharp
// Given: CreateFoo(Option<List<string>> items = default)

// ❌ Build error: Cannot convert collection literal to Option<List<string>>
var foo = CreateFoo(items: ["a", "b"]);

// ✅ Works: Wrap in explicit list constructor
var foo = CreateFoo(items: new List<string> { "a", "b" });

// ✅ Works: Use List<T> with collection expression
var foo = CreateFoo(items: new List<string>(["a", "b"]));
```

## Temporary Directories for Tests

Tests that require temporary files or directories should use the `TempDirectoryTestBase` base class (located at `tests/Orchestrator.Tests/Infrastructure/TempDirectoryTestBase.cs`). This provides:

1. **Unique directories per test** - Each test gets a GUID-named subdirectory
2. **Automatic cleanup** - Directories are deleted after tests complete
3. **Stale directory cleanup** - Old directories from interrupted test runs are cleaned up before the test class runs

### Using TempDirectoryTestBase

Inherit from `TempDirectoryTestBase` and override `TestDirectoryName`:

```csharp
using Orchestrator.Tests.Infrastructure;

public class MyFileTests : TempDirectoryTestBase
{
    protected override string TestDirectoryName => "MyFileTests";

    [Test]
    public async Task Writing_file_creates_file_in_temp_directory()
    {
        // TestDirectory is the unique path for this test run
        var filePath = Path.Combine(TestDirectory, "output.txt");
        
        await File.WriteAllTextAsync(filePath, "content");
        
        await Assert.That(File.Exists(filePath)).IsTrue();
    }
}
```

### How the Base Classes Work

The base classes use TUnit's lifecycle hooks:

1. **`[Before(Class)]`** (static) - Cleans up all stale subdirectories from previous test runs
2. **`[Before(Test)]`** - Creates a unique GUID-named subdirectory for the test
3. **`[After(Test)]`** - Deletes the subdirectory after the test completes

**Directory structure:**

```text
%TEMP%/
  {TestDirectoryName}/           # e.g., "SnapshotsEncryptTests"
    {guid-1}/                    # Directory for test run 1
    {guid-2}/                    # Directory for test run 2
    ...
```

The `[Before(Class)]` hook deletes all subdirectories under `{TestDirectoryName}`, ensuring cleanup even if a previous test run was interrupted (e.g., process killed).

### Why Not Use IAsyncDisposable?

TUnit guarantees that `[After(Test)]` hooks run even when tests throw exceptions. This makes `IAsyncDisposable` redundant for cleanup purposes. Use `[After(Test)]` instead:

```csharp
// ✅ Good: Use TUnit's After hook for cleanup
[After(Test)]
public void TearDown()
{
    _resource?.Dispose();
}

// ❌ Avoid: IAsyncDisposable is unnecessary with TUnit's guaranteed cleanup
public class MyTests : IAsyncDisposable
{
    public async ValueTask DisposeAsync() { ... }
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
public async Task Processing_valid_input_returns_expected_result()
{
    // Arrange - Set up test data and dependencies
    var service = new MyService();
    var input = new InputData { Value = 42 };

    // Act - Execute the method being tested
    var result = await service.ProcessAsync(input);

    // Assert - Verify the outcome
    await Assert.That(result).IsNotNull().And.HasValue(42);
}
```

### No Regions

Do not use `#region`/`#endregion` blocks in test files. Regions hide code and make navigation harder. If you find yourself wanting to group tests into regions:

1. **Consider splitting the file**: If there are distinct groups of tests, create separate test files (e.g., `MyService_MethodA_Tests.cs`, `MyService_MethodB_Tests.cs`)
2. **Use XML documentation**: Add `/// <summary>` comments to describe test groupings or scenarios
3. **Order tests logically**: Group related tests together by method/scenario without artificial separators

```csharp
// ❌ Avoid: Using regions to group tests
#region Validation Tests

[Test]
public async Task Empty_input_throws_exception() { }

[Test]
public async Task Null_input_throws_exception() { }

#endregion

// ✅ Good: Use XML documentation for logical groupings when needed
/// <summary>
/// Tests for input validation scenarios.
/// </summary>
[Test]
public async Task Empty_input_throws_exception() { }

[Test]
public async Task Null_input_throws_exception() { }
```

### Assertion Chaining

When asserting multiple properties of the same object, use TUnit's assertion chaining to keep tests concise and readable.

```csharp
// ✅ Good: Chained assertions
await Assert.That(result)
    .IsNotNull()
    .And.HasValue(42);

// ❌ Avoid: Multiple separate assertions for the same object
await Assert.That(result).IsNotNull();
await Assert.That(result.Value).IsEqualTo(42);
```

### Record Equality Assertions

When asserting objects that are records or have value equality, prefer using `IsEqualTo` with an expected object instead of asserting individual properties. This is more concise and ensures complete equality.

```csharp
// ✅ Good: Use record equality
var expected = new DocumentContext("team-data", "team content");
await Assert.That(context).IsEqualTo(expected);

// ✅ Good: Inline expected object
await Assert.That(result).IsEqualTo(new Prediction(2, 1, null));

// ❌ Avoid: Member-by-member assertions when record equality suffices
await Assert.That(context).Member(c => c.Name, n => n.IsEqualTo("team-data"))
    .And.Member(c => c.Content, c => c.IsEqualTo("team content"));
```

**When to use record equality:**

- The type is a `record` or has proper `Equals` implementation
- You want to verify all properties match
- The expected object can be easily constructed

**When to use member assertions:**

- You only need to verify specific properties (partial assertion)
- The type doesn't have proper equality semantics
- Constructing a complete expected object would be impractical

**⚠️ Records with collection properties:**

Records use reference equality for collection properties (e.g., `List<T>`, `Dictionary<K,V>`). For records containing collections, compare the collection property directly:

```csharp
// Given: record BonusPrediction(List<string> SelectedOptionIds)

// ❌ Fails: List uses reference equality
await Assert.That(retrieved).IsEqualTo(prediction);

// ✅ Works: Compare collection contents directly
await Assert.That(retrieved!.SelectedOptionIds).IsEquivalentTo(prediction.SelectedOptionIds);
```

### Collection Equality Assertions

When asserting collections, prefer `IsEquivalentTo` with an expected collection over asserting individual elements by index.

```csharp
// ✅ Good: Collection equality
var expected = new List<DocumentContext>
{
    new("team-data", "team content"),
    new("manager-data", "manager content")
};
await Assert.That(contexts).IsEquivalentTo(expected);

// ✅ Good: Inline collection literal
await Assert.That(contexts).IsEquivalentTo([
    new DocumentContext("team-data", "team content"),
    new DocumentContext("manager-data", "manager content")
]);

// ✅ Good: For ordered comparisons with partial data
await Assert.That(versions.Select(v => (v.Version, v.Content)))
    .IsEquivalentTo([(0, "v0"), (1, "v1"), (2, "v2")]);

// ❌ Avoid: Element-by-element assertions
await Assert.That(contexts[0]).Member(c => c.Name, n => n.IsEqualTo("team-data"));
await Assert.That(contexts[1]).Member(c => c.Name, n => n.IsEqualTo("manager-data"));
```

**When to use collection assertions:**

- Verifying entire collection contents
- Order matters: use `IsEquivalentTo` (order-agnostic) or `IsEqualTo` (order-sensitive)
- The expected collection can be easily constructed

**When to use element assertions:**

- You only need to verify specific elements exist (use `.Contains()` or `.Any()`)
- The collection has dynamic values that can't be fully predicted

### CSV Assertions

When asserting CSV string data, always use `IsEqualToWithNormalizedLineEndings` to handle line ending differences (CRLF vs LF) consistently. This is crucial because CSV writers often use CRLF (in compliance with RFC 4180) while test strings might use LF (for example, if that is the environment's newline sequence).

```csharp
// ✅ Good: Use the custom assertion for CSV data
await Assert.That(csvContent).IsEqualToWithNormalizedLineEndings(expectedCsv);

// ❌ Avoid: Standard string equality checks for CSVs
await Assert.That(csvContent).IsEqualTo(expectedCsv);
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

## Running and Filtering Tests

### Running Tests

Always use `dotnet run` instead of `dotnet test` to run TUnit tests:

```powershell
dotnet run --project tests/MyProject.Tests
```

To see available command-line options:

```powershell
dotnet run --project tests/MyProject.Tests -- --help
```

### Filtering Tests

Use `--treenode-filter` to run specific tests. The filter syntax is:

```text
/<Assembly>/<Namespace>/<Class>/<Test>
```

Use `*` as a wildcard and `**` for multi-level matching.

**Common Filter Patterns:**

| Goal | Command |
|------|---------|
| Run all tests in a class | `dotnet run -- --treenode-filter "/*/*/MyTestClass/*"` |
| Run a specific test | `dotnet run -- --treenode-filter "/*/*/*/My_test_name"` |
| Run tests matching a prefix | `dotnet run -- --treenode-filter "/*/*/*/Adding_*"` |
| Run all tests in matching classes | `dotnet run -- --treenode-filter "/*/*/MyService*/**"` |

**Combining Filters:**

Use `&` (AND) and `|` (OR) operators. OR requires parentheses at the name level:

```powershell
# Tests starting with "Valid" OR "Invalid"
dotnet run -- --treenode-filter "/*/*/*/(Valid*)|(Invalid*)"
```

**Filtering by Properties:**

Filter tests by custom properties using `[PropertyName=Value]`:

```powershell
dotnet run -- --treenode-filter "/*/*/*/*[Category=Unit]"
```

### Listing Available Tests

To see all available tests without running them:

```powershell
dotnet run -- --list-tests
```
