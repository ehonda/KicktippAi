# TUnit Usage Guide

Welcome to the TUnit usage guide for this project. This guide is structured to provide progressively discoverable information - start with the overview and drill down into specific topics as needed.

## Purpose

This guide helps you write effective unit tests for this project using TUnit, a modern, fast, and flexible .NET testing framework with async-first design and source generator-based performance optimizations.

## Document Structure

This guide is organized hierarchically to facilitate finding information at the appropriate level of detail:

### General

Foundational information for getting started and following project conventions:

- **[Quickstart](general/quickstart.md)** - Essential information to start writing tests immediately
- **[Project Style Guide](general/project_style_guide.md)** - Conventions and patterns specific to this project

### Usage Patterns

Detailed guides for common testing scenarios:

- **[Basic Tests](usage_patterns/basic_tests.md)** - Writing simple test methods
- **[Parameterized Tests](usage_patterns/parameterized_tests.md)** - Data-driven testing with multiple inputs
- **[Setup and Teardown](usage_patterns/setup_teardown.md)** - Test lifecycle management
- **[Async Testing](usage_patterns/async_testing.md)** - Testing asynchronous code
- **[Assertions](usage_patterns/assertions.md)** - Using TUnit's fluent assertion library
- **[Shared Context](usage_patterns/shared_context.md)** - Sharing expensive setup across tests
- **[Test Organization](usage_patterns/test_organization.md)** - Categorization and filtering
- **[Test Dependencies](usage_patterns/test_dependencies.md)** - Ordering and dependencies between tests
- **[Retries](usage_patterns/retries.md)** - Handling flaky tests
- **[Output and Diagnostics](usage_patterns/output_diagnostics.md)** - Writing test output and debugging

## Key Features

TUnit provides several unique features that make it particularly suited for modern .NET development:

1. **Async-First Design** - All tests and assertions are async by default
2. **Source Generator-Based** - Compile-time test discovery for better performance
3. **Fluent Assertions** - Readable, chainable assertion syntax
4. **Built-in Orchestration** - Test dependencies, retries, and parallel execution control
5. **Modern .NET Support** - Built for .NET 6+ with full async/await support

## Quick Example

```csharp
using TUnit.Core;

public class CalculatorTests
{
    [Test]
    public async Task Adding_two_positive_numbers_returns_their_sum()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(2, 3);

        // Assert
        await Assert.That(result).IsEqualTo(5);
    }
}
```

## Getting Help

- Check the [Quickstart](general/quickstart.md) for immediate answers to common questions
- Browse [Usage Patterns](usage_patterns/) for detailed examples of specific scenarios
- Refer to the [Project Style Guide](general/project_style_guide.md) for conventions

## Next Steps

1. **New to TUnit?** Start with the [Quickstart](general/quickstart.md)
2. **Writing tests?** Reference the [Usage Patterns](usage_patterns/) for your specific scenario
3. **Reviewing code?** Ensure adherence to the [Project Style Guide](general/project_style_guide.md)
