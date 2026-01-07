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

Commands still build their own `ServiceCollection` internally for other services. See **Phase 1.5** in the [test plan](orchestrator-test-plan.md) for plans to move all dependencies to global DI.
