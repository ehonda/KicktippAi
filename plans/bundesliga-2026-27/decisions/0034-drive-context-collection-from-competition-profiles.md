# ADR-0034: Drive development context collection from competition profiles

- Status: Accepted
- Date: 2026-08-21

## Context

`collect-context-dev` was a WM26 preset encoded directly in one command. It admitted only the WM26 development community, always invoked the FIFA and national-lineup collectors, and described its target as WM26 even after Bundesliga 2026/27 gained its own strict history, Club Elo, roster, prompt, and document contracts. Reusing that branch for Bundesliga would either make FIFA behavior leak into club competition collection or add another hard-coded competition conditional for every collector.

P0-22 also established an important publication boundary: Bundesliga Kicktipp collection reconstructs played dates before it atomically publishes the complete selected recent/home/away history set. Running a second history apply after Kicktipp would add redundant Firestore reads and a second publication attempt, while splitting the transform from Kicktipp would expose an undated or partial intermediate set.

## Decision

Development collection resolves one typed profile from the exact competition and supported development community. A profile declares, in one immutable contract:

- its ordered collector phases;
- required match-document templates, aggregate context documents, and KPI documents;
- expected team and competition match counts, plus a fixed per-matchday count where one exists;
- season bounds;
- hosted match and bonus prompt names, exact versions when accepted, label, and local fallback model;
- context features such as home/away history, head-to-head history, knockout rules, and the prohibition on transfers; and
- reproducible validation commands.

The Bundesliga profile supports `ehonda-dev-buli-2627` and executes these phases in order: Kicktipp, Bundesliga history played-date reconstruction, Club Elo, and rosters. It requires 18 teams, 306 matches, nine matches per matchday, the match context fixed by P0-12, the `team-rosters` aggregate, and the `club-elo-rankings` plus `team-squad-summary` KPI documents. The profile-owned count is passed into Kicktipp collection; each fetched current or explicit target matchday must contain exactly nine fixtures before provider enumeration or context publication. It enables home/away and head-to-head history and disables FIFA rankings, WM26 national lineups, WM26 date mapping, knockout behavior, and transfers.

The Bundesliga prompt route carries the accepted exact hosted identities from ADR-0033: match version `2` and bonus version `1` under the `production` label. The WM26 route intentionally retains its existing `latest` label with no numeric version; this profile records that label-resolved behavior rather than implying a nonexistent pinned WM26 version.

The Bundesliga history phase is explicit in the ordered profile but has the typed disposition `IncludedInPrevious`. The immediately preceding Kicktipp phase executes that Core collector before its single atomic selected-history publication under ADR-0032. Normal and dry-run reporting name both phases; neither mode performs a second history apply.

The WM26 profile supports `ehonda-dev-wm26` and preserves the existing order and output contracts: Kicktipp, guarded WM26 recent-history date mapping, FIFA rankings, then national lineups. It retains variable matchday size, 48 teams, 104 competition matches, WM26 knockout rules, its existing hosted prompt route, and no Bundesliga history, Club Elo, roster, home/away, head-to-head, or transfer phase. Collector commands are constructed lazily for each directly executed step, so absent Bundesliga sources cannot prevent WM26 profile resolution or collection. Because WM26 is complete, its profile validation commands are deterministic TUnit regressions only; they do not contact live Kicktipp or Firestore.

`collect-context-dev` prints the resolved competition and the full profile contract before invoking any collector. It passes dry-run to every directly executed selected phase. A non-zero or thrown phase fails the command immediately; every remaining selected phase is reported as skipped and is not invoked. Collectors absent from the profile are never constructed as work by the runner. The profile resolver owns collection metadata and consumes the same exact community-to-competition mapping as the shared development shortcuts, rather than inferring a competition from fallback/default logic.

The shared development-community mapping now pairs `ehonda-dev-buli-2627` only with `bundesliga-2026-27` and `ehonda-dev-wm26` only with `fifa-world-cup-2026`; an explicit cross-competition override fails before writable participation settings are created. This mapping is also consumed by the collection-profile resolver. Under ADR-0006 and ADR-0033, the Bundesliga `matchday-dev` and `bonus-dev` shortcuts use only the validation identity `gpt-5.6-luna`, reasoning effort `none`, output cap `10000`, and the appropriate exact hosted prompt version `2` or `1`. WM26 shortcut defaults remain unchanged.

## Alternatives considered

- **Continue adding competition conditionals to `CollectContextDevCommand`:** Rejected because collector order, document requirements, and validation metadata would remain fragmented and difficult for reusable workflows to consume.
- **Run `bundesliga-history apply` after Kicktipp:** Rejected because Kicktipp already executes the strict transform before its atomic history write; a second apply is redundant and dry-run would inspect stored bytes rather than the just-collected candidate set.
- **Split raw Kicktipp history publication from played-date reconstruction:** Rejected because it violates ADR-0032's last-complete-set and atomic-publication contract.
- **Treat WM26 as a default branch for unknown profiles:** Rejected because an unknown competition or development community must fail before collector access.

## Consequences

- Development orchestration is inspectable and testable without invoking live collectors.
- P0-18 can consume the same collector order and validation metadata when adding reusable Bundesliga workflows.
- Adding a competition or changing a collector order requires a reviewed profile and ADR rather than another implicit default.
- The `IncludedInPrevious` disposition is deliberately limited to composition where the previous collector truly executes the phase within the same safe publication boundary.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-18](../tasks/p0-18-base-workflow-support.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

None.
