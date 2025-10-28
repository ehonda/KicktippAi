# Project Style Guide

This document defines conventions and best practices for writing tests in this project using TUnit.

## Test Naming Conventions

### Fluent Test Names

**Do:** Use fluent, descriptive names that read like sentences

```csharp
[Test]
public async Task Adding_positive_numbers_returns_sum()
{
    // Test implementation
}

[Test]
public async Task Dividing_by_zero_throws_DivideByZeroException()
{
    // Test implementation
}

[Test]
public async Task Empty_string_is_considered_invalid()
{
    // Test implementation
}
```

**Don't:** Use the traditional `[MethodName]_[Scenario]_[Expected]` format with abbreviations

```csharp
// ❌ Avoid
[Test]
public async Task Add_PosNums_ReturnsSum() { }

// ❌ Avoid
[Test]
public async Task Div_ZeroDiv_Throws() { }

// ❌ Avoid
[Test]
public async Task ValidateStr_EmptyStr_RetsFalse() { }
```

### Naming Guidelines

1. **Start with the action** being tested (e.g., `Adding`, `Dividing`, `Validating`)
2. **Describe the scenario** clearly (e.g., `positive_numbers`, `by_zero`, `empty_string`)
3. **State the expected outcome** (e.g., `returns_sum`, `throws_exception`, `is_invalid`)
4. **Use underscores** to separate words for readability
5. **Be specific** - avoid vague names like `Test1` or `BasicTest`

## Examples by Category

### Testing Methods

```csharp
public class CalculatorTests
{
    [Test]
    public async Task Adding_two_positive_numbers_returns_their_sum()
    {
        var calculator = new Calculator();
        var result = calculator.Add(2, 3);
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    public async Task Subtracting_larger_from_smaller_returns_negative()
    {
        var calculator = new Calculator();
        var result = calculator.Subtract(3, 7);
        await Assert.That(result).IsEqualTo(-4);
    }

    [Test]
    public async Task Dividing_by_zero_throws_DivideByZeroException()
    {
        var calculator = new Calculator();
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>();
    }
}
```

### Testing Validation

```csharp
public class UserValidatorTests
{
    [Test]
    public async Task Validating_valid_user_returns_true()
    {
        var validator = new UserValidator();
        var user = new User { Name = "John", Email = "john@example.com" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validating_user_with_empty_name_returns_false()
    {
        var validator = new UserValidator();
        var user = new User { Name = "", Email = "john@example.com" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Validating_user_with_invalid_email_returns_false()
    {
        var validator = new UserValidator();
        var user = new User { Name = "John", Email = "invalid-email" };
        
        var result = validator.Validate(user);
        
        await Assert.That(result).IsFalse();
    }
}
```

### Testing State Changes

```csharp
public class ShoppingCartTests
{
    [Test]
    public async Task Adding_item_increases_cart_count()
    {
        var cart = new ShoppingCart();
        var item = new CartItem { ProductId = 1, Quantity = 1 };
        
        cart.AddItem(item);
        
        await Assert.That(cart.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Removing_last_item_empties_cart()
    {
        var cart = new ShoppingCart();
        var item = new CartItem { ProductId = 1, Quantity = 1 };
        cart.AddItem(item);
        
        cart.RemoveItem(item.ProductId);
        
        await Assert.That(cart.Items).IsEmpty();
    }

    [Test]
    public async Task Clearing_cart_removes_all_items()
    {
        var cart = new ShoppingCart();
        cart.AddItem(new CartItem { ProductId = 1, Quantity = 1 });
        cart.AddItem(new CartItem { ProductId = 2, Quantity = 2 });
        
        cart.Clear();
        
        await Assert.That(cart.Items).IsEmpty();
    }
}
```

## Test Organization

### Class Naming

Test classes should be named after the class being tested with a `Tests` suffix:

```csharp
// Testing Calculator class
public class CalculatorTests { }

// Testing UserService class
public class UserServiceTests { }

// Testing OrderValidator class
public class OrderValidatorTests { }
```

### Namespace Organization

Match the namespace structure of the code being tested, but add `.Tests`:

```csharp
// Production code: KicktippAi.Core.Services
namespace KicktippAi.Core.Services.Tests;

// Production code: KicktippAi.Orchestrator.Commands
namespace KicktippAi.Orchestrator.Commands.Tests;
```

### File Organization

- One test class per file
- File name matches class name: `CalculatorTests.cs`
- Group related tests in the same class
- Use nested classes for grouping when appropriate

## Code Style

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

### Use Clear Variable Names

Prefer descriptive names over abbreviations:

```csharp
// ✅ Good
var expectedResult = 42;
var actualResult = calculator.Add(20, 22);
await Assert.That(actualResult).IsEqualTo(expectedResult);

// ❌ Avoid
var exp = 42;
var act = calc.Add(20, 22);
await Assert.That(act).IsEqualTo(exp);
```

### One Logical Assert Per Test

Focus each test on one specific behavior:

```csharp
// ✅ Good - Single concern
[Test]
public async Task User_registration_creates_new_user()
{
    var result = await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(result.Success).IsTrue();
}

[Test]
public async Task User_registration_sends_confirmation_email()
{
    await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(emailService.SentEmails).Contains(e => e.To == "user@example.com");
}

// ❌ Avoid - Multiple concerns
[Test]
public async Task User_registration_works()
{
    var result = await authService.RegisterAsync("user@example.com", "password");
    await Assert.That(result.Success).IsTrue();
    await Assert.That(emailService.SentEmails).Contains(e => e.To == "user@example.com");
}
```

**Exception:** Use `Assert.Multiple()` when testing multiple properties of the same object:

```csharp
[Test]
public async Task Created_user_has_correct_properties()
{
    var user = await userService.CreateAsync("John", "john@example.com");

    using (Assert.Multiple())
    {
        await Assert.That(user.Name).IsEqualTo("John");
        await Assert.That(user.Email).IsEqualTo("john@example.com");
        await Assert.That(user.CreatedDate).IsGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }
}
```

## Test Attributes

### Use Descriptive Categories

```csharp
[Test]
[Category("Integration")]
[Category("Database")]
public async Task Database_query_returns_users()
{
    // Test implementation
}

[Test]
[Category("Unit")]
[Category("Validation")]
public async Task Validator_rejects_invalid_input()
{
    // Test implementation
}
```

### Add Display Names for Complex Scenarios

```csharp
[Test]
[DisplayName("User registration with existing email should fail")]
public async Task Register_User_ExistingEmail()
{
    // Test implementation
}
```

## Performance Considerations

### Share Expensive Setup

Use class-level setup for expensive operations:

```csharp
public class DatabaseIntegrationTests
{
    private static DatabaseConnection? _connection;

    [Before(Class)]
    public static async Task SetupDatabase()
    {
        _connection = new DatabaseConnection();
        await _connection.OpenAsync();
        await _connection.MigrateAsync();
    }

    [After(Class)]
    public static async Task CleanupDatabase()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
    }

    [Test]
    public async Task Query_returns_results()
    {
        var results = await _connection!.QueryAsync("SELECT * FROM Users");
        await Assert.That(results).IsNotEmpty();
    }
}
```

### Use Parallel Execution Wisely

Group tests that share resources:

```csharp
[ParallelGroup("DatabaseTests")]
public class UserRepositoryTests
{
    // These tests share database resources and run sequentially
}

[ParallelGroup("DatabaseTests")]
public class OrderRepositoryTests
{
    // These also share database resources
}

[ParallelGroup("ApiTests")]
public class ApiIntegrationTests
{
    // These can run in parallel with database tests
}
```

## Common Anti-Patterns to Avoid

### ❌ Don't Use Logic in Tests

```csharp
// ❌ Avoid
[Test]
public async Task Bad_test_with_logic()
{
    var items = GetItems();
    var sum = 0;
    foreach (var item in items)
    {
        sum += item.Value;
    }
    await Assert.That(sum).IsGreaterThan(0);
}

// ✅ Good
[Test]
public async Task Total_of_items_is_greater_than_zero()
{
    var items = GetItems();
    var total = items.Sum(i => i.Value);
    await Assert.That(total).IsGreaterThan(0);
}
```

### ❌ Don't Use Magic Numbers

```csharp
// ❌ Avoid
[Test]
public async Task Bad_test_with_magic_numbers()
{
    var result = calculator.Calculate(42, 7);
    await Assert.That(result).IsEqualTo(49);
}

// ✅ Good
[Test]
public async Task Adding_two_numbers_returns_sum()
{
    const int firstNumber = 42;
    const int secondNumber = 7;
    const int expectedSum = 49;

    var result = calculator.Add(firstNumber, secondNumber);

    await Assert.That(result).IsEqualTo(expectedSum);
}
```

### ❌ Don't Test Implementation Details

```csharp
// ❌ Avoid - Testing private implementation
[Test]
public async Task Bad_test_checking_internals()
{
    var service = new UserService();
    service.ProcessUser(user);
    await Assert.That(service.InternalCache.Count).IsEqualTo(1);
}

// ✅ Good - Testing public behavior
[Test]
public async Task Processing_user_stores_in_cache()
{
    var service = new UserService();
    service.ProcessUser(user);
    var cachedUser = service.GetCachedUser(user.Id);
    await Assert.That(cachedUser).IsEqualTo(user);
}
```

## Summary

Follow these key principles:

1. **Fluent test names** - Use descriptive, readable names with underscores
2. **Clear structure** - Use Arrange-Act-Assert pattern
3. **Single responsibility** - One logical assertion per test
4. **Descriptive code** - No abbreviations or magic numbers
5. **Async throughout** - All tests and assertions are async
6. **Performance awareness** - Share expensive setup when appropriate
