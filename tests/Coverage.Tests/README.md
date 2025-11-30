# Coverage.Tests

**⚠️ TEMPORARY PROJECT** - This project will be removed once all `src/` assemblies have dedicated test projects.

## Purpose

This project exists solely to ensure all `src/` assemblies appear in code coverage reports, even those that don't yet have dedicated test projects. Without this, `dotnet-coverage` only instruments assemblies that are actually loaded during test execution, causing untested assemblies to be **missing** from reports rather than showing as **0% covered**.

## The Problem

`dotnet-coverage` uses runtime instrumentation - it only reports on assemblies that are loaded during test execution. If an assembly has no tests, it's never loaded, and therefore:

- ❌ It doesn't appear in the coverage report at all
- ❌ We have no visibility into which assemblies lack coverage
- ❌ The "total coverage" percentage is artificially inflated

## The Solution

We force assembly loading by referencing a type from each untested assembly:

```csharp
public static readonly Type[] LoadedAssemblyTypes =
[
    typeof(KicktippContextProvider),     // ContextProviders.Kicktipp.dll
    typeof(FirebaseContextRepository),   // FirebaseAdapter.dll
    typeof(IKicktippClient),             // KicktippIntegration.dll
    typeof(Program)                      // Orchestrator.dll
];
```

### Why `typeof(T)` over alternatives?

| Approach | Pros | Cons |
|----------|------|------|
| **`typeof(T)`** ✅ | Simple, compile-time checked, sufficient for instrumentation | None significant |
| `RuntimeHelpers.RunClassConstructor` | Forces static constructor execution | Overkill for coverage purposes |
| `Assembly.Load("name")` | Explicit loading | String-based, not compile-time checked |

## Current Status

### Assemblies WITHOUT dedicated test projects (referenced here):

| Assembly | Type Referenced | Status |
|----------|----------------|--------|
| ContextProviders.Kicktipp | `KicktippContextProvider` | ⏳ Needs tests |
| FirebaseAdapter | `FirebaseContextRepository` | ⏳ Needs tests |
| KicktippIntegration | `IKicktippClient` | ⏳ Needs tests |
| Orchestrator | `Program` | ⏳ Needs tests |

### Assemblies WITH dedicated test projects (NOT referenced here):

| Assembly | Test Project |
|----------|--------------|
| Core | Tested transitively via OpenAiIntegration.Tests |
| OpenAiIntegration | OpenAiIntegration.Tests |
| TestUtilities | TestUtilities.Tests |

## Removal Criteria

1. When a dedicated test project is created for an assembly, remove its type reference from `AssemblyLoader.cs` and its project reference from `Coverage.Tests.csproj`.

2. When all `src/` assemblies have dedicated test projects, delete this entire project.

## Files

- **`AssemblyLoader.cs`** - Static class that references types from untested assemblies
- **`AssemblyLoaderTests.cs`** - Test that verifies assemblies are loaded (also triggers the loading)
- **`Coverage.Tests.csproj`** - Project file with references to untested assemblies
