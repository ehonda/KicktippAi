# ADR-0012: Make matchday completion competition aware

- Status: Accepted
- Date: 2026-08-16

## Context

The match-outcome repository decided Bundesliga completeness by comparing the repository competition with the historical `bundesliga-2025-26` identifier. Every other competition, including the live Bundesliga 2026/27 identifier, was treated as variable-size and complete whenever all currently stored rows were complete. Consequently, one through eight stored completed fixtures could incorrectly close a live Bundesliga matchday.

Completion also relied on stored row count rather than stable Kicktipp fixture identity. Duplicate, blank, or surplus rows could therefore hide incomplete or corrupt collection. WM26 genuinely has variable matchday sizes and must retain that behavior without weakening Bundesliga validation.

## Decision

Core owns the supported competition completion metadata and evaluates completion from `tippSpielId` plus outcome availability.

For `bundesliga-2026-27`, a matchday is complete only when:

- exactly nine rows exist, not at least nine;
- every row has a nonblank `tippSpielId`;
- all nine IDs are distinct using ordinal comparison; and
- every outcome has `Completed` availability.

Zero through eight rows, ten or more rows, any pending row, any duplicate ID, and any null, empty, or whitespace ID are incomplete.

For `fifa-world-cup-2026`, matchday size remains variable. A matchday is complete only when it is nonempty, every `tippSpielId` is nonblank and ordinal-distinct, and every outcome is completed.

Every other nonblank competition, including the historical Bundesliga 2025/26 identifier, throws `NotSupportedException` when its completion policy is resolved. Missing competition remains an argument error. The collection service resolves the policy before creating its Kicktipp client or repository, and the Firebase repository resolves it in its constructor, so unsupported competitions fail before network or Firestore queries.

`FirebaseMatchOutcomeRepository.GetIncompleteMatchdaysAsync` remains responsible for enumerating absent matchdays from one through the current matchday, but delegates each stored matchday decision to the Core policy. There is no second expected-count constant in the service or adapter.

This decision does not alter P0-02 storage scoping. Match outcomes remain partitioned by explicit competition and community and keep competition-prefixed Kicktipp IDs. Prediction document IDs remain GUIDs.

## Alternatives considered

- **Change the old season comparison to the new season ID:** Rejected because it would preserve duplicated provider logic and would not validate stable fixture identity.
- **Treat nine or more completed Bundesliga rows as complete:** Rejected because extra rows indicate duplicate or corrupt membership and must fail closed.
- **Require nine fixtures for WM26:** Rejected because World Cup matchdays have variable fixture counts.
- **Let unknown competitions use the variable rule:** Rejected because new competitions require an explicit reviewed completion contract.

## Consequences

- Eight completed Bundesliga fixtures remain collectible rather than being silently considered complete.
- Duplicate, blank, and surplus persisted outcomes surface as incomplete and are rechecked.
- WM26 keeps its valid variable-size behavior with stronger identity validation.
- Onboarding a new competition requires adding explicit Core metadata and tests.

## Affected tasks

- [P0-03](../tasks/p0-03-matchday-completion.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)

## Supersedes

The historical `bundesliga-2025-26` comparison and implicit variable-size fallback in `FirebaseMatchOutcomeRepository`.
