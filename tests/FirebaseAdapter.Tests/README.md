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

## Copilot Coding Agent firewall note

The Firestore integration tests use the `google/cloud-sdk:*-emulators` image through `Testcontainers.Firestore`. During startup, the Google Cloud SDK may probe `metadata.google.internal` to check for GCE metadata-based credentials or project information.

To keep the emulator startup isolated in hardened environments, `FirestoreFixture` sets `CLOUDSDK_CORE_CHECK_GCE_METADATA=false` on the container. If a future SDK or image version still performs blocked lookups, the remaining options are:

- allowlist `metadata.google.internal` for the Copilot Coding Agent
- move emulator/image setup into repository actions setup steps that run before the firewall is enabled
- replace the current emulator startup path with a different container/image that does not invoke the Google Cloud SDK startup flow
