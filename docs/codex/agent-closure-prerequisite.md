# MultiAgent V2 lifecycle prerequisite

**Status:** lifecycle prerequisite resolved; no longer a merge or activation blocker
**Originally recorded:** 2026-09-03
**Resolved:** 2026-09-04

PR #98 was blocked on the assumption that spawned-agent capacity could not be
reclaimed without an explicit `close_agent` operation. The follow-up source
review and the fresh-session experiment below falsified that assumption for
the collaboration surface used by Codex Desktop in this repository.

The observed surface is consistent with MultiAgent V2. It automatically
evicts terminal, idle, mailbox-clean residents under capacity pressure and can
reload their logical identities later. The missing V1 `close_agent` operation
is therefore not a thread-capacity blocker. Proactive process and memory
cleanup remains a distinct runtime limitation.

## Environment

- The experiment ran in a deliberately small, non-`$orchestrate` root session.
- The injected collaboration surface exposed `spawn_agent`, `followup_task`,
  `send_message`, `interrupt_agent`, `list_agents`, and `wait_agent`; it did
  not expose `close_agent`.
- `codex --version` reported `codex-cli 0.153.0` from
  `C:\Users\dennis\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe`.
- `codex features list` reported `multi_agent` as `stable true`. The separate
  `multi_agent_v2` feature row reported `stable false`; the negotiated
  six-tool surface and the observed residency behavior, rather than that
  standalone row, establish the applicable lifecycle model.
- User-level `CODEX_CLI_PATH` selected the same current executable and version.
  No stale-CLI mismatch was present in the tested session.
- The PR branch config specifies eight spawned-agent threads, but the session
  started before that branch was checked out and exposed three usable child
  slots. The experiment therefore used the actual root-plus-three limit. It
  validates lifecycle semantics, not startup loading of the eight-thread
  repository setting.
- The desktop package version was not exposed through the session or the
  available Windows package query. The directly exercised CLI/runtime version
  and tool surface are recorded instead.

## Fresh-session experiment

The experiment used short-lived `gpt-5.6-luna` / `low` diagnostic agents with
no repository access or file changes.

| Case | Capacity setup | Observed result | Verdict |
| --- | --- | --- | --- |
| Natural completion | A completed while B and C remained active; D was then spawned | D spawned successfully even though A, B, and C had filled the three child slots | Clean terminal residents are automatically evictable |
| Repeated recycling | Six distinct logical agents, A through F, were created over the session | Total successful creations exceeded the three simultaneously resident child slots | The limit is not a lifetime creation cap |
| Reload and context | A first returned `A-FIRST-731`; after capacity pressure, `followup_task` recalled A | A returned `A-RELOADED-731`; the terminal D resident disappeared from `list_agents` when A was reloaded | Evicted logical identities can be reloaded with prior context |
| Queue-only mailbox pin | A completed, received `send_message`, and remained terminal while B and C were active | E failed with `collab spawn failed: agent thread limit reached` | Unread queue-only mail makes a terminal resident non-evictable |
| Mailbox recovery | `followup_task` woke A and consumed `QUEUE-ONLY-PIN-913`; the same E spawn was retried | A returned `A-CONSUMED-PIN-913`, then E spawned successfully | Consuming the queued work restores normal eviction |
| Interruption | B was interrupted while C and E were active; F was then spawned | F spawned and B disappeared from `list_agents` | An idle interrupted resident is evictable |
| Interrupted reload | `followup_task` later recalled evicted B | B returned `B-RELOADED-AFTER-INTERRUPT` with earlier context intact | Interrupted, evicted identities remain reusable in this runtime |

The successful D, E, and F admissions were made under deliberate residency
pressure. The failure and recovery around E used the same task shape; the
pending mailbox was the controlled difference.

## Resource observation

The count of local Codex-named processes remained three before and after the
experiment. Their combined working set increased from approximately `0.257
GiB` to `0.353 GiB`. Available system memory could not be sampled through the
restricted session, and the process list did not expose a reliable per-agent
mapping.

This is insufficient to claim that automatic residency eviction proactively
reclaims memory. The workflow must continue to treat machine-resource
admission separately from logical agent capacity and must not report memory as
released merely because another spawn succeeded.

## Upstream and release check

The 2026-09-04 GitHub refresh found:

- [`openai/codex#32353`](https://github.com/openai/codex/issues/32353) describes
  the exact MultiAgent V2 queue-only mailbox pin reproduced here.
- [`openai/codex#39694`](https://github.com/openai/codex/issues/39694) records a
  broader false thread-limit symptom; its later reproduction also narrows the
  failure to a near-completion queue-only message.
- [`openai/codex#36211`](https://github.com/openai/codex/issues/36211) remains
  open for the missing `close_agent` surface. Its V1 closure expectation does
  not override the V2 eviction and reload behavior directly observed here.
- [`openai/codex#40796`](https://github.com/openai/codex/issues/40796) confirms
  that a stale `CODEX_CLI_PATH` can create desktop/CLI version skew. The tested
  path instead resolved to the active `0.153.0` executable.
- Stable releases
  [`0.153.1`](https://github.com/openai/codex/releases/tag/rust-v0.153.1) and
  [`0.153.2`](https://github.com/openai/codex/releases/tag/rust-v0.153.2) contain
  unrelated model-catalog and display-text changes. No later stable lifecycle
  fix needed to be applied before evaluating this experiment.

## Selected workflow policy

The owner requested that the follow-up experiments be run and that sound
adjustments be implemented. The selected policy is:

- Model lifecycle as `running -> terminal -> reclaimable -> automatically
  evicted when residency is needed`, while keeping logical identity available
  for supported reload.
- Mark an agent reclaimable only when it has no active turn, no pending
  mailbox work, and no recorded near-term retention reason.
- Use `send_message` only for necessary mid-turn steering when the target is
  clearly active and completion is not near. Use `followup_task` whenever work
  must be consumed across an idle or completion boundary.
- Never send speculative, status-only, or late queue-only messages to a
  terminal, release-due, or possibly completing agent.
- Treat `reclaimable` as orchestration intent, not proof of physical unload.
  Record eviction only when the runtime exposes evidence such as removal from
  `list_agents` or successful admission under known capacity pressure.
- If a spawn reports `agent thread limit reached`, inspect live state. When a
  known pending queue-only message is the blocker and the work is still valid,
  use one bounded `followup_task` to consume it and retry admission once.
  Otherwise record the capacity blocker and queue the lane; do not spam
  messages, interrupts, or retries.
- Reuse an agent with `followup_task` only when continuity is valuable. Use a
  fresh agent when independence or clean context is more important.
- Keep resource admission independent. If resource pressure persists without
  an explicit release operation, stop admitting affected work, record the
  blocker, and request an owner-controlled end/restart of the current session.
  Recover from the run ledger and re-sample resources before new admission;
  do not open a concurrent replacement or claim cleanup.

## Gate completion

The natural-completion, reload, mailbox-pinning, mailbox-recovery,
interruption, and interrupted-reload behaviors are established. Relevant
upstream reports and releases were checked against the actual runtime, the
stale CLI-path hypothesis was excluded for this session, and the owner-directed
V2 policy is selected above.

This prerequisite no longer blocks merging or activating PR #98. The amended
PR still requires its normal affected validation, exact-head independent
review, and green required checks before merge.
