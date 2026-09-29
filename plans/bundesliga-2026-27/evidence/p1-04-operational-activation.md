# P1-04 operational activation evidence

- Status: In progress; eight normal production source flags remain false.
- Recorded: 2026-09-29.
- Operative gate: [ADR-0086](../decisions/0086-bound-live-club-elo-replay-gate.md) with ADR-0085's ordinary-continuation requirement.

## Verified live evidence

| Run | Result | Bounded proof |
| --- | --- | --- |
| [36476906456](https://github.com/ehonda/KicktippAi/actions/runs/36476906456) | Development source accepted | Official HTML HTTP 200, parser/table v2, displayed date 2026-09-24, 18/18 Bundesliga clubs; development head and receipt persisted. No model job. |
| [36477928076](https://github.com/ehonda/KicktippAi/actions/runs/36477928076) | Later development source timeout | Original development head retained and cycle/receipt completed. No model job. |
| [36478629294](https://github.com/ehonda/KicktippAi/actions/runs/36478629294) | Production source accepted | Eight ordered source-only jobs succeeded; one artifact, eight receipts, four community heads, 76/76 team/aggregate payload bindings, current health and synchronized desired issue. No model job. |
| [36481153403](https://github.com/ehonda/KicktippAi/actions/runs/36481153403) attempt 1 | Later production source timeout | Eight source-only jobs succeeded with rejected-source/retained-head receipts, unchanged four heads and 76/76 payload bindings. No model job. |
| [36481153403](https://github.com/ehonda/KicktippAi/actions/runs/36481153403) attempt 2 | **Replay failed** | First source-only job failed during preparation; later jobs skipped. Original artifact ID `10996642926` was visible before rerun and returned 404 afterward. Inspected Firestore cycle, receipt, head, payload and health values remained unchanged. This is fail-closed evidence, not replay success. |
| [36512975584](https://github.com/ehonda/KicktippAi/actions/runs/36512975584) | Old main schedule failed | Old v1-only reader could not reconstruct the new v2 production head (`parserContract` invalid). This incident was caused by the early v2 production publication. |
| [PR #116](https://github.com/ehonda/KicktippAi/pull/116), [main CI 36533424137](https://github.com/ehonda/KicktippAi/actions/runs/36533424137) | Safety repair merged | Main merge `27b38d6ae90c7af3f4902666ec796d1bff560d3c` includes the v2 reader with all eight normal flags false. CI passed, and a source-off production dry run read the retained v2 Elo head successfully without Firestore writes. |

The detailed Firestore hashes, receipts, logs, and before/after comparisons remain in ignored local run evidence under `.tmp/orchestration/01a0a211-c4bc-7941-86f9-37dfba1da3fa/evidence/`. The accepted production artifact `10994562013` was still available on 2026-09-29. GitHub's `actions/upload-artifact` project has an [open report](https://github.com/actions/upload-artifact/issues/585) matching the earlier-attempt artifact disappearance; exact causation here is unproved.

## Remaining gates

1. Run a controlled optional refresh failure in a **normal** context job and prove that its required ordinary profile succeeds with unchanged verified Elo provenance/payload and no fabricated receipt. The probe runs no prediction job.
2. Review and pass exact-head CI for the probe and the later all-eight flag activation. Keep schedule, topology, model/prompt/cap, credentials, posting, and copy routes unchanged.
3. After activation, inspect the next existing schedule and report optional refresh degradation separately from ordinary failures. Roll back by a reviewed all-eight flags-off commit if needed.

Live GitHub rerun success is no longer an A2 prerequisite under ADR-0086. The failed attempt remains a recorded recovery limit.
