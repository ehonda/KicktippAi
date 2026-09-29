# P1-04 operational activation evidence

- Status: In progress; all eight normal production source flags enabled on main, first scheduled observation pending.
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
| [36537325441](https://github.com/ehonda/KicktippAi/actions/runs/36537325441), [exact-head CI 36536799543](https://github.com/ehonda/KicktippAi/actions/runs/36536799543) | Controlled optional failure with ordinary success | On commit `72ec288a`, the one manual context job logged `CONTEXT_PROFILE_FAILED` in its optional refresh action, then the required ordinary profile succeeded and retained the verified Elo head. All 16 normal context/match jobs were skipped; no model jobs ran. Pre/post canonical Firestore state matched across the head, 19 verified payloads, original receipt and health. The probe run had no cycle, source observation, receipt or GitHub artifact. |
| [PR #124](https://github.com/ehonda/KicktippAi/pull/124), [candidate CI 36576000482](https://github.com/ehonda/KicktippAi/actions/runs/36576000482), [main CI 36576769563](https://github.com/ehonda/KicktippAi/actions/runs/36576769563) | A2 merged and Pages deployed | Reviewed all-eight normal source flag activation committed as `88a94048`, merged into main as `1b2e84cd`. Candidate and main build, eight test projects, and coverage passed; main Pages deploy succeeded. Existing schedule, job chain, model/prompt/cap, credentials, posting, and copy routes were unchanged. The [public review brief](https://ehonda.github.io/KicktippAi/experiment-analysis/review-briefs/bundesliga-2026-27/p1-04-operational-refresh.report.html) returned HTTP 200 and appeared in the public index. |
| [36577026768](https://github.com/ehonda/KicktippAi/actions/runs/36577026768) | Postmerge source-only validation succeeded | Run on main merge `1b2e84cd`: eight ordered source-only context jobs succeeded; all 16 ordinary context/match jobs and model jobs were skipped. One bundle artifact `11037988051` was present. Every lane reported `NETWORK_RATED_AT_NOT_NEWER:2026-09-24` and `Club Elo publication NotAttempted (retained verified head)`; no fresh 2026-09-29 observation is claimed. |

The detailed Firestore hashes, receipts, logs, and before/after comparisons remain in ignored local run evidence under `.tmp/orchestration/01a0a211-c4bc-7941-86f9-37dfba1da3fa/evidence/`. The accepted production artifact `10994562013` was still available on 2026-09-29. GitHub's `actions/upload-artifact` project has an [open report](https://github.com/actions/upload-artifact/issues/585) matching the earlier-attempt artifact disappearance; exact causation here is unproved.

## Remaining gates

The independent gate review accepted run `36537325441` for A2 admission. Its job summary records `SOURCE_REFRESH_OUTCOME=failure` and `ORDINARY_OUTCOME=success`. This proves containment for the tested action input error; a runtime network timeout remains an operational monitoring risk under the owner's accepted uncertainty.

1. Inspect the next existing schedule after activation and report optional refresh degradation separately from ordinary failures. Verify required ordinary context and its dependent match jobs, selected head dates/provenance, receipts, health, and desired issue state. Roll back by a reviewed all-eight flags-off commit if needed.

Live GitHub rerun success is no longer an A2 prerequisite under ADR-0086. The failed attempt remains a recorded recovery limit.
