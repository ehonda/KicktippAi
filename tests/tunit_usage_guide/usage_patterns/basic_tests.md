# Basic Tests

This guide covers writing simple, fundamental test cases in TUnit.

## Test Method Structure

Every test follows the same basic structure:

```csharp
using TUnit.Core;

public class MyTests
{
    [Test]
    public async Task Test_name_describing_behavior()
    {
        // Arrange - Set up test data
        var sut = new SystemUnderTest();

        // Act - Execute the behavior
        var result = sut.DoSomething();

        // Assert - Verify the outcome
        await Assert.That(result).IsEqualTo(expectedValue);
    }
}
```

## Essential Elements

### 1. The `[Test]` Attribute

All test methods must be marked with `[Test]`:

```csharp
[Test]  // Required for test discovery
public async Task My_test()
{
    await Assert.That(true).IsTrue();
}
```

### 2. Async Return Type

All test methods must return `Task`:

```csharp
// ✅ Correct
[Test]
public async Task Valid_test()
{
    await Assert.That(42).IsEqualTo(42);
}

// ❌ Wrong - missing async/Task
[Test]
public void Invalid_test()  // Will not compile or run correctly
{
    Assert.That(42).IsEqualTo(42);  // Missing await
}
```

### 3. Awaiting Assertions

All assertions must be awaited:

```csharp
[Test]
public async Task Test_with_assertions()
{
    var value = 42;

    // ✅ Correct - awaited
    await Assert.That(value).IsEqualTo(42);

    // ❌ Wrong - not awaited (test may not fail when it should)
    Assert.That(value).IsEqualTo(42);
}
```

## Complete Examples

### Testing a Simple Method

```csharp
using TUnit.Core;

public class CalculatorTests
{
    [Test]
    public async Task Adding_two_numbers_returns_sum()
    {
        // Arrange
        var calculator = new Calculator();
        var a = 2;
        var b = 3;

        // Act
        var result = calculator.Add(a, b);

        // Assert
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    public async Task Multiplying_by_zero_returns_zero()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Multiply(42, 0);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Dividing_by_zero_throws_exception()
    {
        // Arrange
        var calculator = new Calculator();

        // Act & Assert
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>();
    }
}
```

### Testing a Service

```csharp
public class UserServiceTests
{
    [Test]
    public async Task Creating_user_returns_user_with_id()
    {
        // Arrange
        var service = new UserService();
        var userName = "John Doe";
        var email = "john@example.com";

        // Act
        var user = await service.CreateUserAsync(userName, email);

        // Assert
        await Assert.That(user).IsNotNull();
        await Assert.That(user.Id).IsGreaterThan(0);
        await Assert.That(user.Name).IsEqualTo(userName);
        await Assert.That(user.Email).IsEqualTo(email);
    }

    [Test]
    public async Task Finding_nonexistent_user_returns_null()
    {
        // Arrange
        var service = new UserService();
        var nonexistentId = 99999;

        // Act
        var user = await service.FindUserAsync(nonexistentId);

        // Assert
        await Assert.That(user).IsNull();
    }
}
```

### Testing Validation Logic

```csharp
public class EmailValidatorTests
{
    [Test]
    public async Task Valid_email_passes_validation()
    {
        // Arrange
        var validator = new EmailValidator();
        var validEmail = "user@example.com";

        // Act
        var isValid = validator.IsValid(validEmail);

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task Email_without_at_symbol_fails_validation()
    {
        // Arrange
        var validator = new EmailValidator();
        var invalidEmail = "userexample.com";

        // Act
        var isValid = validator.IsValid(invalidEmail);

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Empty_email_fails_validation()
    {
        // Arrange
        var validator = new EmailValidator();

        // Act
        var isValid = validator.IsValid(string.Empty);

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Null_email_throws_ArgumentNullException()
    {
        // Arrange
        var validator = new EmailValidator();

        // Act & Assert
        await Assert.That(() => validator.IsValid(null!))
            .Throws<ArgumentNullException>();
    }
}
```

## Test Class Organization

### Grouping Related Tests

```csharp
public class StringExtensionsTests
{
    // Group 1: Testing IsNullOrWhiteSpace extension
    [Test]
    public async Task Null_string_is_considered_null_or_whitespace()
    {
        string? value = null;
        await Assert.That(value.IsNullOrWhiteSpace()).IsTrue();
    }

    [Test]
    public async Task Empty_string_is_considered_null_or_whitespace()
    {
        var value = "";
        await Assert.That(value.IsNullOrWhiteSpace()).IsTrue();
    }

    [Test]
    public async Task Whitespace_string_is_considered_null_or_whitespace()
    {
        var value = "   ";
        await Assert.That(value.IsNullOrWhiteSpace()).IsTrue();
    }

    // Group 2: Testing Truncate extension
    [Test]
    public async Task Truncating_short_string_returns_original()
    {
        var value = "Hello";
        var result = value.Truncate(10);
        await Assert.That(result).IsEqualTo("Hello");
    }

    [Test]
    public async Task Truncating_long_string_returns_truncated_with_ellipsis()
    {
        var value = "Hello, World!";
        var result = value.Truncate(5);
        await Assert.That(result).IsEqualTo("Hello...");
    }
}
```

### Testing Private Logic Through Public Interface

```csharp
public class OrderProcessorTests
{
    // Don't test private methods directly
    // Test them through the public interface

    [Test]
    public async Task Processing_order_with_discount_code_applies_discount()
    {
        // This tests the private ApplyDiscount method indirectly
        var processor = new OrderProcessor();
        var order = new Order { Total = 100, DiscountCode = "SAVE10" };

        var result = await processor.ProcessAsync(order);

        await Assert.That(result.FinalTotal).IsEqualTo(90);
    }

    [Test]
    public async Task Processing_order_with_invalid_discount_code_uses_original_total()
    {
        // Also tests private discount logic
        var processor = new OrderProcessor();
        var order = new Order { Total = 100, DiscountCode = "INVALID" };

        var result = await processor.ProcessAsync(order);

        await Assert.That(result.FinalTotal).IsEqualTo(100);
    }
}
```

## Common Patterns

### Testing State Changes

```csharp
[Test]
public async Task Adding_item_to_cart_increases_item_count()
{
    // Arrange
    var cart = new ShoppingCart();
    var initialCount = cart.ItemCount;
    var item = new CartItem { ProductId = 1, Quantity = 1 };

    // Act
    cart.AddItem(item);

    // Assert
    await Assert.That(cart.ItemCount).IsEqualTo(initialCount + 1);
}

[Test]
public async Task Removing_item_from_cart_decreases_item_count()
{
    // Arrange
    var cart = new ShoppingCart();
    var item = new CartItem { ProductId = 1, Quantity = 1 };
    cart.AddItem(item);
    var countBeforeRemoval = cart.ItemCount;

    // Act
    cart.RemoveItem(item.ProductId);

    // Assert
    await Assert.That(cart.ItemCount).IsEqualTo(countBeforeRemoval - 1);
}
```

### Testing Boolean Conditions

```csharp
[Test]
public async Task User_is_eligible_when_age_is_18_or_above()
{
    var user = new User { Age = 18 };
    await Assert.That(user.IsEligible()).IsTrue();
}

[Test]
public async Task User_is_not_eligible_when_age_is_below_18()
{
    var user = new User { Age = 17 };
    await Assert.That(user.IsEligible()).IsFalse();
}
```

### Testing Collections

```csharp
[Test]
public async Task New_list_is_empty()
{
    var list = new List<string>();
    await Assert.That(list).IsEmpty();
}

[Test]
public async Task Adding_item_to_list_makes_it_non_empty()
{
    var list = new List<string>();
    list.Add("item");
    await Assert.That(list).IsNotEmpty();
}

[Test]
public async Task List_contains_added_item()
{
    var list = new List<string>();
    var item = "test item";
    list.Add(item);
    await Assert.That(list).Contains(item);
}
```

## Best Practices

### 1. Use Descriptive Variable Names

```csharp
// ✅ Good
[Test]
public async Task Descriptive_names()
{
    var expectedTotal = 100;
    var actualTotal = calculator.CalculateTotal(items);
    await Assert.That(actualTotal).IsEqualTo(expectedTotal);
}

// ❌ Avoid
[Test]
public async Task Short_names()
{
    var exp = 100;
    var act = calc.CalcTotal(itms);
    await Assert.That(act).IsEqualTo(exp);
}
```

### 2. Keep Tests Simple and Focused

```csharp
// ✅ Good - One responsibility
[Test]
public async Task Saving_user_stores_in_database()
{
    await service.SaveUserAsync(user);
    var saved = await repository.GetByIdAsync(user.Id);
    await Assert.That(saved).IsNotNull();
}

// ❌ Avoid - Multiple responsibilities
[Test]
public async Task Complex_test_doing_too_much()
{
    await service.SaveUserAsync(user);
    await service.SendWelcomeEmail(user);
    await service.UpdateStatistics();
    // Testing too many things at once
}
```

### 3. Avoid Test Interdependence

```csharp
// ✅ Good - Independent tests
[Test]
public async Task Test_A()
{
    var service = new MyService();  // Fresh instance
    var result = await service.DoSomethingAsync();
    await Assert.That(result).IsTrue();
}

[Test]
public async Task Test_B()
{
    var service = new MyService();  // Fresh instance
    var result = await service.DoSomethingElseAsync();
    await Assert.That(result).IsFalse();
}

// ❌ Avoid - Dependent tests (Test_B depends on Test_A running first)
private static MyService _sharedService = new();

[Test]
public async Task Test_A()
{
    await _sharedService.InitializeAsync();
    await Assert.That(_sharedService.IsInitialized).IsTrue();
}

[Test]
public async Task Test_B()  // Breaks if Test_A doesn't run first
{
    await _sharedService.DoWork();  // Depends on Test_A's initialization
    await Assert.That(_sharedService.WorkComplete).IsTrue();
}
```

## See Also

- [Parameterized Tests](parameterized_tests.md) - For testing with multiple inputs
- [Assertions](assertions.md) - Complete assertion reference
- [Async Testing](async_testing.md) - Testing asynchronous code
- [Setup and Teardown](setup_teardown.md) - Test lifecycle management
