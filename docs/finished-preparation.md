# Finished Preparation Steps

This document contains completed preparation steps that were part of the [orchestrator test plan](orchestrator-test-plan.md).

---

## Phase 1: IAnsiConsole Injection for Testability

Commands have been refactored to accept `IAnsiConsole` via constructor injection. This enables console output verification using `TestConsole` from `Spectre.Console.Testing`.

### Approach

Use Spectre.Console.Cli's `ITypeRegistrar` pattern to bridge Microsoft.Extensions.DependencyInjection:

- **Production**: Implement `TypeRegistrar` and `TypeResolver` classes following the official documentation and examples:
  - [Dependency Injection in CLI Apps](https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/tutorials/dependency-injection-in-cli-apps.md)
  - [Testing Commandline Applications](https://github.com/spectreconsole/website/blob/main/Spectre.Docs/Content/cli/how-to/testing-command-line-applications.md)
  - Canonical [TypeRegistrar](https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeRegistrar.cs)
  - Canonical [TypeResolver](https://github.com/spectreconsole/examples/blob/main/examples/Cli/Logging/Infrastructure/TypeResolver.cs)
- **Tests**: Replace with `FakeTypeRegistrar` from `Spectre.Console.Cli.Testing` - no custom implementation needed
- Register `IAnsiConsole` (use `AnsiConsole.Console` in production, `TestConsole` in tests)
- Update each command to receive `IAnsiConsole` via constructor

### Completed Work

#### Infrastructure

- [x] Created `src/Orchestrator/Infrastructure/TypeRegistrar.cs`
- [x] Created `src/Orchestrator/Infrastructure/TypeResolver.cs`
- [x] Updated `src/Orchestrator/Program.cs` to use DI infrastructure
- [x] Registered `IAnsiConsole` as `AnsiConsole.Console` in production

#### Shared Components

- [x] Refactored `JustificationConsoleWriter` from static to instance class (accepts `IAnsiConsole` in constructor)

#### Commands Refactored for IAnsiConsole

All 15 commands now accept `IAnsiConsole` via constructor injection:

**Operations Commands:**
- [x] `MatchdayCommand`
- [x] `BonusCommand`
- [x] `VerifyMatchdayCommand`
- [x] `VerifyBonusCommand`
- [x] `CollectContextKicktippCommand`

**Observability Commands:**
- [x] `CostCommand`
- [x] `AnalyzeMatchDetailedCommand`
- [x] `AnalyzeMatchComparisonCommand`
- [x] `ContextChangesCommand`

**Utility Commands:**
- [x] `ListKpiCommand`
- [x] `UploadKpiCommand`
- [x] `UploadTransfersCommand`
- [x] `SnapshotsFetchCommand`
- [x] `SnapshotsEncryptCommand`
- [x] `SnapshotsAllCommand`

### What This Enables

Tests can now verify console output by injecting `TestConsole`:

```csharp
var registrar = new FakeTypeRegistrar();
registrar.RegisterInstance(typeof(IAnsiConsole), new TestConsole());

var app = new CommandAppTester(registrar: registrar);
app.Configure(config => config.AddCommand<MyCommand>("mycommand"));

var result = await app.RunAsync("mycommand", "--option", "value");

// Assert on result.Output
```

### Remaining Work

~~Commands still build their own `ServiceCollection` internally for other services. See **Phase 1.5** in the [test plan](orchestrator-test-plan.md) for plans to move all dependencies to global DI.~~

This has been addressed - see **Phase 1.5** below.

---

## Phase 1.5: Full Dependency Injection for Commands

All commands now use factory-based dependency injection instead of building their own `ServiceCollection` internally. This enables mocking all external dependencies in tests.

### Architecture

- **Factory pattern** for settings-dependent services (Firebase, Kicktipp, OpenAI, Context Providers)
- **Idempotent registration** via `TryAdd*` methods
- **No keyed services needed** - factories handle runtime configuration

### Infrastructure Created

| File | Purpose |
|------|---------|
| `Infrastructure/Factories/IFirebaseServiceFactory.cs` | Interface for Firebase service creation |
| `Infrastructure/Factories/FirebaseServiceFactory.cs` | Creates `FirestoreDb`, repositories |
| `Infrastructure/Factories/IKicktippClientFactory.cs` | Interface for Kicktipp client creation |
| `Infrastructure/Factories/KicktippClientFactory.cs` | Creates `IKicktippClient`, `SnapshotClient` with credentials |
| `Infrastructure/Factories/IOpenAiServiceFactory.cs` | Interface for OpenAI service creation |
| `Infrastructure/Factories/OpenAiServiceFactory.cs` | Creates `IPredictionService`, `ITokenUsageTracker` |
| `Infrastructure/Factories/IContextProviderFactory.cs` | Interface for context provider creation |
| `Infrastructure/Factories/ContextProviderFactory.cs` | Creates `KicktippContextProvider`, `CommunityRulesFileProvider` |
| `Infrastructure/ServiceRegistrationExtensions.cs` | Extension methods for service registration |

### All Commands Refactored

All 15 commands now receive factory interfaces via constructor injection:

**Operations Commands:**
- [x] `MatchdayCommand` - Uses all four factories
- [x] `BonusCommand` - Uses all four factories
- [x] `VerifyMatchdayCommand` - Uses `IFirebaseServiceFactory`, `IKicktippClientFactory`
- [x] `VerifyBonusCommand` - Uses `IFirebaseServiceFactory`, `IKicktippClientFactory`
- [x] `CollectContextKicktippCommand` - Uses `IFirebaseServiceFactory`, `IKicktippClientFactory`, `IContextProviderFactory`

**Observability Commands:**
- [x] `CostCommand` - Uses `IFirebaseServiceFactory`
- [x] `AnalyzeMatchDetailedCommand` - Uses all four factories
- [x] `AnalyzeMatchComparisonCommand` - Uses all four factories
- [x] `ContextChangesCommand` - Uses `IFirebaseServiceFactory`

**Utility Commands:**
- [x] `ListKpiCommand` - Uses `IFirebaseServiceFactory`
- [x] `UploadKpiCommand` - Uses `IFirebaseServiceFactory`
- [x] `UploadTransfersCommand` - Uses `IFirebaseServiceFactory`
- [x] `SnapshotsFetchCommand` - Uses `IKicktippClientFactory`
- [x] `SnapshotsEncryptCommand` - No factory needed (file operations only)
- [x] `SnapshotsAllCommand` - Uses `IKicktippClientFactory`

### What This Enables

Tests can now mock all external dependencies:

```csharp
// In tests, mock the factory interfaces
var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
var mockKpiRepository = new Mock<IKpiRepository>();

mockFirebaseFactory
    .Setup(f => f.CreateKpiRepository())
    .Returns(mockKpiRepository.Object);

// Register mocks in TypeRegistrar
var services = new ServiceCollection();
services.AddSingleton<IAnsiConsole>(new TestConsole());
services.AddSingleton(mockFirebaseFactory.Object);

var registrar = new TypeRegistrar(services);
var app = new CommandAppTester(registrar: registrar);
```
