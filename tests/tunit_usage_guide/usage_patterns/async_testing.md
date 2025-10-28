# Async Testing

TUnit is designed async-first, making asynchronous testing natural and straightforward.

## Basic Async Tests

All tests return `Task` and use `await`:

```csharp
using TUnit.Core;

public class AsyncTests
{
    [Test]
    public async Task Fetching_data_asynchronously_returns_results()
    {
        // Arrange
        var service = new DataService();

        // Act
        var data = await service.GetDataAsync();

        // Assert
        await Assert.That(data).IsNotNull();
        await Assert.That(data.Count).IsGreaterThan(0);
    }
}
```

## Testing Async Methods

```csharp
public class UserServiceTests
{
    [Test]
    public async Task Creating_user_asynchronously_returns_user_with_id()
    {
        // Arrange
        var service = new UserService();
        var user = new User { Name = "John", Email = "john@example.com" };

        // Act
        var created = await service.CreateAsync(user);

        // Assert
        await Assert.That(created).IsNotNull();
        await Assert.That(created.Id).IsGreaterThan(0);
        await Assert.That(created.Name).IsEqualTo(user.Name);
    }

    [Test]
    public async Task Deleting_user_asynchronously_removes_from_database()
    {
        // Arrange
        var service = new UserService();
        var user = await service.CreateAsync(new User { Name = "Test" });

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        var retrieved = await service.GetByIdAsync(user.Id);
        await Assert.That(retrieved).IsNull();
    }
}
```

## Testing Multiple Async Operations

```csharp
[Test]
public async Task Processing_multiple_items_concurrently_completes_all()
{
    // Arrange
    var service = new BatchService();
    var items = Enumerable.Range(1, 10).ToArray();

    // Act
    var tasks = items.Select(item => service.ProcessAsync(item));
    var results = await Task.WhenAll(tasks);

    // Assert
    await Assert.That(results).All(r => r != null);
    await Assert.That(results.Length).IsEqualTo(items.Length);
}
```

## Testing Async Exceptions

```csharp
public class ExceptionTests
{
    [Test]
    public async Task Invalid_operation_throws_exception_asynchronously()
    {
        // Arrange
        var service = new ValidationService();

        // Act & Assert
        await Assert.That(async () => await service.ValidateAsync(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Async_method_with_specific_error_message_throws()
    {
        var service = new PaymentService();

        var exception = await Assert.That(
            async () => await service.ProcessPaymentAsync(amount: -100))
            .Throws<InvalidOperationException>();

        await Assert.That(exception.Message).Contains("negative");
    }
}
```

## Testing Task-Based Operations

### Testing Task Completion

```csharp
[Test]
public async Task Long_running_operation_completes_successfully()
{
    var service = new LongRunningService();

    var task = service.StartOperationAsync();
    
    // Verify task completes
    await task;
    await Assert.That(task.IsCompleted).IsTrue();
}

[Test]
public async Task Operation_completes_within_timeout()
{
    var service = new Service();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var task = service.DoWorkAsync(cts.Token);
    
    await task;  // Should complete before timeout
    await Assert.That(task.IsCompletedSuccessfully).IsTrue();
}
```

### Testing Task Results

```csharp
[Test]
public async Task Async_calculation_returns_correct_result()
{
    var calculator = new AsyncCalculator();

    var result = await calculator.CalculateAsync(10, 20);

    await Assert.That(result).IsEqualTo(30);
}

[Test]
public async Task Async_query_returns_expected_data()
{
    var repository = new UserRepository();

    var users = await repository.GetActiveUsersAsync();

    await Assert.That(users).IsNotEmpty();
    await Assert.That(users).All(u => u.IsActive);
}
```

## Testing ValueTask

```csharp
public class ValueTaskTests
{
    [Test]
    public async Task ValueTask_operation_returns_expected_value()
    {
        var service = new OptimizedService();

        var result = await service.GetCachedValueAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ValueTask_without_result_completes()
    {
        var service = new OptimizedService();

        await service.UpdateCacheAsync(100);

        var cached = await service.GetCachedValueAsync();
        await Assert.That(cached).IsEqualTo(100);
    }
}
```

## Testing IAsyncEnumerable

```csharp
public class AsyncEnumerableTests
{
    [Test]
    public async Task Streaming_data_yields_all_items()
    {
        // Arrange
        var service = new StreamingService();
        var items = new List<int>();

        // Act
        await foreach (var item in service.GetDataStreamAsync())
        {
            items.Add(item);
        }

        // Assert
        await Assert.That(items.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Async_enumerable_can_be_filtered()
    {
        var service = new StreamingService();
        var evenItems = new List<int>();

        await foreach (var item in service.GetDataStreamAsync())
        {
            if (item % 2 == 0)
            {
                evenItems.Add(item);
            }
        }

        await Assert.That(evenItems).All(x => x % 2 == 0);
    }
}
```

## Testing Cancellation

```csharp
public class CancellationTests
{
    [Test]
    public async Task Cancelling_operation_stops_execution()
    {
        // Arrange
        var service = new CancellableService();
        var cts = new CancellationTokenSource();

        // Act
        var task = service.LongRunningAsync(cts.Token);
        await Task.Delay(100);  // Let it start
        cts.Cancel();

        // Assert
        await Assert.That(async () => await task)
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Cancellation_token_is_respected()
    {
        var service = new Service();
        var cts = new CancellationTokenSource();
        cts.Cancel();  // Cancel immediately

        await Assert.That(async () => await service.ProcessAsync(cts.Token))
            .Throws<OperationCanceledException>();
    }
}
```

## Testing Timeouts

```csharp
public class TimeoutTests
{
    [Test]
    public async Task Operation_completes_before_timeout()
    {
        var service = new Service();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await service.QuickOperationAsync(cts.Token);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Slow_operation_times_out()
    {
        var service = new Service();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.That(async () => await service.SlowOperationAsync(cts.Token))
            .Throws<OperationCanceledException>();
    }
}
```

## Testing Async Initialization

```csharp
public class InitializationTests
{
    [Test]
    public async Task Service_initializes_asynchronously()
    {
        var service = new AsyncInitializableService();

        await service.InitializeAsync();

        await Assert.That(service.IsInitialized).IsTrue();
    }

    [Test]
    public async Task Using_service_before_initialization_throws()
    {
        var service = new AsyncInitializableService();

        await Assert.That(async () => await service.DoWorkAsync())
            .Throws<InvalidOperationException>();
    }
}
```

## Testing Async Disposal

```csharp
public class DisposalTests
{
    [Test]
    public async Task Resource_disposes_asynchronously()
    {
        var resource = new AsyncDisposableResource();
        await resource.InitializeAsync();

        await resource.DisposeAsync();

        await Assert.That(resource.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Using_disposed_resource_throws()
    {
        var resource = new AsyncDisposableResource();
        await resource.DisposeAsync();

        await Assert.That(async () => await resource.UseAsync())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Resource_cleanup_via_await_using()
    {
        AsyncDisposableResource? resource = null;

        await using (resource = new AsyncDisposableResource())
        {
            await resource.InitializeAsync();
            await Assert.That(resource.IsInitialized).IsTrue();
        }

        await Assert.That(resource.IsDisposed).IsTrue();
    }
}
```

## Testing Parallel Async Operations

```csharp
public class ParallelTests
{
    [Test]
    public async Task Multiple_async_operations_run_concurrently()
    {
        var service = new ParallelService();
        var startTime = DateTime.UtcNow;

        var task1 = service.Operation1Async();  // Takes 1 second
        var task2 = service.Operation2Async();  // Takes 1 second
        var task3 = service.Operation3Async();  // Takes 1 second

        await Task.WhenAll(task1, task2, task3);

        var duration = DateTime.UtcNow - startTime;
        
        // Should complete in ~1 second (parallel), not 3 seconds (sequential)
        await Assert.That(duration.TotalSeconds).IsLessThan(2);
    }

    [Test]
    public async Task Processing_collection_in_parallel_improves_performance()
    {
        var service = new BatchProcessor();
        var items = Enumerable.Range(1, 100).ToArray();

        var startTime = DateTime.UtcNow;
        
        await service.ProcessInParallelAsync(items, maxDegreeOfParallelism: 10);
        
        var duration = DateTime.UtcNow - startTime;

        await Assert.That(service.ProcessedCount).IsEqualTo(items.Length);
    }
}
```

## Testing Async Retry Logic

```csharp
public class RetryTests
{
    [Test]
    public async Task Failed_operation_retries_and_eventually_succeeds()
    {
        var service = new RetryableService();
        service.FailureCount = 2;  // Fail twice, then succeed

        var result = await service.OperationWithRetryAsync(maxRetries: 3);

        await Assert.That(result).IsTrue();
        await Assert.That(service.AttemptCount).IsEqualTo(3);
    }

    [Test]
    public async Task Operation_fails_after_max_retries()
    {
        var service = new RetryableService();
        service.FailureCount = 10;  // Always fail

        await Assert.That(async () => await service.OperationWithRetryAsync(maxRetries: 3))
            .Throws<InvalidOperationException>();
    }
}
```

## Testing Async Event Handlers

```csharp
public class AsyncEventTests
{
    [Test]
    public async Task Async_event_handler_is_invoked()
    {
        var publisher = new EventPublisher();
        var wasInvoked = false;

        publisher.DataReceived += async (sender, args) =>
        {
            await Task.Delay(10);
            wasInvoked = true;
        };

        await publisher.PublishAsync("test data");

        await Assert.That(wasInvoked).IsTrue();
    }

    [Test]
    public async Task Multiple_async_handlers_are_all_invoked()
    {
        var publisher = new EventPublisher();
        var invocationCount = 0;

        publisher.DataReceived += async (s, e) => { await Task.Delay(10); invocationCount++; };
        publisher.DataReceived += async (s, e) => { await Task.Delay(10); invocationCount++; };
        publisher.DataReceived += async (s, e) => { await Task.Delay(10); invocationCount++; };

        await publisher.PublishAsync("test");

        await Assert.That(invocationCount).IsEqualTo(3);
    }
}
```

## Best Practices

### 1. Always Await Async Operations

```csharp
// ✅ Good - Awaited
[Test]
public async Task Proper_async_test()
{
    var result = await service.GetDataAsync();
    await Assert.That(result).IsNotNull();
}

// ❌ Wrong - Not awaited
[Test]
public async Task Improper_async_test()
{
    var task = service.GetDataAsync();  // Not awaited!
    await Assert.That(task).IsNotNull();  // Testing the task, not the result
}
```

### 2. Use ConfigureAwait(false) in Library Code, Not Tests

```csharp
// ✅ Good - Simple await in tests
[Test]
public async Task Test_method()
{
    await service.DoWorkAsync();
    await Assert.That(service.WorkComplete).IsTrue();
}

// ❌ Unnecessary - ConfigureAwait not needed in tests
[Test]
public async Task Overcomplicated_test()
{
    await service.DoWorkAsync().ConfigureAwait(false);  // Not needed
}
```

### 3. Test Cancellation Scenarios

```csharp
// ✅ Good - Test cancellation
[Test]
public async Task Operation_respects_cancellation()
{
    var cts = new CancellationTokenSource();
    var task = service.LongOperationAsync(cts.Token);
    
    cts.Cancel();
    
    await Assert.That(async () => await task)
        .Throws<OperationCanceledException>();
}
```

### 4. Handle Async Disposal Properly

```csharp
// ✅ Good - Proper async disposal
[Test]
public async Task Resource_cleanup()
{
    await using var resource = new AsyncDisposableResource();
    await resource.UseAsync();
    // Automatically disposed here
}

// ❌ Wrong - Sync dispose on async resource
[Test]
public async Task Improper_cleanup()
{
    using var resource = new AsyncDisposableResource();  // Should use await using
    await resource.UseAsync();
}
```

## See Also

- [Basic Tests](basic_tests.md) - Test fundamentals
- [Assertions](assertions.md) - Assertion techniques
- [Setup and Teardown](setup_teardown.md) - Test lifecycle
