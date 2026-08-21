# ADR-0024: Select bonus context by competition and question

- Status: Accepted
- Date: 2026-08-21

## Context

The existing KPI context provider reads every generic latest KPI document for a community and then applies WM26-era name and text filters. That path can mix `team-data`, `manager-data`, `fifa-rankings`, and `lineups` into Bundesliga bonus prompts. It also cannot load the reserved per-team Bundesliga roster documents because those are context documents, and generic latest reads are not a coherent read boundary for the roster or Club Elo publication sets under ADR-0014.

Bundesliga bonus questions need a small useful baseline for every prediction. Top-scorer and coach questions additionally need current membership facts, but loading `team-rosters` or every roster as an implicit fallback would make selection oversized and hide unmapped question identities. P0-16 will add the final category table, multilingual matching, budgets, and trace-visible exclusion reasons; P0-13 must first establish the safe competition and document boundary on which that work operates.

## Decision

The resolved competition is bound to `IKpiContextProvider` construction and determines the complete selection branch. A Bundesliga 2026/27 call never executes the WM26 or generic latest-KPI branch. WM26 retains its existing `fifa-rankings` baseline and exact top-scorer-team `lineups` behavior.

Every Bundesliga bonus question receives exactly this ordered aggregate baseline:

1. KPI document `club-elo-rankings` from the current valid `club-elo` publication head;
2. KPI document `team-squad-summary` from the current valid `rosters` publication head.

The provider loads each publication as head -> immutable snapshot -> exact payload versions through `IDocumentPublicationRepository`. It validates both with their canonical semantic reconstruction contract before selecting any content. A missing, corrupt, incomplete, or wrong-scope publication fails the question with an actionable collection command; the provider never falls back to a generic latest version of a reserved name.

The bonus selector receives the full `BonusQuestion`, not only its text. The known Bundesliga top-scorer-team wording and coach/top-scorer signals may add per-team context documents named `roster-{manifestSlug}`. A roster is targeted only when an exact manifest team identity or an exact current roster-member identity occurs in the question text or an option. Player identities target top-scorer questions; coach identities target coach questions. Targeted rosters are ordered by manifest slug. A roster-relevant question with no exact target fails actionably rather than loading `team-rosters` or every roster as a fallback.

The Bundesliga branch has an explicit allowlist consisting only of the two baseline aggregates and those exact targeted roster names. It cannot select transfer documents, `team-rosters`, `team-data`, `manager-data`, `fifa-rankings`, `lineups`, or any unrelated stored document. Generic KPI enumeration remains available only to the preserved non-Bundesliga branch and diagnostics that do not supply Bundesliga prompt context.

P0-16 may refine question categories, language variants, document/token budgets, and selection telemetry. It must preserve the competition split, headed publication reads, aggregate baseline, exact roster targeting, fail-closed behavior, and live allowlist established here.

## Alternatives considered

- **Filter all latest KPI documents by name:** Rejected because roster context lives in the context collection and reserved latest reads can straddle or bypass a publication head.
- **Use `team-rosters` for every Bundesliga bonus question:** Rejected because it is unnecessarily large and duplicates the compact squad summary.
- **Load all 18 rosters for any top-scorer or coach wording:** Rejected because it hides unmapped identities and makes an unbounded fallback part of the launch contract.
- **Reuse WM26 `fifa-rankings`, `lineups`, and manager/team filters:** Rejected because those names and semantics belong to a different competition.

## Consequences

- The context-provider factory must provide both competition-scoped generic KPI and document-publication repositories.
- Bundesliga prompt content uses coherent reserved snapshots and fails before a model call when required context is unavailable.
- The full question and option set becomes available to later P0-16 categorization without changing the provider boundary again.
- Existing WM26 prompt selection remains independent and regression-testable.

## Affected tasks

- [P0-13](../tasks/p0-13-bonus-context-baseline.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-16](../tasks/p0-16-question-aware-bonus-context.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)

## Supersedes

None.
