# Test Organization and Categorization

Organizing tests with categories, properties, and naming conventions.

## Categories

Group related tests using the `[Category]` attribute:

```csharp
using TUnit.Core;

[Category("Unit")]
public class CalculatorTests
{
    [Test]
    public async Task Adding_numbers_returns_sum()
    {
        var calc = new Calculator();
        var result = calc.Add(2, 3);
        await Assert.That(result).IsEqualTo(5);
    }
}

[Category("Integration")]
public class DatabaseTests
{
    [Test]
    public async Task Can_save_to_database()
    {
        var repository = new UserRepository();
        var user = await repository.CreateAsync(new User { Name = "John" });
        await Assert.That(user.Id).IsGreaterThan(0);
    }
}

[Category("E2E")]
public class CheckoutFlowTests
{
    [Test]
    public async Task Complete_checkout_process_succeeds()
    {
        // End-to-end test
        await Assert.That(true).IsTrue();
    }
}
```

## Multiple Categories

Apply multiple categories to a single test:

```csharp
[Category("Integration")]
[Category("Database")]
[Category("SlowTests")]
public class ComplexDatabaseTests
{
    [Test]
    public async Task Complex_query_returns_results()
    {
        await Assert.That(true).IsTrue();
    }
}

// Can also apply to individual tests
public class MixedTests
{
    [Test]
    [Category("Fast")]
    [Category("Unit")]
    public async Task Quick_unit_test()
    {
        await Assert.That(1 + 1).IsEqualTo(2);
    }

    [Test]
    [Category("Slow")]
    [Category("Integration")]
    public async Task Slower_integration_test()
    {
        await Task.Delay(100);
        await Assert.That(true).IsTrue();
    }
}
```

## Common Category Patterns

### Test Type Categories

```csharp
[Category("Unit")]
public class UnitTests { }

[Category("Integration")]
public class IntegrationTests { }

[Category("E2E")]
public class EndToEndTests { }

[Category("Smoke")]
public class SmokeTests { }
```

### Performance Categories

```csharp
[Category("Fast")]  // < 100ms
public class FastTests { }

[Category("Medium")]  // 100ms - 1s
public class MediumTests { }

[Category("Slow")]  // > 1s
public class SlowTests { }
```

### Feature Categories

```csharp
[Category("Auth")]
public class AuthenticationTests { }

[Category("Payment")]
public class PaymentTests { }

[Category("Reporting")]
public class ReportingTests { }
```

### Environment Categories

```csharp
[Category("RequiresDatabase")]
public class DatabaseDependentTests { }

[Category("RequiresExternalService")]
public class ExternalServiceTests { }

[Category("Offline")]
public class OfflineTests { }
```

## Properties

Add custom metadata to tests:

```csharp
using TUnit.Core;

public class PropertyTests
{
    [Test]
    [Property("Priority", "High")]
    [Property("Owner", "TeamA")]
    public async Task Critical_functionality_works()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [Property("Priority", "Low")]
    [Property("Owner", "TeamB")]
    [Property("Bug", "12345")]
    public async Task Regression_test_for_bug()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [Property("Browser", "Chrome")]
    [Property("Browser", "Firefox")]
    public async Task Cross_browser_test()
    {
        await Assert.That(true).IsTrue();
    }
}
```

## Display Names

Customize test names in test output:

```csharp
public class DisplayNameTests
{
    [Test]
    [DisplayName("Verify that adding two positive numbers produces the correct sum")]
    public async Task Adding_positive_numbers_returns_sum()
    {
        var result = 2 + 3;
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    [DisplayName("User registration: Email validation prevents invalid formats")]
    public async Task Email_validation_rejects_invalid_format()
    {
        var validator = new EmailValidator();
        var isValid = validator.Validate("invalid-email");
        await Assert.That(isValid).IsFalse();
    }
}
```

## Organizing Test Files

### By Feature

```
Tests/
  Features/
    Authentication/
      LoginTests.cs
      RegistrationTests.cs
      PasswordResetTests.cs
    Payment/
      CheckoutTests.cs
      RefundTests.cs
    Reporting/
      SalesReportTests.cs
      UserReportTests.cs
```

### By Layer

```
Tests/
  Unit/
    Services/
      UserServiceTests.cs
      ProductServiceTests.cs
    Validators/
      EmailValidatorTests.cs
  Integration/
    Database/
      UserRepositoryTests.cs
    Api/
      UserApiTests.cs
  E2E/
    Workflows/
      CheckoutFlowTests.cs
```

### By Component

```
Tests/
  UserManagement/
    UserServiceTests.cs          [Category("Unit")]
    UserRepositoryTests.cs       [Category("Integration")]
    UserApiTests.cs              [Category("Integration")]
    UserRegistrationFlowTests.cs [Category("E2E")]
```

## Nested Test Classes

Group related tests within a class:

```csharp
public class CalculatorTests
{
    public class AdditionTests
    {
        [Test]
        public async Task Adding_positive_numbers_returns_sum()
        {
            var calc = new Calculator();
            var result = calc.Add(2, 3);
            await Assert.That(result).IsEqualTo(5);
        }

        [Test]
        public async Task Adding_negative_numbers_returns_sum()
        {
            var calc = new Calculator();
            var result = calc.Add(-2, -3);
            await Assert.That(result).IsEqualTo(-5);
        }
    }

    public class SubtractionTests
    {
        [Test]
        public async Task Subtracting_numbers_returns_difference()
        {
            var calc = new Calculator();
            var result = calc.Subtract(5, 3);
            await Assert.That(result).IsEqualTo(2);
        }
    }

    public class DivisionTests
    {
        [Test]
        [Category("EdgeCase")]
        public async Task Dividing_by_zero_throws_exception()
        {
            var calc = new Calculator();
            await Assert.That(() => calc.Divide(10, 0))
                .Throws<DivideByZeroException>();
        }
    }
}
```

## Test Suites

Create logical test suites:

```csharp
// All smoke tests
[Category("Smoke")]
public class SmokeTestSuite
{
    [Test]
    public async Task Application_starts() { }

    [Test]
    public async Task Database_is_accessible() { }

    [Test]
    public async Task Api_responds() { }
}

// Critical path tests
[Category("CriticalPath")]
[Category("E2E")]
public class CriticalPathTests
{
    [Test]
    public async Task User_can_register() { }

    [Test]
    public async Task User_can_login() { }

    [Test]
    public async Task User_can_checkout() { }
}

// Nightly tests
[Category("Nightly")]
[Category("Slow")]
public class NightlyTestSuite
{
    [Test]
    public async Task Full_data_migration() { }

    [Test]
    public async Task Performance_benchmarks() { }

    [Test]
    public async Task Security_scan() { }
}
```

## Filtering Tests

Tests can be filtered by category or property (runner-dependent):

```bash
# Run only unit tests
dotnet test --filter "Category=Unit"

# Run fast tests
dotnet test --filter "Category=Fast"

# Run specific feature
dotnet test --filter "Category=Payment"

# Exclude slow tests
dotnet test --filter "Category!=Slow"

# Combine filters
dotnet test --filter "(Category=Unit|Category=Integration)&Category!=Slow"
```

## Test Naming Conventions

### Fluent Style (Project Standard)

```csharp
public class UserServiceTests
{
    [Test]
    public async Task Creating_user_with_valid_data_returns_user_with_id()
    {
        // Test implementation
    }

    [Test]
    public async Task Creating_user_with_null_name_throws_ArgumentNullException()
    {
        // Test implementation
    }

    [Test]
    public async Task Updating_nonexistent_user_throws_NotFoundException()
    {
        // Test implementation
    }
}
```

### Class Naming

```csharp
// Service tests
public class UserServiceTests { }
public class PaymentServiceTests { }

// Repository tests
public class UserRepositoryTests { }
public class OrderRepositoryTests { }

// Validator tests
public class EmailValidatorTests { }
public class PasswordValidatorTests { }

// API tests
public class UserApiTests { }
public class ProductApiTests { }

// Integration tests
public class DatabaseIntegrationTests { }
public class ExternalApiIntegrationTests { }

// E2E tests
public class CheckoutFlowTests { }
public class UserRegistrationFlowTests { }
```

## Documentation

Add XML documentation to test classes and methods:

```csharp
/// <summary>
/// Tests for the UserService class, covering user creation, updates, and deletions.
/// </summary>
[Category("Unit")]
[Category("UserManagement")]
public class UserServiceTests
{
    /// <summary>
    /// Verifies that creating a user with valid data stores the user in the repository
    /// and returns a user object with a positive ID.
    /// </summary>
    [Test]
    [Property("Priority", "High")]
    public async Task Creating_user_with_valid_data_returns_user_with_id()
    {
        // Arrange
        var service = new UserService();
        var userData = new User { Name = "John", Email = "john@example.com" };

        // Act
        var created = await service.CreateAsync(userData);

        // Assert
        await Assert.That(created.Id).IsGreaterThan(0);
    }
}
```

## Test Data Organization

### Shared Test Data

```csharp
public class TestData
{
    public static class Users
    {
        public static User ValidUser => new() 
        { 
            Name = "John Doe", 
            Email = "john@example.com" 
        };

        public static User InvalidUser => new() 
        { 
            Name = "", 
            Email = "invalid" 
        };
    }

    public static class Products
    {
        public static Product Widget => new() 
        { 
            Name = "Widget", 
            Price = 9.99m 
        };
    }
}

public class UserTests
{
    [Test]
    public async Task Creating_valid_user_succeeds()
    {
        var service = new UserService();
        var user = await service.CreateAsync(TestData.Users.ValidUser);
        await Assert.That(user.Id).IsGreaterThan(0);
    }
}
```

## Real-World Example

```csharp
/// <summary>
/// Integration tests for the payment processing subsystem.
/// Requires a test payment gateway configuration.
/// </summary>
[Category("Integration")]
[Category("Payment")]
[Category("RequiresExternalService")]
public class PaymentProcessingTests
{
    /// <summary>
    /// Verifies successful payment processing for a standard checkout scenario.
    /// </summary>
    [Test]
    [Property("Priority", "Critical")]
    [Property("Owner", "PaymentTeam")]
    [DisplayName("Standard Checkout: Payment processes successfully with valid card")]
    public async Task Processing_payment_with_valid_card_succeeds()
    {
        // Arrange
        var gateway = new PaymentGateway();
        var payment = new PaymentRequest
        {
            Amount = 99.99m,
            CardNumber = "4111111111111111",
            Cvv = "123"
        };

        // Act
        var result = await gateway.ProcessAsync(payment);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.TransactionId).IsNotEmpty();
    }

    /// <summary>
    /// Verifies that invalid card numbers are rejected with appropriate error.
    /// </summary>
    [Test]
    [Property("Priority", "High")]
    [Property("Owner", "PaymentTeam")]
    [DisplayName("Payment Validation: Invalid card number is rejected")]
    [Category("Validation")]
    public async Task Processing_payment_with_invalid_card_fails()
    {
        // Arrange
        var gateway = new PaymentGateway();
        var payment = new PaymentRequest
        {
            Amount = 99.99m,
            CardNumber = "0000000000000000",
            Cvv = "123"
        };

        // Act & Assert
        await Assert.That(async () => await gateway.ProcessAsync(payment))
            .Throws<InvalidCardException>();
    }
}
```

## Best Practices

### 1. Use Meaningful Categories

```csharp
// ✅ Good - Clear, useful categories
[Category("Integration")]
[Category("Database")]
[Category("RequiresPostgres")]

// ❌ Wrong - Too vague
[Category("Tests")]
[Category("Important")]
```

### 2. Consistent Naming

```csharp
// ✅ Good - Consistent pattern
public async Task Creating_user_with_valid_data_succeeds() { }
public async Task Creating_user_with_null_name_throws() { }
public async Task Creating_user_with_duplicate_email_throws() { }

// ❌ Wrong - Inconsistent
public async Task TestUserCreation() { }
public async Task User_Create_Null_Name() { }
public async Task duplicate_email_test() { }
```

### 3. Hierarchical Organization

```csharp
// ✅ Good - Clear hierarchy
public class UserServiceTests
{
    public class CreationTests { }
    public class UpdateTests { }
    public class DeletionTests { }
}

// ❌ Wrong - Flat structure for complex tests
public class UserTests { } // 100 tests in one class
```

### 4. Document Complex Tests

```csharp
// ✅ Good - Complex scenarios documented
/// <summary>
/// Tests the race condition handling when two users attempt to
/// purchase the last item concurrently.
/// </summary>
[Test]
[Property("Complexity", "High")]
public async Task Concurrent_purchase_of_last_item_handles_race_condition() { }

// ❌ Wrong - No context for complex scenario
[Test]
public async Task Test_concurrent_purchase() { }
```

## See Also

- [Basic Tests](basic_tests.md) - Test fundamentals
- [Setup and Teardown](setup_teardown.md) - Test lifecycle
- [Project Style Guide](../general/project_style_guide.md) - Naming conventions
