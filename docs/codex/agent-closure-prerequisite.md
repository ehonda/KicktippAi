# Explicit subagent closure prerequisite

**Status:** unresolved merge and activation blocker

**Recorded:** 2026-09-03

This prerequisite blocks merging the orchestration follow-up PR and starting a
new `$orchestrate` run under its workflow. It does not block preparing,
validating, independently reviewing, pushing, or running CI on the draft PR.
The current P1 orchestration may continue under the existing workflow.

The lifecycle policy now decides when a specialist is no longer worth
retaining. It must not claim that thread capacity was reclaimed until Codex can
explicitly close the thread and the client confirms the result. No missing-tool
fallback is selected here; the owner will choose policy after a separate
fresh-session investigation.

## Current state of knowledge

The following observations were made while checking out the PR branch:

- The injected collaboration surface exposed `spawn_agent`, `followup_task`,
  `send_message`, `interrupt_agent`, `list_agents`, and `wait_agent`, but not
  `close_agent`.
- Current command path observed in this session:
  `C:\Users\dennis\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe`
  (version `0.153.0`).
- `codex features list` reports `multi_agent` as `stable true` on that binary.
- User-level config (`C:\Users\dennis\.codex\config.toml`) now points
  `CODEX_CLI_PATH` at:
  `C:\Users\dennis\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe`.
- Running that configured executable directly reports:
  `codex-cli 0.153.0`.
- Repo-scoped `.codex/config.toml` only contains:
  `[agents] max_concurrent_threads_per_session = 8`.
- The highest-probability root cause remains the negotiated per-session tool
  surface; the repository config is not the blocker.
- User has already done a full restart before this update.
- This run re-generated the app-server protocol schema and still did not find
  `close_agent` anywhere in the tool schema, so capability exposure is still
  inconsistent with docs and expected `multi_agent` tool list.
- Official docs still state subagent closure support exists, which keeps the gap
  at host/client-level behavior rather than a known policy gap.

Additional checks and external evidence:

- `openai/codex` issue `#36211` is an active report explicitly titled
  `close_agent is missing from the VS Code multi-agent tool schema`.
- Adjacent historical reports (`#24389`, `#35435`, `#37761`) also describe
  lifecycle, leak, or stale-complete-agent behavior around subagent teardown.
- Current GitHub API access from this environment is blocked by local proxy
  settings, so no new issue/release refresh was possible in this exact run.

Official OpenAI documentation currently says that:

- stable `features.multi_agent` includes `close_agent`, and
- the app, CLI, and IDE can be asked to close completed subagent threads.

Sources:

- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference#configtoml)
- [Codex subagents: managing subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents#managing-subagents)

The mismatch could be stale client/version skew, host/tool-generation variance, or
staged feature-path differences. None is proven yet.

## Checkpoint status (this branch check)

- ✅ `codex/orchestration-follow-up-fixes` checked out in isolated worktree.
- ✅ CLI version/path/config evidence captured.
- ✅ User-level configured CLI path now points to updated launcher path (`bin\codex.exe`).
- ✅ User reports a fresh restart was already performed before this checkpoint
  update.
- ⚠️ Missing `close_agent` exposure is still not yet demonstrated end-to-end in a
  fresh user session transcript.
- ☐ GitHub issue/release re-check blocked by local network/proxy access.

## Fresh-session investigation

Run this in a separate, deliberately small, non-`$orchestrate` session after
the draft PR is otherwise exact-head green and reviewed.

1. Record the desktop/app and CLI versions, effective launch path, relevant
   config layers, enabled multi-agent feature, and collaboration operations
   exposed to the new session. Do not record secrets.
2. Search the official `openai/codex` GitHub issues, discussions, release notes,
   and OpenAI documentation for reports involving missing `close_agent`,
   completed subagents consuming capacity, tool-surface differences, stale
   `CODEX_CLI_PATH`, and client/app version mismatches. Record direct links and
   whether any proposed fix matches the observed build and host.
3. If closure is absent, fully restart the desktop app, install an available
   supported update, and create another fresh session. Recheck the exposed
   operations before changing configuration.
4. Determine whether the older configured CLI path affects the desktop-hosted
   tool surface. Test a corrected or removed path only in the diagnostic
   session, preserve the prior value, and do not mutate repository config to
   paper over a user/client installation issue.
5. If `close_agent` is exposed, spawn bounded diagnostic agents. Let one finish
   naturally, close it explicitly, confirm it leaves live/capacity accounting,
   and prove the slot can be reused. Then interrupt a no-longer-needed agent,
   close it, and prove that slot can also be reused.
6. If closure remains absent, preserve a minimal reproduction containing the
   client versions, enabled feature, effective tool list, sanitized config
   facts, restart/update result, and relevant GitHub findings. Use it for an
   OpenAI defect report if the owner chooses that path.

## Gate completion

The experiment produces evidence, not policy. This gate remains unresolved
until all of the following are true:

- the observed closure mechanism and capacity semantics are recorded here;
- natural-completion and interrupted-agent behavior are established, or the
  absence is reproducible after the supported restart/update/path checks;
- relevant GitHub reports or fixes are evaluated against the actual client;
- the owner explicitly selects the workflow behavior for both available and
  unavailable closure; and
- the same PR is amended, affected validation and independent review are
  rerun, and the unresolved warning is removed from `$orchestrate`.

Until then, keep the PR draft and unmerged.
