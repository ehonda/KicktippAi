# Explicit subagent closure prerequisite

**Status:** unresolved merge and activation blocker

**Recorded:** 2026-09-01

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

The following observations were made in the session that prepared the draft:

- The injected collaboration surface exposed `spawn_agent`, `followup_task`,
  `send_message`, `interrupt_agent`, `list_agents`, and `wait_agent`, but not
  `close_agent`.
- `codex --version` reported `codex-cli 0.151.0`.
- `codex features list` reported stable `multi_agent` support enabled.
- Neither the inspected repository config nor user config disabled multi-agent
  tools. Adding `features.multi_agent = true` would therefore be redundant.
- A string inspection of the installed executable found `close_agent` and
  `CloseAgent`, which shows that some closure implementation is present but
  does not prove that this host negotiated or exposed it.
- The configured CLI path appeared to reference an older cached executable
  than the current desktop launchers. It is a possible influence, not an
  established cause.
- Collaboration tools appear to be selected when a session starts. No
  repository setting discovered during the audit can add a missing operation
  to the already-running session.

Official OpenAI documentation currently says that:

- stable `features.multi_agent` includes `close_agent`; and
- the app, CLI, and IDE can be asked to close completed subagent threads.

Sources:

- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference#configtoml)
- [Codex subagents: managing subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents#managing-subagents)

The mismatch could be a stale client or launch path, host/tool-generation
issue, managed capability difference, or staged defect. None is proven yet.

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
