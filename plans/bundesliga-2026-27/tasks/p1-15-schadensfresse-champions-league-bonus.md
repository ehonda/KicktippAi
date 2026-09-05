# P1-15 — Deliver frozen Schadensfresse Champions-League bonus

- Status: In progress — reviewed corrections and strict transport pass the latest logged Release build and 129 focused tests; final affected-suite rerun, independent re-review, CI, and production execution gates remain pending
- Outcome: place the three exact deadline-bound CL answers through a target-owned manual route without importing P1-10/P1-13 global typing.
- Depends on: [ADR-0069](../decisions/0069-deliver-frozen-schadensfresse-champions-league-bonus.md), [ADR-0070](../decisions/0070-isolate-the-strict-cl-bonus-mutation-transport.md), frozen candidate `6ab7a3548dd5e56e2dcb360de8bc2dde9a9e902fc291cb0955da5f10fff4020a`

## Contract

- Exact scope: IDs `1662326752/53/54`, deadline `2026-09-08T16:45:00Z`, current 36-option arrays and 1/4/1 selection counts from source snapshot `4299e240f7909f24c2b7f4d2eeeaef564beaea4a3539fe87984867fa890205b0`.
- Exact invocation: target/context `schadensfresse`, partition `bundesliga-2026-27`, Sol/xhigh/cap-10000, hosted `kicktippai/bundesliga-2026-27/champions-league/predict-bonus` v1/`production`, normalized SHA `70819641df57c8979f1c11dfe4e3df920bca96defdbef29646fd22247dfd0ee2`, and zero document/token budget.
- Preserve ordinary cache, repredict, force, and override semantics, but require all three valid results before one strict POST and independently read back all three afterward.
- The strict mutation transport shares the authenticated cookie jar but has no authentication replay or automatic redirects. It sends the POST at most once; only one exact `302`/`303` bodyless GET follow is allowed, and the independent final GET remains on the same strict chain.

## Completion criteria

- Strict seed/form, dedicated prompt fallback, exclusive specialized manifest/lineage, command/verification, single-attempt transport, and manual-only workflow contracts are implemented with focused and real-handler request-journal tests.
- Focused suites, affected project suites, Release build, workflow-contract script, and actionlint pass under an admitted heavy lease.
- Root review/CI and separate mutation authority complete before model, Firestore, Langfuse, or Kicktipp writes.

## Local validation checkpoint

- Latest source/test tip: Release solution build exit `0`, zero errors, with ignored evidence in `.tmp/cl-test-results/transport-final-7.binlog` and `transport-final-build-7.log`.
- Latest focused filters: strict transport `24/24`, strict route `26/26`, generic reauthentication `4/4`, production factory `7/7`, Firebase CL lineage `7/7`, CL command `7/7`, CL verification `4/4`, and prompt/service tuple `50/50` (`129/129`, all exit `0`; ignored JSON reports under `.tmp/cl-test-results/final-*`).
- The real-handler loopback journal proves shared authentication cookies, the selected target payload, redirect method/count rules, and the single POST boundary. Repeated-name ordered-multimap serialization remains covered by the in-process request-capture test because WireMock.Net 2.14 cannot journal repeated URL-encoded form names without returning an unmatched `500`.
- The earlier affected-project baseline (`2257/2257`), workflow contract, and actionlint passes were collected on the pre-review transport baseline. The workflow files are unchanged, but affected project suites must be rerun against the final reviewed transport tip before completion.
