# Bundesliga 2026/27 Prediction Authority

This context defines how KicktippAi names the identities and authority used to
decide whether a Bundesliga 2026/27 prediction may be treated as current.

## Language

**Posting Community**:
The Kicktipp community whose live prediction form is read and written.
_Avoid_: Posting target, target community, credential community

**Prediction-source Community**:
The community under which the candidate prediction was generated and stored.
It equals the Posting Community for self-contained generation; for an accepted
copy it may differ and is identified by the Copy Binding.
_Avoid_: Source context, reference context, copy community

**Community Context**:
The community-owned rules and evidence set used to inform generation.
_Avoid_: Posting community, prediction source

**Posting Item Identity**:
The exact identity of a fixture or bonus question in the Posting Community.
_Avoid_: Match text, team pairing, form name

**Source Item Identity**:
The exact identity of the corresponding fixture or bonus question in the
Prediction-source Community.
_Avoid_: Source match, source question text

**Stable Local Item Key**:
The season-, community-, kind-, and Kicktipp-ID-scoped identity that remains
stable when the item's semantic details change.
_Avoid_: Global Kicktipp ID, snapshot ID, team-and-time key

**Snapshot Hash**:
A versioned identity for the complete current semantic state of a Stable Local
Item Key.
_Avoid_: Stable item key, row hash, content checksum

**Identity Seed Generation**:
An immutable, versioned inventory of the supported item identities and route
classifications for one Posting Community.
_Avoid_: Latest seed, shared global seed, mutable manifest

**Copy Binding**:
An immutable one-to-one correspondence between a Posting Item Identity and a
Source Item Identity, including option correspondence for a bonus question.
_Avoid_: Alias, text match, copy permission

**Prediction Authority**:
The complete identity and provenance basis that determines which prediction
may be treated as current for an item.
_Avoid_: Latest prediction, storage partition, model row

**Authority Epoch**:
An immutable generation of Prediction Authority whose current records do not
mix with records from another generation.
_Avoid_: Deployment, migration flag, latest namespace

**Typed Current Prediction**:
A prediction whose item identity, semantic snapshot, generation provenance,
and Authority Epoch all agree with the current Prediction Authority.
_Avoid_: Newest prediction, typed row, non-legacy prediction

**Legacy Row**:
A Bundesliga 2026/27 record that lacks or contradicts the identity or
provenance required by the current Prediction Authority.
_Avoid_: Invalid history, migration candidate, current fallback

**Generation Provenance**:
The immutable identity of the item, evidence, route, prompt, model, service,
and source used when a prediction was produced or copied.
_Avoid_: Current attestation, trace metadata, configuration snapshot
