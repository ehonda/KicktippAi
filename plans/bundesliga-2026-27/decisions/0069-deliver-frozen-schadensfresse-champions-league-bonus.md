# ADR-0069: Deliver the frozen Schadensfresse Champions-League bonus exception

- Status: Accepted
- Date: 2026-09-05

## Context

Three authenticated, open Schadensfresse Champions-League bonus questions close at `2026-09-08T16:45:00Z`. The accepted P1-10 route is deliberately deferred because it imports global typed identity/context work; the normal Bundesliga bonus route requires publications that this narrow repair must not read. The reviewed current-form evidence is snapshot `4299e240f7909f24c2b7f4d2eeeaef564beaea4a3539fe87984867fa890205b0`, while the reviewed implementation contract is `6ab7a3548dd5e56e2dcb360de8bc2dde9a9e902fc291cb0955da5f10fff4020a`.

The reviewed prompt was published on `2026-09-05T00:00:32.001Z` in Langfuse project `cmm5exwe200ctad07o35ezcn9` as prompt ID `9b8f97a4-6993-46be-99ec-447312433b8a`, version 1 with `production` membership. Independent normalized readback produced `70819641df57c8979f1c11dfe4e3df920bca96defdbef29646fd22247dfd0ee2`. This completed prompt-publication gate is distinct from the still-gated model, Firestore, Kicktipp, and workflow actions.

## Decision

Implement only `schadensfresse-champions-league-bonus-context-free-v1` for IDs `1662326752`, `1662326753`, and `1662326754`: target-owned, manual-only, exact 1/4/1 answers, zero documents/tokens, `gpt-5.6-sol`/`xhigh`/`10000`, and Flex-first with one Standard fallback per question. It has a strict local form adapter, a specialized exclusive Firestore manifest, exact lineage filtering, and exact initial/pre-POST/final form reads. The immutable hosted v1/`production` prompt is the sole authority; only its normalized-byte-identical dedicated mirror may serve an availability outage.

## Alternatives considered

- **Use the ordinary Bundesliga bonus route:** Rejected because it reads unrelated context/publications and its generic Kicktipp methods do not provide the required strict form or replacement semantics.
- **Import deferred P1-10 typing:** Rejected because it expands this deadline-critical repair into the separate primary-routing program.

## Consequences

- The exception is fail-closed on question/form/prompt/lineage drift and remains manual-only.
- It neither changes ordinary bonus branches nor migrates/deletes historical predictions; rollback is a code/workflow revert.
- The immutable prompt is already published; future production execution still requires root review/CI and explicit bounded activation of the model, Firestore, Kicktipp, and workflow gates.

## Affected tasks

- [P1-15](../tasks/p1-15-schadensfresse-champions-league-bonus.md)
- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)

## Supersedes

None. This is a narrow exception to ADR-0058, not a general replacement.
