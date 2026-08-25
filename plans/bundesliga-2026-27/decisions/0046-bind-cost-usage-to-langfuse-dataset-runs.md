# ADR-0046: Bind cost usage to exact Langfuse dataset runs

- Status: Accepted
- Date: 2026-08-25

## Context

The P0-06 Luna five-by-four cost run first completed at parallelism 5, but one
flex request received HTTP 429 and used the standard-tier fallback. The
prescribed retry reused the manifest, run name, and shared run stamp at
parallelism 3 with `--replace-run`. That replacement completed all 20 items on
flex without fallback.

Langfuse replacement removes the previous dataset-run object, but it does not
remove the prediction traces whose session ID equals the reused run name. The
cost collector historically selected generation observations by that session
ID alone. It therefore saw 40 observations for the expected 20: both paid
attempts had the same run name even though only the second dataset run was the
accepted sample.

A timestamp window would be a mutable, approximate attempt selector. Taking
the newest 20 observations or truncating the result would silently manufacture
provenance. Langfuse already exposes an immutable dataset-run ID and dataset-run
item records that link each accepted dataset item to its exact trace.

## Decision

Exact dataset-run-bound cost-usage collection requires a measured group to
bind all of the following:

- the Langfuse dataset ID;
- the exact Langfuse dataset-run ID returned by the accepted execution;
- the prepared manifest;
- the exact expected observation count.

`--manifest` and `--expect` are therefore mandatory whenever a group supplies
an exact dataset-run ID. The collector refuses the exact mode before querying
Langfuse if either is absent.

In this mode the collector lists dataset-run items for the dataset and run
name, retains only items with the requested dataset-run ID, and validates:

- every retained item repeats the expected run name;
- dataset item IDs and trace IDs are present and each is unique;
- the number of immutable item-to-trace links equals the expected observation
  count once ingestion is complete;
- the linked dataset item set exactly equals the prepared manifest item set;
- the manifest's `sampleSize` equals both sets.

Only generation observations whose trace IDs occur in those validated links
are admitted. Each observation must repeat the linked dataset item ID. The
compact usage output embeds the dataset ID, dataset-run ID, run name,
prepared-manifest SHA-256, and prepared-manifest sample size on every admitted
record. The manifest hash and sample size are an inseparable grouped tuple:
both must be present on every record, identical across the accepted group, and
the sample size must equal both the linked-record count and expected count.
`base-row` and `upsert-row` propagate that exact tuple into the authoritative
estimate row. They reject whole omission, partial omission, per-record drift,
or count mismatch instead of stripping provenance.

Run-name-only collection remains supported for older, unambiguous executions.
When its observation count exceeds `--expect`, it fails immediately with exact
dataset-run binding guidance. It never truncates records. Retried or replaced
cost runs must use exact dataset-run binding.

## Alternatives considered

- **Take the newest expected number of traces:** Rejected because ingestion
  order and trace timestamps do not constitute an immutable attempt identity.
- **Delete the old traces:** Rejected because replacement is defined at the
  dataset-run layer and old traces remain useful operational evidence.
- **Run another 20 predictions under a new name:** Rejected for the current
  evidence because the clean parallelism-3 dataset run already has exact
  immutable item-to-trace links; spending again cannot repair collector
  identity.
- **Silently accept 40 observations:** Rejected because it mixes retry attempts,
  changes the authorized sample size, and can mix service tiers.

## Consequences

- A same-name retry can reuse the accepted 20-item dataset run without another
  model call.
- Cost evidence is linked to the exact Langfuse attempt and prepared sample.
- Missing, duplicate, mixed, or drifted links fail before a base estimate row
  can be written.
- Existing one-name/one-attempt collection remains compatible.

## Affected tasks

- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)

## Supersedes

None. This decision refines the experiment-cost evidence contract used by
ADR-0033, ADR-0040, and ADR-0043.
