# FirebaseAdapter.Tests

Unit and integration tests for the FirebaseAdapter project.

## Coverage Decisions

### Intentionally Uncovered Code

The following code paths are intentionally excluded from coverage targets:

| Location | Reason |
|----------|--------|
| Repository exception catch blocks | These paths only log the exception and rethrow. Testing them would require mocking `FirestoreDb` failures, adding complexity without testing meaningful business logic. |
| `ServiceCollectionExtensions` FirestoreDb initialization failures | The catch block in the `FirestoreDb` singleton factory logs and rethrows. This is infrastructure code that is difficult to test without real Firebase failures. |

### Coverage Strategy

- **Integration tests** use the Firestore emulator via Testcontainers for realistic database interactions
- **Unit tests** use Moq for testing components with mockable dependencies (e.g., `FirebaseKpiContextProvider`)
- **Constructor null guards** are tested to ensure proper validation of required dependencies

## Test Organization

Tests follow the complex test fixture pattern with:
- Base classes for shared test functionality (`*Tests_Base.cs`)
- Separate test files per method or feature (`*_{Method}_Tests.cs`)

See [project_style_guide.md](../project_style_guide.md) for detailed conventions.

## Running Tests

```powershell
# Run all FirebaseAdapter tests
dotnet run --project tests/FirebaseAdapter.Tests

# Run with coverage
.\Generate-CoverageReport.ps1 -Project FirebaseAdapter.Tests
```
