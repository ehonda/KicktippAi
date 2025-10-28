# Setup and Teardown

Setup and teardown methods allow you to initialize resources before tests and clean them up afterward, following the test lifecycle.

## Test Lifecycle

TUnit provides several lifecycle hooks:

- `[Before(Test)]` - Runs before each test method
- `[After(Test)]` - Runs after each test method
- `[Before(Class)]` - Runs once before all tests in the class
- `[After(Class)]` - Runs once after all tests in the class

## Per-Test Setup and Teardown

Use when each test needs fresh state:

```csharp
using TUnit.Core;

public class DatabaseTests
{
    private DatabaseConnection? _connection;

    [Before(Test)]
    public async Task Setup()
    {
        // Runs before each test
        _connection = new DatabaseConnection();
        await _connection.OpenAsync();
    }

    [After(Test)]
    public async Task Teardown()
    {
        // Runs after each test
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }

    [Test]
    public async Task Querying_users_returns_results()
    {
        var users = await _connection!.QueryAsync<User>("SELECT * FROM Users");
        await Assert.That(users).IsNotEmpty();
    }

    [Test]
    public async Task Inserting_user_increases_count()
    {
        var initialCount = await _connection!.CountAsync<User>();
        
        await _connection.ExecuteAsync("INSERT INTO Users (Name) VALUES ('Test')");
        
        var newCount = await _connection.CountAsync<User>();
        await Assert.That(newCount).IsEqualTo(initialCount + 1);
    }
}
```

## Class-Level Setup and Teardown

Use for expensive operations that can be shared across all tests:

```csharp
public class ApiIntegrationTests
{
    private static HttpClient? _httpClient;
    private static TestServer? _server;

    [Before(Class)]
    public static async Task SetupTestServer()
    {
        // Runs once before all tests in the class
        _server = new TestServer();
        await _server.StartAsync();
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
    }

    [After(Class)]
    public static async Task TeardownTestServer()
    {
        // Runs once after all tests in the class
        _httpClient?.Dispose();
        
        if (_server != null)
        {
            await _server.StopAsync();
            await _server.DisposeAsync();
        }
    }

    [Test]
    public async Task Getting_users_endpoint_returns_ok()
    {
        var response = await _httpClient!.GetAsync("/api/users");
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    public async Task Creating_user_returns_created_status()
    {
        var content = new StringContent("{\"name\":\"Test User\"}");
        var response = await _httpClient!.PostAsync("/api/users", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }
}
```

## Combining Both Levels

```csharp
public class UserServiceTests
{
    private static Database? _database;
    private UserService? _service;

    [Before(Class)]
    public static async Task SetupDatabase()
    {
        // Expensive: Start database container
        _database = new Database();
        await _database.StartAsync();
        await _database.MigrateAsync();
    }

    [After(Class)]
    public static async Task TeardownDatabase()
    {
        // Clean up database container
        if (_database != null)
        {
            await _database.StopAsync();
        }
    }

    [Before(Test)]
    public async Task SetupTest()
    {
        // Per-test: Create fresh service instance
        _service = new UserService(_database!.ConnectionString);
        
        // Clean database state for each test
        await _database!.TruncateAllTablesAsync();
    }

    [After(Test)]
    public async Task TeardownTest()
    {
        // Per-test cleanup
        if (_service != null)
        {
            await _service.DisposeAsync();
        }
    }

    [Test]
    public async Task Creating_user_stores_in_database()
    {
        var user = new User { Name = "John", Email = "john@example.com" };
        
        await _service!.CreateAsync(user);
        
        var retrieved = await _service.GetByIdAsync(user.Id);
        await Assert.That(retrieved).IsNotNull();
    }
}
```

## Using Context Objects

Lifecycle methods can receive context objects:

```csharp
public class ContextTests
{
    [Before(Class)]
    public static async Task ClassSetup(ClassHookContext context)
    {
        // Access test class information
        context.TestContext.Output.WriteLine($"Setting up {context.TestDetails.TestName}");
        await Task.CompletedTask;
    }

    [Before(Test)]
    public async Task TestSetup(TestContext context)
    {
        // Access test-specific information
        context.Output.WriteLine($"Running test: {context.TestDetails.TestName}");
        await Task.CompletedTask;
    }

    [Test]
    public async Task My_test()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## Property Injection with Async Initialization

For complex setup requiring async operations:

```csharp
public class WebApplicationTests
{
    [ClassDataSource<WebApplicationFactory>(Shared = SharedType.PerTestSession)]
    public required WebApplicationFactory Factory { get; init; }

    [Test]
    public async Task Application_responds_to_ping()
    {
        // Factory is automatically initialized before this runs
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/ping");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}

public class WebApplicationFactory : IAsyncInitializable, IAsyncDisposable
{
    private TestServer? _server;

    public async Task InitializeAsync()
    {
        _server = new TestServer();
        await _server.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
        }
    }

    public HttpClient CreateClient()
    {
        return _server!.CreateClient();
    }
}
```

## Sharing Expensive Resources

### Per-Test Session Sharing

```csharp
public class IntegrationTests
{
    [ClassDataSource<DatabaseContainer>(Shared = SharedType.PerTestSession)]
    public required DatabaseContainer Database { get; init; }

    [Test]
    public async Task Test1()
    {
        // Database container is shared across ALL tests in the session
        var result = await Database.Connection.QueryAsync("SELECT 1");
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Test2()
    {
        // Same container instance as Test1
        var result = await Database.Connection.QueryAsync("SELECT 2");
        await Assert.That(result).IsNotNull();
    }
}

public class DatabaseContainer : IAsyncInitializable, IAsyncDisposable
{
    public SqlConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Expensive: Start Docker container
        await StartContainerAsync();
        Connection = new SqlConnection(ConnectionString);
        await Connection.OpenAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.CloseAsync();
        await StopContainerAsync();
    }

    private async Task StartContainerAsync() { /* ... */ }
    private async Task StopContainerAsync() { /* ... */ }
    private string ConnectionString => "...";
}
```

## Error Handling in Lifecycle Methods

### Handling Setup Failures

```csharp
public class ResilientTests
{
    [Before(Test)]
    public async Task Setup()
    {
        try
        {
            await InitializeResourceAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Setup failed: {ex.Message}");
            
            // Optionally skip the test
            throw new SkipException($"Setup failed: {ex.Message}");
        }
    }

    [After(Test)]
    public async Task Teardown()
    {
        try
        {
            await CleanupResourceAsync();
        }
        catch (Exception ex)
        {
            // Don't let teardown failures hide test results
            Console.WriteLine($"Teardown warning: {ex.Message}");
        }
    }

    private async Task InitializeResourceAsync() { await Task.CompletedTask; }
    private async Task CleanupResourceAsync() { await Task.CompletedTask; }
}
```

## Common Patterns

### Database Test Pattern

```csharp
public class RepositoryTests
{
    private static DbContext? _context;
    private UserRepository? _repository;

    [Before(Class)]
    public static async Task SetupDatabase()
    {
        _context = new DbContext(useInMemoryDatabase: true);
        await _context.Database.EnsureCreatedAsync();
    }

    [After(Class)]
    public static async Task TeardownDatabase()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
    }

    [Before(Test)]
    public async Task SetupTest()
    {
        _repository = new UserRepository(_context!);
        await _context!.Database.BeginTransactionAsync();
    }

    [After(Test)]
    public async Task TeardownTest()
    {
        // Rollback transaction to isolate tests
        await _context!.Database.RollbackTransactionAsync();
    }

    [Test]
    public async Task Adding_user_saves_to_database()
    {
        var user = new User { Name = "Test" };
        
        await _repository!.AddAsync(user);
        
        var retrieved = await _repository.GetByIdAsync(user.Id);
        await Assert.That(retrieved).IsNotNull();
    }
}
```

### File System Test Pattern

```csharp
public class FileServiceTests
{
    private string _testDirectory = null!;
    private FileService _fileService = null!;

    [Before(Test)]
    public async Task Setup()
    {
        // Create unique test directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        _fileService = new FileService(_testDirectory);
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Teardown()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        await Task.CompletedTask;
    }

    [Test]
    public async Task Writing_file_creates_file_on_disk()
    {
        var fileName = "test.txt";
        var content = "Hello, World!";
        
        await _fileService.WriteAsync(fileName, content);
        
        var exists = File.Exists(Path.Combine(_testDirectory, fileName));
        await Assert.That(exists).IsTrue();
    }
}
```

### HTTP Client Test Pattern

```csharp
public class HttpClientTests
{
    private HttpClient? _httpClient;
    private MockHttpMessageHandler? _mockHandler;

    [Before(Test)]
    public async Task Setup()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Teardown()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Getting_data_from_api_succeeds()
    {
        _mockHandler!.SetupResponse(HttpStatusCode.OK, "{ \"data\": \"test\" }");
        
        var response = await _httpClient!.GetAsync("/endpoint");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

## Best Practices

### 1. Keep Setup and Teardown Simple

```csharp
// ✅ Good - Simple and focused
[Before(Test)]
public async Task Setup()
{
    _service = new MyService();
    await _service.InitializeAsync();
}

// ❌ Avoid - Too much logic
[Before(Test)]
public async Task ComplexSetup()
{
    _service = new MyService();
    await _service.InitializeAsync();
    await _service.LoadDataAsync();
    await _service.ConfigureAsync();
    if (_service.NeedsValidation)
    {
        await _service.ValidateAsync();
    }
    // Too much happening here
}
```

### 2. Use Class-Level Setup for Expensive Operations

```csharp
// ✅ Good - Share expensive setup
[Before(Class)]
public static async Task SetupOnce()
{
    await StartDatabaseContainerAsync();  // Expensive
}

// ❌ Avoid - Repeat expensive setup
[Before(Test)]
public async Task SetupEveryTest()
{
    await StartDatabaseContainerAsync();  // Too slow per test
}
```

### 3. Always Clean Up Resources

```csharp
// ✅ Good - Proper cleanup
[After(Test)]
public async Task Teardown()
{
    if (_connection != null)
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}

// ❌ Avoid - Resource leak
[After(Test)]
public async Task BadTeardown()
{
    // Forgot to close connection
    await Task.CompletedTask;
}
```

### 4. Handle Teardown Errors Gracefully

```csharp
// ✅ Good - Don't throw from teardown
[After(Test)]
public async Task SafeTeardown()
{
    try
    {
        await CleanupAsync();
    }
    catch (Exception ex)
    {
        // Log but don't fail the test
        Console.WriteLine($"Cleanup warning: {ex.Message}");
    }
}
```

## See Also

- [Basic Tests](basic_tests.md) - Test structure fundamentals
- [Shared Context](shared_context.md) - Advanced resource sharing
- [Async Testing](async_testing.md) - Async patterns
