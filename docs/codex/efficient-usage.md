# Codex Efficient Usage

## Purpose

This note documents the workflow changes we applied after the `Update prediction identity` session analysis so future Codex work in `KicktippAi` stays inside the sandbox where it works well, while the commands that still misbehave are clearly called out.

## Fresh Clone Setup

On a fresh clone, do this once before relying on sandboxed `git` and `dotnet` workflows in Codex:

1. Trust the clone path for Git:

```powershell
git config --global --add safe.directory C:/path/to/your/KicktippAi-clone
```

The repo [AGENTS.md](../../AGENTS.md) keeps the agent-relevant search-scope guidance, current sandbox notes, and Git guidance, while the one-time clone setup details stay here.

## Native Windows Sandbox Mode

The repo-local [`.codex/config.toml`](../../.codex/config.toml) in this repo does not choose the native Windows sandbox backend.

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

Why we are not retrying this casually:

- The one-time `safe.directory` setup was worth keeping because it fixed routine read-only `git` commands.
- The follow-up attempt to reopen `.git` for writes and allow GitHub network access was not reliable enough to replace the outside-sandbox workflow.
- On native Windows `unelevated` sandbox mode, the repo-local permission-profile experiment also reintroduced permission-prompt friction, so it made the fallback workflow worse instead of better.
- If mutating sandboxed `git` is ever revisited, only retry it with a clearly different platform/runtime setup, for example a working `elevated` sandbox path or WSL, and revalidate `git add`, `git commit`, and `git push` explicitly.

### 2. `dotnet` currently runs outside the sandbox

The earlier workaround was to run all `dotnet` commands outside the sandbox because sandboxed runs could not reliably use the default user-profile locations for CLI and NuGet writable state.

We tried two sandboxed `dotnet` variants:

- repo-local relative paths under `.tmp/`
- user-level absolute paths to the default Windows user directories

Neither variant is part of the current workflow anymore. The path-override experiments were rolled back, and this repo no longer sets `dotnet` or NuGet path environment variables through Codex config.

Current working guidance:

- run `dotnet` commands outside the sandbox in this repo
- do not rely on repo-local or user-level Codex env overrides for `dotnet` here

Important assumptions:

- if a future `dotnet` sandbox optimization is retried, treat it as an experiment until the full repo build is revalidated
- keep machine-specific path overrides out of committed repo files unless they are intentionally being tested and documented

Why we are not retrying this casually:

- The repo-local relative-path variant under `.tmp/` did not stay boring. It added duplicate cache state, created path-handling confusion, and complicated debugging.
- The user-level absolute-path variant looked better for individual `dotnet` commands, but the overall workflow became harder to trust and reason about on this machine.
- After reverting all `dotnet` env overrides and going back to outside-sandbox `dotnet`, the repo's normal root-level `dotnet restore` plus `dotnet build --no-restore --configuration Release` flow worked again.
- Because build reliability matters more than shaving approval overhead, do not retry sandboxed `dotnet` here unless you are prepared to revalidate the full root-level build flow and accept rollback work if it regresses.

### 3. Failed Optimization Summary

Two Codex sandbox optimizations were explored and then intentionally abandoned:

1. Mutating `git` in the sandbox via repo-local permission profiles.
2. `dotnet` in the sandbox via repo-local or user-level path env overrides.

Shared lesson:

- a small local command success was not enough proof
- the repo's real workflow had to be revalidated end to end
- when the optimization made the machine-specific behavior harder to trust, rollback was the right outcome

### 4. Repo-local scratch state is explicitly ignored

We added `.tmp/` to [`.gitignore`](../../.gitignore) so ad-hoc repo-local scratch state stays out of version control.

### 5. Broad searches skip dependency submodules by default

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

The higher-level Codex instructions should say to run `dotnet` outside the sandbox for this repo.

The repo guidance in [AGENTS.md](../../AGENTS.md) now keeps the agent-relevant search-scope guidance, `.tmp/` scratch-state note, and the current exception for mutating `git` commands.

Fresh-clone and one-time setup details stay here instead:

- trust the repo once for sandboxed `git`
- rerun the `safe.directory` command if the clone path changes

## Expected Outcome

These changes should reduce:

- approval-review fork traffic caused by routine read-only `git` commands
- extra transcript growth from approval prompts and review payloads
- extra transcript growth from broad first-pass searches that include dependency mirrors
- friction from partially working `dotnet` sandbox path experiments

Tradeoff:

- `dotnet` commands keep their outside-sandbox approval overhead for now, but the workflow is simpler and less likely to hide machine-specific failures

## Current Git Limitation

As of `2026-06-01`, the attempted sandbox Git follow-up is still not good enough to rely on:

- `.git/index.lock` creation still fails for `git add`
- `git push --dry-run origin main` still did not complete reliably in the sandbox

The practical outcome is unchanged for mutating Git operations:

- use the sandbox for read-only `git` work
- run `git add`, `git commit`, and `git push` outside the sandbox with approval

## Related Analysis

- Detailed investigation copy: [codex-session-analysis-2026-05-31-update-prediction-identity.md](./codex-session-analysis-2026-05-31-update-prediction-identity.md)
