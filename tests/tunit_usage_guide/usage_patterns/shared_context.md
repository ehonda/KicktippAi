# Shared Context and Resources

Managing shared resources and test context efficiently across multiple tests.

## ClassDataSource with SharedType

Share expensive resources across tests using `ClassDataSource<T>` with `SharedType.Globally`.

### Basic Shared Context

```csharp
using TUnit.Core;

public class DatabaseFixture
{
    public string ConnectionString { get; set; } = null!;
    public DatabaseContext Context { get; set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionString = "Server=localhost;Database=TestDb";
        Context = new DatabaseContext(ConnectionString);
        await Context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}

public class SharedDatabaseTests
{
    [Test]
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public async Task First_test_uses_shared_database(DatabaseFixture fixture)
    {
        var repository = new UserRepository(fixture.Context);
        
        var user = await repository.CreateAsync(new User { Name = "John" });
        
        await Assert.That(user.Id).IsGreaterThan(0);
    }

    [Test]
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public async Task Second_test_uses_same_database(DatabaseFixture fixture)
    {
        var repository = new ProductRepository(fixture.Context);
        
        var product = await repository.CreateAsync(new Product { Name = "Widget" });
        
        await Assert.That(product.Id).IsGreaterThan(0);
    }
}
```

### Sharing Across Test Classes

```csharp
// Shared fixture
public class ApiClientFixture : IAsyncInitializable, IAsyncDisposable
{
    public HttpClient Client { get; private set; } = null!;
    public string BaseUrl { get; private set; } = "https://api.test.com";

    public async Task InitializeAsync()
    {
        Client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Task.CompletedTask;
    }
}

// First test class
public class UserApiTests
{
    [Test]
    [ClassDataSource<ApiClientFixture>(SharedType.Globally)]
    public async Task Getting_user_returns_user_data(ApiClientFixture fixture)
    {
        var response = await fixture.Client.GetAsync("/users/1");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}

// Second test class - uses the same fixture instance
public class ProductApiTests
{
    [Test]
    [ClassDataSource<ApiClientFixture>(SharedType.Globally)]
    public async Task Getting_products_returns_list(ApiClientFixture fixture)
    {
        var response = await fixture.Client.GetAsync("/products");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

## Shared Type Options

### PerTestSession (Default)

One instance per test run:

```csharp
public class ExpensiveSetup
{
    public ExpensiveSetup()
    {
        // This runs once for the entire test session
        Console.WriteLine("Expensive setup");
    }
}

public class Tests
{
    [Test]
    [ClassDataSource<ExpensiveSetup>(SharedType.PerTestSession)]
    public async Task First_test(ExpensiveSetup setup) { }

    [Test]
    [ClassDataSource<ExpensiveSetup>(SharedType.PerTestSession)]
    public async Task Second_test(ExpensiveSetup setup) { }
    // Both tests share the same instance
}
```

### Globally

One instance shared across all test sessions:

```csharp
public class GlobalResource
{
    public static int InstanceCount = 0;
    
    public GlobalResource()
    {
        InstanceCount++;
    }
}

public class Tests1
{
    [Test]
    [ClassDataSource<GlobalResource>(SharedType.Globally)]
    public async Task Test_in_class_1(GlobalResource resource) { }
}

public class Tests2
{
    [Test]
    [ClassDataSource<GlobalResource>(SharedType.Globally)]
    public async Task Test_in_class_2(GlobalResource resource) { }
    // Uses the exact same instance as Tests1
}
```

### None

New instance per test:

```csharp
public class IsolatedContext
{
    public List<string> Items { get; } = new();
}

public class IsolationTests
{
    [Test]
    [ClassDataSource<IsolatedContext>(SharedType.None)]
    public async Task First_test_has_own_context(IsolatedContext context)
    {
        context.Items.Add("Item1");
        await Assert.That(context.Items.Count).IsEqualTo(1);
    }

    [Test]
    [ClassDataSource<IsolatedContext>(SharedType.None)]
    public async Task Second_test_has_own_context(IsolatedContext context)
    {
        // Fresh context, not affected by first test
        await Assert.That(context.Items.Count).IsEqualTo(0);
    }
}
```

## Property Injection with IAsyncInitializable

Inject shared resources as properties:

```csharp
public class DatabaseTests
{
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public required DatabaseFixture Database { get; init; }

    [Test]
    public async Task Can_insert_user()
    {
        var repository = new UserRepository(Database.Context);
        
        var user = await repository.CreateAsync(new User { Name = "Alice" });
        
        await Assert.That(user.Id).IsGreaterThan(0);
    }

    [Test]
    public async Task Can_query_users()
    {
        var repository = new UserRepository(Database.Context);
        
        var users = await repository.GetAllAsync();
        
        await Assert.That(users).IsNotNull();
    }
}
```

## Complex Shared Scenarios

### Multiple Shared Resources

```csharp
public class DatabaseFixture : IAsyncInitializable, IAsyncDisposable
{
    public DatabaseContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Context = new DatabaseContext("TestDb");
        await Context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}

public class ApiFixture : IAsyncInitializable, IAsyncDisposable
{
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = new HttpClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Task.CompletedTask;
    }
}

public class IntegrationTests
{
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public required DatabaseFixture Database { get; init; }

    [ClassDataSource<ApiFixture>(SharedType.Globally)]
    public required ApiFixture Api { get; init; }

    [Test]
    public async Task Can_save_and_retrieve_via_api()
    {
        // Use both shared resources
        var repository = new UserRepository(Database.Context);
        var user = await repository.CreateAsync(new User { Name = "John" });

        var response = await Api.Client.GetAsync($"/users/{user.Id}");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

### Hierarchical Shared Context

```csharp
// Global configuration
public class AppConfig
{
    public string Environment { get; } = "Test";
    public string ConnectionString { get; } = "Server=localhost";
}

// Database that depends on config
public class DatabaseFixture : IAsyncInitializable, IAsyncDisposable
{
    private readonly AppConfig _config;
    public DatabaseContext Context { get; private set; } = null!;

    public DatabaseFixture(AppConfig config)
    {
        _config = config;
    }

    public async Task InitializeAsync()
    {
        Context = new DatabaseContext(_config.ConnectionString);
        await Context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
    }
}

public class Tests
{
    [ClassDataSource<AppConfig>(SharedType.Globally)]
    public required AppConfig Config { get; init; }

    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public required DatabaseFixture Database { get; init; }

    [Test]
    public async Task Uses_hierarchical_context()
    {
        await Assert.That(Config.Environment).IsEqualTo("Test");
        await Assert.That(Database.Context).IsNotNull();
    }
}
```

## Test Data Builders

Create reusable test data:

```csharp
public class TestDataBuilder
{
    private readonly DatabaseContext _context;

    public TestDataBuilder(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<User> CreateUserAsync(string name = "TestUser")
    {
        var user = new User { Name = name, Email = $"{name}@test.com" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<Product> CreateProductAsync(string name = "TestProduct", decimal price = 10.0m)
    {
        var product = new Product { Name = name, Price = price };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<Order> CreateOrderAsync(User user, params Product[] products)
    {
        var order = new Order { UserId = user.Id, Items = products.ToList() };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }
}

public class OrderTests
{
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public required DatabaseFixture Database { get; init; }

    private TestDataBuilder Builder => new(Database.Context);

    [Test]
    public async Task Order_calculates_total_correctly()
    {
        // Arrange
        var user = await Builder.CreateUserAsync("John");
        var product1 = await Builder.CreateProductAsync("Widget", 10.0m);
        var product2 = await Builder.CreateProductAsync("Gadget", 20.0m);
        var order = await Builder.CreateOrderAsync(user, product1, product2);

        // Act
        var total = order.CalculateTotal();

        // Assert
        await Assert.That(total).IsEqualTo(30.0m);
    }
}
```

## Caching Expensive Operations

```csharp
public class CachedDataFixture : IAsyncInitializable
{
    public List<Country> Countries { get; private set; } = null!;
    public Dictionary<string, string> TranslationCache { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Expensive: Load from external API
        Countries = await LoadCountriesAsync();
        
        // Expensive: Load translation files
        TranslationCache = await LoadTranslationsAsync();
    }

    private async Task<List<Country>> LoadCountriesAsync()
    {
        // Simulating expensive API call
        await Task.Delay(1000);
        return new List<Country>
        {
            new() { Code = "US", Name = "United States" },
            new() { Code = "GB", Name = "United Kingdom" }
        };
    }

    private async Task<Dictionary<string, string>> LoadTranslationsAsync()
    {
        // Simulating expensive file I/O
        await Task.Delay(500);
        return new Dictionary<string, string>
        {
            ["hello"] = "Hello",
            ["goodbye"] = "Goodbye"
        };
    }
}

public class LocalizationTests
{
    [ClassDataSource<CachedDataFixture>(SharedType.Globally)]
    public required CachedDataFixture Data { get; init; }

    [Test]
    public async Task Can_lookup_country_by_code()
    {
        var country = Data.Countries.First(c => c.Code == "US");
        await Assert.That(country.Name).IsEqualTo("United States");
    }

    [Test]
    public async Task Can_translate_text()
    {
        var translation = Data.TranslationCache["hello"];
        await Assert.That(translation).IsEqualTo("Hello");
    }
}
```

## Mocked Services

Share mock configurations:

```csharp
public class MockedServicesFixture
{
    public Mock<IEmailService> EmailService { get; }
    public Mock<IPaymentGateway> PaymentGateway { get; }

    public MockedServicesFixture()
    {
        EmailService = new Mock<IEmailService>();
        EmailService.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        PaymentGateway = new Mock<IPaymentGateway>();
        PaymentGateway.Setup(x => x.ProcessAsync(It.IsAny<decimal>()))
            .ReturnsAsync(new PaymentResult { Success = true });
    }
}

public class CheckoutTests
{
    [ClassDataSource<MockedServicesFixture>(SharedType.PerTestSession)]
    public required MockedServicesFixture Mocks { get; init; }

    [Test]
    public async Task Successful_checkout_sends_confirmation_email()
    {
        var service = new CheckoutService(
            Mocks.EmailService.Object,
            Mocks.PaymentGateway.Object);

        await service.ProcessOrderAsync(new Order { Total = 100m });

        Mocks.EmailService.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }
}
```

## Test Containers

Using test containers for integration tests:

```csharp
public class ContainerFixture : IAsyncInitializable, IAsyncDisposable
{
    private PostgreSqlContainer? _container;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();
            
        await _container.StartAsync();
        
        ConnectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}

public class DatabaseIntegrationTests
{
    [ClassDataSource<ContainerFixture>(SharedType.Globally)]
    public required ContainerFixture Container { get; init; }

    [Test]
    public async Task Can_connect_to_test_database()
    {
        await using var connection = new NpgsqlConnection(Container.ConnectionString);
        await connection.OpenAsync();
        
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Open);
    }
}
```

## Cleanup Strategies

### Automatic Cleanup

```csharp
public class DatabaseFixture : IAsyncInitializable, IAsyncDisposable
{
    public DatabaseContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Context = new DatabaseContext("TestDb");
        await Context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Automatic cleanup when fixture is disposed
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}
```

### Manual Cleanup Between Tests

```csharp
public class DatabaseFixture
{
    public DatabaseContext Context { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Context = new DatabaseContext("TestDb");
        await Context.Database.EnsureCreatedAsync();
    }

    public async Task ResetAsync()
    {
        // Clear all data but keep schema
        await Context.Users.ExecuteDeleteAsync();
        await Context.Products.ExecuteDeleteAsync();
        await Context.Orders.ExecuteDeleteAsync();
    }
}

public class Tests
{
    [ClassDataSource<DatabaseFixture>(SharedType.Globally)]
    public required DatabaseFixture Database { get; init; }

    [After(Test)]
    public async Task Cleanup()
    {
        await Database.ResetAsync();
    }

    [Test]
    public async Task First_test()
    {
        // Database is clean
        var users = await Database.Context.Users.ToListAsync();
        await Assert.That(users).IsEmpty();
    }

    [Test]
    public async Task Second_test()
    {
        // Database is clean again (cleaned up after first test)
        var users = await Database.Context.Users.ToListAsync();
        await Assert.That(users).IsEmpty();
    }
}
```

## Best Practices

### 1. Use Shared Resources for Expensive Operations

```csharp
// ✅ Good - Share expensive setup
[ClassDataSource<DatabaseFixture>(SharedType.Globally)]
public required DatabaseFixture Database { get; init; }

// ❌ Wrong - Recreate for each test
[Before(Test)]
public async Task Setup()
{
    _database = new DatabaseContext();  // Expensive!
    await _database.Database.EnsureCreatedAsync();
}
```

### 2. Clean Up Properly

```csharp
// ✅ Good - Implements disposal
public class Fixture : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        // Cleanup resources
    }
}

// ❌ Wrong - No cleanup
public class Fixture
{
    // Resources leaked!
}
```

### 3. Use Appropriate Sharing Level

```csharp
// ✅ Good - Global for expensive, immutable resources
[ClassDataSource<CountryDataFixture>(SharedType.Globally)]

// ✅ Good - PerTestSession for mutable, isolated resources
[ClassDataSource<DatabaseFixture>(SharedType.PerTestSession)]

// ✅ Good - None for test-specific data
[ClassDataSource<TestCaseFixture>(SharedType.None)]
```

## See Also

- [Setup and Teardown](setup_teardown.md) - Test lifecycle
- [Basic Tests](basic_tests.md) - Test fundamentals
- [Parameterized Tests](parameterized_tests.md) - Data sources
