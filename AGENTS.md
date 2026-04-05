# KicktippAi Agent Context

This document contains context relevant when working on tasks in this repository.

## GitHub Copilot Configuration

### Workaround for truncated output when using the `run_in_terminal` tool

This is a workaround for this [issue](https://github.com/microsoft/vscode/issues/299486).

**CRITICAL**: When using the `run_in_terminal` tool, **ALWAYS USE `"timeout": 0`**. Otherwise, outputs from commands will be silently truncated if they time out, which happens often on our slow local machine.

### Invoking Powershell Commands

When invoking Powershell commands via `run_in_terminal`, don't prefix them with `&`, and don't quote the script path.

````powershell
# ❌ Avoid this:
& .github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "scores" -QueryParams @{limit=10; name='avg_kicktipp_points'}

# ✅ Do this instead:
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "scores" -QueryParams @{limit=10; name='avg_kicktipp_points'}
```

## Gathering Information

We use different external dependencies, some of which are partially or fully available locally via git submodules. When gathering information like

- Code
- Documentation
- Usage examples

search it in the following places, in that order:

1. Local git submodules (See [Submodule Tree](#submodule-tree))
2. GitHub via MCP
3. Web search

## Git Submodules

### Submodule Tree

@agent-files/submodule-tree.txt

### Updating the Submodules

When you encounter a dependency that is not available locally, and which has a chance of being consulted multiple times, use the `submodules-manage` skill to add it or part of it as a git submodule. This will make it available locally for future reference and easy agentic access.

## Langfuse Experiments

For Langfuse evaluation and experiment work, prefer the Python SDK first because Langfuse's experiment runner, examples, and evaluation integrations are strongest there.

- Keep JS/TS as the fallback when local Python tooling becomes the main source of friction
- Before starting Phase 2 implementation work, read `plans/langfuse-integration/phase-2/AGENTS.md` and the linked tracker documents there
- For verified repository-specific Langfuse tracing and filtering behavior, read [docs/langfuse.md](docs/langfuse.md)

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
