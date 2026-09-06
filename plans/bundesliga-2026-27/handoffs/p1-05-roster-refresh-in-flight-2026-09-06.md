# P1-05 roster refresh in-flight handoff

- Status: Common foundation implemented locally; fresh cumulative review and P1-05 source implementation not started
- Date: 2026-09-06
- Orchestration run: `01a07449-de77-7ae0-ac4a-8f5330c43121`
- Task: [P1-05](../tasks/p1-05-roster-refresh.md)
- Shared authority: [ADR-0073](../decisions/0073-refresh-strength-and-rosters-during-context-collection.md), [ADR-0074](../decisions/0074-freeze-context-source-cycle-handoff-and-provenance.md)
- Current integration head: `204cfd8db2c4163ca320a7c8a1829ebbc2ee1ed2` on clean, pushed `main`

## Resume boundary

P1-05 is independent of P1-04 after the source-neutral ADR-0074 seam. Its own
roster acquisition/diff/v3 implementation has not started. The common seam is
preserved in a clean local worktree and branch but remains deliberately
unintegrated because its final correction did not receive a fresh cumulative
review before the Owner-directed stop.

```text
Worktree: .tmp/worktrees/p1-04-05-common
Branch: codex/01a07449-de77-7ae0-ac4a-8f5330c43121-common-foundation
Reviewed base: c99e1635428bcfea48271e4169767b38f014148c
Current local tip: 852d1798e77d78e4dee4350ddc1d59cba54f60a5
Tip parent: 91ef68dcea8a841becef73292cee803888e7fb19
Range: 20 local commits, 20 common-owned paths
Push state: local only
```

The primary and remote `main` advanced outside this objective to `204cfd8` via
an unrelated orchestration-analysis commit. Preserve it. Do not reset main or
the common lane, and do not touch the separate dirty P1-10 worktree at
`634b65316422b545ecd1956996a74525fafdd80d`.

## First actions in the next session

1. Start a new explicit orchestration run under the improved protocol and
   re-verify exact repository, branch, worktree, resource, and CI state.
2. Commission a fresh `gpt-5.6-sol` / `xhigh` cumulative review of exact range
   `c99e1635428bcfea48271e4169767b38f014148c..852d1798e77d78e4dee4350ddc1d59cba54f60a5`
   against all ADR-0074 invariants and every prior review finding. Do not rely
   on focused tests as approval.
3. If approved, content-integrate the reviewed common range on top of the then
   current `main` as one cohesive source-neutral milestone. Re-verify exact
   paths and preserve unrelated `204cfd8` content. Do not integrate the 20
   lane commits blindly if the improved protocol selects a safer equivalent
   method.
4. Reuse the clean common worktree only after its reviewed content is
   recoverably integrated or published, create the allowlisted P1-05 branch
   from current main, and admit the already frozen P1-05 implementation scope.
5. Keep every roster source flag false. Dormant/synthetic implementation does
   not grant development or production persistence.

## Final common correction evidence

Fresh review of `91ef68d` found two receipt defects: accepted/mixed roster
selections rejected historical-byte `Reactivated`, and mixed selection did not
bind membership capture to the eligible descriptor. Local commit `852d179`
corrects both.

- Exact touched paths in the final correction:
  `src/Core/BundesligaContextSourceHealth.cs`,
  `tests/Core.Tests/BundesligaContextSourceHealthContractTests.cs`, and
  `tests/FirebaseAdapter.Tests/FirebaseContextSourceCycleRepositoryTests.cs`.
- Focused Core health: 20/20 passed in 2.259 seconds.
- Focused Firebase repository: 63/63 passed in 2m04.062s.
- `git diff --check`: clean.
- Post-commit worktree: clean.
- No amend, history rewrite, integration, or push occurred.

The cumulative common range changes exactly the 20 paths enumerated in
[the frozen execution packet](../p1-04-05-execution-packet.md). It implements
the source-neutral cycle identity, claims/leases, canonical bundle, handoff,
receipt replay, health/revision reduction, strict Firestore reconstruction,
development handoff, disabled bypass, and profile seams. The next reviewer
must inspect the code, not infer completeness from this summary.

## Exact current source evidence

- Artifact URL:
  `https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb`.
- Length: `210,776,064` bytes.
- SHA-256:
  `ba1eff7337b8ca78cb533df0b6eba0d6fc58218e0767460f6770e0b37f5a2113`.
- Embedded revision: `e44f186d6f06dd8452aaf54c7921ba66c961f637`.
- All 18 manifest IDs have zero eligible L1 club/player rows with
  `last_season=2026`.
- No revision-bound authoritative artifact capture, membership effective, or
  enrichment capture date was proven.
- The real candidate must reject with `NO_ELIGIBLE_2026_MEMBERSHIP` and
  `UNKNOWN_SOURCE_DATE`, retaining fallback/LKG. It is not completion evidence.
- Ignored local evidence, when retained:
  `.tmp/p1-05-evidence-01a07449/`.

## Frozen P1-05 implementation boundary

After common approval/integration, implement only the literal P1-05 paths in
[the execution packet](../p1-04-05-execution-packet.md): the refresh policy,
bounded artifact acquirer, roster selection/diff/carry logic, self-contained v3
publication/reconstruction, attribution, and focused tests. The implementation
must prove both sides of the present source state:

- the exact current artifact rejects and cannot displace a valid head; and
- a synthetic future artifact with explicit 2026/27 membership and proven
  revision-bound dates automatically takes over per valid club.

Cover changed/unchanged/pending revision, remote drift, hash/revision/schema/
date/season/identity rejection, size/time budget, additions, departures, team
and coach changes, enrichment-only changes, stable-ID carry, new-player `N/A`,
genuine valuation decreases, candidate-only conflicts, final-set conflicts,
v1/v2/v3 reconstruction, same-cycle reuse, dry-run, and copy compatibility.

## Activation and completion gates

No production authority was granted. Development persistence and production
activation remain separate Owner decisions. Production requires scheduled
provider acquisition, Firestore cycle/health/context writes, GitHub bundle and
issue permissions, rollback ownership, and restoration criteria. P1-05 is not
complete until trustworthy enrichment automation is active and the future
valid-membership takeover path is proven; a paused real upstream may retain
membership on LKG with an open source issue.
