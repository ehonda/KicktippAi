# Project Style Guide

This document defines conventions and best practices for writing production code in this project.

## Quick Reference

- **Error Handling**: Fail fast—throw exceptions instead of using fallbacks
- **Solution-Relative Files**: Use `SolutionRelativeFileProvider` pattern for file access

## Error Handling

### Fail Fast Principle

**Always fail fast** instead of providing fallbacks or default values when encountering unexpected conditions. This approach:

1. **Surfaces bugs early** rather than masking them with questionable defaults
2. **Makes debugging easier** by failing close to the source of the problem
3. **Prevents data corruption** by not proceeding with invalid state

**Note:** We generally trust nullability annotations in APIs. Only add null checks when the API genuinely returns `null` to signal an error or missing data (e.g., a client returning `null` on failure).

**✅ Do:** Throw exceptions when nullable APIs return null unexpectedly

```csharp
public async Task<UserProfile> GetUserProfileAsync(string userId)
{
    // Client returns null when user is not found (nullable API)
    UserProfile? profile = await _userClient.GetProfileAsync(userId);
    
    if (profile is null)
    {
        throw new InvalidOperationException($"User profile for '{userId}' not found.");
    }
    
    return profile;
}
```

**❌ Avoid:** Silently returning fallback values

```csharp
public async Task<UserProfile> GetUserProfileAsync(string userId)
{
    UserProfile? profile = await _userClient.GetProfileAsync(userId);
    
    // BAD: Masks missing data, bug surfaces elsewhere with wrong user info
    return profile ?? new UserProfile { Id = userId, Name = "Unknown" };
}
```

### Exception: Genuinely Optional Data

The only exception to the fail-fast rule is when data is **genuinely optional by design**:

- **Caches** that may not have data yet or have expired entries
- **Optional user preferences** with documented default behavior
- **Graceful degradation** in explicitly designed fallback scenarios

In these cases, document why the fallback is intentional:

```csharp
public CachedData? TryGetFromCache(string key)
{
    // Cache miss is expected behavior, not an error condition.
    // Callers should handle null by fetching from the primary source.
    return _cache.TryGetValue(key, out var data) ? data : null;
}
```

## Solution-Relative File Access

### Pattern: Solution-Relative File Providers

When providing access to files or directories that live under the solution root (e.g., `prompts/`, `community-rules/`), use the established `SolutionRelativeFileProvider` pattern.

### Architecture

The pattern consists of two components:

1. **`SolutionPathUtility`** (in `Core`): Finds the solution root by walking up the directory tree looking for `KicktippAi.slnx`
2. **`SolutionRelativeFileProvider`** (in `Core`): Creates an `IFileProvider` rooted at a directory under the solution root

### Creating a New File Provider

To expose files from a new solution-relative directory:

1. **Create a static factory class** in the appropriate project
2. **Use `SolutionRelativeFileProvider.Create()`** with the directory name
3. **Return `IFileProvider`** for flexibility

**Example:**

```csharp
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.FileProviders;

namespace MyProject;

/// <summary>
/// Factory for creating an IFileProvider rooted at the my-files directory
/// </summary>
public static class MyFilesFileProvider
{
    /// <summary>
    /// Creates a PhysicalFileProvider rooted at the my-files directory by finding the solution root
    /// </summary>
    /// <returns>An IFileProvider rooted at the my-files directory</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found</exception>
    public static IFileProvider Create() => SolutionRelativeFileProvider.Create("my-files");
}
```

### Existing Implementations

| Provider | Directory | Project |
|----------|-----------|---------|
| `PromptsFileProvider` | `prompts/` | `OpenAiIntegration` |
| `CommunityRulesFileProvider` | `community-rules/` | `ContextProviders.Kicktipp` |

### Why This Pattern?

1. **Consistent discovery**: All file providers find the solution root the same way
2. **Fail-fast behavior**: Throws `DirectoryNotFoundException` if solution root isn't found
3. **Testability**: Returns `IFileProvider` interface, allowing mocks in tests
4. **Encapsulation**: Consumers don't need to know about directory traversal logic
