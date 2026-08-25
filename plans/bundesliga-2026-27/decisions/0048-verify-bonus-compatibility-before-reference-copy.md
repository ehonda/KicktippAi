# ADR-0048: Verify bonus compatibility before reference copying

- Status: Accepted
- Date: 2026-08-25

## Context

The accepted production topology generates the primary Bundesliga configuration
for `pes-squad` and posts that stored prediction through the matching participant
in `ehonda-ai-arena`. Match fixtures already carry enough identity for safe
reuse. Bonus predictions did not: persistence kept the question text and the
selected option IDs/texts, but not the complete source option set. A target
community can use different form option IDs, reordered options, or a changed
question. Reusing selected source IDs without proving semantic compatibility can
therefore post the wrong choice.

P0-21 requires exact normalized question and option compatibility. It also
requires an incompatible arena question to generate independently, rather than
silently treating the reference context as the target context or stopping all
otherwise valid bonus questions.

## Decision

Every new stored bonus prediction and reprediction carries a versioned canonical
compatibility manifest. Version 1 records the normalized question text,
`MaxSelections`, every source option ID and normalized option text, and a
lowercase SHA-256 over the semantic identity. Text normalization uses Unicode
Form KC, trims leading/trailing whitespace, and collapses internal Unicode
whitespace to one ASCII space. Comparison remains ordinal and case- and
accent-sensitive. Options are sorted by normalized text for hashing, so source
order and source IDs do not affect compatibility. Empty or duplicate source
IDs, empty normalized text, duplicate normalized option text, and an invalid
selection limit are rejected.

Cross-community reference copying is the Bundesliga route whose posting
`community` differs from `community_context`. For each target question it reads
the latest exact model-configuration candidate from the source context and
compares the complete canonical identity. A compatible candidate must also pass
the existing immutable resolved-context freshness check. Its selected source
IDs are translated through normalized option text to the target community's
option IDs. The mapped prediction is posted without a model call and without
persisting another model result.

A missing source candidate, legacy or malformed compatibility provenance, or
an ordinary question, `MaxSelections`, or complete-option-set mismatch is never
reused. The same invocation instead makes exactly one independent prediction
for that target question using the posting community as its effective context,
persists the new canonical manifest under that target context, and posts the
target prediction. Legacy rows are not rewritten merely to become copyable.
Invalid target definitions and immutable source/target context safety failures
remain fail closed.

Trace metadata records only payload-safe mode, effective context, counts,
source context and stored Firestore prediction identity, compatibility hashes,
and fixed fallback-reason codes. It does not expose question text, option text,
predictions, prompts, context content, or secrets. Prediction-service creation
is lazy, so a compatible-only copy run neither constructs a model service nor
fetches its hosted prompt.

## Alternatives considered

- **Reuse source option IDs directly:** Rejected because form IDs are owned by
  each Kicktipp community and do not prove semantic equality.
- **Compare only selected option text:** Rejected because missing or additional
  unselected options can change the question contract.
- **Require byte-identical option order and IDs:** Rejected because order and
  form IDs may differ without changing the normalized semantic option set.
- **Fail the complete copy run on every ordinary incompatibility:** Rejected
  because P0-21 explicitly requires incompatible arena questions to generate
  independently. Invalid definitions and immutable-context failures still fail
  closed.
- **Backfill legacy rows:** Rejected because the complete historical source
  option set cannot be reconstructed truthfully from selected options alone.

## Consequences

- Compatible arena copy-posting makes no additional model call and safely maps
  to target option IDs.
- An incompatible question incurs one independently attributable target-context
  model call instead of contaminating or extending the `pes-squad` source row.
- New persistence has complete source option provenance; legacy rows remain
  readable for their original community but are not copyable.
- Copy-candidate reads scan the small source-context bonus set to support
  normalized question matching without relying on raw cross-community form IDs.

## Affected tasks

- [P0-21](../tasks/p0-21-production-activation.md)
- [P0-24](../tasks/p0-24-bonus-copy-post-compatibility.md)

## Supersedes

- [ADR-0039](0039-record-bundesliga-community-and-credential-topology.md),
  only its broader consequence that an incompatible `arena-production-copy`
  question cannot make any model call in the same invocation. The copy branch
  still fails closed: it never reuses an incompatible source prediction and
  never invokes a model with the source `pes-squad` context. This ADR replaces
  that consequence with an explicit independent branch that switches effective
  context to the target posting community before constructing or invoking the
  model service. All other ADR-0039 topology, credential, and owner-gate
  decisions remain in force.
