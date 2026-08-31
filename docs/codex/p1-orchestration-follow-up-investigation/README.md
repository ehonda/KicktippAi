# P1 orchestration follow-up investigation

**Baseline session:** `01a04fd8-ffcf-7263-944f-98d1bc53c645`

**Successor session:** `01a054ee-67b4-7ab2-a0b4-c9ffabc2da2e`

**Successor snapshot interval:** 2026-08-31 01:10 CEST to 22:05 CEST,
including a 10h30m owner-wait interval; 10h24m remains after that pause.

**Repository boundary:** `71637cc..5891d48`

**Interactive report:** [open the self-contained HTML report](../../../session-analysis/p1-orchestration-follow-up/index.html)

## Executive conclusion

The new regime is less parallel and more efficient. Average concurrency while
any task agent was active fell from `1.52` to `1.12`; the share of active time
with at least two agents fell from `52.1%` to `11.8%`. Yet logged token
intensity per pause-adjusted hour fell `42.2%`, and non-cached input plus output
intensity fell `41.6%`. The user's approximate weekly-quota observation points
in the same direction, but Codex quota units cannot be reconstructed from the
transcripts, so the report does not claim an exact quota-to-token conversion.

Machine discipline contributed to the parallelism decline but does not fully
explain it. The run sampled resources 38 times, admitted only one heavy local
operation at a time, and never overlapped directly classified `dotnet` calls.
This reduced subagent tool time from `42.8%` to `19.1%` of worker-active time.
The larger concurrency limiters were the recovery-first dependency chain, one
reused writer worktree, and three persistent architecture/review/writer threads
occupying the available task-agent slots. An attempted Luna/low production-run
monitor could not be admitted, so the root absorbed its polling.

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
xhigh specification reviewer as the exact-code reviewer preserved context but
also blurred role boundaries and occupied scarce task-agent capacity.

Ledger coalescing materially improved: completed state patches fell from 375
to 106, or from 28.4 to 10.2 per pause-adjusted hour. The current state file is
about 45k characters versus 141k in the earlier sample. Polling did not improve:
root waits remained effectively flat at 46.7 versus 46.6 per adjusted hour.
The root now represents 59.1% of logged run tokens, up from 37.6%, making the
control plane the most visible next optimization target.

## Early recommendations

1. Keep the one-heavy-operation lease and milestone PR/CI cadence. They reduce
   duplicate machine and release work without weakening gates.
2. Reconcile “recallable” roles with the actual task-thread limit. Preserve one
   architecture owner, but leave capacity for an ephemeral Luna monitor or an
   independent read/edit lane.
3. Use Sol/high for post-freeze correctness review unless a fresh cross-cutting
   design risk explicitly justifies continuing the xhigh specification role.
4. Exploit safe non-heavy overlap: prepare the next independent read/edit lane
   while one heavy gate or remote CI run is active.
5. Reduce root polling. Ledger traffic improved, but waits per adjusted hour
   did not, and root token intensity barely moved.
6. Re-evaluate after the session completes. R2 onward, PR merge outcome,
   escaped defects, final quota use, and cleanup are not in this interim cut.

## Scope and limitations

This is an early follow-up against an active session, not the planned final
evaluation. The two runs worked on different slices of a changing P1-10 task.
Worker intervals overlap and are not timesheets. Logged tokens are not Codex
subscription quota units. Git line counts are scope evidence, not productivity
scores. Direct `dotnet` overlap covers completed classified tool calls and does
not reconstruct every continuation process. The incomplete R2a turn at cutoff
is excluded from completed worker duration and result analysis.

Normalized evidence and reproduction notes are in [`data/`](data/README.md).
