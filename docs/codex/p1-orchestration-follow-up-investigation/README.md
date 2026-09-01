# P1 orchestration follow-up investigation

**Baseline session:** `01a04fd8-ffcf-7263-944f-98d1bc53c645`

**Successor session:** `01a054ee-67b4-7ab2-a0b4-c9ffabc2da2e`

**Successor snapshot interval:** 2026-08-31 01:10 CEST to 22:05 CEST,
including a 10h30m owner-wait interval; 10h24m remains after that pause.

**Repository boundary:** `71637cc..5891d48`

**Interactive report:** [open the self-contained HTML report](../../../session-analysis/p1-orchestration-follow-up/index.html)

## Executive conclusion

**Correction published 2026-09-01:** the original cutoff and normalized
measurements are preserved, but the causal and efficiency conclusions below
supersede the first publication. The machine-readable correction and strictly
separated post-cutoff evidence are in [`data/corrections.json`](data/corrections.json).

The defensible headline is **less parallel, less wasteful, efficiency
unproven**. Average concurrency while any task agent was active fell from
`1.52` to `1.12`; the share of active time with at least two agents fell from
`52.1%` to `11.8%`. The dominant explanation is the runnable graph: only P1-10
had completed grilling, while P1-04/05/06/07/11 remained `needs-interview`.
The recovery-first dependency chain therefore offered little independent work
to schedule. Machine admission and retained-agent capacity were secondary
constraints, not the demonstrated primary cause.

Logged tokens per pause-adjusted hour fell `42.2%`, but worker activity per
adjusted wall-second fell `41.3%`. The more comparable non-cached-input-plus-
output rate per worker-hour fell only `5.6%`, from about `771k` to `728k`.
That does not establish accepted-work-per-token or accepted-work-per-quota
efficiency. Quota burn is not itself undesirable when it buys useful accepted
work, and subscription quota must not become an orchestration throttle.

Machine discipline still did useful work. The run sampled resources 38 times,
admitted only one heavy local operation at a time, and never overlapped directly
classified `dotnet` calls. Subagent tool time fell from `42.8%` to `19.1%` of
worker-active time. Three retained architecture/review/writer threads occupied
the available task-agent slots and blocked a lightweight monitor. The cutoff's
low-memory sample shows that the conservative `1.50 GiB` floor denied work, but
does not by itself establish the best safe threshold.

The clearest workflow wins are publication and continuity. The first P1 sample
used 14 main CI runs and 14 remote P1-10 lane branches. The successor used two
main CI runs plus three draft-PR milestone runs and only two remote run
branches. Flawed R1 commits stayed local until an independently accepted
milestone was pushed. Production-safe `main` was restored and a 16-job live
workflow was verified while the still-unsafe P1-10 replacement remained in
draft PR #97.

The new architecture and specification roles are useful but expensive. Six
design-review turns all returned concrete `ACCEPT-WITH-FIXES` findings before
downstream work. The architecture/specification portfolio consumed 45.6M
tokens, 37.0% of subagent usage. It did not eliminate artifact churn: R1 still
needed three rejected exact-commit reviews before acceptance. Reusing the
xhigh specification reviewer as the exact-code reviewer preserved context in
some cases, but it also blurred role boundaries and occupied scarce task-agent
capacity long after subsequent reviews said no architecture recall was needed.

Ledger coalescing materially improved: completed state patches fell from 375
to 106, or from 28.4 to 10.2 per pause-adjusted hour. The current state file is
about 45k characters versus 141k in the earlier sample. Generic `wait_agent`
frequency remained flat at 46.7 versus 46.6 per adjusted hour, but that is a
weak efficiency measure. The actionable polling case is the root-owned external
CI loop caused by the unavailable monitor slot. Root share rose to 59.1% of
logged tokens from 37.6%, but accepted-work attribution is needed before
calling that overhead.

## Post-cutoff addendum (excluded from snapshot)

After R1 froze shared contracts, the first explicitly concurrent-ready R2
siblings ran sequentially because the three reusable threads occupied the
spawned-agent limit. A later sample at `1.48 GiB` also missed the old `1.50 GiB`
hard floor by only `0.02 GiB`. These observations sharpen the lifecycle and
memory-calibration hypotheses; they are excluded from every cutoff metric and
ordinary scorecard above.

## Early recommendations

1. Keep one heavy-operation lease and the milestone PR/CI cadence, but lower
   the hard memory floor to `1.10 GiB` and retain `1.50 GiB` as a warning.
2. Raise the repository-scoped spawned-agent limit to eight while preserving
   the independent two-writer-worktree and one-heavy-operation budgets.
3. Give retained specialists a reason and release trigger. Do not retain one
   merely as insurance when it blocks useful ready work.
4. Keep Sol/xhigh as the review default for now, but permit Sol/high for a
   frozen, bounded, exact artifact with deterministic criteria and no new
   architecture, ownership, invariant, or continuity question.
5. Record only transition-level ready/running lane, blocker, retention/release,
   and heavy-lease fields. Do not build a telemetry service in the ledger.
6. Make the preview-to-execution handoff visible with a one-line marker; leave
   a richer discoverable status surface as a future improvement.
7. Re-evaluate with accepted milestones/review closures, ready-lane
   utilization, correction work, blocker time, release/slot pressure, and
   operation-start/post-memory outcomes. Do not use quota burn as a throttle.

## Scope and limitations

This is an early follow-up against an active session, not the planned final
evaluation. The two runs worked on different slices of a changing P1-10 task.
Worker intervals overlap and are not timesheets. Logged tokens are not Codex
subscription quota units, and lower token throughput is not an efficiency
result. Git line counts are scope evidence, not productivity scores. Direct
`dotnet` overlap covers completed classified tool calls and does not reconstruct
every continuation process. The incomplete R2a turn at cutoff is excluded from
completed worker duration and result analysis. Post-cutoff evidence is labelled
separately and does not alter the preserved snapshot.

Normalized evidence and reproduction notes are in [`data/`](data/README.md).
