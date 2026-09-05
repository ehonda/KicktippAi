# P1-15 — Deliver frozen Schadensfresse Champions-League bonus

- Status: In progress — implementation and focused verification pending root heavy-operation lease
- Outcome: place the three exact deadline-bound CL answers through a target-owned manual route without importing P1-10/P1-13 global typing.
- Depends on: [ADR-0069](../decisions/0069-deliver-frozen-schadensfresse-champions-league-bonus.md), frozen candidate `6ab7a3548dd5e56e2dcb360de8bc2dde9a9e902fc291cb0955da5f10fff4020a`

## Contract

- Exact scope: IDs `1662326752/53/54`, deadline `2026-09-08T16:45:00Z`, current 36-option arrays and 1/4/1 selection counts from source snapshot `4299e240f7909f24c2b7f4d2eeeaef564beaea4a3539fe87984867fa890205b0`.
- Exact invocation: target/context `schadensfresse`, partition `bundesliga-2026-27`, Sol/xhigh/cap-10000, hosted `kicktippai/bundesliga-2026-27/champions-league/predict-bonus` v1/`production`, normalized SHA `70819641df57c8979f1c11dfe4e3df920bca96defdbef29646fd22247dfd0ee2`, and zero document/token budget.
- Preserve ordinary cache, repredict, force, and override semantics, but require all three valid results before one strict POST and independently read back all three afterward.

## Completion criteria

- Strict seed/form, dedicated prompt fallback, exclusive specialized manifest/lineage, command/verification, and manual-only workflow contracts are implemented with focused tests.
- Focused suites, affected project suites, Release build, workflow-contract script, and actionlint pass under an admitted heavy lease.
- Root review/CI and separate mutation authority complete before model, Firestore, Langfuse, or Kicktipp writes.
