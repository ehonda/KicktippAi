# P1-15 — Deliver frozen Schadensfresse Champions-League bonus

- Status: In progress — the first authorized production dispatch failed closed before model, storage, or answer mutation on a refreshed form-action identity; the bounded correction passes local validation, while independent review, CI, fresh read-only preflight, and a new production gate remain pending
- Outcome: place the three exact deadline-bound CL answers through a target-owned manual route without importing P1-10/P1-13 global typing.
- Depends on: [ADR-0069](../decisions/0069-deliver-frozen-schadensfresse-champions-league-bonus.md), [ADR-0070](../decisions/0070-isolate-the-strict-cl-bonus-mutation-transport.md), frozen candidate `6ab7a3548dd5e56e2dcb360de8bc2dde9a9e902fc291cb0955da5f10fff4020a`

## Contract

- Exact scope: IDs `1662326752/53/54`, deadline `2026-09-08T16:45:00Z`, current 36-option arrays and 1/4/1 selection counts from source snapshot `4299e240f7909f24c2b7f4d2eeeaef564beaea4a3539fe87984867fa890205b0`.
- Exact invocation: target/context `schadensfresse`, partition `bundesliga-2026-27`, Sol/xhigh/cap-10000, hosted `kicktippai/bundesliga-2026-27/champions-league/predict-bonus` v1/`production`, normalized SHA `70819641df57c8979f1c11dfe4e3df920bca96defdbef29646fd22247dfd0ee2`, and zero document/token budget.
- Preserve ordinary cache, repredict, force, and override semantics, but require all three valid results before one strict POST and independently read back all three afterward.
- The strict mutation transport shares the authenticated cookie jar but has no authentication replay or automatic redirects. It sends the POST at most once; only one exact `302`/`303` bodyless GET follow is allowed, and the independent final GET remains on the same strict chain.
- The immutable route owns distinct exact identities: bonus page `https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true` and current form action `https://www.kicktipp.de/schadensfresse/tippabgabeForm`. The older snapshot's `/schadensfresse/tippabgabe` action is superseded route evidence and is not a fallback.

## Completion criteria

- Strict seed/form, dedicated prompt fallback, exclusive specialized manifest/lineage, command/verification, single-attempt transport, and manual-only workflow contracts are implemented with focused and real-handler request-journal tests.
- Focused suites, affected project suites, Release build, workflow-contract script, and actionlint pass under an admitted heavy lease.
- Root review/CI and separate mutation authority complete before model, Firestore, Langfuse, or Kicktipp writes.

## Local validation checkpoint

- Release solution build at the complete transport checkpoint exited `0` with zero errors (`.tmp/cl-test-results/transport-final-7.binlog` and `transport-final-build-7.log`). The final review correction's Release Orchestrator project build also exited `0` with zero errors (`finalcorrection-orchestrator-build.binlog` and `finalcorrection-orchestrator-build.log`).
- Focused filters at the complete transport checkpoint: strict transport `24/24`, strict route `26/26`, generic reauthentication `4/4`, production factory `7/7`, Firebase CL lineage `7/7`, CL command `7/7`, CL verification `4/4`, and prompt/service tuple `50/50` (`129/129`, all exit `0`; ignored JSON reports under `.tmp/cl-test-results/final-*`).
- The real-handler loopback journal proves shared authentication cookies, the selected target payload, redirect method/count rules, and the single POST boundary. Parser-retention assertions plus exact ordered payload-list construction cover repeated-name ordered-multimap preservation because WireMock.Net 2.14 cannot journal repeated URL-encoded form names without returning an unmatched `500`.
- Full affected suites are green: Firebase `300/300` and Kicktipp `251/251` at the accepted transport checkpoint, Orchestrator `1217/1217` after the final correction, and unchanged Core `309/309` plus OpenAI `234/234` baselines (`2311/2311` total). The workflow-contract and actionlint passes predate the source-only corrections; workflow files remain unchanged, and correction publication CI remains pending.
- Exact publication CI run `33941968888` passed all 12 jobs at `0838fce407d144d64ff11cb9010ad0f18ce24779`. The subsequent authorized production run `33953252166` failed closed in both verification and generation while parsing the first authenticated form because Kicktipp now advertises relative action `tippabgabeForm`; it made no model call, Firestore prediction write, or Kicktipp answer POST. Four useful authenticated observations, including two with the production User-Agent, confirmed the unchanged page and corrected exact action. The older snapshot hash remains authoritative for the question/option seed but is superseded for action identity only.
- The final action/base correction Release solution build exited `0` with zero errors (`.tmp/cl-test-results/action-correction-final-build.binlog` and `.log`). Focused TUnit JSON reports are green: strict transport `24/24`, strict route and pinned AngleSharp 1.7.2 base behavior `43/43`, production factory `7/7`, CL command `7/7`, and CL verification `4/4` (`85/85`). Full affected suites are Kicktipp `268/268` and Orchestrator `1217/1217` (`1485/1485`), with zero failed, skipped, cancelled, timed-out, or flaky tests; ignored reports use `.tmp/cl-test-results/action-correction-final-*`.
- After independent review and corrected CI, one fresh read-only `verify-bonus` preflight must pass route/form validation and exit `1` solely at the first missing exact-lineage row before any new production authority is considered; it does not report an all-three `0/3` audit.
