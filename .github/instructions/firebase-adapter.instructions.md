---
applyTo: '**/FirebaseAdapter/**,**/FirebaseAdapter.Tests/**'
---
# Firebase Adapter Test Instructions

This document describes the test infrastructure for `FirebaseAdapter` and its integration tests.

## Overview

The `FirebaseAdapter.Tests` project contains **integration tests** that use a real Firestore emulator running in Docker via [Testcontainers](https://testcontainers.com/). These tests verify our Firebase repository implementations against actual Firestore behavior.

## Test Infrastructure Architecture

### Shared Container with Collection-Based Parallelization

We use a **single shared Firestore emulator container** across all test classes within each parallel group, with **collection-based parallelization** to maximize throughput while maintaining test isolation.

#### Why This Architecture?

1. **Shared Container per Parallel Group**: Starting a Docker container takes ~5-15 seconds. Using `SharedType.Keyed` shares one container across test classes within the same parallel group, reducing startup overhead.

2. **Collection-Based Parallelization**: Our repositories operate on **isolated collections**:
   - `FirebaseContextRepository` → `context-documents`
   - `FirebasePredictionRepository` → `match-predictions`, `matches`, `bonus-predictions`
   - `FirebaseKpiRepository` → `kpi-documents`

   Since these collections don't overlap, tests targeting different repositories can run **in parallel** safely.

3. **Sequential Within Collection Groups**: Tests within the same repository/collection group run **sequentially** with data clearing between tests. This ensures isolation without requiring unique collection names per test (which would leak test concerns into business logic).

#### Why `SharedType.Keyed` Over `SharedType.PerAssembly`?

Both could achieve container sharing, but `Keyed` is preferred because:

- **Explicit Intent**: The key `"FirestoreEmulator"` clearly documents what resource is being shared.
- **Future Flexibility**: If we later need multiple fixture types (e.g., a separate container for a different service), `Keyed` allows fine-grained control over which tests share which fixture.
- **Discoverability**: Searching for the key string reveals all tests sharing that resource.

#### Important: TUnit Keyed Sharing Behavior

> **Note:** TUnit's `SharedType.Keyed` on a base class creates fixture instances per concrete derived class, not truly globally across all classes with the same key. This means if tests from different parallel groups run concurrently, they may get separate fixture instances. This is still significantly better than `SharedType.PerClass` (which created one container per class), but you may see multiple containers in CI logs when parallel groups overlap in execution time.

### Parallel Groups

Each repository's tests belong to a **parallel constraint group** named after their collection domain:

| Repository | Parallel Group Key | Collections |
|------------|-------------------|-------------|
| `FirebaseContextRepository` | `Firestore:ContextDocuments` | `context-documents` |
| `FirebasePredictionRepository` | `Firestore:Predictions` | `match-predictions`, `matches`, `bonus-predictions` |
| `FirebaseKpiRepository` | `Firestore:Kpi` | `kpi-documents` |

Tests with **different keys can run in parallel**. Tests with the **same key run sequentially**.

## ⚠️ Modifying Business Logic (Repository Collections)

> **VERY IMPORTANT:** When modifying repository implementations in `src/FirebaseAdapter/`, you MUST verify that test isolation is maintained if you change which collections a method uses.

### Why This Matters

Our test parallelization relies on the assumption that **collections are isolated between repositories**. If you change a repository method to use a collection that belongs to another repository's parallel group, you will break test isolation and cause **intermittent test failures** that are extremely hard to debug.

### Checklist When Changing Collections

Before changing which collection(s) a repository method uses:

1. **Check the current collection → parallel group mapping** (see [Parallel Groups](#parallel-groups) table above).
2. **If adding a new collection**: Add a corresponding clear method to `FirestoreFixture` and update the appropriate `ClearXxxAsync` method.
3. **If using a collection from another group**: You MUST either:
   - Move your tests to use that group's `[NotInParallel]` key, OR
   - Merge the two parallel groups into one (losing parallelization between them).
4. **Update the mapping table** in this document and in `FirestoreFixture` XML docs.
5. **Run the full test suite multiple times** to verify no intermittent failures.

### Example: Adding a New Collection

If `FirebasePredictionRepository` needs to start using a new `prediction-history` collection:

1. Add the constant to `FirestoreFixture`:
   ```csharp
   private const string PredictionHistoryCollection = "prediction-history";
   ```

2. Update `ClearPredictionsAsync()` to include it:
   ```csharp
   public async Task ClearPredictionsAsync()
   {
       await Task.WhenAll(
           ClearCollectionAsync(MatchPredictionsCollection),
           ClearCollectionAsync(MatchesCollection),
           ClearCollectionAsync(BonusPredictionsCollection),
           ClearCollectionAsync(PredictionHistoryCollection)); // Added
   }
   ```

3. Update the parallel groups table in this document.

### Example: Using Another Group's Collection (⚠️ Dangerous)

If `FirebaseContextRepository` needs to read from `matches` (owned by Predictions group):

**Option A (Preferred):** Reconsider the design. Can you avoid cross-collection dependencies?

**Option B:** Merge the groups:
1. Change `FirebaseContextRepositoryTests_Base` to use `FirestoreFixture.PredictionsParallelKey`.
2. Update `ClearPredictionsAsync()` to also clear `context-documents`.
3. Remove the now-unused `ContextDocumentsParallelKey`.
4. Update all documentation.

This loses parallelization between these two test groups, so only do this if truly necessary.

## Writing New Tests

### Adding Tests to Existing Repository Test Classes

1. **Inherit from the appropriate base class** (e.g., `FirebaseContextRepositoryTests_Base`).
2. **The base class handles**:
   - Fixture injection via `[ClassDataSource<FirestoreFixture>]`
   - Sequential execution within the collection group via `[NotInParallel]`
   - Data clearing before each test via `[Before(Test)]`
3. **Just write your test methods** - no additional setup needed.

### Creating Tests for a New Repository

If you create a new Firebase repository that uses **new, isolated collections**:

1. **Create a new base class** following the pattern in existing base classes.
2. **Use `SharedType.Keyed` with `Key = FirestoreFixture.SharedKey`** to share the container.
3. **Create a new `[NotInParallel]` key** based on your collection domain (e.g., `"Firestore:MyNewCollection"`).
4. **Add a collection-specific clear method** to `FirestoreFixture` (see below).
5. **Call your collection-specific clear method** in `[Before(Test)]`.
6. **Add a constant** for the parallel key to `FirestoreFixture` for discoverability.

### If Your New Repository Shares Collections with Existing Ones

If your new repository uses collections that overlap with an existing repository:

1. **Use the SAME `[NotInParallel]` key** as the existing repository.
2. **Use the SAME clear method** in your `[Before(Test)]` hook.
3. **Document the shared collection relationship** in both base classes.

This ensures tests don't run in parallel and corrupt each other's data.

## FirestoreFixture API

### Shared Constants

Use these constants from `FirestoreFixture` for consistency:

```csharp
// Fixture sharing key - use this in ClassDataSource
FirestoreFixture.SharedKey  // "FirestoreEmulator"

// Parallel constraint keys - use these in NotInParallel
FirestoreFixture.ContextDocumentsParallelKey  // "Firestore:ContextDocuments"
FirestoreFixture.PredictionsParallelKey       // "Firestore:Predictions"
FirestoreFixture.KpiParallelKey               // "Firestore:Kpi"
```

### Collection-Specific Clear Methods

The fixture provides methods to clear specific collection groups:

```csharp
// Clear only context-documents collection
await Fixture.ClearContextDocumentsAsync();

// Clear prediction-related collections (match-predictions, matches, bonus-predictions)
await Fixture.ClearPredictionsAsync();

// Clear only kpi-documents collection
await Fixture.ClearKpiDocumentsAsync();
```

**Always use the collection-specific method** in your `[Before(Test)]` hook to enable parallel execution with other collection groups.

## Common Pitfalls

### ❌ Don't Use `SharedType.PerClass`

```csharp
// BAD: Creates a new container for each test class
[ClassDataSource<FirestoreFixture>(Shared = SharedType.PerClass)]
```

This defeats the purpose of container sharing and significantly increases test time.

### ❌ Don't Remove `[NotInParallel]` from Repository Tests

```csharp
// BAD: Tests within the same class will interfere with each other
// (they use the same collections and ClearXxxAsync() would clear data mid-test)
[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
// Missing [NotInParallel]!
public abstract class MyRepositoryTests_Base { ... }
```

### ❌ Don't Use Magic Strings for Keys

```csharp
// BAD: Magic strings make refactoring error-prone
[NotInParallel("Firestore:ContextDocuments")]
```

```csharp
// GOOD: Use constants from FirestoreFixture
[NotInParallel(FirestoreFixture.ContextDocumentsParallelKey)]
```

### ✅ Correct Pattern

```csharp
[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.MyNewCollectionParallelKey)]
public abstract class MyRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    [Before(Test)]
    public async Task ClearMyCollectionAsync()
    {
        await Fixture.ClearMyCollectionAsync(); // Collection-specific!
    }
}
```

## Unit Tests (Non-Fixture Tests)

Tests that don't need the Firestore emulator (e.g., `ServiceCollectionExtensionsTests`, `FirebaseKpiContextProviderTests`) should:

1. **Not use `FirestoreFixture`** - use Moq/FakeLogger instead.
2. **Not use `[NotInParallel]`** - they can run in parallel with everything.
3. **Be placed in their own files**, not in repository test folders.
