# P1-11 — Migrate the Langfuse project to v4

- Status: Not started
- Priority: P1
- Depends on: [P0-21](p0-21-production-activation.md)
- Coordinates with: [P1-06](p1-06-observability-datasets.md)
- Deadline: Complete before Langfuse Cloud removes legacy APIs on 2026-11-16

## Trigger

The Langfuse Migration Assistant reported seven deprecated read-API surfaces
used during the preceding 14 days. Its 2026-08-30 snapshot showed:

| Deprecated endpoint | Calls | Last seen |
|---|---:|---|
| `GET /api/public/dataset-run-items` | 216 | 1 hour ago |
| `GET /api/public/datasets/{datasetName}/runs/{runName}` | 131 | 1 hour ago |
| `GET /api/public/traces` | 26 | 5 hours ago |
| `GET /api/public/v2/scores` | 55 | 5 hours ago |
| `GET /api/public/traces/{id}` | 15 | 6 hours ago |
| `GET /api/public/observations/{id}` | 4 | 13 hours ago |
| `GET /api/public/observations` | 3 | 20 hours ago |

The same snapshot marked evals, experiments, integrations, and detected
instrumentation as v4-compatible. Those green checks are provisional project
evidence, not a substitute for verifying repository consumers or the final
cutover. Migration Assistant activity can lag live traffic and refreshes about
every 15 minutes.

Langfuse's current [v4 overview](https://langfuse.com/docs/v4),
[compatibility guide](https://langfuse.com/docs/compatibility), and
[deprecated API migration guide](https://langfuse.com/faq/all/deprecated-api-migration)
are the implementation sources of truth. Re-read them when this task starts;
do not implement against this snapshot alone.

## Outcome

Every repository-owned Langfuse caller uses the v4-supported observation,
score, experiment, dataset, and ingestion contracts. The confirmed Cloud
project produces no deprecated API activity, representative non-production
traces and experiment exports preserve their existing semantics, and an owner
can switch the project to v4 with a documented rollback boundary.

## Work items

- [ ] Confirm the target Langfuse host and project without printing or
      committing credentials; refresh the Migration Assistant before changing
      code and record the current endpoint counts and timestamps.
- [ ] Inventory Langfuse SDKs, resolved versions, direct HTTP callers, OTEL
      setup, CLI/CI invocations, repository skills, active analysis utilities,
      tests, evaluators, and export integrations. Classify each dashboard hit
      by an owned caller or record the remaining caller as project-dependent
      investigation rather than guessing.
- [ ] Upgrade each maintained SDK or CLI to the latest stable supported version
      needed by the selected v4 APIs, update lockfiles where present, and
      record declared and resolved versions.
- [ ] Replace dataset-run reads with `GET /experiments` followed by cursor-
      paginated `GET /experiment-items`, including required bounded
      `fromStartTime`, dataset-ID resolution through `GET /v2/datasets`, field
      groups, response parsing, and score/input/output semantics.
- [ ] Remove direct dataset-run-item creation from maintained callers in favor
      of the supported experiment-runner contract, or the documented OTEL
      experiment attributes where no runner exists. Do not treat an SDK's
      internal transitional request as an application-owned direct caller.
- [ ] Replace legacy trace and observation reads with bounded,
      cursor-paginated `GET /v2/observations` queries. Reconstruct trace input
      and output only from the root observation, request every required field
      group explicitly, preserve session/trace grouping, and parse raw IO only
      where consumers require JSON.
- [ ] Replace score reads with cursor-paginated `GET /v3/scores`; migrate
      `datasetRunId` filters to `experimentId`, the subject and detail field
      groups, typed score values, timestamp boundaries, and any removed
      metadata filtering without changing supported score writes.
- [ ] Verify the OTEL exporter uses the current v4 ingestion contract and
      carries root input/output plus required trace attributes to applicable
      observations, including session ID on cost-bearing generations. Preserve
      buffering, retry, flush, shutdown, and error behavior or document an
      intentional change.
- [ ] Update the public API client models, retry classification, pagination,
      fixtures, WireMock expectations, active experiment/cost tooling, and
      current documentation together. Keep historical records historical, but
      remove deprecated endpoints from executable examples and active guidance.
- [ ] Inspect the confirmed project's Evaluators UI and Project Settings >
      Integrations. Verify there are no active Legacy evaluator rows or legacy
      export sources; if either exists, follow the current evaluator or
      dual-source export migration and keep the task open through validation.
- [ ] Send representative traces and an experiment smoke slice to a
      non-production project. Verify hierarchy, root IO, propagated session and
      trace attributes, session cost, prompt identity, experiment items,
      scores, pagination, and export/report parity against pre-migration
      fixtures or a recorded baseline.
- [ ] Re-run the Migration Assistant after its refresh window and investigate
      every remaining deprecated call. Do not switch while a repository-owned
      caller remains or while an unknown caller is still active.
- [ ] Produce the Langfuse migration readiness report with exactly these seven
      rows, each marked `ready`, `changed`, `manual action`, or `blocked`, with
      blocker and next action where applicable: project access;
      SDK/instrumentation; trace evaluators; dataset evaluators; direct APIs;
      exports; verification/rollback.
- [ ] Obtain owner approval for the Cloud project switch, select v4 in the
      Migration Assistant, then verify representative production reads,
      traces, experiments, scores, dashboards, and rollback evidence.
- [ ] Verify the exact Git target, commit the scoped changes intentionally, and
      push the explicit remote and branch.

## Validation

- Run focused public-API client, experiment export, cost-estimation, retry,
  serialization, and service-registration tests, followed by the complete
  affected test projects using the repository-prescribed TUnit commands.
- Run active repository skill validation for every modified skill and exercise
  its maintained Langfuse API path without exposing credentials.
- Compare one pre-migration and one v4 smoke export for item identity, trace
  grouping, observations, inputs/outputs, expected outputs, typed scores,
  tokens, and costs.
- Record the target project, Migration Assistant evidence, smoke dataset/run/
  trace identifiers, validation commands, exact pushed commit, and post-switch
  verification without recording private payloads or secrets.

## Complete when

- The confirmed project has no unexplained deprecated API calls after the
  Migration Assistant refresh window, and repository search plus tests find no
  active legacy read path.
- The v4 smoke path preserves trace/session attribution, experiment exports,
  score interpretation, usage, cost, and report behavior.
- Evaluators and integrations are verified in the project UI, not inferred
  solely from code or the earlier green dashboard checks.
- The seven-row readiness report is accepted, the project is switched to v4,
  and representative post-switch production behavior is verified with a
  documented rollback boundary.
