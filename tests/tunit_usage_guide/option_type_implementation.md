# Option Type Implementation Using OneOf

## Use Case

We need an Option type for test helper methods to distinguish between "parameter not provided" and "explicitly passing null". This allows us to write flexible test helpers where callers can:

1. **Omit a parameter** - Use default value/behavior
2. **Pass a value** - Use the provided value
3. **Explicitly pass null** - Assert that something should be null

### Problem with Nullable Arguments

The traditional nullable approach fails when we need to pass null explicitly:

```csharp
Service CreateService(IDependency? dep = null) 
    => new(dep ?? CreateDefaultDependency());

// These work:
var serviceA = CreateService();                           // Gets default
var serviceB = CreateService(CreateDependency("param")); // Gets custom

// This FAILS - cannot distinguish null from "not provided":
var serviceC = CreateService(null); // Still gets default, but we wanted null!
```

### Solution with Option Type

With an Option type, all cases work correctly:

```csharp
Service CreateService(Option<IDependency?> dep) 
    => dep.Match(
        value => new Service(value),           // Some: use the value (even if null)
        none => new Service(CreateDefaultDependency()) // None: use default
    );

// All use cases work:
var serviceA = CreateService(default);              // None: gets default
var serviceB = CreateService(CreateDependency());   // Some: gets custom
var serviceC = CreateService((IDependency?)null);   // Some(null): gets null!
```

## Why OneOf?

We evaluated several options and chose **OneOf** (https://github.com/mcintyre321/OneOf) because it meets all our requirements:

### ✅ Actively Maintained
- Last commit: May 2024 (recent activity)
- 3,937 GitHub stars
- Active community and contributors
- Responsive to issues and PRs

### ✅ Minimal & Focused
- Small package size (~40KB)
- Zero dependencies
- Focused on discriminated unions, not a full FP framework
- Provides exactly what we need: `OneOf<T, None>` as an Option type

### ✅ Modern .NET Patterns
- Uses modern C# features (readonly structs, implicit conversions)
- Source generator support via `OneOf.SourceGenerator` (optional)
- Compile-time exhaustive matching
- Compatible with latest .NET versions

### ✅ Built-in Option Support
- Includes `None` type in `OneOf.Types` namespace
- Provides `OneOf<T, None>` as a natural Option type
- Clean, ergonomic API with `.Match()` and `.Switch()` methods

### Alternatives Considered

- **Optional (nlkl/Optional)**: Last updated 2018 - not actively maintained ❌
- **LanguageExt**: Too comprehensive (2.14MB), full FP framework - overkill ❌
- **Custom implementation**: Reinventing the wheel, no community testing ❌
- **ActualLab.Fusion**: Good Option implementation, but part of larger framework ❌

## Implementation Plan

### Next Steps

1. **Add NuGet Package**
   - Add `OneOf` package to `src/TestUtilities/TestUtilities.csproj`
   - Version: Latest stable version from NuGet

2. **Create Helper Infrastructure** (if needed)
   - Namespace: `TestUtilities.Options`
   - Add extension methods for common patterns (e.g., `GetValueOr`, `GetValueOrDefault`)
   - Only add helpers if OneOf's built-in API is insufficient for test helpers

3. **Documentation**
   - Update `src/TestUtilities/README.md` with Option usage examples
   - Add section to `tests/tunit_usage_guide/general/project_style_guide.md`
   - Include concrete test helper examples

4. **Migration Strategy**
   - Update existing test helpers with nullable parameters to use `Option<T>`
   - Gradually migrate as helpers are modified (not all at once)

### Design Decisions

#### Type Alias

~~We'll use a global type alias for convenience:~~

```csharp
global using Option<T> = OneOf<T, None>;
```

**Update**: This may not be possible in C# because type aliases cannot have open generic parameters. We'll need to verify this and either:
- Use `OneOf<T, None>` directly throughout the codebase, or
- Create wrapper types/extension methods if needed

**Action Required**: Test if generic type aliases work, and document the chosen approach.

#### Namespace for Helpers

Extensions and helper methods will live in `TestUtilities.Options` namespace:

```csharp
namespace TestUtilities.Options;

public static class OptionExtensions
{
    public static T GetValueOr<T>(this OneOf<T, None> option, T defaultValue)
        => option.Match(
            value => value,
            none => defaultValue
        );
        
    public static T? GetValueOrDefault<T>(this OneOf<T, None> option)
        => option.Match(
            value => value,
            none => default(T)
        );
}
```

#### Factory Methods

We may not need factory methods - OneOf provides:
- Implicit conversions: `OneOf<T, None> option = value;` automatically creates `Some`
- Explicit None: `new None()` or `default(OneOf<T, None>)` creates `None`

**Decision**: Start without factory methods and add only if the ergonomics are poor in practice.

## Example Usage

### Basic Test Helper

```csharp
using OneOf;
using OneOf.Types;
using TestUtilities.Options;

public static class TestHelpers
{
    public static MyService CreateService(
        OneOf<ILogger?, None> logger = default,
        OneOf<IRepository?, None> repository = default)
    {
        var actualLogger = logger.GetValueOr(CreateDefaultLogger());
        var actualRepository = repository.GetValueOr(CreateDefaultRepository());
        return new MyService(actualLogger, actualRepository);
    }
}
```

### In Tests

```csharp
[Test]
public async Task Test_with_default_dependencies()
{
    // None for both - uses defaults
    var service = CreateService();
    
    // Act & Assert
    var result = service.DoSomething();
    await Assert.That(result).IsNotNull();
}

[Test]
public async Task Test_with_custom_logger()
{
    // Custom logger, default repository
    var logger = new FakeLogger<MyService>();
    var service = CreateService(logger: logger);
    
    service.DoSomething();
    logger.AssertLogContains(LogLevel.Information, "Expected");
}

[Test]
public async Task Test_with_null_logger()
{
    // Explicitly pass null logger
    var service = CreateService(logger: (ILogger?)null);
    
    // Now service actually has null logger
    var result = service.DoSomething();
    await Assert.That(result).IsNotNull();
}
```

## Open Questions

1. **Generic Type Alias**: Can we use `global using Option<T> = OneOf<T, None>;`? Needs verification.
2. **Extension Methods**: Do we need `GetValueOr` extensions, or is `.Match()` ergonomic enough?
3. **Factory Methods**: Should we provide `Option.Some<T>()` and `Option.None<T>()` static helpers?
4. **Documentation Location**: Should Option examples be in the main style guide or a separate document?

## References

- **OneOf GitHub**: https://github.com/mcintyre321/OneOf
- **OneOf NuGet**: https://www.nuget.org/packages/OneOf/
- **Related Discussion**: Initial research in development session
