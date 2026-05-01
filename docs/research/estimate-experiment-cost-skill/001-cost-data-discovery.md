# 001 - Cost Data Discovery

Status: Placeholder

This file is a temporary placeholder from the initial research scaffold. The user has a different first experiment in mind, and this document will be replaced shortly once that experiment is specified.

## Research Question

Which existing repository artifacts, Orchestrator exports, and Langfuse API resources expose enough token usage and monetary cost data to support an experiment cost estimator without running new predictions?

## Methodology

Start with read-only discovery.

1. Inspect existing experiment docs and generated analysis artifacts, if present.
2. Inspect Orchestrator export contracts to see whether token usage or cost fields are already normalized.
3. Use the `$langfuse` skill and installed `langfuse` CLI for read-only Langfuse API discovery against existing runs.
4. Compare trace, observation, dataset-run, score, and generation-style records for available fields.
5. Document which fields are reliable, which require joins, and which are missing.

Avoid live model calls during this first experiment unless read-only evidence proves insufficient.

## Outcome

Not run yet.

Expected output for this experiment:

- a table of available cost and usage fields by source
- the most reliable query path for existing experiment runs
- gaps in the current export bundle, if any
- recommendation for the first tiny live preflight, only if needed

## Further Research Directions

Likely follow-up questions:

- Can prompt reconstruction estimate input tokens accurately before model execution?
- How well does one-item observed cost predict a larger fixed-slice run?
- How much completion-token variance appears across repeated-match runs?
- Do model, prompt source, and reasoning effort need separate estimator coefficients?
- Should Orchestrator exports be extended to include normalized usage and cost fields?
