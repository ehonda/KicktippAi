# P1 orchestration interim investigation

**Investigated session:** `01a04fd8-ffcf-7263-944f-98d1bc53c645`

**Snapshot interval:** 2026-08-30 01:27 CEST to 20:23 CEST

**Repository boundary:** `c4669aa..04a6d85`

**Interactive report:** [open the self-contained HTML report](../../../session-analysis/p1-orchestration-interim/index.html)

## Executive conclusion

The slow progress is not explained by one cause. P1-10 is genuinely much
larger than its task label suggested: by the cutoff, the run had changed 100
files with a net +9,578/-922 lines, added three ADRs, integrated 13 P1-10-era
commits, and still had a typed resolver lane in progress. The P1-10 task record
itself grew from 65 to 326 lines while implementation was underway.

The local machine and external tools are material but not dominant. Subagent
tool calls occupied 8.07 aggregate worker-hours, 42.8% of worker-active time;
382 directly classified `dotnet` cells accounted for 2.36 hours before
continuation polling. The 18h56m observed wall span also contains a 5h43m
overnight interval with no task agent active, ending when explicit push
authorization arrived. Excluding that pause leaves about 13h13m.

The avoidable slowdown is workflow fragmentation. P1-10 used 14 local task
branches by the snapshot, all 14 already pushed remotely. The whole run
created 26 CI-only agents and triggered 14 main Build-and-Test runs totaling
56m15s of serial CI compute. Independent review was valuable—it found real
compile, fail-closed, concurrency, and contract defects—but 19 completed P1-10
review turns returned findings/rejection versus 14 approval turns, and 11 of
13 completed review lanes required more than one review turn.

Model allocation is substantially improved from P0: all 78 spawns explicitly
selected a model and effort and all used `fork_turns: none`. Luna/low handled
the mechanical lanes and is not the source of review churn. Terra/high writers
often needed correction, but Sol/high contract and ADR writers also failed
first review. The common weakness is that the end-to-end design was specified
incrementally between implementation slices. The run had research and ADR
agents, but no persistent high-tier architecture/specification owner.

The durable orchestration ledger is not a major direct wall-clock cost: 375
completed ledger patches took about 11.5 minutes. It is nevertheless too
chatty—roughly one edit every three minutes—and contributes to root context,
token, and compaction pressure. Coalescing checkpoint updates into material
phase transitions would preserve recovery value with less control-plane noise.

## Recommended changes

1. Add a Sol/high end-to-end architecture brief plus independent spec review
   before the first writer, and retain that lead for emergent design changes.
2. Keep focused local tests and exact-SHA review, but batch integration and full
   main CI into three or four cohesive P1-10 layers.
3. Keep worktrees/local branches for isolation; remote-push only recovery-
   critical lanes and milestone integration branches.
4. Reclassify a Terra implementation lane to Sol/high planning as soon as a new
   cross-cutting contract or architecture decision appears.
5. Update the orchestration ledger at ownership, blocker, frozen-commit,
   integration, and release-gate transitions—not after routine polls and test
   checkpoints.
6. If PRs are introduced, use one draft PR per top-level task or cohesive
   milestone. A PR per micro-branch would increase overhead.

## Scope and limitations

This is an intermediary snapshot of a still-running session. Worker durations
overlap and are not timesheets. Tool time includes local execution, Git/file
operations, network calls, and polling, so it is an upper bound for machine
cost. Review verdicts are classified from bounded final-message excerpts.
Complete prompts, reasoning, private payloads, and secrets are not copied.

Normalized evidence and schema notes are in [`data/`](data/README.md).
