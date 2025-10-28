# Test Dependencies and Ordering

Control test execution order and dependencies using the `[DependsOn]` attribute.

## Basic Test Dependencies

Make one test depend on another:

```csharp
using TUnit.Core;

public class DependentTests
{
    [Test]
    public async Task First_test_runs_first()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(First_test_runs_first))]
    public async Task Second_test_depends_on_first()
    {
        // This runs after First_test_runs_first
        await Assert.That(true).IsTrue();
    }
}
```

## Multiple Dependencies

A test can depend on multiple other tests:

```csharp
public class MultipleDependenciesTests
{
    [Test]
    public async Task Setup_database()
    {
        // Initialize database
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Seed_test_data()
    {
        // Add test data
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Setup_database))]
    [DependsOn(nameof(Seed_test_data))]
    public async Task Query_test_data()
    {
        // Runs after both Setup_database and Seed_test_data complete
        await Assert.That(true).IsTrue();
    }
}
```

## Cross-Class Dependencies

Tests can depend on tests in other classes:

```csharp
public class SetupTests
{
    [Test]
    public async Task Initialize_system()
    {
        await Assert.That(true).IsTrue();
    }
}

public class WorkflowTests
{
    [Test]
    [DependsOn(typeof(SetupTests), nameof(SetupTests.Initialize_system))]
    public async Task Run_workflow()
    {
        // Runs after SetupTests.Initialize_system
        await Assert.That(true).IsTrue();
    }
}
```

## Sequential Test Execution

Ensure tests run in a specific order:

```csharp
public class SequentialTests
{
    [Test]
    public async Task Step_1_create_user()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Step_1_create_user))]
    public async Task Step_2_verify_user()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Step_2_verify_user))]
    public async Task Step_3_update_user()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Step_3_update_user))]
    public async Task Step_4_delete_user()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## Database Setup Example

```csharp
public class DatabaseWorkflowTests
{
    [Test]
    public async Task Create_database_schema()
    {
        // Create tables
        await CreateTablesAsync();
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Create_database_schema))]
    public async Task Seed_reference_data()
    {
        // Add reference data
        await SeedDataAsync();
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Seed_reference_data))]
    public async Task Insert_test_records()
    {
        // Add test records
        await InsertTestDataAsync();
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Insert_test_records))]
    public async Task Verify_data_integrity()
    {
        // Verify all data is correct
        var count = await GetRecordCountAsync();
        await Assert.That(count).IsGreaterThan(0);
    }

    private async Task CreateTablesAsync() { await Task.CompletedTask; }
    private async Task SeedDataAsync() { await Task.CompletedTask; }
    private async Task InsertTestDataAsync() { await Task.CompletedTask; }
    private async Task<int> GetRecordCountAsync() { await Task.CompletedTask; return 10; }
}
```

## Integration Test Pipeline

```csharp
public class IntegrationPipeline
{
    [Test]
    public async Task Deploy_application()
    {
        // Deploy to test environment
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Deploy_application))]
    public async Task Verify_deployment()
    {
        // Check deployment succeeded
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Verify_deployment))]
    public async Task Run_smoke_tests()
    {
        // Run basic health checks
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Run_smoke_tests))]
    public async Task Run_integration_tests()
    {
        // Run full integration suite
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Run_integration_tests))]
    public async Task Cleanup_environment()
    {
        // Clean up test environment
        await Assert.That(true).IsTrue();
    }
}
```

## Conditional Execution

Skip dependent tests if prerequisite fails:

```csharp
public class ConditionalTests
{
    private static bool _setupSucceeded = false;

    [Test]
    public async Task Setup_environment()
    {
        try
        {
            await PerformSetupAsync();
            _setupSucceeded = true;
            await Assert.That(true).IsTrue();
        }
        catch
        {
            _setupSucceeded = false;
            throw;
        }
    }

    [Test]
    [DependsOn(nameof(Setup_environment))]
    public async Task Test_requiring_setup()
    {
        // Only runs if Setup_environment succeeded
        await Assert.That(_setupSucceeded).IsTrue();
    }

    private async Task PerformSetupAsync() { await Task.CompletedTask; }
}
```

## Parallel Groups with Dependencies

```csharp
public class ParallelGroupTests
{
    // Group 1: Can run in parallel
    [Test]
    public async Task Group1_Test_A()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Group1_Test_B()
    {
        await Assert.That(true).IsTrue();
    }

    // Group 2: Depends on Group 1, but tests within can run in parallel
    [Test]
    [DependsOn(nameof(Group1_Test_A))]
    [DependsOn(nameof(Group1_Test_B))]
    public async Task Group2_Test_A()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Group1_Test_A))]
    [DependsOn(nameof(Group1_Test_B))]
    public async Task Group2_Test_B()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## Resource Initialization Chain

```csharp
public class ResourceInitializationTests
{
    [Test]
    public async Task Initialize_configuration()
    {
        // Load config files
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Initialize_configuration))]
    public async Task Connect_to_database()
    {
        // Requires configuration
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Initialize_configuration))]
    public async Task Connect_to_cache()
    {
        // Requires configuration
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Connect_to_database))]
    [DependsOn(nameof(Connect_to_cache))]
    public async Task Initialize_application()
    {
        // Requires both database and cache
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Initialize_application))]
    public async Task Run_application_tests()
    {
        // Application must be fully initialized
        await Assert.That(true).IsTrue();
    }
}
```

## E2E User Journey

```csharp
public class UserJourneyTests
{
    private static string? _userId;
    private static string? _sessionToken;

    [Test]
    public async Task User_registers()
    {
        _userId = await RegisterUserAsync();
        await Assert.That(_userId).IsNotNull();
    }

    [Test]
    [DependsOn(nameof(User_registers))]
    public async Task User_logs_in()
    {
        _sessionToken = await LoginUserAsync(_userId!);
        await Assert.That(_sessionToken).IsNotNull();
    }

    [Test]
    [DependsOn(nameof(User_logs_in))]
    public async Task User_views_profile()
    {
        var profile = await GetProfileAsync(_sessionToken!);
        await Assert.That(profile).IsNotNull();
    }

    [Test]
    [DependsOn(nameof(User_views_profile))]
    public async Task User_updates_profile()
    {
        var updated = await UpdateProfileAsync(_sessionToken!, "New Name");
        await Assert.That(updated).IsTrue();
    }

    [Test]
    [DependsOn(nameof(User_updates_profile))]
    public async Task User_logs_out()
    {
        await LogoutUserAsync(_sessionToken!);
        await Assert.That(true).IsTrue();
    }

    private async Task<string> RegisterUserAsync() { await Task.CompletedTask; return "user123"; }
    private async Task<string> LoginUserAsync(string userId) { await Task.CompletedTask; return "token456"; }
    private async Task<object> GetProfileAsync(string token) { await Task.CompletedTask; return new { }; }
    private async Task<bool> UpdateProfileAsync(string token, string name) { await Task.CompletedTask; return true; }
    private async Task LogoutUserAsync(string token) { await Task.CompletedTask; }
}
```

## Data Migration Pipeline

```csharp
public class DataMigrationTests
{
    [Test]
    public async Task Backup_existing_data()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Backup_existing_data))]
    public async Task Transform_data_schema()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Transform_data_schema))]
    public async Task Migrate_data()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Migrate_data))]
    public async Task Verify_data_integrity()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Verify_data_integrity))]
    public async Task Update_indexes()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Update_indexes))]
    public async Task Verify_performance()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## Cleanup Dependencies

```csharp
public class CleanupChainTests
{
    [Test]
    public async Task Create_test_resources()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Create_test_resources))]
    public async Task Use_test_resources()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Use_test_resources))]
    public async Task Cleanup_application_data()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Cleanup_application_data))]
    public async Task Cleanup_database()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [DependsOn(nameof(Cleanup_database))]
    public async Task Cleanup_file_system()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## When to Use Dependencies

### ✅ Good Use Cases

1. **Sequential Workflows**: User journeys, deployment pipelines
2. **Resource Initialization**: Database setup, environment configuration
3. **Expensive Setup**: Share expensive initialization across tests
4. **Integration Tests**: Multi-step integration scenarios
5. **Cleanup Chains**: Ordered cleanup operations

### ❌ Avoid Dependencies For

1. **Unit Tests**: Should be independent
2. **Asserting State**: Don't depend on test data from previous tests
3. **Parallel Execution**: Dependencies reduce parallelism
4. **Simple Tests**: Use setup/teardown instead

## Best Practices

### 1. Minimize Dependencies

```csharp
// ✅ Good - Independent tests with shared setup
public class IndependentTests
{
    [Before(Test)]
    public async Task Setup()
    {
        await InitializeAsync();
    }

    [Test]
    public async Task Test_A() { }

    [Test]
    public async Task Test_B() { }
}

// ❌ Wrong - Unnecessary dependencies
public class DependentTests
{
    [Test]
    public async Task Test_A() { }

    [Test]
    [DependsOn(nameof(Test_A))]
    public async Task Test_B() { } // No real dependency
}
```

### 2. Use for Workflows, Not State

```csharp
// ✅ Good - Sequential workflow steps
[Test]
[DependsOn(nameof(Deploy_application))]
public async Task Verify_deployment() { }

// ❌ Wrong - Relying on test state
[Test]
[DependsOn(nameof(Create_user))]
public async Task Update_user()
{
    // Bad: Assumes user from Create_user still exists
}
```

### 3. Clear Dependency Chains

```csharp
// ✅ Good - Clear, linear chain
A -> B -> C -> D

// ❌ Wrong - Complex dependency graph
A -> B -> D
A -> C -> D
B -> E
C -> E -> F
```

### 4. Document Why Dependencies Exist

```csharp
/// <summary>
/// Verifies deployment health. Depends on Deploy_application
/// because it requires the application to be deployed first.
/// </summary>
[Test]
[DependsOn(nameof(Deploy_application))]
public async Task Verify_deployment_health() { }
```

## See Also

- [Test Organization](test_organization.md) - Organizing tests
- [Setup and Teardown](setup_teardown.md) - Alternative to dependencies
- [Shared Context](shared_context.md) - Sharing resources
