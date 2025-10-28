# Retries and Flaky Test Handling

Handle flaky tests and implement retry logic using the `[Retry]` attribute.

## Basic Retry

Retry a test up to a specified number of times:

```csharp
using TUnit.Core;

public class RetryTests
{
    [Test]
    [Retry(3)]
    public async Task Flaky_test_with_retry()
    {
        // Test will retry up to 3 times if it fails
        var result = await UnreliableOperation();
        await Assert.That(result).IsTrue();
    }

    private async Task<bool> UnreliableOperation()
    {
        await Task.Delay(10);
        return Random.Shared.Next(0, 2) == 1; // 50% success rate
    }
}
```

## External Service Tests

Retry tests that depend on external services:

```csharp
public class ExternalServiceTests
{
    [Test]
    [Retry(5)]
    public async Task Calling_external_api_succeeds()
    {
        using var client = new HttpClient();
        
        var response = await client.GetAsync("https://api.example.com/health");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    [Retry(3)]
    public async Task Database_connection_succeeds()
    {
        await using var connection = new SqlConnection("connection-string");
        
        await connection.OpenAsync();
        
        await Assert.That(connection.State).IsEqualTo(ConnectionState.Open);
    }
}
```

## Network Operations

Handle network flakiness:

```csharp
public class NetworkTests
{
    [Test]
    [Retry(3)]
    public async Task Downloading_file_completes()
    {
        using var client = new HttpClient();
        
        var data = await client.GetByteArrayAsync("https://example.com/large-file.zip");
        
        await Assert.That(data.Length).IsGreaterThan(0);
    }

    [Test]
    [Retry(5)]
    public async Task Uploading_data_succeeds()
    {
        using var client = new HttpClient();
        var content = new StringContent("test data");
        
        var response = await client.PostAsync("https://api.example.com/upload", content);
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

## Timing-Sensitive Tests

Retry tests with timing dependencies:

```csharp
public class TimingTests
{
    [Test]
    [Retry(3)]
    public async Task Operation_completes_within_timeout()
    {
        var startTime = DateTime.UtcNow;
        
        await PerformOperationAsync();
        
        var duration = DateTime.UtcNow - startTime;
        await Assert.That(duration.TotalSeconds).IsLessThan(5);
    }

    [Test]
    [Retry(5)]
    public async Task Polling_operation_succeeds()
    {
        var maxAttempts = 10;
        var attempt = 0;
        
        while (attempt < maxAttempts)
        {
            var status = await CheckStatusAsync();
            if (status == "completed")
            {
                await Assert.That(status).IsEqualTo("completed");
                return;
            }
            
            await Task.Delay(500);
            attempt++;
        }
        
        await Assert.That(false).IsTrue(); // Fail if not completed
    }

    private async Task PerformOperationAsync()
    {
        await Task.Delay(Random.Shared.Next(100, 2000));
    }

    private async Task<string> CheckStatusAsync()
    {
        await Task.Delay(100);
        return Random.Shared.Next(0, 3) == 2 ? "completed" : "pending";
    }
}
```

## UI/Browser Tests

Retry UI tests that may be flaky:

```csharp
public class UiTests
{
    [Test]
    [Retry(3)]
    public async Task Clicking_button_navigates_to_page()
    {
        // Simulate UI interaction
        await ClickButtonAsync();
        await WaitForPageLoadAsync();
        
        var currentUrl = await GetCurrentUrlAsync();
        await Assert.That(currentUrl).Contains("/success");
    }

    [Test]
    [Retry(5)]
    public async Task Element_becomes_visible()
    {
        await NavigateToPageAsync();
        
        var element = await WaitForElementAsync("#dynamic-content", timeout: TimeSpan.FromSeconds(10));
        
        await Assert.That(element).IsNotNull();
    }

    private async Task ClickButtonAsync() { await Task.Delay(100); }
    private async Task WaitForPageLoadAsync() { await Task.Delay(500); }
    private async Task<string> GetCurrentUrlAsync() { await Task.CompletedTask; return "/success"; }
    private async Task NavigateToPageAsync() { await Task.Delay(100); }
    private async Task<object?> WaitForElementAsync(string selector, TimeSpan timeout) 
    { 
        await Task.Delay((int)timeout.TotalMilliseconds / 2); 
        return Random.Shared.Next(0, 2) == 1 ? new object() : null;
    }
}
```

## Race Condition Testing

Test concurrent operations with retries:

```csharp
public class ConcurrencyTests
{
    [Test]
    [Retry(5)]
    public async Task Concurrent_updates_handle_conflicts()
    {
        var resource = new SharedResource();
        
        var task1 = UpdateResourceAsync(resource, "Value1");
        var task2 = UpdateResourceAsync(resource, "Value2");
        
        await Task.WhenAll(task1, task2);
        
        // One update should succeed
        await Assert.That(resource.Value).IsNotNull();
    }

    [Test]
    [Retry(3)]
    public async Task Optimistic_concurrency_succeeds()
    {
        var repository = new OptimisticRepository();
        var entity = await repository.GetAsync(1);
        
        entity.Name = "Updated";
        
        // May fail with concurrency exception, retry handles it
        await repository.UpdateAsync(entity);
        
        await Assert.That(entity.Name).IsEqualTo("Updated");
    }

    private async Task UpdateResourceAsync(SharedResource resource, string value)
    {
        await Task.Delay(Random.Shared.Next(10, 50));
        resource.Value = value;
    }

    private class SharedResource
    {
        public string? Value { get; set; }
    }

    private class OptimisticRepository
    {
        public async Task<Entity> GetAsync(int id)
        {
            await Task.Delay(10);
            return new Entity { Id = id, Name = "Original", Version = 1 };
        }

        public async Task UpdateAsync(Entity entity)
        {
            await Task.Delay(10);
            if (Random.Shared.Next(0, 3) == 0)
            {
                throw new DbUpdateConcurrencyException("Concurrency conflict");
            }
        }
    }

    private class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Version { get; set; }
    }
}
```

## Retry with Logging

Track retry attempts:

```csharp
public class RetryLoggingTests
{
    private int _attemptCount = 0;

    [Test]
    [Retry(3)]
    public async Task Test_with_retry_logging(TestContext context)
    {
        _attemptCount++;
        context.OutputWriter.WriteLine($"Attempt {_attemptCount}");
        
        var success = await UnstableOperation();
        
        if (!success)
        {
            context.OutputWriter.WriteLine($"Attempt {_attemptCount} failed, will retry...");
        }
        
        await Assert.That(success).IsTrue();
    }

    private async Task<bool> UnstableOperation()
    {
        await Task.Delay(10);
        return Random.Shared.Next(0, 3) == 2; // 33% success rate
    }
}
```

## Conditional Retry Logic

Implement custom retry conditions:

```csharp
public class ConditionalRetryTests
{
    [Test]
    [Retry(5)]
    public async Task Retry_only_on_specific_exceptions()
    {
        try
        {
            await OperationThatMayThrowAsync();
            await Assert.That(true).IsTrue();
        }
        catch (HttpRequestException)
        {
            // Retryable - network issue
            throw;
        }
        catch (InvalidOperationException)
        {
            // Not retryable - logical error
            throw;
        }
    }

    [Test]
    [Retry(3)]
    public async Task Retry_with_exponential_backoff()
    {
        var attempt = 0;
        
        try
        {
            attempt++;
            await OperationWithBackoffAsync(attempt);
            await Assert.That(true).IsTrue();
        }
        catch (Exception)
        {
            // Add delay before retry (simulated)
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            await Task.Delay(delay);
            throw;
        }
    }

    private async Task OperationThatMayThrowAsync()
    {
        await Task.Delay(10);
        var random = Random.Shared.Next(0, 3);
        if (random == 0) throw new HttpRequestException();
        if (random == 1) throw new InvalidOperationException();
    }

    private async Task OperationWithBackoffAsync(int attempt)
    {
        await Task.Delay(10);
        if (Random.Shared.Next(0, 2) == 0)
        {
            throw new Exception($"Failed attempt {attempt}");
        }
    }
}
```

## Integration Test Retries

Handle environment-dependent failures:

```csharp
public class IntegrationRetryTests
{
    [Test]
    [Retry(5)]
    [Category("Integration")]
    public async Task Container_startup_succeeds()
    {
        // Docker container might take time to start
        var isHealthy = await CheckContainerHealthAsync();
        await Assert.That(isHealthy).IsTrue();
    }

    [Test]
    [Retry(3)]
    [Category("Integration")]
    public async Task Database_migration_completes()
    {
        // Migration might fail if database is busy
        await RunMigrationAsync();
        
        var version = await GetSchemaVersionAsync();
        await Assert.That(version).IsGreaterThan(0);
    }

    [Test]
    [Retry(10)]
    [Category("Integration")]
    public async Task Message_queue_processes_message()
    {
        await SendMessageAsync("test-message");
        
        // Message processing might take time
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        var processed = await IsMessageProcessedAsync("test-message");
        await Assert.That(processed).IsTrue();
    }

    private async Task<bool> CheckContainerHealthAsync()
    {
        await Task.Delay(100);
        return Random.Shared.Next(0, 3) == 2;
    }

    private async Task RunMigrationAsync()
    {
        await Task.Delay(100);
        if (Random.Shared.Next(0, 3) == 0)
        {
            throw new Exception("Migration failed");
        }
    }

    private async Task<int> GetSchemaVersionAsync()
    {
        await Task.Delay(10);
        return 1;
    }

    private async Task SendMessageAsync(string message)
    {
        await Task.Delay(10);
    }

    private async Task<bool> IsMessageProcessedAsync(string message)
    {
        await Task.Delay(10);
        return Random.Shared.Next(0, 2) == 1;
    }
}
```

## Performance Test Retries

Handle variance in performance tests:

```csharp
public class PerformanceRetryTests
{
    [Test]
    [Retry(5)]
    [Category("Performance")]
    public async Task Operation_completes_within_performance_target()
    {
        var stopwatch = Stopwatch.StartNew();
        
        await PerformanceOperationAsync();
        
        stopwatch.Stop();
        
        // Allow retries for performance variance
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
    }

    [Test]
    [Retry(3)]
    [Category("Performance")]
    public async Task Throughput_meets_target()
    {
        var itemsProcessed = 0;
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await ProcessItemAsync();
            itemsProcessed++;
        }
        
        // Expect at least 100 items/second
        var itemsPerSecond = itemsProcessed / stopwatch.Elapsed.TotalSeconds;
        await Assert.That(itemsPerSecond).IsGreaterThan(100);
    }

    private async Task PerformanceOperationAsync()
    {
        await Task.Delay(Random.Shared.Next(500, 1500));
    }

    private async Task ProcessItemAsync()
    {
        await Task.Delay(Random.Shared.Next(5, 15));
    }
}
```

## Cleanup After Failed Retries

Ensure cleanup happens even after retries:

```csharp
public class RetryCleanupTests
{
    private Resource? _resource;

    [Before(Test)]
    public async Task Setup()
    {
        _resource = new Resource();
        await _resource.InitializeAsync();
    }

    [Test]
    [Retry(3)]
    public async Task Test_with_cleanup()
    {
        await _resource!.UseAsync();
        
        var result = await UnstableOperation();
        await Assert.That(result).IsTrue();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        // Cleanup runs after all retries complete or test passes
        if (_resource != null)
        {
            await _resource.DisposeAsync();
        }
    }

    private async Task<bool> UnstableOperation()
    {
        await Task.Delay(10);
        return Random.Shared.Next(0, 2) == 1;
    }

    private class Resource : IAsyncDisposable
    {
        public async Task InitializeAsync() { await Task.Delay(10); }
        public async Task UseAsync() { await Task.Delay(10); }
        public async ValueTask DisposeAsync() { await Task.Delay(10); }
    }
}
```

## Best Practices

### 1. Use Retries Sparingly

```csharp
// ✅ Good - Retry for known flaky external dependency
[Test]
[Retry(3)]
public async Task External_api_call_succeeds()
{
    var response = await _httpClient.GetAsync("https://external-api.com");
    await Assert.That(response.IsSuccessStatusCode).IsTrue();
}

// ❌ Wrong - Retry to hide test bugs
[Test]
[Retry(10)]
public async Task Poorly_written_test()
{
    // Test has race condition, retry masks the problem
}
```

### 2. Retry Only Appropriate Tests

```csharp
// ✅ Good - Integration test with external service
[Test]
[Retry(3)]
[Category("Integration")]
public async Task Database_query_succeeds() { }

// ❌ Wrong - Pure unit test shouldn't need retry
[Test]
[Retry(3)]
[Category("Unit")]
public async Task Adding_numbers_returns_sum() { }
```

### 3. Use Reasonable Retry Counts

```csharp
// ✅ Good - Reasonable retry count
[Test]
[Retry(3)]
public async Task Network_operation() { }

// ❌ Wrong - Too many retries
[Test]
[Retry(100)]
public async Task Operation() { } // If it fails 100 times, it's broken
```

### 4. Combine with Proper Timeouts

```csharp
// ✅ Good - Timeout + retry
[Test]
[Retry(3)]
public async Task Operation_with_timeout()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await OperationAsync(cts.Token);
}

// ❌ Wrong - Retry without timeout
[Test]
[Retry(3)]
public async Task Operation_no_timeout()
{
    await OperationAsync(); // Might hang indefinitely on each retry
}
```

### 5. Document Why Retries Are Needed

```csharp
/// <summary>
/// Tests external payment gateway. Retries are necessary because
/// the sandbox environment occasionally returns 503 errors during
/// deployment windows.
/// </summary>
[Test]
[Retry(5)]
public async Task Payment_processing_succeeds() { }
```

## When to Use Retries

### ✅ Use Retries For

- External service dependencies (APIs, databases)
- Network operations
- Container/environment startup
- Timing-sensitive integration tests
- Known infrastructure flakiness

### ❌ Don't Use Retries For

- Pure unit tests
- Hiding bugs or race conditions
- Masking test design problems
- Tests that should be deterministic
- Logic errors in code under test

## See Also

- [Async Testing](async_testing.md) - Async operations
- [Test Organization](test_organization.md) - Categorizing flaky tests
- [Output and Diagnostics](output_diagnostics.md) - Logging retry attempts
