# Langfuse Notes

This document captures repository-specific findings about how Langfuse currently behaves for our tracing setup.

## Versioned Prompt Retrieval

The Langfuse v2 prompt endpoint accepts either a label or an immutable version
selector. It rejects a request that sends both. Repository prompt retrieval
therefore sends only `version` when a numbered version is configured and uses
`label` only for label-resolved lookups. When both an immutable version and a
required promotion label are part of the runtime contract, the provider fetches
by version and then verifies the returned prompt name, exact version, and label
membership.

Name, version, or label drift fails closed and never activates a checked-in
fallback. Ordinary fetch failures retain the visible local-mirror behavior from
ADR-0004. Bundesliga `matchday-dev` and `bonus-dev` are hosted-required
validation paths: the exact v2/`production` or v1/`production` binding must pass
before prediction-service construction, and a fallback cannot pass that rung.

## Metadata Filtering Status

We verified the current behavior against the live Langfuse Cloud project on 2026-03-13.

### Trace-Level Metadata

Recent `matchday` traces do not expose a single top-level `repredictionIndex` field.

Instead, traces currently expose aggregate-style fields such as:

- `repredictionIndices`, e.g. `|0|1|2|`
- `hasRepredictions`

This means trace-level filtering cannot answer questions like `repredictionIndex > 0` for an individual prediction.

### Observation-Level Metadata

Individual `predict-match` observations do expose a top-level metadata field named `repredictionIndex`.

Example shape from the live Langfuse API:

```json
"metadata": {
  "repredictMode": true,
  "model": "o4-mini",
  "matchday": 26,
  "communityContext": "pes-squad",
  "community": "ehonda-ai-arena",
  "homeTeam": "VfB Stuttgart",
  "awayTeam": "RB Leipzig",
  "repredictionIndex": 1,
  "match": "VfB Stuttgart vs RB Leipzig"
}
```

This metadata is emitted by the application via `langfuse.observation.metadata.*` tags in [src/OpenAiIntegration/PredictionTelemetryMetadata.cs](../src/OpenAiIntegration/PredictionTelemetryMetadata.cs).

## Verified Filter Behavior

### What Works

Exact-match filtering on observation metadata works.

Example observations API filter:

```json
[
  {
    "type": "string",
    "column": "name",
    "operator": "=",
    "value": "predict-match"
  },
  {
    "type": "stringObject",
    "column": "metadata",
    "key": "repredictionIndex",
    "operator": "=",
    "value": "1"
  }
]
```

This returns the expected `predict-match` observations.

### What Does Not Work

Numeric comparison on metadata does not currently work in the live project, even though the public docs describe `numberObject` support.

The following filter fails on both traces and observations:

```json
{
  "type": "numberObject",
  "column": "metadata",
  "key": "repredictionIndex",
  "operator": ">",
  "value": 0
}
```

Observed API error:

```text
Invalid filter type 'numberObject' for column 'metadata'. Expected filter type 'stringObject'.
```

In practice, the live Langfuse API currently treats metadata filters as string-based for this project.

## Practical Consequences

- You can filter `predict-match` observations by exact `repredictionIndex` values such as `0`, `1`, or `2`.
- You cannot currently filter by numeric thresholds such as `repredictionIndex > 0`.
- If we need a generic "reprediction" filter in the UI or API, the simplest workaround is to emit an additional derived observation metadata field such as `isReprediction=true`.

## Useful Query Patterns

Observation-level exact match for first repredictions:

```json
[
  {
    "type": "string",
    "column": "name",
    "operator": "=",
    "value": "predict-match"
  },
  {
    "type": "stringObject",
    "column": "metadata",
    "key": "repredictionIndex",
    "operator": "=",
    "value": "1"
  }
]
```

Trace-level filter for traces that include repredictions at all:

```json
[
  {
    "type": "stringObject",
    "column": "metadata",
    "key": "hasRepredictions",
    "operator": "=",
    "value": "true"
  }
]
```

## Notes For Future Investigation

- Re-check this behavior after Langfuse upgrades. The docs currently describe metadata `numberObject` filters, but the live API rejected them during verification.
- If we need threshold-style filtering, prefer adding a derived categorical or boolean field instead of relying on undocumented numeric metadata support.
