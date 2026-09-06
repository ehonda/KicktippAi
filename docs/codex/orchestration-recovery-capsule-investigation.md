# Initial orchestration recovery-capsule investigation

Status: initial implementation hypothesis, 2026-09-06. Revisit after at least
one substantial `$orchestrate` run has exercised automatic compaction.

## Why change the current ledger

The first P1 investigation found 375 root patches to orchestration state. The
latest urgent-production run still patched `state.md` 207 times, or 18.51 times
per effective hour, and its ignored run artifacts grew to roughly 68 KiB for
`state.md` plus 26 KiB for `preview.md`. The detailed state was intended to
improve recovery after compaction, but maintaining it became a meaningful
source of root-context churn in its own right. See the
[urgent-production orchestration investigation](urgent-production-orchestration-investigation/README.md).

The capsule design separates two needs:

- `preview.md` keeps the reviewed design tree and frozen runnable graph; and
- `capsule.json` keeps only the small set of non-derivable facts required to
  restart safely after compaction.

Everything with an authoritative live source—agent status, capacity, resource
measurements, worktrees, Git, CI, and ordinary completion history—is
rehydrated instead of copied into the capsule.

## Selected initial design

The capsule is model-independent. Sol/xhigh, Astra, or another root model sees
the same validated developer context and follows the same recovery contract.
The design therefore does not encode model names or reasoning effort.

Each explicit orchestration run owns:

- `.tmp/orchestration/<run-id>/preview.md`;
- `.tmp/orchestration/<run-id>/capsule.json`;
- `.tmp/orchestration/<run-id>/capsule.sha256`; and
- `.tmp/orchestration/<run-id>/active`, an exact-session activation marker.

The capsule is an overwrite-only snapshot capped at 8 KiB. It contains the
objective and stop condition, lifecycle status and wave, durable decisions and
owner gates, fixed-category blockers, frozen artifact pointers and exact SHAs,
the Git allowlist and any reviewed unpublished recovery commit, current
ownership/lease reservations, compact resource verdict/warning/override state,
retained-agent release contracts, and the next safe root and delegated actions.
It deliberately has no event history.

The root updates and reseals it only at a durability barrier: initial freeze,
gate change, material reviewed re-freeze, runnable blocker change, reservation
or lease-owner change, reviewed unpublished recovery commit, milestone
integration/publication, stop, or completion. Near-simultaneous barriers are
coalesced. An unchanged resource sample is explicitly capsule-silent.

## Hook seam

Codex project hooks can be declared in `<repo>/.codex/hooks.json`, receive the
exact `session_id`, and run around compaction. The official hook contract says
that `PreCompact` matches `manual|auto`; `SessionStart` matches
`startup|resume|clear|compact`; and a `SessionStart(source=compact)` hook runs
before the immediate continuation, including after mid-turn automatic
compaction. It can inject `additionalContext` as developer context. The same
documentation warns that `transcript_path` is convenient but not a stable hook
interface. See [Codex hooks](https://learn.chatgpt.com/docs/hooks), especially
[hook locations](https://learn.chatgpt.com/docs/hooks#where-codex-looks-for-hooks),
[common input](https://learn.chatgpt.com/docs/hooks#common-input-fields),
[PreCompact](https://learn.chatgpt.com/docs/hooks#precompact), and
[SessionStart](https://learn.chatgpt.com/docs/hooks#sessionstart).

This implementation uses two synchronous 30-second hooks:

1. `PreCompact` validates the exact-session activation marker, JSON schema,
   8 KiB cap, session binding, and SHA-256 checksum. Valid state produces no
   output. Invalid active state emits a warning but allows compaction to
   continue so automatic compaction cannot strand the run.
2. `SessionStart(source=compact)` repeats that validation. On success it
   injects the capsule together with an explicit instruction to continue under
   the already-invoked `$orchestrate` skill, re-read the skill and `AGENTS.md`,
   and rehydrate live state before substantive work. Because the hook enforces
   the 8 KiB cap, `additionalContextLimit: 0` delivers the complete validated
   output directly instead of spilling a worst-case valid capsule to disk.

There is no `PostCompact` hook. `SessionStart(source=compact)` already occupies
the reliable post-compaction seam and reaches the immediate model continuation;
adding diagnostic work after every compaction would create another moving
part without improving the current recovery contract.

Codex matchers cannot inspect whether a skill is active. The hook command is
therefore registered for compaction lifecycle events, but its first operation
is an exact-session `active`-marker check. A plain Codex session has no such
marker and exits successfully with no capsule read, warning, or injected
context. The static command still has to start before it can read `session_id`;
Codex exposes no skill-aware matcher. Status messages are omitted, but a plain
compaction still pays PowerShell startup latency (warm samples were around one
second and a cold sample exceeded three seconds on the trial system). Sealing a
`complete` or `stopped` capsule removes the marker, so ended runs are silent as
well. If that unavoidable cost becomes material, a future native gate is the
next optimization rather than weakening the exact-session check.

Project-local hooks require the user to review and trust their exact definition
with `/hooks`, and changed definitions require renewed trust. Hook readiness is
therefore a fail-closed execution gate: before writers, `$orchestrate` verifies
that the hooks feature is enabled and requires owner confirmation of current
trust. An untrusted or disabled hook must not be represented as automatic
recovery coverage.

## Missing and corrupt recovery state

The active marker is intentionally separate from the capsule. That lets the
hook distinguish “ordinary session” from “active orchestration capsule is
missing.” For an active exact session, the following are recovery failures:

- missing capsule or checksum;
- malformed or unsupported JSON/schema;
- capsule larger than 8 KiB;
- run/session identity mismatch;
- invalid lifecycle or blocker values; and
- checksum mismatch.

`PreCompact` surfaces the failure in the UI/event stream and lets compaction
finish. The compact `SessionStart` hook forwards the exact failure class as
developer context, explicitly reactivates the `$orchestrate` recovery rules,
and requires reconstruction from the exact run directory plus live agents,
Git/worktrees, fresh resources, and CI. It forbids selecting another run by
recency. The failure itself is retained as useful recovery evidence.

## Other workflow corrections included

The latest run also supports three small policy changes:

- A retained thread is released whenever its role changes materially or its
  recorded observable context-cost bound is reached. The next role gets a
  fresh bounded thread. If live token usage is unavailable, the preview must
  name an enforceable proxy such as completed follow-up turns or one milestone
  rather than pretending an exact token counter exists.
- Resource samples that do not change admission, warning band, reservation,
  override, or lease owner do not update the capsule. Detailed calibration
  evidence can remain outside recovery context.
- A later `EXECUTION START` marker is allowed only when an independently
  reviewed material re-freeze begins a new execution wave, not for routine
  continuation, recovery, polling, or scheduling.

The report's suggestion about standing production authority is intentionally
excluded. It arose from an ambiguous owner message in the trial rather than a
workflow defect.

## Alternatives not selected

- Keep a smaller Markdown ledger: simpler, but still invites incremental
  operational logging and has weaker machine validation.
- Parse the transcript before compaction: rejected because Codex explicitly
  does not promise a stable transcript format for hooks and semantic recovery
  would be slow and model-dependent.
- Generate a new model summary in `PreCompact`: rejected because it adds cost,
  latency, and another summarization failure mode at the context-pressure
  boundary.
- Add `PostCompact` diagnostics: deferred until evidence shows that validated
  capsule injection plus live rehydration leaves a concrete recovery gap.

## Evaluation plan

After a natural orchestration run with at least one compaction, compare it with
the current 207-patch baseline. Measure capsule update count and bytes injected
per compaction; record valid, missing, and corrupt hook outcomes; inspect time
to resume useful delegated work; and check for stale ownership, duplicate work,
wrong-run selection, or missed gates. Also examine retained-thread role changes
and proxy-threshold releases, no-change resource silence, and the count and
meaning of `EXECUTION START` markers.

The first revision should remain deliberately small. Change the schema or add
another hook only when a concrete recovery failure cannot be answered from the
capsule, `preview.md`, and authoritative live surfaces.
