# Codex Efficient Usage

## Purpose

This note documents the fixes we applied after the `Update prediction identity` session analysis so future Codex work in `KicktippAi` stays inside the sandbox more often and triggers less approval-review overhead.

## Fresh Clone Setup

On a fresh clone, do this once before relying on sandboxed `git` and `dotnet` workflows in Codex:

1. Start Codex from the repo root so the relative paths in [`.codex/config.toml`](../../.codex/config.toml) resolve into the repo's `.tmp/` directory.
2. Trust the clone path for Git:

```powershell
git config --global --add safe.directory C:/path/to/your/KicktippAi-clone
```

3. Restart Codex after changing [`.codex/config.toml`](../../.codex/config.toml), because the repo-local env setup is applied when the session starts.

The repo [AGENTS.md](../../AGENTS.md) keeps the agent-relevant search-scope guidance and `.tmp/` scratch-state note, while the one-time clone setup details stay here.

## Resolved Issues

### 1. Sandbox `git` no longer needs a forced escalation workaround

The earlier workaround was to run all `git` commands outside the sandbox because sandboxed runs could hit Git's dubious-ownership protection.

We replaced that with a one-time machine setup:

```powershell
git config --global --add safe.directory C:/Users/dennis/source/repos/ehonda/KicktippAi
```

Effect:

- Routine `git` commands such as `status`, `diff`, `log`, `add`, and `commit` can run inside the sandbox again.
- We no longer need a standing instruction that forces every `git` command outside the sandbox.
- If the repo is cloned to a different path, rerun the same command with that clone path.

### 2. Sandbox `dotnet` now uses repo-local writable paths

The earlier workaround was to run all `dotnet` commands outside the sandbox because sandboxed runs could not reliably use the default user-profile locations for CLI and NuGet writable state.

We replaced that with a repo-local Codex config in [`.codex/config.toml`](../../.codex/config.toml):

```toml
[shell_environment_policy.set]
DOTNET_CLI_HOME = ".tmp/dotnet-cli-home"
NUGET_PACKAGES = ".tmp/nuget/packages"
NUGET_HTTP_CACHE_PATH = ".tmp/nuget/v3-cache"
NUGET_PLUGINS_CACHE_PATH = ".tmp/nuget/plugins-cache"
NUGET_SCRATCH = ".tmp/nuget/scratch"
DOTNET_NOLOGO = "1"
DOTNET_CLI_TELEMETRY_OPTOUT = "1"
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"
DOTNET_GENERATE_ASPNET_CERTIFICATE = "0"
```

Effect:

- `dotnet` and NuGet write their temporary and cache state under `.tmp/` in the repo.
- We avoid writes to user-profile locations such as `%USERPROFILE%\\.dotnet` and `%USERPROFILE%\\.nuget`, which were the problematic paths in the sandbox.
- The config is clone-portable because it uses relative paths.

Important assumptions:

- Start Codex from the repo root. These relative paths are intended to resolve from the root session working directory.
- Restart Codex after changing `.codex/config.toml`. The repo-local env setup is applied when the session starts.

### 3. Repo-local scratch state is now explicitly ignored

We added `.tmp/` to [`.gitignore`](../../.gitignore) so the repo-local `dotnet` and NuGet scratch directories stay out of version control.

### 4. Broad searches now skip dependency submodules by default

The earlier problem was that broad `rg` and `rg --files` searches pulled dependency mirrors from `external/` into first-pass results, which inflated transcript size and made narrowing slower.

We replaced that with two repo-local changes:

- a repo-root [`.ignore`](../../.ignore) that excludes `external/` from broad ripgrep searches
- updated search guidance in [AGENTS.md](../../AGENTS.md) that keeps first-pass searches in repo-owned paths and only searches a relevant submodule when the task is dependency-specific

The ignore rule is intentionally small:

```text
external/
```

Effect:

- broad `rg` and `rg --files` searches no longer surface dependency mirrors by default
- dependency lookups still work by targeting the relevant submodule path directly
- when file discovery inside an ignored submodule is needed, use `rg --no-ignore --files external/<owner>/<repo>`

## Instruction Changes

The higher-level Codex instructions should no longer say to always run `git` or `dotnet` outside the sandbox for this repo.

The repo guidance in [AGENTS.md](../../AGENTS.md) now keeps the agent-relevant search-scope guidance and `.tmp/` scratch-state note.

Fresh-clone and one-time setup details stay here instead:

- use the repo-local Codex config for `dotnet`
- trust the repo once for sandboxed `git`
- rerun the `safe.directory` command if the clone path changes

## Expected Outcome

These changes should reduce:

- approval-review fork traffic caused by routine `git` and `dotnet` commands
- extra transcript growth from approval prompts and review payloads
- extra transcript growth from broad first-pass searches that include dependency mirrors
- friction from repo-local `.dotnet` or user-profile cache issues during sandboxed `dotnet` runs

## Current Push Behavior

As of `2026-05-31`, sandboxed remote Git access is still blocked in this environment.

We tested:

```powershell
git ls-remote --heads origin
```

and it failed with a connection error to `github.com:443`.

Effect:

- the `safe.directory` fix from issue 1 removed the dubious-ownership workaround for routine local `git` commands
- `git push` still needs escalation here because sandbox network access is blocked
- the global push approval workflow is therefore still relevant for now

## Related Analysis

- Detailed investigation copy: [codex-session-analysis-2026-05-31-update-prediction-identity.md](./codex-session-analysis-2026-05-31-update-prediction-identity.md)
