# Orchestrator Test Coverage Plan

This document outlines the approach for achieving comprehensive unit test coverage for the `Orchestrator` project.

## Current Status

- **Test Project**: `tests/Orchestrator.Tests/` - Created and building
- **Commands to Test**: 15 commands across 3 categories
- **Refactoring Required**: Commands currently use static `AnsiConsole` which makes console output verification difficult

## Package References

Add the following packages to `Orchestrator.Tests.csproj`:

```xml
<!-- Most recent versions at the time of writing -->
<PackageReference Include="Spectre.Console.Cli.Testing" Version="0.54.0" />
<PackageReference Include="Spectre.Console.Testing" Version="0.54.0" />
```

These provide:
- `CommandAppTester` - Test harness for CLI commands
- `FakeTypeRegistrar` / `FakeTypeResolver` - Test doubles for DI infrastructure
- `TestConsole` - Captures console output for assertions

---

## Testing Strategy

### Preparation: IAnsiConsole Injection (Completed)

Commands have been refactored to accept `IAnsiConsole` via constructor injection. This enables console output verification using `TestConsole`. See [finished-preparation.md](finished-preparation.md) for details on the completed work.

### Phase 1.5: Full Dependency Injection for Commands (In Progress)

Commands currently have `IAnsiConsole` injected but still build their own `ServiceCollection` internally. Phase 1.5 addresses moving **all** command dependencies to the global DI container.

**Architecture:**
- **Factory pattern** for settings-dependent services (Firebase, Kicktipp, OpenAI)
- **Idempotent registration** via `TryAdd*` methods
- **No keyed services needed** - factories handle runtime configuration

**Infrastructure Created:**

| File | Purpose |
|------|---------|
| `Infrastructure/Factories/IFirebaseServiceFactory.cs` | Interface for Firebase service creation |
| `Infrastructure/Factories/FirebaseServiceFactory.cs` | Creates `FirestoreDb`, repositories |
| `Infrastructure/Factories/IKicktippClientFactory.cs` | Interface for Kicktipp client creation |
| `Infrastructure/Factories/KicktippClientFactory.cs` | Creates `IKicktippClient` with credentials |
| `Infrastructure/Factories/IOpenAiServiceFactory.cs` | Interface for OpenAI service creation |
| `Infrastructure/Factories/OpenAiServiceFactory.cs` | Creates `IPredictionService`, `ITokenUsageTracker` |
| `Infrastructure/ServiceRegistrationExtensions.cs` | Extension methods for service registration |

**Refactored Commands (✅ Complete):**
- [x] `ListKpiCommand` - Uses `IFirebaseServiceFactory`
- [x] `UploadKpiCommand` - Uses `IFirebaseServiceFactory`
- [x] `CostCommand` - Uses `IFirebaseServiceFactory`

**Remaining Commands:**
- [ ] `UploadTransfersCommand`
- [ ] `ContextChangesCommand`
- [ ] `SnapshotsEncryptCommand`
- [ ] `SnapshotsFetchCommand`
- [ ] `SnapshotsAllCommand`
- [ ] `AnalyzeMatchDetailedCommand`
- [ ] `AnalyzeMatchComparisonCommand`
- [ ] `VerifyMatchdayCommand`
- [ ] `VerifyBonusCommand`
- [ ] `CollectContextKicktippCommand`
- [ ] `BonusCommand`
- [ ] `MatchdayCommand`

**Testing Approach with Factories:**
```csharp
// In tests, mock the factory interfaces
var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
var mockKpiRepository = new Mock<IKpiRepository>();

mockFirebaseFactory
    .Setup(f => f.CreateKpiRepository(It.IsAny<FirestoreDb>()))
    .Returns(mockKpiRepository.Object);

// Register mocks in TypeRegistrar
var services = new ServiceCollection();
services.AddSingleton<IAnsiConsole>(new TestConsole());
services.AddSingleton(mockFirebaseFactory.Object);

var registrar = new TypeRegistrar(services);
var app = new CommandAppTester(registrar: registrar);
```

### Optional Future Refactoring

**Abstract Base Command Class**: Consider introducing an abstract base class with `IAnsiConsole`:
```csharp
public abstract class BaseCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    protected IAnsiConsole Console { get; }
    
    protected BaseCommand(IAnsiConsole console)
    {
        Console = console;
    }
}
```

**Evaluation needed**: This reduces boilerplate but adds inheritance coupling. May not be worth it given:
- Only 15 commands total
- Each command has unique dependencies beyond `IAnsiConsole`
- Composition (storing `IAnsiConsole` in each command) is simpler and more flexible

**Recommendation**: Defer this refactoring until after Phase 1 when all dependencies are injected. At that point, patterns may emerge that justify a base class.

### Phase 2: Shared Test Infrastructure

Create reusable test infrastructure in `Orchestrator.Tests`:

```
tests/Orchestrator.Tests/
├── Infrastructure/
│   ├── OrchestratorTestFactories.cs    # Factory methods for domain objects and mocked services
│   └── ConsoleAssertions.cs            # Custom assertions for output patterns (if needed beyond Contains)
├── Commands/
│   ├── Operations/
│   ├── Observability/
│   └── Utility/
└── Orchestrator.Tests.csproj
```

### CommandAppTester Usage

Use `CommandAppTester` from `Spectre.Console.Cli.Testing` as the primary test harness. It wraps command execution and provides:

| Property/Method | Description |
|-----------------|-------------|
| `Console` | The `TestConsole` instance for input simulation |
| `Configure(Action<IConfigurator>)` | Configure commands and settings |
| `SetDefaultCommand<T>()` | Set the default command |
| `RunAsync(params string[])` | Execute and return `CommandAppResult` |

**`CommandAppResult` properties:**

| Property | Description |
|----------|-------------|
| `ExitCode` | Command exit code (0 = success) |
| `Output` | Captured console output as string |
| `Context` | The `CommandContext` from execution |
| `Settings` | The parsed `CommandSettings` instance |

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
- [x] Complete Phase 1.5 (factory-based DI for command dependencies)
- [ ] Create `OrchestratorTestFactories.cs` with factory methods for domain objects and mocked services
- [ ] Test `JustificationConsoleWriter`
- [ ] Test `BaseSettings` validation
- [ ] Test `EnvironmentHelper`
- [ ] Test `PathUtility`

### Milestone 2: Utility Commands (Simplest)
- [x] DI for `ListKpiCommand`
- [x] DI for `UploadKpiCommand`
- [ ] DI for `UploadTransfersCommand`
- [ ] DI for `ContextChangesCommand`
- [ ] DI for `SnapshotsEncryptCommand`
- [ ] DI for `SnapshotsFetchCommand`
- [ ] DI for `SnapshotsAllCommand`
- [ ] Test `ListKpiCommand`
- [ ] Test `UploadKpiCommand`
- [ ] Test `UploadTransfersCommand`
- [ ] Test `ContextChangesCommand`
- [ ] Test `SnapshotsEncryptCommand`
- [ ] Test `SnapshotsFetchCommand`
- [ ] Test `SnapshotsAllCommand`

### Milestone 3: Observability Commands
- [x] DI for `CostCommand`
- [ ] DI for `AnalyzeMatchDetailedCommand`
- [ ] DI for `AnalyzeMatchComparisonCommand`
- [ ] Test `CostCommand`
- [ ] Test `AnalyzeMatchDetailedCommand`
- [ ] Test `AnalyzeMatchComparisonCommand`

### Milestone 4: Operations Commands (Most Complex)
- [ ] DI for `VerifyMatchdayCommand`
- [ ] DI for `VerifyBonusCommand`
- [ ] DI for `CollectContextKicktippCommand`
- [ ] DI for `BonusCommand`
- [ ] DI for `MatchdayCommand`
- [ ] Test `VerifyMatchdayCommand`
- [ ] Test `VerifyBonusCommand`
- [ ] Test `CollectContextKicktippCommand`
- [ ] Test `BonusCommand`
- [ ] Test `MatchdayCommand`

---

## Console Output Testing Approach

Using `CommandAppTester` from `Spectre.Console.Cli.Testing`:

```csharp
// Example test pattern
[Test]
public async Task Command_displays_success_message_on_completion()
{
    // Arrange
    var registrar = new FakeTypeRegistrar();
    // Register mocked dependencies as needed
    registrar.RegisterInstance(typeof(IMyService), mockService.Object);
    
    var app = new CommandAppTester(registrar: registrar);
    app.Configure(config => config.AddCommand<MyCommand>("mycommand"));

    // Act
    var result = await app.RunAsync("mycommand", "--option", "value");

    // Assert
    await Assert.That(result.ExitCode).IsEqualTo(0);
    await Assert.That(result.Output).Contains("Successfully");
    
    // Optionally verify parsed settings
    var settings = result.Settings as MyCommand.Settings;
    await Assert.That(settings!.Option).IsEqualTo("value");
}
```

```csharp
// Testing interactive input
[Test]
public async Task Command_handles_user_confirmation()
{
    // Arrange
    var app = new CommandAppTester();
    app.Console.Input.PushTextWithEnter("yes");
    app.Configure(config => config.AddCommand<ConfirmCommand>("confirm"));

    // Act
    var result = await app.RunAsync("confirm");

    // Assert
    await Assert.That(result.ExitCode).IsEqualTo(0);
}
```

**Assertion Strategy**:
- Use `Contains()` for key content patterns (not exact matching)
- Verify structured output (tables) by checking column headers and key data
- Avoid asserting ANSI escape codes or exact formatting
- Use `result.Settings` to verify argument/option parsing

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

- Commands use factory pattern for service creation - factories are injected via constructor, making them easily mockable in tests
- Some commands read from file system (`kpi-documents/`, `transfers-documents/`) - refactor to use `SolutionRelativeFileProvider` pattern (from `EHonda.KicktippAi.Core`) which can be easily mocked via `IFileProvider` in tests
- Environment variables are loaded per-command (lazy loading) - commands only load what they need
- Mock factory interfaces (`IFirebaseServiceFactory`, `IKicktippClientFactory`, `IOpenAiServiceFactory`) in tests rather than the underlying services directly
