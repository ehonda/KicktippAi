# Codex Efficient Usage

## Purpose

This note documents the workflow changes we applied after the `Update prediction identity` session analysis so future Codex work in `KicktippAi` stays inside the sandbox where it works well, while the commands that still misbehave are clearly called out.

## Fresh Clone Setup

On a fresh clone, do this once before relying on sandboxed `git` and `dotnet` workflows in Codex:

1. Trust the clone path for Git:

```powershell
git config --global --add safe.directory C:/path/to/your/KicktippAi-clone
```

2. If your local Codex sandbox is allowed to write to the default Windows user directories, put the machine-specific `dotnet` and NuGet path overrides into your user-level `~/.codex/config.toml`.

3. Restart Codex after changing either `~/.codex/config.toml` or [`.codex/config.toml`](../../.codex/config.toml), because the env setup is applied when the session starts.

The repo [AGENTS.md](../../AGENTS.md) keeps the agent-relevant search-scope guidance, `.tmp/` scratch-state note, and current Git guidance, while the one-time clone setup details stay here.

## Native Windows Sandbox Mode

The repo-local [`.codex/config.toml`](../../.codex/config.toml) in this repo only carries machine-agnostic `dotnet` flags. It does not choose the native Windows sandbox backend, and it does not contain machine-local absolute paths.

That backend is a user-level Codex setting in `~/.codex/config.toml`:

```toml
[windows]
sandbox = "elevated" # or "unelevated"
```

Important distinction:

- `elevated` is the preferred native Windows sandbox, but it depends on administrator-approved local setup.
- `unelevated` is the documented fallback when the elevated setup path is blocked or broken in the local environment.

If Codex starts showing Windows "modify the system" prompts for routine sandboxed commands, or even simple read-only commands fail with `CreateProcessWithLogonW failed: 1326`, check the user-level setting first. In that situation, switching the user-level mode to `unelevated` and restarting Codex restores the older no-admin-prompt fallback behavior on this machine.

## Current Status

### 1. Git trust is fixed, but mutating `git` commands still need escalation

The earlier workaround was to run all `git` commands outside the sandbox because sandboxed runs could hit Git's dubious-ownership protection.

We replaced that with a one-time machine setup:

```powershell
git config --global --add safe.directory C:/Users/dennis/source/repos/ehonda/KicktippAi
```

Effect:

- Routine read-only `git` commands such as `status`, `diff`, and `log` can run inside the sandbox again without hitting dubious-ownership errors.
- If the repo is cloned to a different path, rerun the same command with that clone path.

Follow-up attempt on `2026-06-01`:

- We tried a repo-local Codex permission profile that reopened `.git` for writes and allowed GitHub network access.
- After restarting Codex, `git add .codex/config.toml` still failed with `Unable to create '.git/index.lock': Permission denied`.
- Sandboxed `git ls-remote origin` succeeded, but `git push --dry-run origin main` still hung long enough to be abandoned as unreliable.

Current working guidance:

- Keep using the sandbox for routine read-only `git` commands.
- Run `git add`, `git commit`, and `git push` outside the sandbox in this repo for now.

### 2. Sandbox `dotnet` uses user-level absolute paths to the default Windows directories

The earlier workaround was to run all `dotnet` commands outside the sandbox because sandboxed runs could not reliably use the default user-profile locations for CLI and NuGet writable state.

We first replaced that with repo-local relative paths under `.tmp/`, but that caused two follow-up problems on this machine:

- some runs still tripped over relative-path handling
- the repo-local NuGet cache duplicated the normal machine-wide package store and wasted disk space

The current approach is:

- keep only machine-agnostic `dotnet` flags in the repo-local [`.codex/config.toml`](../../.codex/config.toml)
- put machine-specific absolute path overrides into the user-level `~/.codex/config.toml`

Example user-level setup:

```toml
[shell_environment_policy.set]
DOTNET_CLI_HOME = "C:\\Users\\<user>"
NUGET_PACKAGES = "C:\\Users\\<user>\\.nuget\\packages"
NUGET_HTTP_CACHE_PATH = "C:\\Users\\<user>\\AppData\\Local\\NuGet\\v3-cache"
NUGET_PLUGINS_CACHE_PATH = "C:\\Users\\<user>\\AppData\\Local\\NuGet\\plugins-cache"
NUGET_SCRATCH = "C:\\Users\\<user>\\AppData\\Local\\Temp\\NuGetScratch"
DOTNET_NOLOGO = "1"
DOTNET_CLI_TELEMETRY_OPTOUT = "1"
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"
DOTNET_GENERATE_ASPNET_CERTIFICATE = "0"
```

Effect:

- `dotnet` and NuGet use the normal absolute Windows user directories instead of a duplicate repo-local cache.
- The machine-specific absolute paths stay out of the committed repo.
- The setup depends on the local Codex sandbox being allowed to write to those user directories.

Important assumptions:

- Restart Codex after changing `~/.codex/config.toml`. The user-level env setup is applied when the session starts.
- Keep machine-specific absolute paths out of committed repo files.
- If the sandbox can no longer write to the default user directories, this setup will stop working and you will need either a sandbox permission change or a different writable path strategy.

### 3. Repo-local scratch state is explicitly ignored

We added `.tmp/` to [`.gitignore`](../../.gitignore) so ad-hoc repo-local scratch state stays out of version control.

### 4. Broad searches skip dependency submodules by default

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

The higher-level Codex instructions should not say to always run `dotnet` outside the sandbox for this repo.

The repo guidance in [AGENTS.md](../../AGENTS.md) now keeps the agent-relevant search-scope guidance, `.tmp/` scratch-state note, and the current exception for mutating `git` commands.

Fresh-clone and one-time setup details stay here instead:

- use the user-level Codex config for machine-specific `dotnet` and NuGet paths
- trust the repo once for sandboxed `git`
- rerun the `safe.directory` command if the clone path changes

## Expected Outcome

These changes should reduce:

- approval-review fork traffic caused by routine read-only `git` and `dotnet` commands
- extra transcript growth from approval prompts and review payloads
- extra transcript growth from broad first-pass searches that include dependency mirrors
- friction from repo-local `.dotnet` or user-profile cache issues during sandboxed `dotnet` runs

## Current Git Limitation

As of `2026-06-01`, the attempted sandbox Git follow-up is still not good enough to rely on:

- `.git/index.lock` creation still fails for `git add`
- `git push --dry-run origin main` still did not complete reliably in the sandbox

The practical outcome is unchanged for mutating Git operations:

- use the sandbox for read-only `git` work
- run `git add`, `git commit`, and `git push` outside the sandbox with approval

## Related Analysis

- Detailed investigation copy: [codex-session-analysis-2026-05-31-update-prediction-identity.md](./codex-session-analysis-2026-05-31-update-prediction-identity.md)
