# TUnit vs XUnit: Unit Testing Framework Comparison

This document provides a comprehensive comparison between TUnit and XUnit for common unit testing patterns in .NET.

## Overview

| Feature | TUnit | XUnit |
|---------|-------|-------|
| **Maturity** | Modern, fast and flexible framework | Community-focused, battle-tested framework |
| **Trust Score** | 9.7 | 8.8 |
| **Code Examples** | 5,212+ snippets | 875+ snippets |
| **Target** | Modern .NET (async-first) | .NET Framework 4.7.2+, .NET 8.0+ |
| **Philosophy** | Source generator-based, async-native | Attribute-based, convention-driven |
| **Instance Model** | Configurable | New instance per test method (default) |

---

## 1. Basic Test Definition

### TUnit
```csharp
using TUnit.Core;

namespace MyTestProject;

public class MyTestClass
{
    [Test]
    public async Task MyTest()
    {
        await Assert.That(value).IsEqualTo(expected);
    }
}
```

**Key Points:**
- All tests are async by default (return `Task`)
- Uses fluent assertion syntax with `await`
- Source generator-based for better performance
- Single `[Test]` attribute for all tests

### XUnit
```csharp
using Xunit;

namespace MyTestProject
{
    public class MyTestClass
    {
        [Fact]
        public void MyTest()
        {
            Assert.Equal(expected, actual);
        }
        
        // Async test
        [Fact]
        public async Task MyAsyncTest()
        {
            var result = await GetResultAsync();
            Assert.NotNull(result);
        }
    }
}
```

**Key Points:**
- `[Fact]` attribute for simple tests
- Tests can be void or async (`Task`)
- New test class instance created for each test method
- Classic assertion syntax (`Assert.Equal(expected, actual)`)

---

## 2. Parameterized Tests

### TUnit
```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(10, 20, 30)]
public async Task AdditionTest(int a, int b, int expected)
{
    await Assert.That(a + b).IsEqualTo(expected);
}

// With method data source
[Test]
[MethodDataSource(nameof(GetTestData))]
public async Task TestWithMethodData(string username, string password)
{
    // Test implementation
}

public static IEnumerable<object[]> GetTestData()
{
    yield return new object[] { "user1", "pass1" };
    yield return new object[] { "user2", "pass2" };
}
```

**Key Points:**
- `[Arguments]` attribute for inline data
- `[MethodDataSource]` for complex data generation
- Supports async data sources
- Fluent async assertions

### XUnit
```csharp
// Inline data
[Theory]
[InlineData(1, 2, 3)]
[InlineData(10, 20, 30)]
public void AdditionTest(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}

// Member data
[Theory]
[MemberData(nameof(GetTestData))]
public void TestWithMemberData(string username, string password)
{
    // Test implementation
}

public static IEnumerable<object[]> GetTestData()
{
    yield return new object[] { "user1", "pass1" };
    yield return new object[] { "user2", "pass2" };
}

// Using TheoryData (strongly typed)
public static TheoryData<int, int, int> AdditionData =>
    new TheoryData<int, int, int>
    {
        { 1, 2, 3 },
        { 10, 20, 30 }
    };

[Theory]
[MemberData(nameof(AdditionData))]
public void AdditionTestWithTheoryData(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}
```

**Key Points:**
- `[Theory]` attribute for parameterized tests
- `[InlineData]` for simple inline parameters
- `[MemberData]` for external data sources
- `TheoryData<T1, T2, ...>` for strongly-typed data (up to 10 parameters)
- Pre-enumeration can be controlled via configuration

---

## 3. Setup and Teardown

### TUnit
```csharp
public class MyTestClass
{
    private int _value;
    private static HttpResponseMessage? _pingResponse;

    // Runs once before all tests in the class
    [Before(Class)]
    public static async Task Ping()
    {
        _pingResponse = await new HttpClient().GetAsync("https://localhost/ping");
    }
    
    // Runs before each test
    [Before(Test)]
    public async Task Setup()
    {
        await Task.CompletedTask;
        _value = 99;
    }

    // Runs after each test
    [After(Test)]
    public async Task Teardown()
    {
        await Task.CompletedTask;
    }

    // Runs once after all tests
    [After(Class)]
    public static async Task Cleanup()
    {
        await Task.CompletedTask;
    }

    [Test]
    public async Task MyTest()
    {
        await Assert.That(_value).IsEqualTo(99);
    }
}
```

**Key Points:**
- `[Before(Class)]` / `[After(Class)]` for class-level setup/teardown
- `[Before(Test)]` / `[After(Test)]` for test-level setup/teardown
- All lifecycle methods support async
- Context objects available (e.g., `ClassHookContext`)

### XUnit
```csharp
// Constructor/Dispose pattern (per-test setup/teardown)
public class StackTests : IDisposable
{
    Stack<int> stack;

    // Runs before each test
    public StackTests()
    {
        stack = new Stack<int>();
    }

    // Runs after each test
    public void Dispose()
    {
        stack.Dispose();
    }

    [Fact]
    public void Test1()
    {
        Assert.Equal(0, stack.Count);
    }
}

// Class fixture (shared setup across tests in one class)
public class DatabaseFixture : IDisposable
{
    public DatabaseFixture()
    {
        Db = new SqlConnection("MyConnectionString");
        // ... initialize data in the test database ...
    }

    public void Dispose()
    {
        // ... clean up test data from the database ...
    }

    public SqlConnection Db { get; private set; }
}

public class MyDatabaseTests : IClassFixture<DatabaseFixture>
{
    DatabaseFixture fixture;

    public MyDatabaseTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        // Use fixture.Db
    }
}

// Async setup/teardown
public class AsyncLifetimeTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Async setup
        await Task.Delay(10);
    }

    public async Task DisposeAsync()
    {
        // Async cleanup
        await Task.Delay(10);
    }

    [Fact]
    public void Test1()
    {
        // Test code
    }
}
```

**Key Points:**
- Constructor/Dispose for per-test setup/teardown
- `IClassFixture<T>` for shared setup across tests in a class
- `IAsyncLifetime` for async initialization and cleanup
- New instance created per test by default (test isolation)

---

## 4. Shared Context Across Multiple Test Classes

### TUnit
```csharp
// Property injection with async initialization
public class AsyncContainer : IAsyncInitializable, IAsyncDisposable
{
    public bool IsInitialized { get; private set; }
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await Task.Delay(10);
        ConnectionString = "Server=localhost;Database=test";
        IsInitialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(1);
        IsInitialized = false;
        ConnectionString = "";
    }
}

[ClassDataSource<AsyncContainer>]
public required AsyncContainer Container { get; init; }

[Test]
public async Task TestWithAsyncInitializedProperty()
{
    await Assert.That(Container.IsInitialized).IsTrue();
    await Assert.That(Container.ConnectionString).IsNotEmpty();
}
```

**Key Points:**
- Property injection with `[ClassDataSource<T>]`
- Built-in support for `IAsyncInitializable` and `IAsyncDisposable`
- Automatic initialization before tests

### XUnit
```csharp
// Collection fixture (shared across multiple test classes)
public class DatabaseFixture : IDisposable
{
    public DatabaseFixture()
    {
        Db = new SqlConnection("MyConnectionString");
        // ... initialize data in the test database ...
    }

    public void Dispose()
    {
        // ... clean up test data from the database ...
    }

    public SqlConnection Db { get; private set; }
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

[Collection("Database collection")]
public class DatabaseTestClass1
{
    DatabaseFixture fixture;

    public DatabaseTestClass1(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        // Use fixture.Db
    }
}

[Collection("Database collection")]
public class DatabaseTestClass2
{
    DatabaseFixture fixture;

    public DatabaseTestClass2(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        // Use fixture.Db
    }
}

// Assembly-wide fixture (v3 only)
[assembly: AssemblyFixture(typeof(DatabaseFixture))]

public class AnyTestClass
{
    DatabaseFixture fixture;

    public AnyTestClass(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        // Use fixture.Db
    }
}
```

**Key Points:**
- `ICollectionFixture<T>` for sharing fixtures across multiple test classes
- Collection definition class to group tests
- `[Collection("name")]` attribute to assign tests to collections
- `[AssemblyFixture]` for assembly-wide fixtures (v3 only)
- Dependency injection via constructor parameters

---

## 5. Assertions

### TUnit
```csharp
// Fluent async assertions
await Assert.That(value).IsEqualTo(expected);
await Assert.That(value).IsNotNull();
await Assert.That(collection).IsEmpty();
await Assert.That(condition).IsTrue();
await Assert.That(value).IsGreaterThan(10);

// Exception assertions (conceptual)
await Assert.That(action).Throws<InvalidOperationException>();
```

**Key Points:**
- All assertions are async and fluent
- Strong typing with generics
- Modern API design
- Source generator creates optimized assertion code

### XUnit
```csharp
// Equality assertions
Assert.Equal(expected, actual);
Assert.NotEqual(expected, actual);
Assert.StrictEqual(expected, actual); // Reference equality

// Numeric assertions
Assert.InRange(actual, low, high);
Assert.NotInRange(actual, low, high);

// Boolean assertions
Assert.True(condition);
Assert.False(condition);

// Null assertions
Assert.Null(value);
Assert.NotNull(value);

// String assertions
Assert.Contains("substring", actual);
Assert.DoesNotContain("substring", actual);
Assert.StartsWith("prefix", actual);
Assert.EndsWith("suffix", actual);
Assert.Matches(@"regex", actual);

// Collection assertions
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Contains(expectedItem, collection);
Assert.DoesNotContain(unexpectedItem, collection);

// Single item in collection
var item = Assert.Single(collection);
Assert.Equal(expected, item);

// Collection with specific elements
Assert.Collection(collection,
    item => Assert.Equal(1, item),
    item => Assert.Equal(2, item),
    item => Assert.Equal(3, item)
);

// All items match condition
Assert.All(collection, item => Assert.True(item > 0));

// Async version for IAsyncEnumerable
await Assert.AllAsync(asyncCollection, item => Assert.True(item > 0));

// Exception assertions
Assert.Throws<InvalidOperationException>(() => method());
var ex = Assert.Throws<InvalidOperationException>(() => method());
Assert.Equal("Expected message", ex.Message);

// Async exception assertions
await Assert.ThrowsAsync<InvalidOperationException>(async () => await methodAsync());

// Type assertions
Assert.IsType<ExpectedType>(obj);
Assert.IsNotType<UnexpectedType>(obj);
Assert.IsAssignableFrom<BaseType>(obj);

// Event assertions
Assert.Raises<EventArgs>(
    handler => obj.Event += handler,
    handler => obj.Event -= handler,
    () => obj.TriggerEvent());

// Property changed assertions
Assert.PropertyChanged(obj, nameof(obj.Property), () => obj.Property = newValue);

// Multiple assertions (v3)
await Assert.MultipleAsync(() => 
{
    Assert.Equal(1, value1);
    Assert.Equal(2, value2);
    Assert.Equal(3, value3);
});

// Skipping tests at runtime
Assert.Skip("Reason for skipping");
Assert.SkipWhen(condition, "Reason");
Assert.SkipUnless(condition, "Reason");
```

**Key Points:**
- Rich assertion library with many specialized methods
- Clear, readable assertion syntax
- Support for `IAsyncEnumerable<T>` (.NET 6+)
- Support for `Span<T>`, `Memory<T>`, `ValueTask` (.NET 6+)
- Multiple assertion patterns for different scenarios

---

## 6. Async Testing

### TUnit
```csharp
// All tests are async by default
[Test]
public async Task AsyncTest()
{
    var result = await SomeAsyncOperation();
    await Assert.That(result).IsNotNull();
}

// Async property initialization
public class AsyncContainer : IAsyncInitializable, IAsyncDisposable
{
    public bool IsInitialized { get; private set; }
    
    public async Task InitializeAsync()
    {
        await Task.Delay(10);
        IsInitialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(1);
        IsInitialized = false;
    }
}
```

**Key Points:**
- Async-first design
- Built-in support for `IAsyncInitializable` and `IAsyncDisposable`
- Seamless async/await integration
- All assertions are async

### XUnit
```csharp
// Async tests return Task
[Fact]
public async Task AsyncTest()
{
    var result = await SomeAsyncOperation();
    Assert.NotNull(result);
}

// Async exception testing
[Fact]
public async Task TestAsyncException()
{
    await Assert.ThrowsAsync<InvalidOperationException>(
        async () => await MethodThatThrows());
}

// Async lifecycle
public class AsyncTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await SetupAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
    }

    [Fact]
    public async Task MyTest()
    {
        await DoSomethingAsync();
    }
}

// Testing IAsyncEnumerable
[Fact]
public async Task TestAsyncEnumerable()
{
    await Assert.AllAsync(GetAsyncData(), item => 
    {
        Assert.True(item > 0);
    });
}

async IAsyncEnumerable<int> GetAsyncData()
{
    yield return 1;
    await Task.Delay(10);
    yield return 2;
}
```

**Key Points:**
- Must explicitly return `Task` for async tests
- `IAsyncLifetime` for async setup/teardown
- Async assertions available (`ThrowsAsync`, `AllAsync`, etc.)
- Support for `IAsyncEnumerable<T>` (.NET 6+)
- Support for `ValueTask` assertions (.NET 6+)

---

## 7. Test Organization and Filtering

### TUnit
```csharp
[Test]
[Property("Category", "Integration")]
[Property("Priority", "High")]
public async Task IntegrationTest()
{
    // Test implementation
}

// Custom attributes
[Test, WindowsOnly, RetryOnHttpError(5)]
public async Task Windows_Specific_Feature()
{
    // Platform-specific test with custom retry logic
}

// Display name
[Test, DisplayName("Register a new account")]
public async Task Register_User()
{
    // Test implementation
}
```

**Key Points:**
- `[Property]` attribute for metadata
- Support for custom conditional attributes
- `[DisplayName]` for readable test names
- Extensible filtering system

### XUnit
```csharp
// Using traits for categorization
[Fact]
[Trait("Category", "Integration")]
[Trait("Priority", "High")]
public void IntegrationTest()
{
    // Test implementation
}

// Test display name
[Fact(DisplayName = "Register a new account")]
public void Register_User()
{
    // Test implementation
}

// Skipping tests
[Fact(Skip = "Not implemented yet")]
public void SkippedTest()
{
    // This test is skipped
}

// Explicit tests (v3 only - not run by default)
[Fact(Explicit = true)]
public void ExplicitTest()
{
    // Must be explicitly run
}

// Theory with skip on individual data row
[Theory]
[InlineData(1, 2, 3)]
[InlineData(4, 5, 9, Skip = "Known issue")]
public void ParameterizedTest(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}

// Custom labels for theory data rows (v3)
public static TheoryData<int, string> TestData =>
    new TheoryData<int, string>
    {
        { 1, "test1" },
        { 2, "test2" }
    }.WithDisplayName(row => $"Test with value {row[0]}");
```

**Key Points:**
- `[Trait]` for categorization and filtering
- `DisplayName` property for readable test names
- `Skip` property to skip tests with a reason
- `Explicit` property for tests that must be explicitly run (v3)
- Conditional skipping at data row level
- Custom labels for theory data

---

## 8. Test Collections and Parallelization

### TUnit
```csharp
[Test]
[Repeat(100)]
[ParallelLimit<LoadTestParallelLimit>]
public async Task Load_Test_Homepage()
{
    // Performance testing with controlled parallelism
}

public class LoadTestParallelLimit : IParallelLimit
{
    public int Limit => 10;
}

[Test, DependsOn(nameof(Setup_Test))]
public async Task Dependent_Test()
{
    // This test runs after Setup_Test completes
}
```

**Key Points:**
- `[Repeat]` for performance testing
- Granular parallel execution control via `IParallelLimit`
- `[DependsOn]` for explicit test dependencies
- Built-in orchestration features

### XUnit
```csharp
// Tests in different classes run in parallel by default

// Disable parallelization for specific collection
[CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
public class NonParallelCollection
{
}

[Collection("Non-Parallel Collection")]
public class TestClass1
{
    [Fact]
    public void Test1() { }
}

[Collection("Non-Parallel Collection")]
public class TestClass2
{
    [Fact]
    public void Test2() { }
}

// Assembly-level parallelization control
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

// Tests in same collection run sequentially
[Collection("Database tests")]
public class DatabaseTest1
{
    [Fact]
    public void Test1()
    {
        Thread.Sleep(3000);
    }
}

[Collection("Database tests")]
public class DatabaseTest2
{
    [Fact]
    public void Test2()
    {
        Thread.Sleep(5000);
    }
}
```

**Key Points:**
- Tests in different classes run in parallel by default
- Tests in the same collection run sequentially
- Collection-level parallelization control
- Assembly-level parallelization settings
- No built-in test dependencies (tests should be independent)

---

## 9. Test Lifecycle and Instance Management

### TUnit
```csharp
// Configurable instance model (details depend on implementation)
public class MyTestClass
{
    private int _counter = 0;

    [Test]
    public async Task Test1()
    {
        _counter++;
        await Assert.That(_counter).IsEqualTo(1);
    }

    [Test]
    public async Task Test2()
    {
        _counter++;
        await Assert.That(_counter).IsEqualTo(1);
    }
}
```

**Key Points:**
- Source generator-based lifecycle management
- Configurable instance creation
- Explicit lifecycle control via attributes

### XUnit
```csharp
// New instance per test (default behavior)
public class MyTestClass
{
    private int _counter = 0;

    public MyTestClass()
    {
        // Constructor runs for each test
        _counter = 0;
    }

    [Fact]
    public void Test1()
    {
        _counter++;
        Assert.Equal(1, _counter); // Always 1
    }

    [Fact]
    public void Test2()
    {
        _counter++;
        Assert.Equal(1, _counter); // Always 1
    }
}

// Test execution flow:
// 1. Constructor
// 2. IAsyncLifetime.InitializeAsync (if implemented)
// 3. Test method
// 4. IAsyncLifetime.DisposeAsync (if implemented)
// 5. IDisposable.Dispose (if implemented)
```

**Key Points:**
- **Fresh instance for each test** (test isolation by default)
- Constructor runs before each test
- Dispose runs after each test
- Predictable, convention-based lifecycle
- No shared state between tests in same class (unless using fixtures)

---

## 10. Timeout Handling

### TUnit
```csharp
[Test]
[Timeout(5000)]  // 5 seconds
public async Task TestWithTimeout()
{
    await SomeLongRunningOperation();
}
```

**Key Points:**
- Built-in timeout support
- Works without requiring separate thread execution

### XUnit
```csharp
// Timeout attribute (requires async test in v2.7+)
[Fact(Timeout = 5000)]  // 5 seconds in milliseconds
public async Task TestWithTimeout()
{
    await SomeLongRunningOperation();
}

// Note: In v2.7+, timeout requires async test methods
// Non-async tests with timeout will fail
```

**Key Points:**
- Timeout in milliseconds
- Must be async test method (v2.7+)
- Test fails if it exceeds timeout
- Timeout ignored on non-async tests in older versions

---

## 11. Test Retries and Stability

### TUnit
```csharp
[Test]
[Retry(3)]
public async Task UnstableTest()
{
    // Retries up to 3 times on failure
    await Assert.That(await GetUnstableResult()).IsTrue();
}

[Test, DependsOn(nameof(Setup_Test))]
[Retry(3)]
public async Task DependentTestWithRetry()
{
    // Runs after Setup_Test, retries if needed
}
```

**Key Points:**
- Built-in `[Retry]` attribute
- Configurable retry count
- Works with test dependencies
- Integrated into test orchestration

### XUnit
```csharp
// No built-in retry mechanism
// Can implement custom retry with custom attributes

public class RetryFactAttribute : FactAttribute
{
    private int _maxRetries;
    
    public RetryFactAttribute(int maxRetries = 3)
    {
        _maxRetries = maxRetries;
    }
    
    // Custom implementation needed for retry logic
}

// Alternative: Use external retry libraries or test runners
// that support retries
```

**Key Points:**
- **No built-in retry mechanism**
- Must implement custom retry logic if needed
- Some test runners may provide retry capabilities
- Tests should ideally be deterministic and not need retries

---

## 12. Data-Driven Testing Advanced Features

### TUnit
```csharp
// Async data sources
[Test]
[MethodDataSource(nameof(GetAsyncData))]
public async Task TestWithAsyncData(string value)
{
    await Assert.That(value).IsNotEmpty();
}

public static async IAsyncEnumerable<object[]> GetAsyncData()
{
    await Task.Delay(10);
    yield return new object[] { "test1" };
    await Task.Delay(10);
    yield return new object[] { "test2" };
}
```

**Key Points:**
- Native async data source support
- Source generator optimizes data retrieval
- Flexible data source patterns

### XUnit
```csharp
// Member data
[Theory]
[MemberData(nameof(TestData))]
public void TestWithMemberData(int value)
{
    Assert.True(value > 0);
}

public static IEnumerable<object[]> TestData =>
    new List<object[]>
    {
        new object[] { 1 },
        new object[] { 2 },
        new object[] { 3 }
    };

// Strongly-typed theory data
public static TheoryData<int, string, bool> ComplexData =>
    new TheoryData<int, string, bool>
    {
        { 1, "test1", true },
        { 2, "test2", false }
    };

[Theory]
[MemberData(nameof(ComplexData))]
public void TestComplex(int id, string name, bool flag)
{
    // Test logic
}

// Theory data with skip (v3)
public static TheoryData<int> ConditionalData()
{
    var data = new TheoryData<int> { 1, 2 };
    data.Add(3, skip: "Not ready yet");
    return data;
}

// Skip theory without data (v3)
[Theory(SkipTestWithoutData = true)]
[MemberData(nameof(EmptyData))]
public void TestThatMayHaveNoData(int value)
{
    // Skipped if EmptyData returns no items
}

// External data from different class
[Theory]
[MemberData(nameof(ExternalDataClass.TestData), 
    MemberType = typeof(ExternalDataClass))]
public void TestWithExternalData(int value)
{
    Assert.True(value > 0);
}

public class ExternalDataClass
{
    public static IEnumerable<object[]> TestData =>
        new List<object[]> { new object[] { 1 }, new object[] { 2 } };
}
```

**Key Points:**
- `TheoryData<T1, T2, ...>` for strongly-typed data (up to 10 type parameters)
- External data sources via `MemberType`
- Conditional skipping at data row level (v3)
- `SkipTestWithoutData` for optional data (v3)
- Pre-enumeration control for better IDE support

---

## 13. Custom Extensions and Attributes

### TUnit
```csharp
// Custom conditional attributes
[Test, WindowsOnly]
public async Task WindowsSpecificTest()
{
    // Only runs on Windows
}

// Custom retry logic
[Test, RetryOnHttpError(5)]
public async Task TestWithCustomRetry()
{
    // Custom retry behavior
}

// Source generator can optimize custom attributes
```

**Key Points:**
- Extensible attribute system
- Source generator can optimize custom logic
- Custom orchestration attributes
- Platform-specific execution control

### XUnit
```csharp
// Custom fact attribute
[XunitTestCaseDiscoverer(typeof(MyCustomFactDiscoverer))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class MyCustomFactAttribute : Attribute, IFactAttribute
{
    public string DisplayName { get; set; }
    public string Skip { get; set; }
    // Implement custom discovery logic
}

// Custom theory data attribute
public class CustomDataAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        // Custom data generation logic
        yield return new object[] { "custom1" };
        yield return new object[] { "custom2" };
    }
}

[Theory]
[CustomData]
public void TestWithCustomData(string value)
{
    Assert.NotEmpty(value);
}

// Custom trait attribute
[TraitDiscoverer(typeof(CategoryDiscoverer))]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class CategoryAttribute : Attribute, ITraitAttribute
{
    public CategoryAttribute(string category) { }
}
```

**Key Points:**
- Extensible via discoverers
- `IFactAttribute` for custom test attributes
- `DataAttribute` for custom theory data
- `ITraitAttribute` for custom categorization
- Reflection-based extensibility

---

## 14. Output and Diagnostics

### TUnit
```csharp
[Test]
public async Task TestWithContext(TestContext context)
{
    context.WriteLine("Test output");
    await Assert.That(true).IsTrue();
}
```

**Key Points:**
- Context injection for output
- Output captured automatically
- Metadata available via context

### XUnit
```csharp
public class MyTests
{
    private readonly ITestOutputHelper _output;

    public MyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestWithOutput()
    {
        _output.WriteLine("Test output");
        _output.WriteLine($"Testing at {DateTime.Now}");
        Assert.True(true);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheoryWithOutput(int value)
    {
        _output.WriteLine($"Testing with value: {value}");
        Assert.True(value > 0);
    }
}
```

**Key Points:**
- `ITestOutputHelper` injected via constructor
- Output captured per-test
- Thread-safe output handling
- Output visible in test results

---

## 15. Configuration

### TUnit
```csharp
// Configuration via attributes and source generation
// Specific configuration patterns depend on implementation
```

**Key Points:**
- Source generator-based configuration
- Compile-time optimization
- Attribute-driven settings

### XUnit
```csharp
// xunit.runner.json or testconfig.json
{
  "maxParallelThreads": 4,
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "preEnumerateTheories": true,
  "diagnosticMessages": true,
  "internalDiagnosticMessages": false
}

// Assembly-level configuration
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
[assembly: TestFramework("CustomTestFramework.CustomFramework", "CustomTestFramework")]
```

**Key Points:**
- JSON configuration files
- Assembly-level attributes
- Runtime configuration
- Flexible parallelization settings
- Custom test framework support

---

## Summary: Which Should You Choose?

### Choose TUnit if you:
- Are building new projects on modern .NET
- Want async-first testing with fluent assertions
- Need advanced orchestration (dependencies, retries)
- Prefer source generator performance benefits
- Want modern, opinionated API design
- Need fine-grained parallel execution control
- Value integrated test lifecycle management

### Choose XUnit if you:
- Have existing XUnit test suites
- Need mature, battle-tested framework
- Value convention over configuration
- Want excellent community support and tooling
- Prefer flexible, extensible architecture
- Need proven stability in production
- Want strong IDE integration (VS, Rider, VS Code)
- Value test isolation via fresh instances
- Need cross-platform compatibility (.NET Framework to .NET 8+)

### Key Philosophical Differences

| Aspect | TUnit | XUnit |
|--------|-------|-------|
| **Test Attributes** | Single `[Test]` attribute | `[Fact]` for simple, `[Theory]` for parameterized |
| **Async** | Async-first, all tests async | Async optional, explicit `Task` return |
| **Assertions** | Fluent async (`await Assert.That(x).IsY()`) | Classic static (`Assert.X(y)`) |
| **Setup/Teardown** | Explicit attributes (`[Before]`, `[After]`) | Constructor/Dispose pattern |
| **Shared Context** | Property injection, data sources | Fixtures with dependency injection |
| **Instance Model** | Configurable | Fresh instance per test (isolation) |
| **Parallelization** | Granular control with limits | Collection-based control |
| **Extensions** | Source generator-based | Reflection-based discoverers |

### Migration Path from XUnit to TUnit

```csharp
// XUnit
[Fact]
public void MyTest()
{
    Assert.Equal(expected, actual);
}

[Theory]
[InlineData(1, 2, 3)]
public void ParameterizedTest(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}

// TUnit
[Test]
public async Task MyTest()
{
    await Assert.That(actual).IsEqualTo(expected);
}

[Test]
[Arguments(1, 2, 3)]
public async Task ParameterizedTest(int a, int b, int expected)
{
    await Assert.That(a + b).IsEqualTo(expected);
}
```

**Key Migration Steps:**
1. Replace `[Fact]` with `[Test]`
2. Replace `[Theory]` + `[InlineData]` with `[Test]` + `[Arguments]`
3. Make all tests async (return `Task`)
4. Use `await` with assertions
5. Update `[MemberData]` to `[MethodDataSource]`
6. Replace constructor/Dispose with `[Before(Test)]`/`[After(Test)]`
7. Replace `IClassFixture<T>` with appropriate TUnit fixture pattern
8. Update assertion syntax to TUnit's fluent async style

---

## Performance Considerations

### TUnit
- **Source Generator Based**: Generates optimized code at compile-time
- **Async Native**: Designed for modern async workflows
- **Lower Overhead**: Less reflection, more direct execution
- **Parallel by Default**: Better parallel execution control
- **Compile-Time Optimization**: Attributes processed during build

### XUnit
- **Reflection Based**: Runtime discovery and execution
- **Mature Optimizer**: Years of performance tuning
- **Efficient Parallelization**: Well-optimized parallel execution
- **Instance Isolation**: Fresh instances ensure clean state
- **Proven Scale**: Used in large enterprise projects

---

## Tooling and IDE Support

### TUnit
- Modern .NET CLI integration
- Growing IDE support
- Source generator integration in Visual Studio/Rider
- Newer ecosystem, evolving tooling

### XUnit
- **Excellent Visual Studio integration** (Test Explorer)
- **Strong ReSharper support**
- **Native Rider support**
- **VS Code extensions** available
- **Wide CI/CD platform support**
- **Rich test adapters** (VSTest, dotnet test, etc.)
- **Mature ecosystem** with extensive community tools

---

## Conclusion

Both frameworks are excellent choices for .NET unit testing. **TUnit** represents a modern, async-first approach with cutting-edge features like source generators, built-in test orchestration, and fluent async assertions. **XUnit** provides battle-tested stability with excellent tooling support, a convention-based approach that emphasizes test isolation through fresh instances, and a flexible, extensible architecture.

**XUnit's strengths:**
- Convention over configuration (constructor/Dispose, fresh instances)
- Mature ecosystem with extensive tooling
- Flexible fixture system (class, collection, assembly)
- No global state or shared instances by default
- Proven in large-scale production environments

**TUnit's strengths:**
- Modern async-first design
- Source generator performance
- Built-in test orchestration (dependencies, retries)
- Fluent assertion syntax
- Integrated lifecycle management

Your choice should depend on your project requirements, team expertise, existing codebase, and whether you prioritize modern language features or proven stability and tooling.
