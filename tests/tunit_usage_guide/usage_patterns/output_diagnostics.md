# Test Output and Diagnostics

Writing output, logging, and debugging tests using TestContext.

## TestContext

Access test metadata and write output:

```csharp
using TUnit.Core;

public class TestContextTests
{
    [Test]
    public async Task Writing_output_to_test_context(TestContext context)
    {
        context.OutputWriter.WriteLine("Test started");
        
        var result = PerformCalculation();
        
        context.OutputWriter.WriteLine($"Result: {result}");
        
        await Assert.That(result).IsGreaterThan(0);
        
        context.OutputWriter.WriteLine("Test completed");
    }

    private int PerformCalculation() => 42;
}
```

## Writing Test Output

### Basic Output

```csharp
public class OutputTests
{
    [Test]
    public async Task Test_with_diagnostic_output(TestContext context)
    {
        context.OutputWriter.WriteLine("Starting test");
        
        // Arrange
        context.OutputWriter.WriteLine("Creating test data...");
        var data = CreateTestData();
        
        // Act
        context.OutputWriter.WriteLine("Performing operation...");
        var result = ProcessData(data);
        
        // Assert
        context.OutputWriter.WriteLine($"Result: {result}");
        await Assert.That(result).IsNotNull();
    }
}
```

### Formatted Output

```csharp
public class FormattedOutputTests
{
    [Test]
    public async Task Test_with_formatted_output(TestContext context)
    {
        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
        
        context.OutputWriter.WriteLine($"Testing user: ID={user.Id}, Name={user.Name}");
        
        var result = await ValidateUser(user);
        
        context.OutputWriter.WriteLine($"Validation result: {result}");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Test_with_json_output(TestContext context)
    {
        var data = new { Name = "Test", Value = 42 };
        
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        context.OutputWriter.WriteLine("Test data:");
        context.OutputWriter.WriteLine(json);
        
        await Assert.That(data.Value).IsEqualTo(42);
    }
}
```

## Debugging Failed Tests

### Detailed Error Information

```csharp
public class DebuggingTests
{
    [Test]
    public async Task Complex_operation_with_debug_output(TestContext context)
    {
        var items = new[] { 1, 2, 3, 4, 5 };
        
        context.OutputWriter.WriteLine($"Processing {items.Length} items");
        
        var results = new List<int>();
        foreach (var item in items)
        {
            var processed = ProcessItem(item);
            results.Add(processed);
            context.OutputWriter.WriteLine($"Item {item} -> {processed}");
        }
        
        context.OutputWriter.WriteLine($"Final results: {string.Join(", ", results)}");
        
        await Assert.That(results.Count).IsEqualTo(items.Length);
    }

    private int ProcessItem(int item) => item * 2;
}
```

### Conditional Logging

```csharp
public class ConditionalLoggingTests
{
    private readonly bool _verboseLogging = true;

    [Test]
    public async Task Test_with_conditional_logging(TestContext context)
    {
        if (_verboseLogging)
        {
            context.OutputWriter.WriteLine("Verbose logging enabled");
        }

        var result = await PerformOperation();

        if (_verboseLogging)
        {
            context.OutputWriter.WriteLine($"Operation result: {result}");
        }

        await Assert.That(result).IsNotNull();
    }

    private async Task<string> PerformOperation()
    {
        await Task.Delay(10);
        return "Success";
    }
}
```

## Accessing Test Metadata

### Test Information

```csharp
public class MetadataTests
{
    [Test]
    [Property("Priority", "High")]
    [Category("Critical")]
    public async Task Accessing_test_metadata(TestContext context)
    {
        context.OutputWriter.WriteLine($"Test Name: {context.TestDetails.TestName}");
        context.OutputWriter.WriteLine($"Test Class: {context.TestDetails.ClassName}");
        
        // Access test properties and categories
        var properties = context.TestDetails.Properties;
        var categories = context.TestDetails.Categories;
        
        context.OutputWriter.WriteLine($"Properties: {string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"))}");
        context.OutputWriter.WriteLine($"Categories: {string.Join(", ", categories)}");
        
        await Assert.That(true).IsTrue();
    }
}
```

### Test Timing

```csharp
public class TimingTests
{
    [Test]
    public async Task Measuring_test_duration(TestContext context)
    {
        var startTime = DateTime.UtcNow;
        context.OutputWriter.WriteLine($"Test started at: {startTime}");
        
        // Perform test operations
        await Task.Delay(100);
        var result = await PerformOperation();
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        
        context.OutputWriter.WriteLine($"Test completed at: {endTime}");
        context.OutputWriter.WriteLine($"Duration: {duration.TotalMilliseconds}ms");
        
        await Assert.That(result).IsNotNull();
    }

    private async Task<string> PerformOperation()
    {
        await Task.Delay(50);
        return "Done";
    }
}
```

## Performance Monitoring

### Timing Critical Operations

```csharp
public class PerformanceTests
{
    [Test]
    public async Task Monitoring_operation_performance(TestContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        context.OutputWriter.WriteLine("Starting performance-critical operation...");
        
        var result = await ExpensiveOperation();
        
        stopwatch.Stop();
        
        context.OutputWriter.WriteLine($"Operation completed in {stopwatch.ElapsedMilliseconds}ms");
        
        // Assert performance requirement
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(1000);
        await Assert.That(result).IsNotNull();
    }

    private async Task<string> ExpensiveOperation()
    {
        await Task.Delay(100);
        return "Result";
    }
}
```

### Memory Monitoring

```csharp
public class MemoryTests
{
    [Test]
    public async Task Monitoring_memory_usage(TestContext context)
    {
        var startMemory = GC.GetTotalMemory(forceFullCollection: false);
        context.OutputWriter.WriteLine($"Starting memory: {startMemory / 1024}KB");
        
        // Perform memory-intensive operation
        var data = new List<byte[]>();
        for (int i = 0; i < 1000; i++)
        {
            data.Add(new byte[1024]);
        }
        
        var endMemory = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsed = endMemory - startMemory;
        
        context.OutputWriter.WriteLine($"Ending memory: {endMemory / 1024}KB");
        context.OutputWriter.WriteLine($"Memory used: {memoryUsed / 1024}KB");
        
        await Assert.That(data.Count).IsEqualTo(1000);
    }
}
```

## Debugging Async Operations

```csharp
public class AsyncDebuggingTests
{
    [Test]
    public async Task Debugging_async_chain(TestContext context)
    {
        context.OutputWriter.WriteLine("Starting async chain...");
        
        var step1 = await Step1();
        context.OutputWriter.WriteLine($"Step 1 completed: {step1}");
        
        var step2 = await Step2(step1);
        context.OutputWriter.WriteLine($"Step 2 completed: {step2}");
        
        var step3 = await Step3(step2);
        context.OutputWriter.WriteLine($"Step 3 completed: {step3}");
        
        await Assert.That(step3).IsEqualTo("Step1->Step2->Step3");
    }

    private async Task<string> Step1()
    {
        await Task.Delay(10);
        return "Step1";
    }

    private async Task<string> Step2(string previous)
    {
        await Task.Delay(10);
        return $"{previous}->Step2";
    }

    private async Task<string> Step3(string previous)
    {
        await Task.Delay(10);
        return $"{previous}->Step3";
    }
}
```

## Debugging Parallel Operations

```csharp
public class ParallelDebuggingTests
{
    [Test]
    public async Task Debugging_parallel_operations(TestContext context)
    {
        var tasks = new[]
        {
            Task1(context),
            Task2(context),
            Task3(context)
        };

        var results = await Task.WhenAll(tasks);
        
        context.OutputWriter.WriteLine($"All tasks completed. Results: {string.Join(", ", results)}");
        
        await Assert.That(results.Length).IsEqualTo(3);
    }

    private async Task<string> Task1(TestContext context)
    {
        context.OutputWriter.WriteLine("Task1 started");
        await Task.Delay(50);
        context.OutputWriter.WriteLine("Task1 completed");
        return "Task1";
    }

    private async Task<string> Task2(TestContext context)
    {
        context.OutputWriter.WriteLine("Task2 started");
        await Task.Delay(100);
        context.OutputWriter.WriteLine("Task2 completed");
        return "Task2";
    }

    private async Task<string> Task3(TestContext context)
    {
        context.OutputWriter.WriteLine("Task3 started");
        await Task.Delay(75);
        context.OutputWriter.WriteLine("Task3 completed");
        return "Task3";
    }
}
```

## Debugging Database Operations

```csharp
public class DatabaseDebuggingTests
{
    [Test]
    public async Task Debugging_database_query(TestContext context)
    {
        await using var connection = new SqlConnection("connection-string");
        await connection.OpenAsync();
        
        context.OutputWriter.WriteLine("Database connection opened");
        
        var query = "SELECT * FROM Users WHERE Age > @MinAge";
        context.OutputWriter.WriteLine($"Executing query: {query}");
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@MinAge", 18);
        
        context.OutputWriter.WriteLine("Query parameters: @MinAge=18");
        
        await using var reader = await command.ExecuteReaderAsync();
        
        var count = 0;
        while (await reader.ReadAsync())
        {
            count++;
            if (count <= 5) // Log first 5 results
            {
                context.OutputWriter.WriteLine($"User: {reader["Name"]}, Age: {reader["Age"]}");
            }
        }
        
        context.OutputWriter.WriteLine($"Total results: {count}");
        
        await Assert.That(count).IsGreaterThan(0);
    }
}
```

## Debugging API Calls

```csharp
public class ApiDebuggingTests
{
    [Test]
    public async Task Debugging_http_request(TestContext context)
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        
        var requestUrl = "/users/1";
        context.OutputWriter.WriteLine($"GET {requestUrl}");
        
        var response = await client.GetAsync(requestUrl);
        
        context.OutputWriter.WriteLine($"Status: {response.StatusCode}");
        context.OutputWriter.WriteLine("Response Headers:");
        foreach (var header in response.Headers)
        {
            context.OutputWriter.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }
        
        var content = await response.Content.ReadAsStringAsync();
        context.OutputWriter.WriteLine($"Response body: {content}");
        
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
```

## Structured Logging

```csharp
public class StructuredLoggingTests
{
    [Test]
    public async Task Test_with_structured_logging(TestContext context)
    {
        var testData = new
        {
            UserId = 123,
            UserName = "John",
            Timestamp = DateTime.UtcNow,
            Environment = "Test"
        };

        // Log structured data
        context.OutputWriter.WriteLine("=== Test Configuration ===");
        context.OutputWriter.WriteLine($"User ID: {testData.UserId}");
        context.OutputWriter.WriteLine($"User Name: {testData.UserName}");
        context.OutputWriter.WriteLine($"Timestamp: {testData.Timestamp:yyyy-MM-dd HH:mm:ss}");
        context.OutputWriter.WriteLine($"Environment: {testData.Environment}");
        context.OutputWriter.WriteLine("=========================");

        var result = await ProcessUser(testData.UserId);

        context.OutputWriter.WriteLine("=== Test Results ===");
        context.OutputWriter.WriteLine($"Result: {result}");
        context.OutputWriter.WriteLine("====================");

        await Assert.That(result).IsNotNull();
    }

    private async Task<string> ProcessUser(int userId)
    {
        await Task.Delay(10);
        return $"Processed user {userId}";
    }
}
```

## Error Diagnostics

```csharp
public class ErrorDiagnosticsTests
{
    [Test]
    public async Task Capturing_error_details(TestContext context)
    {
        try
        {
            context.OutputWriter.WriteLine("Attempting risky operation...");
            
            await RiskyOperation();
            
            await Assert.That(true).IsTrue();
        }
        catch (Exception ex)
        {
            context.OutputWriter.WriteLine("=== ERROR OCCURRED ===");
            context.OutputWriter.WriteLine($"Exception Type: {ex.GetType().Name}");
            context.OutputWriter.WriteLine($"Message: {ex.Message}");
            context.OutputWriter.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                context.OutputWriter.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            context.OutputWriter.WriteLine("=====================");
            
            throw; // Re-throw to fail the test
        }
    }

    private async Task RiskyOperation()
    {
        await Task.Delay(10);
        // Simulate operation
    }
}
```

## Best Practices

### 1. Use Output for Debugging, Not Production

```csharp
// ✅ Good - Diagnostic output for debugging
[Test]
public async Task Test_with_debug_output(TestContext context)
{
    context.OutputWriter.WriteLine($"Debug: Processing item {item}");
    await Assert.That(result).IsEqualTo(expected);
}

// ❌ Wrong - Excessive output in all tests
[Test]
public async Task Test_with_too_much_output(TestContext context)
{
    context.OutputWriter.WriteLine("Starting");
    context.OutputWriter.WriteLine("Step 1");
    context.OutputWriter.WriteLine("Step 2");
    // ... 50 more lines
}
```

### 2. Include Relevant Context in Output

```csharp
// ✅ Good - Meaningful context
context.OutputWriter.WriteLine($"Testing with userId={userId}, status={status}");

// ❌ Wrong - No context
context.OutputWriter.WriteLine("Testing...");
```

### 3. Use TestContext Parameter

```csharp
// ✅ Good - Accept TestContext parameter
[Test]
public async Task Proper_test(TestContext context)
{
    context.OutputWriter.WriteLine("Output");
}

// ❌ Wrong - No access to TestContext
[Test]
public async Task Missing_context()
{
    // Can't write output
}
```

### 4. Structure Output for Readability

```csharp
// ✅ Good - Structured output
context.OutputWriter.WriteLine("=== Setup Phase ===");
// ... setup logs
context.OutputWriter.WriteLine("=== Execution Phase ===");
// ... execution logs
context.OutputWriter.WriteLine("=== Verification Phase ===");
// ... verification logs

// ❌ Wrong - Unstructured
context.OutputWriter.WriteLine("doing stuff");
context.OutputWriter.WriteLine("more stuff");
```

## See Also

- [Basic Tests](basic_tests.md) - Test fundamentals
- [Async Testing](async_testing.md) - Debugging async code
- [Setup and Teardown](setup_teardown.md) - Test lifecycle
