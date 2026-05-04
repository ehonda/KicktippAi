# KicktippAi Agent Context

This document contains context relevant when working on tasks in this repository.

@AUTO-REVIEW.md

## GitHub Copilot Configuration

### Workaround for truncated output when using the `run_in_terminal` tool

This is a workaround for this [issue](https://github.com/microsoft/vscode/issues/299486).

**CRITICAL**: When using the `run_in_terminal` tool, **ALWAYS USE `"timeout": 0`**. Otherwise, outputs from commands will be silently truncated if they time out, which happens often on our slow local machine.

### Invoking Powershell Commands

When invoking Powershell commands via `run_in_terminal`, don't prefix them with `&`, and don't quote the script path.

```powershell
# ❌ Avoid this:
& .github/copilot/skills/submodules-display-tree/scripts/Display-Tree.ps1 -Format tree -Depth 2

# ✅ Do this instead:
.github/copilot/skills/submodules-display-tree/scripts/Display-Tree.ps1 -Format tree -Depth 2
```

### Running Parallel Powershell Work

When a workflow says to run independent commands in parallel, do not place the commands on one line separated by `;`. Semicolon-chained Powershell commands run sequentially. Use `Start-Job` or separate terminal tasks to launch all commands first, then wait for all of them with `Wait-Job`, collect output with `Receive-Job`, and fail the workflow if any job failed.

For Langfuse experiment run families, create one shared `$runStamp` before launching jobs, pass that stamp into every job's run name, and start all jobs before waiting for any one of them.

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

The hosted Langfuse prompt route is still a POC and remains opt-in for experiment runs. Production and default local experiment runs keep using file-based prompts.

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
