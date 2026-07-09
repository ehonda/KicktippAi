# Maintenance Low-Hanging Fruit Audit

Status: Completed

Date: 2026-07-10

## Goal

Identify concrete, comparatively low-risk maintenance improvements in the
KicktippAi codebase. The findings in this document are intended to be used as
the source material for a separate implementation plan.

The audit focused on:

- dead or apparently unused code;
- compiler warnings and obsolete APIs;
- outdated, vulnerable, or legacy dependencies;
- duplicated configuration and implementation patterns;
- inconsistencies that require manual synchronization;
- missing automated checks; and
- large maintenance hotspots that should be considered after the quick wins.

No production behavior was changed as part of this audit.

## Repository Snapshot

The audit was performed on a clean `main` branch tracking `origin/main`.

The following checks were run:

```powershell
dotnet build KicktippAi.slnx --configuration Release --no-restore --no-incremental
dotnet package list --project KicktippAi.slnx --outdated --format json
dotnet package list --project KicktippAi.slnx --vulnerable --include-transitive --format json
dotnet package list --project KicktippAi.slnx --deprecated --include-transitive --format json
uv --cache-dir .uv-cache run python -m unittest discover -s tools/tests -v
```

Results:

- Release build: succeeded with 0 errors and 189 warnings.
- Python tooling tests: 12 passed.
- NuGet vulnerability audit: one transitive package with two moderate
  advisories, limited to test projects.
- NuGet deprecation audit: seven legacy transitive IdentityModel packages,
  limited to the same test dependency branch.
- NuGet outdated audit: nine centrally managed direct packages have newer
  stable patch or minor versions.

The full C# test suite was not rerun for this read-only audit. The repository's
normal build workflow already runs each C# test project independently with
coverage.

## Recommended Order

The recommended implementation order is:

1. Update the vulnerable test dependency and rerun package audits.
2. Eliminate build warnings, starting with production warnings and mechanical
   TUnit migrations.
3. Remove the high-confidence dead declarations.
4. Add a warning regression gate.
5. Centralize repeated MSBuild and test-project configuration.
6. Add Python tests and dependency maintenance to automation.
7. Centralize production-community classification.
8. Address large-file decomposition in later, behavior-focused changes.

This order removes known risk and noise before making structural changes.

## Findings

### MNT-001: Vulnerable and Legacy Transitive Test Dependencies

Priority: High

Estimated effort: Small

Category: Dependencies / security maintenance

`WireMock.Net` is centrally pinned to `2.11.0`. Its `WireMock.Net.Minimal`
dependency resolves `Scriban.Signed 7.2.0` in `KicktippIntegration.Tests` and
`Orchestrator.Tests`. NuGet reports two moderate advisories for that version:

- [GHSA-q6rr-fm2g-g5x8](https://github.com/advisories/GHSA-q6rr-fm2g-g5x8)
- [GHSA-6q7j-xr26-3h2c](https://github.com/advisories/GHSA-6q7j-xr26-3h2c)

The same WireMock dependency branch resolves seven IdentityModel `6.34.0`
packages that NuGet classifies as legacy:

- `Microsoft.IdentityModel.Abstractions`
- `Microsoft.IdentityModel.JsonWebTokens`
- `Microsoft.IdentityModel.Logging`
- `Microsoft.IdentityModel.Protocols`
- `Microsoft.IdentityModel.Protocols.OpenIdConnect`
- `Microsoft.IdentityModel.Tokens`
- `System.IdentityModel.Tokens.Jwt`

The exposure is test-only, but it should still be removed from developer and CI
environments.

Recommendation:

1. Upgrade `WireMock.Net` from `2.11.0` to the currently available `2.12.0`.
2. Restore and run the affected test projects.
3. Rerun both the vulnerable and deprecated package audits.
4. If the transitive issues remain, evaluate a compatible central transitive
   pin or the next parent-package upgrade rather than suppressing the audit.

Acceptance criteria:

- NuGet reports no known vulnerable direct or transitive packages.
- The legacy IdentityModel `6.34.0` branch is removed or its remaining presence
  is explicitly documented with an upgrade constraint.
- `KicktippIntegration.Tests` and `Orchestrator.Tests` pass.

### MNT-002: Release Build Produces 189 Warnings

Priority: High

Estimated effort: Small to medium

Category: Outdated APIs / consistency / build health

The warning breakdown is:

| Code | Count | Meaning |
| --- | ---: | --- |
| `CS0618` | 125 | Obsolete API usage |
| `CS8602` | 63 | Possible null dereference |
| `CS8603` | 1 | Possible null return |

Of the 125 obsolete warnings:

- 122 are TUnit assertions using `.HasCount()` instead of `.Count()`;
- two use Firestore's obsolete `JsonCredentials` property; and
- one uses the obsolete parameterless `FirestoreBuilder` constructor.

The 63 `CS8602` warnings are in test code. Most are nullable-flow friction at
test-library boundaries, especially WireMock request objects, but each should
be reviewed before adding a null-forgiving operator.

The single `CS8603` production warning is in
`Core/HistoryCsvUtility.AddDataCollectedAtColumn`. The method returns the
nullable `previousCsvContent` after a helper predicate that does not establish
non-null state for the compiler.

Recommendation:

1. Apply the mechanical TUnit `.HasCount()` to `.Count()` migration.
2. Fix the production Firestore and `HistoryCsvUtility` warnings.
3. Review the test nullability warnings by hotspot, using explicit assertions
   or guards where possible.
4. Once the build reaches zero warnings, enforce warning-free CI with either
   `--warnaserror` in the build workflow or a shared MSBuild property.

Acceptance criteria:

- A clean Release build reports 0 warnings and 0 errors.
- No broad warning suppression is introduced.
- CI fails when a new compiler warning is added.

### MNT-003: Obsolete Firestore Credential Construction Is Duplicated

Priority: High

Estimated effort: Small

Category: Duplication / obsolete API / security hardening

Both of these production paths construct a `FirestoreDbBuilder` from service
account JSON and use the obsolete `JsonCredentials` property:

- `src/FirebaseAdapter/ServiceCollectionExtensions.cs`
- `src/Orchestrator/Infrastructure/Factories/FirebaseServiceFactory.cs`

The compiler warning states that `JsonCredentials` is deprecated because of a
potential security risk and recommends supplying an explicit credential object
through `GoogleCredential`.

Recommendation:

- Extract a shared credential/builder helper at the lowest practical layer.
- Parse the service account JSON into an explicit credential object.
- Make both dependency-injection and factory paths call the same helper.
- Preserve the emulator/default-credential behavior separately from the
  service-account path.

This finding should be implemented together with the production-warning part
of MNT-002.

Acceptance criteria:

- Neither production path uses `JsonCredentials`.
- Credential parsing and Firestore builder construction are implemented once.
- Existing Firebase adapter and factory tests pass.

### MNT-004: Obsolete Firestore Testcontainer Construction

Priority: Medium

Estimated effort: Small

Category: Outdated API

`src/TestUtilities/FirestoreFixture.cs` calls the obsolete parameterless
`FirestoreBuilder` constructor and then supplies the image with `.WithImage()`.
The API now expects the image in the constructor.

Recommendation:

- Upgrade `Testcontainers.Firestore` from `4.12.0` to `4.13.0`.
- Pass the existing pinned emulator image to the supported constructor.
- Keep the current image pin and environment configuration unless the package
  upgrade specifically requires a change.

Acceptance criteria:

- The obsolete constructor warning is gone.
- Firebase adapter and integration test fixtures start successfully.

### MNT-005: High-Confidence Dead Declarations

Priority: Medium

Estimated effort: Small

Category: Dead code

A repository-wide identifier-reference scan followed by manual inspection found
the following declarations with no consumers in `src` or `tests`:

| Declaration | Location | Notes |
| --- | --- | --- |
| `PredictionResult` | `src/Core/Prediction.cs` | Superseded by the active prediction and metadata contracts. |
| `BonusPredictionResult` | `src/Core/BonusQuestion.cs` | Superseded by the active bonus prediction and metadata contracts. |
| `KicktippSeasonMetadata` | `src/Orchestrator/Infrastructure/KicktippSeasonMetadata.cs` | Unused and still labels Bundesliga 2025/26 as current. |
| `PreparedExperimentExecutionRequest` | `PreparedExperimentContracts.cs` | Internal record with no construction or consumption. |
| `ExportedExperimentDataset` | `ExportedExperimentDataset.cs` | Unused wrapper only; the item and expected-output records remain active. |
| `BonusPredictionsResponse` | `PredictionService.cs` | Unused private DTO. |
| `BonusPredictionEntry` | `PredictionService.cs` | Used only by the unused private DTO above. |

The scan intentionally did not classify extension container classes as dead
when their extension methods are used. Public declarations should still be
checked for external consumers before deletion, although the repository does
not currently show such consumers.

Recommendation:

- Remove these declarations in one focused cleanup change.
- Rename `ExportedExperimentDataset.cs` if its remaining active item contracts
  make the current filename misleading.

Acceptance criteria:

- The listed declarations are removed or a concrete consumer is documented.
- The solution builds and all relevant tests pass.
- No public serialization contract used by stored artifacts is changed.

### MNT-006: Repeated MSBuild and Test-Project Configuration

Priority: Medium

Estimated effort: Medium

Category: Duplication / inconsistency

All 15 C# project files repeat the following baseline properties:

- `TargetFramework` set to `net10.0`;
- `ImplicitUsings` enabled; and
- nullable reference types enabled.

Eight test projects additionally repeat executable/test properties and common
package references such as TUnit, the TRX reporter, and Moq. `Core.csproj`
already contains a `TODO` to migrate shared assembly configuration to
`Directory.Build.props`.

Recommendation:

- Add a root `Directory.Build.props` for common target-framework, nullable,
  implicit-using, analyzer, and warning policy.
- Add test-scoped shared build configuration for common test properties and
  package references.
- Keep project-specific dependencies in their project files.
- Decide separately whether assembly/root-namespace naming should be unified.
  Currently only Core uses the `EHonda.KicktippAi.*` assembly prefix, so that
  change may have a larger compatibility surface than the baseline-property
  cleanup.

Acceptance criteria:

- Common properties have one source of truth.
- Test projects no longer repeat the agreed common package references.
- Generated assembly names and root namespaces change only if explicitly
  approved.
- Restore, build, and test behavior remains unchanged.

### MNT-007: Python Tooling Tests Are Not Run by CI

Priority: Medium

Estimated effort: Small

Category: Missing automation

The repository contains Python experiment-analysis and cost-estimator tooling
with 12 passing `unittest` tests under `tools/tests`. The GitHub Actions build
workflow discovers only C# test projects under `tests/*` and does not invoke the
Python suite.

Recommendation:

- Add a Python tooling job to the existing build workflow.
- Use the repository-standard `uv` workflow and locked dependencies.
- Run:

  ```powershell
  uv --cache-dir .uv-cache run python -m unittest discover -s tools/tests -v
  ```

- Consider adding a lightweight invocation test for the
  `experiment-analysis-report` console entry point.

Acceptance criteria:

- Pull requests run all tests under `tools/tests`.
- The job installs from `uv.lock` and fails on lockfile drift or test failure.
- The job does not require repository secrets.

### MNT-008: Python Dependencies Are Outside Current Update Automation

Priority: Medium

Estimated effort: Small

Category: Dependency automation

`.github/dependabot.yml` currently covers only NuGet and GitHub Actions. The
Python dependencies declared in `pyproject.toml` and resolved in `uv.lock` do
not have equivalent automated update coverage.

Recommendation:

- Add supported Python dependency-update automation for `pyproject.toml` and
  `uv.lock`, or add a scheduled locked-dependency audit if the chosen updater
  cannot maintain the uv lockfile correctly.
- Keep Python updates separate from the grouped NuGet update to make statistical
  tooling regressions easier to diagnose.

Acceptance criteria:

- Python dependency updates are proposed or audited automatically.
- Updated lockfiles are validated by the Python CI job from MNT-007.

### MNT-009: Several Direct Packages Have Newer Stable Versions

Priority: Medium

Estimated effort: Small to medium

Category: Outdated dependencies

The 2026-07-10 NuGet audit reported these newer stable versions:

| Package | Current | Available |
| --- | ---: | ---: |
| `AngleSharp` | 1.5.1 | 1.5.2 |
| `Google.Cloud.Firestore` | 4.2.0 | 4.3.0 |
| `Microsoft.Testing.Extensions.TrxReport` | 2.2.3 | 2.3.1 |
| `NodaTime` | 3.3.2 | 3.3.3 |
| `OpenAI` | 2.11.0 | 2.12.0 |
| `Spectre.Console.Testing` | 0.57.0 | 0.57.2 |
| `Testcontainers.Firestore` | 4.12.0 | 4.13.0 |
| `TUnit` | 1.55.2 | 1.58.0 |
| `WireMock.Net` | 2.11.0 | 2.12.0 |

Dependabot is configured for daily grouped NuGet updates, so implementation
should first check whether an existing dependency PR already covers these
versions.

Recommendation:

- Prioritize WireMock, Testcontainers, and TUnit because they overlap with
  MNT-001, MNT-002, and MNT-004.
- Upgrade the remaining patch/minor versions in a separately reviewable group.
- Rerun the complete C# test matrix after central package changes.

Acceptance criteria:

- The chosen package set is upgraded to the intended stable versions.
- Vulnerability, deprecation, and outdated audits are attached to the change.
- All C# test projects pass.

### MNT-010: Production-Community Classification Has Multiple Sources of Truth

Priority: Medium

Estimated effort: Small to medium

Category: Duplication / manual synchronization

The same production-community set is hard-coded independently in
`MatchdayCommand` and `BonusCommand`:

- `schadensfresse`
- `pes-squad`
- `ehonda-ai-arena`
- `rabetrabauken2026`

`.github/workflows/AGENTS.md` explicitly instructs maintainers to update both
sets whenever workflows change. Community names also appear throughout the
workflow entrypoints, so drift can silently change Langfuse environment tagging.

Recommendation:

- Extract a shared `ProductionCommunityPolicy` or equivalent configuration
  source for command-side classification.
- Keep matchday/bonus differences expressible even if their current sets are
  identical.
- Retain tests that verify production and development telemetry tagging.

Acceptance criteria:

- Each community classification is declared once in production code.
- Matchday and bonus policies can diverge deliberately without copying an
  entire implementation.
- Telemetry tests cover every configured production community and one unknown
  development community.

### MNT-011: Repository Style Is Documented but Not Mechanically Enforced

Priority: Low

Estimated effort: Small to medium

Category: Consistency / tooling

The repository contains detailed production and test style guides, but it has
no repository-owned `.editorconfig`. Formatting and many analyzer conventions
therefore depend on individual IDE settings and review discipline.

Recommendation:

- Add a conservative `.editorconfig` that captures already-dominant conventions
  without triggering an unrelated whole-repository rewrite.
- Add `dotnet format --verify-no-changes` or an equivalent focused formatting
  check only after establishing a clean baseline.
- Introduce analyzer severities incrementally after MNT-002 reaches zero
  compiler warnings.

Acceptance criteria:

- Common formatting behavior is reproducible across IDEs and CI.
- The initial configuration does not mix broad formatting churn with functional
  changes.

## Lower-Confidence Cleanup Candidates

These items should be verified before inclusion in an implementation plan:

- `Encrypt-Fixture.ps1` overlaps with the newer `snapshots encrypt` command and
  is not referenced by repository documentation. Confirm whether any manual
  workflow still needs it before retiring it.
- `Create-KpiDocument.ps1` and `Create-TransfersDocument.ps1` are also not
  referenced by repository documentation, but they create input/document
  templates rather than merely duplicating the upload commands. They should be
  documented, moved under `tools`, or explicitly retired after checking the
  manual workflow.
- The repository has 36 workflow YAML files, of which 21 are explicitly marked
  deactivated. `.github/workflows/AGENTS.md` says they are intentionally retained
  for reuse. Treat archive, deletion, or generation as a workflow-design
  decision rather than automatic dead-code cleanup.

## Second-Wave Structural Maintenance

The following production files are maintenance hotspots but are not the first
low-hanging changes to implement:

| File | Lines | Approximate methods |
| --- | ---: | ---: |
| `src/KicktippIntegration/KicktippClient.cs` | 2,838 | 58 |
| `src/FirebaseAdapter/FirebasePredictionRepository.cs` | 1,859 | 69 |
| `src/OpenAiIntegration/PredictionService.cs` | 1,284 | 41 |
| `PreparedExperimentRunExecutor.cs` | 1,275 | 40 |
| `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs` | 1,137 | 17 |
| `ExportExperimentAnalysisCommand.cs` | 950 | 36 |

Potential extraction boundaries include:

- HTTP transport versus Kicktipp HTML parsing and form construction;
- match, bonus, justification, and persistence mapping responsibilities;
- OpenAI request execution versus response mapping and telemetry;
- experiment batching, execution, scoring, and summary construction; and
- command orchestration versus reusable domain services.

These changes should be planned around behavior and test seams. Splitting files
into partial classes solely to reduce line counts would improve navigation but
would not reduce coupling.

## Suggested Implementation Slices

The future implementation plan can group the findings into the following
reviewable slices.

### Slice A: Dependency Risk and Supported APIs

Includes:

- MNT-001
- MNT-003
- MNT-004
- the WireMock, Testcontainers, and relevant Firestore portions of MNT-009

Validation:

- vulnerable and deprecated package audits;
- Release build;
- Firebase adapter tests;
- Kicktipp integration tests; and
- Orchestrator tests.

### Slice B: Warning-Free Build

Includes:

- MNT-002
- the TUnit portion of MNT-009

Validation:

- Release rebuild with 0 warnings;
- all C# tests; and
- warning-as-error CI verification.

### Slice C: Dead Code Removal

Includes:

- MNT-005
- verified items from the lower-confidence candidate list

Validation:

- repository reference searches;
- Release build; and
- relevant unit tests.

### Slice D: Shared Build Configuration and Enforcement

Includes:

- MNT-006
- MNT-011

Validation:

- compare evaluated properties before and after the move;
- clean restore and Release build;
- all C# tests; and
- formatting/analyzer checks if introduced.

### Slice E: Python Maintenance Automation

Includes:

- MNT-007
- MNT-008

Validation:

- local locked `uv` test run;
- pull-request CI run; and
- a test dependency-update proposal or scheduled audit.

### Slice F: Runtime Configuration Consistency

Includes:

- MNT-010

Validation:

- matchday, bonus, and random-match telemetry tests; and
- comparison against the active workflow community inventory.

## Decisions Needed Before Planning

The implementation plan should record decisions for these questions:

1. Should warnings be errors for every local build, or only in CI?
2. Should assembly and root-namespace names be standardized now, or should
   MNT-006 preserve all current names?
3. Should common test packages be inherited automatically by every project
   under `tests`, or should some remain explicit?
4. Which updater will own `uv.lock` maintenance?
5. Are the public dead records known to have any consumers outside this
   repository?
6. Should deactivated workflows remain callable, move to an archive, or be
   generated from a smaller configuration source?
7. Are the three unreferenced root PowerShell scripts still part of supported
   manual workflows?

## Conclusion

The repository is functionally healthy: it builds successfully, has a broad C#
test workflow, and its Python tooling tests pass. The clearest maintenance debt
is concentrated rather than systemic:

- one vulnerable test dependency branch;
- 189 build warnings dominated by mechanical test migrations;
- duplicated obsolete Firestore credential construction;
- a small set of high-confidence dead declarations;
- repeated project configuration; and
- Python tooling that is tested locally but not by CI.

Addressing MNT-001 through MNT-008 would provide the highest return before
starting larger source-file decomposition work.
