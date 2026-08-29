# KicktippAi Agent Context

This document contains context relevant when working on tasks in this repository.

@AUTO-REVIEW.md

## Running Parallel Powershell Work

When a workflow says to run independent commands in parallel, do not place the commands on one line separated by `;`. Semicolon-chained Powershell commands run sequentially. Use `Start-Job` or separate terminal tasks to launch all commands first, then wait for all of them with `Wait-Job`, collect output with `Receive-Job`, and fail the workflow if any job failed.

For Langfuse experiment run families, create one shared `$runStamp` before launching jobs, pass that stamp into every job's run name, and start all jobs before waiting for any one of them.

## Subagent Model Allocation

This section explicitly authorizes model and reasoning-effort overrides for subagent spawns.

Before the first spawn in a workflow or work wave, classify each planned role and record its model, reasoning effort, fork strategy, and a concise justification. A role mapping may be recorded once and reused for equivalent tasks in the same wave.

Use these starting points:

- Mechanical CI/status/exact-SHA checks and deterministic lookups: `gpt-5.6-luna` / `low`.
- Bounded, well-defined read-only exploration: `gpt-5.6-luna` / `medium` or `gpt-5.6-terra` / `medium`, depending on breadth and ambiguity.
- Normal bounded implementation and deterministic fixes: `gpt-5.6-terra` / `medium`; raise to `high` when the implementation has substantial ambiguity, integration risk, or difficult edge cases.
- Independent correctness, security, or regression review: prefer `gpt-5.6-sol` / `high`. Review establishes whether work is safe to accept, so do not routinely assign it to the cheapest adequate tier.
- Open-ended or complex research whose conclusions will guide later design or implementation: prefer `gpt-5.6-sol` / `high`. Use a lighter model only when the question is bounded, evidence gathering is mechanical, and the result will receive stronger independent synthesis or review.
- Ambiguous cross-cutting work, launch gates, architecture decisions, or difficult failure analysis: `gpt-5.6-sol` / `high`.

`gpt-5.6-sol` / `xhigh` is exceptional for task agents. Before selecting it, state why `gpt-5.6-sol` / `high` is insufficient for the task's difficulty or risk.

Every override-compatible spawn must explicitly set both `model` and `reasoning_effort`. Omitting either field is a protocol violation.

Use `fork_turns: "none"` or the smallest bounded positive history when the child should differ from the parent. Do not choose a full-history fork merely for convenience. A full-history fork is allowed only when the child intentionally needs the parent's exact model and reasoning effort; record that reason explicitly.

Before repeating an allocation pattern, verify that the first realized child used the intended model and reasoning effort. If it unexpectedly inherited the orchestrator configuration, stop that pattern and correct the spawn strategy.

When a task changes role materially—for example, from mechanical evidence collection to open-ended analysis—reclassify it before assigning a follow-up turn or reuse a differently configured agent.

## Gathering Information

We use different external dependencies, some of which are partially or fully available locally via git submodules.

For routine repository searches, start with repo-owned paths such as `src`, `tests`, `.github`, `.agents`, and only add `docs` when the task needs them.

The repo-root `.ignore` intentionally excludes `external/` from broad `rg` and `rg --files` searches so dependency mirrors do not pollute first-pass results.

Search `external/` only when the task is clearly dependency-specific or when repo-local code points to a dependency. When a submodule is relevant, search the narrowest submodule path directly, for example `rg -n "ChatClient" external/openai/openai-dotnet`. Use `--no-ignore` when you need file discovery inside an ignored tree, for example `rg --no-ignore --files external/openai/openai-dotnet`.

When gathering dependency information like

- Code
- Documentation
- Usage examples

search it in the following places, in that order:

1. The relevant local git submodule (See [Submodule Tree](#submodule-tree))
2. GitHub via MCP
3. Web search

## CSV Context Documents

When creating or updating CSV context documents that may appear in prompts or Langfuse trace views, match the rendering style used by the FIFA ranking docs:

- The first byte of the content should be the first header character; do not add leading blank lines.
- The header row and first data row must be separated by exactly one line terminator.
- Use one record per line and keep rows in deterministic order.
- End every CSV content string with a final trailing line terminator.
- Prefer CRLF line endings for generated CSV context stored in Firestore, matching the currently cleanly rendered ranking documents.
- Use empty fields for genuinely blank values. Use an explicit sentinel such as `N/A` for unavailable supplemental values where an empty field would be ambiguous; do not use `0` to mean unknown.
- For large integer money-like values, use readable thousands separators that do not conflict with the CSV delimiter, for example `15.000.000` for EUR values.

## Git Submodules

### Submodule Tree

@agent-files/submodule-tree.txt

### Updating the Submodules

When you encounter a dependency that is not available locally, and which has a chance of being consulted multiple times, use the `submodules-manage` skill to add it or part of it as a git submodule. This will make it available locally for future reference and easy agentic access.

## Langfuse Agent Tooling

@agent-files/langfuse-agent-tooling.md

Use the installed `langfuse` CLI entrypoint for Langfuse API work. Do not use `npx langfuse-cli` for routine agent workflows with repository secrets; install or update the global `langfuse-cli` package only when the `langfuse` command is missing or stale.

## Langfuse Experiments

The initial Langfuse integration is complete. Treat the active repository docs as the source of truth, and treat the old phase trackers as historical design context.

- For current Langfuse docs, generic API access, prompt management, SDK guidance, and prompt migration, use the official global `$langfuse` skill and the global Langfuse tooling described above.
- For verified repository-specific Langfuse tracing and filtering behavior, read [docs/langfuse.md](docs/langfuse.md).
- For active experiment preparation, execution, analysis, and publishing workflows, read [docs/langfuse/experiments](docs/langfuse/experiments).
- Use `.agents/skills/langfuse-experiments/` for KicktippAi-specific experiment orchestration, statistical report generation, Pages verification, and commit/push workflow.
- Read `plans/langfuse-integration/phase-2/AGENTS.md` and linked trackers only when researching historical implementation decisions or changing experiment behavior.

Hosted Langfuse prompts are an established runtime path, not merely a POC. WM26 used hosted prompts as primary with checked-in files as the outage/first-fetch fallback, and Bundesliga 2026/27 follows the same pattern. Competition configuration and its accepted ADR determine the prompt names and labels; scheduled production must use an explicitly promoted version rather than a floating `latest` label.

## Prediction Validation Safety

- Agents may autonomously write test predictions to `ehonda-dev-buli-2627` only with `gpt-5.6-luna`, reasoning effort `none`, and an explicitly pinned output cap. Prediction quality is irrelevant in this community; use it only to validate plumbing.
- The same Luna/none participant in `ehonda-ai-arena` is authorized for the Bundesliga validation ladder: local CLI, `workflow_dispatch`, then an arena-only schedule with result, Firestore, Langfuse, and ordering inspection.
- Never silently promote the Luna/none validation configuration to production. The project owner selects and approves the final production model, reasoning effort, output cap, prompt versions, cost ceiling, and arena challenger matrix.
- For local community writes, load the matching sibling `.env.<community>` credentials where available. Do not swap or overwrite the base development `.env`, and never print secret values while inspecting configuration.
- Final production schedules remain disabled until the Bundesliga activation task's manual evidence and owner-controlled decisions pass. See [the Bundesliga execution strategy](plans/bundesliga-2026-27/execution-strategy.md), [ADR-0005](plans/bundesliga-2026-27/decisions/0005-launch-community-and-prediction-topology.md), and [ADR-0006](plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md).

## Python Tooling

Use `uv` to manage everything Python-related in this repository, including interpreter selection, virtual environments, dependencies, and repo-local command execution.

When running `uv` from Codex, prefer the repo-local cache form:

```powershell
uv --cache-dir .uv-cache run ...
```

The default Windows uv cache under `%LOCALAPPDATA%` can be blocked by sandbox permissions. The `.uv-cache/` directory is ignored by git. If a `uv` command still fails due to permissions, needs network access, or needs unrestricted external secrets access, rerun that same command outside the sandbox with approval.

When validating Codex skills with the global `skill-creator` validator, use `uv --with PyYAML` because the ambient Python environment often does not have the `yaml` module installed:

```powershell
uv --cache-dir .uv-cache run --with PyYAML python C:\Users\dennis\.codex\skills\.system\skill-creator\scripts\quick_validate.py path\to\skill-folder
```

If this command fails because `PyYAML` needs to be fetched and sandbox networking blocks PyPI, rerun the same command outside the sandbox with approval.

## Codex Sandbox State

This repo does not currently configure `dotnet` or NuGet path overrides through [`.codex/config.toml`](.codex/config.toml).

- `.tmp/` is ignored by git and is safe for ad-hoc repo-local scratch state when a task needs it.
- Run all `dotnet` commands outside the sandbox in this repo for now.
- Routine read-only `git` commands such as `status`, `diff`, and `log` can run in the sandbox, but `git add`, `git commit`, and `git push` should still be run outside the sandbox in this repo for now.
- Fresh-clone setup and one-time trust steps are documented in [docs/codex/efficient-usage.md](docs/codex/efficient-usage.md).

## Running and Filtering Tests

This project uses TUnit for testing, which has some differences compared to more common frameworks like xUnit or NUnit. They are documented in the following sections.

### Running Tests

Always use `dotnet run` instead of `dotnet test` to run TUnit tests:

```powershell
dotnet run --project tests/MyProject.Tests
```

To see available command-line options:

```powershell
dotnet run --project tests/MyProject.Tests -- --help
```

### Filtering Tests

Use `--treenode-filter` to run specific tests. The filter syntax is:

```text
/<Assembly>/<Namespace>/<Class>/<Test>
```

Use `*` as a wildcard and `**` for multi-level matching.

**Common Filter Patterns:**

| Goal | Command |
|------|---------|
| Run all tests in a class | `dotnet run -- --treenode-filter "/*/*/MyTestClass/*"` |
| Run a specific test | `dotnet run -- --treenode-filter "/*/*/*/My_test_name"` |
| Run tests matching a prefix | `dotnet run -- --treenode-filter "/*/*/*/Adding_*"` |
| Run all tests in matching classes | `dotnet run -- --treenode-filter "/*/*/MyService*/**"` |

**Combining Filters:**

Use `&` (AND) and `|` (OR) operators. OR requires parentheses at the name level:

```powershell
# Tests starting with "Valid" OR "Invalid"
dotnet run -- --treenode-filter "/*/*/*/(Valid*)|(Invalid*)"
```

**Filtering by Properties:**

Filter tests by custom properties using `[PropertyName=Value]`:

```powershell
dotnet run -- --treenode-filter "/*/*/*/*[Category=Unit]"
```

### Copilot Auto-Approval Workaround for TUnit Filters

When running filtered TUnit commands through Copilot's terminal tool in PowerShell, inline `--treenode-filter "/*/*/.../*"` arguments may fail terminal auto-approval because VS Code sometimes parses the command as having no sub-commands.

This is a Copilot/VS Code parser workaround, not a TUnit requirement. In a normal terminal, the inline form is still fine.

If you want reliable terminal auto-approval in Copilot, put the filter into a variable first:

```powershell
$filter = '/*/*/MyTestClass/*'
dotnet run --project tests/Orchestrator.Tests -- --treenode-filter $filter
```

This variable-based form has been observed to auto-approve reliably, while the inline quoted filter often does not.

### Listing Available Tests

To see all available tests without running them:

```powershell
dotnet run -- --list-tests
```
