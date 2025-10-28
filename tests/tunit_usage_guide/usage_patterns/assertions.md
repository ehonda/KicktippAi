# Assertions

TUnit uses a fluent assertion syntax through `Assert.That()`. All assertions return `Task` and must be awaited.

## Basic Assertions

### Equality

```csharp
using TUnit.Core;

public class EqualityTests
{
    [Test]
    public async Task Value_equals_expected()
    {
        var result = 42;
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Value_does_not_equal_unexpected()
    {
        var result = 42;
        await Assert.That(result).IsNotEqualTo(0);
    }

    [Test]
    public async Task Objects_are_equal()
    {
        var person1 = new Person { Name = "John", Age = 30 };
        var person2 = new Person { Name = "John", Age = 30 };
        
        await Assert.That(person1).IsEqualTo(person2);  // Uses Equals()
    }

    [Test]
    public async Task Objects_are_same_instance()
    {
        var person = new Person { Name = "John" };
        var same = person;
        
        await Assert.That(person).IsSameAs(same);  // Reference equality
    }
}
```

### Null Checks

```csharp
public class NullTests
{
    [Test]
    public async Task Value_is_null()
    {
        string? result = null;
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Value_is_not_null()
    {
        var result = "Hello";
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Nullable_has_value()
    {
        int? value = 42;
        await Assert.That(value).IsNotNull();
        await Assert.That(value!.Value).IsEqualTo(42);
    }
}
```

### Boolean Assertions

```csharp
public class BooleanTests
{
    [Test]
    public async Task Condition_is_true()
    {
        var isValid = true;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task Condition_is_false()
    {
        var isExpired = false;
        await Assert.That(isExpired).IsFalse();
    }
}
```

## Comparison Assertions

```csharp
public class ComparisonTests
{
    [Test]
    public async Task Value_is_greater_than()
    {
        var score = 85;
        await Assert.That(score).IsGreaterThan(70);
    }

    [Test]
    public async Task Value_is_greater_than_or_equal()
    {
        var score = 80;
        await Assert.That(score).IsGreaterThanOrEqualTo(80);
    }

    [Test]
    public async Task Value_is_less_than()
    {
        var age = 17;
        await Assert.That(age).IsLessThan(18);
    }

    [Test]
    public async Task Value_is_less_than_or_equal()
    {
        var count = 10;
        await Assert.That(count).IsLessThanOrEqualTo(10);
    }

    [Test]
    public async Task Value_is_in_range()
    {
        var temperature = 25;
        await Assert.That(temperature).IsGreaterThan(20);
        await Assert.That(temperature).IsLessThan(30);
    }
}
```

## String Assertions

```csharp
public class StringTests
{
    [Test]
    public async Task String_contains_substring()
    {
        var message = "Hello, World!";
        await Assert.That(message).Contains("World");
    }

    [Test]
    public async Task String_starts_with()
    {
        var filename = "document.pdf";
        await Assert.That(filename).StartsWith("document");
    }

    [Test]
    public async Task String_ends_with()
    {
        var filename = "document.pdf";
        await Assert.That(filename).EndsWith(".pdf");
    }

    [Test]
    public async Task String_is_empty()
    {
        var empty = "";
        await Assert.That(empty).IsEmpty();
    }

    [Test]
    public async Task String_is_not_empty()
    {
        var text = "content";
        await Assert.That(text).IsNotEmpty();
    }

    [Test]
    public async Task String_matches_pattern()
    {
        var email = "test@example.com";
        await Assert.That(email).Contains("@");
        await Assert.That(email).Contains(".");
    }
}
```

## Collection Assertions

### Basic Collection Checks

```csharp
public class CollectionTests
{
    [Test]
    public async Task Collection_is_empty()
    {
        var list = new List<int>();
        await Assert.That(list).IsEmpty();
    }

    [Test]
    public async Task Collection_is_not_empty()
    {
        var list = new List<int> { 1, 2, 3 };
        await Assert.That(list).IsNotEmpty();
    }

    [Test]
    public async Task Collection_has_count()
    {
        var items = new[] { 1, 2, 3, 4, 5 };
        await Assert.That(items.Length).IsEqualTo(5);
    }

    [Test]
    public async Task Collection_contains_item()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        await Assert.That(numbers).Contains(3);
    }

    [Test]
    public async Task Collection_does_not_contain_item()
    {
        var numbers = new[] { 1, 2, 3 };
        await Assert.That(numbers).DoesNotContain(10);
    }
}
```

### Collection Content Verification

```csharp
public class CollectionContentTests
{
    [Test]
    public async Task All_items_meet_condition()
    {
        var numbers = new[] { 2, 4, 6, 8 };
        await Assert.That(numbers).All(n => n % 2 == 0);
    }

    [Test]
    public async Task Any_item_meets_condition()
    {
        var numbers = new[] { 1, 2, 3, 4 };
        await Assert.That(numbers).Any(n => n > 3);
    }

    [Test]
    public async Task No_items_meet_condition()
    {
        var numbers = new[] { 1, 3, 5, 7 };
        await Assert.That(numbers).None(n => n % 2 == 0);
    }

    [Test]
    public async Task Collections_are_equivalent()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new List<int> { 3, 2, 1 };
        
        await Assert.That(actual.OrderBy(x => x)).IsEqualTo(expected);
    }

    [Test]
    public async Task Collections_are_equal_in_order()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new[] { 1, 2, 3 };
        
        await Assert.That(actual).IsEqualTo(expected);
    }
}
```

## Type Assertions

```csharp
public class TypeTests
{
    [Test]
    public async Task Object_is_of_type()
    {
        object obj = "Hello";
        await Assert.That(obj).IsOfType<string>();
    }

    [Test]
    public async Task Object_is_assignable_to_type()
    {
        IEnumerable<int> collection = new List<int> { 1, 2, 3 };
        await Assert.That(collection).IsOfType<List<int>>();
    }

    [Test]
    public async Task Object_is_not_of_type()
    {
        object obj = 42;
        await Assert.That(obj).IsNotOfType<string>();
    }
}
```

## Exception Assertions

### Basic Exception Testing

```csharp
public class ExceptionTests
{
    [Test]
    public async Task Method_throws_exception()
    {
        var calculator = new Calculator();
        
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>();
    }

    [Test]
    public async Task Async_method_throws_exception()
    {
        var service = new ValidationService();
        
        await Assert.That(async () => await service.ValidateAsync(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Method_does_not_throw()
    {
        var calculator = new Calculator();
        
        await Assert.That(() => calculator.Add(1, 2))
            .DoesNotThrow();
    }
}
```

### Exception Details

```csharp
public class ExceptionDetailTests
{
    [Test]
    public async Task Exception_has_expected_message()
    {
        var service = new ValidationService();
        
        var exception = await Assert.That(() => service.Validate(""))
            .Throws<ArgumentException>();
            
        await Assert.That(exception.Message).Contains("cannot be empty");
    }

    [Test]
    public async Task Exception_has_parameter_name()
    {
        var service = new ValidationService();
        
        var exception = await Assert.That(() => service.Validate(null!))
            .Throws<ArgumentNullException>();
            
        await Assert.That(exception.ParamName).IsEqualTo("value");
    }

    [Test]
    public async Task Inner_exception_is_correct_type()
    {
        var service = new DataService();
        
        var exception = await Assert.That(async () => await service.LoadAsync())
            .Throws<InvalidOperationException>();
            
        await Assert.That(exception.InnerException).IsNotNull();
        await Assert.That(exception.InnerException).IsOfType<FileNotFoundException>();
    }
}
```

## Property and Member Assertions

```csharp
public class MemberTests
{
    [Test]
    public async Task Object_has_expected_property_value()
    {
        var person = new Person { Name = "John", Age = 30 };
        
        await Assert.That(person.Name).IsEqualTo("John");
        await Assert.That(person.Age).IsEqualTo(30);
    }

    [Test]
    public async Task Multiple_properties_are_correct()
    {
        var user = new User 
        { 
            Id = 1, 
            Name = "Alice", 
            Email = "alice@example.com",
            IsActive = true
        };
        
        await Assert.That(user.Id).IsEqualTo(1);
        await Assert.That(user.Name).IsEqualTo("Alice");
        await Assert.That(user.Email).Contains("@");
        await Assert.That(user.IsActive).IsTrue();
    }

    [Test]
    public async Task Object_properties_match_expectations()
    {
        var result = new CalculationResult
        {
            Value = 42,
            IsValid = true,
            ErrorMessage = null
        };

        await Assert.That(result.Value).IsGreaterThan(0);
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.ErrorMessage).IsNull();
    }
}
```

## Multiple Assertions

### Sequential Assertions

```csharp
public class MultipleAssertionTests
{
    [Test]
    public async Task Multiple_independent_assertions()
    {
        var user = CreateUser();
        
        // All assertions are checked independently
        await Assert.That(user).IsNotNull();
        await Assert.That(user.Name).IsNotEmpty();
        await Assert.That(user.Email).Contains("@");
        await Assert.That(user.Age).IsGreaterThan(0);
    }

    [Test]
    public async Task Complex_object_validation()
    {
        var order = new Order
        {
            Id = 123,
            Items = new[] { "Item1", "Item2" },
            Total = 99.99m,
            Status = OrderStatus.Pending
        };

        await Assert.That(order.Id).IsGreaterThan(0);
        await Assert.That(order.Items).IsNotEmpty();
        await Assert.That(order.Items.Length).IsEqualTo(2);
        await Assert.That(order.Total).IsGreaterThan(0);
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Pending);
    }
}
```

### Grouped Assertions

```csharp
public class GroupedAssertionTests
{
    [Test]
    public async Task Validate_all_aspects_of_result()
    {
        var result = PerformCalculation();

        // Group related assertions together
        // Value checks
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value).IsGreaterThan(0);
        
        // Metadata checks
        await Assert.That(result.Timestamp).IsNotNull();
        await Assert.That(result.Timestamp).IsLessThanOrEqualTo(DateTime.UtcNow);
        
        // Status checks
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.ErrorMessage).IsNull();
    }
}
```

## Numeric Assertions

```csharp
public class NumericTests
{
    [Test]
    public async Task Integer_arithmetic()
    {
        var result = 10 + 5;
        await Assert.That(result).IsEqualTo(15);
    }

    [Test]
    public async Task Decimal_precision()
    {
        var price = 19.99m;
        await Assert.That(price).IsGreaterThan(19.98m);
        await Assert.That(price).IsLessThan(20.00m);
    }

    [Test]
    public async Task Floating_point_comparison()
    {
        var result = 0.1 + 0.2;
        // Note: Direct equality can be tricky with floating point
        await Assert.That(result).IsGreaterThan(0.299);
        await Assert.That(result).IsLessThan(0.301);
    }

    [Test]
    public async Task Value_is_positive()
    {
        var amount = 100;
        await Assert.That(amount).IsGreaterThan(0);
    }

    [Test]
    public async Task Value_is_negative()
    {
        var deficit = -50;
        await Assert.That(deficit).IsLessThan(0);
    }
}
```

## DateTime Assertions

```csharp
public class DateTimeTests
{
    [Test]
    public async Task Date_is_in_past()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1);
        await Assert.That(pastDate).IsLessThan(DateTime.UtcNow);
    }

    [Test]
    public async Task Date_is_in_future()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        await Assert.That(futureDate).IsGreaterThan(DateTime.UtcNow);
    }

    [Test]
    public async Task Date_is_recent()
    {
        var timestamp = DateTime.UtcNow;
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        
        await Assert.That(timestamp).IsGreaterThan(fiveMinutesAgo);
    }

    [Test]
    public async Task Dates_are_same_day()
    {
        var date1 = new DateTime(2024, 1, 15, 10, 0, 0);
        var date2 = new DateTime(2024, 1, 15, 18, 30, 0);
        
        await Assert.That(date1.Date).IsEqualTo(date2.Date);
    }
}
```

## Custom Assertions

### Domain-Specific Assertions

```csharp
public class CustomAssertionTests
{
    [Test]
    public async Task User_is_valid()
    {
        var user = new User { Name = "John", Email = "john@example.com", Age = 30 };
        
        await AssertUserIsValid(user);
    }

    private static async Task AssertUserIsValid(User user)
    {
        await Assert.That(user).IsNotNull();
        await Assert.That(user.Name).IsNotEmpty();
        await Assert.That(user.Email).Contains("@");
        await Assert.That(user.Age).IsGreaterThan(0);
    }

    [Test]
    public async Task Collection_contains_only_even_numbers()
    {
        var numbers = new[] { 2, 4, 6, 8 };
        
        await AssertAllEven(numbers);
    }

    private static async Task AssertAllEven(IEnumerable<int> numbers)
    {
        await Assert.That(numbers).All(n => n % 2 == 0);
    }
}
```

## Assertion Best Practices

### 1. One Concept Per Test

```csharp
// ✅ Good - Tests one thing
[Test]
public async Task Adding_positive_numbers_returns_sum()
{
    var result = calculator.Add(5, 3);
    await Assert.That(result).IsEqualTo(8);
}

// ❌ Wrong - Tests too many things
[Test]
public async Task Calculator_works()
{
    await Assert.That(calculator.Add(1, 1)).IsEqualTo(2);
    await Assert.That(calculator.Subtract(5, 3)).IsEqualTo(2);
    await Assert.That(calculator.Multiply(2, 3)).IsEqualTo(6);
    await Assert.That(calculator.Divide(10, 2)).IsEqualTo(5);
}
```

### 2. Assert the Most Specific Thing

```csharp
// ✅ Good - Specific assertion
[Test]
public async Task Creating_user_generates_positive_id()
{
    var user = await service.CreateUserAsync(new User { Name = "John" });
    await Assert.That(user.Id).IsGreaterThan(0);
}

// ❌ Less useful - Too general
[Test]
public async Task Creating_user_returns_user()
{
    var user = await service.CreateUserAsync(new User { Name = "John" });
    await Assert.That(user).IsNotNull();
}
```

### 3. Use Meaningful Test Data

```csharp
// ✅ Good - Meaningful values
[Test]
public async Task Email_validation_accepts_valid_format()
{
    var isValid = validator.IsValidEmail("user@example.com");
    await Assert.That(isValid).IsTrue();
}

// ❌ Less clear - Magic values
[Test]
public async Task Email_validation_works()
{
    var isValid = validator.IsValidEmail("a@b.c");
    await Assert.That(isValid).IsTrue();
}
```

### 4. Always Await Assertions

```csharp
// ✅ Good - Properly awaited
[Test]
public async Task Proper_assertion()
{
    var result = 42;
    await Assert.That(result).IsEqualTo(42);
}

// ❌ Wrong - Not awaited
[Test]
public async Task Missing_await()
{
    var result = 42;
    Assert.That(result).IsEqualTo(42);  // Compiler warning, assertion not executed!
}
```

## See Also

- [Basic Tests](basic_tests.md) - Test fundamentals
- [Async Testing](async_testing.md) - Async patterns
- [Parameterized Tests](parameterized_tests.md) - Data-driven assertions
