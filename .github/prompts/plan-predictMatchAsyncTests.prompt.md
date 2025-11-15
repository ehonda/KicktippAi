Research synthesized; drafting options-focused plan next.

## Plan: Reduce PredictMatch Arrange Repetition

Group the shared Arrange logic for `PredictMatchAsync` into reusable, TUnit-aligned helpers so each test only specifies its scenario deltas (response JSON, token usage, flags) while everything else defaults via the existing base class.

### Steps
1. Capture current duplication in `tests/OpenAiIntegration.Tests/PredictionServiceTests/PredictionService_PredictMatchAsync_Tests.cs` and list the varying knobs (response payload, token usage, `includeJustification`, context docs).
2. Extend `tests/OpenAiIntegration.Tests/PredictionServiceTests/PredictionServiceTests_Base.cs` with a `PredictMatchScenario` helper (factory or fixture) that builds service/match/docs, accepts optional overrides, executes `PredictMatchAsync`, and returns a context object for assertions.
3. Evaluate a TUnit `[MethodDataSource]` or `[Arguments]`-driven approach so multiple scenarios can share a single test body while configuring overrides through a record (e.g., `PredictMatchTestCase`).
4. Optionally design a shared-context fixture (per `tests/tunit_usage_guide/usage_patterns/shared_context.md`) or attribute to inject the helper context directly into tests, ensuring easy reuse for `PredictBonusQuestion` suites as well.
5. Align documentation/examples in `tests/tunit_usage_guide/usage_patterns/parameterized_tests.md` and `.shared_context.md` with the chosen approach to keep future contributors on the same page.

### Further Considerations
1. Should we prefer helper-method orchestration (easier adoption) or go straight to parameterized/fixture injection for maximal reuse?
