# TUnit vs NUnit: Unit Testing Framework Comparison

This document provides a comprehensive comparison between TUnit and NUnit for common unit testing patterns in .NET.

## Overview

| Feature | TUnit | NUnit |
|---------|-------|-------|
| **Maturity** | Modern, fast and flexible framework | Established framework for all .NET languages |
| **Trust Score** | 9.7 | 7.8 |
| **Code Examples** | 5,212+ snippets | 368+ snippets |
| **Target** | Modern .NET (async-first) | .NET Framework, .NET Core, .NET 5+ |
| **Philosophy** | Source generator-based, async-native | Attribute-based, reflection-driven |

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

### NUnit
```csharp
using NUnit.Framework;

namespace MyTestProject
{
    public class MyTestClass
    {
        [Test]
        public void MyTest()
        {
            Assert.AreEqual(expected, actual);
            // Or constraint model:
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
```

**Key Points:**
- Tests can be void or async (`Task`)
- Classic assertions (`Assert.AreEqual`) or constraint model (`Assert.That`)
- Async tests must explicitly return `Task` (void async not supported in modern versions)

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
[MethodDataSource(nameof(GetTestUsers))]
public async Task Register_User(string username, string password)
{
    // Test implementation
}

public static IEnumerable<object[]> GetTestUsers()
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

### NUnit
```csharp
[TestCase(1, 2, 3)]
[TestCase(10, 20, 30)]
public void AdditionTest(int a, int b, int expected)
{
    Assert.AreEqual(expected, a + b);
}

// With test case source
[TestCaseSource(nameof(GetTestUsers))]
public void Register_User(string username, string password)
{
    // Test implementation
}

public static IEnumerable<object[]> GetTestUsers()
{
    yield return new object[] { "user1", "pass1" };
    yield return new object[] { "user2", "pass2" };
}
```

**Key Points:**
- `[TestCase]` for inline data
- `[TestCaseSource]` for external data
- Can omit arguments with default values
- Supports platform-specific tests via `Platform` property

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

### NUnit
```csharp
public class MyTestClass
{
    private int _value;
    private static HttpResponseMessage _pingResponse;

    // Runs once before all tests in the class
    [OneTimeSetUp]
    public async Task Ping()
    {
        _pingResponse = await new HttpClient().GetAsync("https://localhost/ping");
    }
    
    // Runs before each test
    [SetUp]
    public async Task Setup()
    {
        await Task.CompletedTask;
        _value = 99;
    }

    // Runs after each test
    [TearDown]
    public async Task Teardown()
    {
        await Task.CompletedTask;
    }

    // Runs once after all tests
    [OneTimeTearDown]
    public async Task Cleanup()
    {
        await Task.CompletedTask;
    }

    [Test]
    public void MyTest()
    {
        Assert.AreEqual(99, _value);
    }
}
```

**Key Points:**
- `[OneTimeSetUp]` / `[OneTimeTearDown]` for fixture-level
- `[SetUp]` / `[TearDown]` for test-level
- Methods can be async (return `Task`)
- Failures in OneTimeSetUp suppress individual test failures

---

## 4. Assertions

### TUnit
```csharp
// Fluent async assertions
await Assert.That(value).IsEqualTo(expected);
await Assert.That(value).IsNotNull();
await Assert.That(collection).IsEmpty();
await Assert.That(condition).IsTrue();
await Assert.That(value).IsGreaterThan(10);

// Exception assertions (conceptual based on docs)
await Assert.That(action).Throws<InvalidOperationException>();
```

**Key Points:**
- All assertions are async and fluent
- Strong typing with generics
- Modern API design
- Source generator creates optimized assertion code

### NUnit
```csharp
// Classic model
Assert.AreEqual(expected, actual);
Assert.IsTrue(condition);
Assert.IsNotNull(value);
Assert.Throws<InvalidOperationException>(() => action());

// Constraint model (recommended)
Assert.That(actual, Is.EqualTo(expected));
Assert.That(condition, Is.True);
Assert.That(value, Is.Not.Null);
Assert.That(collection, Is.Empty);
Assert.That(value, Is.GreaterThan(10));
Assert.That(() => action(), Throws.TypeOf<InvalidOperationException>());

// Async assertions
Assert.That(async () => await asyncAction(), Throws.Nothing);
Assert.ThrowsAsync<InvalidOperationException>(async () => await asyncAction());

// Collection assertions
CollectionAssert.AreEquivalent(expected, actual);
Assert.That(collection, Is.EquivalentTo(expected));

// Additional assertions
Assert.Zero(value);
Assert.NotZero(value);
```

**Key Points:**
- Two models: Classic and Constraint
- Constraint model is more flexible and readable
- Special handling for async exceptions
- Rich collection assertions

---

## 5. Async Testing

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

[ClassDataSource<AsyncContainer>]
public required AsyncContainer Container { get; init; }

[Test]
public async Task TestWithAsyncInitializedProperty()
{
    await Assert.That(Container.IsInitialized).IsTrue();
}
```

**Key Points:**
- Async-first design
- Built-in support for `IAsyncInitializable` and `IAsyncDisposable`
- Seamless async/await integration
- All assertions are async

### NUnit
```csharp
// Async tests must return Task
[Test]
public async Task AsyncTest()
{
    var result = await SomeAsyncOperation();
    Assert.That(result, Is.Not.Null);
}

// Async setup/teardown
[SetUp]
public async Task SetupAsync()
{
    await InitializeAsync();
}

// Testing async exceptions
[Test]
public void TestAsyncException()
{
    Assert.ThrowsAsync<InvalidOperationException>(
        async () => await MethodThatThrows());
}

// Constraint with async delegate
[Test]
public void TestConstraintWithAsync()
{
    Assert.That(async () => await AsyncMethod(), Throws.Nothing);
}
```

**Key Points:**
- Must explicitly return `Task` for async tests
- Void async not supported (breaking change from older versions)
- Special `ThrowsAsync` for async exceptions
- Setup/teardown can be async
- Windows Forms/WPF message pump detection to avoid deadlocks

---

## 6. Test Dependencies and Ordering

### TUnit
```csharp
[Test, DisplayName("Register a new account")]
[MethodDataSource(nameof(GetTestUsers))]
public async Task Register_User(string username, string password)
{
    // Test implementation
}

[Test, DependsOn(nameof(Register_User))]
[Retry(3)]
public async Task Login_With_Registered_User(string username, string password)
{
    // This test runs after Register_User completes
}

[Test]
[Repeat(100)]
[ParallelLimit<LoadTestParallelLimit>]
public async Task Load_Test_Homepage()
{
    // Performance testing
}

public class LoadTestParallelLimit : IParallelLimit
{
    public int Limit => 10;
}
```

**Key Points:**
- `[DependsOn]` for explicit test dependencies
- Built-in retry mechanism
- `[Repeat]` for performance testing
- Granular parallel execution control
- Display names for better test reporting

### NUnit
```csharp
// Test ordering (attribute-based, limited)
[Test, Order(1)]
public void FirstTest()
{
    // Runs first
}

[Test, Order(2)]
public void SecondTest()
{
    // Runs second
}

// Retry on failure
[Test, Retry(3)]
public void UnstableTest()
{
    // Retries up to 3 times on failure
}

// Parallelization
[Parallelizable(ParallelScope.Self)]
public class ParallelTests
{
    [Test]
    public void Test1() { }
    
    [Test]
    public void Test2() { }
}

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable]
public class IsolatedParallelTests
{
    // New instance per test for isolation
}
```

**Key Points:**
- `[Order]` for basic test ordering
- No built-in test dependencies
- `[Retry]` for unstable tests
- `[Parallelizable]` with scopes (Self, Children, Fixtures, All)
- `[FixtureLifeCycle]` for instance management

---

## 7. Test Categories and Filtering

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
```

**Key Points:**
- `[Property]` attribute for metadata
- Support for custom conditional attributes
- Extensible filtering system

### NUnit
```csharp
[Test]
[Category("Integration")]
[Category("Slow")]
public void IntegrationTest()
{
    // Test implementation
}

// Platform-specific tests
[Test]
[Platform("Win")]
public void WindowsOnlyTest()
{
    // Only runs on Windows
}

// Custom properties
[Test]
[Property("Priority", "High")]
[Property("Feature", "Login")]
public void LoginTest()
{
    // Test with custom properties
}

// Author attribution
[Test]
[Author("John Doe")]
public void MyTest()
{
    // Track test ownership
}
```

**Key Points:**
- `[Category]` is inheritable
- `[Platform]` for OS-specific tests
- `[Property]` for custom metadata
- `[Author]` for tracking ownership
- Filters available in console runner

---

## 8. Ignoring Tests

### TUnit
```csharp
// Custom ignore logic via attributes
[Test]
[RunOn(OS.Windows)]  // Only runs on Windows
public async Task WindowsSpecificTest()
{
    // Test implementation
}
```

**Key Points:**
- Conditional execution via attributes
- Custom ignore logic extensible

### NUnit
```csharp
[Test]
[Ignore("Known bug - issue #123")]
public void IgnoredTest()
{
    // This test is skipped
}

// Ignore with until date
[Test]
[Ignore("Temporarily disabled", Until = "2024-12-31")]
public void TemporarilyIgnoredTest()
{
    // Ignored until specified date
}

// Explicit test (only runs when explicitly requested)
[Test, Explicit]
public void ManualTest()
{
    // Must be explicitly run
}

// Platform-based skipping
[Test]
[Platform(Exclude = "Linux")]
public void NotOnLinux()
{
    // Doesn't run on Linux
}
```

**Key Points:**
- Mandatory reason parameter for `[Ignore]`
- `Until` parameter for temporary ignores
- `[Explicit]` for manual-only tests
- Platform-based inclusion/exclusion

---

## 9. Timeout Handling

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

### NUnit
```csharp
[Test]
[Timeout(5000)]  // 5 seconds in milliseconds
public void TestWithTimeout()
{
    SomeLongRunningOperation();
}

// Async test with timeout
[Test]
[Timeout(5000)]
public async Task AsyncTestWithTimeout()
{
    await SomeLongRunningOperation();
}
```

**Key Points:**
- Timeout in milliseconds
- Works with both sync and async tests
- Fixed issues with TestCaseSource in recent versions
- Can work without running test on its own thread (v3.6.1+)

---

## 10. Special Execution Contexts

### TUnit
```csharp
[Test]
[STAThreadExecutor]
[RunOn(OS.Windows)]
[Repeat(100)]
public async Task STA_Test()
{
    // Runs on STA thread (Windows only)
    await Task.Delay(10);
}
```

**Key Points:**
- `[STAThreadExecutor]` for STA thread execution
- OS-specific execution control
- Platform compatibility attributes

### NUnit
```csharp
[Test]
[Apartment(ApartmentState.STA)]
public void STATest()
{
    // Runs on STA thread
}

[Test]
[RequiresThread]
public void RequiresOwnThread()
{
    // Runs on its own thread
}

// Only available on .NET Framework
[Test]
[Apartment(ApartmentState.MTA)]
public void MTATest()
{
    // Runs on MTA thread
}
```

**Key Points:**
- `[Apartment]` for thread apartment state
- `[RequiresThread]` for dedicated thread execution
- Limited on .NET Standard 2.0
- Windows Forms/WPF message pump detection

---

## 11. Generic Tests

### TUnit
```csharp
[Test]
[GenerateGenericTest(typeof(int))]
[GenerateGenericTest(typeof(string))]
public void TestMethod<T>()
{
    // Test runs for both int and string
}
```

**Key Points:**
- Source generator creates concrete test instances
- Type-safe generic test execution
- Compile-time generation

### NUnit
```csharp
[TestFixture(typeof(int))]
[TestFixture(typeof(string))]
public class GenericTests<T>
{
    [Test]
    public void GenericTest()
    {
        // Test runs for both int and string
    }
}

// Or with TestCaseGeneric
[TestCaseGeneric(typeof(int))]
[TestCaseGeneric(typeof(string))]
public void GenericMethodTest<T>()
{
    // Test runs for both types
}
```

**Key Points:**
- `[TestFixture(Type)]` for generic fixtures
- `[TestCaseGeneric]` for generic methods (if available)
- Reflection-based instantiation

---

## 12. Test Context and Output

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
- Context can be injected into tests
- Output captured automatically
- Metadata available via context

### NUnit
```csharp
[Test]
public void TestWithContext()
{
    TestContext.WriteLine("Test output");
    TestContext.Progress.WriteLine("Progress info");
    
    var testName = TestContext.CurrentContext.Test.Name;
    var workDir = TestContext.CurrentContext.WorkDirectory;
    
    // Attach files to test results
    TestContext.AddTestAttachment("path/to/file.txt");
}
```

**Key Points:**
- Static `TestContext` for accessing current context
- `TestContext.Out` for output
- `TestContext.Progress` for real-time progress
- File attachment support
- Access to test metadata, work directory, etc.
- Context flows through async methods

---

## Summary: Which Should You Choose?

### Choose TUnit if you:
- Are building new projects on modern .NET
- Want async-first testing
- Need advanced orchestration (dependencies, retries)
- Prefer source generator performance benefits
- Want modern fluent assertion syntax
- Need fine-grained parallel execution control

### Choose NUnit if you:
- Have existing NUnit test suites
- Need mature, battle-tested framework
- Require broad platform support (including .NET Framework 2.0+)
- Prefer established tooling and IDE integration
- Want wide community support and documentation
- Need both classic and constraint assertion models

### Migration Path from NUnit to TUnit

```csharp
// NUnit
[TestCase(1, 2, 3)]
public void AdditionTest(int a, int b, int expected)
{
    Assert.AreEqual(expected, a + b);
}

// TUnit
[Test]
[Arguments(1, 2, 3)]
public async Task AdditionTest(int a, int b, int expected)
{
    await Assert.That(a + b).IsEqualTo(expected);
}
```

**Key Migration Steps:**
1. Change `[TestCase]` to `[Arguments]` (on `[Test]` methods)
2. Make tests async (return `Task`)
3. Use `await` with assertions
4. Replace `[OneTimeSetUp]`/`[OneTimeTearDown]` with `[Before(Class)]`/`[After(Class)]`
5. Replace `[SetUp]`/`[TearDown]` with `[Before(Test)]`/`[After(Test)]`
6. Update assertion syntax to TUnit's fluent async style

---

## Performance Considerations

### TUnit
- **Source Generator Based**: Generates optimized code at compile-time
- **Async Native**: Designed for modern async workflows
- **Parallel by Default**: Better parallel execution control
- **Lower Overhead**: Less reflection, more direct execution

### NUnit
- **Reflection Based**: Runtime discovery and execution
- **Mature Optimizer**: Years of performance tuning
- **Flexible Parallelization**: Multiple parallel scopes
- **Proven Scale**: Used in large enterprise projects

---

## Tooling and IDE Support

### TUnit
- Modern .NET CLI integration
- Growing IDE support
- Source generator integration in Visual Studio/Rider

### NUnit
- Excellent Visual Studio integration (Test Explorer)
- ReSharper support
- Rider native support
- VS Code extensions
- Wide CI/CD platform support
- Rich test adapters

---

## Conclusion

Both frameworks are excellent choices for .NET unit testing. TUnit represents the modern, async-first approach with cutting-edge features, while NUnit provides battle-tested stability with comprehensive features and broad compatibility. Your choice should depend on your project requirements, team expertise, and whether you're starting fresh or maintaining existing tests.
