# Orchestrator Test Coverage Plan

This document outlines the approach for achieving comprehensive unit test coverage for the `Orchestrator` project.

## Current Status

- **Test Project**: `tests/Orchestrator.Tests/` - Created and building
- **Commands to Test**: 15 commands across 3 categories
- **Refactoring Required**: Commands currently use static `AnsiConsole` which makes console output verification difficult

## Testing Strategy

### Phase 1: Command Refactoring for Testability

Before comprehensive testing, commands need refactoring to accept `IAnsiConsole` via constructor injection. This enables:

1. **Console output verification** using `TestConsole` from `Spectre.Console.Testing`
2. **Dependency injection** for mocking services (IKicktippClient, IPredictionService, etc.)

**Approach**: Use Spectre.Console.Cli's `ITypeRegistrar` pattern to bridge Microsoft.Extensions.DependencyInjection:

- Create `TypeRegistrar` and `TypeResolver` infrastructure classes
- Register `IAnsiConsole` (use `AnsiConsole.Console` in production, `TestConsole` in tests)
- Update each command to receive `IAnsiConsole` via constructor

### Phase 2: Shared Test Infrastructure

Create reusable test infrastructure in `Orchestrator.Tests`:

```
tests/Orchestrator.Tests/
├── Infrastructure/
│   ├── OrchestratorTestFactories.cs    # Factory methods for creating commands with mocked dependencies
│   └── ConsoleAssertions.cs            # Custom assertions for TestConsole output patterns
├── Commands/
│   ├── Operations/
│   ├── Observability/
│   └── Utility/
└── Orchestrator.Tests.csproj
```

### Phase 3: Command-by-Command Testing

Test each command to ~100% unit test coverage with mocked dependencies.

**Coverage Workflow**: When implementing tests for individual commands, follow the workflow in `.github/instructions/test-coverage.instructions.md`:
1. Run `Generate-CoverageReport.ps1 -Projects Orchestrator.Tests` to generate coverage data
2. Use `Get-CoverageDetails.ps1` to identify untested code areas
3. Iterate on tests until coverage targets are met

---

## Commands Inventory

### Operations Commands (High Priority)

| Command | File | Dependencies | Complexity |
|---------|------|--------------|------------|
| `MatchdayCommand` | `Commands/Operations/Matchday/MatchdayCommand.cs` | IKicktippClient, IPredictionService, KicktippContextProvider, IPredictionRepository, IContextRepository, ITokenUsageTracker | High |
| `BonusCommand` | `Commands/Operations/Bonus/BonusCommand.cs` | IKicktippClient, IPredictionService, FirebaseKpiContextProvider, IPredictionRepository, ITokenUsageTracker | High |
| `VerifyMatchdayCommand` | `Commands/Operations/Verify/VerifyMatchdayCommand.cs` | IKicktippClient, IPredictionRepository, IContextRepository | Medium |
| `VerifyBonusCommand` | `Commands/Operations/Verify/VerifyBonusCommand.cs` | IKicktippClient, IPredictionRepository, IKpiRepository | Medium |
| `CollectContextKicktippCommand` | `Commands/Operations/CollectContext/CollectContextKicktippCommand.cs` | IKicktippClient, KicktippContextProvider, IContextRepository | Medium |

### Observability Commands (Medium Priority)

| Command | File | Dependencies | Complexity |
|---------|------|--------------|------------|
| `CostCommand` | `Commands/Observability/Cost/CostCommand.cs` | IPredictionRepository, FirestoreDb | Medium |
| `AnalyzeMatchDetailedCommand` | `Commands/Observability/AnalyzeMatch/AnalyzeMatchDetailedCommand.cs` | IPredictionService, ITokenUsageTracker, IContextRepository | Medium |
| `AnalyzeMatchComparisonCommand` | `Commands/Observability/AnalyzeMatch/AnalyzeMatchComparisonCommand.cs` | IPredictionService, ITokenUsageTracker, IContextRepository | Medium |
| `ContextChangesCommand` | `Commands/Observability/ContextChanges/ContextChangesCommand.cs` | IContextRepository | Low |

### Utility Commands (Lower Priority)

| Command | File | Dependencies | Complexity |
|---------|------|--------------|------------|
| `UploadKpiCommand` | `Commands/Utility/UploadKpi/UploadKpiCommand.cs` | IKpiRepository | Low |
| `UploadTransfersCommand` | `Commands/Utility/UploadTransfers/UploadTransfersCommand.cs` | IContextRepository | Low |
| `ListKpiCommand` | `Commands/Utility/ListKpi/ListKpiCommand.cs` | IKpiRepository | Low |
| `SnapshotsFetchCommand` | `Commands/Utility/Snapshots/SnapshotsFetchCommand.cs` | SnapshotClient (internal HTTP handling) | Medium |
| `SnapshotsEncryptCommand` | `Commands/Utility/Snapshots/SnapshotsEncryptCommand.cs` | File system operations | Low |
| `SnapshotsAllCommand` | `Commands/Utility/Snapshots/SnapshotsAllCommand.cs` | Combines fetch + encrypt | Medium |

### Shared Components

| Component | File | Test Focus |
|-----------|------|------------|
| `JustificationConsoleWriter` | `Commands/Shared/JustificationConsoleWriter.cs` | Console output formatting |
| `BaseSettings` | `Commands/Operations/Matchday/BaseSettings.cs` | Settings validation |
| `EnvironmentHelper` | `EnvironmentHelper.cs` | Environment variable loading |
| `PathUtility` | `PathUtility.cs` | Path resolution |
| `LoggingConfiguration` | `LoggingConfiguration.cs` | Logger creation |

---

## Test Categories per Command

For each command, implement tests covering:

### 1. Settings Validation Tests
- Required arguments validation
- Option combinations (e.g., `--override-database` + `--repredict` conflict)
- Default value behavior

### 2. Workflow Logic Tests
- Happy path execution
- Error handling (missing credentials, service failures)
- Dry-run mode behavior
- Verbose mode output differences
- Agent mode output filtering

### 3. Console Output Tests
- Key messages appear in output (using pattern matching)
- Progress indicators for long operations
- Table/panel formatting for structured data
- Error messages are properly formatted

### 4. Service Interaction Tests
- Services called with correct parameters
- Results properly processed and displayed
- Database persistence (when not dry-run)

---

## Implementation Order

### Milestone 1: Infrastructure & Shared Components
- [ ] Implement `TypeRegistrar` / `TypeResolver` for DI
- [ ] Create `OrchestratorTestFactories.cs` with common factory methods
- [ ] Create `ConsoleAssertions.cs` for pattern-based output assertions
- [ ] Test `JustificationConsoleWriter`
- [ ] Test `BaseSettings` validation
- [ ] Test `EnvironmentHelper`
- [ ] Test `PathUtility`

### Milestone 2: Utility Commands (Simplest)
- [ ] Refactor & test `ListKpiCommand`
- [ ] Refactor & test `UploadKpiCommand`
- [ ] Refactor & test `UploadTransfersCommand`
- [ ] Refactor & test `ContextChangesCommand`
- [ ] Refactor & test `SnapshotsEncryptCommand`
- [ ] Refactor & test `SnapshotsFetchCommand`
- [ ] Refactor & test `SnapshotsAllCommand`

### Milestone 3: Observability Commands
- [ ] Refactor & test `CostCommand`
- [ ] Refactor & test `AnalyzeMatchDetailedCommand`
- [ ] Refactor & test `AnalyzeMatchComparisonCommand`

### Milestone 4: Operations Commands (Most Complex)
- [ ] Refactor & test `VerifyMatchdayCommand`
- [ ] Refactor & test `VerifyBonusCommand`
- [ ] Refactor & test `CollectContextKicktippCommand`
- [ ] Refactor & test `BonusCommand`
- [ ] Refactor & test `MatchdayCommand`

---

## Console Output Testing Approach

Using `Spectre.Console.Testing.TestConsole`:

```csharp
// Example test pattern
[Test]
public async Task Command_displays_success_message_on_completion()
{
    // Arrange
    var console = new TestConsole();
    var command = CreateCommand(console: console);
    var settings = CreateSettings();

    // Act
    var result = await command.ExecuteAsync(CreateContext(), settings);

    // Assert
    await Assert.That(result).IsEqualTo(0);
    await Assert.That(console.Output).Contains("Successfully");
}
```

**Assertion Strategy**:
- Use `Contains()` for key content patterns (not exact matching)
- Verify structured output (tables) by checking column headers and key data
- Avoid asserting ANSI escape codes or exact formatting

---

## Dependencies to Mock

| Interface | Mock Behavior |
|-----------|---------------|
| `IKicktippClient` | Return predefined matches, predictions, bonus questions |
| `IPredictionService` | Return predefined predictions |
| `IPredictionRepository` | Track save/retrieve calls, return predefined data |
| `IContextRepository` | Return predefined context documents |
| `IKpiRepository` | Return predefined KPI documents |
| `ITokenUsageTracker` | Track usage, return predefined costs |


---

## Notes

- Commands use internal DI setup via `ServiceCollection` - tests can either mock at service level or refactor to inject services directly
- Some commands read from file system (`kpi-documents/`, `transfers-documents/`) - refactor to use `SolutionRelativeFileProvider` pattern (from `EHonda.KicktippAi.Core`) which can be easily mocked via `IFileProvider` in tests
- Environment variables are used extensively - use test fixtures to set up required variables
- Mock repository interfaces (`IPredictionRepository`, `IContextRepository`, `IKpiRepository`) rather than the underlying Firebase/Firestore infrastructure
