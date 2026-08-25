# ADR-0044: Select canonical preseason history sources

- Status: Accepted
- Date: 2026-08-25

## Context

ADR-0042 introduced a Bundesliga-only `--full-season` collection mode after a
current-matchday collection left 315 strict-catalog documents absent. Its first
authorized live attempt validated all 34 pages, nine fixtures per page, and all
306 distinct ordered fixtures. It then failed closed at the first matchday-2
fixture, before outcome refresh or any context save.

The matchday-1 Bayern–VfB fixture exposed VfB as the away team, while the
matchday-2 VfB–Köln fixture exposed it as the home team. Both provider paths
produced the global identity `recent-history-vfb.csv`, but their bytes differed.
This is valid fixture-date and role-sensitive Kicktipp behavior behind a global
document name. A full schedule would request every recent identity 34 times and
every home/away identity 17 times. Treating the first or last collision as
canonical would make the frozen ADR-0032/ADR-0041 54-document inventory depend
on incidental enumeration order.

The same attempt also exposed that detailed H2H lookup began on the current
`tippabgabe` page even when a provider represented a future matchday. A complete
preseason catalog requires each ordered fixture's H2H source to begin on that
fixture's exact matchday page.

## Decision

Retain the explicit Bundesliga-only `--full-season` mode and its typed profile
contract. Derive `34` pages from `306 / 9`; reject explicit `--matchdays`,
outcome-only usage, WM26, and unsupported profiles. Perform the following work
serially and in this order:

1. Fetch and validate all matchdays before constructing any context provider.
   Require nine distinct fixtures, the requested matchday on every fixture,
   all 18 manifest clubs exactly once per page, 306 distinct ordered manifest
   pairs, and exact equality with the strict H2H catalog.
2. Create matchday-scoped providers only after the schedule gate passes. Collect
   standings and the exact community rules document once.
3. Select the canonical 54 globally named histories by semantic source, never by
   collision order: collect all 18 recent documents from each team's matchday-1
   fixture; collect every home and away document from that team's earliest
   scheduled fixture in the corresponding role. Require exact equality with the
   accepted 54-name history map. Every accepted selector fixture must lie in
   matchday 1 or 2; a later selector fails closed.
4. Collect all 306 ordered H2H documents separately. Each lookup must begin on
   its fixture's exact `tippabgabe?spieltagIndex=<matchday>` page, never the
   current page. Unselected per-fixture history variants are not requested or
   silently collapsed.
5. Require the exact raw 362-document Kicktipp subset: standings, rules, 54
   selected histories, and 306 H2Hs. Missing, unexpected, WM26, unscoped,
   case-variant, or duplicate identities fail. Duplicate diagnostics may expose
   only the document name, UTF-8 byte counts, and SHA-256 hashes, never content.
6. Only after the complete remote candidate passes, refresh current outcomes and
   run the strict played-date reconstruction. Require all 54 selected names,
   exactly 430 completed frozen-map resolutions, exactly two accepted excluded
   incomplete rows, and zero unresolved, ambiguous, missing, unexpected, or
   conflicting rows.
7. Recheck the exact 362 names after transformation and submit deterministic
   ordinal-name-ordered writes in one
   `SaveContextDocumentsAtomicallyAsync` call. No individual-save fallback is
   allowed; any failure preserves the last complete context set.

The ordinary current/explicit-matchday and WM26 paths remain unchanged. A
full-season schedule, provider, history, or Kicktipp failure still prevents
Club Elo and roster construction in the profile runner, and collection does not
construct model or prediction services.

## Alternatives considered

- **Keep the first observed bytes:** Rejected because loop order would become an
  undocumented source-selection policy.
- **Keep the last observed bytes:** Rejected for the same reason and because a
  later schedule page could silently replace an already validated identity.
- **Reject all repeated global history identities:** Rejected because repeated
  fixture-scoped variants are valid provider behavior; the accepted frozen
  inventory needs one deterministic semantic source for each global identity.
- **Use the current H2H page for future fixtures:** Rejected because it cannot
  prove that the returned document belongs to the ordered fixture being
  collected.

## Consequences

- Full-season collection reproduces the accepted 54-history inventory without
  depending on schedule enumeration collisions.
- Every H2H document is bound to its exact fixture matchday while the strict
  306-name catalog and one-transaction publication boundary remain unchanged.
- Collection performs only 54 selected-history requests instead of 1,224
  fixture-scoped variants, while preserving fail-closed unique-identity checks
  within each semantic phase.
- Live collection remains paused until this decision and implementation are
  independently reviewed, integrated, pushed, and exact-head CI is green.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0042. This decision retains its typed 34-by-9 schedule validation and
single atomic publication boundary while replacing collision-driven provider
enumeration with canonical selected-history sources and exact-matchday H2H.
